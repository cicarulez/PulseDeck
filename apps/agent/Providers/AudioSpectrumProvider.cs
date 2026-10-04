using System.Diagnostics;
using System.Management;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class AudioSpectrumProvider : BackgroundService
{
    private sealed record Target(int Pid, DateTime Started);
    private readonly object gate = new();
    private Target? target;
    private AudioSpectrumSnapshot latest = new();
    private long lastSample;
    public void Select(MediaSnapshot media, bool enabled)
    {
        lock (gate) SelectCore(media, enabled);
    }
    private void SelectCore(MediaSnapshot media, bool enabled)
    {
        if (!enabled || !SpotifyLyrics.IsSpotify(media) || !media.Playing)
        {
            Volatile.Write(ref target, null);
            Volatile.Write(ref latest, new(enabled ? media.Playing ? "unavailable" : "paused" : "disabled",
                Detail: enabled && media.Playing ? "Il player musicale selezionato non è supportato." : null));
            return;
        }
        var current = Volatile.Read(ref target);
        if (current is not null)
        {
            try { using var p = Process.GetProcessById(current.Pid); if (!p.HasExited && p.StartTime == current.Started) return; }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        try
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)) throw new NotSupportedException();
            using var query = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId FROM Win32_Process WHERE Name='Spotify.exe'");
            using var rows = query.Get();
            var candidates = rows.Cast<ManagementObject>().Select(row => (Id: Convert.ToInt32(row["ProcessId"]), Parent: Convert.ToInt32(row["ParentProcessId"]))).ToArray();
            var ids = candidates.Select(p => p.Id).ToHashSet();
            using var own = Process.GetCurrentProcess();
            var sessionId = own.SessionId;
            var roots = candidates.Where(p => !ids.Contains(p.Parent)).Where(p =>
            {
                try { using var process = Process.GetProcessById(p.Id); return process.SessionId == sessionId; }
                catch (ArgumentException) { return false; }
            }).ToArray();
            if (roots.Length != 1) throw new InvalidOperationException("Player non identificabile in modo univoco.");
            using var root = Process.GetProcessById(roots[0].Id);
            var next = new Target(root.Id, root.StartTime);
            Volatile.Write(ref target, next);
            Volatile.Write(ref latest, new("connecting", ProcessId: next.Pid));
        }
        catch (Exception e) when (e is ManagementException or InvalidOperationException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            Volatile.Write(ref target, null);
            Volatile.Write(ref latest, new("unavailable", Detail: "Cattura del solo Spotify non disponibile."));
        }
    }
    public AudioSpectrumSnapshot Read()
    {
        lock (gate)
        {
            var value = Volatile.Read(ref latest);
            if (value.Status == "connected" && Environment.TickCount64 - Interlocked.Read(ref lastSample) > 300)
                return value with { Bands = new float[AudioSpectrumAnalyzer.BandCount] };
            return value;
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var active = Volatile.Read(ref target);
            if (active is null) { await Task.Delay(100, stoppingToken); continue; }
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var task = Task.Run(() => ProcessAudioCapture.Run(active.Pid, cancel.Token, (bands, samples) =>
            {
                lock (gate)
                {
                    if (!ReferenceEquals(active, Volatile.Read(ref target))) return;
                    Interlocked.Exchange(ref lastSample, Environment.TickCount64);
                    Volatile.Write(ref latest, new("connected", bands, ProcessId: active.Pid, Samples: samples));
                }
            }), CancellationToken.None);
            try
            {
                while (!task.IsCompleted && ReferenceEquals(active, Volatile.Read(ref target)) && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(100, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            finally { cancel.Cancel(); }
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or IOException or TimeoutException or InvalidCastException)
            {
                lock (gate)
                if (ReferenceEquals(active, Volatile.Read(ref target)))
                    Volatile.Write(ref latest, new("unavailable", Detail: "Cattura audio del player non disponibile.", ProcessId: active.Pid));
                await Task.Delay(3000, stoppingToken);
            }
        }
    }
}
