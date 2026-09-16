using System.Globalization;

namespace PulseDeck.Core;

public sealed record WidgetConfig(string Slot, string Source, string MetricId, string SensorId,
    string SensorName, string Label, double Maximum = 100);
public sealed record WidgetSlot(string Id, string Name, bool IsBar);
public sealed record WidgetReading(string Slot, string Label, double? Value, string Unit, double Maximum, bool Hidden)
{
    public string DisplayValue => Value is { } value && double.IsFinite(value)
        ? value.ToString(Math.Abs(value) >= 100 ? "0" : "0.#", CultureInfo.GetCultureInfo("it-IT"))
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
        new WidgetSlot("value4", "Valore centrale 4", false), new WidgetSlot("side", "Valore a destra", false)
    });
    public static readonly IReadOnlyList<string> MetricIds = Array.AsReadOnly(new[]
    { "cpu.load", "gpu.load", "ram.load", "cpu.temperature", "gpu.temperature", "gpu.power", "gpu.memory", "ram.used" });

    public static WidgetConfig[] Defaults() =>
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
        if (widgets is null || widgets.Length != Slots.Count || widgets.Any(w => w is null)) return "Configure all eight widget slots.";
        if (widgets.Select(w => w.Slot).Distinct().Count() != Slots.Count || widgets.Any(w => !Slots.Any(s => s.Id == w.Slot)))
            return "Unknown or duplicate widget slot.";
        foreach (var widget in widgets)
        {
            if (widget.Source is not ("metric" or "sensor" or "none")) return "Unknown widget source.";
            if (widget.Label is null || widget.Label.Length > 24 || widget.Label.Any(char.IsControl)) return "Widget labels must contain at most 24 printable characters.";
            if (!double.IsFinite(widget.Maximum) || widget.Maximum <= 0 || widget.Maximum > 1e15) return "Widget maximum must be a positive finite number.";
            if (widget.MetricId is null || widget.SensorId is null || widget.SensorName is null
                || widget.MetricId.Length > 100 || widget.SensorId.Length > 1024 || widget.SensorName.Length > 256) return "Invalid widget binding.";
            if (widget.Source == "metric" && !MetricIds.Contains(widget.MetricId)) return "Unknown summary metric.";
            if (widget.Source == "sensor" && (string.IsNullOrWhiteSpace(widget.SensorId) || string.IsNullOrWhiteSpace(widget.SensorName)))
                return "Select a sensor for this widget.";
        }
        return null;
    }

    public static WidgetReading Resolve(WidgetConfig widget, HardwareSnapshot hardware)
    {
        // Match name as well as ID: LibreHardwareMonitor can reuse a source ID.
        var sensor = widget.Source == "sensor"
            ? hardware.Sensors.FirstOrDefault(s => s.Id == widget.SensorId && s.Name == widget.SensorName) : null;
        var metric = widget.Source == "metric" ? hardware.Metrics.FirstOrDefault(m => m.Id == widget.MetricId) : null;
        var value = sensor?.Value ?? metric?.Value;
        if (value is { } v && !double.IsFinite(v)) value = null;
        var label = widget.Label.Length > 0 ? widget.Label : sensor?.Name ?? metric?.Label ?? widget.SensorName;
        return new(widget.Slot, label, value, sensor?.Unit ?? metric?.Unit ?? "", widget.Maximum, widget.Source == "none");
    }
}
