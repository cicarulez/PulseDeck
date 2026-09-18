namespace PulseDeck.Core;

public static class ForegroundTitles
{
    public static bool IsChrome(string process) => process.Equals("chrome", StringComparison.OrdinalIgnoreCase);
    public static bool IsTerminal(string process) => process.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase)
        || process.Equals("WindowsTerminalHost", StringComparison.OrdinalIgnoreCase);
    public static bool IsExplorer(string process, string? windowClass) => process.Equals("explorer", StringComparison.OrdinalIgnoreCase)
        && windowClass is "CabinetWClass" or "ExploreWClass";
    public static string Clean(string value) => new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();

    public static string? FromWindow(string process, string? title, string? windowClass = null)
    {
        if (title is null || !IsChrome(process) && !IsTerminal(process) && !IsExplorer(process, windowClass)) return null;
        var clean = Clean(title);
        if (IsChrome(process))
        {
            foreach (var suffix in new[] { " - Google Chrome", " – Google Chrome", " — Google Chrome" })
                if (clean.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                { clean = clean[..^suffix.Length].TrimEnd(); break; }
            if (clean.Equals("Google Chrome", StringComparison.OrdinalIgnoreCase)) return null;
        }
        if (IsExplorer(process, windowClass) && (clean.StartsWith(@"\\", StringComparison.Ordinal)
            || clean.Length >= 3 && char.IsLetter(clean[0]) && clean[1] == ':' && clean[2] is '\\' or '/'))
        {
            // Explorer can be configured to expose the full path in its title bar.
            // Match the tab's folder label instead, retaining a drive-root label.
            var path = clean.TrimEnd('\\', '/');
            var separator = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            if (separator >= 0 && separator + 1 < path.Length) clean = path[(separator + 1)..];
        }
        return clean.Length == 0 ? null : clean;
    }
}
