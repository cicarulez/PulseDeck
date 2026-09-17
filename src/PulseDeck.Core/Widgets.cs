using System.Globalization;

namespace PulseDeck.Core;

public sealed record WidgetConfig(string Slot, string Source, string MetricId, string SensorId,
    string SensorName, string Label, double Maximum = 100, string Style = "auto");
public sealed record WidgetSlot(string Id, string Name, bool IsBar);
public sealed record WidgetReading(string Slot, string Label, double? Value, string Unit, double Maximum, bool Hidden)
{
    public double? Capacity { get; init; }
    public double? Used { get; init; }
    public WidgetReading? Upload { get; init; }
    private double Divisor => Unit == "B/s" && Value is { } v ? Math.Abs(v) >= 1048576 ? 1048576 : Math.Abs(v) >= 1024 ? 1024 : 1 : 1;
    public string DisplayUnit => Unit == "B/s" && Divisor > 1 ? Divisor == 1048576 ? "MiB/s" : "KiB/s" : Unit;
    public string DisplayValue => Value is { } value && double.IsFinite(value)
        ? (value / Divisor).ToString(Math.Abs(value / Divisor) >= 100 ? "0" : "0.#", CultureInfo.GetCultureInfo("it-IT"))
        : "—";
    public double? Fraction => Value is { } value && double.IsFinite(value) && Maximum > 0
        ? Math.Clamp(value / Maximum, 0, 1) : null;
}

public static class WidgetCatalog
{
    public static readonly IReadOnlyList<WidgetSlot> Slots = Array.AsReadOnly(new[]
    {
        new WidgetSlot("bar1", "Barra sinistra 1", true), new WidgetSlot("bar2", "Barra sinistra 2", true),
        new WidgetSlot("bar3", "Barra sinistra 3", true), new WidgetSlot("value1", "Valore centrale 1", false),
        new WidgetSlot("value2", "Valore centrale 2", false), new WidgetSlot("value3", "Valore centrale 3", false),
        new WidgetSlot("value4", "Valore centrale 4", false), new WidgetSlot("side", "Valore a destra", false),
        new WidgetSlot("extra1", "Widget aggiuntivo 1", false), new WidgetSlot("extra2", "Widget aggiuntivo 2", false),
        new WidgetSlot("extra3", "Widget aggiuntivo 3", false), new WidgetSlot("extra4", "Widget aggiuntivo 4", false),
        new WidgetSlot("extra5", "Widget aggiuntivo 5", false), new WidgetSlot("extra6", "Widget aggiuntivo 6", false),
        new WidgetSlot("extra7", "Widget aggiuntivo 7", false), new WidgetSlot("extra8", "Widget aggiuntivo 8", false)
    });
    public static readonly IReadOnlyList<string> MetricIds = Array.AsReadOnly(new[]
    { "cpu.load", "gpu.load", "ram.load", "cpu.temperature", "gpu.temperature", "gpu.power", "gpu.memory", "ram.used", "ram.free", "ram.total" });

    public static WidgetConfig[] Defaults() => [.. LegacyDefaults(), .. Slots.Skip(8).Select(s => new WidgetConfig(s.Id, "none", "", "", "", ""))];

    private static WidgetConfig[] LegacyDefaults() =>
    [
        new("bar1", "metric", "cpu.load", "", "", "CPU"),
        new("bar2", "metric", "gpu.load", "", "", "GPU"),
        new("bar3", "metric", "ram.load", "", "", "RAM"),
        new("value1", "metric", "cpu.temperature", "", "", "CPU TEMP"),
        new("value2", "metric", "gpu.temperature", "", "", "GPU TEMP"),
        new("value3", "metric", "gpu.power", "", "", "GPU POWER"),
        new("value4", "metric", "gpu.memory", "", "", "VRAM"),
        new("side", "metric", "ram.used", "", "", "RAM ALLOCATED")
    ];

