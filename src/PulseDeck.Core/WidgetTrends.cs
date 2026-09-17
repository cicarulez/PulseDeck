namespace PulseDeck.Core;

// Short history only; missing samples and binding changes cannot carry an old trend.
public sealed class WidgetTrends
{
    private readonly Dictionary<string, (string Identity, Queue<(DateTimeOffset At, double Value)> Samples)> history = new();
    public int Read(WidgetConfig config, WidgetReading reading, DateTimeOffset now)
    {
        var threshold = reading.Unit switch { "°C" => 2d, "W" => 5d, _ => 0d };
        var identity = string.Join('|', config.Source, config.MetricId, config.SensorId, config.SensorName);
        if (threshold == 0 || reading.Hidden || reading.Value is not { } value || !double.IsFinite(value))
        { history.Remove(config.Slot); return 0; }
        if (!history.TryGetValue(config.Slot, out var entry) || entry.Identity != identity
            || entry.Samples.Count > 0 && (now < entry.Samples.Last().At || now - entry.Samples.Last().At > TimeSpan.FromSeconds(5)))
            entry = (identity, new());
        var samples = entry.Samples;
        if (samples.Count == 0 || now > samples.Last().At) samples.Enqueue((now, value));
        while (samples.Count > 0 && now - samples.Peek().At > TimeSpan.FromSeconds(20)) samples.Dequeue();
        history[config.Slot] = entry;
        var previous = samples.LastOrDefault(s => now - s.At >= TimeSpan.FromSeconds(15));
        if (previous == default) return 0;
        var delta = value - previous.Value;
        return delta >= threshold ? 1 : delta <= -threshold ? -1 : 0;
    }
}
