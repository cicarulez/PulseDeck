using System.Buffers.Binary;

namespace PulseDeck.Core;

public sealed record BrowserTab(string Title, string? FaviconPng);
public sealed record BrowserTabUpdate(BrowserTab? Tab);

/// <summary>Only the extension's focused tab, in memory. The native window title
/// must match before using its icon; expired metadata never pins a stale tab.</summary>
public sealed class BrowserTabStore(TimeProvider? clock = null)
{
    public const int MaximumIconBytes = 32768;
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly object gate = new();
    private BrowserTab? current;
    private DateTimeOffset received;

    public bool Update(BrowserTab? tab)
    {
        if (tab is not null)
        {
            if (string.IsNullOrWhiteSpace(tab.Title) || tab.Title.Length > 512) return false;
            tab = tab with { Title = ForegroundTitles.Clean(tab.Title) };
            if (tab.Title.Length == 0) return false;
            if (tab.FaviconPng is not null)
            {
                if (tab.FaviconPng.Length > 4 * ((MaximumIconBytes + 2) / 3)) return false;
                byte[] bytes;
                try { bytes = Convert.FromBase64String(tab.FaviconPng); }
                catch (FormatException) { return false; }
                // Bound decoded dimensions before any image decoder sees the bytes.
                if (bytes.Length is < 33 or > MaximumIconBytes
                    || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                    || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)) != 13
                    || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
                    || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16)) is < 1 or > 128
                    || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20)) is < 1 or > 128) return false;
            }
        }
        lock (gate) { current = tab; received = time.GetUtcNow(); }
        return true;
    }

    public BrowserTab? Read(string? nativeTabTitle)
    {
        lock (gate)
        {
            var age = time.GetUtcNow() - received;
            return current is not null && age >= TimeSpan.Zero && age < TimeSpan.FromSeconds(6)
                && current.Title.Equals(nativeTabTitle, StringComparison.Ordinal) ? current : null;
        }
    }
}
