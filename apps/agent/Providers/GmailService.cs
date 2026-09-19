using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PulseDeck.Agent.Configuration;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed record GmailStatus(bool ClientConfigured, bool Connected, string AuthorizationStatus, NotificationSource Source);
public sealed record GmailClientRequest(string Json);

public sealed class GmailService : BackgroundService
{
    private const string Scope = "https://www.googleapis.com/auth/gmail.metadata";
    private readonly ConfigStore config;
    private readonly GmailCredentials credentials;
    private readonly NotificationCenter center;
    private readonly HttpClient client;
    private readonly GmailMailbox mailbox;
    private readonly SemaphoreSlim gate = new(1);
    private GmailSecret? secret;
    private string? accessToken;
    private DateTimeOffset expires, nextRead;
    private int failures;
    private CancellationTokenSource? authorization;
    private Task? authorizationTask;
    private volatile GmailStatus status = new(false, false, "idle", new("gmail", "mail", "not-configured"));
    public GmailStatus Status => status;
    public GmailService(ConfigStore config, GmailCredentials credentials, NotificationCenter center, IHttpClientFactory clients)
    {
        this.config = config; this.credentials = credentials; this.center = center;
        client = clients.CreateClient("gmail"); mailbox = new(client);
        try { secret = credentials.Read(); }
        catch { status = status with { AuthorizationStatus = "credentials-unavailable" }; }
        status = status with { ClientConfigured = secret is not null, Connected = secret?.RefreshToken is not null };
    }
    private void Publish(string state, int? count = null, bool arrival = false)
    {
        var source = new NotificationSource("gmail", "mail", state, count, count.HasValue ? DateTimeOffset.UtcNow : null);
        status = status with { Source = source };
        center.Publish(source, arrival ? new("gmail", "mail", "Nuova email", "GMAIL") : null, config.Current.Notifications.Animate, config.Current.Notifications.PollSeconds + 10);
    }
    public async Task Import(string json, CancellationToken token)
    {
        if (json.Length > 16384) throw new ArgumentException("File OAuth troppo grande.");
        GmailSecret value;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var installed = doc.RootElement.GetProperty("installed");
            var id = installed.GetProperty("client_id").GetString()!;
            var key = installed.GetProperty("client_secret").GetString()!;
            if (!id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal) || id.Length > 256 || string.IsNullOrWhiteSpace(key) || key.Length > 256)
                throw new FormatException();
            value = new(id, key);
        }
        catch { throw new ArgumentException("Scegli il JSON di un client OAuth Google di tipo Desktop."); }
        await gate.WaitAsync(token);
        try
        {
            credentials.Save(value); authorization?.Cancel(); authorization = null; secret = value;
            accessToken = null; mailbox.Reset(); nextRead = default; failures = 0;
            status = new(true, false, "idle", new("gmail", "mail", "not-configured")); Publish("not-configured");
        }
        finally { gate.Release(); }
    }
    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public async Task<string> Connect(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (secret is null) throw new ArgumentException("Importa prima il client OAuth Desktop.");
            authorization?.Cancel();
            var listener = new HttpListener();
            string redirect;
            // Bind only loopback; retry if another process obtains the selected ephemeral port.
            for (var attempt = 0; ; attempt++)
            {
                using var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
                var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
                redirect = $"http://127.0.0.1:{port}/";
                listener.Prefixes.Clear(); listener.Prefixes.Add(redirect);
                try { listener.Start(); break; }
                catch (HttpListenerException) when (attempt < 4) { }
            }
            var pending = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            authorization = pending;
            var state = Base64(RandomNumberGenerator.GetBytes(32));
            var verifier = Base64(RandomNumberGenerator.GetBytes(48));
            var challenge = Base64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            status = status with { AuthorizationStatus = "waiting" };
            authorizationTask = CompleteAuthorization(listener, secret, redirect, state, verifier, pending);
            return "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join('&', new Dictionary<string, string>
            {
                ["client_id"] = secret.ClientId, ["redirect_uri"] = redirect, ["response_type"] = "code",
                ["scope"] = Scope, ["state"] = state, ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256", ["access_type"] = "offline", ["prompt"] = "consent"
            }.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));
        }
        finally { gate.Release(); }
    }
    private async Task CompleteAuthorization(HttpListener listener, GmailSecret pendingSecret, string redirect, string state, string verifier, CancellationTokenSource pending)
    {
        try
        {
            using var close = pending.Token.Register(listener.Close);
            HttpListenerContext callback;
            while (true)
            {
                callback = await listener.GetContextAsync().WaitAsync(pending.Token);
                if (callback.Request.HttpMethod == "GET" && callback.Request.Url?.AbsolutePath == "/" && callback.Request.QueryString["state"] == state) break;
                callback.Response.StatusCode = 400; callback.Response.Close();
            }
            var code = callback.Request.QueryString["code"];
            var denied = callback.Request.QueryString["error"] is not null || string.IsNullOrWhiteSpace(code);
            callback.Response.Headers["Cache-Control"] = "no-store";
            callback.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
            callback.Response.ContentType = "text/plain; charset=utf-8";
            var text = Encoding.UTF8.GetBytes(denied ? "Collegamento annullato. Torna a PulseDeck." : "Autorizzazione ricevuta. Torna a PulseDeck per verificare il collegamento.");
            await callback.Response.OutputStream.WriteAsync(text, pending.Token); callback.Response.Close();
            if (denied) throw new InvalidOperationException();
            using var result = await Token(new() { ["client_id"] = pendingSecret.ClientId, ["client_secret"] = pendingSecret.ClientSecret,
                ["code"] = code!, ["code_verifier"] = verifier, ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code" }, pending.Token);
            var root = result.RootElement;
            if (!root.TryGetProperty("scope", out var granted) || !granted.GetString()!.Split(' ').Contains(Scope)) throw new InvalidOperationException();
            var refresh = root.GetProperty("refresh_token").GetString();
            if (string.IsNullOrWhiteSpace(refresh)) throw new InvalidOperationException();
            await gate.WaitAsync(pending.Token);
            try
            {
                pending.Token.ThrowIfCancellationRequested();
                var saved = pendingSecret with { RefreshToken = refresh };
                credentials.Save(saved); secret = saved; SetAccess(root);
                mailbox.Reset(); nextRead = default; failures = 0;
                status = status with { Connected = true, AuthorizationStatus = "connected" };
            }
            finally { gate.Release(); }
        }
        catch
        {
            await gate.WaitAsync();
            try { if (ReferenceEquals(authorization, pending)) status = status with { AuthorizationStatus = pending.IsCancellationRequested ? "cancelled" : "failed" }; }
            finally { gate.Release(); }
        }
        finally
        {
            listener.Close();
            await gate.WaitAsync();
            try { if (ReferenceEquals(authorization, pending)) authorization = null; }
            finally { gate.Release(); pending.Dispose(); }
        }
    }
    private async Task<JsonDocument> Token(Dictionary<string, string> values, CancellationToken token)
    {
        using var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(values), token);
        if (!response.IsSuccessStatusCode)
        {
            using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (error.RootElement.TryGetProperty("error", out var name) && name.GetString() == "invalid_grant")
                throw new GmailAuthorizationException();
            throw new HttpRequestException("Google authorization unavailable.", null, response.StatusCode);
        }
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
    }
    private void SetAccess(JsonElement root)
    {
        accessToken = root.GetProperty("access_token").GetString() ?? throw new InvalidDataException();
        expires = DateTimeOffset.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32() - 60);
    }
    public async Task Disconnect(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            credentials.Clear(); authorization?.Cancel(); authorization = null;
            secret = null; accessToken = null; mailbox.Reset(); failures = 0;
            status = new(false, false, "idle", new("gmail", "mail", "not-configured")); Publish("not-configured");
        }
        finally { gate.Release(); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await gate.WaitAsync(stoppingToken);
                try
                {
                    if (!config.Current.Notifications.GmailEnabled) { Publish("disabled"); mailbox.Reset(); nextRead = default; }
                    else if (secret?.RefreshToken is null) Publish("not-configured");
                    else if (status.AuthorizationStatus == "reauthorize") Publish("reauthorize");
                    else if (DateTimeOffset.UtcNow >= nextRead)
                    {
                        using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        budget.CancelAfter(TimeSpan.FromSeconds(20));
                        try
                        {
                            if (accessToken is null || expires <= DateTimeOffset.UtcNow)
                            {
                                using var result = await Token(new() { ["client_id"] = secret.ClientId, ["client_secret"] = secret.ClientSecret,
                                    ["refresh_token"] = secret.RefreshToken, ["grant_type"] = "refresh_token" }, budget.Token);
                                SetAccess(result.RootElement);
                            }
                            var update = await mailbox.Read(accessToken!, budget.Token);
                            Publish("connected", update.UnreadCount, update.NewMail);
                            failures = 0; nextRead = DateTimeOffset.UtcNow.AddSeconds(config.Current.Notifications.PollSeconds);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (GmailAuthorizationException) { accessToken = null; status = status with { Connected = false, AuthorizationStatus = "reauthorize" }; Publish("reauthorize"); }
                        catch (Exception e)
                        {
                            if (e is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized }) accessToken = null;
                            Publish("unavailable"); nextRead = DateTimeOffset.UtcNow.AddSeconds(Math.Min(900, 30 * Math.Pow(2, Math.Min(++failures, 5))));
                        }
                    }
                }
                finally { gate.Release(); }
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            authorization?.Cancel();
            if (authorizationTask is not null) await authorizationTask;
        }
    }
    private sealed class GmailAuthorizationException : Exception;
}
