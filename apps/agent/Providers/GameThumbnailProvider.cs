using PulseDeck.Core;
using SkiaSharp;

namespace PulseDeck.Agent.Providers;

public sealed class GameThumbnailProvider(GameArtworkProvider artwork, SteamGridArtwork steamGrid, IHostApplicationLifetime lifetime)
{
    private readonly SemaphoreSlim downloads = new(2);
    private readonly object gate = new();
    private readonly Dictionary<string, (Task<byte[]?> Task, DateTimeOffset Expires)> cache = new();
    private int revision = -1;
    public Task<byte[]?> Read(string title)
    {
        lock (gate)
        {
            if (revision != steamGrid.Revision) { cache.Clear(); revision = steamGrid.Revision; }
            var key = SteamGridImages.Normalize(title);
            if (cache.TryGetValue(key, out var entry) && entry.Expires > DateTimeOffset.UtcNow) return entry.Task;
            if (cache.Count >= 128) cache.Clear();
            var task = Load(title);
            cache[key] = (task, DateTimeOffset.UtcNow.AddMinutes(5));
            return task;
        }
    }
    private async Task<byte[]?> Load(string title)
    {
        try
        {
            await downloads.WaitAsync(lifetime.ApplicationStopping);
            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
                budget.CancelAfter(TimeSpan.FromSeconds(15));
                var result = await artwork.Fetch(title, budget.Token);
                if (result.Path is null) return null;
                using var bitmap = SKBitmap.Decode(result.Path);
                if (bitmap is null) return null;
                using var surface = SKSurface.Create(new SKImageInfo(160, 90));
                surface.Canvas.Clear(SKColors.Black);
                var scale = Math.Max(160f / bitmap.Width, 90f / bitmap.Height);
                var width = bitmap.Width * scale; var height = bitmap.Height * scale;
                using var paint = new SKPaint { IsAntialias = true };
                surface.Canvas.DrawBitmap(bitmap, SKRect.Create((160 - width) / 2, (90 - height) / 2, width, height), paint);
                using var image = surface.Snapshot(); using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
                return encoded.ToArray();
            }
            finally { downloads.Release(); }
        }
        catch { return null; }
    }
}
