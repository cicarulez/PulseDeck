using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;

namespace PulseDeck.Core;

public sealed record CalendarOptions
{
    public bool Enabled { get; init; } = true;
    public bool HideTitles { get; init; }
    public bool NotifyNewEvents { get; init; } = true;
    public int PollMinutes { get; init; } = 5;
    public string? Validate() => PollMinutes is < 1 or > 60 ? "Il controllo calendario deve essere tra 1 e 60 minuti." : null;
}
public sealed record CalendarAppointment(string Title, DateTimeOffset Start, DateTimeOffset End, bool AllDay);
public sealed record CalendarSnapshot(string Status = "not-configured", CalendarAppointment[]? Events = null, DateTimeOffset? FetchedAt = null)
{
    [JsonIgnore] public CalendarIdentity[]? Identities { get; init; }
    public CalendarAppointment? Next(DateTimeOffset now) => Status is "connected" or "empty"
        ? Events?.Where(e => e.End > now || e.Start >= now)
            .OrderBy(e => !e.AllDay && e.Start <= now && e.End > now ? 0 : e.AllDay ? 2 : 1)
            .ThenBy(e => e.Start).FirstOrDefault() : null;
}
public static class GoogleCalendarUrl
{
    public static bool IsValid(string? value) => value is { Length: > 0 and <= 2048 }
        && !value.Any(char.IsControl) && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "calendar.google.com" && uri.Port == 443
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && uri.AbsolutePath.StartsWith("/calendar/ical/", StringComparison.Ordinal)
        && uri.AbsolutePath.EndsWith(".ics", StringComparison.OrdinalIgnoreCase);
}
public static class CalendarParser
{
    public const int MaximumBytes = 4 * 1024 * 1024;
    private static bool HasStableEventIds(string text)
    {
        // Ical.Net assigns a random UID to malformed VEVENTs that omit it.
        // Keep their appointments renderable but suppress identity-based notifications
        // for an incomplete feed, rather than inventing arrivals on every refresh.
        var unfolded = System.Text.RegularExpressions.Regex.Replace(text, @"\r?\n[ \t]", "");
        var depth = 0; var hasUid = false;
        foreach (var raw in unfolded.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) { depth = 1; hasUid = false; }
            else if (depth > 0 && line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase)) depth++;
            else if (depth > 0 && line.StartsWith("END:", StringComparison.OrdinalIgnoreCase))
            {
                if (--depth == 0 && !hasUid) return false;
            }
            else if (depth == 1 && (line.StartsWith("UID:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("UID;", StringComparison.OrdinalIgnoreCase)))
            {
                var colon = line.IndexOf(':');
                hasUid |= colon >= 0 && !string.IsNullOrWhiteSpace(line[(colon + 1)..]);
            }
        }
        return true;
    }
    public static CalendarSnapshot Parse(string text, DateTimeOffset now, CancellationToken token = default, TimeZoneInfo? localZone = null)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaximumBytes || !text.TrimStart('\uFEFF', ' ', '\r', '\n').StartsWith("BEGIN:VCALENDAR", StringComparison.Ordinal)
            || !text.TrimEnd().EndsWith("END:VCALENDAR", StringComparison.Ordinal)) throw new InvalidDataException("Invalid calendar.");
        token.ThrowIfCancellationRequested();
        var calendar = Calendar.Load(text.TrimStart('\uFEFF', ' ', '\r', '\n')) ?? throw new InvalidDataException("Invalid calendar.");
        if (calendar.Events.Count > 3000) throw new InvalidDataException("Calendar too large.");
        var zoneName = calendar.Properties.Get<string>("X-WR-TIMEZONE");
        var zone = string.IsNullOrWhiteSpace(zoneName) ? localZone ?? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(zoneName);
        DateTimeOffset Instant(CalDateTime date) => new(date.IsFloating || !date.HasTime
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Value, DateTimeKind.Unspecified), date.HasTime ? zone : localZone ?? TimeZoneInfo.Local) : date.AsUtc);
        // High-frequency rules are not suitable for an appointment panel; bound evaluation work.
        if (calendar.Events.Any(e => e.Properties.GetMany<RecurrencePattern>("RRULE").Any(r => r.Frequency is FrequencyType.Secondly or FrequencyType.Minutely)))
            throw new InvalidDataException("Calendar recurrence too frequent.");
        string? Identity(CalendarEvent entry) => string.IsNullOrWhiteSpace(entry.Uid) ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.Uid)));
        var identities = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var entry in calendar.Events)
        {
            token.ThrowIfCancellationRequested();
            if (Identity(entry) is not { } id) continue;
            var future = entry.IsActive && entry.DtStart is { } date && Instant(date) >= now;
            identities[id] = identities.GetValueOrDefault(id) || future;
        }
        var events = new List<CalendarAppointment>();
        var horizon = now.AddDays(7);
        var occurrences = calendar.GetOccurrences<CalendarEvent>(new CalDateTime(now.AddDays(-2).UtcDateTime),
            new EvaluationOptions { MaxUnmatchedIncrementsLimit = 1000 })
            .TakeWhileBefore(new CalDateTime(horizon.AddDays(2).UtcDateTime)).Take(10001);
        var count = 0;
        foreach (var occurrence in occurrences)
        {
            token.ThrowIfCancellationRequested();
            if (++count > 10000) throw new InvalidDataException("Too many calendar occurrences.");
            if (occurrence.Source is not CalendarEvent entry || !entry.IsActive) continue;
            var start = Instant(occurrence.Period.StartTime);
            var allDay = !occurrence.Period.StartTime.HasTime;
            var end = occurrence.Period.EffectiveEndTime is { } finish ? Instant(finish) : start;
            if (end <= now && start < now || start >= horizon) continue;
            if (Identity(entry) is { } id) identities[id] = true;
            var title = new string((entry.Summary ?? "").Select(c => char.IsControl(c) ? ' ' : c).Take(240).ToArray()).Trim();
            events.Add(new(title.Length == 0 ? "Impegno" : title, start, end, allDay));
        }
        var result = events.OrderBy(e => e.Start).Take(100).ToArray();
        return new(result.Length == 0 ? "empty" : "connected", result, now)
            { Identities = HasStableEventIds(text) ? identities.Select(p => new CalendarIdentity(p.Key, p.Value)).ToArray() : null };
    }
}

