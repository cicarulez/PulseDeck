using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PulseDeck.Core;

public sealed record SpotifyTrack(string Id, string Title, string[] Artists, string Album, double DurationSeconds)
{
    public string DisplayArtist => string.Join(", ", Artists);
    public bool Matches(MediaSnapshot media) => SpotifyLyrics.Eligible(media)
        && SteamGridImages.Normalize(Title) == SteamGridImages.Normalize(media.Title)
        && Math.Abs(DurationSeconds - media.DurationSeconds) <= 2
        && (string.IsNullOrWhiteSpace(media.Album) || SteamGridImages.Normalize(Album) == SteamGridImages.Normalize(media.Album))
        && (Artists.Any(a => SteamGridImages.Normalize(a) == SteamGridImages.Normalize(media.Artist))
            || SteamGridImages.Normalize(DisplayArtist) == SteamGridImages.Normalize(media.Artist));
}

public sealed record SpotifySnapshot(string Status = "disconnected", SpotifyTrack? Current = null,
    string? DeviceName = null, SpotifyTrack[]? Queue = null, string QueueStatus = "unavailable", DateTimeOffset? FetchedAt = null)
{
    public SpotifySnapshot ForLocal(MediaSnapshot media, DateTimeOffset now) => Status == "connected"
        && FetchedAt is { } fetched && now >= fetched && now - fetched <= TimeSpan.FromSeconds(25)
        && Current?.Matches(media) == true ? this : new();
}
public sealed record SpotifyConnection(string? ClientId, bool Connected, string AuthorizationStatus, string DataStatus)
{
    public string RedirectUri => SpotifyOAuth.RedirectUri;
}
public sealed record SpotifySecret(string ClientId, string? RefreshToken = null);
public interface ISpotifyCredentials
{
    SpotifySecret? Read();
    void Save(SpotifySecret value);
    void Clear();
}
public sealed record SpotifyToken(string AccessToken, string? RefreshToken, int ExpiresIn);
public sealed record SpotifyAuthorization(string State, string Verifier, string Url);
public static class SpotifyOAuth
{
    public const string RedirectUri = "http://127.0.0.1:5179/spotify/callback/";
    public const string Scope = "user-read-playback-state user-read-currently-playing";
    public static bool ValidClientId(string id) => id.Length == 32 && id.All(Uri.IsHexDigit);
    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static SpotifyAuthorization Create(string clientId)
    {
        if (!ValidClientId(clientId)) throw new ArgumentException("Inserisci il Client ID Spotify di 32 caratteri.");
        var state = Base64(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64(RandomNumberGenerator.GetBytes(48));
        var values = new Dictionary<string, string> { ["client_id"] = clientId, ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri, ["scope"] = Scope, ["state"] = state, ["code_challenge_method"] = "S256",
            ["code_challenge"] = Base64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) };
        return new(state, verifier, "https://accounts.spotify.com/authorize?" + string.Join('&',
            values.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value))));
    }
}
public sealed class SpotifyApiException(string status, TimeSpan? retryAfter = null) : Exception("Spotify request failed.")
{
    public string Status { get; } = status;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

// Only fixed Spotify endpoints are used. Error bodies and tokens are never surfaced to the UI/logs.
public sealed class SpotifyApi(HttpClient client)
{
    public Task<SpotifyToken> Exchange(string clientId, string code, string verifier, CancellationToken token) => Token(new()
    {
        ["client_id"] = clientId, ["grant_type"] = "authorization_code", ["code"] = code,
        ["code_verifier"] = verifier, ["redirect_uri"] = SpotifyOAuth.RedirectUri
    }, true, token);
    public Task<SpotifyToken> Refresh(SpotifySecret secret, CancellationToken token) => Token(new()
    {
        ["client_id"] = secret.ClientId, ["grant_type"] = "refresh_token", ["refresh_token"] = secret.RefreshToken!
    }, false, token);
    private async Task<SpotifyToken> Token(Dictionary<string, string> values, bool initial, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token") { Content = new FormUrlEncodedContent(values) };
        using var response = await client.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var code = String(error.RootElement, "error");
            if (code is "invalid_grant" or "invalid_client") throw new SpotifyApiException("reauthorize");
        }
        Check(response);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = json.RootElement;
        if (initial || root.TryGetProperty("scope", out _))
        {
            var scopes = String(root, "scope").Split(' ');
            if (!SpotifyOAuth.Scope.Split(' ').All(scopes.Contains)) throw new SpotifyApiException("reauthorize");
        }
        var access = String(root, "access_token"); var refresh = String(root, "refresh_token");
        if (string.IsNullOrWhiteSpace(access) || !String(root, "token_type").Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || !root.TryGetProperty("expires_in", out var expiration) || !expiration.TryGetInt32(out var seconds) || seconds < 60
            || initial && string.IsNullOrWhiteSpace(refresh)) throw new SpotifyApiException("unavailable");
        return new(access, string.IsNullOrWhiteSpace(refresh) ? null : refresh, seconds);
    }
    public async Task<SpotifySnapshot> Playback(string accessToken, CancellationToken token)
    {
        using var response = await Get("me/player", accessToken, token);
        if (response.StatusCode == HttpStatusCode.NoContent) return new("idle");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = json.RootElement;
        if (root.TryGetProperty("device", out var device) && device.ValueKind == JsonValueKind.Object
            && device.TryGetProperty("is_private_session", out var privacy) && privacy.ValueKind == JsonValueKind.True) return new("private");
        var track = root.TryGetProperty("item", out var item) ? ParseTrack(item) : null;
        if (track is null) return new("idle");
        return new("connected", track, String(device, "name"), FetchedAt: DateTimeOffset.UtcNow);
    }
    public async Task<(string? CurrentId, SpotifyTrack[] Tracks)> Queue(string accessToken, CancellationToken token)
    {
        using var response = await Get("me/player/queue", accessToken, token);
        if (response.StatusCode == HttpStatusCode.NoContent) return (null, []);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = json.RootElement;
        var current = root.TryGetProperty("currently_playing", out var playing) ? ParseTrack(playing) : null;
        var tracks = root.TryGetProperty("queue", out var queue) && queue.ValueKind == JsonValueKind.Array
            ? queue.EnumerateArray().Take(3).Select(ParseTrack).OfType<SpotifyTrack>().ToArray() : [];
        return (current?.Id, tracks);
    }
    private async Task<HttpResponseMessage> Get(string path, string accessToken, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request, token);
        try { Check(response); return response; }
        catch { response.Dispose(); throw; }
    }
    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
        throw new SpotifyApiException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "unauthorized", HttpStatusCode.Forbidden => "forbidden",
            HttpStatusCode.TooManyRequests => "rate-limited", _ => "unavailable"
        }, retry);
    }
    private static string String(JsonElement root, string key) => root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    public static SpotifyTrack? ParseTrack(JsonElement root)
    {
        if (String(root, "type") != "track" || root.TryGetProperty("is_local", out var local) && local.ValueKind == JsonValueKind.True) return null;
        var id = String(root, "id"); var title = String(root, "name");
        if (id.Length != 22 || !id.All(char.IsAsciiLetterOrDigit) || string.IsNullOrWhiteSpace(title) || title.Length > 300
            || !root.TryGetProperty("duration_ms", out var duration) || !duration.TryGetDouble(out var ms)
            || !double.IsFinite(ms) || ms is < 1000 or > 3600000) return null;
        var artists = root.TryGetProperty("artists", out var names) && names.ValueKind == JsonValueKind.Array
            ? names.EnumerateArray().Take(20).Select(a => String(a, "name")).Where(a => !string.IsNullOrWhiteSpace(a) && a.Length <= 300).Distinct().ToArray() : [];
        if (artists.Length == 0) return null;
        var album = root.TryGetProperty("album", out var value) ? String(value, "name") : "";
        return new(id, title, artists, album, ms / 1000d);
    }
}
