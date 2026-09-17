using System.Net;
using System.Text;
using System.Xml;
using PulseDeck.Core;
using Xunit;

public class NewsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 18, 0, 0, TimeSpan.Zero);
    private static readonly NewsChannel Channel = new("Synthetic source", "https://example.com/feed");
    private static string Rss(string title = "Synthetic headline") => $"<rss version='2.0'><channel><item><title>{title}</title><link>https://example.com/article</link><pubDate>Thu, 17 Sep 2026 17:00:00 +0000</pubDate></item></channel></rss>";
    private static NewsItem[] Parse(string xml) => NewsParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)), Channel, Now);

    [Fact]
    public void DefaultsAndFeedValidationKeepRequestsOptInAndPublic()
    {
        Assert.False(new DeckConfig().News.Enabled);
        Assert.Null(new DeckConfig().Validate());
        foreach (var url in new[] { "file:///tmp/feed", "http://example.com/feed", "https://user:pass@example.com/feed", "https://127.0.0.1/feed", "https://192.168.1.2/feed", "https://localhost/feed", "https://example.com:444/feed" })
            Assert.NotNull(new NewsOptions { Channels = [Channel with { Url = url }] }.Validate());
        Assert.NotNull(new NewsOptions { Channels = [Channel, Channel] }.Validate());
        Assert.NotNull(new NewsOptions { RotationSeconds = 0 }.Validate());
        Assert.NotNull(new NewsOptions { FontSize = 30 }.Validate());
        Assert.NotNull((new DeckConfig { News = null! }).Validate());
        foreach (var ip in new[] { "127.0.0.1", "10.0.0.2", "172.16.0.1", "169.254.1.1", "::1", "fe80::1", "fc00::1", "::ffff:192.168.0.1" }) Assert.False(NewsUrls.IsPublic(IPAddress.Parse(ip)));
        Assert.True(NewsUrls.IsPublic(IPAddress.Parse("1.1.1.1")));
        Assert.True(NewsUrls.IsPublic(IPAddress.Parse("2606:4700:4700::1111")));
    }
    [Fact]
    public void RssAndAtomDecodeTextAndFilterUnsafeLinksDatesAndDuplicates()
    {
        var item = Assert.Single(Parse(Rss("&lt;b&gt;Synthetic&lt;/b&gt; &amp; test")));
        Assert.Equal("Synthetic & test", item.Title);
        Assert.Equal(Now.AddHours(-1), item.PublishedAt);
        Assert.Empty(Parse(Rss().Replace("https://example.com/article", "javascript:alert(1)")));
        Assert.Empty(Parse(Rss().Replace("https://example.com/article", "")));
        Assert.Empty(Parse(Rss().Replace("Thu, 17 Sep", "Tue, 01 Sep")));
        Assert.Null(Assert.Single(Parse(Rss().Replace("Thu, 17 Sep 2026 17:00:00 +0000", ""))).PublishedAt);
        const string atom = "<feed xmlns='http://www.w3.org/2005/Atom'><entry><title>Atom test</title><link href='/article'/><updated>2026-09-17T17:00:00Z</updated></entry><entry><title>Duplicate</title><link href='/article'/></entry></feed>";
        Assert.Equal("https://example.com/article", Assert.Single(Parse(atom)).Url);
        Assert.Throws<XmlException>(() => Parse("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><rss><channel>&x;</channel></rss>"));
        Assert.Throws<InvalidDataException>(() => Parse("<html/>"));
        Assert.Equal(300, Assert.Single(Parse(Rss(new string('X', 1000)))).Title.Length);
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = NewsTests.Now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Rss()) });
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t) { Calls++; return Respond(r, t); }
    }
    [Fact]
    public async Task PollingIsNonBlockingCancelsRemovedFeedsAndClearsFailures()
    {
        var clock = new Clock(); var handler = new Handler(); using var client = new HttpClient(handler); using var feed = new NewsFeed(client, clock);
        var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Respond = (_, _) => gate.Task;
        var options = new NewsOptions { Enabled = true, Channels = [Channel] };
        Assert.Equal("loading", feed.Read(options, default).Status); // returns before network completion
        feed.Read(options, default); Assert.Equal(1, handler.Calls);
        Assert.Equal("disabled", feed.Read(options with { Enabled = false }, default).Status);
        gate.SetResult(new(HttpStatusCode.OK) { Content = new StringContent(Rss()) });
        await Task.Yield();
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Rss()) });
        feed.Read(options, default); var live = feed.Read(options, default);
        Assert.Equal("connected", live.Status); Assert.Single(live.Items);
        feed.Read(options, default); Assert.Equal(2, handler.Calls);
        clock.Now = clock.Now.AddMinutes(16);
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        feed.Read(options, default); var failed = feed.Read(options, default);
        Assert.Equal("unavailable", failed.Status); Assert.Empty(failed.Items);
        clock.Now = clock.Now.AddMinutes(1); feed.Read(options, default); Assert.Equal(3, handler.Calls);
        clock.Now = clock.Now.AddMinutes(1); feed.Read(options, default); Assert.Equal(4, handler.Calls);
    }
    [Fact]
    public void PartialFailuresRotationAndExpiryRemainExplicit()
    {
        var clock = new Clock(); var handler = new Handler(); using var client = new HttpClient(handler); using var feed = new NewsFeed(client, clock);
        var options = new NewsOptions { Enabled = true, Channels = [Channel, new("Second synthetic", "https://example.org/rss")] };
        handler.Respond = (r, _) => Task.FromResult(new HttpResponseMessage(r.RequestUri!.Host == "example.org" ? HttpStatusCode.BadGateway : HttpStatusCode.OK) { Content = new StringContent(Rss()) });
        feed.Read(options, default); var partial = feed.Read(options, default);
        Assert.Equal("partial", partial.Status); Assert.Single(partial.Items); Assert.Equal("unavailable", partial.Channels[1].Status);
        clock.Now = clock.Now.AddHours(1);
        var expired = feed.Read(options, default); Assert.Empty(expired.Items);
        Assert.Equal("unavailable", expired.Status);
        var items = new[] { new NewsItem("A", "One", "https://example.com/1", null), new NewsItem("B", "Two", "https://example.com/2", null) };
        var rotation = new NewsSnapshot { Items = items, FetchedAt = Now };
        Assert.Equal("One", rotation.Select(Now.AddSeconds(19), 20)!.Title);
        Assert.Equal("Two", rotation.Select(Now.AddSeconds(20), 20)!.Title);
        Assert.Equal("One", rotation.Select(Now.AddSeconds(40), 20)!.Title);
    }
    [Fact]
    public async Task ChannelChangesDiscardOldResponsesAndInterleaveCurrentSources()
    {
        var clock = new Clock(); var handler = new Handler(); using var client = new HttpClient(handler); using var feed = new NewsFeed(client, clock);
        var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Respond = (_, _) => gate.Task;
        feed.Read(new() { Enabled = true, Channels = [Channel] }, default);
        var options = new NewsOptions { Enabled = true, Channels = [new("Second", "https://example.org/rss"), new("Third", "https://example.net/rss")] };
        handler.Respond = (r, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Rss().Replace("example.com", r.RequestUri!.Host)) });
        feed.Read(options, default);
        gate.SetResult(new(HttpStatusCode.OK) { Content = new StringContent(Rss("Obsolete result")) });
        await Task.Yield();
        var current = feed.Read(options, default);
        Assert.Equal(new[] { "Second", "Third" }, current.Items.Select(i => i.Source));
        Assert.DoesNotContain(current.Items, i => i.Title == "Obsolete result");
        Assert.Equal("Third", current.Select(clock.Now.AddSeconds(20), 20)!.Source);
    }
    [Fact]
    public void OversizedOrMalformedFeedNeverBreaksPolling()
    {
        var handler = new Handler(); using var client = new HttpClient(handler); using var feed = new NewsFeed(client, new Clock());
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 524289)) });
        var options = new NewsOptions { Enabled = true, Channels = [Channel] };
        feed.Read(options, default); Assert.Equal("unavailable", feed.Read(options, default).Status);
    }
}
