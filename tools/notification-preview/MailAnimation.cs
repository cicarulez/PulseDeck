using SkiaSharp;

internal static class MailAnimation
{
    public static void Draw(SKCanvas canvas, float seconds, SKTypeface typeface) =>
        PulseDeck.Agent.Rendering.MailAnimation.Draw(canvas, seconds, typeface, 3, "Nuova email", "GMAIL · NOTIFICA DI PROVA", true);
}
