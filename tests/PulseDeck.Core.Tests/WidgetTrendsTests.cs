using PulseDeck.Core;
using Xunit;
public class WidgetTrendsTests
{
    [Theory]
    [InlineData("°C", 2)]
    [InlineData("W", 5)]
    public void TrendUsesFifteenSecondsAndIgnoresSmallChanges(string unit, double threshold)
    {
        var trends = new WidgetTrends(); var config = WidgetCatalog.Defaults()[3];
        var reading = new WidgetReading(config.Slot, "TEST", 40, unit, 100, false);
        var now = DateTimeOffset.UnixEpoch;
        for (int i = 0; i < 15; i++) Assert.Equal(0, trends.Read(config, reading, now.AddSeconds(i)));
        Assert.Equal(0, trends.Read(config, reading with { Value = 40 + threshold - .1 }, now.AddSeconds(15)));
        Assert.Equal(1, trends.Read(config, reading with { Value = 40 + threshold }, now.AddSeconds(16)));
        Assert.Equal(-1, trends.Read(config, reading with { Value = 40 - threshold }, now.AddSeconds(17)));
        Assert.Equal(0, trends.Read(config, reading with { Value = null }, now.AddSeconds(18)));
        Assert.Equal(0, trends.Read(config, reading with { Value = 90 }, now.AddSeconds(19)));
    }
    [Fact]
    public void NoTrendForLoadOrAfterDataGapOrRebinding()
    {
        var trends = new WidgetTrends(); var config = WidgetCatalog.Defaults()[3];
        var reading = new WidgetReading(config.Slot, "TEST", 40, "°C", 100, false);
        var now = DateTimeOffset.UnixEpoch;
        for (int i = 0; i < 16; i++) trends.Read(config, reading, now.AddSeconds(i));
        Assert.Equal(0, trends.Read(config with { MetricId = "gpu.temperature" }, reading with { Value = 80 }, now.AddSeconds(16)));
        Assert.Equal(0, trends.Read(config, reading with { Value = 80 }, now.AddSeconds(40)));
        Assert.Equal(0, trends.Read(config, reading with { Unit = "%", Value = 100 }, now.AddSeconds(41)));
    }
}
