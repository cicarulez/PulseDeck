using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

// Decode once on path/modification changes. Strictly bounded memory, no frame queue.
public sealed class BackgroundFrames(TimeProvider? clock = null) : IDisposable
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly List<SKBitmap> frames = [];
    private readonly List<long> ends = [];
    private string path = "";
    private DateTime modified;
    private long started;
    private bool animate;
    public string Status { get; private set; } = "none";
    public int Count => frames.Count;

    public SKBitmap? Get(string nextPath, bool animation)
    {
        DateTime nextModified;
        try { nextModified = File.Exists(nextPath) ? File.GetLastWriteTimeUtc(nextPath) : DateTime.MinValue; }
        catch { nextModified = DateTime.MinValue; }
        if (nextPath != path || nextModified != modified || animation != animate)
        {
            Dispose(); path = nextPath; modified = nextModified; animate = animation;
            Status = path.Length == 0 ? "none" : "unavailable";
            if (nextModified != DateTime.MinValue) Load();
            started = time.GetTimestamp();
        }
        if (frames.Count == 0) return null;
        if (frames.Count == 1) return frames[0];
        var position = Math.Max(0, (long)time.GetElapsedTime(started).TotalMilliseconds) % ends[^1];
        var index = ends.FindIndex(end => position < end);
        return frames[index < 0 ? 0 : index];
    }

    private void Load()
    {
        try
        {
            if (new FileInfo(path).Length > 32 * 1024 * 1024) return;
            using var data = SKData.Create(path);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0) return;
            var pixels = (long)codec.Info.Width * codec.Info.Height;
            if (pixels > 4 * 1024 * 1024) return;
            int count = animate ? Math.Max(1, codec.FrameCount) : 1;
            bool limited = count > 120 || pixels * 4 * count > 64 * 1024 * 1024;
            if (limited) count = 1;
            var frameInfo = codec.FrameInfo;
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            long end = 0;
            for (int i = 0; i < count; i++)
            {
                var frame = new SKBitmap(info);
                // PriorFrame=-1 asks Skia to reconstruct required/disposed GIF frames itself.
                var result = codec.GetPixels(info, frame.GetPixels(), new SKCodecOptions(i));
                if (result != SKCodecResult.Success) { frame.Dispose(); throw new InvalidDataException("Background frame decoding failed."); }
                frames.Add(frame);
                end += frameInfo.Length > i ? Math.Max(20, frameInfo[i].Duration) : 1000;
                ends.Add(end);
            }
            Status = limited ? "static-limit" : count > 1 ? "animated" : "static";
        }
        catch { Dispose(); Status = "unavailable"; }
    }

    public void Dispose() { foreach (var frame in frames) frame.Dispose(); frames.Clear(); ends.Clear(); }
}
