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
    private AuraSnapshot latest = new("unavailable");

    public AuraSnapshot Read(bool enabled)
    {
        if (!enabled) return new();
        lock (gate)
        {
            var tick = Environment.TickCount64;
            if (tick - lastRead < 250) return latest;
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
                return latest = AuraColor.Evaluate(report, DateTimeOffset.UtcNow, (ulong)Environment.TickCount64, alive);
            }
            catch (UnauthorizedAccessException) { return latest = new("unavailable", Detail: "Avvia PulseDeck con l’attività interattiva elevata per leggere Aura."); }
            catch (System.ComponentModel.Win32Exception) { return latest = new("unavailable", Detail: "Impossibile verificare il ricevitore Aura. Controlla i permessi di PulseDeck."); }
            catch (Exception e) when (e is IOException or JsonException)
            { return latest = new("unavailable", Detail: "Ricevitore Aura assente o non disponibile."); }
        }
    }
}
