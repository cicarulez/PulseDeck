using System.Text.Json;
using PulseDeck.Core;
using Xunit;

public class AuraColorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 30, 0, TimeSpan.Zero);
    private static readonly AuraReport Report = new()
    {
        ProtocolVersion = 1, State = "running", Pid = 123, Utc = Now,
        HeartbeatTick = 100000, RawSampleTick = 1000, RawSamples = 41,
        RawWord = 0xff00ffff, LastEffectMethod = 3, LastEffectId = 0,
        LastEffectCount = 1, LastEffectVariant = 8211
    };

    [Theory]
    [InlineData(0xff00ffffu, "#FFFF00")]
    [InlineData(0xff0000ffu, "#FF0000")]
    [InlineData(0xffff0000u, "#0000FF")]
    [InlineData(0xff000000u, "#000000")]
    [InlineData(0x00123456u, "#563412")]
    public void DecodesObservedChannelOrderIgnoringUnknownHighByte(uint word, string expected) =>
        Assert.Equal(expected, AuraColor.Hex(word));

    [Fact]
    public void StaticColorRemainsAvailableWhileHostHeartbeats()
    {
        var value = AuraColor.Evaluate(Report, Now, 100000, true);
        Assert.Equal("connected", value.Status);
        Assert.Equal("#FFFF00", value.Color);
        Assert.Equal(Now.AddSeconds(-99), value.ReceivedAt);
        Assert.Equal(41, value.Samples);
    }

    [Fact]
    public void StaleDeadFutureAndPreviousBootReportsNeverSupplyAColor()
    {
        Assert.Null(AuraColor.Evaluate(Report, Now.AddSeconds(4), 104000, true).Color);
        Assert.Null(AuraColor.Evaluate(Report, Now, 100000, false).Color);
        Assert.Null(AuraColor.Evaluate(Report, Now, 500, true).Color);
        Assert.Null(AuraColor.Evaluate(Report with { Utc = Now.AddMinutes(1) }, Now, 100000, true).Color);
        Assert.Null(AuraColor.Evaluate(Report with { Utc = Now.AddHours(-1) }, Now, 100000, true).Color);
        Assert.Null(AuraColor.Evaluate(Report with { State = "idle-exit" }, Now, 100000, true).Color);
    }

    [Fact]
    public void MissingSamplesUnsupportedEnvelopesAndOldProtocolAreExplicit()
    {
        Assert.Equal("waiting", AuraColor.Evaluate(Report with { RawSamples = 0 }, Now, 100000, true).Status);
        Assert.Equal("waiting", AuraColor.Evaluate(Report with { RawSampleTick = 100001 }, Now, 100000, true).Status);
        Assert.Equal("unsupported", AuraColor.Evaluate(Report with { LastEffectId = 1 }, Now, 100000, true).Status);
        Assert.Equal("unsupported", AuraColor.Evaluate(Report with { LastEffectCount = 2 }, Now, 100000, true).Status);
        Assert.Equal("unsupported", AuraColor.Evaluate(Report with { LastEffectVariant = 0 }, Now, 100000, true).Status);
        Assert.Equal("incompatible", AuraColor.Evaluate(Report with { ProtocolVersion = 0 }, Now, 100000, true).Status);
        Assert.Throws<JsonException>(() => AuraColor.Parse("{broken"));
        Assert.Equal("incompatible", AuraColor.Evaluate(AuraColor.Parse("{}")!, Now, 100000, true).Status);
    }

    [Fact]
    public void ManualChoiceAndUnavailableAuraAlwaysUseSavedFallback()
    {
        var config = new DeckConfig { AccentColor = "#123456" };
        Assert.False(config.AuraEnabled);
        Assert.Equal("#123456", AuraColor.Accent(config, new("connected", "#FFFF00")));
        Assert.Equal("#FFFF00", AuraColor.Accent(config with { AuraEnabled = true }, new("connected", "#FFFF00")));
        Assert.Equal("#123456", AuraColor.Accent(config with { AuraEnabled = true }, new("unavailable", "#FFFF00")));
    }
}
