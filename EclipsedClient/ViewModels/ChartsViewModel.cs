using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using EclipsedClient.Services;

namespace EclipsedClient.ViewModels;

internal sealed record ChartGame(Experience Game, ImageSource? Icon)
{
    public string Title => Game.Name;
    public string Players => Game.Players.ToString("N0") + " playing";
}

internal sealed class ChartsViewModel : ViewModel
{
    private readonly Discovery _discovery;
    private ChartsCache _cache;
    private DateTimeOffset _nextRefresh;
    public string State { get; private set; } = "Loading Roblox charts…";
    public ChartGame[] Games { get; private set; } = [];
    public ICommand Refresh { get; }
    public ICommand Play { get; }
    public ICommand Pin { get; }

    internal ChartsViewModel(LauncherViewModel shell, Discovery discovery, CancellationToken token)
    {
        _discovery = discovery;
        try { _cache = LocalData.Load<ChartsCache>("charts.json"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { _cache = new(); }
        if (_cache.Games.Length > 0) Show("Cached");
        Refresh = new ActionCommand(async _ =>
        {
            _nextRefresh = DateTimeOffset.Now.AddMinutes(5);
            try
            {
                Experience[] entries = await _discovery.ChartsAsync(token);
                var previous = _cache.Games.ToDictionary(game => game.UniverseId);
                _cache = new(DateTimeOffset.Now, entries.Select(game => game.Icon is null &&
                    previous.TryGetValue(game.UniverseId, out var old) ? game with { Icon = old.Icon } : game).ToArray());
                Show("Updated");
                try { LocalData.Save("charts.json", _cache); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception)
            {
                Show(_cache.Games.Length > 0 ? "Cached" : "Charts unavailable");
            }
        });
        Play = shell.Async(value => shell.LaunchPlace(((ChartGame)value!).Game.PlaceId));
        Pin = shell.Action(value =>
        {
            var game = ((ChartGame)value!).Game;
            shell.Home.Pin(game);
            shell.Report($"{game.Name} is in your library.");
        });
    }

    internal void Tick(DateTimeOffset now)
    {
        if (now >= _nextRefresh && Refresh.CanExecute(null)) Refresh.Execute(null);
    }

    internal static ImageSource? Image(byte[]? bytes)
    {
        if (bytes is null) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            return LauncherViewModel.Decode(stream, 150);
        }
        catch (Exception error) when (error is IOException or FormatException or NotSupportedException) { return null; }
    }

    private void Show(string label)
    {
        State = _cache.Games.Length == 0 ? label : $"{label} {_cache.Updated.LocalDateTime:g}";
        Games = _cache.Games.Select(game => new ChartGame(game, Image(game.Icon))).ToArray();
        Notify(nameof(State));
        Notify(nameof(Games));
    }
}
