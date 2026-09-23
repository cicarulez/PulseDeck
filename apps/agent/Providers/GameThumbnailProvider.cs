using System.Security.Cryptography;
using PulseDeck.Agent.Configuration;
using PulseDeck.Core;
using SkiaSharp;

namespace PulseDeck.Agent.Providers;

public sealed class GameThumbnailProvider(GameArtworkProvider artwork, SteamGridArtwork steamGrid, ConfigStore config, IHostApplicationLifetime lifetime)
{
    private readonly SemaphoreSlim downloads = new(2);
    private readonly object gate = new();
    private readonly Dictionary<string, (Task<byte[]?> Task, DateTimeOffset Expires)> cache = new();
    private int revision = -1;
    public Task<byte[]?> Read(string title)
        => ReadPreview(title, "thumbnail");

    public Task<byte[]?> ReadPreview(string title, string kind)
    {
        lock (gate)
        {
            if (revision != steamGrid.Revision) { cache.Clear(); revision = steamGrid.Revision; }
            var key = kind + ":" + SteamGridImages.Normalize(title);
            if (cache.TryGetValue(key, out var entry) && entry.Expires > DateTimeOffset.UtcNow) return entry.Task;
            if (cache.Count >= 128) cache.Clear();
            var task = Load(title, kind);
            cache[key] = (task, DateTimeOffset.UtcNow.AddMinutes(5));
            return task;
        }
    }
    private async Task<byte[]?> Load(string title, string kind)
    {
        try
        {
            await downloads.WaitAsync(lifetime.ApplicationStopping);
            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
                budget.CancelAfter(TimeSpan.FromSeconds(15));
                var result = await artwork.Fetch(title, budget.Token);
                var path = kind == "hero" ? result.BackgroundPath
                    : kind == "thumbnail" ? result.BackgroundPath ?? result.Path : result.Path;
                if (path is null) return null;
                using var bitmap = SKBitmap.Decode(path);
                if (bitmap is null) return null;
                var preview = kind switch { "thumbnail" => (Width: 160, Height: 90), "cover" => (Width: 240, Height: 360), _ => (Width: 480, Height: 120) };
                using var surface = SKSurface.Create(new SKImageInfo(preview.Width, preview.Height));
                surface.Canvas.Clear(SKColors.Black);
                var scale = kind == "thumbnail"
                    ? Math.Max((float)preview.Width / bitmap.Width, (float)preview.Height / bitmap.Height)
                    : Math.Min((float)preview.Width / bitmap.Width, (float)preview.Height / bitmap.Height);
                var width = bitmap.Width * scale; var height = bitmap.Height * scale;
                using var paint = new SKPaint { IsAntialias = true };
                surface.Canvas.DrawBitmap(bitmap, SKRect.Create((preview.Width - width) / 2, (preview.Height - height) / 2, width, height), paint);
                using var image = surface.Snapshot(); using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
                return encoded.ToArray();
            }
            finally { downloads.Release(); }
        }
        catch { return null; }
    }

    public byte[]? ReadCustom(string path, string kind)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)
                || !new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(path).ToLowerInvariant())) return null;
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 32 * 1024 * 1024) return null;
            using var data = SKData.CreateCopy(File.ReadAllBytes(path));
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.FrameCount > 1 || codec.Info.Width < 1 || codec.Info.Height < 1
                || (long)codec.Info.Width * codec.Info.Height > 4 * 1024 * 1024) return null;
            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap is null) return null;
            var preview = kind == "cover" ? (Width: 240, Height: 360) : (Width: 480, Height: 120);
            using var surface = SKSurface.Create(new SKImageInfo(preview.Width, preview.Height));
            surface.Canvas.Clear(SKColors.Black);
            var scale = Math.Min((float)preview.Width / bitmap.Width, (float)preview.Height / bitmap.Height);
            using var paint = new SKPaint { IsAntialias = true };
            surface.Canvas.DrawBitmap(bitmap, SKRect.Create((preview.Width - bitmap.Width * scale) / 2,
                (preview.Height - bitmap.Height * scale) / 2, bitmap.Width * scale, bitmap.Height * scale), paint);
            using var image = surface.Snapshot(); using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
            return encoded.ToArray();
        }
        catch { return null; }
    }

    public async Task<string?> SaveCustom(IFormFile file, CancellationToken token)
    {
        if (file.Length is < 1 or > 32 * 1024 * 1024
            || !new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) return null;
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, token);
        using var data = SKData.CreateCopy(stream.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp)
            || codec.FrameCount > 1 || codec.Info.Width < 100 || codec.Info.Height < 100
            || (long)codec.Info.Width * codec.Info.Height > 4 * 1024 * 1024) return null;
        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null) return null;
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        if (encoded is null) return null;
        var bytes = encoded.ToArray();
        var directory = Path.Combine(config.DirectoryPath, "game-artwork");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "custom-" + Convert.ToHexString(SHA256.HashData(bytes)) + ".png");
        if (!File.Exists(path))
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllBytesAsync(temporary, bytes, token); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return path;
    }
}

public sealed record CustomGameImageRequest(string? Path, string? Kind);
