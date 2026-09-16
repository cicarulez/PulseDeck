using System.Runtime.InteropServices;
using PulseDeck.Core;
using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

public sealed record RenderedFrame(byte[] Png, byte[] Pixels);

public sealed class DeckRenderer : IDisposable
{
    private readonly SKTypeface typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
    private readonly SKTypeface bold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
    private SKBitmap? background;
    private string backgroundPath = "";
    private DateTime backgroundModified;

    public RenderedFrame Render(DeckState state, DeckConfig config)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(1920, 480, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(10, 16, 18));
        var modified = File.Exists(config.BackgroundPath) ? File.GetLastWriteTimeUtc(config.BackgroundPath) : DateTime.MinValue;
        if (config.BackgroundPath != backgroundPath || modified != backgroundModified)
        {
            background?.Dispose(); background = null;
            backgroundPath = config.BackgroundPath; backgroundModified = modified;
            if (modified != DateTime.MinValue)
            {
                try { background = SKBitmap.Decode(backgroundPath); } catch { /* Keep the readable base theme. */ }
            }
        }
        if (background is not null)
        {
            float scale = Math.Max(1920f / background.Width, 480f / background.Height);
            var width = background.Width * scale; var height = background.Height * scale;
            canvas.DrawBitmap(background, SKRect.Create((1920 - width) / 2, (480 - height) / 2, width, height));
            using var shade = new SKPaint { Color = new SKColor(5, 10, 13, 210) }; canvas.DrawRect(0, 0, 1920, 480, shade);
        }
        var accent = SKColor.Parse(config.AccentColor);
        var muted = new SKColor(144, 162, 161);
        using var line = new SKPaint { Color = new SKColor(48, 65, 64), StrokeWidth = 1 };
        using var accentPaint = new SKPaint { Color = accent, IsAntialias = true };
        using var grid = new SKPaint { Color = new SKColor(38, 60, 56, 35) };
        for (int x = 0; x < 1920; x += 80) canvas.DrawLine(x, 0, x, 480, grid);
        void Text(string value, float x, float y, float size, SKColor? color = null, bool heavy = false, float maxWidth = 0)
        {
            using var font = new SKFont(heavy ? bold : typeface, size);
            using var paint = new SKPaint { Color = color ?? SKColors.White, IsAntialias = true };
            var text = value;
            if (maxWidth > 0 && font.MeasureText(text) > maxWidth)
            {
                while (text.Length > 0 && font.MeasureText(text + "…") > maxWidth) text = text[..^1];
                text += "…";
            }
            canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
        }
        double? Value(string id) => state.Hardware.Metrics.FirstOrDefault(m => m.Id == id)?.Value;
        string Format(string id, string suffix = "", string number = "0") => Value(id) is { } v ? v.ToString(number) + suffix : "—";
        void Bar(string name, string id, float y)
        {
            Text(name, 32, y, 18, muted); Text(Format(id, "%"), 224, y, 18);
            using var track = new SKPaint { Color = new SKColor(38, 53, 53) };
            canvas.DrawRect(32, y + 12, 250, 6, track);
            if (Value(id) is { } v) canvas.DrawRect(32, y + 12, (float)Math.Clamp(v, 0, 100) * 2.5f, 6, accentPaint);
        }
        Text("PULSEDECK", 32, 42, 23, accent, true);
        Text("RECON / " + state.Profile.ToUpperInvariant(), 320, 42, 18, muted);
        Text(state.Timestamp.ToLocalTime().ToString("HH:mm:ss"), 1760, 42, 22);
        canvas.DrawLine(32, 66, 1888, 66, line);
        canvas.DrawLine(310, 90, 310, 446, line);
        canvas.DrawLine(1370, 90, 1370, 446, line);
        Bar("CPU", "cpu.load", 113); Bar("GPU", "gpu.load", 171); Bar("RAM", "ram.load", 229);
        canvas.DrawLine(32, 273, 282, 273, line);
        Text("VOICE / DISCORD", 32, 307, 16, accent, true);
        if (state.Discord.Status != "connected") Text("Backend offline", 32, 344, 19, muted);
        else if (state.Discord.Members.Count == 0) Text("Nessun partecipante", 32, 344, 18, muted);
        else
        {
            var members = state.Discord.Members.OrderByDescending(m => m.Id == config.TrackedMemberId).Take(3).ToArray();
            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];
                Text(member.Name, 32, 340 + i * 30, 18, maxWidth: 172);
                Text(member.Deaf ? "DEAF" : member.Mute ? "MUTE" : "ON", 216, 340 + i * 30, 15, member.Mute || member.Deaf ? new SKColor(255, 146, 131) : accent);
            }
            if (state.Discord.Members.Count > 3) Text($"+{state.Discord.Members.Count - 3} partecipanti", 32, 436, 15, muted);
        }

        Text(state.Profile == "gaming" ? "GAME TELEMETRY" : state.Profile == "music" ? "NOW PLAYING" : "SYSTEM OVERVIEW", 350, 119, 16, accent, true);
        if (state.Profile == "music")
        {
            Text(state.Media.Title, 350, 188, 42, heavy: true, maxWidth: 960);
            Text(state.Media.Artist, 350, 234, 26, muted, maxWidth: 960);
        }
        else
        {
            Text(state.Profile == "gaming" ? "FPS  —" : "READY FOR ACTION", 350, 192, 50, heavy: true);
            Text(state.Profile == "gaming" ? "Collegamento PresentMon da configurare" : "Telemetria locale / profili automatici", 350, 236, 22, muted);
        }
        Text("CPU TEMP", 350, 302, 16, muted); Text(Format("cpu.temperature", "°"), 350, 365, 48);
        Text("GPU TEMP", 590, 302, 16, muted); Text(Format("gpu.temperature", "°"), 590, 365, 48);
        Text("GPU POWER", 830, 302, 16, muted); Text(Format("gpu.power", " W"), 830, 365, 42);
        Text("VRAM", 1100, 302, 16, muted); Text(Format("gpu.memory", " MB"), 1100, 365, 34);
        Text("ACTIVE / " + (state.ForegroundApp.Length > 0 ? state.ForegroundApp : "Desktop"), 350, 432, 18, muted, maxWidth: 970);

        Text("MEDIA SESSION", 1410, 119, 16, accent, true);
        Text(state.Media.Status == "connected" ? state.Media.Title : "Nessuna riproduzione", 1410, 170, 26, heavy: true, maxWidth: 465);
        Text(state.Media.Artist, 1410, 204, 20, muted, maxWidth: 465);
        Text(state.Media.Playing ? "PLAYING" : "PAUSED / IDLE", 1410, 247, 15, state.Media.Playing ? accent : muted);
        canvas.DrawLine(1410, 279, 1875, 279, line);
        if (state.Media.DurationSeconds > 0)
            canvas.DrawRect(1410, 277, (float)Math.Clamp(state.Media.PositionSeconds / state.Media.DurationSeconds, 0, 1) * 465, 4, accentPaint);
        Text("RAM ALLOCATED", 1410, 335, 15, muted); Text(Format("ram.used", " GB", "0.0"), 1410, 379, 30);
        Text(state.Hardware.Status == "connected" ? "LIVE SENSOR DATA" : "SENSORS UNAVAILABLE", 1410, 432, 16, muted);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var pixels = new byte[1920 * 480 * 4]; Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return new(encoded.ToArray(), pixels);
    }
    public void Dispose() { background?.Dispose(); typeface.Dispose(); bold.Dispose(); }
}
