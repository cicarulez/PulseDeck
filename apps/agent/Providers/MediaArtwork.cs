using System.Security.Cryptography;
using SkiaSharp;

namespace PulseDeck.Agent.Providers;

public sealed record MediaArtwork(string Id, byte[] Png)
{
    public const int MaximumBytes = 4 * 1024 * 1024;

    public static MediaArtwork? Decode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length is 0 or > MaximumBytes) return null;
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0
            || (long)codec.Info.Width * codec.Info.Height > 4 * 1024 * 1024) return null;
        using var original = SKBitmap.Decode(codec);
        if (original is null) return null;
        float scale = Math.Min(1, 256f / Math.Max(original.Width, original.Height));
        using var small = original.Resize(new SKImageInfo(Math.Max(1, (int)(original.Width * scale)),
            Math.Max(1, (int)(original.Height * scale))), new SKSamplingOptions(SKFilterMode.Linear));
        if (small is null) return null;
        using var encoded = small.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded is null) return null;
        var png = encoded.ToArray();
        return new(Convert.ToHexString(SHA256.HashData(png)), png);
    }
}

// Single latest item, memory only. No artwork or media identifiers are saved to disk.
public sealed class MediaArtworkCache(TimeProvider? clock = null)
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private string? key;
    private DateTimeOffset nextRead;
    public MediaArtwork? Current { get; private set; }

    public void Clear() { key = null; Current = null; nextRead = default; }

    public async Task RefreshAsync(string identity, Func<CancellationToken, Task<byte[]?>> read, CancellationToken cancellationToken)
    {
        if (identity != key) { Clear(); key = identity; }
        if (time.GetUtcNow() < nextRead) return;
        // Clear first so a new track, missing thumbnail or failed refresh cannot show old art.
        Current = null;
        nextRead = time.GetUtcNow().AddSeconds(5);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            var bytes = await read(timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            Current = MediaArtwork.Decode(bytes);
            if (Current is not null) nextRead = time.GetUtcNow().AddSeconds(30);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { Current = null; } // Artwork is optional; metadata remains usable.
    }
}
