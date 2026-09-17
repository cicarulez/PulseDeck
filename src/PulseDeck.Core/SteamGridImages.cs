using System.Text;
using System.Text.Json;

namespace PulseDeck.Core;

public static class SteamGridImages
{
    public static string Normalize(string name) => new(name.Normalize(NormalizationForm.FormD)
        .Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static int? MatchGame(JsonElement root, string title)
    {
        if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True
            || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return null;
        var matches = data.EnumerateArray().Where(item => item.TryGetProperty("name", out var name)
            && name.ValueKind == JsonValueKind.String && Normalize(name.GetString()!) == Normalize(title))
            .Where(item => item.TryGetProperty("id", out var id) && id.TryGetInt32(out var value) && value > 0).ToArray();
        return matches.Length == 1 ? matches[0].GetProperty("id").GetInt32() : null;
    }
    public static IReadOnlyList<Uri> Candidates(JsonElement root, bool hero)
    {
        if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True
            || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return [];
        var images = new List<(Uri Url, double Ratio, int Score)>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.TryGetProperty("nsfw", out var nsfw) && nsfw.ValueKind == JsonValueKind.True
                || item.TryGetProperty("humor", out var humor) && humor.ValueKind == JsonValueKind.True) continue;
            if (!item.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String
                || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri) || !SafeImageUrl(uri)
                || !item.TryGetProperty("width", out var width) || !width.TryGetInt32(out var w)
                || !item.TryGetProperty("height", out var height) || !height.TryGetInt32(out var h)
                || w < 100 || h < 100 || w <= h || (long)w * h > 4 * 1024 * 1024) continue;
            var score = item.TryGetProperty("score", out var points) && points.TryGetInt32(out var p) ? p : 0;
            images.Add((uri, Math.Abs((double)w / h - (hero ? 4 : 3)), score));
        }
        return images.OrderBy(i => i.Ratio).ThenByDescending(i => i.Score).Take(3).Select(i => i.Url).ToArray();
    }
    public static bool SafeImageUrl(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Host is "cdn2.steamgriddb.com" or "cdn.steamgriddb.com";
}
