using System.Runtime.InteropServices;
using PulseDeck.Core;
using PulseDeck.Agent.Providers;
using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

public sealed record RenderedFrame(byte[] Png, byte[] Pixels);

public sealed class DeckRenderer : IDisposable
{
    private readonly SKTypeface typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
    private readonly SKTypeface bold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
    private readonly StaticBackground backgrounds = new();
    public string BackgroundStatus => backgrounds.Status;
    public int BackgroundFrameCount => backgrounds.Count;
    private SKBitmap? cover;
    private string? coverId;
    private SKBitmap? appIcon;
    private string? appIconId;
    private readonly SKShader baseGradient = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(1920, 480),
        [new SKColor(10, 16, 24), new SKColor(19, 35, 43), new SKColor(9, 15, 24)], [0, .55f, 1], SKShaderTileMode.Clamp);

    public RenderedFrame Render(DeckState state, DeckConfig config, MediaArtwork? artwork = null, ApplicationIcon? applicationIcon = null)
    {
        var nextAppIconId = state.Foreground.IconId == applicationIcon?.Id ? applicationIcon?.Id : null;
        if (appIconId != nextAppIconId)
        {
            appIcon?.Dispose(); appIcon = null; appIconId = nextAppIconId;
            if (nextAppIconId is not null)
                try { appIcon = SKBitmap.Decode(applicationIcon!.Png); } catch { }
        }
        var nextCoverId = state.Media.Status == "connected" && state.Media.ArtworkId == artwork?.Id ? artwork?.Id : null;
        if (coverId != nextCoverId)
        {
            cover?.Dispose(); cover = null; coverId = nextCoverId;
            if (nextCoverId is not null)
                try { cover = SKBitmap.Decode(artwork!.Png); } catch { /* Missing cover is explicit. */ }
        }
        using var bitmap = new SKBitmap(new SKImageInfo(1920, 480, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(10, 16, 18));
        using (var basePaint = new SKPaint { Shader = baseGradient }) canvas.DrawRect(0, 0, 1920, 480, basePaint);
        var background = backgrounds.Get(config.BackgroundPath);
        if (background is not null)
        {
            float scale = Math.Max(1920f / background.Width, 480f / background.Height);
            var width = background.Width * scale; var height = background.Height * scale;
            canvas.DrawBitmap(background, SKRect.Create((1920 - width) / 2, (480 - height) / 2, width, height));
            using var shade = new SKPaint { Color = new SKColor(5, 10, 13, 130) }; canvas.DrawRect(0, 0, 1920, 480, shade);
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
        var widgets = WidgetCatalog.Expand(config.Widgets).ToDictionary(w => w.Slot, w => WidgetCatalog.Resolve(w, state.Hardware));
        void ValueText(WidgetReading widget, float x, float y, float size, float maxWidth, float minimumSize = 18)
        {
            var text = widget.DisplayValue + (widget.Value.HasValue && widget.DisplayUnit.Length > 0 ? " " + widget.DisplayUnit : "");
            using var font = new SKFont(typeface, size);
            if (font.MeasureText(text) > maxWidth) size = Math.Max(minimumSize, size * maxWidth / font.MeasureText(text));
            Text(text, x, y, size, maxWidth: maxWidth);
        }
        void Bar(string slot, float y)
        {
            var widget = widgets[slot];
            if (widget.Hidden) return;
            Text(widget.Label, 32, y, 16, muted, maxWidth: 126);
            ValueText(widget, 168, y, 17, 114, 12);
            using var track = new SKPaint { Color = new SKColor(38, 53, 53) };
            canvas.DrawRect(32, y + 12, 250, 6, track);
            if (widget.Fraction is { } fraction) canvas.DrawRect(32, y + 12, (float)fraction * 250, 6, accentPaint);
        }
        void Cover(float x, float y, float size)
        {
            var bounds = SKRect.Create(x, y, size, size);
            canvas.Save();
            using var rounded = new SKRoundRect(bounds, 8);
            canvas.ClipRoundRect(rounded, SKClipOperation.Intersect, true);
            using var placeholder = new SKPaint { Color = new SKColor(29, 43, 43) };
            canvas.DrawRect(bounds, placeholder);
            if (cover is not null)
            {
                var scale = Math.Min(size / cover.Width, size / cover.Height);
                canvas.DrawBitmap(cover, SKRect.Create(x + (size - cover.Width * scale) / 2,
                    y + (size - cover.Height * scale) / 2, cover.Width * scale, cover.Height * scale));
            }
            else
            {
                Text("COPERTINA", x + 10, y + size / 2 - 4, 12, muted, maxWidth: size - 20);
                Text("non disponibile", x + 10, y + size / 2 + 16, 12, muted, maxWidth: size - 20);
            }
            canvas.Restore();
        }
        void Media(float x, float y, float width)
        {
            const float size = 132;
            var available = state.Media.Status == "connected";
            Text(state.Media.App.Contains("Spotify", StringComparison.OrdinalIgnoreCase) ? "SPOTIFY" : "MEDIA SESSION", x, y, 15, accent, true);
            Cover(x, y + 16, size);
            var textX = x + size + 18;
            var textWidth = width - size - 18;
            Text(available && state.Media.Title.Length > 0 ? state.Media.Title : "Nessuna riproduzione", textX, y + 47, 24, heavy: true, maxWidth: textWidth);
            Text(available ? state.Media.Artist : "", textX, y + 80, 18, muted, maxWidth: textWidth);
            Text(available ? state.Media.Playing ? "IN RIPRODUZIONE" : "IN PAUSA" : "INATTIVO", textX, y + 115, 13, state.Media.Playing ? accent : muted, maxWidth: textWidth);
            string Time(double seconds) => TimeSpan.FromSeconds(Math.Clamp(double.IsFinite(seconds) ? seconds : 0, 0, 359999)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
            Text(available && state.Media.DurationSeconds > 0 ? $"{Time(state.Media.PositionSeconds)} / {Time(state.Media.DurationSeconds)}" : "— / —", textX, y + 144, 14, muted);
            canvas.DrawLine(x, y + 166, x + width, y + 166, line);
            if (available && state.Media.DurationSeconds > 0)
                canvas.DrawRect(x, y + 164, (float)Math.Clamp(state.Media.PositionSeconds / state.Media.DurationSeconds, 0, 1) * width, 4, accentPaint);
        }
        void Compact()
        {
            const float startX = 32, startY = 86, cellWidth = 314, cellHeight = 80, gap = 12;
            using var card = new SKPaint { Color = new SKColor(12, 24, 28, 185), IsAntialias = true };
            using var track = new SKPaint { Color = new SKColor(43, 58, 57) };
            canvas.DrawRoundRect(SKRect.Create(1352, 86, 548, 356), 8, 8, card);
            for (int i = 0; i < WidgetCatalog.Slots.Count; i++)
            {
                var widget = widgets[WidgetCatalog.Slots[i].Id];
                if (widget.Hidden) continue;
                float x = startX + i % 4 * (cellWidth + gap), y = startY + i / 4 * (cellHeight + gap);
                canvas.DrawRoundRect(SKRect.Create(x, y, cellWidth, cellHeight), 8, 8, card);
                var style = config.Widgets.FirstOrDefault(w => w.Slot == widget.Slot)?.Style ?? "auto";
                if (style == "ring")
                {
                    var bounds = SKRect.Create(x + 16, y + 14, 52, 52);
                    using var ring = new SKPaint { Color = track.Color, Style = SKPaintStyle.Stroke, StrokeWidth = 6, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                    canvas.DrawOval(bounds, ring);
                    if (widget.Fraction is { } fraction)
                    {
                        ring.Color = accent;
                        using var arc = new SKPath(); arc.AddArc(bounds, -90, (float)fraction * 359.99f); canvas.DrawPath(arc, ring);
                    }
                    else Text("—", x + 31, y + 47, 18, muted);
                    Text(widget.Label, x + 84, y + 23, 13, muted, maxWidth: cellWidth - 100);
                    if (widget.Capacity is { } capacity && widget.Value is { } used)
                    {
                        Text($"{widget.DisplayValue} / {capacity:0.#} {widget.DisplayUnit}", x + 84, y + 47, 22, maxWidth: cellWidth - 100);
                        Text($"{Math.Max(0, capacity - used):0.#} {widget.DisplayUnit} liberi", x + 84, y + 67, 13, muted, maxWidth: cellWidth - 100);
                    }
                    else ValueText(widget, x + 84, y + 58, 29, cellWidth - 100);
                }
                else
                {
                    Text(widget.Label, x + 16, y + 24, 14, muted, maxWidth: cellWidth - 32);
                    ValueText(widget, x + 16, y + 60, 30, cellWidth - 32, 18);
                }
                if (style == "bar" || style == "auto" && WidgetCatalog.Slots[i].IsBar)
                {
                    canvas.DrawRect(x + 16, y + 70, cellWidth - 32, 3, track);
                    if (widget.Fraction is { } fraction)
                        canvas.DrawRect(x + 16, y + 70, (float)fraction * (cellWidth - 32), 3, accentPaint);
                }
            }
            canvas.DrawLine(1340, 86, 1340, 442, line);
            Media(1364, 108, 524);
            canvas.DrawLine(1364, 296, 1888, 296, line);
            Text("DISCORD", 1364, 322, 15, accent, true);
            if (state.Discord.Status != "connected") Text("Discord non collegato", 1364, 356, 18, muted);
            else if (state.Discord.Members.Count == 0) Text("Nessun partecipante", 1364, 356, 18, muted);
            else
            {
                var members = state.Discord.Members.OrderByDescending(m => m.Id == config.TrackedMemberId).Take(3).ToArray();
                for (int i = 0; i < members.Length; i++)
                {
                    Text(members[i].Name, 1364, 351 + i * 29, 18, maxWidth: 408);
                    Text(members[i].Deaf ? "DEAF" : members[i].Mute ? "MUTE" : "ON", 1818, 351 + i * 29, 14,
                        members[i].Deaf || members[i].Mute ? new SKColor(255, 146, 131) : accent);
                }
                if (state.Discord.Members.Count > 3) Text($"+{state.Discord.Members.Count - 3} partecipanti", 1364, 436, 13, muted);
            }
            Text("ACTIVE / " + (state.ForegroundApp.Length > 0 ? state.ForegroundApp : "Desktop"), 32, 469, 13, muted, maxWidth: 850);
            Text(state.Hardware.Status == "connected" ? "SENSORI LIVE" : "SENSORI NON DISPONIBILI", 920, 469, 13, muted, maxWidth: 400);
            if (state.Profile == "gaming") Text("FPS non disponibili", 1364, 469, 13, muted);
        }
        Text("PULSEDECK", 32, 42, 23, accent, true);
        Text("RECON / " + state.Profile.ToUpperInvariant(), 320, 42, 18, muted);
        if (appIcon is not null) canvas.DrawBitmap(appIcon, SKRect.Create(642, 14, 40, 40));
        else Text("APP", 643, 41, 13, muted);
        Text(state.Foreground.IsGame ? "GIOCO IN PRIMO PIANO" : "APP IN PRIMO PIANO", 698, 25, 11, state.Foreground.IsGame ? accent : muted, true);
        Text(state.Foreground.DisplayName, 698, 49, 20, maxWidth: 450);
        Text(state.Timestamp.ToLocalTime().ToString("HH:mm:ss"), 1760, 42, 22);
        if (BackgroundStatus == "unavailable") Text("Sfondo non disponibile", 1320, 42, 15, muted, maxWidth: 390);
        canvas.DrawLine(32, 66, 1888, 66, line);
        if (config.Layout == "compact") Compact();
        else
        {
        canvas.DrawLine(310, 90, 310, 446, line);
        canvas.DrawLine(1370, 90, 1370, 446, line);
        Bar("bar1", 113); Bar("bar2", 171); Bar("bar3", 229);
        canvas.DrawLine(32, 273, 282, 273, line);
        Text("VOICE / DISCORD", 32, 307, 16, accent, true);
        if (state.Discord.Status != "connected") Text("Discord non collegato", 32, 344, 19, muted);
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
        for (int i = 0; i < 4; i++)
        {
            var widget = widgets[$"value{i + 1}"];
            if (widget.Hidden) continue;
            var x = 350 + i * 250;
            Text(widget.Label, x, 302, 16, muted, maxWidth: 225);
            ValueText(widget, x, 365, 42, 225);
        }
        Text("ACTIVE / " + (state.ForegroundApp.Length > 0 ? state.ForegroundApp : "Desktop"), 350, 432, 18, muted, maxWidth: 970);

        Media(1410, 113, 465);
        var side = widgets["side"];
        if (!side.Hidden)
        {
            Text(side.Label, 1410, 335, 15, muted, maxWidth: 465);
            ValueText(side, 1410, 379, 30, 465);
        }
        Text(state.Hardware.Status == "connected" ? "LIVE SENSOR DATA" : "SENSORS UNAVAILABLE", 1410, 432, 16, muted);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var pixels = new byte[1920 * 480 * 4]; Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return new(encoded.ToArray(), pixels);
    }
    public void Dispose() { appIcon?.Dispose(); cover?.Dispose(); backgrounds.Dispose(); baseGradient.Dispose(); typeface.Dispose(); bold.Dispose(); }
}
