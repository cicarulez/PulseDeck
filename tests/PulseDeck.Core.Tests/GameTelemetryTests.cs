using PulseDeck.Core;
using Xunit;

public class GameTelemetryTests
{
    [Fact]
    public void SessionContinuesAcrossAltTabAndAgentRestartAndResetsForNewProcess()
    {
        var started = DateTimeOffset.UnixEpoch;
        var first = GameSessions.FromProcess(42, "bf6", started, started.AddMinutes(10));
        var returned = GameSessions.FromProcess(42, "bf6", started, started.AddMinutes(15));
        Assert.Equal(first.StartedAt, returned.StartedAt);
        Assert.Equal(900, returned.ElapsedSeconds);
        var restarted = GameSessions.FromProcess(42, "bf6", started.AddMinutes(15), started.AddMinutes(16));
        Assert.Equal(60, restarted.ElapsedSeconds);
        Assert.NotEqual(first.StartedAt, restarted.StartedAt);
    }

    [Theory]
    [InlineData("msBetweenPresents")] // Actual PresentMon 2.5.1 --v1_metrics header.
    [InlineData("MsBetweenPresents")]
    public void PresentMonFiltersPidSelectsMainSwapChainAndExpiresOldFrames(string intervalColumn)
    {
        var samples = new PresentMonSamples(42);
        var now = DateTimeOffset.UnixEpoch;
        samples.Add("Application,ProcessID,SwapChainAddress," + intervalColumn, now);
        samples.Add("\"game, title.exe\",42,0x1,10", now);
        samples.Add("game.exe,42,0x1,10", now);
        samples.Add("game.exe,42,0x2,100", now);
        samples.Add("browser.exe,43,0x1,1", now);
        samples.Add("game.exe,42,0x1,NaN", now);
        samples.Add("game.exe,42,0x1,0", now);
        Assert.Equal(3, samples.Read(now).SamplesAccepted);
        Assert.Equal(100, samples.Read(now).FramesPerSecond);
        Assert.Equal(10, samples.Read(now).FrameTimeMs);
        Assert.Null(samples.Read(now.AddSeconds(3)).FramesPerSecond);
    }
}
