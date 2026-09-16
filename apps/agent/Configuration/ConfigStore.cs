using System.Text.Json;
using PulseDeck.Core;

namespace PulseDeck.Agent.Configuration;

public sealed class ConfigStore
{
    private readonly object gate = new();
    private readonly string path;
    private DeckConfig current;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public DeckConfig Current { get { lock (gate) return current; } }
    public string DirectoryPath { get; }

    public ConfigStore(ILogger<ConfigStore> logger)
    {
        DirectoryPath = Environment.GetEnvironmentVariable("PULSEDECK_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PulseDeck");
        Directory.CreateDirectory(DirectoryPath);
        path = Path.Combine(DirectoryPath, "config.json");
        current = new();
        if (!File.Exists(path)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<DeckConfig>(File.ReadAllText(path), Json);
            if (loaded is null || loaded.Validate() is { }) throw new InvalidDataException("Invalid saved configuration.");
            current = loaded;
        }
        catch (Exception e) { logger.LogWarning(e, "Could not read config; using defaults. The existing file was preserved."); }
    }

    public void Save(DeckConfig config)
    {
        if (config.Validate() is { } error) throw new ArgumentException(error);
        if (config.BackgroundPath.Length > 0 && (!File.Exists(config.BackgroundPath) || !new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(config.BackgroundPath).ToLowerInvariant())))
            throw new ArgumentException("Background must be an existing PNG, JPEG or WebP file on this Windows PC.");
        lock (gate)
        {
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(config, Json));
            File.Move(temporary, path, true);
            current = config;
        }
    }
}
