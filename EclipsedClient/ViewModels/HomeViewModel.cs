using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EclipsedClient.Services;
using EclipsedClient.Views;

namespace EclipsedClient.ViewModels;

internal sealed record GameCard(Favorite Game, Brush Accent)
{
    public string Title => Game.Title;
    public string Notes => Game.Notes;
    public ImageSource? Icon { get; } = ChartsViewModel.Image(Game.Icon);
    public string PinLabel => Game.Pinned ? "Unpin from top" : "Pin to top";
}
internal sealed class FriendCard(Bookmark friend, ImageSource? avatar) : ViewModel
{
    public Bookmark Friend { get; } = friend;
    public ImageSource? Avatar { get; internal set; } = avatar;
    public string Name => Friend.DisplayName;
    public string Handle => "@" + Friend.Username;
    public string Presence { get; private set; } = "";
    internal void AvatarChanged() => Notify(nameof(Avatar));
    internal void ShowPresence(string text)
    {
        Presence = text;
        Notify(nameof(Presence));
    }
}

internal sealed class HomeViewModel : ViewModel, IDisposable
{
    private readonly PublicProfiles _api = new();
    private ProfileData _data = LocalData.Load<ProfileData>("profiles.json");
    private PublicProfile? _profile;
    public string Username { get; set; } = "";
    public string ProfileName => _profile?.DisplayName ?? "Public profile";
    public string Handle => _profile is null ? "Switch by username" : "@" + _profile.Name;
    public ImageSource? Avatar { get; private set; }
    public string Installation { get; private set; } = "Checking installation…";
    public string Session { get; private set; } = "Roblox is idle";
    public GameCard[] Games { get; private set; } = [];
    public FriendCard[] Friends { get; private set; } = [];
    public Visibility EmptyGames => Games.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyFriends => Friends.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    public string EmptyGameTitle => _data.Favorites.Count == 0 ? "Your library is empty" : "No matching games";
    public string EmptyGameHint => _data.Favorites.Count == 0
        ? "Pin a game from Top Charts or add one using its Roblox link."
        : "Try another title or keyword.";
    private string _search = "";
    public string Search
    {
        get => _search;
        set { _search = value; RefreshGames(); }
    }
    private readonly Discovery _discovery;
    public PlaceEntryViewModel Entry { get; }
    public ICommand Add { get; }
    public ICommand Scan { get; }
    public ICommand Lookup { get; }
    public ICommand BookmarkProfile { get; }
    public ICommand LoadAvatars { get; }
    public ICommand Edit { get; }
    public ICommand CopyLink { get; }
    public ICommand TogglePin { get; }
    public ICommand CheckPresence { get; }
    public ICommand Remove { get; }
    public ICommand Play { get; }
    public ICommand OpenGame { get; }
    public ICommand Profile { get; }
    public ICommand Join { get; }
    public ICommand RemoveFriend { get; }

    internal HomeViewModel(LauncherViewModel shell, Discovery discovery, CancellationToken lifetime)
    {
        _discovery = discovery;
        Entry = new(shell, discovery);
        Add = shell.Action(_ => ShowEditor(shell, null));
        Refresh();
        Scan = shell.Async(async _ =>
        {
            Installation = await Task.Run(OfficialLauncher.DescribeInstallation);
            Notify(nameof(Installation));
        });
        Lookup = shell.Async(async _ =>
        {
            _profile = await _api.LookupAsync(Username.Trim(), lifetime)
                ?? throw new InvalidOperationException("No public profile found.");
            Avatar = null;
            Username = _profile.Name;
            Notify(nameof(Username));
            Notify(nameof(ProfileName));
            Notify(nameof(Handle));
            Notify(nameof(Avatar));
            if (_profile.Headshot is { } url)
                Avatar = await AvatarAsync(url);
            Notify(nameof(Avatar));
        });
        BookmarkProfile = shell.Action(_ =>
        {
            var profile = _profile ?? throw new InvalidOperationException("Look up a public username first.");
            Save(_data with { Bookmarks = [.. _data.Bookmarks.Where(item => item.Id != profile.Id),
                new(profile.Id, profile.Name, profile.DisplayName)] });
            UpdateAvatar(profile.Id, Avatar);
            shell.Report($"Saved @{profile.Name}.");
        });
        LoadAvatars = shell.Async(async _ =>
        {
            foreach (var card in Friends.Where(card => card.Avatar is null))
            {
                var url = await _api.LookupHeadshotAsync(card.Friend.Id, lifetime);
                if (url is not null) UpdateAvatar(card.Friend.Id, await AvatarAsync(url));
            }
        });
        Edit = shell.Action(value => ShowEditor(shell, ((GameCard)value!).Game));
        CopyLink = shell.Action(value =>
        {
            Clipboard.SetText(OfficialLauncher.PlacePage(((GameCard)value!).Game.PlaceId).AbsoluteUri);
            shell.Report("Game link copied.");
        });
        TogglePin = shell.Action(value =>
        {
            var game = ((GameCard)value!).Game;
            Save(_data with { Favorites = _data.Favorites
                .Select(item => item.PlaceId == game.PlaceId ? item with { Pinned = !item.Pinned } : item).ToList() });
        });
        CheckPresence = new ActionCommand(async value =>
        {
            if (value is not FriendCard card) return;
            card.ShowPresence("Checking presence…");
            try { card.ShowPresence(await _api.GetPresenceAsync(card.Friend.Id, lifetime)); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception) { card.ShowPresence("Presence unavailable"); }
        });
        Remove = shell.Action(value =>
        {
            var game = ((GameCard)value!).Game;
            if (Confirm($"Remove {game.Title} from the library?"))
                Save(_data with { Favorites = [.. _data.Favorites.Where(item => item.PlaceId != game.PlaceId)] });
        });
        Play = shell.Async(value => shell.LaunchPlace(((GameCard)value!).Game.PlaceId));
        OpenGame = shell.Action(value => OfficialLauncher.OpenWeb(OfficialLauncher.PlacePage(((GameCard)value!).Game.PlaceId)));
        Profile = shell.Action(value => OfficialLauncher.OpenWeb(new Uri(FormattableString.Invariant(
            $"https://www.roblox.com/users/{((FriendCard)value!).Friend.Id}/profile"))));
        Join = shell.Async(value => shell.LaunchRoblox(() => OfficialLauncher.JoinUserAsync(((FriendCard)value!).Friend.Id)));
        RemoveFriend = shell.Action(value =>
        {
            var friend = ((FriendCard)value!).Friend;
            if (Confirm($"Remove @{friend.Username} from bookmarks?"))
                Save(_data with { Bookmarks = [.. _data.Bookmarks.Where(item => item.Id != friend.Id)] });
        });
        async Task<ImageSource> AvatarAsync(Uri url)
        {
            using var stream = new MemoryStream(await _api.LoadAvatarAsync(url, lifetime));
            return LauncherViewModel.Decode(stream);
        }
    }

