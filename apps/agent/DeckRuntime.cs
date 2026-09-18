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
    CalendarFeed calendar, CalendarCredentials calendarCredentials, ILogger<DeckRuntime> logger) : BackgroundService
{
    private readonly ProfileSelector selector = new();
    private readonly GameSceneSelector gameSelector = new();
    public DeckState State { get; private set; } = new(DateTimeOffset.UtcNow, "desktop", "",
        new([], "starting"), new(false, "", "", "", 0, 0, "starting"), new([], null, "starting"), new(false, "", null, "disconnected"));
    public byte[]? Preview { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
                    var rendered = renderer.Render(next, settings, media.Artwork, foreground.Icon);
                    Preview = rendered.Png;
                    display.Send(rendered.Pixels);
                    State = next with { Display = display.Status };
                    await hub.Clients.All.SendAsync("state", State, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception e) { logger.LogError(e, "Could not update the panel; retrying next tick."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { fps.Dispose(); display.Shutdown(); }
    }
}
