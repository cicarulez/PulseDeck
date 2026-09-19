using System.Net;
using System.Text;
using PulseDeck.Core;
using Xunit;

namespace PulseDeck.Core.Tests;
public class NotificationTests
{
    private sealed class Clock : TimeProvider
    {
        public long Seconds;
        public override long GetTimestamp() => Seconds * TimestampFrequency;
    }
    [Fact] public void CountsDoNotAnimateAndBurstDoesNotRestart()
    {
        var clock = new Clock(); var center = new NotificationCenter(clock);
        center.Publish(new("gmail", "mail", "connected", 42));
        Assert.Null(center.Read().Arrival);
        var arrival = new NotificationEvent("gmail", "mail", "New", "Gmail");
        center.Publish(new("gmail", "mail", "connected", 43), arrival);
        clock.Seconds = 2;
        center.Publish(new("gmail", "mail", "connected", 44), arrival);
        Assert.Equal(2, center.Read().Seconds); Assert.Equal(44, center.Read().Sources[0].UnreadCount);
        Assert.Equal(1, center.AnimationsStarted);
        clock.Seconds = 5; Assert.Null(center.Read().Arrival);
        center.Publish(new("gmail", "mail", "connected", 30)); Assert.Null(center.Read().Arrival);
        clock.Seconds = 15; center.Publish(new("gmail", "mail", "connected", 31), arrival);
        Assert.Equal(2, center.AnimationsStarted);
        center.Read(false); Assert.Null(center.Read().Arrival); // No replay on profile return.
    }
    [Fact] public void TestRestoresRealStateAndUnavailableHasNoStaleCount()
    {
        var clock = new Clock(); var center = new NotificationCenter(clock);
        center.Publish(new("gmail", "mail", "connected", 99)); center.StartTest();
        Assert.True(center.Read().IsTest); clock.Seconds = 2; Assert.Equal(3, center.Read().Sources[0].UnreadCount);
        clock.Seconds = 6; Assert.Equal(0, center.Read().Sources[0].UnreadCount);
        clock.Seconds = 9; Assert.Equal(99, center.Read().Sources[0].UnreadCount);
        center.Publish(new("gmail", "mail", "unavailable")); Assert.Null(center.Read().Sources[0].UnreadCount);
    }
    [Theory]
    [InlineData(14, false)] [InlineData(15, true)] [InlineData(300, true)] [InlineData(301, false)]
    public void GmailPollIntervalHasEnforcedLimits(int seconds, bool valid) =>
        Assert.Equal(valid, (new DeckConfig { Notifications = new() { PollSeconds = seconds } }).Validate() is null);
    private sealed class Handler : HttpMessageHandler
    {
        public Queue<(HttpStatusCode Code, string Json)> Replies = new();
        public List<string> Paths = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Paths.Add(request.RequestUri!.PathAndQuery);
            var reply = Replies.Dequeue();
            return Task.FromResult(new HttpResponseMessage(reply.Code) { Content = new StringContent(reply.Json, Encoding.UTF8, "application/json") });
        }
        public void Ok(string json) => Replies.Enqueue((HttpStatusCode.OK, json));
    }
    [Fact] public async Task BaselinePaginationReadAndExpiredHistory()
    {
        var handler = new Handler(); using var client = new HttpClient(handler); var mailbox = new GmailMailbox(client);
        handler.Ok("{\"historyId\":\"10\"}"); handler.Ok("{\"messagesUnread\":42}");
        Assert.Equal(new GmailUpdate(42, false), await mailbox.Read("test", default));
        handler.Ok("""{"historyId":"20","nextPageToken":"page","history":[{"messagesAdded":[{"message":{"id":"a","labelIds":["UNREAD","INBOX"]}}]}]}""");
        handler.Ok("""{"historyId":"20","history":[{"messagesAdded":[{"message":{"id":"b","labelIds":["UNREAD"]}}]}]}""");
        handler.Ok("{\"messagesUnread\":42}"); // Equal count, but new mail arrived while another was read.
        Assert.Equal(new GmailUpdate(42, true), await mailbox.Read("test", default));
        Assert.Contains(handler.Paths, p => p.Contains("pageToken=page"));
        handler.Ok("{\"historyId\":\"30\"}"); handler.Ok("{\"messagesUnread\":0}");
        Assert.Equal(new GmailUpdate(0, false), await mailbox.Read("test", default));
        handler.Replies.Enqueue((HttpStatusCode.NotFound, "{}"));
        handler.Ok("{\"historyId\":\"50\"}"); handler.Ok("{\"messagesUnread\":8}");
        Assert.Equal(new GmailUpdate(8, false), await mailbox.Read("test", default));
    }
    [Theory]
    [InlineData("[\"UNREAD\"]", false)]
    [InlineData("[\"INBOX\"]", false)]
    [InlineData("[\"INBOX\",\"UNREAD\"]", true)]
    [InlineData("[\"SPAM\",\"UNREAD\"]", false)]
    [InlineData("[\"INBOX\",\"UNREAD\",\"SPAM\"]", false)]
    [InlineData("[\"INBOX\",\"UNREAD\",\"TRASH\"]", false)]
    public async Task OnlyUnreadInboxMessagesAnimate(string labels, bool expected)
    {
        var handler = new Handler(); using var client = new HttpClient(handler); var mailbox = new GmailMailbox(client);
        handler.Ok("{\"historyId\":\"10\"}"); handler.Ok("{\"messagesUnread\":3}");
        Assert.Equal(new GmailUpdate(3, false), await mailbox.Read("test", default));
        Assert.EndsWith("labels/INBOX?fields=messagesUnread", handler.Paths[^1]);
        handler.Ok("{\"historyId\":\"20\",\"history\":[{\"messagesAdded\":[{\"message\":{\"id\":\"a\",\"labelIds\":" + labels + "}}]}]}");
        handler.Ok("{\"messagesUnread\":2}");
        Assert.Equal(new GmailUpdate(2, expected), await mailbox.Read("test", default));
        Assert.EndsWith("labels/INBOX?fields=messagesUnread", handler.Paths[^1]);
    }
    [Fact] public async Task HistoryWithoutLabelsFetchesOnlyMetadataAndIgnoresDeletedMessages()
    {
        var handler = new Handler(); using var client = new HttpClient(handler); var mailbox = new GmailMailbox(client);
        handler.Ok("{\"historyId\":\"10\"}"); handler.Ok("{\"messagesUnread\":1}"); await mailbox.Read("test", default);
        handler.Ok("""{"historyId":"20","history":[{"messagesAdded":[{"message":{"id":"a"}},{"message":{"id":"deleted"}}]}]}""");
        handler.Ok("""{"labelIds":["UNREAD","INBOX"]}"""); handler.Replies.Enqueue((HttpStatusCode.NotFound, "{}"));
        handler.Ok("{\"messagesUnread\":2}"); Assert.True((await mailbox.Read("test", default)).NewMail);
        Assert.Contains(handler.Paths, p => p.EndsWith("messages/a?format=metadata&fields=labelIds"));
    }
    [Fact] public async Task FailureDoesNotAdvanceCursorAndSentSpamAreNotArrivals()
    {
        var handler = new Handler(); using var client = new HttpClient(handler); var mailbox = new GmailMailbox(client);
        handler.Ok("{\"historyId\":\"10\"}"); handler.Ok("{\"messagesUnread\":1}"); await mailbox.Read("test", default);
        handler.Ok("{\"historyId\":\"20\"}"); handler.Replies.Enqueue((HttpStatusCode.ServiceUnavailable, "{}"));
        await Assert.ThrowsAsync<HttpRequestException>(() => mailbox.Read("test", default));
        handler.Ok("""{"historyId":"30","history":[{"messagesAdded":[{"message":{"id":"x","labelIds":["UNREAD","SENT"]}},{"message":{"id":"y","labelIds":["UNREAD","SPAM"]}}]}]}""");
        handler.Ok("{\"messagesUnread\":1}"); Assert.False((await mailbox.Read("test", default)).NewMail);
        Assert.Contains("startHistoryId=10", handler.Paths[^2]);
    }
}
