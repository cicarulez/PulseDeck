using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using PulseDeck.Agent.Configuration;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class GameDiscoveryService(ConfigStore config) : BackgroundService
{
    private volatile GameLibraryScan scan = new([], [], []);
    private DateTimeOffset? scannedAt;
    private volatile bool scanning;
    private int requested = 1;
    private string settingsKey = "";
    private sealed record Cache(string Settings, DateTimeOffset ScannedAt, GameLibraryScan Scan);
    private string CachePath => Path.Combine(config.DirectoryPath, "game-library.json");
    public object Status => new { enabled = config.Current.GameDiscovery.Enabled, scanning, scannedAt,
        libraries = scan.Libraries, warnings = scan.Warnings,
        games = scan.Games.Select(g => new { g.Name, g.Executable, g.InstallDirectory, g.Source, g.SteamAppId, g.Automatic, thumbnailId = ThumbnailId(g), status = g.Status(config.Current.GameDiscovery) }).ToArray() };
    private static string ThumbnailId(DiscoveredGame game) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(game.Executable.ToUpperInvariant())));
    public string? ThumbnailTitle(string id) => id.Length == 64 ? scan.Games.FirstOrDefault(g => ThumbnailId(g) == id)?.Name : null;
    public void RequestScan() => Interlocked.Exchange(ref requested, 1);
    public DiscoveredGame? Find(string? executable)
    {
        var options = config.Current.GameDiscovery;
        if (!options.Enabled || executable is null) return null;
        return scan.Games.FirstOrDefault(g => string.Equals(g.Executable, executable, StringComparison.OrdinalIgnoreCase)
            && g.Status(options) == "recognized" && File.Exists(g.Executable));
    }
    public DeckConfig EffectiveConfig(DeckConfig settings) => !settings.GameDiscovery.Enabled ? settings : settings with
    {
        GameProcesses = settings.GameProcesses.Concat(scan.Games.Where(g => g.Status(settings.GameDiscovery) == "recognized" && File.Exists(g.Executable))
            .Select(g => Path.GetFileNameWithoutExtension(g.Executable))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
    };
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // All disk scanning happens on a worker, never in the rendering loop.
        await Task.Yield();
        try
        {
            if (File.Exists(CachePath) && new FileInfo(CachePath).Length < 4 * 1024 * 1024)
            {
                var cached = JsonSerializer.Deserialize<Cache>(await File.ReadAllTextAsync(CachePath, stoppingToken));
                if (cached is not null && cached.Settings == JsonSerializer.Serialize(config.Current.GameDiscovery))
                { scan = cached.Scan; scannedAt = cached.ScannedAt; }
            }
        }
        catch { }
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = config.Current.GameDiscovery;
            var key = JsonSerializer.Serialize(options);
            if (options.Enabled && (Interlocked.Exchange(ref requested, 0) != 0 || key != settingsKey || scannedAt is null || DateTimeOffset.UtcNow - scannedAt > TimeSpan.FromMinutes(10)))
            {
                scanning = true;
                try
                {
                    var next = await Task.Run(() => GameLibraryScanner.Scan(options, stoppingToken), stoppingToken);
                    if (key == JsonSerializer.Serialize(config.Current.GameDiscovery))
                    {
                        scan = next; scannedAt = DateTimeOffset.UtcNow; settingsKey = key;
                        var temporary = CachePath + ".tmp";
                        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Cache(key, scannedAt.Value, next)), stoppingToken);
                        File.Move(temporary, CachePath, true);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch { scan = scan with { Warnings = ["Scansione o salvataggio del catalogo non riuscito; nuovo tentativo tra dieci minuti."] }; scannedAt = DateTimeOffset.UtcNow; settingsKey = key; }
                finally { scanning = false; }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
