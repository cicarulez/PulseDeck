using System.Diagnostics;
using System.Text.Json;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

// Reads only our separate native destination. Never loads ASUS SDK or invokes COM.
public sealed class AuraColorProvider
{
    private readonly object gate = new();
    private readonly string reportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "SysWOW64", "config", "systemprofile", "AppData", "Local", "PulseDeck", "aura-installed-probe", "session-0.json");
    private readonly string hostPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "PulseDeck Aura Probe", "PulseDeck.AuraHal.exe");
    private long lastRead = -1000;
    private long lastPowerRead = -1000;
    private bool? lightingOff;
    private readonly string serviceDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LightingService");
    private AuraSnapshot latest = new("unavailable");

    public AuraSnapshot Read(bool enabled)
    {
        if (!enabled) return new();
        lock (gate)
        {
            var tick = Environment.TickCount64;
            if (tick - lastRead < 80) return latest;
            lastRead = tick;
            try
            {
                using var stream = new FileStream(reportPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > 16384) return latest = new("unavailable", Detail: "Rapporto Aura non valido.");
                using var reader = new StreamReader(stream);
                var report = AuraColor.Parse(reader.ReadToEnd());
                if (report is null) return latest = new("unavailable", Detail: "Rapporto Aura vuoto.");
                var alive = false;
                if (report.Pid > 0)
                {
                    try
                    {
                        using var host = Process.GetProcessById(report.Pid);
                        alive = !host.HasExited && host.SessionId == 0
                            && string.Equals(host.MainModule?.FileName, hostPath, StringComparison.OrdinalIgnoreCase)
                            && host.StartTime.ToUniversalTime() <= report.Utc.UtcDateTime;
                    }
                    catch (ArgumentException) { }
                    catch (InvalidOperationException) { }
                }
                var snapshot = AuraColor.Evaluate(report, DateTimeOffset.UtcNow, (ulong)Environment.TickCount64, alive);
                if (snapshot.Status is not ("connected" or "unsupported" or "waiting")) return latest = snapshot;
                return latest = AuraPower.Apply(snapshot, ReadPower(tick));
            }
            catch (UnauthorizedAccessException) { return latest = new("unavailable", Detail: "Avvia PulseDeck con l’attività interattiva elevata per leggere Aura."); }
            catch (System.ComponentModel.Win32Exception) { return latest = new("unavailable", Detail: "Impossibile verificare il ricevitore Aura. Controlla i permessi di PulseDeck."); }
            catch (Exception e) when (e is IOException or JsonException)
            { return latest = new("unavailable", Detail: "Ricevitore Aura assente o non disponibile."); }
        }
    }

    private bool? ReadPower(long tick)
    {
        if (tick - lastPowerRead < 500) return lightingOff;
        lastPowerRead = tick;
        lightingOff = null;
        try
        {
            var processes = Process.GetProcessesByName("LightingService");
            try
            {
                if (!processes.Any(p => !p.HasExited && string.Equals(p.MainModule?.FileName,
                    Path.Combine(serviceDirectory, "LightingService.exe"), StringComparison.OrdinalIgnoreCase))) return null;
            }
            finally { foreach (var process in processes) process.Dispose(); }
            using var stream = new FileStream(Path.Combine(serviceDirectory, "LastProfile.xml"), FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 262144) return null;
            using var reader = new StreamReader(stream);
            return lightingOff = AuraPower.ReadOff(reader.ReadToEnd());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { return null; }
    }
}
