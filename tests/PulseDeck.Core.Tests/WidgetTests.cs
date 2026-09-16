using System.Text.Json;
using PulseDeck.Core;
using Xunit;

public class WidgetTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ExistingConfigurationGetsTheOriginalEightWidgets()
    {
        var config = JsonSerializer.Deserialize<DeckConfig>("""{"schemaVersion":1,"accentColor":"#123456"}""", Json)!;
        Assert.Null(config.Validate());
        Assert.Equal("#123456", config.AccentColor);
        Assert.Equal(8, config.Widgets.Length);
        Assert.Equal("cpu.load", config.Widgets.Single(w => w.Slot == "bar1").MetricId);
        Assert.Equal("ram.used", config.Widgets.Single(w => w.Slot == "side").MetricId);
    }

    [Fact]
    public void DuplicateSourceIdentifiersRemainDistinctAndMissingSensorsDoNotFallBack()
    {
        var hardware = new HardwareSnapshot([], "connected") { Sensors =
        [
            new("/gpu/load/3", "GPU Bus", "/gpu", "GPU", "GpuNvidia", "Load", 12, 4, 100, "%"),
            new("/gpu/load/3", "GPU Memory", "/gpu", "GPU", "GpuNvidia", "Load", 23, 19, 30, "%")
        ] };
        var widget = new WidgetConfig("value1", "sensor", "", "/gpu/load/3", "GPU Memory", "VRAM load");
        Assert.Equal(23d, WidgetCatalog.Resolve(widget, hardware).Value);
        var missing = WidgetCatalog.Resolve(widget with { SensorName = "Removed sensor" }, hardware);
        Assert.Null(missing.Value);
        Assert.Equal("—", missing.DisplayValue);
    }

    [Fact]
    public void SelectionAndScaleSurviveJsonPersistence()
    {
        var config = new DeckConfig();
        config.Widgets[0] = new("bar1", "sensor", "", "/mainboard/fan/1", "Fan #2", "CASE FAN", 3000);
        var restored = JsonSerializer.Deserialize<DeckConfig>(JsonSerializer.Serialize(config, Json), Json)!;
        Assert.Null(restored.Validate());
        Assert.Equal(config.Widgets[0], restored.Widgets[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidBarScalesAreRejected(double maximum)
    {
        var config = new DeckConfig();
        config.Widgets[0] = config.Widgets[0] with { Maximum = maximum };
        Assert.NotNull(config.Validate());
    }

    [Fact]
    public void DuplicateSlotsAndInvalidBindingsAreRejected()
    {
        var config = new DeckConfig();
        config.Widgets[1] = config.Widgets[1] with { Slot = "bar1" };
        Assert.NotNull(config.Validate());
        config = new();
        config.Widgets[0] = config.Widgets[0] with { Source = "sensor", SensorName = "", SensorId = "" };
        Assert.NotNull(config.Validate());
        Assert.NotNull((config with { Widgets = null! }).Validate());
        Assert.NotNull((config with { Widgets = [null!] }).Validate());
    }

    [Fact]
    public void FanBarUsesConfiguredScaleAndClampsWithoutChangingDisplayedReading()
    {
        var reading = new WidgetReading("bar1", "Fan", 1500, "RPM", 3000, false);
        Assert.Equal(.5d, reading.Fraction);
        Assert.Equal(1d, (reading with { Value = 4000 }).Fraction);
        Assert.Equal("4000", (reading with { Value = 4000 }).DisplayValue);
        Assert.Equal(0d, (reading with { Value = -2 }).Fraction);
        Assert.Null((reading with { Value = null }).Fraction);
    }
}
