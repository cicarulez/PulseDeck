using PulseDeck.Agent.Rendering;
using PulseDeck.Core;
using SkiaSharp;

// Isolated visual experiment: no providers, credentials, network or serial access.
var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/notification-preview");
Directory.CreateDirectory(output);
var stamp = new DateTimeOffset(2026, 9, 19, 18, 42, 0, TimeSpan.Zero);
var config = new DeckConfig { Layout = "weather", News = new() { Enabled = false } };
var state = new DeckState(stamp, "desktop", "Anteprima notifiche",
    new([], "unavailable"), new(false, "", "", "", 0, 0, "idle"), new([], null, "idle"), new(false, "", null, "disconnected"))
{
    Foreground = new(0, "preview", "Prova animazione", false, null, "unavailable"),
    Calendar = new("empty", [], stamp),
    Volume = new("connected", 32, false)
};
using var renderer = new DeckRenderer();
File.WriteAllBytes(Path.Combine(output, "base.png"), renderer.Render(state, config).Png);
using var typeface = SKTypeface.FromFamilyName(OperatingSystem.IsWindows() ? "Segoe UI" : "DejaVu Sans", SKFontStyle.Bold);
for (var frame = 0; frame <= 120; frame++)
{
    using var bitmap = new SKBitmap(1920, 480, SKColorType.Bgra8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);
    MailAnimation.Draw(canvas, frame / 30f, typeface);
    using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(Path.Combine(output, $"frame-{frame:D3}.png"), encoded.ToArray());
}
File.Copy(Path.Combine(AppContext.BaseDirectory, "index.html"), Path.Combine(output, "index.html"), true);
Console.WriteLine("Rendered 121 notification preview frames. Synthetic count: 3. No network or display access.");
