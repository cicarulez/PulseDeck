using PulseDeck.Core;
using Xunit;

public class PlaybackClockTests
{
    [Fact]
    public void InterpolationRespectsPauseRateDurationSeekAndStaleSamples()
    {
        var sample = new MediaSnapshot(true, "Track", "Artist", "Spotify", 10, 60, "connected") { PlaybackRate = 2 };
        Assert.Equal(11, PlaybackClock.Advance(sample, .5).PositionSeconds);
        Assert.Equal(10, PlaybackClock.Advance(sample with { Playing = false }, .5).PositionSeconds);
        Assert.Equal(60, PlaybackClock.Advance(sample with { PositionSeconds = 59 }, .8).PositionSeconds);
        Assert.Equal(14, PlaybackClock.Advance(sample, 50).PositionSeconds);
        Assert.Equal(3, PlaybackClock.Advance(sample with { PositionSeconds = 2 }, .5).PositionSeconds);
        Assert.Same(sample, PlaybackClock.Advance(sample, double.NaN));
        Assert.Equal(10, PlaybackClock.Advance(sample with { Status = "unavailable" }, .5).PositionSeconds);
    }
}
