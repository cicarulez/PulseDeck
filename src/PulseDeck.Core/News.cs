using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PulseDeck.Core;

public sealed record NewsChannel(string Name, string Url, bool Enabled = true);
public sealed record NewsOptions
{
    public bool Enabled { get; init; }
    public int RotationSeconds { get; init; } = 20;
    public int FontSize { get; init; } = 24;
    public NewsChannel[] Channels { get; init; } = [];
    public string? Validate()
    {
        if (RotationSeconds is < 10 or > 120) return "News rotation must be between 10 and 120 seconds.";
        if (FontSize is < 20 or > 26) return "News text size must be between 20 and 26 pixels.";
        if (Channels is null || Channels.Length > 8 || Channels.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name)
            || c.Name.Length > 40 || c.Name.Any(char.IsControl) || !NewsUrls.IsFeed(c.Url))) return "Use up to eight named public HTTPS feeds without credentials.";
        if (Channels.Select(c => new Uri(c.Url).AbsoluteUri).Distinct(StringComparer.Ordinal).Count() != Channels.Length) return "Duplicate news feed.";
        return null;
    }
}
public sealed record NewsItem(string Source, string Title, string Url, DateTimeOffset? PublishedAt);
public sealed record NewsChannelState(string Name, string Url, string Status, IReadOnlyList<NewsItem> Items);
public sealed record NewsSnapshot
{
    public string Status { get; init; } = "disabled";
    public IReadOnlyList<NewsChannelState> Channels { get; init; } = [];
    public IReadOnlyList<NewsItem> Items { get; init; } = [];
    public DateTimeOffset? FetchedAt { get; init; }
    public NewsItem? Select(DateTimeOffset now, int seconds) => Items.Count == 0 ? null
        : Items[(int)(Math.Max(0, (now - (FetchedAt ?? now)).TotalSeconds) / Math.Clamp(seconds, 10, 120) % Items.Count)];
}
public static class NewsUrls
{
    public static bool IsFeed(string? value) => value?.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Port == 443 && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0
        && !uri.IsLoopback && uri.Host.Contains('.') && !uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
        && (!IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) || IsPublic(ip));
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var b = address.GetAddressBytes();
        if (b.Length == 16) return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8);
        return b[0] is not (0 or 10 or 127) && b[0] < 224
            && !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] is >= 16 and <= 31)
            && !(b[0] == 192 && (b[1] == 168 || b[1] == 0 || b[1] == 2))
            && !(b[0] == 100 && b[1] is >= 64 and <= 127) && !(b[0] == 198 && b[1] is 18 or 19 or 51)
            && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
}
public static class NewsParser
{
    public static NewsItem[] Parse(Stream stream, NewsChannel channel, DateTimeOffset now)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 524288 });
        var root = XDocument.Load(reader).Root ?? throw new InvalidDataException("Empty feed.");
        XNamespace atom = "http://www.w3.org/2005/Atom";
        var entries = root.Name == atom + "feed" ? root.Elements(atom + "entry")
            : root.Name == "rss" ? root.Element("channel")?.Elements("item") ?? []
            : throw new InvalidDataException("Expected RSS 2.0 or Atom.");
        var items = new List<NewsItem>();
        foreach (var entry in entries.Take(100))
        {
            var isAtom = entry.Name.Namespace == atom;
            string Value(string name) => entry.Element((isAtom ? atom : XNamespace.None) + name)?.Value ?? "";
            var title = WebUtility.HtmlDecode(Value("title"));
            title = Regex.Replace(title, "<[^>]*>", "", RegexOptions.None, TimeSpan.FromMilliseconds(100));
            title = Regex.Replace(title, @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(100)).Trim();
            title = new string(title.Where(c => !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format).Take(300).ToArray());
            var link = isAtom ? entry.Elements(atom + "link").FirstOrDefault(e => (string?)e.Attribute("rel") is null or "alternate")?.Attribute("href")?.Value : Value("link");
            if (title.Length == 0 || string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(new Uri(channel.Url), link, out var url) || url.Scheme is not ("http" or "https") || url.UserInfo.Length > 0) continue;
            var rawDate = isAtom ? Value("published") : Value("pubDate");
            if (isAtom && rawDate.Length == 0) rawDate = Value("updated");
            DateTimeOffset? published = DateTimeOffset.TryParse(rawDate.Replace("GMT", "+0000"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var date) ? date : null;
            if (published is { } at && (at < now.AddDays(-7) || at > now.AddMinutes(30))) continue;
            items.Add(new(channel.Name, title, url.AbsoluteUri, published));
        }
        return items.DistinctBy(i => i.Url).OrderByDescending(i => i.PublishedAt).Take(10).ToArray();
    }
}

