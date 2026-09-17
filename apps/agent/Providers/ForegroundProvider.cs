using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class ForegroundProvider(InstalledGameCatalog catalog)
{
    private sealed record Entry(string Name, ApplicationIcon? Icon, DateTimeOffset Expires);
    private readonly Dictionary<string, Entry> cache = new(StringComparer.OrdinalIgnoreCase);
    public ApplicationIcon? Icon { get; private set; }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    public ForegroundSnapshot Read(DeckConfig config)
    {
        Icon = null;
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var id);
            if (id == 0) return ForegroundSnapshot.Empty;
            using var process = Process.GetProcessById((int)id);
            var name = process.ProcessName;
            var snapshot = new ForegroundSnapshot((int)id, name, name, ProfileSelector.IsGame(config, name), null, "unavailable");
            try
            {
                string? path = null;
                try { path = process.MainModule?.FileName; } catch { }
                var installed = snapshot.IsGame ? catalog.Find(name, path) : null;
                path ??= installed?.Executable;
                if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) return snapshot;
                if (!cache.TryGetValue(path, out var entry) || entry.Expires <= DateTimeOffset.UtcNow)
                {
                    entry = Load(path, name);
                    if (!cache.ContainsKey(path) && cache.Count >= 32) cache.Remove(cache.Keys.First());
                    cache[path] = entry;
                }
                Icon = entry.Icon;
                return snapshot with { DisplayName = installed?.Name ?? entry.Name, IconId = Icon?.Id, IconStatus = Icon is null ? "unavailable" : "available" };
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