    public static string? Validate(WidgetConfig[]? widgets)
    {
        if (widgets is null || widgets.Length is not (8 or 16) || widgets.Any(w => w is null)) return "Configure all sixteen widget slots (or the original eight).";
        var expected = Slots.Take(widgets.Length).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        if (widgets.Select(w => w.Slot).Distinct().Count() != widgets.Length || widgets.Any(w => !expected.Contains(w.Slot)))
            return "Unknown or duplicate widget slot.";
        foreach (var widget in widgets)
        {
            if (widget.Source is not ("metric" or "sensor" or "network" or "none")) return "Unknown widget source.";
            if (widget.Style is not ("auto" or "value" or "bar" or "ring")) return "Unknown widget style.";
            if (widget.Label is null || widget.Label.Length > 24 || widget.Label.Any(char.IsControl)) return "Widget labels must contain at most 24 printable characters.";
            if (!double.IsFinite(widget.Maximum) || widget.Maximum <= 0 || widget.Maximum > 1e15) return "Widget maximum must be a positive finite number.";
            if (widget.MetricId is null || widget.SensorId is null || widget.SensorName is null
                || widget.MetricId.Length > 100 || widget.SensorId.Length > 1024 || widget.SensorName.Length > 256) return "Invalid widget binding.";
            if (widget.Source == "metric" && !MetricIds.Contains(widget.MetricId)) return "Unknown summary metric.";
            if (widget.Source is "sensor" or "network" && (string.IsNullOrWhiteSpace(widget.SensorId) || string.IsNullOrWhiteSpace(widget.SensorName)))
                return "Select a sensor for this widget.";
        }
        return null;
    }

    public static WidgetConfig[] Expand(WidgetConfig[] widgets)
    {
        if (Validate(widgets) is { } error) throw new ArgumentException(error);
        return widgets.Length == Slots.Count ? widgets : [.. widgets, .. Defaults().Skip(8)];
    }

    public static WidgetReading Resolve(WidgetConfig widget, HardwareSnapshot hardware)
    {
        if (widget.Source == "network")
        {
            // Bind both directions to one adapter; never sum virtual/filter duplicates.
            double? Read(string name) => hardware.Sensors.FirstOrDefault(s => s.HardwareId == widget.SensorId
                && s.HardwareName == widget.SensorName && s.SensorType == "Throughput" && s.Name == name)?.Value;
            return new(widget.Slot, widget.Label.Length > 0 ? widget.Label : "RETE", Read("Download Speed"), "B/s", 0, false)
                { Upload = new(widget.Slot, "UPLOAD", Read("Upload Speed"), "B/s", 0, false) };
        }
        // Match name as well as ID: LibreHardwareMonitor can reuse a source ID.
        var sensor = widget.Source == "sensor"
            ? hardware.Sensors.FirstOrDefault(s => s.Id == widget.SensorId && s.Name == widget.SensorName) : null;
        var metric = widget.Source == "metric" ? hardware.Metrics.FirstOrDefault(m => m.Id == widget.MetricId) : null;
        var value = sensor?.Value ?? metric?.Value;
        if (value is { } v && !double.IsFinite(v)) value = null;
        var label = widget.Label.Length > 0 ? widget.Label : sensor?.Name ?? metric?.Label ?? widget.SensorName;
        var capacity = widget.Source == "metric" && widget.MetricId is "ram.used" or "ram.load"
            ? hardware.Metrics.FirstOrDefault(m => m.Id == "ram.total")?.Value : null;
        if (capacity is not { } total || !double.IsFinite(total) || total <= 0) capacity = null;
        var maximum = widget.Source == "metric" && widget.MetricId == "ram.used" ? capacity ?? 0 : widget.Maximum;
        return new(widget.Slot, label, value, sensor?.Unit ?? metric?.Unit ?? "", maximum, widget.Source == "none") { Capacity = capacity, Used = widget.Source == "metric" && widget.MetricId == "ram.load"
            ? hardware.Metrics.FirstOrDefault(m => m.Id == "ram.used")?.Value : null };
    }
}
