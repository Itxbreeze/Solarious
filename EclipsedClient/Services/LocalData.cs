using System.IO;
using System.Text.Json;

namespace EclipsedClient.Services;

internal static class LocalData
{
    internal static string DirectoryPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EclipsedClient");

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
internal sealed record LauncherPreferences(bool MinimizeAfterLaunch = false, bool AllowUriFallback = false, string Backdrop = "earthrise")
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
    public string TopText { get; init; } = "Playing via Eclipsed Client";
    public string BottomText { get; init; } = "{game}";
}
