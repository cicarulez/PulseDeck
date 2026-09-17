using System.Diagnostics;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class PresentMonProvider : IDisposable
{
    private Process? capture;
    private PresentMonSamples? samples;
    private GameSession? session;
    private DateTimeOffset retryAt;
    private readonly string executable = Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon.exe");
    private readonly string traceName = "PulseDeck-" + Environment.ProcessId;

    public FpsSnapshot Read(GameSession? current, DateTimeOffset now)
    {
        if (current?.ProcessId != session?.ProcessId || current?.StartedAt != session?.StartedAt)
        {
            Stop(); session = current; retryAt = DateTimeOffset.MinValue;
        }
        if (current is null) return new("idle");
        if (!File.Exists(executable)) return new("not-installed", Detail: "PresentMon non installato");
        if (capture is not null && !capture.HasExited) return samples!.Read(now);
        if (now < retryAt) return new("unavailable", ProcessId: current.ProcessId, Detail: "PresentMon non disponibile; nuovo tentativo tra poco");
        Stop(); retryAt = now.AddSeconds(30);
        try
        {
            samples = new(current.ProcessId);
            var collector = samples;
            var start = StartInfo();
            foreach (var arg in new[] { "--process_id", current.ProcessId.ToString(), "--output_stdout", "--no_console_stats",
                "--v1_metrics", "--terminate_on_proc_exit", "--session_name", traceName }) start.ArgumentList.Add(arg);
            capture = new Process { StartInfo = start };
            capture.OutputDataReceived += (_, e) => { if (e.Data is { Length: > 0 } line) collector.Add(line, DateTimeOffset.UtcNow); };
            capture.ErrorDataReceived += (_, _) => { }; // Drain, never retain a per-frame log.
            capture.Start(); capture.BeginOutputReadLine(); capture.BeginErrorReadLine();
            return new("waiting", ProcessId: current.ProcessId);
        }
        catch { Stop(); return new("unavailable", ProcessId: current.ProcessId, Detail: "Avvio PresentMon non riuscito"); }
    }

    private ProcessStartInfo StartInfo() => new(executable)
    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };

    private void Stop()
    {
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
        finally { capture.Dispose(); capture = null; samples = null; }
    }

    public void Dispose() => Stop();
}