    private static bool Confirm(string message) => MessageBox.Show(Application.Current.MainWindow, message,
        "Eclipsed Client", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void ShowEditor(LauncherViewModel shell, Favorite? original)
    {
        using var entry = new PlaceEntryViewModel(shell, _discovery);
        var editor = new GameDialog(original, entry) { Owner = Application.Current.MainWindow };
        if (editor.ShowDialog() == true && editor.Game is { } game)
            Save(_data with { Favorites = [.. _data.Favorites.Where(item => item.PlaceId != game.PlaceId), game] });
    }

    private void Save(ProfileData value)
    {
        LocalData.Save("profiles.json", value);
        _data = value;
        Refresh();
    }

    private void Refresh()
    {
        RefreshGames();
        var avatars = Friends.ToDictionary(item => item.Friend.Id, item => item.Avatar);
        Friends = _data.Bookmarks.OrderBy(friend => friend.DisplayName)
            .Select(friend => new FriendCard(friend, avatars.GetValueOrDefault(friend.Id))).ToArray();
        Notify(nameof(Friends));
        Notify(nameof(EmptyFriends));
    }

    internal void Pin(Experience game)
    {
        if (_data.Favorites.Any(item => item.PlaceId == game.PlaceId)) return;
        Save(_data with { Favorites = [.. _data.Favorites, new(game.PlaceId, game.Name, "", "#7C3AED", game.Icon)] });
    }

    private void RefreshGames()
    {
        string query = Search.Trim();
        Games = _data.Favorites.Where(game => game.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                game.Notes.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                game.PlaceId.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.Ordinal))
            .OrderByDescending(game => game.Pinned).ThenBy(game => game.Title).Select(game => new GameCard(game, GetAccent(game.Accent))).ToArray();
        Notify(nameof(Games));
        Notify(nameof(EmptyGames));
        Notify(nameof(EmptyGameTitle));
        Notify(nameof(EmptyGameHint));
    }

    private static Brush GetAccent(string color)
    {
        if (color.Length == 7 && color[0] == '#' && color.AsSpan(1).ToString().All(Uri.IsHexDigit))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        return Brushes.MediumPurple;
    }

    private void UpdateAvatar(long id, ImageSource? avatar)
    {
        if (Friends.FirstOrDefault(card => card.Friend.Id == id) is { } card)
        {
            card.Avatar = avatar;
            card.AvatarChanged();
        }
    }

    internal string? TitleFor(long? place) => place is long id
        ? _data.Favorites.FirstOrDefault(item => item.PlaceId == id)?.Title ?? $"Place {id}"
        : null;

    internal void UpdateSession(DateTimeOffset? started, DateTimeOffset now)
    {
        var text = started is { } time ? $"Playing  ·  {Elapsed(now - time)}" : "Roblox is idle";
        if (text == Session) return;
        Session = text;
        Notify(nameof(Session));
    }

    internal static string Elapsed(TimeSpan time) =>
        time < TimeSpan.Zero ? "00:00:00" : $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";

    public void Dispose()
    {
        Entry.Dispose();
        _api.Dispose();
    }
}
