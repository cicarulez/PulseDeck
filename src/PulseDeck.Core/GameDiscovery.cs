using System.Text.RegularExpressions;

namespace PulseDeck.Core;

public sealed record GameDiscoveryOptions
{
    public bool Enabled { get; init; } = true;
    public string[] Folders { get; init; } = [];
    public string[] ConfirmedExecutables { get; init; } = [];
    public string[] IgnoredExecutables { get; init; } = [];
    public string? Validate()
    {
        if (Folders is null || Folders.Length > 20 || Folders.Any(p => !LocalPath(p))) return "Enter local Windows library paths.";
        if (ConfirmedExecutables is null || IgnoredExecutables is null || ConfirmedExecutables.Length > 1000 || IgnoredExecutables.Length > 1000
            || ConfirmedExecutables.Concat(IgnoredExecutables).Any(p => !LocalPath(p) || !p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            return "Invalid game executable override.";
        return null;
    }
    public static bool LocalPath(string? path) => path is { Length: >= 3 and <= 1024 }
        && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/' && !path.Any(char.IsControl);
}

public sealed record DiscoveredGame(string Name, string Executable, string InstallDirectory, string Source, string? SteamAppId, bool Automatic)
{
    public string Status(GameDiscoveryOptions options) => options.IgnoredExecutables.Contains(Executable, StringComparer.OrdinalIgnoreCase) ? "ignored"
        : Automatic || options.ConfirmedExecutables.Contains(Executable, StringComparer.OrdinalIgnoreCase) ? "recognized" : "review";
}

public static class GameDiscoveryRules
{
    // Library VDF and ACF fields are quoted strings. Only unescape Valve's escaped quotes/backslashes.
    public static string[] Values(string text, string key) => Regex.Matches(text, "\"" + Regex.Escape(key) + "\"\\s*\"((?:\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))
        .Select(m => m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\")).ToArray();
    public static bool IsHelper(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        string[] folders = ["__installer", "installer", "redist", "_commonredist", "support", "crashreporter", "cef", "easyanticheat", "eaanticheat", "battleye", "dotnet", "engine", "tools", "jre"];
        if (parts.SkipLast(1).Any(p => folders.Contains(p, StringComparer.OrdinalIgnoreCase))) return true;
        var name = parts[^1].ToLowerInvariant();
        string[] words = ["anticheat", "crash", "launcher", "unins", "setup", "install", "updat", "activation", "touchup", "report", "helper", "vc_redist", "vcredist", "dxsetup", "selector", "dbxmotion", "benchmark", "start_protected_game", "workshopuploader"];
        return words.Any(name.Contains);
    }
    public static bool IsDeclaredUnrealTarget(string relative, IEnumerable<string> declared)
    {
        var path = relative.Replace('\\', '/');
        var name = path.Split('/')[^1];
        return (path.Contains("/Binaries/Win64/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Binaries/Win32/", StringComparison.OrdinalIgnoreCase))
            && declared.Any(p => p.Replace('\\', '/').Split('/')[^1].Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    public static bool MatchesTitle(string title, string executable, string description)
    {
        var wanted = SteamGridImages.Normalize(title);
        var name = executable.Replace('\\', '/').Split('/')[^1];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        var actual = SteamGridImages.Normalize(name);
        var desc = SteamGridImages.Normalize(description);
        return wanted.Length >= 3 && (actual == wanted || desc == wanted || desc == wanted + "EXECUTABLE"
            || actual == wanted + "WIN64SHIPPING" || actual == wanted + "WIN32SHIPPING");
    }
}
