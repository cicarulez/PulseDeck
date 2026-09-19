using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PulseDeck.Agent.Configuration;
using PulseDeck.Agent.Providers;
using PulseDeck.Core;

// Windows-only synthetic test. No Google traffic, browser or serial access.
var directory = Path.Combine(Path.GetTempPath(), "PulseDeck-notifications-" + Guid.NewGuid().ToString("N"));
Environment.SetEnvironmentVariable("PULSEDECK_DATA_DIR", directory);
try
{
    var config = new ConfigStore(NullLogger<ConfigStore>.Instance);
    var credentials = new GmailCredentials(config);
    var fake = new Google(); var center = new NotificationCenter();
    using var service = new GmailService(config, credentials, center, fake);
    const string json = """{"installed":{"client_id":"synthetic.apps.googleusercontent.com","client_secret":"synthetic-client-secret"}}""";
    await service.Import(json, default);
    var bytes = File.ReadAllBytes(Path.Combine(directory, "gmail.credentials"));
    Check(!Encoding.UTF8.GetString(bytes).Contains("synthetic"), "DPAPI encryption");
    Check(credentials.Read()?.ClientSecret == "synthetic-client-secret", "DPAPI reload");
    var url = new Uri(await service.Connect(default));
    var values = Query(url.Query);
    Check(values["scope"] == "https://www.googleapis.com/auth/gmail.metadata", "minimal scope");
    Check(values["code_challenge_method"] == "S256", "PKCE method");
    fake.Challenge = values["code_challenge"];
    using var callback = new HttpClient();
    var redirect = values["redirect_uri"];
    Check(new Uri(redirect).Host == "127.0.0.1", "loopback listener");
    using var wrong = await callback.GetAsync(redirect + "?code=synthetic&state=wrong");
    Check(wrong.StatusCode == HttpStatusCode.BadRequest, "wrong OAuth state rejected");
    using var good = await callback.GetAsync(redirect + "?code=synthetic&state=" + values["state"]);
    good.EnsureSuccessStatusCode();
    await Until(() => service.Status.Connected);
    Check(credentials.Read()?.RefreshToken == "synthetic-refresh", "encrypted refresh persistence");
    Check(!JsonSerializer.Serialize(service.Status).Contains("synthetic"), "no credential exposure");
    await service.StartAsync(default);
    await Until(() => service.Status.Source.Status == "connected");
    Check(service.Status.Source.UnreadCount == 7 && center.Read().Arrival is null, "baseline count without animation");
    await service.StopAsync(default);
    // Restart forces refresh, and never replays the old unread baseline.
    using var resumed = new GmailService(config, credentials, center, fake);
    await resumed.StartAsync(default);
    await Until(() => resumed.Status.Source.Status == "connected");
    Check(fake.Refreshes == 1 && resumed.Status.Source.UnreadCount == 7, "refresh after restart");
    var pendingUrl = new Uri(await resumed.Connect(default));
    await resumed.Disconnect(default);
    Check(!File.Exists(Path.Combine(directory, "gmail.credentials")) && !resumed.Status.Connected, "disconnect clears credentials");
    await Task.Delay(100);
    Check(!resumed.Status.ClientConfigured, "pending callback cannot restore disconnected account");
    await resumed.StopAsync(default);
    Console.WriteLine("PASS: Windows DPAPI, reload, minimal scope, PKCE, loopback state, authorization, refresh, baseline, credential privacy and disconnect. Synthetic Google transport; no Gmail or display accessed.");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

static Dictionary<string, string> Query(string query) => query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
    .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("Failed: " + name); }
static async Task Until(Func<bool> condition)
{
    for (var i = 0; i < 100; i++) { if (condition()) return; await Task.Delay(50); }
    throw new TimeoutException("Synthetic integration timed out.");
}
sealed class Google : HttpMessageHandler, IHttpClientFactory
{
    public string Challenge = "";
    public int Refreshes;
    public HttpClient CreateClient(string name) => new(this, false);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string json;
        if (request.RequestUri!.AbsoluteUri == "https://oauth2.googleapis.com/token")
        {
            var form = (await request.Content!.ReadAsStringAsync(cancellationToken)).Split('&').Select(p => p.Split('=', 2))
                .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
            if (form["grant_type"] == "authorization_code")
            {
                var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                if (challenge != Challenge) throw new InvalidOperationException("PKCE mismatch");
            }
            else Interlocked.Increment(ref Refreshes);
            json = """{"access_token":"synthetic-access","refresh_token":"synthetic-refresh","scope":"https://www.googleapis.com/auth/gmail.metadata","expires_in":3600}""";
        }
        else if (request.RequestUri.AbsolutePath.EndsWith("/profile")) json = """{"historyId":"100"}""";
        else if (request.RequestUri.AbsolutePath.EndsWith("/labels/INBOX")) json = """{"messagesUnread":7}""";
        else throw new InvalidOperationException("Unexpected synthetic request");
        return new(HttpStatusCode.OK) { Content = new StringContent(json) };
    }
}
