using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

namespace EclipsedClient.Services;

internal sealed record ServerConnection(long PlaceId, IPAddress Address, string? Region);

internal sealed partial class RobloxLogs
{
    private const int InitialTailBytes = 1024 * 1024;
    private string? _path;
    private long _offset;
    private long? _pendingPlace;
    private string? _pendingRegion;
    private ServerConnection? _connection;

    internal void Reset()
    {
        _path = null;
        _offset = 0;
        _pendingPlace = null;
        _pendingRegion = null;
        _connection = null;
    }

    internal ServerConnection? Read(DateTimeOffset playerStarted)
    {
        string directory = Path.Combine(OfficialLauncher.RobloxDirectory, "logs");
        if (!Directory.Exists(directory))
            return null;

        try
        {
            FileInfo? file = new DirectoryInfo(directory).EnumerateFiles("*Player*.log")
                .Where(log => log.CreationTimeUtc >= playerStarted.UtcDateTime.AddSeconds(-15))
                .OrderByDescending(log => log.CreationTimeUtc)
                .FirstOrDefault();
            if (file is null)
                return null;

            using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!string.Equals(_path, file.FullName, StringComparison.OrdinalIgnoreCase) ||
                stream.Length < _offset || stream.Length - _offset > InitialTailBytes)
            {
                Reset();
                _path = file.FullName;
                _offset = Math.Max(0, stream.Length - InitialTailBytes);
            }

            stream.Position = _offset;
            byte[] bytes = new byte[(int)Math.Min(InitialTailBytes, stream.Length - _offset)];
            int read = stream.ReadAtLeast(bytes, bytes.Length, false);
            int lastNewline = bytes.AsSpan(0, read).LastIndexOf((byte)'\n');
            if (lastNewline >= 0)
            {
                foreach (string line in Encoding.UTF8.GetString(bytes, 0, lastNewline + 1).Split('\n'))
                    ReadLine(line);
                _offset += lastNewline + 1;
            }

            return _connection;
        }
        catch (IOException)
        {
            return _connection;
        }
        catch (UnauthorizedAccessException)
        {
            return _connection;
        }
    }

    internal void ReadLine(string line)
    {
        if (line.Contains("[FLog::Output] ! Joining game", StringComparison.Ordinal))
        {
            Match match = PlacePattern().Match(line);
            _pendingPlace = long.TryParse(match.Groups[1].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out long id) && id > 0 ? id : null;
            _pendingRegion = null;
            _connection = null;
        }
        else if (line.Contains("[FLog::Network] Replicator created:", StringComparison.Ordinal) &&
                 _pendingPlace is long place && IPAddress.TryParse(AddressPattern().Match(line).Groups[1].Value, out IPAddress? address))
        {
            _connection = new(place, address, _pendingRegion);
        }
        else if (line.Contains("[FLog::Network] Time to disconnect replication data:", StringComparison.Ordinal) ||
                 line.Contains("[FLog::SingleSurfaceApp] leaveUGCGameInternal", StringComparison.Ordinal))
        {
            _connection = null;
            _pendingPlace = null;
            _pendingRegion = null;
        }
        else if (_pendingPlace is not null && line.Contains("[FLog::Network]", StringComparison.Ordinal))
        {
            Match region = RegionPattern().Match(line);
            if (region.Success)
            {
                _pendingRegion = region.Groups[1].Value;
                if (_connection is not null)
                    _connection = _connection with { Region = _pendingRegion };
            }
        }
    }

    internal static async Task<string> PingAsync(IPAddress address)
    {
        if (!IsPublicIpv4(address))
            throw new InvalidOperationException("The connected address is unavailable for a public ICMP check.");

        using var ping = new Ping();
        PingReply reply = await ping.SendPingAsync(address, 2000);
        return reply.Status == IPStatus.Success
            ? $"ICMP round trip to {address} · {reply.RoundtripTime} ms"
            : $"ICMP to {address} · {reply.Status}. The server may block ICMP.";
    }

    private static bool IsPublicIpv4(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] is > 0 and < 224 and not (10 or 127) &&
            !(bytes[0] == 169 && bytes[1] == 254) &&
            !(bytes[0] == 192 && bytes[1] is 0 or 168) &&
            !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
            !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
            !(bytes[0] == 198 && bytes[1] is 18 or 19);
    }

    [GeneratedRegex(@"! Joining game '[0-9a-f-]{36}' place ([0-9]+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlacePattern();

    [GeneratedRegex(@"serverId:\s*([0-9.]+)\|[0-9]+")]
    private static partial Regex AddressPattern();

    [GeneratedRegex(@"\b(?:serverRegion|datacenterRegion|RCCRegion)\s*[:=]\s*([a-z0-9-]{2,24})\b", RegexOptions.IgnoreCase)]
    private static partial Regex RegionPattern();
}
