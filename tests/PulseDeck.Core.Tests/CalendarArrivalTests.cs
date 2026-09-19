using System.Text.Json;
using PulseDeck.Core;
using Xunit;

public class CalendarArrivalTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);
    private static string Event(string id, string date = "20260920T120000Z", string extra = "") => $"BEGIN:VEVENT\nUID:{id}\nDTSTART:{date}\nDURATION:PT1H\nSUMMARY:Private title\n{extra}\nEND:VEVENT";
    private static CalendarSnapshot Parse(DateTimeOffset now, params string[] events) => CalendarParser.Parse("BEGIN:VCALENDAR\nVERSION:2.0\n" + string.Join('\n', events) + "\nEND:VCALENDAR", now);
    [Fact] public void WholeFeedBaselinePreventsHorizonAndEditDuplicates()
    {
        var tracker = new CalendarArrivalTracker();
        var baseline = Parse(Now, Event("near"), Event("far", "20261001T120000Z"));
        Assert.Single(baseline.Events!); Assert.Equal(2, baseline.Identities!.Length);
        Assert.Equal(0, tracker.Observe(baseline, 1, true));
        var updated = Parse(Now.AddMinutes(5), Event("near", "20260921T120000Z", "SEQUENCE:2"), Event("far", "20261001T120000Z"));
        Assert.Equal(0, tracker.Observe(updated, 1, true));
        var later = Parse(Now.AddDays(7), Event("far", "20261001T120000Z"));
        Assert.Equal(0, tracker.Observe(later, 1, true));
    }
    [Fact] public void GroupsNewFutureEventsAndIgnoresPastCancelledAndExistingSeries()
    {
        var tracker = new CalendarArrivalTracker();
        tracker.Observe(Parse(Now, Event("series", extra: "RRULE:FREQ=DAILY;COUNT=5")), 1, true);
        var added = Parse(Now.AddMinutes(5), Event("series", extra: "RRULE:FREQ=DAILY;COUNT=10"),
            Event("invite"), Event("far-invite", "20261101T120000Z"), Event("past", "20260901T120000Z"), Event("cancel", extra: "STATUS:CANCELLED"));
        Assert.Equal(2, tracker.Observe(added, 1, true));
        Assert.Equal(0, tracker.Observe(added, 1, true));
    }
    [Fact] public void FailureAndTemporaryOmissionDoNotReplayAndNewSourceIsQuiet()
    {
        var tracker = new CalendarArrivalTracker();
        tracker.Observe(Parse(Now, Event("one")), 1, true);
        Assert.Equal(0, tracker.Observe(new("unavailable"), 1, true));
        Assert.Equal(0, tracker.Observe(Parse(Now.AddMinutes(5)), 1, true));
        Assert.Equal(1, tracker.Observe(Parse(Now.AddMinutes(10), Event("one"), Event("two")), 1, true));
        Assert.Equal(0, tracker.Observe(Parse(Now.AddMinutes(15), Event("other-calendar")), 2, true));
    }
    [Fact] public void DisabledNotificationsStillConsumeChangesAndIdentitiesArePrivate()
    {
        var tracker = new CalendarArrivalTracker(); tracker.Observe(Parse(Now), 1, true);
        var added = Parse(Now.AddMinutes(5), Event("private-uid@example.com"));
        Assert.Equal(0, tracker.Observe(added, 1, false));
        Assert.Equal(0, tracker.Observe(Parse(Now.AddMinutes(10), Event("private-uid@example.com")), 1, true));
        var serialized = JsonSerializer.Serialize(added);
        Assert.DoesNotContain("Identities", serialized); Assert.DoesNotContain("private-uid", serialized);
        Assert.All(added.Identities!, i => Assert.Equal(64, i.Id.Length));
    }
    [Fact] public void MissingUidDoesNotInventAnArrivalIdentity()
    {
        var value = Parse(Now, Event("missing").Replace("UID:missing\n", ""));
        Assert.Null(value.Identities);
    }
    [Fact] public void CalendarAndMailDoNotShareTheirBurstWindow()
    {
        var center = new NotificationCenter();
        center.Publish(new("gmail", "mail", "connected", 2), new("gmail", "mail", "Mail", "GMAIL"));
        center.Publish(new("calendar", "calendar", "connected"), new("calendar", "calendar", "Event", "CALENDAR"));
        Assert.Equal("calendar", center.Read().Arrival!.Kind);
        center.Read(false); Assert.Null(center.Read().Arrival);
        center.StartTest("calendar"); var test = center.Read();
        Assert.True(test.IsTest); Assert.Equal("calendar", test.Arrival!.Kind);
        Assert.Equal(2, test.Sources.Single(s => s.Id == "gmail").UnreadCount);
        center.Read(false); Assert.False(center.Read().IsTest);
    }
}
