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
builder.Services.AddSingleton<MediaProvider>();
builder.Services.AddSingleton<ForegroundProvider>();
builder.Services.AddHttpClient("weather", client => { client.Timeout = TimeSpan.FromSeconds(5); client.MaxResponseContentBufferSize = 65536; });
builder.Services.AddSingleton(provider => new WeatherFeed(provider.GetRequiredService<IHttpClientFactory>().CreateClient("weather")));
builder.Services.AddHttpClient("news", client => { client.Timeout = TimeSpan.FromSeconds(8); client.DefaultRequestHeaders.UserAgent.ParseAdd("PulseDeck/0.2.5"); })
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
