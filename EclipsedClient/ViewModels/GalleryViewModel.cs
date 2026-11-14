using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EclipsedClient.Services;

namespace EclipsedClient.ViewModels;

internal sealed record MediaCard(Screenshot File, ImageSource? Thumbnail)
{
    public string Name => File.Name;
    public string Date => File.CapturedAt.ToString("MMM d, yyyy · HH:mm");
}

internal sealed class GalleryViewModel : ViewModel
{
    private IReadOnlyList<Screenshot> _files = [];
    private int _generation;
    private const int PageSize = 36;
    private bool _oldestFirst;
    private int _page;
    private string _search = "";
    public string Search
    {
        get => _search;
        set { _search = value; _ = ShowPage(0); }
    }

    private IEnumerable<Screenshot> Filtered => _files.Where(file =>
        file.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool Loaded { get; private set; }
    public MediaCard[] Cards { get; private set; } = [];
    private int _matches;
    public string Summary => _files.Count == 0 ? "No screenshots in Pictures → Roblox." :
        _matches == 0 ? "No matching screenshots." :
        $"{_matches} screenshots · Page {_page + 1} of {(_matches + PageSize - 1) / PageSize}";
    public bool HasNext => (_page + 1) * PageSize < _matches;
    public bool HasPrevious => _page > 0;
    public bool OldestFirst
    {
        get => _oldestFirst;
        set
        {
            if (value == _oldestFirst) return;
            _oldestFirst = value;
            Notify();
            Refresh.Execute(null);
        }
    }
    public ICommand Refresh { get; }
    public ICommand Next { get; }
    public ICommand Previous { get; }
    public ICommand Preview { get; }
    public ICommand Copy { get; }
    public ICommand OpenFolder { get; }

    internal GalleryViewModel(LauncherViewModel shell)
    {
        Refresh = shell.Async(async _ =>
        {
            _files = await Task.Run(() => Screenshots.Find(null, null));
            Loaded = true;
            await ShowPage(0);
        });
        Next = shell.Async(_ => ShowPage(_page + 1));
        Previous = shell.Async(_ => ShowPage(_page - 1));
        Preview = shell.Action(value =>
        {
            var card = (MediaCard)value!;
            using var stream = File.OpenRead(card.File.Path);
            var image = new Image { Source = LauncherViewModel.Decode(stream), Stretch = Stretch.Uniform };
            var scroll = new ScrollViewer { Content = image };
            var dialog = new Window
            {
                Owner = Application.Current.MainWindow, Title = $"Solarious — {card.Name}", Content = scroll,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Width = 950, Height = 680,
                Background = new SolidColorBrush(Color.FromRgb(11, 13, 18)), ShowInTaskbar = false
            };
            dialog.PreviewKeyDown += (_, args) =>
            {
                if (args.Key == Key.Escape) dialog.Close();
            };
            dialog.ShowDialog();
        });
        Copy = shell.Action(value =>
        {
            Screenshots.Copy(((MediaCard)value!).File);
            shell.Report("Screenshot file copied to the clipboard.");
        });
        OpenFolder = shell.Action(value => Screenshots.OpenFolder((value as MediaCard)?.File));
    }

    private async Task ShowPage(int page)
    {
        int generation = ++_generation;
        if (!string.IsNullOrEmpty(Search)) await Task.Delay(150);
        if (generation != _generation) return;
        var matches = Filtered.ToArray();
        int target = Math.Clamp(page, 0, Math.Max(0, (matches.Length - 1) / PageSize));
        var files = (OldestFirst ? matches.Reverse() : matches).Skip(target * PageSize).Take(PageSize).ToArray();
        MediaCard[] cards = await Task.Run(() => files.Select(file =>
        {
            try
            {
                using var stream = File.OpenRead(file.Path);
                return new MediaCard(file, LauncherViewModel.Decode(stream, 360));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or FormatException)
            {
                return new MediaCard(file, null);
            }
        }).ToArray());
        if (generation != _generation) return;
        _page = target;
        _matches = matches.Length;
        Cards = cards;
        Notify(nameof(Cards));
        Notify(nameof(Summary));
        Notify(nameof(HasNext));
        Notify(nameof(HasPrevious));
    }


}
