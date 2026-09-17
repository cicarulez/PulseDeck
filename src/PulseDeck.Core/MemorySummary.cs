namespace PulseDeck.Core;

public static class MemorySummary
{
    public static IReadOnlyList<Metric> Read(IReadOnlyList<SensorReading> sensors)
    {
        // LHM exposes virtual and physical memory as HardwareType.Memory. Never mix them.
        double? Value(string type, string name)
        {
            var value = sensors.FirstOrDefault(s => s.HardwareId == "/ram" && s.SensorType == type && s.Name == name)?.Value;
            return value is { } v && double.IsFinite(v) && v >= 0 ? value : null;
        }
        var used = Value("Data", "Memory Used");
        var free = Value("Data", "Memory Available");
        return [new("ram.load", "RAM", Value("Load", "Memory"), "%"),
            new("ram.used", "RAM usata", used, "GiB"), new("ram.free", "RAM libera", free, "GiB"),
            new("ram.total", "RAM totale utilizzabile", used + free, "GiB")];
    }
}
