using PulseDeck.Core;
using Xunit;

public class BrowserMediaTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static BrowserMedia Video(bool playing = true) => new("abcdefghijk", "Video", "Author", playing, 20, 100, 1);

    [Fact]
    public void ExtensionExpiresAndClearImmediatelyRestoresWindowsFallback()
    {
        var clock = new Clock(); var store = new BrowserMediaStore(clock);
        Assert.Null(store.Read());
        Assert.True(store.Update(Video()));
        clock.Now = clock.Now.AddSeconds(3);
        Assert.Equal(23, store.Read()!.PositionSeconds);
        clock.Now = clock.Now.AddSeconds(7);
        Assert.Null(store.Read());
        store.Update(Video(false));
        clock.Now = clock.Now.AddSeconds(3);
        Assert.Equal(20, store.Read()!.PositionSeconds);
        store.Update(null);
        Assert.Null(store.Read());
    }
    [Fact]
    public void RejectsInvalidInputWithoutReplacingValidVideo()
    {
        var store = new BrowserMediaStore(); store.Update(Video());
        Assert.False(store.Update(Video() with { VideoId = "../../secrets" }));
        Assert.False(store.Update(Video() with { PositionSeconds = double.NaN }));
        Assert.False(store.Update(Video() with { PlaybackRate = -1 }));
        Assert.False(store.Update(Video() with { Title = new string('x', 301) }));
        Assert.Equal("abcdefghijk", store.Read()!.VideoId);
    }
    [Theory]
    [InlineData(true, "Spotify.exe", true, false)]
    [InlineData(true, "Other player", true, false)]
    [InlineData(true, "Spotify.exe", false, true)]
    [InlineData(false, "Spotify.exe", false, false)]
    [InlineData(false, "Chrome", true, true)] // Reject a falsely-playing browser preview.
    [InlineData(true, "Chrome", false, true)]
    [InlineData(false, null, false, true)]
    public void BrowserOverridePreservesNativePlayers(bool playing, string? app, bool windowsPlaying, bool expected)
        => Assert.Equal(expected, BrowserMediaSelection.PreferBrowser(playing, app, windowsPlaying));
}
