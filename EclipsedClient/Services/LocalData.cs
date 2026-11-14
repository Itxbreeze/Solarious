using System.IO;
using System.Text.Json;

namespace EclipsedClient.Services;

internal static class LocalData
{
    internal static string DirectoryPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Solarious");

    internal static IReadOnlyList<string> Files { get; } =
        Array.AsReadOnly(new[] { "profiles.json", "stats.json", "launcher.json", "settings.json", "charts.json" });

    private static bool _cleared;

    internal static void Migrate()
    {
        if (Directory.Exists(DirectoryPath)) return;

        string previous = Path.Combine(Path.GetDirectoryName(DirectoryPath)!, "EclipsedClient");
        if (!Directory.Exists(previous)) return;

        string staging = DirectoryPath + ".migration-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            foreach (string name in Files)
            {
                string source = Path.Combine(previous, name);
                if (File.Exists(source)) File.Copy(source, Path.Combine(staging, name));
            }
            Directory.Move(staging, DirectoryPath);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                foreach (string name in Files) File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
    }

    internal static void Clear()
    {
        // Retain the directory so the previous edition cannot be imported again.
        Directory.CreateDirectory(DirectoryPath);
        if (File.GetAttributes(DirectoryPath).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("Solarious cannot clear files through a redirected data folder.");

        foreach (string name in Files)
        {
            string path = Path.Combine(DirectoryPath, name);
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
        _cleared = true;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    internal static T Load<T>(string name) where T : new()
    {
        string path = Path.Combine(DirectoryPath, name);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) ?? new T()
            : new T();
    }

    internal static void Save<T>(string name, T value)
    {
        if (_cleared) return;
        Directory.CreateDirectory(DirectoryPath);
        string path = Path.Combine(DirectoryPath, name);
        string temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value, Json);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }
}

internal sealed record Favorite(long PlaceId, string Title, string Notes, string Accent, byte[]? Icon = null, bool Pinned = false);
internal sealed record LauncherPreferences(bool MinimizeAfterLaunch = false, bool AllowUriFallback = false, string Backdrop = "solar-corona")
{
    public LauncherPreferences() : this(false) { }
}
internal sealed record Bookmark(long Id, string Username, string DisplayName);
internal sealed record ProfileData
{
    public List<Favorite> Favorites { get; init; } = [];
    public List<Bookmark> Bookmarks { get; init; } = [];
}

internal sealed record PresenceSettings
{
    public bool Enabled { get; init; }
    public string ApplicationId { get; init; } = "";
    public string Username { get; init; } = "";
    public bool HideGame { get; init; } = true;
    public bool ShowTimer { get; init; } = true;
    public string TopText { get; init; } = "Playing via Solarious";
    public string BottomText { get; init; } = "{game}";
}
