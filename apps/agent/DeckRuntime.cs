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
                    var foreground = ForegroundProvider.Read();
                    var next = new DeckState(now, selector.Select(settings, foreground, mediaTask.Result.Playing, now), foreground,
                        hardwareTask.Result, mediaTask.Result, discordTask.Result, display.Status);
                    var rendered = renderer.Render(next, settings);
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
