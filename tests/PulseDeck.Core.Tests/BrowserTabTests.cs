using System.Buffers.Binary;
using PulseDeck.Core;
using Xunit;

public sealed class BrowserTabTests
{
    [Theory]
    [InlineData("chrome", "Page title - Google Chrome", "Page title")]
    [InlineData("CHROME", "Page — Google Chrome", "Page")]
    [InlineData("chrome", "Page – Google Chrome", "Page")]
    [InlineData("chrome", "Google Chrome", null)]
    [InlineData("chrome", "  \r\n ", null)]
    [InlineData("chrome", "Google Chrome tips - Google Chrome", "Google Chrome tips")]
    [InlineData("chrome", "Page - Google Chrome - Google Chrome", "Page - Google Chrome")]
    [InlineData("WindowsTerminalHost", "WSL tmux - pulsedeck-codex", "WSL tmux - pulsedeck-codex")]
    [InlineData("WindowsTerminal", "PowerShell", "PowerShell")]
    [InlineData("bf6", "Loading...", null)]
    public void NativeTitleOnlyOverridesSupportedApps(string process, string raw, string? expected)
        => Assert.Equal(expected, ForegroundTitles.FromWindow(process, raw));

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void OnlyFreshMatchingTabCanProvideIconAndClearIsImmediate()
    {
        var clock = new Clock(); var store = new BrowserTabStore(clock);
        Assert.True(store.Update(new("First page", null)));
        Assert.NotNull(store.Read("First page"));
        Assert.Null(store.Read("Second page"));
        Assert.Null(store.Read(null));
        clock.Now = clock.Now.AddSeconds(6);
        Assert.Null(store.Read("First page"));
        store.Update(new("Second page", null));
        Assert.Null(store.Read("First page"));
        Assert.NotNull(store.Read("Second page"));
        store.Update(null);
        Assert.Null(store.Read("Second page"));
        store.Update(new("First page", null));
        clock.Now = clock.Now.AddSeconds(-1);
        Assert.Null(store.Read("First page"));
    }

    [Fact]
    public void InvalidMetadataCannotReplaceCurrentTab()
    {
        var store = new BrowserTabStore(); store.Update(new("Valid", null));
        foreach (var invalid in new BrowserTab[] {
            new(null!, null), new("\n\t", null), new(new string('a', 513), null),
            new("Bad", "https://example.test/icon.png"), new("Bad", new string('a', 50000)),
            new("Bad", Convert.ToBase64String(new byte[100])) })
            Assert.False(store.Update(invalid));
        Assert.NotNull(store.Read("Valid"));
    }

    [Theory]
    [InlineData(0, 32, false)]
    [InlineData(32, 0, false)]
    [InlineData(129, 32, false)]
    [InlineData(32, 129, false)]
    [InlineData(32, 32, true)]
    public void ImageDimensionsAreBoundedBeforeDecode(uint width, uint height, bool valid)
    {
        var png = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(8), 13);
        "IHDR"u8.CopyTo(png.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), width);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), height);
        // Header acceptance is separate from the provider's full PNG decode check.
        Assert.Equal(valid, new BrowserTabStore().Update(new("Page", Convert.ToBase64String(png))));
    }
}
