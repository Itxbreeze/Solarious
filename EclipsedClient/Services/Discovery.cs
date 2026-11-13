using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;

namespace EclipsedClient.Services;

internal sealed record Experience(long UniverseId, long PlaceId, string Name, long Players, byte[]? Icon = null);
internal sealed record ChartsCache(DateTimeOffset Updated, Experience[] Games)
{
    public ChartsCache() : this(default, []) { }
}

internal sealed class Discovery : IDisposable
{
    private readonly HttpClient _http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(8),
        MaxResponseContentBufferSize = 4 * 1024 * 1024
    };

    private sealed record Universe(long UniverseId);
    private sealed record Game(long Id, string Name, long Playing);

    internal async Task<Experience?> LookupAsync(long place, CancellationToken token)
    {
        var universe = await _http.GetFromJsonAsync<Universe>(FormattableString.Invariant(
            $"https://apis.roblox.com/universes/v1/places/{place}/universe"), token);
        if (universe is null || universe.UniverseId <= 0) return null;
        var games = await _http.GetFromJsonAsync<Results<Game>>(FormattableString.Invariant(
            $"https://games.roblox.com/v1/games?universeIds={universe.UniverseId}"), token);
        var game = games?.Data.FirstOrDefault();
        if (game is null || string.IsNullOrWhiteSpace(game.Name)) return null;
        return (await AddIconsAsync([new(game.Id, place, game.Name, game.Playing)], token))[0];
    }

    private sealed record SortGame(long UniverseId, long RootPlaceId, string Name, long PlayerCount, bool IsSponsored);
    private sealed record Sort(SortGame[] Games);
    private sealed record Icon(long TargetId, string State, Uri? ImageUrl);
    private sealed record Results<T>(T[] Data);

    internal async Task<Experience[]> ChartsAsync(CancellationToken token)
    {
        var feed = await _http.GetFromJsonAsync<Sort>(
            $"https://apis.roblox.com/explore-api/v1/get-sort-content?sessionId={Guid.NewGuid():D}&sortId=top-playing-now&device=computer&country=all", token);
        var games = (feed?.Games ?? []).Where(game => !game.IsSponsored && game.RootPlaceId > 0 && game.UniverseId > 0 &&
                !string.IsNullOrWhiteSpace(game.Name))
            .OrderByDescending(game => game.PlayerCount).DistinctBy(game => game.RootPlaceId).Take(3)
            .Select(game => new Experience(game.UniverseId, game.RootPlaceId, game.Name, game.PlayerCount)).ToArray();
        if (games.Length == 0)
            throw new InvalidOperationException("Roblox did not return any chart entries.");
        return await AddIconsAsync(games, token);
    }

    private async Task<Experience[]> AddIconsAsync(Experience[] games, CancellationToken token)
    {
        try
        {
            var ids = string.Join(',', games.Select(game => game.UniverseId.ToString(CultureInfo.InvariantCulture)));
            var icons = await _http.GetFromJsonAsync<Results<Icon>>(
                $"https://thumbnails.roblox.com/v1/games/icons?universeIds={ids}&size=150x150&format=Png&isCircular=false", token);
            return await Task.WhenAll(games.Select(async game =>
            {
                var url = icons?.Data.FirstOrDefault(icon => icon.TargetId == game.UniverseId && icon.State == "Completed")?.ImageUrl;
                if (url is null || !url.IsAbsoluteUri || url.Scheme != "https" || !url.IsDefaultPort || url.UserInfo.Length != 0 ||
                    !url.Host.EndsWith(".rbxcdn.com", StringComparison.OrdinalIgnoreCase))
                    return game;
                try
                {
                    byte[] bytes = await _http.GetByteArrayAsync(url, token);
                    return bytes.Length <= 300_000 && bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                        ? game with { Icon = bytes } : game;
                }
                catch (HttpRequestException) { return game; }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { return game; }
            }));
        }
        catch (HttpRequestException) { return games; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return games; }
        catch (System.Text.Json.JsonException) { return games; }
    }

    public void Dispose() => _http.Dispose();
}
