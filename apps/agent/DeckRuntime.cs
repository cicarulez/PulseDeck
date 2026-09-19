using Microsoft.AspNetCore.SignalR;
using PulseDeck.Agent.Configuration;
using PulseDeck.Agent.Display;
using PulseDeck.Agent.Providers;
using PulseDeck.Agent.Rendering;
using PulseDeck.Core;

namespace PulseDeck.Agent;

public sealed class DeckHub : Hub;

public sealed class DeckRuntime(ConfigStore config, HardwareProvider hardware, MediaProvider media,
    DiscordProvider discord, ForegroundProvider foreground, GameSessionProvider sessions, GameArtworkProvider gameArtwork, GameDiscoveryService discovery, LyricsProvider lyrics, PresentMonProvider fps, VolumeProvider volume, WeatherFeed weather, NewsFeed news, DeckRenderer renderer, TurzxDisplay display, IHubContext<DeckHub> hub,
    CalendarFeed calendar, CalendarCredentials calendarCredentials, NotificationCenter notifications, ILogger<DeckRuntime> logger) : BackgroundService
{
    private readonly CalendarArrivalTracker calendarArrivals = new();
    private readonly ProfileSelector selector = new();
    private readonly GameSceneSelector gameSelector = new();
    public DeckState State { get; private set; } = new(DateTimeOffset.UtcNow, "desktop", "",
        new([], "starting"), new(false, "", "", "", 0, 0, "starting"), new([], null, "starting"), new(false, "", null, "disconnected"));
    public byte[]? Preview { get; private set; }

    private sealed record RenderInput(DeckState State, DeckConfig Config, MediaArtwork? Artwork, ApplicationIcon? Icon);
    private RenderInput? latest;
    public long ProviderUpdates => Interlocked.Read(ref providerUpdates);
    public long RenderUpdates => Interlocked.Read(ref renderUpdates);
    private long providerUpdates, renderUpdates;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.WhenAll(Collect(stoppingToken), Task.Run(() => Render(stoppingToken), stoppingToken)); }
        finally { fps.Dispose(); display.Shutdown(); }
    }

    private async Task Collect(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                try
                {
                    var settings = config.Current;
                    var hardwareTask = Task.Run(hardware.Read, stoppingToken);
                    var mediaTask = media.ReadAsync(stoppingToken);
                    var discordTask = discord.ReadAsync(settings, stoppingToken);
                    await Task.WhenAll(hardwareTask, mediaTask, discordTask);
                    var now = DateTimeOffset.UtcNow;
                    var active = foreground.Read(settings);
                    settings = discovery.EffectiveConfig(settings);
                    var profile = selector.Select(settings, active.ProcessName, mediaTask.Result.Playing, now, active.IsGame, mediaTask.Result);
                    var game = gameSelector.Select(settings, profile, active, now);
                    var session = sessions.Read(game, now);
                    if (game is not null && session is null && active.ProcessId != game.ProcessId) game = null;
                    var frameRate = fps.Read(profile == "gaming" ? session : null, settings, now);
                    var next = new DeckState(now, profile, active.ProcessName,
                        hardwareTask.Result, mediaTask.Result, discordTask.Result, display.Status, frameRate.Status)
                        { Foreground = active, Game = game, GameSession = session, Fps = frameRate, Volume = volume.Read(),
                            GameArtwork = gameArtwork.Read(game, stoppingToken),
                            Lyrics = lyrics.Read(mediaTask.Result, profile, settings.SpotifyLyrics, stoppingToken),
                            SpotifyTransition = selector.SpotifyTransition,
                            News = news.Read(settings.News, stoppingToken),
                            Calendar = calendar.Read(settings.Calendar, calendarCredentials.Read(), stoppingToken),
                            Weather = weather.Read(settings.WeatherLocation, settings.Layout == "weather", stoppingToken) };
                    var addedEvents = calendarArrivals.Observe(next.Calendar, calendar.SourceRevision, settings.Calendar.NotifyNewEvents);
                    var calendarStatus = next.Calendar.Status is "connected" or "empty" ? "connected" : next.Calendar.Status;
                    notifications.Publish(new("calendar", "calendar", calendarStatus, UpdatedAt: next.Calendar.FetchedAt),
                        addedEvents == 0 ? null : new("calendar", "calendar",
                            addedEvents == 1 ? "Nuovo evento nel calendario" : $"{addedEvents} nuovi eventi nel calendario", "GOOGLE CALENDAR"),
                        settings.Notifications.Animate && settings.Calendar.NotifyNewEvents && profile == "desktop");
                    Volatile.Write(ref latest, new(next, settings, media.Artwork, foreground.Icon));
                    Interlocked.Increment(ref providerUpdates);
                    State = next with { Display = display.Status, Notifications = notifications.Read(settings.Notifications.Animate && next.Profile == "desktop") };
                    await hub.Clients.All.SendAsync("state", State, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception e) { logger.LogError(e, "Could not update the panel; retrying next tick."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task Render(CancellationToken token)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var last = -1000d;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var input = Volatile.Read(ref latest);
                if (input is not null)
                {
                    var visual = notifications.Read(config.Current.Notifications.Animate && input.State.Profile == "desktop");
                    var interval = visual.Arrival is not null && visual.Seconds < 3.2f ? 100 : 1000;
                    if (watch.Elapsed.TotalMilliseconds - last >= interval)
                    {
                        last = watch.Elapsed.TotalMilliseconds;
                        try
                        {
                            // One renderer and one synchronous USB writer. Nothing is queued while USB is busy.
                            var rendered = renderer.Render(input.State with { Notifications = visual }, input.Config, input.Artwork, input.Icon);
                            Preview = rendered.Png;
                            display.Send(rendered.Pixels, fullFrame: visual.Arrival is not null || visual.IsTest);
                            Interlocked.Increment(ref renderUpdates);
                        }
                        catch (Exception e) { logger.LogError(e, "Could not render the panel."); }
                    }
                }
                await Task.Delay(25, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