// Single collection-loop owner. Fetch and recurrence evaluation never run on the render thread.
public sealed class CalendarFeed(HttpClient client, TimeProvider? timeProvider = null) : IDisposable
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private string? selected;
    private CalendarSnapshot snapshot = new();
    private Task<CalendarSnapshot>? pending;
    private CancellationTokenSource? request;
    private DateTimeOffset? lastCompleted;
    private bool lastFailed;
    private int revision, pendingRevision;
    public int SourceRevision => revision;
    public CalendarSnapshot Read(CalendarOptions options, string? url, CancellationToken stoppingToken)
    {
        var next = options.Enabled && GoogleCalendarUrl.IsValid(url) ? url : null;
        if (selected != next)
        {
            request?.Cancel(); revision++; selected = next; snapshot = new(next is null ? "not-configured" : "loading");
            lastCompleted = null; lastFailed = false;
            // Keep the old task until it ends; do not queue abandoned CPU work on repeated edits.
        }
        if (pending?.IsCompleted == true)
        {
            var result = pending.GetAwaiter().GetResult(); pending = null; request!.Dispose(); request = null;
            if (pendingRevision == revision && selected is not null)
            {
                snapshot = result;
                lastCompleted = clock.GetUtcNow();
                lastFailed = result.Status == "unavailable";
            }
        }
        if (next is null) return new(options.Enabled ? "not-configured" : "disabled");
        var now = clock.GetUtcNow();
        var interval = Math.Clamp(options.PollMinutes, 1, 60);
        var nextFetch = lastCompleted?.AddMinutes(lastFailed ? Math.Max(2, interval) : interval) ?? DateTimeOffset.MinValue;
        if (snapshot.FetchedAt is { } fetched && now - fetched > TimeSpan.FromMinutes(Math.Max(15, interval * 3))) snapshot = new("unavailable");
        if (pending is null && now >= nextFetch && !stoppingToken.IsCancellationRequested)
        {
            request = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            request.CancelAfter(TimeSpan.FromSeconds(15));
            var fetchToken = request.Token; pendingRevision = revision;
            pending = Task.Run(() => Fetch(client, next, now, fetchToken));
        }
        return options.HideTitles ? snapshot with { Events = snapshot.Events?.Select(e => e with { Title = "Impegno" }).ToArray() } : snapshot;
    }
    public static async Task<CalendarSnapshot> Fetch(HttpClient client, string url, DateTimeOffset now, CancellationToken token)
    {
        if (!GoogleCalendarUrl.IsValid(url)) return new("unavailable");
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > CalendarParser.MaximumBytes) return new("unavailable");
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var bytes = new byte[CalendarParser.MaximumBytes + 1]; var length = 0;
            while (length < bytes.Length)
            {
                var n = await stream.ReadAsync(bytes.AsMemory(length), token);
                if (n == 0) break;
                length += n;
            }
            if (length > CalendarParser.MaximumBytes) return new("unavailable");
            return CalendarParser.Parse(new UTF8Encoding(false, true).GetString(bytes, 0, length), now, token);
        }
        catch { return new("unavailable"); } // Never log URLs, event contents or parser exceptions.
    }
    public void Dispose() { request?.Cancel(); request?.Dispose(); }
}
