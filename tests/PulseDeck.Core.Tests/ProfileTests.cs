using PulseDeck.Core;
using Xunit;

public class ProfileTests
{
    [Fact]
    public void GameWinsOverMusicAfterDebounce()
    {
        var selector = new ProfileSelector(); var config = new DeckConfig(); var now = DateTimeOffset.UtcNow;
        Assert.Equal("desktop", selector.Select(config, "BF6.exe", true, now));
        Assert.Equal("desktop", selector.Select(config, "BF6.exe", true, now.AddSeconds(2)));
        Assert.Equal("gaming", selector.Select(config, "BF6.exe", true, now.AddSeconds(3)));
        Assert.Equal("gaming", selector.Select(config, "explorer", true, now.AddSeconds(4)));
        Assert.Equal("music", selector.Select(config, "explorer", true, now.AddSeconds(7)));
    }
    [Fact]
    public void BriefAltTabDoesNotSwitchProfile()
    {
        var selector = new ProfileSelector(); var config = new DeckConfig(); var now = DateTimeOffset.UtcNow;
        selector.Select(config, "bf6", false, now); selector.Select(config, "bf6", false, now.AddSeconds(4));
        selector.Select(config, "explorer", false, now.AddSeconds(5));
        Assert.Equal("gaming", selector.Select(config, "bf6", false, now.AddSeconds(6)));
    }
    [Fact]
    public void ManualProfileChangesImmediatelyAndMatchingIsExact()
    {
        var selector = new ProfileSelector(); var now = DateTimeOffset.UtcNow;
        Assert.Equal("music", selector.Select(new() { ProfileMode = "music" }, "bf6", false, now));
        Assert.Equal("desktop", selector.Select(new() { ProfileDelaySeconds = 0 }, "bf6-launcher", false, now));
    }
    [Theory]
    [InlineData("file:///C:/secret")]
    [InlineData("http://user:pass@localhost")]
    public void RejectsInvalidBackendLocations(string url) => Assert.NotNull((new DeckConfig { DiscordBaseUrl = url }).Validate());
}
