using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

// Approved effect shared by the live renderer and both standalone probes.
internal static class MailAnimation
{
    private static float Smooth(float value) { var t = Math.Clamp(value, 0, 1); return t * t * (3 - 2 * t); }
    public static void Draw(SKCanvas canvas, float seconds, SKTypeface typeface, int? count = 3, string title = "Nuova email", string caption = "GMAIL", bool test = false, string kind = "mail")
    {
        if (seconds <= 0) return;
        var arrival = Smooth(seconds / .25f);
        if (kind == "calendar") arrival *= 1 - Smooth((seconds - 2.75f) / .4f);
        var travel = Smooth((seconds - 1.7f) / 1.05f);
        var x = 960 + (1707 - 960) * travel;
        var y = 207 + (33 - 207) * travel - 55 * MathF.Sin(travel * MathF.PI);
        var size = (150 + (31 - 150) * travel) * (.8f + .2f * arrival);
        var rotation = 360 * Smooth((seconds - .25f) / 1.15f);
        using var paint = new SKPaint { IsAntialias = true };
        var cardAlpha = (byte)(arrival * (1 - Smooth((seconds - 1.65f) / .35f)) * 245);
        if (cardAlpha > 0)
        {
            paint.Color = new SKColor(8, 18, 25, cardAlpha);
            canvas.DrawRoundRect(new SKRect(757, 102, 1163, 355), 24, 24, paint);
            paint.Color = new SKColor(169, 255, 105, (byte)(cardAlpha * .35f));
            paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 1;
            canvas.DrawRoundRect(new SKRect(757, 102, 1163, 355), 24, 24, paint);
            paint.Style = SKPaintStyle.Fill;
            using var font = new SKFont(typeface, 25);
            paint.Color = new SKColor(237, 245, 248, cardAlpha);
            if (font.MeasureText(title) > 375) font.Size *= 375 / font.MeasureText(title);
            canvas.DrawText(title, 960, 306, SKTextAlign.Center, font, paint);
            using var captionFont = new SKFont(typeface, 14);
            paint.Color = new SKColor(144, 162, 161, cardAlpha);
            canvas.DrawText(caption, 960, 331, SKTextAlign.Center, captionFont, paint);
        }
        canvas.Save();
        canvas.Translate(x, y); canvas.RotateDegrees(rotation); canvas.Scale(size / 100);
        paint.Color = new SKColor(240, 243, 247, (byte)(arrival * 255));
        canvas.DrawRoundRect(new SKRect(-46, -30, 46, 31), 7, 7, paint);
        if (kind == "calendar")
        {
            paint.Color = SKColor.Parse("#4285F4").WithAlpha((byte)(arrival * 255));
            canvas.DrawRoundRect(new SKRect(-46, -30, 46, -8), 5, 5, paint);
            for (var row = 0; row < 2; row++)
                for (var col = 0; col < 3; col++) canvas.DrawRoundRect(new SKRect(-29 + col * 23, 1 + row * 15, -16 + col * 23, 10 + row * 15), 2, 2, paint);
            paint.Color = SKColors.White.WithAlpha((byte)(arrival * 255));
            canvas.DrawRoundRect(new SKRect(-24, -37, -17, -19), 3, 3, paint);
            canvas.DrawRoundRect(new SKRect(17, -37, 24, -19), 3, 3, paint);
            canvas.Restore();
            return; // Calendar has no unread count: its arrival fades out at the clock.
        }
        paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 12; paint.StrokeCap = SKStrokeCap.Round; paint.StrokeJoin = SKStrokeJoin.Round;
        void Line(SKColor color, params SKPoint[] points)
        {
            paint.Color = color.WithAlpha((byte)(arrival * 255));
            using var path = new SKPath(); path.MoveTo(points[0]);
            foreach (var point in points.Skip(1)) path.LineTo(point);
            canvas.DrawPath(path, paint);
        }
        Line(SKColor.Parse("#4285F4"), new(-42, 28), new(-42, -24));
        Line(SKColor.Parse("#34A853"), new(42, 28), new(42, -24));
        Line(SKColor.Parse("#EA4335"), new(-42, -24), new(0, 7), new(25, -11));
        Line(SKColor.Parse("#FBBC04"), new(25, -11), new(42, -24));
        canvas.Restore();
        var badge = Smooth((seconds - 2.6f) / .25f);
        if (badge > 0)
        {
            paint.Style = SKPaintStyle.Fill; paint.Color = new SKColor(234, 67, 53, (byte)(badge * 255));
            var value = count?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?";
            using var measure = new SKFont(typeface, 14);
            var width = Math.Max(22, measure.MeasureText(value) + 10);
            canvas.DrawRoundRect(new SKRect(1738 - width, 9, 1738, 31), 11, 11, paint);
            using var font = new SKFont(typeface, 14);
            paint.Color = SKColors.White.WithAlpha((byte)(badge * 255));
            canvas.DrawText(value, 1738 - width / 2, 25, SKTextAlign.Center, font, paint);
        }
    }
}
