using System.Diagnostics;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class ForegroundProvider(InstalledGameCatalog catalog, GameDiscoveryService discovery, BrowserTabStore browserTabs)
{
    private sealed record Entry(string Name, ApplicationIcon? Icon, DateTimeOffset Expires);
    private readonly Dictionary<string, Entry> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TerminalIcon terminalIcon = new();
    private readonly PackagedApplicationProvider packagedApps = new();
    private readonly BrowserTabIcon browserTabIcon = new(browserTabs);
    private ApplicationIcon? desktopIcon;
    public ApplicationIcon? Icon { get; private set; }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int maximum);
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
            var windowClass = new StringBuilder(256);
            GetClassName(window, windowClass, windowClass.Capacity);
            var isDesktop = ForegroundTitles.IsDesktop(name, windowClass.ToString());
            if (isDesktop)
            {
                Icon = ReadDesktopIcon();
                return new((int)id, name, "Desktop", false, Icon?.Id, Icon is null ? "unavailable" : "available");
            }
            var packaged = packagedApps.Read((int)id, out var metadataLoading);
            // Read active tab titles every tick, outside the executable metadata cache.
            string? tabTitle = null;
            if (ForegroundTitles.IsTerminal(name) || ForegroundTitles.IsChrome(name) || ForegroundTitles.IsExplorer(name, windowClass.ToString()))
            {
                var title = new StringBuilder(2048);
                if (GetWindowText(window, title, title.Capacity) > 0)
                    tabTitle = ForegroundTitles.FromWindow(name, title.ToString(), windowClass.ToString());
            }
            var chromeIcon = ForegroundTitles.IsChrome(name) ? browserTabIcon.Read(tabTitle) : null;
            var terminalTitle = ForegroundTitles.IsTerminal(name) ? tabTitle : null;
            var displayTitle = tabTitle is null ? null : new string(tabTitle.Take(120).ToArray());
            // Do not flash executable metadata while Windows resolves a packaged app.
            // The real foreground PID/process remain available for profile selection.
            if (metadataLoading && packaged?.Name is null) displayTitle ??= "";
            Icon = chromeIcon ?? packaged?.Icon;
            var snapshot = new ForegroundSnapshot((int)id, name, displayTitle ?? packaged?.Name ?? name, ProfileSelector.IsGame(config, name),
                Icon?.Id, Icon is null ? "unavailable" : "available");
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
                Icon = chromeIcon ?? (terminalTitle is null ? null : terminalIcon.Read(path, terminalTitle)) ?? packaged?.Icon ?? (metadataLoading ? null : entry.Icon);
                return snapshot with { DisplayName = displayTitle ?? discovered?.Name ?? installed?.Name ?? packaged?.Name ?? entry.Name, IconId = Icon?.Id, IconStatus = Icon is null ? "unavailable" : "available" };
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

    private ApplicationIcon? ReadDesktopIcon()
    {
        if (desktopIcon is not null) return desktopIcon;
        try
        {
            using var bitmap = new SkiaSharp.SKBitmap(64, 64);
            using var canvas = new SkiaSharp.SKCanvas(bitmap);
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            using var paint = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(0, 120, 212) };
            foreach (var x in new[] { 4, 34 })
                foreach (var y in new[] { 4, 34 }) canvas.DrawRect(x, y, 26, 26, paint);
            using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            var bytes = png.ToArray();
            return desktopIcon = new(Convert.ToHexString(SHA256.HashData(bytes)), bytes);
        }
        catch { return null; }
    }
}
