using Microsoft.AspNetCore.SignalR;
using PulseDeck.Agent.Configuration;
using PulseDeck.Agent.Display;
using PulseDeck.Agent.Providers;
using PulseDeck.Agent.Rendering;
using PulseDeck.Core;

namespace PulseDeck.Agent;

public sealed class DeckHub : Hub;

public sealed class DeckRuntime(ConfigStore config, HardwareProvider hardware, MediaProvider media,
    DiscordProvider discord, DeckRenderer renderer, TurzxDisplay display, IHubContext<DeckHub> hub,
    ILogger<DeckRuntime> logger) : BackgroundService
{
    private readonly ProfileSelector selector = new();
    private readonly AnimationGuard animation = new();
    public DeckState State { get; private set; } = new(DateTimeOffset.UtcNow, "desktop", "",
        new([], "starting"), new(false, "", "", "", 0, 0, "starting"), new([], null, "starting"), new(false, "", null, "disconnected"));
    public byte[]? Preview { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        long sampled = 0, renderedAt = 0;
        try
        {
            do
            {
                try
                {
                    var settings = config.Current;
                    var animationStatus = animation.Select(settings.AnimateBackground, settings.BackgroundPath, display.Status);
                    var renderSettings = animationStatus == "suspended" ? settings with { AnimateBackground = false } : settings;
                    if (renderedAt != 0 && System.Diagnostics.Stopwatch.GetElapsedTime(renderedAt).TotalMilliseconds < (renderSettings.AnimateBackground ? 450 : 950)) continue;
                    renderedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                    var hardwareState = State.Hardware;
                    var mediaState = State.Media;
                    var discordState = State.Discord;
                    if (sampled == 0 || System.Diagnostics.Stopwatch.GetElapsedTime(sampled).TotalMilliseconds >= 950)
                    {
                        sampled = System.Diagnostics.Stopwatch.GetTimestamp();
                        var hardwareTask = Task.Run(hardware.Read, stoppingToken);
                        var mediaTask = media.ReadAsync(stoppingToken);
                        var discordTask = discord.ReadAsync(settings, stoppingToken);
                        await Task.WhenAll(hardwareTask, mediaTask, discordTask);
                        hardwareState = hardwareTask.Result; mediaState = mediaTask.Result; discordState = discordTask.Result;
                    }
                    var now = DateTimeOffset.UtcNow;
                    var foreground = ForegroundProvider.Read();
                    var next = new DeckState(now, selector.Select(settings, foreground, mediaState.Playing, now), foreground,
                        hardwareState, mediaState, discordState, display.Status) { AnimationStatus = animationStatus };
                    var rendered = renderer.Render(next, renderSettings, media.Artwork);
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
        finally { display.Shutdown(); }
    }
}
