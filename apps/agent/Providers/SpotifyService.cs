using System.Net;
using Microsoft.Extensions.Hosting;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed record SpotifyClientRequest(string ClientId);

public sealed class SpotifyService : BackgroundService
{
    private readonly ISpotifyCredentials credentials;
    private readonly SpotifyApi api;
    private readonly TimeProvider clock;
    private readonly SemaphoreSlim gate = new(1);
    private readonly List<Task> authorizations = [];
    private SpotifySecret? secret;
    private SpotifyToken? access;
    private DateTimeOffset expires, nextRead, nextQueue;
    private int failures;
    private CancellationTokenSource? authorization;
    private volatile SpotifyConnection status = new(null, false, "idle", "disconnected");
    private volatile SpotifySnapshot snapshot = new();
    public SpotifyConnection Status => status;
    public SpotifySnapshot Read(MediaSnapshot media, DateTimeOffset now) => snapshot.ForLocal(media, now);

    public SpotifyService(ISpotifyCredentials credentials, SpotifyApi api, TimeProvider? clock = null)
    {
        this.credentials = credentials; this.api = api; this.clock = clock ?? TimeProvider.System;
        try
        {
            secret = credentials.Read();
            if (secret is not null && !SpotifyOAuth.ValidClientId(secret.ClientId)) throw new InvalidDataException();
            status = new(secret?.ClientId, secret?.RefreshToken is not null, "idle", "disconnected");
        }
        catch { secret = null; status = new(null, false, "credentials-unavailable", "disconnected"); }
    }
    public async Task Configure(string clientId, CancellationToken token)
    {
        clientId = clientId.Trim();
        if (!SpotifyOAuth.ValidClientId(clientId)) throw new ArgumentException("Inserisci il Client ID Spotify di 32 caratteri.");
        await gate.WaitAsync(token);
        try
        {
            if (secret?.ClientId == clientId) return;
            var saved = new SpotifySecret(clientId);
            credentials.Save(saved); authorization?.Cancel(); authorization = null;
            secret = saved; access = null; snapshot = new(); failures = 0; nextRead = nextQueue = default;
            status = new(clientId, false, "idle", "disconnected");
        }
        finally { gate.Release(); }
    }
    public async Task<string> Connect(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (secret is null) throw new ArgumentException("Configura prima il Client ID dell’app Spotify.");
            authorization?.Cancel(); authorization = null;
            var flow = SpotifyOAuth.Create(secret.ClientId);
            var listener = new HttpListener(); listener.Prefixes.Add(SpotifyOAuth.RedirectUri);
            try { listener.Start(); }
            catch { listener.Close(); status = status with { AuthorizationStatus = "failed" }; throw new ArgumentException("La porta di collegamento Spotify (5179) non è disponibile. Riprova dopo aver chiuso l’altro collegamento."); }
            var pending = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            authorization = pending;
            snapshot = new(); status = status with { AuthorizationStatus = "waiting", DataStatus = "disconnected" };
            authorizations.RemoveAll(t => t.IsCompleted);
            authorizations.Add(CompleteAuthorization(listener, secret.ClientId, flow, pending));
            return flow.Url;
        }
        finally { gate.Release(); }
    }
    private async Task CompleteAuthorization(HttpListener listener, string clientId, SpotifyAuthorization flow, CancellationTokenSource pending)
    {
        HttpListenerContext? callback = null;
        try
        {
            using var close = pending.Token.Register(listener.Close);
            while (true)
            {
                callback = await listener.GetContextAsync().WaitAsync(pending.Token);
                if (callback.Request.HttpMethod == "GET" && callback.Request.Url?.AbsolutePath == new Uri(SpotifyOAuth.RedirectUri).AbsolutePath
                    && callback.Request.QueryString.GetValues("state") is { Length: 1 } states && states[0] == flow.State) break;
                callback.Response.StatusCode = 400; callback.Response.Close(); callback = null;
            }
            var codes = callback.Request.QueryString.GetValues("code");
            if (callback.Request.QueryString["error"] is not null || codes is not { Length: 1 } || string.IsNullOrWhiteSpace(codes[0]))
                throw new SpotifyApiException("denied");
            var result = await api.Exchange(clientId, codes[0], flow.Verifier, pending.Token);
            await gate.WaitAsync(pending.Token);
            try
            {
                pending.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(authorization, pending)) return;
                var saved = new SpotifySecret(clientId, result.RefreshToken);
                credentials.Save(saved); secret = saved; SetAccess(result);
                failures = 0; nextRead = nextQueue = default;
                status = new(clientId, true, "connected", "loading");
            }
            finally { gate.Release(); }
        }
        catch (Exception e)
        {
            await gate.WaitAsync();
            try
            {
                if (ReferenceEquals(authorization, pending)) status = status with
                { AuthorizationStatus = pending.IsCancellationRequested ? "expired" : e is SpotifyApiException { Status: "denied" } ? "denied" : "failed" };
            }
            finally { gate.Release(); }
        }
        finally
        {
            if (callback is not null)
                try
                {
                    callback.Response.Headers["Cache-Control"] = "no-store";
                    callback.Response.Headers["Referrer-Policy"] = "no-referrer";
                    callback.Response.Redirect("http://127.0.0.1:5178/#settings/spotify");
                    callback.Response.Close();
                }
                catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException) { }
            listener.Close();
            await gate.WaitAsync();
            try { if (ReferenceEquals(authorization, pending)) authorization = null; }
            finally { gate.Release(); pending.Dispose(); }
        }
    }
    private void SetAccess(SpotifyToken token) { access = token; expires = clock.GetUtcNow().AddSeconds(token.ExpiresIn - 30); }
    public async Task Disconnect(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            // Keep the public app ID so reconnecting does not require setup again.
            if (secret is not null) credentials.Save(new(secret.ClientId)); else credentials.Clear();
            authorization?.Cancel(); authorization = null;
            secret = secret is null ? null : new(secret.ClientId); access = null; snapshot = new();
            status = new(secret?.ClientId, false, "idle", "disconnected");
            failures = 0; nextRead = nextQueue = default;
        }
        finally { gate.Release(); }
    }
    public async Task Poll(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (authorization is not null || secret?.RefreshToken is null || clock.GetUtcNow() < nextRead) return;
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                if (access is null || expires <= clock.GetUtcNow())
                {
                    var refreshed = await api.Refresh(secret, budget.Token);
                    if (refreshed.RefreshToken is { } rotated)
                    {
                        var saved = secret with { RefreshToken = rotated }; credentials.Save(saved); secret = saved;
                    }
                    SetAccess(refreshed);
                }
                var playback = (await api.Playback(access!.AccessToken, budget.Token)) with { FetchedAt = clock.GetUtcNow() };
                if (playback.Current is not null)
                {
                    if (snapshot.Current?.Id != playback.Current.Id || clock.GetUtcNow() >= nextQueue)
                    {
                        try
                        {
                            var queue = await api.Queue(access.AccessToken, budget.Token);
                            playback = playback with { Queue = queue.CurrentId == playback.Current.Id ? queue.Tracks : null,
                                QueueStatus = queue.CurrentId == playback.Current.Id ? "connected" : "unavailable" };
                        }
                        catch (SpotifyApiException e) when (e.Status is "unavailable" or "forbidden") { }
                        catch (HttpRequestException) { }
                        nextQueue = clock.GetUtcNow().AddSeconds(30);
                    }
                    else playback = playback with { Queue = snapshot.Queue, QueueStatus = snapshot.QueueStatus };
                }
                snapshot = playback;
                status = status with { Connected = true, DataStatus = playback.Status };
                failures = 0; nextRead = clock.GetUtcNow().AddSeconds(10);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception e)
            {
                snapshot = new();
                var problem = e as SpotifyApiException;
                var state = problem?.Status ?? "unavailable";
                if (state is "unauthorized" or "reauthorize") access = null;
                if (state == "reauthorize")
                {
                    secret = secret with { RefreshToken = null };
                    try { credentials.Save(secret); } catch (Exception) { /* Do not let a storage failure stop the local display. */ }
                    status = status with { Connected = false, AuthorizationStatus = "reauthorize" };
                }
                status = status with { DataStatus = state };
                var retry = TimeSpan.FromSeconds(Math.Min(300, 10 * Math.Pow(2, Math.Min(++failures, 5))));
                if (state == "rate-limited") retry = problem?.RetryAfter is { } requested && requested > retry ? requested : TimeSpan.FromMinutes(1) > retry ? TimeSpan.FromMinutes(1) : retry;
                nextRead = clock.GetUtcNow().Add(retry);
            }
        }
        finally { gate.Release(); }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        Task[] tasks;
        try { authorization?.Cancel(); tasks = authorizations.ToArray(); }
        finally { gate.Release(); }
        await Task.WhenAll(tasks).WaitAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Poll(stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            await gate.WaitAsync();
            Task[] tasks;
            try { authorization?.Cancel(); tasks = authorizations.ToArray(); }
            finally { gate.Release(); }
            await Task.WhenAll(tasks);
        }
    }
}
