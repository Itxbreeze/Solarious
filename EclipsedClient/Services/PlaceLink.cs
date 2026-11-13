using System.Globalization;

namespace EclipsedClient.Services;

internal static class PlaceLink
{
    internal static bool TryParse(string? value, out long id)
    {
        id = 0;
        string input = value?.Trim() ?? "";
        if (TryId(input, out id)) return true;
        if (input.StartsWith("roblox.com/", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("www.roblox.com/", StringComparison.OrdinalIgnoreCase))
            input = "https://" + input;
        if (!Uri.TryCreate(input, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https") ||
            uri.Host is not ("roblox.com" or "www.roblox.com") || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
            return false;

        string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return TryId(parts[0], out id);
        if (parts.Length < 2 || !parts[0].Equals("games", StringComparison.OrdinalIgnoreCase)) return false;
        if (!parts[1].Equals("start", StringComparison.OrdinalIgnoreCase)) return TryId(parts[1], out id);

        foreach (string parameter in uri.Query.TrimStart('?').Split('&'))
            if (parameter.StartsWith("placeId=", StringComparison.OrdinalIgnoreCase))
                return TryId(parameter[8..], out id);
        return false;
    }

    private static bool TryId(string value, out long id) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
}
