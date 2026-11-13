using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using EclipsedClient.ViewModels;

namespace EclipsedClient;

public partial class MainWindow : Window
{
    private readonly LauncherViewModel _launcher = new();
    private readonly TrayController _tray;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _launcher;
        _tray = new(this, _launcher);
        _launcher.PropertyChanged += OnLauncherChanged;
        _clock.Tick += (_, _) => _launcher.Tick();
        Loaded += (_, _) =>
        {
            _launcher.Home.Scan.Execute(null);
            _launcher.Tick();
            _clock.Start();
        };
        WindowFrame.SizeChanged += (_, _) => UpdateFrame();
        StateChanged += (_, _) =>
        {
            MaxButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            UpdateFrame();
        };
        SourceInitialized += (_, _) =>
        {
            int dark = 1;
            IntPtr handle = new WindowInteropHelper(this).Handle;
            DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            int round = 2;
            DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
        };
    }

    private void UpdateFrame()
    {
        double radius = WindowState == WindowState.Maximized ? 0 : 16;
        WindowFrame.CornerRadius = new CornerRadius(radius);
        WindowFrame.Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(0, 0, WindowFrame.ActualWidth, WindowFrame.ActualHeight), radius, radius);
    }

    private void OnLauncherChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LauncherViewModel.Selected)) return;
        PageScroll.ScrollToTop();
        if (SystemParameters.ClientAreaAnimation)
            PageContent.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(
                0.86, 1, TimeSpan.FromMilliseconds(145))
            {
                FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
            });
    }

    private void Minimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void Maximize(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _clock.Stop();
        _launcher.PropertyChanged -= OnLauncherChanged;
        _tray.Dispose();
        _launcher.Dispose();
        base.OnClosed(e);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
