using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace EclipsedClient.Services;

internal sealed record Screenshot(string Path, string Name, DateTime CapturedAt);

internal static class Screenshots
{
    internal static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Roblox");

    internal static IReadOnlyList<Screenshot> Find(DateOnly? from, DateOnly? through)
    {
        if (!Directory.Exists(Folder))
            return [];

        return new DirectoryInfo(Folder).EnumerateFiles("*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 3,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System
        })
        .Where(file => file.Extension.ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")
        .Select(file => new Screenshot(file.FullName, file.Name, file.LastWriteTime))
        .Where(file => (from is null || DateOnly.FromDateTime(file.CapturedAt) >= from) &&
                       (through is null || DateOnly.FromDateTime(file.CapturedAt) <= through))
        .OrderByDescending(file => file.CapturedAt)
        .ToArray();
    }

    internal static void OpenFolder(Screenshot? screenshot)
    {
        string path = screenshot is null ? Folder : Path.GetDirectoryName(screenshot.Path)!;
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException("Roblox has not saved any screenshots to Pictures\\Roblox yet.");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        start.ArgumentList.Add(path);
        Process.Start(start)?.Dispose();
    }

    internal static BitmapImage LoadPreview(Screenshot screenshot)
    {
        using var stream = File.OpenRead(screenshot.Path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 720;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    internal static void Copy(Screenshot screenshot)
    {
        var files = new System.Collections.Specialized.StringCollection { screenshot.Path };
        Clipboard.SetFileDropList(files);
    }
}
