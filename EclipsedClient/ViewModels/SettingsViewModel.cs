using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using EclipsedClient.Services;

namespace EclipsedClient.ViewModels;

internal sealed class SettingsViewModel : ViewModel
{
    private LauncherPreferences _preferences = LocalData.Load<LauncherPreferences>("launcher.json");
    private static readonly (string Id, string Title, string Credit)[] Backdrops =
    [
        ("solar-corona", "Solar Eclipse Corona", "NASA / Keegan Barber"),
        ("earthrise", "Apollo Earthrise", "NASA / Apollo 11"),
        ("hadley-apennine", "Lunar Surface", "NASA / Shawn Quinn")
    ];

    private int BackdropIndex => Math.Max(0, Array.FindIndex(Backdrops, backdrop => backdrop.Id == _preferences.Backdrop));
    public Uri BackdropImage => new($"pack://application:,,,/Assets/{Backdrops[BackdropIndex].Id}.jpg");
    public string BackdropTitle => Backdrops[BackdropIndex].Title;
    public Rect BackdropCrop => Backdrops[BackdropIndex].Id == "earthrise" ? new Rect(0, 0, 1, .80) : new Rect(0, 0, 1, 1);
    public string BackdropCredit => Backdrops[BackdropIndex].Credit;
    public ICommand NextBackdrop { get; }
    public ICommand PreviousBackdrop { get; }

    private void ChangeBackdrop(int direction)
    {
        int index = (BackdropIndex + direction + Backdrops.Length) % Backdrops.Length;
        var next = _preferences with { Backdrop = Backdrops[index].Id };
        LocalData.Save("launcher.json", next);
        _preferences = next;
        Notify(nameof(BackdropImage));
        Notify(nameof(BackdropCrop));
        Notify(nameof(BackdropTitle));
        Notify(nameof(BackdropCredit));
    }
    public bool MinimizeAfterLaunch
    {
        get => _preferences.MinimizeAfterLaunch;
        set
        {
            if (value == _preferences.MinimizeAfterLaunch) return;
            try
            {
                var next = _preferences with { MinimizeAfterLaunch = value };
                LocalData.Save("launcher.json", next);
                _preferences = next;
            }
            catch (Exception error) { _shell.Report(error.Message); }
            Notify();
        }
    }
    public bool AllowUriFallback
    {
        get => _preferences.AllowUriFallback;
        set
        {
            if (value == _preferences.AllowUriFallback) return;
            try
            {
                var next = _preferences with { AllowUriFallback = value };
                LocalData.Save("launcher.json", next);
                _preferences = next;
            }
            catch (Exception error) { _shell.Report(error.Message); }
            Notify();
        }
    }

    private readonly LauncherViewModel _shell;
    public string Storage => LocalData.DirectoryPath;
    public IReadOnlyList<string> DataFiles => LocalData.Files;
    public ICommand OpenData { get; }
    public ICommand ClearData { get; }
    public bool Registered => ProtocolRegistration.IsRegistered;
    public string Mode => Registered ? "Solarious handles roblox-player: links" : "Side-by-side mode";
    public string Version => "Solarious " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
    public ICommand Register { get; }
    public ICommand Restore { get; }
    public ICommand Releases { get; }
    public ICommand Source { get; }
    public ICommand Notices { get; }

    internal SettingsViewModel(LauncherViewModel shell)
    {
        _shell = shell;
        OpenData = shell.Action(_ =>
        {
            Directory.CreateDirectory(Storage);
            Process.Start(new ProcessStartInfo(Storage) { UseShellExecute = true });
        });
        ClearData = shell.Action(_ =>
        {
            if (MessageBox.Show(Application.Current.MainWindow,
                "Clear saved games, bookmarks, playtime, settings, and cached charts? Solarious will close so you can reopen it with fresh settings. Screenshots and previous editions are kept.",
                "Clear Solarious data", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;

            LocalData.Clear();
            Application.Current.Shutdown();
        });
        NextBackdrop = shell.Action(_ => ChangeBackdrop(1));
        PreviousBackdrop = shell.Action(_ => ChangeBackdrop(-1));
        Register = shell.Action(_ =>
        {
            if (MessageBox.Show(Application.Current.MainWindow,
                "Set this copy of Solarious as the per-user handler for roblox-player: links?",
                "Windows link handler", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            ProtocolRegistration.Register();
            Notify(string.Empty);
        });
        Restore = shell.Action(_ =>
        {
            ProtocolRegistration.Restore();
            Notify(string.Empty);
        });
        Releases = shell.Action(_ => OfficialLauncher.OpenWeb(new Uri("https://github.com/Itxbreeze/Solarious/releases")));
        Source = shell.Action(_ => OfficialLauncher.OpenWeb(new Uri("https://github.com/bloxstraplabs/bloxstrap")));
        Notices = shell.Action(_ =>
        {
            var text = new System.Windows.Controls.TextBox
            {
                IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                Text = string.Join("\n\n", new[] { "Assets/LICENSE", "Assets/THIRD-PARTY-NOTICES",
                    "Notices/DotNetRuntimeLicense.txt", "Notices/WindowsDesktopLicense.txt",
                    "Notices/DotNetRuntimeThirdParty.txt" }.Select(LauncherViewModel.Resource))
            };
            var dialog = new Window
            {
                Owner = Application.Current.MainWindow, Content = text, Title = "Solarious — Licenses and notices",
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Width = 760, Height = 600,
                ShowInTaskbar = false
            };
            dialog.PreviewKeyDown += (_, e) =>
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                dialog.Close();
            };
            dialog.ShowDialog();
        });
    }
}
