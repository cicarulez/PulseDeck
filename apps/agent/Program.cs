using Microsoft.AspNetCore.Http.Json;
using PulseDeck.Agent;
using PulseDeck.Agent.Configuration;
using PulseDeck.Agent.Display;
using PulseDeck.Agent.Providers;
using PulseDeck.Agent.Rendering;
using PulseDeck.Core;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseUrls("http://127.0.0.1:5178");
builder.Services.AddSignalR();
builder.Services.AddSingleton<ConfigStore>();
builder.Services.AddSingleton<HardwareProvider>();
builder.Services.AddSingleton<BrowserMediaStore>();
builder.Services.AddSingleton<BrowserTabStore>();
builder.Services.AddHttpClient("youtube-artwork", client =>
    { client.Timeout = TimeSpan.FromSeconds(1); client.MaxResponseContentBufferSize = MediaArtwork.MaximumBytes; })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddSingleton<MediaProvider>();
builder.Services.AddHttpClient("lyrics", client => { client.Timeout = TimeSpan.FromSeconds(8); client.MaxResponseContentBufferSize = 256 * 1024; client.DefaultRequestHeaders.UserAgent.ParseAdd("PulseDeck/0.7.9"); })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddSingleton(p => new LyricsProvider(p.GetRequiredService<IHttpClientFactory>().CreateClient("lyrics"), p.GetRequiredService<ConfigStore>()));
builder.Services.AddSingleton<InstalledGameCatalog>();
builder.Services.AddSingleton<GameDiscoveryService>();
builder.Services.AddSingleton<GameThumbnailProvider>();
builder.Services.AddHostedService(p => p.GetRequiredService<GameDiscoveryService>());
builder.Services.AddSingleton<ForegroundProvider>();
builder.Services.AddSingleton<GameSessionProvider>();
builder.Services.AddSingleton<SteamGridCredentials>();
builder.Services.AddHttpClient("steamgriddb", client => { client.Timeout = TimeSpan.FromSeconds(8); client.MaxResponseContentBufferSize = 4 * 1024 * 1024; })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddSingleton(provider => new SteamGridArtwork(provider.GetRequiredService<IHttpClientFactory>().CreateClient("steamgriddb"), provider.GetRequiredService<ConfigStore>(), provider.GetRequiredService<SteamGridCredentials>()));
builder.Services.AddSingleton<PresentMonProvider>();
builder.Services.AddSingleton<VolumeProvider>();
builder.Services.AddHttpClient("game-artwork", client => { client.Timeout = TimeSpan.FromSeconds(10); client.MaxResponseContentBufferSize = 4 * 1024 * 1024; });
builder.Services.AddSingleton(provider => new GameArtworkProvider(provider.GetRequiredService<IHttpClientFactory>().CreateClient("game-artwork"), provider.GetRequiredService<ConfigStore>(), provider.GetRequiredService<SteamGridArtwork>()));
builder.Services.AddHttpClient("weather", client => { client.Timeout = TimeSpan.FromSeconds(5); client.MaxResponseContentBufferSize = 65536; });
builder.Services.AddSingleton(provider => new WeatherFeed(provider.GetRequiredService<IHttpClientFactory>().CreateClient("weather")));
builder.Services.AddHttpClient("news", client => { client.Timeout = TimeSpan.FromSeconds(8); client.DefaultRequestHeaders.UserAgent.ParseAdd("PulseDeck/0.7.9"); })
    .ConfigurePrimaryHttpMessageHandler(NewsHttp.CreateHandler);
builder.Services.AddSingleton(provider => new NewsFeed(provider.GetRequiredService<IHttpClientFactory>().CreateClient("news")));
builder.Services.AddSingleton<EmbeddedDiscordService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<EmbeddedDiscordService>());
builder.Services.AddHttpClient<DiscordProvider>(client => client.Timeout = TimeSpan.FromMilliseconds(1200));
builder.Services.AddSingleton<DeckRenderer>();
builder.Services.AddSingleton<TurzxDisplay>();
builder.Services.AddSingleton<DeckRuntime>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DeckRuntime>());
builder.Services.AddHostedService<WindowsSessionLifetime>();
var app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Host.Host is not ("127.0.0.1" or "localhost" or "[::1]")) { context.Response.StatusCode = 403; return; }
    // The extension may only write its two browser endpoints, never configuration/control APIs.
    if (context.Request.Path == "/api/browser-media" || context.Request.Path == "/api/browser-tab")
    {
        if (context.Request.Headers.Origin != BrowserMediaStore.ExtensionOrigin)
        { context.Response.StatusCode = 403; return; }
        context.Response.Headers.AccessControlAllowOrigin = BrowserMediaStore.ExtensionOrigin;
        context.Response.Headers.CacheControl = "no-store";
        if (context.Request.Method == "OPTIONS")
        {
            context.Response.Headers.AccessControlAllowMethods = "POST";
            context.Response.Headers.AccessControlAllowHeaders = "Content-Type, X-PulseDeck-Client";
            context.Response.StatusCode = 204; return;
        }
        if (context.Request.Method != "POST" || context.Request.Headers["X-PulseDeck-Client"] != "youtube-extension")
        { context.Response.StatusCode = 403; return; }
        var size = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (size is { IsReadOnly: false }) size.MaxRequestBodySize = context.Request.Path == "/api/browser-tab" ? 65536 : 8192;
        await next(); return;
    }
    if (context.Request.Headers.Origin.Count > 0 && (!Uri.TryCreate(context.Request.Headers.Origin, UriKind.Absolute, out var origin)
        || !origin.IsLoopback || origin.Scheme != "http" || origin.Port is not (5178 or 4200)))
    { context.Response.StatusCode = 403; return; }
    if (context.Request.Path.StartsWithSegments("/api") && context.Request.Method is "POST" or "PUT" or "DELETE"
        && context.Request.Headers["X-PulseDeck-Client"] != "configurator")
    { context.Response.StatusCode = 403; return; }
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/health", () => new { ok = true, app = "PulseDeck", version = typeof(DeckRuntime).Assembly.GetName().Version?.ToString(3) });
app.MapPost("/api/browser-media", (BrowserMediaUpdate update, BrowserMediaStore store) =>
    store.Update(update.Media) ? Results.Ok(new { connected = true }) : Results.BadRequest());
