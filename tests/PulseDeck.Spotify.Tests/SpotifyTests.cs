using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using PulseDeck.Agent.Providers;
using PulseDeck.Core;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

public class SpotifyTests
{
    private const string ClientId = "0123456789abcdef0123456789abcdef";
    private const string Id = "0123456789ABCDEFGHIJKL";
    private static readonly string TrackJson = JsonSerializer.Serialize(new { id = Id, type = "track", name = "Synthetic song", duration_ms = 180000,
        artists = new[] { new { name = "First artist" }, new { name = "Second artist" } }, album = new { name = "Synthetic album" } });
    private static string PlaybackJson => "{\"device\":{\"name\":\"My PC\",\"is_private_session\":false},\"item\":" + TrackJson + "}";
    private static string QueueJson => "{\"currently_playing\":" + TrackJson + ",\"queue\":[" + TrackJson + "," + TrackJson + "]}";
    private static MediaSnapshot Local => new(true, "Synthetic song", "First artist", "Spotify.exe", 5, 180, "connected") { Album = "Synthetic album" };
    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static string TokenJson(string? refresh = "rotated-refresh") => JsonSerializer.Serialize(new { access_token = "private-access", refresh_token = refresh,
        token_type = "Bearer", expires_in = 3600, scope = SpotifyOAuth.Scope });
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Store(SpotifySecret? value = null) : ISpotifyCredentials
    {
        public SpotifySecret? Value = value;
        public SpotifySecret? Read() => Value;
        public void Save(SpotifySecret value) => Value = value;
        public void Clear() => Value = null;
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public readonly List<string> Requests = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Requests.Add(request.Method + " " + request.RequestUri!.AbsoluteUri); return respond(request, token); }
    }
    [Fact]
    public void PkceUsesRandomStateAndVerifierAndOnlyReadScopes()
    {
        var flow = SpotifyOAuth.Create(ClientId); var query = QueryHelpers.ParseQuery(new Uri(flow.Url).Query);
        Assert.Equal("https", new Uri(flow.Url).Scheme); Assert.Equal("accounts.spotify.com", new Uri(flow.Url).Host);
        Assert.Equal(SpotifyOAuth.RedirectUri, query["redirect_uri"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        var digest = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(flow.Verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(digest, query["code_challenge"]);
        Assert.Equal(SpotifyOAuth.Scope, query["scope"]);
        Assert.DoesNotContain("modify", flow.Url); Assert.DoesNotContain("client_secret", flow.Url);
        Assert.NotEqual(flow.State, SpotifyOAuth.Create(ClientId).State);
        Assert.InRange(flow.Verifier.Length, 43, 128);
        Assert.Throws<ArgumentException>(() => SpotifyOAuth.Create("invalid"));
    }
    [Fact]
    public void ExtrasMatchOnlyFreshLocalSpotifyAndDoNotChangeLyricsIdentity()
    {
        using var json = JsonDocument.Parse(TrackJson);
        var track = Assert.IsType<SpotifyTrack>(SpotifyApi.ParseTrack(json.RootElement));
        var now = DateTimeOffset.UtcNow;
        var snapshot = new SpotifySnapshot("connected", track, "My PC", [], "connected", now);
        Assert.Equal("First artist, Second artist", track.DisplayArtist);
        Assert.Equal(snapshot, snapshot.ForLocal(Local, now));
        foreach (var media in new[] { Local with { Title = "Other" }, Local with { Artist = "Other" }, Local with { Album = "Other" },
            Local with { DurationSeconds = 200 }, Local with { App = "chrome.exe" }, Local with { Source = "youtube-extension" } })
            Assert.Null(snapshot.ForLocal(media, now).Current);
        Assert.Null(snapshot.ForLocal(Local, now.AddSeconds(26)).Current);
        Assert.Null(snapshot.ForLocal(Local, now.AddSeconds(-1)).Current);
        Assert.Null((snapshot with { Status = "unavailable" }).ForLocal(Local, now).Current);
        var enriched = Local with { Artists = track.Artists };
        Assert.Equal(track.DisplayArtist, enriched.DisplayArtist);
        Assert.Equal(SpotifyLyrics.Key(Local), SpotifyLyrics.Key(enriched));
    }
    [Theory]
    [InlineData("2024", 2024)]
    [InlineData("2024-03", 2024)]
    [InlineData("2024-03-15", 2024)]
    [InlineData("", null)]
    [InlineData("unknown", null)]
    [InlineData("0000", null)]
    [InlineData("2024oops", null)]
    public void AlbumYearUsesReleaseMetadataWhenPresent(string releaseDate, int? expected)
    {
        using var json = JsonDocument.Parse(TrackJson.Replace("\"Synthetic album\"", "\"Synthetic album\",\"release_date\":" + JsonSerializer.Serialize(releaseDate)));
        var track = Assert.IsType<SpotifyTrack>(SpotifyApi.ParseTrack(json.RootElement));
        Assert.Equal(expected, track.AlbumYear);
    }
    [Theory]
    [InlineData("null")]
    [InlineData("{\"type\":\"episode\"}")]
    [InlineData("{\"type\":\"track\",\"is_local\":true}")]
    [InlineData("{\"type\":\"track\",\"id\":\"bad\"}")]
    public void UnsupportedItemsAreNotGuessed(string content)
    {
        using var doc = JsonDocument.Parse(content); Assert.Null(SpotifyApi.ParseTrack(doc.RootElement));
    }
    [Fact]
    public async Task NoLoginNeverContactsSpotify()
    {
        using var handler = new Handler((_, _) => throw new Exception("Unexpected network"));
        using var http = new HttpClient(handler); using var service = new SpotifyService(new Store(), new(http));
        await service.Poll(default);
        Assert.Empty(handler.Requests); Assert.False(service.Status.Connected); Assert.Null(service.Read(Local, DateTimeOffset.UtcNow).Current);
    }
    [Fact]
    public async Task RefreshRotationQueueMatchingAndDisconnectAreSafe()
    {
        var clock = new Clock(); var store = new Store(new(ClientId, "old-refresh"));
        using var handler = new Handler(async (request, token) =>
        {
            if (request.RequestUri!.Host == "accounts.spotify.com")
            {
                var body = await request.Content!.ReadAsStringAsync(token);
                Assert.Contains("refresh_token=old-refresh", body); Assert.DoesNotContain("client_secret", body);
                return Json(TokenJson());
            }
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme); Assert.Equal("private-access", request.Headers.Authorization?.Parameter);
            return Json(request.RequestUri.AbsolutePath.EndsWith("queue") ? QueueJson : PlaybackJson);
        });
        using var http = new HttpClient(handler); using var service = new SpotifyService(store, new(http), clock);
        await service.Poll(default);
        Assert.Equal("rotated-refresh", store.Value?.RefreshToken);
        var data = service.Read(Local, clock.Now);
        Assert.Equal("My PC", data.DeviceName); Assert.Equal(2, data.Queue?.Length); // Repeated queued tracks are legitimate.
        Assert.Equal("connected", data.QueueStatus);
        Assert.Equal(3, handler.Requests.Count);
        await service.Poll(default); Assert.Equal(3, handler.Requests.Count);
        clock.Now = clock.Now.AddSeconds(11); await service.Poll(default); Assert.Equal(4, handler.Requests.Count);
        Assert.DoesNotContain("refresh", JsonSerializer.Serialize(service.Status)); Assert.DoesNotContain("private-access", JsonSerializer.Serialize(data));
        await service.Disconnect(default);
        Assert.Equal(ClientId, store.Value?.ClientId); Assert.Null(store.Value?.RefreshToken);
        Assert.Null(service.Read(Local, clock.Now).Current);
        clock.Now = clock.Now.AddHours(1); await service.Poll(default); Assert.Equal(4, handler.Requests.Count);
    }
    [Fact]
    public async Task InvalidGrantRequiresLoginAndFallsBackWithoutRetrying()
    {
        var store = new Store(new(ClientId, "expired-refresh")); var clock = new Clock();
        using var handler = new Handler((_, _) => Task.FromResult(Json("{\"error\":\"invalid_grant\"}", HttpStatusCode.BadRequest)));
        using var http = new HttpClient(handler); using var service = new SpotifyService(store, new(http), clock);
        await service.Poll(default);
        Assert.False(service.Status.Connected); Assert.Equal("reauthorize", service.Status.AuthorizationStatus);
        Assert.Null(store.Value?.RefreshToken); Assert.Null(service.Read(Local, clock.Now).Current);
        clock.Now = clock.Now.AddDays(1); await service.Poll(default); Assert.Single(handler.Requests);
    }
    [Fact]
    public async Task RateLimitHonorsRetryAfterAndClearsExtras()
    {
        var clock = new Clock(); var limited = false;
        using var handler = new Handler((request, _) =>
        {
            if (request.RequestUri!.Host == "accounts.spotify.com") return Task.FromResult(Json(TokenJson()));
            if (limited) { var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests); response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120)); return Task.FromResult(response); }
            return Task.FromResult(Json(request.RequestUri.AbsolutePath.EndsWith("queue") ? QueueJson : PlaybackJson));
        });
        using var http = new HttpClient(handler); using var service = new SpotifyService(new Store(new(ClientId, "refresh")), new(http), clock);
        await service.Poll(default); Assert.NotNull(service.Read(Local, clock.Now).Current);
        limited = true; clock.Now = clock.Now.AddSeconds(11); await service.Poll(default);
        Assert.Equal("rate-limited", service.Status.DataStatus); Assert.Null(service.Read(Local, clock.Now).Current);
        var count = handler.Requests.Count;
        clock.Now = clock.Now.AddSeconds(119); await service.Poll(default); Assert.Equal(count, handler.Requests.Count);
        clock.Now = clock.Now.AddSeconds(1); await service.Poll(default); Assert.Equal(count + 1, handler.Requests.Count);
    }
    [Fact]
    public async Task QueueFromAnotherTrackCannotLeakAcrossTrackChange()
    {
        using var handler = new Handler((request, _) => Task.FromResult(Json(request.RequestUri!.Host == "accounts.spotify.com" ? TokenJson()
            : request.RequestUri.AbsolutePath.EndsWith("queue") ? QueueJson.Replace(Id, "ZYXWVUTSRQPONMLKJIHGFE") : PlaybackJson)));
        using var http = new HttpClient(handler); using var service = new SpotifyService(new Store(new(ClientId, "refresh")), new(http));
        await service.Poll(default); var data = service.Read(Local, DateTimeOffset.UtcNow);
        Assert.NotNull(data.Current); Assert.Null(data.Queue); Assert.Equal("unavailable", data.QueueStatus);
    }
    [Theory]
    [InlineData(204, "", "idle")]
    [InlineData(403, "", "forbidden")]
    [InlineData(500, "", "unavailable")]
    [InlineData(200, "malformed", "unavailable")]
    [InlineData(200, "{\"device\":{\"is_private_session\":true}}", "private")]
    public async Task MissingPrivateAndFailedPlaybackRemovePreviousExtras(int code, string content, string expected)
    {
        var clock = new Clock(); var fail = false;
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "accounts.spotify.com" ? Json(TokenJson())
            : fail ? Json(content, (HttpStatusCode)code) : Json(request.RequestUri.AbsolutePath.EndsWith("queue") ? QueueJson : PlaybackJson)));
        using var http = new HttpClient(handler); using var service = new SpotifyService(new Store(new(ClientId, "refresh")), new(http), clock);
        await service.Poll(default); Assert.NotNull(service.Read(Local, clock.Now).Current);
        fail = true; clock.Now = clock.Now.AddSeconds(11); await service.Poll(default);
        Assert.Equal(expected, service.Status.DataStatus); Assert.Null(service.Read(Local, clock.Now).Current);
    }
    [Fact]
    public async Task TokenRefreshWithoutRotationKeepsExistingRefreshToken()
    {
        var store = new Store(new(ClientId, "original-refresh"));
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "accounts.spotify.com" ? Json(TokenJson(null)) : new HttpResponseMessage(HttpStatusCode.NoContent)));
        using var http = new HttpClient(handler); using var service = new SpotifyService(store, new(http));
        await service.Poll(default); Assert.Equal("original-refresh", store.Value?.RefreshToken); Assert.True(service.Status.Connected);
    }

    [Fact]
    public async Task CallbackRejectsWrongStateThenPersistsValidPkceGrant()
    {
        var store = new Store();
        using var handler = new Handler(async (request, token) =>
        {
            Assert.Equal("https://accounts.spotify.com/api/token", request.RequestUri!.AbsoluteUri);
            var body = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal("authorization_code", body["grant_type"]); Assert.Equal("fixture-code", body["code"]);
            Assert.Equal(SpotifyOAuth.RedirectUri, body["redirect_uri"]); Assert.NotEmpty(body["code_verifier"].ToString());
            return Json(TokenJson());
        });
        using var http = new HttpClient(handler); using var service = new SpotifyService(store, new(http));
        using var callback = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            await service.Configure(ClientId, default);
            var url = await service.Connect(default); var state = QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString();
            using var wrong = await callback.GetAsync(SpotifyOAuth.RedirectUri + "?state=wrong&code=fixture-code");
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode); Assert.Null(store.Value?.RefreshToken);
            using var right = await callback.GetAsync(SpotifyOAuth.RedirectUri + "?state=" + state + "&code=fixture-code");
            Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
            Assert.Equal("http://127.0.0.1:5178/#settings/spotify", right.Headers.Location?.AbsoluteUri);
            Assert.Equal("rotated-refresh", store.Value?.RefreshToken); Assert.True(service.Status.Connected);
            Assert.Single(handler.Requests);
        }
        finally { await service.StopAsync(default); }
    }
    [Fact]
    public async Task CancelledLoginCannotRestoreCredentialsAfterDisconnect()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Store(new(ClientId));
        using var handler = new Handler(async (_, _) => { entered.SetResult(); await release.Task; return Json(TokenJson()); });
        using var http = new HttpClient(handler); using var service = new SpotifyService(store, new(http));
        using var callback = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        try
        {
            var url = await service.Connect(default); var state = QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString();
            var response = callback.GetAsync(SpotifyOAuth.RedirectUri + "?state=" + state + "&code=fixture-code");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.Disconnect(default); release.SetResult();
            try { (await response).Dispose(); } catch (HttpRequestException) { }
            await service.StopAsync(default);
            Assert.Null(store.Value?.RefreshToken); Assert.False(service.Status.Connected);
        }
        finally { release.TrySetResult(); await service.StopAsync(default); }
    }
}
