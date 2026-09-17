using System.Text.Json;
using PulseDeck.Core;
using Xunit;

public class WidgetTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ExistingConfigurationGetsOriginalBindingsAndEightHiddenSlots()
    {
        var config = JsonSerializer.Deserialize<DeckConfig>("""{"schemaVersion":1,"accentColor":"#123456"}""", Json)!;
        Assert.Null(config.Validate());
        Assert.Equal("#123456", config.AccentColor);
        Assert.Equal(16, config.Widgets.Length);
        Assert.All(config.Widgets.Skip(8), w => Assert.Equal("none", w.Source));
        Assert.Equal("cpu.load", config.Widgets.Single(w => w.Slot == "bar1").MetricId);
        Assert.Equal("ram.used", config.Widgets.Single(w => w.Slot == "side").MetricId);
    }

    [Fact]
    public void LegacyMigrationPreservesOrderBindingsLabelsVisibilityAndScale()
    {
        var original = WidgetCatalog.Defaults().Take(8).Reverse().ToArray();
        original[0] = original[0] with { Source = "none", Label = "CUSTOM", Maximum = 2048 };
        original[1] = original[1] with { Source = "sensor", SensorId = "/duplicated", SensorName = "Bus" };
        Assert.Null(WidgetCatalog.Validate(original));
        var migrated = WidgetCatalog.Expand(original);
        Assert.Equal(original, migrated.Take(8));
        Assert.All(migrated.Skip(8), w => Assert.Equal("none", w.Source));
        Assert.Equal(migrated, WidgetCatalog.Expand(migrated));
        var config = new DeckConfig { Widgets = migrated, Layout = "classic" };
        var restored = JsonSerializer.Deserialize<DeckConfig>(JsonSerializer.Serialize(config, Json), Json)!;
        Assert.Equal("classic", restored.Layout);
        Assert.Equal(migrated, restored.Widgets);
    }

    [Fact]
    public void MigrationRejectsIncompleteSetsAndUnknownSlotsRatherThanDroppingBindings()
    {
        var widgets = WidgetCatalog.Defaults();
        Assert.NotNull(WidgetCatalog.Validate(widgets.Take(9).ToArray()));
        Assert.NotNull(WidgetCatalog.Validate(widgets.Skip(8).ToArray()));
        Assert.Throws<ArgumentException>(() => WidgetCatalog.Expand(widgets.Take(7).ToArray()));
        Assert.NotNull((new DeckConfig { Layout = "unknown" }).Validate());
        widgets[0] = widgets[0] with { Style = "pie-invalid" };
        Assert.NotNull(WidgetCatalog.Validate(widgets));
    }

    [Fact]
    public void NetworkDisplayScalesBytesWithoutChangingTheValueOrFraction()
    {
        var reading = new WidgetReading("extra1", "Download", 524288, "B/s", 1048576, false);
        Assert.Equal("512", reading.DisplayValue);
        Assert.Equal("KiB/s", reading.DisplayUnit);
        Assert.Equal(.5d, reading.Fraction);
        Assert.Equal("MiB/s", (reading with { Value = 2097152 }).DisplayUnit);
    }

    [Fact]
    public void CombinedNetworkKeepsDirectionsOnSelectedAdapterAndMissingUploadExplicit()
    {
        SensorReading Sensor(string adapter, string name, double value) => new(adapter + name, name, adapter, adapter, "Network", "Throughput", value, null, null, "B/s");
        var hardware = new HardwareSnapshot([], "connected") { Sensors = [Sensor("ethernet", "Download Speed", 2048),
            Sensor("ethernet", "Upload Speed", 1024), Sensor("virtual", "Upload Speed", 99999)] };
        var widget = new WidgetConfig("extra1", "network", "", "ethernet", "ethernet", "RETE");
        var reading = WidgetCatalog.Resolve(widget, hardware);
        Assert.Equal("2", reading.DisplayValue); Assert.Equal("1", reading.Upload!.DisplayValue);
        Assert.Null(reading.Fraction);
        reading = WidgetCatalog.Resolve(widget, hardware with { Sensors = hardware.Sensors.Where(s => s.Name != "Upload Speed" || s.HardwareId != "ethernet").ToArray() });
        Assert.Equal(2048, reading.Value); Assert.Equal("—", reading.Upload!.DisplayValue);
        var config = WidgetCatalog.Defaults(); config[8] = widget;
        Assert.Null(WidgetCatalog.Validate(config));
    }

    [Fact]
    public void RamCapacityUsesPhysicalMemoryAndDoesNotInventMissingTotals()
    {
        SensorReading Sensor(string hardware, string name, double? value) => new(hardware + name, name, hardware, hardware, "Memory", "Data", value, null, null, "GiB");
        var metrics = MemorySummary.Read([Sensor("/vram", "Memory Used", 123), Sensor("/ram", "Memory Used", 20), Sensor("/ram", "Memory Available", 44)]);
        Assert.Equal(64d, metrics.Single(m => m.Id == "ram.total").Value);
        var widget = WidgetCatalog.Defaults().Single(w => w.Slot == "side") with { Style = "ring" };
        var reading = WidgetCatalog.Resolve(widget, new(metrics, "connected"));
        Assert.Equal(20d, reading.Value);
        Assert.Equal(64d, reading.Capacity);
        Assert.Equal(20d / 64, reading.Fraction);
        var missing = MemorySummary.Read([Sensor("/ram", "Memory Used", 20), Sensor("/vram", "Memory Available", 100)]);
        Assert.Null(missing.Single(m => m.Id == "ram.total").Value);
        Assert.Null(WidgetCatalog.Resolve(widget, new(missing, "connected")).Fraction);
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
