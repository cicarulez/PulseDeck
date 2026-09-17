using System.Text.Json;

namespace PulseDeck.Agent.Providers;

public sealed record InstalledGame(string Name, string Executable);

// NVIDIA's local catalog is an optional, undocumented source. Only existing EXEs are accepted.
public sealed class InstalledGameCatalog
{
    private DateTimeOffset refreshAt;
    private InstalledGame[] games = [];
    public InstalledGame? Find(string processName, string? executable)
    {
        if (DateTimeOffset.UtcNow >= refreshAt)
        {
            refreshAt = DateTimeOffset.UtcNow.AddMinutes(1);
            games = [];
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NVIDIA Corporation", "NVIDIA app", "NvBackend", "ApplicationStorage.json");
                if (new FileInfo(path).Length > 16 * 1024 * 1024) return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var result = new List<InstalledGame>();
                foreach (var item in doc.RootElement.GetProperty("Applications").EnumerateArray().Take(2000))
                {
                    if (!item.TryGetProperty("Application", out var app) || !app.TryGetProperty("DisplayName", out var title)
                        || title.ValueKind != JsonValueKind.String || !app.TryGetProperty("ImageFiles", out var files)
                        || files.ValueKind != JsonValueKind.Array) continue;
                    var name = new string((title.GetString() ?? "").Where(c => !char.IsControl(c)).Take(120).ToArray()).Trim();
                    if (name.Length == 0) continue;
                    foreach (var file in files.EnumerateArray().Take(32))
                    {
                        if (file.ValueKind != JsonValueKind.String) continue;
                        var exe = file.GetString();
                        if (exe is null || !Path.IsPathFullyQualified(exe) || exe.StartsWith(@"\\")
                            || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(exe)) continue;
                        result.Add(new(name, exe));
                    }
                }
                games = result.ToArray();
            }
            catch { }
        }
        var matches = games.Where(g => File.Exists(g.Executable)).Where(g => executable is not null
            ? string.Equals(g.Executable, executable, StringComparison.OrdinalIgnoreCase)
            : string.Equals(Path.GetFileNameWithoutExtension(g.Executable), processName, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : null; // Never guess between ambiguous installations.
    }
}