/// <summary>Single render-loop owner; requests only produce immutable results.</summary>
public sealed class NewsFeed(HttpClient client, TimeProvider? timeProvider = null) : IDisposable
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private NewsChannel[] selected = [];
    private NewsSnapshot snapshot = new();
    private Task<NewsChannelState[]>? pending;
    private CancellationTokenSource? request;
    private DateTimeOffset nextFetch;
    private DateTimeOffset requestedAt;
    public NewsSnapshot Read(NewsOptions options, CancellationToken stoppingToken)
    {
        var channels = options.Enabled && options.Validate() is null ? options.Channels.Where(c => c.Enabled).ToArray() : [];
        if (!channels.SequenceEqual(selected))
        {
            request?.Cancel(); request?.Dispose(); request = null; pending = null;
            selected = channels; snapshot = new() { Status = "loading" }; nextFetch = DateTimeOffset.MinValue;
        }
        if (channels.Length == 0) return snapshot = new() { Status = options.Enabled ? "not-configured" : "disabled" };
        var now = clock.GetUtcNow();
        if (pending?.IsCompleted == true)
        {
            var results = pending.GetAwaiter().GetResult(); pending = null; request?.Dispose(); request = null;
            // Interleave sources so a busy channel cannot crowd out another one.
            var items = Enumerable.Range(0, 10).SelectMany(i => results.SelectMany(c => c.Items.Skip(i).Take(1))).DistinctBy(i => i.Url).ToArray();
            var errors = results.Count(c => c.Status == "unavailable");
            snapshot = new() { Status = errors == results.Length ? "unavailable" : errors > 0 ? "partial" : items.Length == 0 ? "empty" : "connected",
                Channels = results, Items = items, FetchedAt = requestedAt };
            nextFetch = now.AddMinutes(errors > 0 ? 2 : 15);
        }
        if (snapshot.FetchedAt is { } fetched && now - fetched > TimeSpan.FromMinutes(30))
        { snapshot = new() { Status = "unavailable" }; nextFetch = DateTimeOffset.MinValue; }
        if (pending is null && now >= nextFetch && !stoppingToken.IsCancellationRequested)
        {
            request = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken); request.CancelAfter(TimeSpan.FromSeconds(8));
            requestedAt = now;
            pending = Task.WhenAll(channels.Select(c => Fetch(c, request.Token)));
        }
        return snapshot;
    }
    private async Task<NewsChannelState> Fetch(NewsChannel channel, CancellationToken token)
    {
        try
        {
            using var response = await client.GetAsync(channel.Url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var bytes = new byte[524289]; var length = 0;
            while (length < bytes.Length) { var n = await stream.ReadAsync(bytes.AsMemory(length), token); if (n == 0) break; length += n; }
            if (length > 524288) throw new InvalidDataException("Feed too large.");
            using var buffer = new MemoryStream(bytes, 0, length);
            var items = NewsParser.Parse(buffer, channel, clock.GetUtcNow());
            return new(channel.Name, channel.Url, items.Length == 0 ? "empty" : "connected", items);
        }
        catch { return new(channel.Name, channel.Url, "unavailable", []); }
    }
    public void Dispose() { request?.Cancel(); request?.Dispose(); }
}
