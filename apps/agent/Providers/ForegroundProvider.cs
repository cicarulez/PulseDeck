using System.Diagnostics;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class ForegroundProvider(InstalledGameCatalog catalog, GameDiscoveryService discovery)
{
    private sealed record Entry(string Name, ApplicationIcon? Icon, DateTimeOffset Expires);
    private readonly Dictionary<string, Entry> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TerminalIcon terminalIcon = new();
    public ApplicationIcon? Icon { get; private set; }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int maximum);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inheritHandle, int processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder name, ref uint size);
    private static string? ExecutablePath(Process process)
    {
        // Query the image path with limited rights; enumerating modules can be denied
        // even when Windows permits reading the process image name.
        try
        {
            using var handle = OpenProcess(0x1000, false, process.Id);
            var buffer = new StringBuilder(32768); uint size = (uint)buffer.Capacity;
            if (!handle.IsInvalid && QueryFullProcessImageName(handle, 0, buffer, ref size)) return buffer.ToString();
        }
        catch { }
        try { return process.MainModule?.FileName; } catch { return null; }
    }
    public ForegroundSnapshot Read(DeckConfig config)
    {
        Icon = null;
        try
        {
            var window = GetForegroundWindow();
            GetWindowThreadProcessId(window, out var id);
            if (id == 0) return ForegroundSnapshot.Empty;
            using var process = Process.GetProcessById((int)id);
            var name = process.ProcessName;
            // Terminal publishes the active tab as its window title. Read it each tick,
            // independently of the executable metadata/icon cache, including tab switches.
            string? terminalTitle = null;
            if (name.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase)
                || name.Equals("WindowsTerminalHost", StringComparison.OrdinalIgnoreCase))
            {
                var title = new StringBuilder(512);
                if (GetWindowText(window, title, title.Capacity) > 0)
                {
                    var clean = new string(title.ToString().Where(c => !char.IsControl(c)).Take(120).ToArray()).Trim();
                    if (clean.Length > 0) terminalTitle = clean;
                }
            }
            var snapshot = new ForegroundSnapshot((int)id, name, terminalTitle ?? name, ProfileSelector.IsGame(config, name), null, "unavailable");
            try
            {
                var path = ExecutablePath(process);
                var discovered = discovery.Find(path);
                snapshot = snapshot with { IsGame = snapshot.IsGame || discovered is not null };
                var installed = snapshot.IsGame ? catalog.Find(name, path) : null;
                path ??= installed?.Executable;
                if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) return snapshot;
                if (!cache.TryGetValue(path, out var entry) || entry.Expires <= DateTimeOffset.UtcNow)
                {
                    entry = Load(path, name);
                    if (!cache.ContainsKey(path) && cache.Count >= 32) cache.Remove(cache.Keys.First());
                    cache[path] = entry;
                }
                Icon = terminalTitle is null ? entry.Icon : terminalIcon.Read(path, terminalTitle) ?? entry.Icon;
                return snapshot with { DisplayName = terminalTitle ?? discovered?.Name ?? installed?.Name ?? entry.Name, IconId = Icon?.Id, IconStatus = Icon is null ? "unavailable" : "available" };
            }
            catch { return snapshot; } // Restricted processes still keep their actual process name.
        }
        catch { return ForegroundSnapshot.Empty; }
    }

    private static Entry Load(string path, string processName)
    {
        string name = processName;
        ApplicationIcon? artwork = null;
        try
        {
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
            if (!string.IsNullOrWhiteSpace(description))
            {
                var clean = new string(description.Where(c => !char.IsControl(c)).Take(120).ToArray()).Trim();
                if (clean.Length > 0) name = clean;
            }
        }
        catch { }
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is not null)
            {
                using var bitmap = icon.ToBitmap();
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                var bytes = stream.ToArray();
                artwork = new(Convert.ToHexString(SHA256.HashData(bytes)), bytes);
            }
        }
        catch { }
        return new(name, artwork, DateTimeOffset.UtcNow.AddSeconds(artwork is null ? 10 : 60));
    }
}
