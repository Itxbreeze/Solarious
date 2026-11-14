using System.Windows.Input;
using EclipsedClient.Services;

namespace EclipsedClient.ViewModels;

internal sealed class DiscordViewModel : ViewModel, IDisposable
{
    private readonly DiscordPresence _presence = new();
    private PresenceSettings _saved = LocalData.Load<PresenceSettings>("settings.json");
    private PresenceSettings _draft;
    private DateTimeOffset? _started;
    private TimeSpan _elapsed;
    private string? _game;
    public string Top { get => _draft.TopText; set => Edit(_draft with { TopText = value }); }
    public string Bottom { get => _draft.BottomText; set => Edit(_draft with { BottomText = value }); }
    public string Username { get => _draft.Username; set => Edit(_draft with { Username = value }); }
    public string ApplicationId { get => _draft.ApplicationId; set => Edit(_draft with { ApplicationId = value }); }
    public bool Enabled { get => _draft.Enabled; set => Edit(_draft with { Enabled = value }); }
    public bool HideGame { get => _draft.HideGame; set => Edit(_draft with { HideGame = value }); }
    public bool ShowTimer { get => _draft.ShowTimer; set => Edit(_draft with { ShowTimer = value }); }
    public string PreviewTop => DiscordPresence.Render(Top, _draft, _game, _elapsed) ?? "Playing via Solarious";
    public string? PreviewBottom => DiscordPresence.Render(Bottom, _draft, _game, _elapsed);
    public string PreviewTimer => ShowTimer ? HomeViewModel.Elapsed(_elapsed) + " elapsed" : "";
    public string PreviewStatus => !Enabled ? "Preview · sharing off" :
        _started is null ? "Preview · waiting for Roblox" : "Preview · Roblox running";
    public ICommand Save { get; }

    internal DiscordViewModel(LauncherViewModel shell)
    {
        _draft = _saved;
        Save = shell.Action(_ =>
        {
            var next = _draft with { ApplicationId = ApplicationId.Trim(), Username = Username.Trim() };
            if (next.Enabled && (next.ApplicationId.Length == 0 || !next.ApplicationId.All(char.IsAsciiDigit) ||
                !ulong.TryParse(next.ApplicationId, out ulong id) || id == 0))
                throw new ArgumentException("Enter the numeric Discord application ID.");
            LocalData.Save("settings.json", next);
            _saved = next;
            Edit(next);
            _presence.Update(_saved, _started, _game, DateTimeOffset.Now);
            shell.Report("Discord settings saved.");
        });
    }

    internal void Update(DateTimeOffset? started, string? game, DateTimeOffset now, bool publish)
    {
        _started = started;
        _game = game;
        _elapsed = started is { } time && now > time ? now - time : TimeSpan.Zero;
        Preview();
        if (publish) _presence.Update(_saved, started, game, now);
    }

    private void Edit(PresenceSettings value)
    {
        _draft = value;
        Notify(string.Empty);
    }

    private void Preview()
    {
        Notify(nameof(PreviewTop));
        Notify(nameof(PreviewBottom));
        Notify(nameof(PreviewTimer));
        Notify(nameof(PreviewStatus));
    }

    public void Dispose() => _presence.Dispose();
}
