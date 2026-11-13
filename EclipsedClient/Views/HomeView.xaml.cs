using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using EclipsedClient.Services;
using EclipsedClient.ViewModels;

namespace EclipsedClient.Views;

public partial class HomeView : UserControl
{
    private static readonly DependencyPropertyKey LibraryColumnsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(LibraryColumns), typeof(int), typeof(HomeView), new PropertyMetadata(2));
    public static readonly DependencyProperty LibraryColumnsProperty = LibraryColumnsPropertyKey.DependencyProperty;

    public int LibraryColumns => (int)GetValue(LibraryColumnsProperty);

    public HomeView() => InitializeComponent();

    private void ResizeLibrary(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
            SetValue(LibraryColumnsPropertyKey, Math.Clamp((int)(e.NewSize.Width / 240), 2, 5));
    }

    private void PastePlace(object sender, RoutedEventArgs e) => PastePlace(overwrite: true);

    private void PasteOnClick(object sender, MouseButtonEventArgs e) => PastePlace(overwrite: false);

    private void PastePlace(bool overwrite)
    {
        if (!overwrite && !string.IsNullOrWhiteSpace(QuickInput.Text)) return;
        try
        {
            if (Clipboard.ContainsText() && PlaceLink.TryParse(Clipboard.GetText(), out long id))
            {
                QuickInput.SetCurrentValue(TextBox.TextProperty, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                QuickInput.CaretIndex = QuickInput.Text.Length;
            }
            else if (overwrite && Window.GetWindow(this)?.DataContext is LauncherViewModel launcher)
                launcher.Report("Copy a Roblox experience link or Place ID first.");
        }
        catch (ExternalException)
        {
            if (Window.GetWindow(this)?.DataContext is LauncherViewModel launcher)
                launcher.Report("The clipboard is busy. Try again.");
        }
    }

    private void OpenGameMenu(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FrameworkElement card } && card.ContextMenu is { } menu)
        {
            menu.PlacementTarget = card;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
