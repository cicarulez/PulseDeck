using System.Diagnostics;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class PresentMonProvider : IDisposable
{
    private Process? capture;
    private PresentMonSamples? samples;
    private GameSession? session;
    private DateTimeOffset retryAt;
    private string? lastError;
    private string? csvHeader;
    private string[] processNames = [];
    private int generation;
    private readonly object sampleGate = new();
    private readonly string executable = Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon.exe");
    private readonly string traceName = "PulseDeck-" + Environment.ProcessId;

    public FpsSnapshot Read(GameSession? current, DeckConfig config, DateTimeOffset now)
    {
        // BF6's anti-cheat denies StartTrace after launch. Keep our trace alive from
        // agent startup, limited to configured executable names. Select samples by PID.
        var names = config.GameProcesses.Select(p => Path.GetFileNameWithoutExtension(p) + ".exe")
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!names.SequenceEqual(processNames, StringComparer.OrdinalIgnoreCase))
        {
            Stop(); processNames = names; retryAt = DateTimeOffset.MinValue; lastError = null;
        }
        lock (sampleGate)
        {
            if (current?.ProcessId != session?.ProcessId || current?.StartedAt != session?.StartedAt)
            {
                session = current;
                samples = current is null ? null : new(current.ProcessId);
                if (csvHeader is not null) samples?.Add(csvHeader, now);
            }
        }
        if (names.Length == 0) return new("idle");
        if (!File.Exists(executable)) return new("not-installed", Detail: "PresentMon non installato");
        if (capture is not null && !capture.HasExited)
        {
            lock (sampleGate) return samples?.Read(now) ?? new("ready", Detail: "Raccolta pronta prima dell’avvio del gioco");
        }
        if (now < retryAt) return new("unavailable", ProcessId: current?.ProcessId,
            Detail: lastError ?? $"PresentMon terminato (codice {capture?.ExitCode}); nuovo tentativo tra poco");
        Stop(); retryAt = now.AddSeconds(30);
        try
        {
            lock (sampleGate) { samples = current is null ? null : new(current.ProcessId); csvHeader = null; }
            lastError = null;
            var start = StartInfo();
            foreach (var name in names) { start.ArgumentList.Add("--process_name"); start.ArgumentList.Add(name); }
            foreach (var arg in new[] { "--output_stdout", "--no_console_stats", "--v1_metrics", "--no_track_gpu",
                "--no_track_display", "--no_track_input", "--session_name", traceName }) start.ArgumentList.Add(arg);
            var owner = generation;
            capture = new Process { StartInfo = start };
            capture.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not { Length: > 0 } line || owner != Volatile.Read(ref generation)) return;
                lock (sampleGate)
                {
                    if (PresentMonSamples.IsHeader(line)) csvHeader = line;
                    samples?.Add(line, DateTimeOffset.UtcNow);
                }
            };
            capture.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not { Length: > 0 } error || owner != Volatile.Read(ref generation)) return;
                // Preserve the actual error instead of overwriting it with follow-up help lines.
                if (error.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                    lastError = "Accesso alla raccolta negato: chiudi il gioco e attendi che PresentMon sia pronto prima di riaprirlo.";
                else if (error.StartsWith("error:", StringComparison.OrdinalIgnoreCase) || lastError is null)
                    lastError = new string(error.Where(c => !char.IsControl(c)).Take(240).ToArray());
            };
            capture.Start(); capture.BeginOutputReadLine(); capture.BeginErrorReadLine();
            return new("starting", ProcessId: current?.ProcessId);
        }
        catch { Stop(); return new("unavailable", ProcessId: current?.ProcessId, Detail: "Avvio PresentMon non riuscito"); }
    }

    private ProcessStartInfo StartInfo() => new(executable)
    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };

    private void Stop()
    {
        Interlocked.Increment(ref generation);
        if (capture is null) return;
        try
        {
            if (!capture.HasExited)
            {
                var start = StartInfo();
                foreach (var arg in new[] { "--session_name", traceName, "--terminate_existing_session" }) start.ArgumentList.Add(arg);
                using var stop = Process.Start(start);
                if (stop is not null && !stop.WaitForExit(1500)) stop.Kill();
                if (!capture.WaitForExit(1500)) capture.Kill(entireProcessTree: true);
            }
        }
        catch { try { capture.Kill(entireProcessTree: true); } catch { } }
        finally { capture.Dispose(); capture = null; lock (sampleGate) { samples = null; csvHeader = null; } }
    }

    public void Dispose() => Stop();
}
