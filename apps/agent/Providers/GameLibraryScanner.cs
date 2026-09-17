using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed record GameLibrary(string Path, string Source);
public sealed record GameLibraryScan(GameLibrary[] Libraries, DiscoveredGame[] Games, string[] Warnings);

public static class GameLibraryScanner
{
    public static GameLibraryScan Scan(GameDiscoveryOptions options, CancellationToken token)
    {
        var libraries = new List<GameLibrary>();
        var games = new List<DiscoveredGame>();
        var warnings = new List<string>();
        void Add(string? path, string source)
        {
            if (!GameDiscoveryOptions.LocalPath(path)) return;
            path = Path.GetFullPath(path!).TrimEnd(Path.DirectorySeparatorChar);
            if (!libraries.Any(l => string.Equals(l.Path, path, StringComparison.OrdinalIgnoreCase))) libraries.Add(new(path, source));
        }
        string? steam = null;
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"); steam = key?.GetValue("SteamPath") as string; } catch { }
        var launchInfo = new Dictionary<string, SteamLaunchInfo>();
        if (steam is not null)
        {
            Add(steam, "Steam");
            try { var info = Path.Combine(steam, "appcache", "appinfo.vdf"); if (new FileInfo(info).Length < 128 * 1024 * 1024) launchInfo = SteamAppInfo.Read(File.ReadAllBytes(info)); }
            catch { warnings.Add("Dati di avvio Steam non disponibili: i casi incerti richiedono conferma."); }
            try { foreach (var path in GameDiscoveryRules.Values(ReadSmall(Path.Combine(steam, "steamapps", "libraryfolders.vdf")), "path")) Add(path, "Steam"); } catch { warnings.Add("Elenco librerie Steam non leggibile."); }
        }
        var ea = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EA Games");
        if (Directory.Exists(ea)) Add(ea, "EA Games");
        foreach (var folder in options.Folders)
        {
            var path = folder.TrimEnd('\\', '/');
            if (Path.GetFileName(path).Equals("steamapps", StringComparison.OrdinalIgnoreCase)) path = Path.GetDirectoryName(path)!;
            if (Path.GetFileName(path).Equals("common", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "steamapps", StringComparison.OrdinalIgnoreCase)) path = Path.GetDirectoryName(Path.GetDirectoryName(path))!;
            Add(path, Directory.Exists(Path.Combine(path, "steamapps")) ? "Steam" : "Cartella");
        }
        foreach (var library in libraries.Take(32))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!Directory.Exists(library.Path)) { warnings.Add("Cartella non disponibile: " + library.Path); continue; }
                if (library.Source == "Steam")
                {
                    foreach (var manifest in Directory.EnumerateFiles(Path.Combine(library.Path, "steamapps"), "appmanifest_*.acf").Take(1000))
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            var text = ReadSmall(manifest);
                            var title = GameDiscoveryRules.Values(text, "name").FirstOrDefault();
                            var directory = GameDiscoveryRules.Values(text, "installdir").FirstOrDefault();
                            var id = GameDiscoveryRules.Values(text, "appid").FirstOrDefault();
                            if (title is null || directory is null || id is null || !uint.TryParse(id, out _) || directory.IndexOfAny(['/', '\\', ':']) >= 0 || directory is "." or "..") continue;
                            if (title.Contains("Redistributables", StringComparison.OrdinalIgnoreCase)) continue;
                            launchInfo.TryGetValue(id, out var launch);
                            if (launch is not null && launch.Type.Length > 0 && !new[] { "game", "demo", "mod" }.Contains(launch.Type, StringComparer.OrdinalIgnoreCase)) continue;
                            Collect(Path.Combine(library.Path, "steamapps", "common", directory), title, "Steam", id, games, warnings, token, launch);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch { warnings.Add("Manifest Steam non leggibile: " + Path.GetFileName(manifest)); }
                    }
                }
                else
                    foreach (var directory in Directory.EnumerateDirectories(library.Path).Take(1000))
                        Collect(directory, Path.GetFileName(directory), library.Source, null, games, warnings, token);
            }
            catch (OperationCanceledException) { throw; }
            catch { warnings.Add("Scansione incompleta: " + library.Path); }
        }
        return new(libraries.ToArray(), games.DistinctBy(g => g.Executable, StringComparer.OrdinalIgnoreCase).Take(2000).ToArray(), warnings.Distinct().Take(30).ToArray());
    }
    private static void Collect(string root, string title, string source, string? appId, List<DiscoveredGame> result, List<string> warnings, CancellationToken token, SteamLaunchInfo? launch = null)
    {
        if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return;
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relative in launch?.Executables ?? [])
        {
            try {
                var full = Path.GetFullPath(Path.Combine(root, relative));
                if (full.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) declared.Add(full);
            } catch { }
        }
        var installer = Path.Combine(root, "__Installer", "installerdata.xml");
        if (File.Exists(installer))
        {
            try
            {
                using var reader = XmlReader.Create(new StringReader(ReadSmall(installer)), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                var xml = XDocument.Load(reader);
                title = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "gameTitle" && (string?)e.Attribute("locale") == "en_US")?.Value ?? title;
                source = "EA Games";
                foreach (var node in xml.Descendants().Where(e => e.Name.LocalName == "filePath"))
                {
                    var path = node.Value.Trim();
                    if (path.Contains(']')) path = path[(path.LastIndexOf(']') + 1)..];
                    path = Path.GetFullPath(Path.Combine(root, path.TrimStart('/', '\\')));
                    if (path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) declared.Add(path);
                }
            }
            catch { warnings.Add("Metadati EA non leggibili: " + title); }
        }
        title = new string(title.Where(c => !char.IsControl(c)).Take(120).ToArray()).Trim();
        var pending = new Stack<(string Path, int Depth)>(); pending.Push((root, 0));
        var visited = 0;
        while (pending.Count > 0 && visited++ < 2000 && result.Count < 2000)
        {
            token.ThrowIfCancellationRequested();
            var (directory, depth) = pending.Pop();
            try
            {
                foreach (var exe in Directory.EnumerateFiles(directory, "*.exe").Take(100))
                {
                    if (GameDiscoveryRules.IsHelper(Path.GetRelativePath(root, exe)) || (File.GetAttributes(exe) & FileAttributes.ReparsePoint) != 0) continue;
                    string description = "";
                    try { description = FileVersionInfo.GetVersionInfo(exe).FileDescription ?? ""; } catch { }
                    var automatic = declared.Contains(exe) || GameDiscoveryRules.IsDeclaredUnrealTarget(Path.GetRelativePath(root, exe), declared) || GameDiscoveryRules.MatchesTitle(title, exe, description);
                    result.Add(new(title, exe, root, source, appId, automatic));
                }
                if (depth < 7)
                    foreach (var child in Directory.EnumerateDirectories(directory).Take(2000))
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0 && !GameDiscoveryRules.IsHelper(Path.GetRelativePath(root, child) + "\\placeholder.exe")) pending.Push((child, depth + 1));
            }
            catch { warnings.Add("Cartella parzialmente leggibile: " + directory); }
        }
        if (pending.Count > 0) warnings.Add("Limite di scansione raggiunto: " + root);
    }
    private static string ReadSmall(string path) => new FileInfo(path).Length <= 2 * 1024 * 1024 ? File.ReadAllText(path) : throw new IOException("Manifest too large.");
}
