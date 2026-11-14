using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;




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
