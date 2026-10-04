using System.Text.Json;
using PulseDeck.Core;
using Xunit;

public class DisplayBrightnessTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(50, 127)]
    [InlineData(100, 255)]
    public void CommandUsesRevisionCByteScaleAndPadding(int percent, byte expected)
    {
        var packet = TurzxProtocol.BrightnessCommand(percent);
        Assert.Equal(250, packet.Length);
        Assert.Equal(Convert.FromHexString("7BEF6900000001000000"), packet[..10]);
        Assert.Equal(expected, packet[10]);
        Assert.All(packet[11..], b => Assert.Equal(0, b));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidBrightnessCannotReachTheDevice(int percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TurzxProtocol.BrightnessCommand(percent));
        Assert.NotNull((new DeckConfig { DisplayBrightness = percent }).Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(100)]
    public void SavedBrightnessRoundTripsWithoutChangingOtherSettings(int? percent)
    {
        var original = new DeckConfig { DisplayBrightness = percent, DisplayPort = "COM7", AuraEnabled = true };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var loaded = JsonSerializer.Deserialize<DeckConfig>(JsonSerializer.Serialize(original, options), options)!;
        Assert.Null(loaded.Validate());
        Assert.Equal(percent, loaded.DisplayBrightness);
        Assert.Equal("COM7", loaded.DisplayPort);
        Assert.True(loaded.AuraEnabled);
    }

    [Fact]
    public void ExistingConfigurationsKeepTheCurrentPanelBrightness()
    {
        var config = JsonSerializer.Deserialize<DeckConfig>("{\"displayPort\":\"COM5\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Null(config.DisplayBrightness);
        Assert.Null(config.Validate());
    }
}
