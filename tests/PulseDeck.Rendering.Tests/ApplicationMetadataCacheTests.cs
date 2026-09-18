using PulseDeck.Agent.Providers;
using Xunit;

public sealed class ApplicationMetadataCacheTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static async Task<ApplicationMetadata?> UntilResult(ApplicationMetadataCache cache, string id)
    {
        for (var i = 0; i < 200; i++)
        {
            if (cache.Read(id) is { } value) return value;
            await Task.Delay(5);
        }
        return null;
    }
    [Fact]
    public async Task SlowLookupDoesNotBlockOrQueueLookupsForOtherApps()
    {
        var entered = new TaskCompletionSource();
        var answer = new TaskCompletionSource<ApplicationMetadata?>();
        var calls = 0;
        var cache = new ApplicationMetadataCache(async (id, token) =>
        {
            Interlocked.Increment(ref calls); entered.TrySetResult();
            return id == "first" ? await answer.Task : new("Second", null);
        });
        Assert.Null(cache.Read("first"));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        for (var i = 0; i < 100; i++) Assert.Null(cache.Read("second"));
        Assert.Equal(1, calls);
        answer.SetResult(new("First", new("first-icon", [1])));
        var first = await UntilResult(cache, "first"); Assert.Equal("First", first?.Name);
        Assert.Null(cache.Read("second"));
        Assert.Equal("Second", (await UntilResult(cache, "second"))?.Name);
        Assert.Equal("first-icon", cache.Read("first")?.Icon?.Id);
    }
    [Fact]
    public async Task MissingLogoPreservesNameAndRetriesAfterShortExpiry()
    {
        var clock = new Clock(); var calls = 0;
        var cache = new ApplicationMetadataCache((id, token) => Task.FromResult<ApplicationMetadata?>(
            Interlocked.Increment(ref calls) == 1 ? new("WhatsApp", null) : new("WhatsApp", new("logo", [1]))), clock);
        Assert.Equal("WhatsApp", (await UntilResult(cache, "app"))?.Name);
        for (var i = 0; i < 100; i++) Assert.Null(cache.Read("app")?.Icon);
        Assert.Equal(1, calls);
        clock.Now = clock.Now.AddSeconds(10);
        cache.Read("app");
        for (var i = 0; i < 200 && cache.Read("app")?.Icon is null; i++) await Task.Delay(5);
        Assert.Equal("logo", cache.Read("app")?.Icon?.Id);
        Assert.Equal(2, calls);
    }
}
