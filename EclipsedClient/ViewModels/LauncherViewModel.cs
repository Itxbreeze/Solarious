using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using EclipsedClient.Services;

namespace EclipsedClient.ViewModels;

internal abstract class ViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class ActionCommand(Func<object?, Task> run) : ICommand
{
    private bool _busy;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_busy;
    public async void Execute(object? parameter)
    {
        if (_busy) return;
        _busy = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await run(parameter); }
        finally
        {
            _busy = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

internal sealed record Navigation(string Label, ViewModel Page);

internal sealed class LauncherViewModel : ViewModel, IDisposable
{
    private readonly Playtime _playtime = new();
    private readonly Discovery _discovery = new();
    public ChartsViewModel Charts { get; }
    internal event Action? RobloxLaunched;
    internal event Action<bool>? SessionChanged;
    private bool _playing;

    private DateTimeOffset? _launchDeadline;
    private long? _targetPlace;
    public bool LaunchPending { get; private set; }
    public bool CanDismissLaunch => ShowLaunchStatus && !LaunchPending;
    public bool ShowLaunchStatus => LaunchStatus.Length > 0;
    public string LaunchStatus { get; private set; } = "";
    public bool ShowBrowser => _targetPlace is not null && ShowLaunchStatus && !LaunchPending;
    public ICommand OpenRobloxPage { get; }
    public ICommand DismissLaunch { get; }

    internal Task LaunchPlace(string input)
    {
        if (OfficialLauncher.IsPlayerLink(input.Trim()))
            return LaunchRoblox(() => OfficialLauncher.LaunchPlayerLinkAsync(input.Trim(), Settings.AllowUriFallback));
        if (!PlaceLink.TryParse(input, out long id))
        {
            if (!LaunchPending)
            {
                _targetPlace = null;
                SetLaunchStatus("Enter a valid Roblox experience URL or Place ID.", false);
            }
            return Task.CompletedTask;
        }
        return LaunchPlace(id);
    }

    internal Task LaunchPlace(long id) =>
        LaunchRoblox(() => OfficialLauncher.LaunchPlaceAsync(id, Settings.AllowUriFallback), id);

    internal async Task LaunchRoblox(Func<Task> launch, long? targetPlace = null)
    {
        if (LaunchPending) return;
        _targetPlace = targetPlace;
        SetLaunchStatus("Launching official Roblox…", true);
        try
        {
            await launch();
            _launchDeadline = DateTimeOffset.Now.AddSeconds(25);
            SetLaunchStatus(targetPlace is null ? "Waiting for Roblox to open…" : "Waiting for Roblox to join…", true);
            RobloxLaunched?.Invoke();
        }
        catch (Exception error)
        {
            SetLaunchStatus(error.Message, false);
        }
    }

    private void SetLaunchStatus(string text, bool pending)
    {
        LaunchStatus = text;
        LaunchPending = pending;
        Notify(nameof(LaunchStatus));
        Notify(nameof(LaunchPending));
        Notify(nameof(ShowLaunchStatus));
        Notify(nameof(ShowBrowser));
        Notify(nameof(CanDismissLaunch));
    }

    private void ObserveLaunch(DateTimeOffset now)
    {
        if (_launchDeadline is not { } deadline) return;
        if (_targetPlace is long id ? _playtime.Connection?.PlaceId == id : _playtime.PlayingSince is not null)
        {
            _launchDeadline = null;
            SetLaunchStatus("", false);
        }
        else if (now >= deadline)
        {
            _launchDeadline = null;
            SetLaunchStatus(_targetPlace is null ? "Roblox did not report an active player."
                : "Roblox did not confirm the join. You can open the experience page to try again.", false);
        }
    }
    private readonly CancellationTokenSource _lifetime = new();
    private DateTimeOffset _nextPoll;
    private Navigation _selected;

    public HomeViewModel Home { get; }
    public PlaytimeViewModel Analytics { get; } = new();
    public DiscordViewModel Discord { get; }
    public GalleryViewModel Gallery { get; }
    public NetworkViewModel Network { get; }
    public SettingsViewModel Settings { get; }
    public Navigation[] Pages { get; }
    public ICommand Launch { get; }
    public ICommand OpenDiscover { get; }
    public string Status { get; private set; } = "";
    public Navigation Selected
    {
        get => _selected;
        set
        {
            if (value is null || _selected == value) return;
            _selected = value;
            Notify();
            if (value.Page == Gallery && !Gallery.Loaded) Gallery.Refresh.Execute(null);
            if (value.Page == Analytics) RefreshCharts(DateTimeOffset.Now);
        }
    }

    internal LauncherViewModel()
    {
        Home = new(this, _discovery, _lifetime.Token);
        Charts = new(this, _discovery, _lifetime.Token);
        Discord = new(this);
        Gallery = new(this);
        Network = new(this);
        Settings = new(this);
        Pages = [
            new("Home", Home),
            new("Discover", new DiscoverViewModel(Home, Charts)),
            new("Playtime", Analytics),
            new("Captures", Gallery),
            new("Discord", Discord),
            new("Network", Network),
            new("Settings", Settings)
        ];
        _selected = Pages[0];
        OpenDiscover = Action(_ => Selected = Pages.First(page => page.Page is DiscoverViewModel));
        Launch = Async(_ => LaunchRoblox(() => OfficialLauncher.LaunchHomeAsync(Settings.AllowUriFallback)));
        OpenRobloxPage = Action(_ =>
        {
            if (_targetPlace is long id) OfficialLauncher.OpenWeb(OfficialLauncher.PlacePage(id));
        });
        DismissLaunch = Action(_ =>
        {
            _launchDeadline = null;
            SetLaunchStatus("", false);
        });
    }

    internal ICommand Action(Action<object?> run) => Async(value =>
    {
        run(value);
        return Task.CompletedTask;
    });

    internal ICommand Async(Func<object?, Task> run) => new ActionCommand(async value =>
    {
        Report("");
        try { await run(value); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) { Report(error.Message); }
    });

    internal void Report(string status)
    {
        Status = status;
        Notify(nameof(Status));
    }

    internal void Tick()
    {
        var now = DateTimeOffset.Now;
        try
        {
            Charts.Tick(now);
            bool poll = now >= _nextPoll;
            if (poll)
            {
                _nextPoll = now.AddSeconds(3);
                _playtime.Poll(now);
                bool playing = _playtime.PlayingSince is not null;
                if (playing != _playing)
                {
                    _playing = playing;
                    SessionChanged?.Invoke(playing);
                }
                Network.Update(_playtime.Connection);
                ObserveLaunch(now);
                if (Selected.Page == Analytics) RefreshCharts(now);
            }
            Home.UpdateSession(_playtime.PlayingSince, now);
            Discord.Update(_playtime.PlayingSince, Home.TitleFor(_playtime.Connection?.PlaceId), now, poll);
        }
        catch (Exception error) { Report(error.Message); }
    }

    private void RefreshCharts(DateTimeOffset now) => Analytics.Update(_playtime.Summarize(now), Home.TitleFor);

    public void Dispose()
    {
        _lifetime.Cancel();
        Home.Dispose();
        _discovery.Dispose();
        Discord.Dispose();
        _lifetime.Dispose();
    }

    internal static BitmapImage Decode(Stream stream, int width = 0)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = width;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    internal static string Resource(string path)
    {
        using var stream = Application.GetResourceStream(new Uri(path, UriKind.Relative)).Stream;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
