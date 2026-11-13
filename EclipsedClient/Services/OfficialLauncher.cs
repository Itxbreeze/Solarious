using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EclipsedClient.Services;

internal static class OfficialLauncher
{
    internal static string RobloxDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");

    internal static bool IsPlayerLink(string value) =>
        value.Length > "roblox-player:".Length && value.Length < 30_000 &&
        value.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase) &&
        !value.Any(char.IsControl) && !value.Contains('"');

    internal static Task LaunchPlayerLinkAsync(string link, bool allowFallback = false)
    {
        if (!IsPlayerLink(link))
            throw new ArgumentException("Enter an official roblox-player: link.");
        return Task.Run(() => StartDirect(link, link, preferLauncher: true, allowFallback));
    }

    internal static Task LaunchHomeAsync(bool allowFallback = false) =>
        LaunchPlayerLinkAsync("roblox-player:1+launchmode:app", allowFallback);

    internal static Task LaunchPlaceAsync(long placeId, bool allowFallback = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(placeId);
        string id = placeId.ToString(CultureInfo.InvariantCulture);
        string deepLink = $"roblox://experiences/start?placeId={id}";
        string endpoint = Uri.EscapeDataString($"https://www.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&placeId={id}");
        string legacy = $"roblox-player:1+launchmode:play+placelauncherurl:{endpoint}";
        return Task.Run(() => StartDirect(deepLink, legacy, preferLauncher: false, allowFallback));
    }

    internal static Task JoinUserAsync(long id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        string link = FormattableString.Invariant($"roblox://experiences/start?userId={id}");
        return Task.Run(() => StartDirect(link, link, preferLauncher: false, allowFallback: false));
    }

    internal static string DescribeInstallation()
    {
        string root = Path.Combine(RobloxDirectory, "Versions");
        if (Directory.Exists(root))
        {
            foreach (var folder in new DirectoryInfo(root).EnumerateDirectories("version-*").OrderByDescending(item => item.LastWriteTimeUtc))
            {
                if (folder.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                foreach (string name in new[] { "RobloxPlayerBeta.exe", "RobloxPlayerLauncher.exe" })
                {
                    string path = Path.Combine(folder.FullName, name);
                    if (File.Exists(path) && !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) && IsSignedByRoblox(path))
                        {
                        var version = FileVersionInfo.GetVersionInfo(path);
                        return $"Installed · {version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}.{version.FilePrivatePart}";
                    }
                }
            }
        }
        return "Official desktop player not detected";
    }

    internal static Uri PlacePage(long placeId) =>
        new($"https://www.roblox.com/games/{placeId.ToString(CultureInfo.InvariantCulture)}");

    internal static void OpenWeb(Uri uri) =>
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });

    private static void StartDirect(string playerLink, string launcherLink, bool preferLauncher, bool allowFallback)
    {
        string root = Path.Combine(RobloxDirectory, "Versions");
        DirectoryInfo[] versions = !Directory.Exists(root) ? [] : new DirectoryInfo(root).EnumerateDirectories("version-*")
            .Where(directory => !directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .ThenByDescending(directory => directory.Name, StringComparer.Ordinal)
            .ToArray();

        bool found = false;
        foreach (DirectoryInfo directory in versions)
        {
            string launcher = Path.Combine(directory.FullName, "RobloxPlayerLauncher.exe");
            string player = Path.Combine(directory.FullName, "RobloxPlayerBeta.exe");
            string[] candidates = preferLauncher ? [launcher, player] : [player, launcher];
            foreach (string path in candidates)
            {
                if (!File.Exists(path) || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) || !IsSignedByRoblox(path))
                    continue;

                found = true;
                var start = new ProcessStartInfo(path)
                {
                    UseShellExecute = false,
                    WorkingDirectory = directory.FullName
                };
                start.ArgumentList.Add(path == launcher ? launcherLink : playerLink);
                try
                {
                    using var process = Process.Start(start);
                    if (process is not null)
                        return;
                }
                catch (Win32Exception) { }
            }
        }

        if (allowFallback && !ProtocolRegistration.IsRegistered)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(playerLink) { UseShellExecute = true });
                return;
            }
            catch (Win32Exception) { }
        }

        if (found)
            throw new InvalidOperationException("Windows could not start the official Roblox player.");
        throw MissingPlayer();
    }

    private static FileNotFoundException MissingPlayer() =>
        new("The signed Roblox desktop player was not found in %LocalAppData%\\Roblox\\Versions. Install the official Windows player from roblox.com.");

    private static bool IsSignedByRoblox(string path)
    {
        try
        {
#pragma warning disable SYSLIB0057 // Read the Authenticode signer from a PE file.
            using X509Certificate rawCertificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(rawCertificate.GetRawCertData());
            if (!certificate.GetNameInfo(X509NameType.SimpleName, false)
                    .Equals("Roblox Corporation", StringComparison.OrdinalIgnoreCase))
                return false;

            var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
            IntPtr filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
            try
            {
                Marshal.StructureToPtr(file, filePointer, false);
                var data = new TrustData
                {
                    Size = (uint)Marshal.SizeOf<TrustData>(),
                    UiChoice = 2,
                    UnionChoice = 1,
                    File = filePointer,
                    ProviderFlags = 0x1000 // Cache-only URL retrieval, so validation makes no network request.
                };
                Guid action = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
                return WinVerifyTrust(new IntPtr(-1), ref action, ref data) == 0;
            }
            finally
            {
                Marshal.DestroyStructure<TrustFile>(filePointer);
                Marshal.FreeHGlobal(filePointer);
            }
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile
    {
        internal uint Size;
        internal string Path;
        internal IntPtr Handle;
        internal IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        internal uint Size;
        internal IntPtr PolicyCallbackData;
        internal IntPtr ClientData;
        internal uint UiChoice;
        internal uint RevocationChecks;
        internal uint UnionChoice;
        internal IntPtr File;
        internal uint StateAction;
        internal IntPtr StateData;
        internal IntPtr UrlReference;
        internal uint ProviderFlags;
        internal uint UiContext;
        internal IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}
