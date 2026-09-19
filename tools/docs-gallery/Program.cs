using System.Globalization;
using System.Text.Json;
using PulseDeck.Agent.Providers;
using PulseDeck.Agent.Rendering;
using PulseDeck.Core;
using SkiaSharp;

// Documentation only. No providers, credentials, network, agent API or display writes.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/docs-gallery");
Directory.CreateDirectory(output);
var stamp = new DateTimeOffset(2026, 9, 18, 19, 42, 0, TimeSpan.Zero);
var green = SKColor.Parse("#A9FF69");
byte[] Art(int width, int height, bool album)
{
    using var bitmap = new SKBitmap(width, height);
    using var canvas = new SKCanvas(bitmap);
    using var background = new SKPaint { Shader = SKShader.CreateLinearGradient(new(0, 0), new(width, height),
        [SKColor.Parse("#12323b"), SKColor.Parse("#070d14")], SKShaderTileMode.Clamp) };
    canvas.DrawRect(0, 0, width, height, background);
    using var line = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = album ? 2 : 1.5f };
    for (var i = 0; i < 24; i++)
    {
        line.Color = green.WithAlpha((byte)(35 + i * 5));
        using var path = new SKPath();
        for (var x = 0; x <= width; x += 4)
        {
            var y = (float)(height * .5 + i * height * .023 - Math.Sin(x * .008 + i * .10) * height * .13 - Math.Cos(x * .015) * height * .07);
            if (x == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        canvas.DrawPath(path, line);
    }
    using var disk = new SKPaint { IsAntialias = true, Color = green };
    canvas.DrawCircle(width * .76f, height * .29f, height * .105f, disk);
    using var font = new SKFont(SKTypeface.FromFamilyName(OperatingSystem.IsWindows() ? "Segoe UI" : "DejaVu Sans"), height * .052f);
    canvas.DrawText(album ? "AFTER HOURS" : "VECTOR / HORIZON", width * .065f, height * .18f, font, disk);
    using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
    return encoded.ToArray();
}
var albumBytes = Art(512, 512, true);
var artwork = MediaArtwork.Decode(albumBytes)!;
var gamePath = Path.Combine(output, "sample-game.png");
File.WriteAllBytes(gamePath, Art(960, 320, false));
var media = new MediaSnapshot(true, "After Hours", "PulseDeck Sessions", "Spotify.exe", 83, 226, "connected") { ArtworkId = artwork.Id };
var metrics = new Metric[] { new("cpu.load", "CPU", 26, "%"), new("gpu.load", "GPU", 63, "%"), new("ram.load", "RAM", 48, "%"),
    new("ram.used", "RAM used", 15.4, "GiB"), new("ram.total", "RAM total", 32, "GiB"), new("cpu.temperature", "CPU temperature", 54, "°C"),
    new("gpu.temperature", "GPU temperature", 61, "°C"), new("gpu.power", "GPU power", 178, "W"), new("system.processes", "Processes", 214, "") };
SensorReading Sensor(string id, string name, string type, double value, string unit) => new(id, name, "demo", "Documentation fixture", "Cpu", type, value, value, value, unit);
var sensors = new[] { Sensor("demo/cpu/power", "CPU Package", "Power", 64, "W"), Sensor("demo/cpu/clock", "CPU Core", "Clock", 5100, "MHz"),
    Sensor("demo/cpu/fan", "CPU Fan", "Fan", 940, "RPM"), Sensor("demo/gpu/clock", "GPU Core", "Clock", 2475, "MHz"),
    Sensor("demo/network/down", "Download Speed", "Throughput", 2516582, "B/s"), Sensor("demo/network/up", "Upload Speed", "Throughput", 353894, "B/s") };
var config = new DeckConfig { Layout = "weather", AccentColor = "#a9ff69", TrackedMemberId = "demo-1", GameProcesses = ["vector-horizon"],
    WeatherLocation = new("Rome, Italy", 41.9, 12.5), News = new() { Enabled = true, FontSize = 20 } };
config.Widgets[0] = new("bar1", "metric", "cpu.load", "", "", "CPU", Style: "ring");
config.Widgets[1] = new("bar2", "metric", "gpu.load", "", "", "GPU", Style: "ring");
config.Widgets[2] = new("bar3", "metric", "ram.load", "", "", "RAM", Style: "ring");
config.Widgets[3] = new("value1", "metric", "cpu.temperature", "", "", "CPU TEMP", Style: "value");
config.Widgets[4] = new("value2", "metric", "gpu.temperature", "", "", "GPU TEMP", Style: "value");
config.Widgets[5] = new("value3", "metric", "system.processes", "", "", "PROCESSI");
config.Widgets[6] = new("value4", "sensor", "", sensors[0].Id, sensors[0].Name, "CPU POWER");
config.Widgets[7] = new("side", "metric", "gpu.power", "", "", "GPU POWER");
config.Widgets[8] = new("extra1", "network", "", sensors[4].HardwareId, sensors[4].HardwareName, "RETE");
for (var i = 9; i < 12; i++) config.Widgets[i] = new(WidgetCatalog.Slots[i].Id, "sensor", "", sensors[i - 8].Id, sensors[i - 8].Name, new[] { "CPU CLOCK", "CPU FAN", "GPU CLOCK" }[i - 9]);
var members = new[] { new VoiceMember("demo-1", "Nova", false, false), new VoiceMember("demo-2", "Orbit", false, false) { Speaking = true },
    new VoiceMember("demo-3", "Echo", true, false), new VoiceMember("demo-4", "Pixel", false, false), new VoiceMember("demo-5", "Lumen", false, false) };
var state = new DeckState(stamp, "desktop", "WindowsTerminal", new(metrics, "connected") { Sensors = sensors, IsAdministrator = true, PawnIoInstalled = true },
    media, new(members, members[0], "connected") { SpeakingStatus = "connected" }, new(true, "COM5", "chs_88inch.dev1_rom1.90", "connected"))
{
    Foreground = new(100, "WindowsTerminal", "WSL / open-source workspace", false, null, "unavailable"),
    Volume = new("connected", 32, false),
    Weather = new() { Status = "connected", LocationName = "Rome, Italy", Temperature = 23, FeelsLike = 24, Minimum = 18, Maximum = 26, Humidity = 54, WindSpeed = 8, Code = 1, IsDay = true },
    News = new() { Status = "connected", FetchedAt = stamp, Items = [new("PULSEDECK", "Documentation preview / sample data / original artwork", "https://github.com/cicarulez/PulseDeck", stamp)] }
};
using var renderer = new DeckRenderer();
File.WriteAllBytes(Path.Combine(output, "desktop.png"), renderer.Render(state, config, artwork).Png);
var game = new ForegroundSnapshot(200, "vector-horizon", "Vector Horizon", true, null, "unavailable");
var gaming = state with { Profile = "gaming", Foreground = game, Game = game,
    GameSession = new(200, "vector-horizon", stamp.AddMinutes(-42), 2543), Fps = new("connected", 144, 6.9, 200),
    GameArtwork = new("available", gamePath, "documentation") };
File.WriteAllBytes(Path.Combine(output, "gaming.png"), renderer.Render(gaming, config, artwork).Png);
var music = state with { Profile = "music", Discord = new([], null, "idle"), Lyrics = new("synced", SpotifyLyrics.Key(media),
    [new(0, "A quiet room, a little light"), new(80, "Keep the rhythm in your sight"), new(95, "Let the evening drift away")]) };
File.WriteAllBytes(Path.Combine(output, "music.png"), renderer.Render(music, config, artwork).Png);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
File.WriteAllText(Path.Combine(output, "state.json"), JsonSerializer.Serialize(state, json));
File.WriteAllText(Path.Combine(output, "config.json"), JsonSerializer.Serialize(config, json));
File.WriteAllText(Path.Combine(output, "catalog.json"), JsonSerializer.Serialize(new { slots = WidgetCatalog.Slots, defaults = WidgetCatalog.Defaults() }, json));
Console.WriteLine($"Documentation fixtures rendered to {output}. Nothing sent to hardware.");

// Notification composition fixtures, including a real-style mail badge underneath
// the transient calendar arrival. All counts and events here are synthetic.
foreach (var (name, seconds) in new[] { ("arrival", 1f), ("travel", 2.2f), ("settled", 3.2f) })
{
    var notification = new NotificationVisual([new("gmail", "mail", "connected", 3)],
        new("calendar", "calendar", "Nuovo evento nel calendario", "GOOGLE CALENDAR"), seconds);
    File.WriteAllBytes(Path.Combine(output, $"calendar-notification-{name}.png"),
        renderer.Render(state with { Notifications = notification }, config, artwork).Png);
}
