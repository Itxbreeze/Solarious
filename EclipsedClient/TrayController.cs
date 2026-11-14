using System.Windows;
using System.Windows.Threading;
using EclipsedClient.ViewModels;
using Forms = System.Windows.Forms;

namespace EclipsedClient;

internal sealed class TrayController : IDisposable
{
    private readonly Window _window;
    private readonly LauncherViewModel _launcher;
    private readonly Forms.NotifyIcon _tray;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly System.Drawing.Icon _icon;
    private readonly DispatcherTimer _startupTimeout;
    private bool _observedSession;

    internal TrayController(Window window, LauncherViewModel launcher)
    {
        _window = window;
        _launcher = launcher;
        using var resource = Application.GetResourceStream(new Uri("Assets/Solarious.ico", UriKind.Relative)).Stream;
        using var loaded = new System.Drawing.Icon(resource);
        _icon = (System.Drawing.Icon)loaded.Clone();
        _menu.Items.Add("Open Eclipsed Client Solarious", null, (_, _) => window.Dispatcher.Invoke(Restore));
        _menu.Items.Add("Exit", null, (_, _) => window.Dispatcher.Invoke(window.Close));
        _tray = new Forms.NotifyIcon { Text = "Eclipsed Client Solarious", Icon = _icon, ContextMenuStrip = _menu };
        _tray.DoubleClick += (_, _) => window.Dispatcher.Invoke(Restore);
        _startupTimeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        _startupTimeout.Tick += (_, _) =>
        {
            _startupTimeout.Stop();
            if (!_observedSession && _tray.Visible)
            {
                Restore();
                launcher.Report("Roblox did not start. Try launching again.");
            }
        };
        window.StateChanged += WindowStateChanged;
        launcher.RobloxLaunched += HideAfterLaunch;
        launcher.SessionChanged += SessionChanged;
    }

    private void HideAfterLaunch()
    {
        if (!_launcher.Settings.MinimizeAfterLaunch || !_window.IsVisible) return;
        HideToTray();
        if (!_observedSession) _startupTimeout.Start();
    }

    private void WindowStateChanged(object? sender, EventArgs e)
    {
        if (_window.WindowState == WindowState.Minimized && _launcher.Settings.MinimizeAfterLaunch)
            HideToTray();
    }

    private void HideToTray()
    {
        _tray.Visible = true;
        _window.Hide();
    }

    private void SessionChanged(bool playing)
    {
        _observedSession = playing;
        _startupTimeout.Stop();
        if (!playing && _tray.Visible)
            Restore();
    }

    private void Restore()
    {
        _startupTimeout.Stop();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Show();
        _window.Activate();
        _tray.Visible = false;
    }

    public void Dispose()
    {
        _startupTimeout.Stop();
        _window.StateChanged -= WindowStateChanged;
        _launcher.RobloxLaunched -= HideAfterLaunch;
        _launcher.SessionChanged -= SessionChanged;
        _tray.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
}
