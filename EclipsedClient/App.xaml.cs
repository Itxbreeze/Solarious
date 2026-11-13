using System.Windows;
using EclipsedClient.Services;

namespace EclipsedClient;

public partial class App : Application
{
    private Mutex? _dashboardLock;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string? protocol = e.Args.Length == 1 ? e.Args[0] : null;
        if (e.Args.Length > 0 && (protocol is null || !OfficialLauncher.IsPlayerLink(protocol)))
        {
            Shutdown(2);
            return;
        }

        try
        {
            if (protocol is not null)
                await OfficialLauncher.LaunchPlayerLinkAsync(protocol);

            _dashboardLock = new Mutex(true, @"Local\EclipsedClient.Dashboard", out bool firstInstance);
            if (!firstInstance)
            {
                Shutdown();
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            if (protocol is not null)
                window.WindowState = WindowState.Minimized;
            window.Show();
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Eclipsed Client", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dashboardLock?.Dispose();
        base.OnExit(e);
    }
}
