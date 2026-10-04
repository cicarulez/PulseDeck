using Microsoft.AspNetCore.SignalR;
using PulseDeck.Agent.Configuration;
using PulseDeck.Agent.Display;
using PulseDeck.Agent.Providers;
using PulseDeck.Agent.Rendering;
using PulseDeck.Core;

namespace PulseDeck.Agent;

public sealed class DeckHub : Hub;

public sealed class DeckRuntime(ConfigStore config, HardwareProvider hardware, MediaProvider media,
    DiscordProvider discord, ForegroundProvider foreground, GameSessionProvider sessions, GameArtworkProvider gameArtwork, GameDiscoveryService discovery, LyricsProvider lyrics, SpotifyService spotify, PresentMonProvider fps, VolumeProvider volume, WeatherFeed weather, NewsFeed news, DeckRenderer renderer, TurzxDisplay display, IHubContext<DeckHub> hub,
    CalendarFeed calendar, CalendarCredentials calendarCredentials, NotificationCenter notifications, AuraColorProvider aura, AudioSpectrumProvider spectrum, ILogger<DeckRuntime> logger) : BackgroundService
{
    private readonly CalendarArrivalTracker calendarArrivals = new();
    private readonly ProfileSelector selector = new();
    private readonly GameSceneSelector gameSelector = new();
    public DeckState State { get; private set; } = new(DateTimeOffset.UtcNow, "desktop", "",
        new([], "starting"), new(false, "", "", "", 0, 0, "starting"), new([], null, "starting"), new(false, "", null, "disconnected"));
    public byte[]? Preview { get; private set; }

    private sealed record RenderInput(DeckState State, DeckConfig Config, MediaArtwork? Artwork, ApplicationIcon? Icon, long MediaSampleTimestamp);
    private sealed record DisplayFrame(byte[] Pixels, bool Animated, long Revision);
    private RenderInput? latest;
    private DisplayFrame? latestFrame;
    public long ProviderUpdates => Interlocked.Read(ref providerUpdates);
    public long RenderUpdates => Interlocked.Read(ref renderUpdates);
    private long providerUpdates, renderUpdates;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.WhenAll(Collect(stoppingToken), Task.Run(() => Render(stoppingToken), stoppingToken),
            Task.Run(() => Deliver(stoppingToken), stoppingToken)); }
        finally { fps.Dispose(); display.Shutdown(); }
    }

    private async Task<(MediaSnapshot Snapshot, long Timestamp)> ReadMedia(CancellationToken token)
    {
        var value = await media.ReadAsync(token);
        return (value, System.Diagnostics.Stopwatch.GetTimestamp());
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
                    var mediaTask = ReadMedia(stoppingToken);
                    var discordTask = discord.ReadAsync(settings, stoppingToken);
                    await Task.WhenAll(hardwareTask, mediaTask, discordTask);
                    var now = DateTimeOffset.UtcNow;
                    var active = foreground.Read(settings);
                    settings = discovery.EffectiveConfig(settings);
                    var profile = selector.Select(settings, active.ProcessName, mediaTask.Result.Snapshot.Playing, now, active.IsGame, mediaTask.Result.Snapshot);
                    var game = gameSelector.Select(settings, profile, active, now);
                    var session = sessions.Read(game, now);
                    if (game is not null && session is null && active.ProcessId != game.ProcessId) game = null;
                    var frameRate = fps.Read(profile == "gaming" ? session : null, settings, now);
                    var spotifyState = spotify.Read(mediaTask.Result.Snapshot, now);
                    var localMedia = mediaTask.Result.Snapshot with { Artists = spotifyState.Current?.Artists ?? [] };
                    spectrum.Select(localMedia, settings.MusicSpectrum && profile == "music" && settings.SpotifyLyrics);
                    var next = new DeckState(now, profile, active.ProcessName,
                        hardwareTask.Result, localMedia, discordTask.Result, display.Status, frameRate.Status)
                        { Foreground = active, Game = game, GameSession = session, Fps = frameRate, Volume = volume.Read(),
                            GameArtwork = gameArtwork.Read(game, stoppingToken),
                            Lyrics = lyrics.Read(mediaTask.Result.Snapshot, profile, settings.SpotifyLyrics, stoppingToken),
                            SpotifyTransition = selector.SpotifyTransition,
                            Spotify = spotifyState, Aura = aura.Read(settings.AuraEnabled), AudioSpectrum = spectrum.Read(),
                            News = news.Read(settings.News, stoppingToken),
                            Calendar = calendar.Read(settings.Calendar, calendarCredentials.Read(), stoppingToken),
                            Weather = weather.Read(settings.WeatherLocation, settings.Layout == "weather", stoppingToken) };
                    var addedEvents = calendarArrivals.Observe(next.Calendar, calendar.SourceRevision, settings.Calendar.NotifyNewEvents);
                    var calendarStatus = next.Calendar.Status is "connected" or "empty" ? "connected" : next.Calendar.Status;
                    notifications.Publish(new("calendar", "calendar", calendarStatus, UpdatedAt: next.Calendar.FetchedAt),
                        addedEvents == 0 ? null : new("calendar", "calendar",
                            addedEvents == 1 ? "Nuovo evento nel calendario" : $"{addedEvents} nuovi eventi nel calendario", "GOOGLE CALENDAR"),
                        settings.Notifications.AnimatesIn(profile) && settings.Calendar.NotifyNewEvents);
                    Volatile.Write(ref latest, new(next, settings, media.Artwork, foreground.Icon, mediaTask.Result.Timestamp));
                    Interlocked.Increment(ref providerUpdates);
                    State = next with { Display = display.Status, Notifications = notifications.Read(settings.Notifications.AnimatesIn(next.Profile)) };
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
                    var visual = notifications.Read(input.Config.Notifications.AnimatesIn(input.State.Profile));
                    var animated = visual.Arrival is not null && visual.Seconds < 3.2f || input.Config.AuraEnabled
                        || input.State.Media.Playing && SpotifyLyrics.Show(input.State, input.Config);
                    var interval = animated ? 100 : 1000;
                    if (watch.Elapsed.TotalMilliseconds - last >= interval)
                    {
                        last = watch.Elapsed.TotalMilliseconds;
                        try
                        {
                            // A single renderer publishes immutable frames; USB consumes only the latest.
                            var age = System.Diagnostics.Stopwatch.GetElapsedTime(input.MediaSampleTimestamp).TotalSeconds;
                            var rendered = renderer.Render(input.State with { Media = PlaybackClock.Advance(input.State.Media, age),
                                Notifications = visual, Aura = aura.Read(input.Config.AuraEnabled), AudioSpectrum = spectrum.Read() }, input.Config, input.Artwork, input.Icon);
                            Preview = rendered.Png;
                            var revision = Interlocked.Increment(ref renderUpdates);
                            Volatile.Write(ref latestFrame, new(rendered.Pixels, animated, revision));
                            // Preview follows rendered frames independently of the slower sensor updates.
                            await hub.Clients.All.SendAsync("frame", revision, token);
                        }
                        catch (Exception e) { logger.LogError(e, "Could not render the panel."); }
                    }
                }
                await Task.Delay(5, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async Task Deliver(CancellationToken token)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var last = -1000d;
        long revision = -1;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var frame = Volatile.Read(ref latestFrame);
                if (frame is not null && frame.Revision != revision
                    && watch.Elapsed.TotalMilliseconds - last >= (frame.Animated ? 250 : 1000))
                {
                    last = watch.Elapsed.TotalMilliseconds;
                    revision = frame.Revision;
                    try
                    {
                        // One synchronous USB writer, no queue. Intermediate rendered frames
                        // are discarded while USB is busy; retain the verified full-frame path.
                        display.ApplyBrightness();
                        display.Send(frame.Pixels, fullFrame: frame.Animated);
                    }
                    catch (Exception e) { logger.LogError(e, "Could not deliver the panel frame."); }
                }
                await Task.Delay(5, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
