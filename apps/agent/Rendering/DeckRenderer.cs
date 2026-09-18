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
    private readonly WidgetTrends trends = new();
    private readonly StaticBackground backgrounds = new();
    private readonly StaticBackground gameArtwork = new();
    public string BackgroundStatus => backgrounds.Status;
    public int BackgroundFrameCount => backgrounds.Count;
    private SKBitmap? cover;
    private string? coverId;
    private SKBitmap? appIcon;
    private string? appIconId;
    private SKBitmap? gameIcon;
    private string? gameIconId;
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
        var nextGameIconId = state.Profile == "gaming" ? state.Game?.IconId : null;
        if (gameIconId != nextGameIconId)
        {
            gameIcon?.Dispose(); gameIcon = null; gameIconId = nextGameIconId;
        }
        if (gameIcon is null && nextGameIconId is not null && nextGameIconId == applicationIcon?.Id)
            try { gameIcon = SKBitmap.Decode(applicationIcon.Png); } catch { }
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
        var background = backgrounds.Get(GameTheme.BackgroundFor(state, config));
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
        void Text(string value, float x, float y, float size, SKColor? color = null, bool heavy = false, float maxWidth = 0, float minimumSize = 0)
        {
            using var font = new SKFont(heavy ? bold : typeface, size);
            using var paint = new SKPaint { Color = color ?? SKColors.White, IsAntialias = true };
            var text = value;
            if (maxWidth > 0 && minimumSize > 0 && font.MeasureText(text) > maxWidth)
                font.Size = Math.Max(minimumSize, size * maxWidth / font.MeasureText(text));
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
            if (widget.Upload is { } upload)
            {
                Text($"↓ {widget.DisplayValue} {widget.DisplayUnit}  ↑ {upload.DisplayValue} {upload.DisplayUnit}", x, y, size, maxWidth: maxWidth, minimumSize: minimumSize);
                return;
            }
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
            Text(available && state.Media.DurationSeconds > 0 ? $"{Time(state.Media.PositionSeconds)} / {Time(state.Media.DurationSeconds)}" : "— / —", textX, y + 144, 20, maxWidth: textWidth);
            if (config.SpotifyLyrics && state.Profile == "music" && SpotifyLyrics.IsSpotify(state.Media))
                Text(state.Lyrics.Status switch { "loading" => "Ricerca testo…", "instrumental" => "Brano strumentale", "not-found" => "Testo non trovato", _ => "Testo non disponibile" }, x, y + 185, 12, muted, maxWidth: width);
            canvas.DrawLine(x, y + 166, x + width, y + 166, line);
            if (available && state.Media.DurationSeconds > 0)
                canvas.DrawRect(x, y + 164, (float)Math.Clamp(state.Media.PositionSeconds / state.Media.DurationSeconds, 0, 1) * width, 4, accentPaint);
        }
        var widgetTrends = WidgetCatalog.Expand(config.Widgets).ToDictionary(w => w.Slot,
            w => trends.Read(w, widgets[w.Slot], state.Timestamp));
        void WidgetCard(int i, float x, float y, float cellWidth)
        {
            const float cellHeight = 80;
            var compact = cellWidth < 200;
            using var card = new SKPaint { Color = new SKColor(12, 24, 28, 200), IsAntialias = true };
            using var track = new SKPaint { Color = new SKColor(43, 58, 57) };
            var widget = widgets[WidgetCatalog.Slots[i].Id];
            if (widget.Hidden) return;
            canvas.DrawRoundRect(SKRect.Create(x, y, cellWidth, cellHeight), 8, 8, card);
            var trend = widgetTrends[widget.Slot];
            if (trend != 0)
                Text(trend > 0 ? "↑" : "↓", x + cellWidth - 31, y + 25, 24,
                    trend > 0 ? new SKColor(255, 120, 105) : new SKColor(112, 224, 144), true);
            var style = config.Widgets.FirstOrDefault(w => w.Slot == widget.Slot)?.Style ?? "auto";
            if (widget.Upload is { } upload)
            {
                Text(widget.Label, x + 16, y + 19, 13, muted, maxWidth: cellWidth - 32);
                Text($"↓ {widget.DisplayValue} {widget.DisplayUnit}", x + 16, y + 44, 23, maxWidth: cellWidth - 32, minimumSize: compact ? 18 : 0);
                Text($"↑ {upload.DisplayValue} {upload.DisplayUnit}", x + 16, y + 69, 23, maxWidth: cellWidth - 32, minimumSize: compact ? 18 : 0);
                return;
            }
            if (style == "ring")
            {
                var bounds = compact ? SKRect.Create(x + 12, y + 23, 36, 36) : SKRect.Create(x + 16, y + 14, 52, 52);
                var textX = x + (compact ? 60 : 84);
                var textWidth = cellWidth - (compact ? 70 : 100);
                using var ring = new SKPaint { Color = track.Color, Style = SKPaintStyle.Stroke, StrokeWidth = 6, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                canvas.DrawOval(bounds, ring);
                if (widget.Fraction is { } fraction)
                {
                    ring.Color = accent;
                    using var arc = new SKPath(); arc.AddArc(bounds, -90, (float)fraction * 359.99f); canvas.DrawPath(arc, ring);
                }
                else Text("—", x + (compact ? 20 : 31), y + 47, 18, muted);
                Text(widget.Label, textX, y + 20, 13, muted, maxWidth: textWidth);
                if (compact && widget.Unit == "%")
                    ValueText(widget, textX, y + 58, 26, textWidth);
                else if (widget.Unit == "%" && widget.Capacity is { } ramTotal && widget.Used is { } ramUsed)
                {
                    ValueText(widget, textX, y + 47, compact ? 24 : 26, textWidth);
                    Text($"{ramUsed:0.#} / {ramTotal:0.#} GiB", compact ? x + 12 : textX, y + (compact ? 72 : 68), compact ? 14 : 17, muted, maxWidth: compact ? cellWidth - 24 : textWidth);
                }
                else if (widget.Capacity is { } capacity && widget.Value is not null)
                {
                    Text($"{widget.DisplayValue} / {capacity:0.#} {widget.DisplayUnit}", textX, y + 58, compact ? 22 : 26, maxWidth: textWidth, minimumSize: compact ? 16 : 22);
                }
                else ValueText(widget, textX, y + 58, compact ? 26 : 29, textWidth);
            }
            else
            {
                Text(widget.Label, x + 16, y + 24, 14, muted, maxWidth: cellWidth - (trend == 0 ? 32 : 65));
                ValueText(widget, x + 16, y + 60, 30, cellWidth - 32, 18);
            }
            if (style == "bar" || style == "auto" && WidgetCatalog.Slots[i].IsBar)
            {
                canvas.DrawRect(x + 16, y + 70, cellWidth - 32, 3, track);
                if (widget.Fraction is { } fraction)
                    canvas.DrawRect(x + 16, y + 70, (float)fraction * (cellWidth - 32), 3, accentPaint);
            }
        }
        void Discord(float x, float y, float width)
        {
            Text("DISCORD", x, y, 15, accent, true);
            if (state.Discord.Status != "connected") Text("Discord non collegato", x, y + 34, 18, muted, maxWidth: width);
            else if (state.Discord.Members.Count == 0) Text("Nessun partecipante", x, y + 34, 18, muted, maxWidth: width);
            else
            {
                var members = state.Discord.Members.OrderByDescending(m => m.Id == config.TrackedMemberId).Take(3).ToArray();
                for (int i = 0; i < members.Length; i++)
                {
                    Text(members[i].Name, x, y + 29 + i * 29, 18, maxWidth: width - 85);
                    Text(members[i].Deaf ? "DEAF" : members[i].Mute ? "MUTE" : "ON", x + width - 70, y + 29 + i * 29, 14,
                        members[i].Deaf || members[i].Mute ? new SKColor(255, 146, 131) : accent);
                }
                if (state.Discord.Members.Count > 3) Text($"+{state.Discord.Members.Count - 3} partecipanti", x, y + 114, 13, muted);
            }
        }
        void GamingDiscord()
        {
            Text("DISCORD", 48, 113, 18, accent, true);
            var online = state.Discord.Status == "connected";
            if (!online) { Text("Discord non collegato", 48, 166, 21, muted, maxWidth: 300); return; }
            var members = state.Discord.Members.OrderByDescending(m => state.Discord.SpeakingStatus == "connected" && m.Speaking == true)
                .ThenByDescending(m => m.Id == config.TrackedMemberId).ThenBy(m => m.Name).ToArray();
            Text($"{members.Length} partecipanti", 48, 139, 16, muted);
            const int capacity = 8;
            var pages = Math.Max(1, (members.Length + capacity - 1) / capacity);
            var page = (int)(state.Timestamp.ToUnixTimeSeconds() / 8 % pages);
            var visible = members.Skip(page * capacity).Take(capacity).ToArray();
            if (members.Length == 0) Text("Nessun partecipante", 48, 183, 20, muted, maxWidth: 300);
            for (var i = 0; i < visible.Length; i++)
            {
                var member = visible[i]; var y = 171 + i * 30;
                var speaking = state.Discord.SpeakingStatus == "connected" && member.Speaking == true && !member.Mute && !member.Deaf;
                if (speaking)
                {
                    using var highlight = new SKPaint { Color = accent.WithAlpha(40), IsAntialias = true };
                    canvas.DrawRoundRect(SKRect.Create(42, y - 22, 312, 28), 5, 5, highlight);
                    canvas.DrawCircle(51, y - 7, 4, accentPaint);
                }
                Text(member.Name, 62, y, 20, speaking ? accent : SKColors.White, speaking, maxWidth: 205);
                Text(member.Deaf ? "DEAF" : member.Mute ? "MUTO" : speaking ? "VOCE" : "", 277, y, 13, speaking ? accent : muted, maxWidth: 70);
            }
            Text(state.Discord.SpeakingStatus == "connected" ? "Attività vocale collegata"
                : state.Discord.SpeakingStatus == "connecting" ? "Collegamento voce…" : "Attività vocale non disponibile", 48, 422, 14, muted, maxWidth: 300);
            if (pages > 1) Text($"{page + 1}/{pages}", 296, 139, 14, muted, maxWidth: 50);
        }
        void GamePanel(float x, float width)
        {
            var game = state.Game;
            var explicitTheme = GameTheme.ManualBackgroundFor(state, config);
            var picture = gameArtwork.Get(explicitTheme.Length > 0 ? explicitTheme : state.GameArtwork.Path ?? "");
            var bounds = SKRect.Create(x + 12, 98, width - 24, 173);
            canvas.Save(); canvas.ClipRect(bounds);
            if (picture is not null)
            {
                var scale = Math.Min(bounds.Width / picture.Width, bounds.Height / picture.Height);
                canvas.DrawBitmap(picture, SKRect.Create(bounds.MidX - picture.Width * scale / 2,
                    bounds.MidY - picture.Height * scale / 2, picture.Width * scale, picture.Height * scale));
            }
            else
            {
                if (gameIcon is not null) canvas.DrawBitmap(gameIcon, SKRect.Create(bounds.MidX - 40, 112, 80, 80));
                Text(state.GameArtwork.Status == "loading" ? "Recupero copertina…" : "Copertina non disponibile", x + 24, 248, 18, muted, maxWidth: width - 48);
            }
            canvas.Restore();
            Text(game?.DisplayName ?? "Nessun gioco in primo piano", x + 14, 306, 27, heavy: true, maxWidth: width - 28, minimumSize: 20);
            var duration = state.GameSession is { } session ? TimeSpan.FromSeconds(session.ElapsedSeconds) : (TimeSpan?)null;
            var time = duration is { } elapsed ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}" : "—";
            Text("SESSIONE", x + 14, 337, 14, muted);
            Text(time, x + 14, 374, 30, heavy: true);
            var fps = state.Fps;
            Text("FPS APP", x + width / 2, 337, 14, muted);
            Text(fps.Status == "connected" && fps.FramesPerSecond is { } value ? value.ToString("0") : "—", x + width / 2, 374, 30, heavy: true);
            Text(fps.Status == "connected" && fps.FrameTimeMs is { } ms ? $"{ms:0.0} ms · PresentMon"
                : fps.Status == "waiting" ? "FPS: in attesa del gioco" : "FPS non disponibili", x + 14, 412, 17, muted, maxWidth: width - 28);
        }
        void WeatherPanel()
        {
            var weather = state.Weather;
            Text("METEO", 48, 113, 15, accent, true);
            Text(config.WeatherLocation?.Name ?? "Scegli una località", 48, 147, 23, heavy: true, maxWidth: 300);
            if (weather.Status != "connected" || weather.LocationName != config.WeatherLocation?.Name)
            {
                Text("—", 48, 235, 64, muted);
                Text(config.WeatherLocation is null ? "Configura il meteo dal pannello" : weather.Status == "loading" ? "Aggiornamento in corso…" : "Meteo non disponibile",
                    48, 278, 17, muted, maxWidth: 300);
                Text("Nessuna lettura disponibile", 48, 309, 14, muted, maxWidth: 300);
            }
            else
            {
                string Number(double? value, string suffix) => value is { } v && double.IsFinite(v) ? v.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("it-IT")) + suffix : "—";
                WeatherGlyph.Draw(canvas, 46, 170, 84, weather.Code, weather.IsDay);
                Text(Number(weather.Temperature, "°"), 149, 235, 60, heavy: true, maxWidth: 190);
                Text(WeatherConditions.Describe(weather.Code, weather.IsDay), 48, 278, 20, maxWidth: 300, minimumSize: 16);
                Text("Percepita " + Number(weather.FeelsLike, " °C"), 48, 306, 16, muted, maxWidth: 300);
                Text("MIN " + Number(weather.Minimum, "°") + "   MAX " + Number(weather.Maximum, "°"), 48, 337, 18, maxWidth: 300);
                Text("Umidità " + Number(weather.Humidity, "%"), 48, 367, 20, maxWidth: 300);
                Text("Vento " + Number(weather.WindSpeed, " km/h"), 48, 397, 20, maxWidth: 300);
            }
        }
        void Compact(bool gaming)
        {
            var weatherLayout = config.Layout == "weather";
            var columns = weatherLayout ? 3 : 4;
            float startX = gaming || weatherLayout ? 380 : 32, cellWidth = weatherLayout ? 308 : gaming ? 252 : 314;
            float mediaX = gaming && !weatherLayout ? 1444 : 1352, mediaWidth = 1900 - mediaX;
            using var card = new SKPaint { Color = new SKColor(12, 24, 28, 200), IsAntialias = true };
            if (gaming || weatherLayout)
            {
                canvas.DrawRoundRect(SKRect.Create(32, 86, 332, 356), 8, 8, card);
                if (gaming) GamingDiscord(); else WeatherPanel();
            }
            canvas.DrawRoundRect(SKRect.Create(mediaX, 86, mediaWidth, 356), 8, 8, card);
            for (int i = 0; i < (weatherLayout ? 12 : WidgetCatalog.Slots.Count); i++)
                WidgetCard(i, startX + i % columns * (cellWidth + 12), 86 + i / columns * 92, cellWidth);
            if (gaming) GamePanel(mediaX, mediaWidth);
            else
            {
                Media(mediaX + 12, 108, mediaWidth - 24);
                canvas.DrawLine(mediaX + 12, 296, 1888, 296, line);
                Discord(mediaX + 12, 322, mediaWidth - 24);
            }
            Text(BackgroundStatus == "unavailable" ? "Sfondo non disponibile" : "ACTIVE / " + (state.ForegroundApp.Length > 0 ? state.ForegroundApp : "Desktop"), 32, 469, 13, muted, maxWidth: 850);
            Text(state.Hardware.Status == "connected" ? "SENSORI LIVE" : "SENSORI NON DISPONIBILI", 920, 469, 13, muted, maxWidth: 400);

        }
        void SpotifyPanel()
        {
            Cover(48, 90, 288);
            Text(state.Media.Title.Length > 0 ? state.Media.Title : "Spotify", 48, 407, 25, heavy: true, maxWidth: 290, minimumSize: 19);
            Text(state.Media.Artist, 48, 435, 20, muted, maxWidth: 290);
            canvas.DrawLine(354, 90, 354, 435, line);
            canvas.DrawLine(1340, 90, 1340, 435, line);
            for (var i = 0; i < 9; i++) WidgetCard(i, 1364 + i % 3 * 178, 100 + i / 3 * 106, 168);
            const float x = 388, width = 920;
            Text(state.SpotifyTransition ? "SPOTIFY / CAMBIO BRANO"
                : state.Media.Playing ? "SPOTIFY / IN RIPRODUZIONE" : "SPOTIFY / IN PAUSA", x, 115, 15, accent, true, width);
            var lyricsCredit = "Testi: LRCLIB";
            List<string> Wrap(string value, float size)
            {
                using var font = new SKFont(bold, size);
                var rows = new List<string>(); var row = "";
                foreach (var word in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var next = row.Length == 0 ? word : row + " " + word;
                    if (row.Length > 0 && font.MeasureText(next) > width) { rows.Add(row); row = word; }
                    else row = next;
                }
                if (row.Length > 0) rows.Add(row);
                return rows;
            }
            if (state.SpotifyTransition || state.Lyrics.Status == "loading")
            {
                Text(state.SpotifyTransition ? "Cambio brano…" : "Caricamento del testo…", x, 247, 32, muted, maxWidth: width);
            }
            else if (state.Lyrics.Status == "synced" && state.Lyrics.Lines is { } lines)
            {
                lyricsCredit += " · Sincronizzato";
                var index = state.Lyrics.CurrentLine(state.Media.PositionSeconds);
                if (index > 0) Text(lines[index - 1].Text, x, 174, 23, muted, maxWidth: width);
                var current = index >= 0 ? lines[index].Text : "";
                var wrapped = Wrap(current.Length == 0 ? "♪" : current, 38);
                for (var i = 0; i < Math.Min(2, wrapped.Count); i++) Text(wrapped[i] + (i == 1 && wrapped.Count > 2 ? " …" : ""), x, 247 + i * 48, 38, accent, true, width);
                if (index + 1 < lines.Length) Text(lines[index + 1].Text, x, 353, 23, muted, maxWidth: width);
            }
            else
            {
                var rows = (state.Lyrics.PlainText ?? "").Split('\n').SelectMany(row => Wrap(row.Trim(), 26)).ToArray();
                var pages = Math.Max(1, (rows.Length + 5) / 6);
                var page = (int)(state.Timestamp.ToUnixTimeSeconds() / 15 % pages);
                lyricsCredit += $" · Non sincronizzato · {page + 1}/{pages}";
                for (var i = 0; i < 6 && page * 6 + i < rows.Length; i++) Text(rows[page * 6 + i], x, 163 + i * 36, 26, maxWidth: width);
            }
            canvas.DrawLine(x, 401, x + width, 401, line);
            var duration = double.IsFinite(state.Media.DurationSeconds) ? Math.Clamp(state.Media.DurationSeconds, 0, 3600) : 0;
            var position = double.IsFinite(state.Media.PositionSeconds) ? Math.Clamp(state.Media.PositionSeconds, 0, duration) : 0;
            if (duration > 0) canvas.DrawRect(x, 399, (float)(position / duration) * width, 4, accentPaint);
            Text(duration > 0 ? TimeSpan.FromSeconds(position).ToString(@"m\:ss") + " / "
                + TimeSpan.FromSeconds(duration).ToString(@"m\:ss") : "— / —", x, 430, 18, muted);
            Text(lyricsCredit, x + width - 400, 430, 15, muted, maxWidth: 400);
        }
        void VoiceHeader()
        {
            var voice = TrackedVoiceHeader.Resolve(state.Discord, config.TrackedMemberId);
            if (voice.Muted is null) return;
            var color = voice.Muted is null ? muted : voice.Muted == true || voice.Deaf ? new SKColor(255, 146, 131) : accent;
            using var panel = new SKPaint { Color = new SKColor(12, 24, 28, 220), IsAntialias = true };
            canvas.DrawRoundRect(SKRect.Create(1180, 10, 340, 46), 7, 7, panel);
            Text(voice.Name, 1194, 40, 20, heavy: true, maxWidth: 194);
            Text(voice.Status, 1400, 40, 14, color, true, maxWidth: 108);
        }
        Text("PULSEDECK", 32, 42, 23, accent, true);
        Text("RECON / " + state.Profile.ToUpperInvariant(), 320, 42, 18, muted);
        if (appIcon is not null) canvas.DrawBitmap(appIcon, SKRect.Create(642, 14, 40, 40));

        Text(state.Foreground.DisplayName, 698, 44, 28, maxWidth: 450);
        Text(state.Timestamp.ToLocalTime().ToString("HH:mm:ss"), 1760, 42, 22);
        VoiceHeader();
        var volume = state.Volume;
        Text(volume.Status == "connected" ? volume.Muted ? "MUTO" : $"VOL {volume.Percent:0}%" : "VOL —",
            1550, 41, 21, volume.Muted ? new SKColor(255, 146, 131) : muted, maxWidth: 180);
        canvas.DrawLine(32, 66, 1888, 66, line);
        var gaming = config.GamingLayout && state.Profile == "gaming";
        if (SpotifyLyrics.Show(state, config)) SpotifyPanel();
        else if (gaming || config.Layout is "compact" or "weather") Compact(gaming);
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
        Text(BackgroundStatus == "unavailable" ? "Sfondo non disponibile" : "ACTIVE / " + (state.ForegroundApp.Length > 0 ? state.ForegroundApp : "Desktop"), 350, 432, 18, muted, maxWidth: 970);

        Media(1410, 113, 465);
        var side = widgets["side"];
        if (!side.Hidden)
        {
            Text(side.Label, 1410, 335, 15, muted, maxWidth: 465);
            ValueText(side, 1410, 379, 30, 465);
        }
        Text(state.Hardware.Status == "connected" ? "LIVE SENSOR DATA" : "SENSORS UNAVAILABLE", 1410, 432, 16, muted);
        }
        if (config.News.Enabled)
        {
            using var strip = new SKPaint { Color = new SKColor(8, 18, 23) };
            canvas.DrawRect(SKRect.Create(0, 446, 1920, 34), strip);
            var headline = state.News.Select(state.Timestamp, config.News.RotationSeconds);
            var newsSize = Math.Clamp(config.News.FontSize, 20, 26);
            Text(headline?.Source ?? "NEWS", 32, 470, newsSize - 4, accent, true, maxWidth: 235);
            var notice = state.News.Status switch { "loading" => "Aggiornamento notizie…", "not-configured" => "Scegli i canali in Configurazione → News",
                "empty" => "Nessuna notizia recente", _ => "Notizie non disponibili" };
            Text(headline?.Title ?? notice, 286, 470, newsSize, maxWidth: 1450, minimumSize: newsSize);
            Text(headline?.PublishedAt?.ToLocalTime().ToString("dd/MM HH:mm") ?? "data n/d", 1764, 470, Math.Max(20, newsSize - 4), maxWidth: 136);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var pixels = new byte[1920 * 480 * 4]; Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return new(encoded.ToArray(), pixels);
    }
    public void Dispose() { gameIcon?.Dispose(); appIcon?.Dispose(); cover?.Dispose(); backgrounds.Dispose(); gameArtwork.Dispose(); baseGradient.Dispose(); typeface.Dispose(); bold.Dispose(); }
}
