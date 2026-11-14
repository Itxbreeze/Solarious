using System.Text;
using System.Text.RegularExpressions;
using DiscordRPC;

namespace EclipsedClient.Services;

internal sealed partial class DiscordPresence : IDisposable
{
    private DiscordRpcClient? _client;
    private string? _applicationId;
    private (string? Details, string? State, DateTimeOffset? Started)? _last;

    internal void Update(PresenceSettings settings, DateTimeOffset? started, string? game, DateTimeOffset now)
    {
        if (!settings.Enabled || started is null)
        {
            Dispose();
            return;
        }

        if (_applicationId != settings.ApplicationId)
        {
            Dispose();
            _client = new DiscordRpcClient(settings.ApplicationId);
            if (!_client.Initialize())
            {
                Dispose();
                return;
            }
            _applicationId = settings.ApplicationId;
        }

        TimeSpan elapsed = now > started.Value ? now - started.Value : TimeSpan.Zero;
        string? details = Render(settings.TopText, settings, game, elapsed) ?? "Playing via Solarious";
        string? state = Render(settings.BottomText, settings, game, elapsed);
        DateTimeOffset? timestamp = settings.ShowTimer ? started : null;
        var next = (details, state, timestamp);
        if (_last == next)
            return;

        _client!.SetPresence(new RichPresence
        {
            Details = details,
            State = state,
            Timestamps = timestamp is { } start ? new Timestamps { Start = start.UtcDateTime } : null
        });
        _last = next;
    }

    internal static string? Render(string template, PresenceSettings settings, string? game, TimeSpan elapsed)
    {
        // Privacy mode suppresses both custom lines so literal game names cannot leak.
        if (settings.HideGame)
            return null;

        string rendered = Placeholder().Replace(template, match => match.Value.ToLowerInvariant() switch
        {
            "{game}" => game ?? "Roblox",
            "{username}" => settings.Username,
            "{elapsed}" => settings.ShowTimer ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes:00}m" : "",
            _ => match.Value
        }).Trim();

        var text = new StringBuilder();
        int bytes = 0;
        foreach (Rune rune in rendered.EnumerateRunes())
        {
            if (Rune.IsControl(rune))
                continue;
            bytes += rune.Utf8SequenceLength;
            if (bytes > 128)
                break;
            text.Append(rune);
        }

        return text.Length == 0 ? null : text.ToString();
    }

    [GeneratedRegex(@"\{(?:game|username|elapsed)\}", RegexOptions.IgnoreCase)]
    private static partial Regex Placeholder();

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
        _applicationId = null;
        _last = null;
    }
}
