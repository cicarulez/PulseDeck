using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

// Only the first frame is decoded, including files saved by the retired GIF trial.
public sealed class StaticBackground : IDisposable
{
    private SKBitmap? bitmap;
    private string path = "";
    private DateTime modified;
    public string Status { get; private set; } = "none";
    public int Count => bitmap is null ? 0 : 1;

    public SKBitmap? Get(string nextPath)
    {
        DateTime nextModified;
        try { nextModified = File.Exists(nextPath) ? File.GetLastWriteTimeUtc(nextPath) : DateTime.MinValue; }
        catch { nextModified = DateTime.MinValue; }
        if (nextPath == path && nextModified == modified) return bitmap;
        Dispose(); path = nextPath; modified = nextModified;
        Status = path.Length == 0 ? "none" : "unavailable";
        if (nextModified == DateTime.MinValue) return null;
        try
        {
            if (new FileInfo(path).Length > 32 * 1024 * 1024) return null;
            using var data = SKData.Create(path);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0
                || (long)codec.Info.Width * codec.Info.Height > 4 * 1024 * 1024) return null;
            bitmap = SKBitmap.Decode(codec);
            if (bitmap is not null) Status = "static";
        }
        catch { Dispose(); }
        return bitmap;
    }

    public void Dispose() { bitmap?.Dispose(); bitmap = null; }
}
