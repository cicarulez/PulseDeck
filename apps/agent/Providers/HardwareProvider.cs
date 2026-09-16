using LibreHardwareMonitor.Hardware;
using PulseDeck.Core;
using System.Security.Principal;

namespace PulseDeck.Agent.Providers;

public sealed class HardwareProvider : IDisposable
{
    private readonly Computer computer = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, IsMotherboardEnabled = true, IsStorageEnabled = true, IsNetworkEnabled = true, IsControllerEnabled = true, IsBatteryEnabled = true, IsPsuEnabled = true, IsPowerMonitorEnabled = true };
    private readonly bool admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    private bool opened;

    public HardwareSnapshot Read()
    {
        try
        {
            if (!opened) { computer.Open(); opened = true; }
            var sensors = new List<SensorReading>();
            var errors = new List<string>();
            var all = new List<(HardwareType Hardware, ISensor Sensor)>();
            void Visit(IHardware hardware)
            {
                try { hardware.Update(); }
                catch { errors.Add($"Lettura non riuscita: {hardware.Name}."); return; }
                static double? Finite(float? value) => value is { } v && float.IsFinite(v) ? v : null;
                sensors.AddRange(hardware.Sensors.Select(s => new SensorReading(s.Identifier.ToString(), s.Name,
                    hardware.Identifier.ToString(), hardware.Name, hardware.HardwareType.ToString(), s.SensorType.ToString(),
                    Finite(s.Value), Finite(s.Min), Finite(s.Max), Unit(s.SensorType))));
                all.AddRange(hardware.Sensors.Select(s => (hardware.HardwareType, s)));
                foreach (var child in hardware.SubHardware) Visit(child);
            }
            foreach (var hardware in computer.Hardware) Visit(hardware);
            double? Get(Func<(HardwareType Hardware, ISensor Sensor), bool> predicate, string? preferred = null)
            {
                // Some inaccessible temperature sensors report zero before initialization.
                var matches = all.Where(predicate).Where(x => x.Sensor.Value is { } value && float.IsFinite(value)
                    && (x.Sensor.SensorType != SensorType.Temperature || value > 0)).ToArray();
                var first = matches.FirstOrDefault(x => preferred is not null && x.Sensor.Name.Contains(preferred, StringComparison.OrdinalIgnoreCase));
                return first.Sensor?.Value ?? matches.FirstOrDefault().Sensor?.Value;
            }
            bool Gpu(HardwareType type) => type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
            var metrics = new List<Metric>
            {
                new("cpu.load", "CPU", Get(x => x.Hardware == HardwareType.Cpu && x.Sensor.SensorType == SensorType.Load, "Total"), "%"),
                new("cpu.temperature", "CPU temperature", Get(x => x.Hardware == HardwareType.Cpu && x.Sensor.SensorType == SensorType.Temperature, "Package"), "°C"),
                new("gpu.load", "GPU", Get(x => Gpu(x.Hardware) && x.Sensor.SensorType == SensorType.Load, "Core"), "%"),
                new("gpu.temperature", "GPU temperature", Get(x => Gpu(x.Hardware) && x.Sensor.SensorType == SensorType.Temperature, "Core"), "°C"),
                new("gpu.power", "GPU power", Get(x => Gpu(x.Hardware) && x.Sensor.SensorType == SensorType.Power, "Package"), "W"),
                new("gpu.memory", "VRAM used", Get(x => Gpu(x.Hardware) && x.Sensor.SensorType == SensorType.SmallData && x.Sensor.Name.Contains("Used")), "MB"),
                new("ram.load", "RAM", Get(x => x.Hardware == HardwareType.Memory && x.Sensor.SensorType == SensorType.Load), "%"),
                new("ram.used", "RAM used", Get(x => x.Hardware == HardwareType.Memory && x.Sensor.SensorType == SensorType.Data && x.Sensor.Name.Equals("Memory Used", StringComparison.OrdinalIgnoreCase)), "GB")
            };
            var pawn = LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled;
            if (!pawn) errors.Add("PawnIO non è installato: mancano le letture a basso livello di CPU, scheda madre e ventole. Installa il driver e riavvia PulseDeck come amministratore.");
            else if (!admin) errors.Add("Avvia PulseDeck come amministratore per accedere anche ai sensori di CPU, scheda madre e ventole.");
            return new(metrics, sensors.Any(s => s.Value.HasValue) ? "connected" : "unavailable",
                errors.Count == 0 ? null : string.Join(" ", errors))
            { Sensors = sensors, IsAdministrator = admin, PawnIoInstalled = pawn };
        }
        catch (Exception e) { return new([], "error", e.Message) { IsAdministrator = admin }; }
    }

    private static string Unit(SensorType type) => type switch
    {
        SensorType.Voltage => "V", SensorType.Current => "A", SensorType.Power => "W",
        SensorType.Clock => "MHz", SensorType.Temperature => "°C", SensorType.Frequency => "Hz",
        SensorType.Fan => "RPM", SensorType.Flow => "L/h",
        SensorType.Load or SensorType.Control or SensorType.Level or SensorType.Humidity => "%",
        SensorType.Data => "GiB", SensorType.SmallData => "MiB", SensorType.Throughput => "B/s",
        SensorType.TimeSpan => "s", SensorType.Timing => "ns", SensorType.Energy => "mWh",
        SensorType.Noise => "dBA", SensorType.Conductivity => "µS/cm", _ => ""
    };

    public void Dispose() => computer.Close();
}
