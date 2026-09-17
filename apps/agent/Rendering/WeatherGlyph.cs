using SkiaSharp;

namespace PulseDeck.Agent.Rendering;

// Code-drawn icons, shared by preview and USB; no downloaded artwork or fonts required.
public static class WeatherGlyph
{
    public static void Draw(SKCanvas canvas, float x, float y, float size, int? code, bool day)
    {
        canvas.Save(); canvas.Translate(x, y); canvas.Scale(size / 80);
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 209, 102) };
        if (PulseDeck.Core.WeatherConditions.Describe(code, day) == "Condizioni non disponibili")
        {
            paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 3; paint.Color = new SKColor(144, 162, 161);
            canvas.DrawCircle(40, 36, 23, paint); canvas.DrawLine(24, 52, 56, 20, paint);
        }
        else
        {
            if (code <= 2)
            {
                if (day)
                {
                    canvas.DrawCircle(31, 28, 15, paint); paint.StrokeWidth = 3; paint.StrokeCap = SKStrokeCap.Round;
                    for (var i = 0; i < 8; i++) { var a = i * MathF.PI / 4; canvas.DrawLine(31 + MathF.Cos(a) * 21, 28 + MathF.Sin(a) * 21, 31 + MathF.Cos(a) * 27, 28 + MathF.Sin(a) * 27, paint); }
                }
                else
                {
                    using var outer = new SKPath(); outer.AddCircle(31, 28, 22);
                    using var inner = new SKPath(); inner.AddCircle(42, 19, 21);
                    using var moon = outer.Op(inner, SKPathOp.Difference); canvas.DrawPath(moon, paint);
                }
            }
            if (code > 0)
            {
                paint.Color = new SKColor(180, 201, 210);
                canvas.DrawCircle(25, 43, 15, paint); canvas.DrawCircle(42, 33, 19, paint); canvas.DrawCircle(61, 45, 13, paint);
                canvas.DrawRoundRect(SKRect.Create(18, 40, 48, 18), 7, 7, paint);
                if (code is 45 or 48) { paint.StrokeWidth = 3; canvas.DrawLine(13, 65, 71, 65, paint); canvas.DrawLine(22, 73, 61, 73, paint); }
                else if (code >= 95)
                {
                    paint.Color = new SKColor(255, 209, 102);
                    using var bolt = new SKPath(); bolt.MoveTo(41, 51); bolt.LineTo(31, 68); bolt.LineTo(41, 65); bolt.LineTo(36, 80); bolt.LineTo(53, 59); bolt.LineTo(42, 60); bolt.Close(); canvas.DrawPath(bolt, paint);
                }
                else if (code >= 51)
                {
                    paint.Color = new SKColor(105, 191, 246); paint.StrokeWidth = 3; paint.StrokeCap = SKStrokeCap.Round;
                    for (var i = 0; i < 3; i++)
                        if (code is 71 or 73 or 75 or 77 or 85 or 86) canvas.DrawCircle(23 + i * 17, 69, 3, paint);
                        else canvas.DrawLine(27 + i * 17, 64, 23 + i * 17, 73, paint);
                }
            }
        }
        canvas.Restore();
    }
}
