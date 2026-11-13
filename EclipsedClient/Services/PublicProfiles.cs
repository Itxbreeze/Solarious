using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace EclipsedClient.Services;

internal sealed record PublicProfile(long Id, string Name, string DisplayName, Uri? Headshot = null);
internal sealed record AvatarEntry(string State, Uri? ImageUrl);
internal sealed record RobloxResponse<T>(T[] Data);

internal sealed class PublicProfiles : IDisposable
{
    private static readonly Uri Users = new("https://users.roblox.com/v1/usernames/users");
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        UseCookies = false,
        AllowAutoRedirect = false,
        UseDefaultCredentials = false
    })
    {
        Timeout = TimeSpan.FromSeconds(8),
        MaxResponseContentBufferSize = 4 * 1024 * 1024
    };

    internal async Task<PublicProfile?> LookupAsync(string username, CancellationToken cancellation)
    {
        if (username.Length is < 1 or > 20 || username.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Enter a Roblox username using letters, numbers, or underscores.");

        // Roblox accepts a batch lookup as a POST. This endpoint only reads public profiles.
        using HttpResponseMessage response = await _http.PostAsJsonAsync(Users,
            new { usernames = new[] { username }, excludeBannedUsers = true }, cancellation);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("Roblox rate-limited public lookups. Try again shortly.");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RobloxResponse<PublicProfile>>(cancellation);
        PublicProfile? profile = result?.Data.FirstOrDefault();
        if (profile is null)
            return null;

        try
        {
            return profile with { Headshot = await LookupHeadshotAsync(profile.Id, cancellation) };
        }

        catch (HttpRequestException) { return profile; }
        catch (JsonException) { return profile; }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return profile; }
    }

    private sealed record PresenceResponse(PresenceEntry[] UserPresences);
    private sealed record PresenceEntry(long UserId, int UserPresenceType);

    internal async Task<string> GetPresenceAsync(long id, CancellationToken cancellation)
    {
        using var response = await _http.PostAsJsonAsync("https://presence.roblox.com/v1/presence/users",
            new { userIds = new[] { id } }, cancellation);
        if (!response.IsSuccessStatusCode) return "Presence unavailable";
        var data = await response.Content.ReadFromJsonAsync<PresenceResponse>(cancellation);
        return data?.UserPresences?.FirstOrDefault(entry => entry.UserId == id)?.UserPresenceType switch
        {
            0 => "Offline or hidden",
            1 => "Online",
            2 => "In game",
            3 => "In Roblox Studio",
            _ => "Presence unavailable"
        };
    }

    internal async Task<Uri?> LookupHeadshotAsync(long id, CancellationToken cancellation)
    {
        var uri = new Uri(FormattableString.Invariant(
            $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={id}&size=150x150&format=Png&isCircular=false"));
        var avatar = await _http.GetFromJsonAsync<RobloxResponse<AvatarEntry>>(uri, cancellation);
        return avatar?.Data.FirstOrDefault() is { State: "Completed", ImageUrl: { } image } && IsAvatarUrl(image) ? image : null;
    }

    internal async Task<byte[]> LoadAvatarAsync(Uri uri, CancellationToken cancellation)
    {
        if (!IsAvatarUrl(uri))
            throw new ArgumentException("The avatar must be hosted on Roblox’s public CDN.");

        byte[] data = await _http.GetByteArrayAsync(uri, cancellation);
        if (!data.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidDataException("Roblox did not return a PNG avatar.");
        return data;
    }

    private static bool IsAvatarUrl(Uri uri) =>
        uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
        uri.Host.EndsWith(".rbxcdn.com", StringComparison.OrdinalIgnoreCase) && uri.UserInfo.Length == 0;

    public void Dispose() => _http.Dispose();
}
