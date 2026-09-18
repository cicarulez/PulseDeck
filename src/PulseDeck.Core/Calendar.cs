using System.Text;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;

namespace PulseDeck.Core;

public sealed record CalendarOptions
{
    public bool Enabled { get; init; } = true;
    public bool HideTitles { get; init; }
}
public sealed record CalendarAppointment(string Title, DateTimeOffset Start, DateTimeOffset End, bool AllDay);
public sealed record CalendarSnapshot(string Status = "not-configured", CalendarAppointment[]? Events = null, DateTimeOffset? FetchedAt = null)
{
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
            var title = new string((entry.Summary ?? "").Select(c => char.IsControl(c) ? ' ' : c).Take(240).ToArray()).Trim();
            events.Add(new(title.Length == 0 ? "Impegno" : title, start, end, allDay));
        }
        var result = events.OrderBy(e => e.Start).Take(100).ToArray();
        return new(result.Length == 0 ? "empty" : "connected", result, now);
    }
}

// Single render-loop owner. Fetch and recurrence evaluation never run on the render thread.
public sealed class CalendarFeed(HttpClient client, TimeProvider? timeProvider = null) : IDisposable
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private string? selected;
    private CalendarSnapshot snapshot = new();
    private Task<CalendarSnapshot>? pending;
    private CancellationTokenSource? request;
    private DateTimeOffset nextFetch;
    private int revision, pendingRevision;
    public CalendarSnapshot Read(CalendarOptions options, string? url, CancellationToken stoppingToken)
    {
        var next = options.Enabled && GoogleCalendarUrl.IsValid(url) ? url : null;
        if (selected != next)
        {
            request?.Cancel(); revision++; selected = next; snapshot = new(next is null ? "not-configured" : "loading");
            nextFetch = DateTimeOffset.MinValue;
            // Keep the old task until it ends; do not queue abandoned CPU work on repeated edits.
        }
        if (pending?.IsCompleted == true)
        {
            var result = pending.GetAwaiter().GetResult(); pending = null; request!.Dispose(); request = null;
            if (pendingRevision == revision && selected is not null)
            {
                snapshot = result;
                nextFetch = clock.GetUtcNow().AddMinutes(result.Status == "unavailable" ? 2 : 5);
            }
        }
        if (next is null) return new(options.Enabled ? "not-configured" : "disabled");
        var now = clock.GetUtcNow();
        if (snapshot.FetchedAt is { } fetched && now - fetched > TimeSpan.FromMinutes(15)) snapshot = new("unavailable");
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
