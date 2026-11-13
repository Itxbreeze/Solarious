using System.Windows.Input;
using System.Windows.Media;
using EclipsedClient.Services;

namespace EclipsedClient.ViewModels;

internal sealed class PlaceEntryViewModel : ViewModel, IDisposable
{
    private readonly Discovery _discovery;
    private CancellationTokenSource? _lookup;
    private string _input = "";
    private bool _disposed;
    public Experience? Experience { get; private set; }
    public string Caption { get; private set; } = "";
    public ImageSource? Icon { get; private set; }
    public ICommand Play { get; }
    public string Input
    {
        get => _input;
        set
        {
            if (_input == value || _disposed) return;
            _input = value;
            _ = ResolveAsync();
        }
    }

    internal PlaceEntryViewModel(LauncherViewModel shell, Discovery discovery)
    {
        _discovery = discovery;
        Play = shell.Async(_ => shell.LaunchPlace(Input));
    }

    private async Task ResolveAsync()
    {
        _lookup?.Cancel();
        using var request = new CancellationTokenSource();
        _lookup = request;
        Experience = null;
        Icon = null;
        Caption = "";
        Notify(string.Empty);
        try
        {
            if (!PlaceLink.TryParse(Input, out long id)) return;
            await Task.Delay(350, request.Token);
            Caption = "Finding experience…";
            Notify(nameof(Caption));
            Experience? game = await _discovery.LookupAsync(id, request.Token);
            if (request.IsCancellationRequested) return;
            Experience = game;
            Caption = game?.Name ?? "Experience details unavailable";
            Icon = ChartsViewModel.Image(game?.Icon);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!request.IsCancellationRequested) Caption = "Experience details unavailable";
        }
        finally
        {
            if (ReferenceEquals(_lookup, request))
            {
                _lookup = null;
                Notify(string.Empty);
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _lookup?.Cancel();
    }
}
