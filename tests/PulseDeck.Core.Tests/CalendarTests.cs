using System.Net;
using PulseDeck.Core;
using Xunit;

public class CalendarTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);
    private const string Url = "https://calendar.google.com/calendar/ical/fixture/private-test/basic.ics";
    private static string Event(string uid, string fields) => $"BEGIN:VEVENT\nUID:{uid}\n{fields}\nEND:VEVENT";
    private static string Feed(params string[] events) => "BEGIN:VCALENDAR\nVERSION:2.0\nX-WR-TIMEZONE:Europe/Rome\n" + string.Join('\n', events) + "\nEND:VCALENDAR";
    private static CalendarSnapshot Parse(string text, DateTimeOffset? now = null) => CalendarParser.Parse(text, now ?? Now, localZone: TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"));

    [Fact]
    public void SelectsOngoingThenUpcomingAndIgnoresEndedOrCancelledEvents()
    {
        var calendar = Parse(Feed(Event("ended", "DTSTART:20260918T070000Z\nDTEND:20260918T080000Z\nSUMMARY:Past"),
            Event("active", "DTSTART:20260918T083000Z\nDTEND:20260918T093000Z\nSUMMARY:Current"),
            Event("next", "DTSTART:20260918T100000Z\nDURATION:PT1H\nSUMMARY:Next"),
            Event("cancel", "DTSTART:20260918T090000Z\nDTEND:20260918T100000Z\nSTATUS:CANCELLED\nSUMMARY:Cancelled")));
        Assert.Equal(2, calendar.Events!.Length);
        Assert.Equal("Current", calendar.Next(Now)!.Title);
        Assert.Equal("Next", calendar.Next(Now.AddMinutes(30))!.Title);
        Assert.Null(calendar.Next(Now.AddHours(3)));
    }
    [Fact]
    public void ResolvesRecurringExceptionsMovesAndCancellationWithoutDuplicates()
    {
        var calendar = Parse(Feed(
            Event("series", "DTSTART;TZID=Europe/Rome:20260917T120000\nDTEND;TZID=Europe/Rome:20260917T130000\nRRULE:FREQ=DAILY;COUNT=5\nEXDATE;TZID=Europe/Rome:20260919T120000\nSUMMARY:Regular"),
            Event("series", "RECURRENCE-ID;TZID=Europe/Rome:20260918T120000\nDTSTART;TZID=Europe/Rome:20260918T140000\nDTEND;TZID=Europe/Rome:20260918T150000\nSUMMARY:Moved"),
            Event("series", "RECURRENCE-ID;TZID=Europe/Rome:20260920T120000\nDTSTART;TZID=Europe/Rome:20260920T120000\nDTEND;TZID=Europe/Rome:20260920T130000\nSTATUS:CANCELLED\nSUMMARY:Cancelled")));
        Assert.Equal(2, calendar.Events!.Length);
        Assert.Equal("Moved", calendar.Events[0].Title);
        Assert.Equal(Now.AddHours(3), calendar.Events[0].Start);
        Assert.Equal(21, calendar.Events[1].Start.Day);
    }
    [Fact]
    public void PreservesLocalClockAcrossDaylightSavingTransition()
    {
        var calendar = Parse(Feed(Event("dst", "DTSTART;TZID=Europe/Rome:20261024T100000\nDURATION:PT1H\nRRULE:FREQ=DAILY;COUNT=3\nSUMMARY:Meeting")), new(2026,10,24,0,0,0,TimeSpan.Zero));
        Assert.Equal(new[] { 8, 9, 9 }, calendar.Events!.Select(e => e.Start.UtcDateTime.Hour));
    }
    [Fact]
    public void HandlesFoldedEscapedTextFloatingTimeAndExclusiveAllDayEnd()
    {
        var calendar = Parse(Feed(Event("day", "DTSTART;VALUE=DATE:20260918\nDTEND;VALUE=DATE:20260919\nSUMMARY:All day"),
            Event("floating", "DTSTART:20260918T120000\nDTEND:20260918T130000\nSUMMARY:Team\\, review and\n  planning")));
        Assert.Equal("Team, review and planning", calendar.Next(Now)!.Title);
        Assert.Equal(10, calendar.Next(Now)!.Start.UtcDateTime.Hour);
        Assert.True(calendar.Events![0].AllDay);
        Assert.Null(calendar.Next(new(2026,9,18,22,0,0,TimeSpan.Zero)));
    }
    [Fact]
    public void AllDayDatesStayOnTheViewersDayEvenWhenCalendarUsesAnotherTimeZone()
    {
        var calendar = Parse(Feed(Event("day", "DTSTART;VALUE=DATE:20260918\nDTEND;VALUE=DATE:20260919\nSUMMARY:All day")).Replace("Europe/Rome", "Asia/Tokyo"));
        Assert.Equal(new DateTimeOffset(2026,9,17,22,0,0,TimeSpan.Zero), calendar.Events![0].Start);
        Assert.Equal(new DateTimeOffset(2026,9,18,22,0,0,TimeSpan.Zero), calendar.Events[0].End);
    }
    [Fact]
    public void KeepsDistinctSimultaneousEventsAndEmptyCalendars()
    {
        var calendar = Parse(Feed(Event("one", "DTSTART:20260918T100000Z\nDTEND:20260918T110000Z\nSUMMARY:One"),
            Event("two", "DTSTART:20260918T100000Z\nDTEND:20260918T110000Z\nSUMMARY:Two")));
        Assert.Equal(2, calendar.Events!.Length);
        Assert.Equal("empty", Parse(Feed()).Status);
        Assert.ThrowsAny<Exception>(() => Parse("<html>Login</html>"));
        Assert.ThrowsAny<Exception>(() => Parse(Feed(Event("fast", "DTSTART:20260918T100000Z\nRRULE:FREQ=SECONDLY\nSUMMARY:Fast"))));
        Assert.ThrowsAny<Exception>(() => Parse(new string('x', CalendarParser.MaximumBytes + 1)));
    }
    [Theory]
    [InlineData(Url, true)]
    [InlineData("https://calendar.google.com.evil.example/calendar/ical/test/basic.ics", false)]
    [InlineData("http://calendar.google.com/calendar/ical/test/basic.ics", false)]
    [InlineData("https://user:pass@calendar.google.com/calendar/ical/test/basic.ics", false)]
    [InlineData("https://calendar.google.com/calendar/ical/test/basic.ics?redirect=elsewhere", false)]
    [InlineData("https://127.0.0.1/calendar/ical/test/basic.ics", false)]
    public void RestrictsCalendarLinksToGoogle(string url, bool valid) => Assert.Equal(valid, GoogleCalendarUrl.IsValid(url));

    private sealed class Clock : TimeProvider { public DateTimeOffset Now = CalendarTests.Now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public string Text = Feed(Event("live", "DTSTART:20260918T100000Z\nDTEND:20260918T110000Z\nSUMMARY:Private fixture"));
        public bool Fail;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent(Text) }); }
    }
    private sealed class DelayedHandler : HttpMessageHandler
    {
        public int Calls;
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref Calls) == 1) { Started.SetResult(); await Release.Task; }
            return new(HttpStatusCode.OK) { Content = new StringContent(Feed(Event("source", "DTSTART:20260918T100000Z\nDTEND:20260918T110000Z\nSUMMARY:" + (request.RequestUri!.AbsolutePath.Contains("replacement") ? "Replacement" : "Old")))) };
        }
    }
    [Fact]
    public async Task SourceChangeDoesNotReuseOldEventsOrQueueConcurrentFetches()
    {
        var handler = new DelayedHandler(); using var client = new HttpClient(handler); using var feed = new CalendarFeed(client, new Clock());
        Assert.Equal("loading", feed.Read(new(), Url, default).Status);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var replacement = Url.Replace("fixture", "replacement");
        Assert.Null(feed.Read(new(), replacement, default).Events);
        Assert.Equal(1, handler.Calls);
        handler.Release.SetResult();
        var result = await Settle(feed, new(), replacement);
        Assert.Equal("Replacement", result.Events!.Single().Title);
        Assert.Equal(2, handler.Calls);
    }
    private static async Task<CalendarSnapshot> Settle(CalendarFeed feed, CalendarOptions options, string? url)
    {
        for (var i = 0; i < 200; i++)
        {
            var state = feed.Read(options, url, default);
            if (state.Status != "loading") return state;
            await Task.Delay(5);
        }
        throw new TimeoutException();
    }
    [Fact]
    public async Task FetchesOffThreadRedactsTitlesAndClearsOnDisconnectOrDisable()
    {
        var handler = new Handler(); using var client = new HttpClient(handler); var clock = new Clock();
        using var feed = new CalendarFeed(client, clock);
        Assert.Equal("not-configured", feed.Read(new(), null, default).Status);
        var state = await Settle(feed, new(), Url);
        Assert.Equal("Private fixture", state.Events![0].Title);
        Assert.Equal("Impegno", feed.Read(new() { HideTitles = true }, Url, default).Events![0].Title);
        Assert.Equal(1, handler.Calls);
        Assert.Null(feed.Read(new() { Enabled = false }, Url, default).Events);
        Assert.Equal("not-configured", feed.Read(new(), null, default).Status);
    }
    [Fact]
    public async Task NewFeedIdentityTriggersOnceWithoutExposingTitlesOrAcceleratingPolls()
    {
        var handler = new Handler(); using var client = new HttpClient(handler); var clock = new Clock();
        using var feed = new CalendarFeed(client, clock); var tracker = new CalendarArrivalTracker();
        var options = new CalendarOptions { HideTitles = true };
        var baseline = await Settle(feed, options, Url);
        Assert.Equal(0, tracker.Observe(baseline, feed.SourceRevision, options.NotifyNewEvents));
        handler.Text = Feed(Event("live", "DTSTART:20260918T100000Z\nDTEND:20260918T110000Z\nSUMMARY:Changed title"),
            Event("new-invite", "DTSTART:20261018T100000Z\nDTEND:20261018T110000Z\nSUMMARY:Private future invitation"));
        for (var i = 0; i < 100; i++) feed.Read(options, Url, default);
        Assert.Equal(1, handler.Calls);
        clock.Now += TimeSpan.FromMinutes(5);
        var snapshot = feed.Read(options, Url, default);
        for (var i = 0; i < 200 && snapshot.FetchedAt != clock.Now; i++)
        { await Task.Delay(5); snapshot = feed.Read(options, Url, default); }
        Assert.Equal(clock.Now, snapshot.FetchedAt);
        Assert.Equal(1, tracker.Observe(snapshot, feed.SourceRevision, options.NotifyNewEvents));
        Assert.Equal(0, tracker.Observe(feed.Read(options, Url, default), feed.SourceRevision, options.NotifyNewEvents));
        Assert.Equal(2, handler.Calls);
        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("Private", json); Assert.DoesNotContain("Changed", json); Assert.DoesNotContain("Identities", json);
    }
    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(5, true)]
    [InlineData(60, true)] [InlineData(61, false)]
    public void PollIntervalValidationAndLegacyDefault(int minutes, bool valid)
    {
        Assert.Equal(valid, (new DeckConfig { Calendar = new() { PollMinutes = minutes } }).Validate() is null);
        Assert.Equal(5, System.Text.Json.JsonSerializer.Deserialize<CalendarOptions>("{}")!.PollMinutes);
    }
    [Fact]
    public async Task IntervalEditsRescheduleWithoutResettingBaselineOrExpiringLongIntervals()
    {
        var handler = new Handler(); using var client = new HttpClient(handler); var clock = new Clock();
        using var feed = new CalendarFeed(client, clock);
        await Settle(feed, new(), Url); var revision = feed.SourceRevision;
        clock.Now += TimeSpan.FromMinutes(2);
        feed.Read(new(), Url, default); Assert.Equal(1, handler.Calls);
        var options = new CalendarOptions { PollMinutes = 1 };
        CalendarSnapshot state = feed.Read(options, Url, default);
        for (var i = 0; i < 200 && state.FetchedAt != clock.Now; i++)
        { await Task.Delay(5); state = feed.Read(options, Url, default); }
        Assert.Equal(clock.Now, state.FetchedAt); Assert.Equal(2, handler.Calls);
        Assert.Equal(revision, feed.SourceRevision);
        options = options with { PollMinutes = 60 };
        clock.Now += TimeSpan.FromMinutes(59);
        Assert.Equal("connected", feed.Read(options, Url, default).Status);
        Assert.Equal(2, handler.Calls);
        clock.Now += TimeSpan.FromMinutes(1);
        state = feed.Read(options, Url, default);
        for (var i = 0; i < 200 && state.FetchedAt != clock.Now; i++)
        { await Task.Delay(5); state = feed.Read(options, Url, default); }
        Assert.Equal(clock.Now, state.FetchedAt); Assert.Equal(3, handler.Calls);
    }
    [Fact]
    public async Task MinimumIntervalStillBacksOffAfterFailure()
    {
        var handler = new Handler { Fail = true }; using var client = new HttpClient(handler); var clock = new Clock();
        using var feed = new CalendarFeed(client, clock); var options = new CalendarOptions { PollMinutes = 1 };
        Assert.Equal("unavailable", (await Settle(feed, options, Url)).Status);
        clock.Now += TimeSpan.FromMinutes(1);
        feed.Read(options, Url, default); Assert.Equal(1, handler.Calls);
        clock.Now += TimeSpan.FromMinutes(1);
        feed.Read(options, Url, default);
        for (var i = 0; i < 200 && handler.Calls < 2; i++) await Task.Delay(5);
        Assert.Equal(2, handler.Calls);
    }
    [Fact]
    public async Task FailureRemovesOldEventsAndRetriesAfterBackoff()
    {
        var handler = new Handler(); using var client = new HttpClient(handler); var clock = new Clock();
        using var feed = new CalendarFeed(client, clock);
        Assert.Equal("connected", (await Settle(feed, new(), Url)).Status);
        clock.Now += TimeSpan.FromMinutes(5); handler.Fail = true;
        feed.Read(new(), Url, default);
        CalendarSnapshot state = new();
        for (var i = 0; i < 200; i++) { state = feed.Read(new(), Url, default); if (state.Status == "unavailable") break; await Task.Delay(5); }
        Assert.Equal("unavailable", state.Status); Assert.Null(state.Events);
        var calls = handler.Calls; feed.Read(new(), Url, default); Assert.Equal(calls, handler.Calls);
    }
}
