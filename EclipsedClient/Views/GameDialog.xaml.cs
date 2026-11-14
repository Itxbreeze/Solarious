using System.Windows;
using EclipsedClient.Services;
using EclipsedClient.ViewModels;

namespace EclipsedClient.Views;

public partial class GameDialog : Window
{
    private readonly Favorite? _original;
    private readonly PlaceEntryViewModel _entry;
    internal Favorite? Game { get; private set; }

    internal GameDialog(Favorite? original, PlaceEntryViewModel entry)
    {
        _original = original;
        _entry = entry;
        InitializeComponent();
        DataContext = entry;
        Heading.Text = original is null ? "Add game" : "Edit saved game";
        PlaceFields.Visibility = original is null ? Visibility.Visible : Visibility.Collapsed;
        GameTitle.Text = original?.Title ?? "";
        Notes.Text = original?.Notes ?? "";
        AccentColor.Text = original?.Accent ?? "#CFA465";
        _entry.PropertyChanged += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(GameTitle.Text) && _entry.Experience is { } game)
                GameTitle.Text = game.Name;
        };
        Loaded += (_, _) =>
        {
            if (original is null) PlaceInput.Focus();
            else GameTitle.Focus();
        };
    }

    private void PasteLink(object sender, RoutedEventArgs e)
    {
        try
        {
            string input = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
            if (!PlaceLink.TryParse(input, out _) && !OfficialLauncher.IsPlayerLink(input))
            {
                Validation.Text = "Copy a Roblox experience link or Place ID first.";
                return;
            }

            Validation.Text = "";
            PlaceInput.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, input);
            PlaceInput.Focus();
            PlaceInput.CaretIndex = PlaceInput.Text.Length;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            Validation.Text = "The clipboard is busy. Try again.";
        }
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        long id = _original?.PlaceId ?? 0;
        string title = GameTitle.Text.Trim();
        string color = AccentColor.Text.Trim();
        if (_original is null && !PlaceLink.TryParse(_entry.Input, out id))
            Validation.Text = "Enter a valid Roblox experience URL or Place ID.";
        else if (title.Length == 0)
            Validation.Text = "Enter a game title.";
        else if (color.Length != 7 || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit))
            Validation.Text = "Enter an accent color in #RRGGBB format.";
        else
        {
            Game = new(id, title, Notes.Text.Trim(), color, _original?.Icon ?? _entry.Experience?.Icon, _original?.Pinned ?? false);
            DialogResult = true;
        }
    }
}