app.MapPost("/api/browser-tab", (BrowserTabUpdate update, BrowserTabStore store) =>
    store.Update(update.Tab) ? Results.Ok(new { connected = true }) : Results.BadRequest());
app.MapGet("/api/state", (DeckRuntime runtime) => runtime.State);
app.MapGet("/api/widget-slots", () => new { slots = WidgetCatalog.Slots, defaults = WidgetCatalog.Defaults() });
app.MapGet("/api/sensors", (DeckRuntime runtime) => runtime.State.Hardware);
app.MapGet("/api/weather/locations", async (string? query, IHttpClientFactory clients, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(query) || query.Trim().Length is < 2 or > 100)
        return Results.BadRequest(new { error = "Inserisci un nome di località tra 2 e 100 caratteri." });
    try
    {
        using var client = clients.CreateClient("weather");
        var json = await client.GetStringAsync("https://geocoding-api.open-meteo.com/v1/search?count=8&language=it&name=" + Uri.EscapeDataString(query.Trim()), token);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var results = new List<object>();
        if (document.RootElement.TryGetProperty("results", out var places))
            foreach (var place in places.EnumerateArray().Take(8))
            {
                string Text(string name) => place.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
                var location = new WeatherLocation(Text("name"), place.GetProperty("latitude").GetDouble(), place.GetProperty("longitude").GetDouble());
                if (location.IsValid) results.Add(new { location.Name, location.Latitude, location.Longitude,
                    label = string.Join(", ", new[] { location.Name, Text("admin2"), Text("admin1"), Text("country") }.Where(s => s.Length > 0).Distinct()) });
            }
        return Results.Ok(results);
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { return Results.StatusCode(499); }
    catch { return Results.Json(new { error = "Ricerca meteo non disponibile. Riprova tra poco." }, statusCode: 502); }
});
app.MapGet("/api/games", (GameDiscoveryService games) => games.Status);
app.MapGet("/api/games/thumbnail/{id}", async (string id, GameDiscoveryService games, GameThumbnailProvider thumbnails, CancellationToken token) =>
{
    var title = games.ThumbnailTitle(id);
    if (title is null) return Results.NotFound();
    var image = await thumbnails.Read(title).WaitAsync(token);
    return image is null ? Results.NotFound() : Results.File(image, "image/jpeg");
});
app.MapPost("/api/games/scan", (GameDiscoveryService games) => { games.RequestScan(); return Results.Ok(new { requested = true }); });
app.MapGet("/api/steamgriddb", (SteamGridCredentials credentials) => new { configured = credentials.Configured });
app.MapPost("/api/steamgriddb", async (SteamGridKeyRequest request, SteamGridArtwork artwork, SteamGridCredentials credentials, CancellationToken token) =>
{
    var key = request.Key?.Trim() ?? "";
    if (!await artwork.CheckKey(key, token)) return Results.BadRequest(new { error = "Chiave non verificata: controlla la chiave e la connessione a SteamGridDB." });
    try { credentials.Save(key); return Results.Ok(new { configured = true }); }
    catch { return Results.Problem("Impossibile salvare la chiave cifrata sul PC."); }
});
app.MapDelete("/api/steamgriddb", (SteamGridCredentials credentials) =>
{
    try { credentials.Clear(); return Results.Ok(new { configured = false }); }
    catch { return Results.Problem("Impossibile rimuovere la chiave dal PC."); }
});
app.MapGet("/api/config", (ConfigStore store) => store.Current);
app.MapPut("/api/config", (DeckConfig config, ConfigStore store) =>
{
    try { store.Save(config); return Results.Ok(store.Current); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
    catch (IOException) { return Results.Problem("Could not save the configuration."); }
});
app.MapGet("/api/preview.png", (DeckRuntime runtime) => runtime.Preview is { } png ? Results.File(png, "image/png") : Results.StatusCode(503));
app.MapGet("/api/rendering", (DeckRenderer renderer) => new { backgroundStatus = renderer.BackgroundStatus, backgroundFrames = renderer.BackgroundFrameCount });
app.MapGet("/api/display", (TurzxDisplay display) => display.Status);
app.MapGet("/api/display/ports", TurzxDisplay.Ports);
app.MapPost("/api/display/connect", (ConfigStore store, TurzxDisplay display, bool? startup) =>
{
    var result = display.Connect(store.Current.DisplayPort, startup == true);
    return result.Connected ? Results.Ok(result) : Results.Conflict(result);
});
app.MapPost("/api/display/disconnect", (TurzxDisplay display) => { display.Disconnect(); return Results.Ok(display.Status); });
app.MapPost("/api/stop", (IHostApplicationLifetime lifetime) => { lifetime.StopApplication(); return Results.Ok(new { stopped = true }); });
app.MapHub<DeckHub>("/live");
app.MapFallbackToFile("index.html");
app.Run();
