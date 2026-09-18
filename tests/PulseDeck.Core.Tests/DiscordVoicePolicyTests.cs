using PulseDeck.Core;
using Xunit;

public class DiscordVoicePolicyTests
{
    [Fact]
    public void ChannelSelectionPreservesImportedDefaultAndRejectsInvalidIds()
    {
        var config = new DeckConfig();
        Assert.Equal(123UL, DiscordVoicePolicy.SelectedChannel(config, 123));
        Assert.Equal(456UL, DiscordVoicePolicy.SelectedChannel(config with { DiscordVoiceChannelId = "456" }, 123));
        Assert.Null((config with { DiscordVoiceChannelId = "456" }).Validate());
        Assert.NotNull((config with { DiscordVoiceChannelId = "0" }).Validate());
        Assert.NotNull((config with { DiscordVoiceChannelId = "-1" }).Validate());
        Assert.NotNull((config with { DiscordVoiceChannelId = " 456" }).Validate());
    }
    [Theory]
    [InlineData("desktop")]
    [InlineData("gaming")]
    [InlineData("music")]
    public void FollowsTrackedUserRegardlessOfProfileOrGamingLayout(string profile)
    {
        var config = new DeckConfig { ProfileMode = profile, GamingLayout = false, TrackedMemberId = "me" };
        Assert.False(DiscordVoicePolicy.ShouldConnect(config, []));
        Assert.False(DiscordVoicePolicy.ShouldConnect(config, ["other"]));
        Assert.True(DiscordVoicePolicy.ShouldConnect(config, ["me", "other"]));
        Assert.False(DiscordVoicePolicy.ShouldConnect(config, ["other"]));
        Assert.True(DiscordVoicePolicy.ShouldConnect(config, ["me"]));
        Assert.False(DiscordVoicePolicy.ShouldConnect(config with { GamingVoiceActivity = false }, ["me"]));
        Assert.False(DiscordVoicePolicy.ShouldConnect(config with { DiscordMode = "external" }, ["me"]));
    }
    [Fact]
    public void WithoutTrackedUserRequiresAHumanParticipant()
    {
        Assert.False(DiscordVoicePolicy.ShouldConnect(new(), []));
        Assert.True(DiscordVoicePolicy.ShouldConnect(new(), ["human"]));
    }
}
