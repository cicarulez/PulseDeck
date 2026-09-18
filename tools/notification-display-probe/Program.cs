using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PulseDeck.Core;
using SkiaSharp;

if (args.Length != 1 || args[0] != "--send")
{
    Console.WriteLine("Use --send to temporarily replace the connected PulseDeck display with three mail animation trials.");
    return;
}
const string expectedId = "chs_88inch.dev1_rom1.90";
using var client = new HttpClient { BaseAddress = new("http://127.0.0.1:5178"), Timeout = TimeSpan.FromSeconds(15) };
client.DefaultRequestHeaders.Add("X-PulseDeck-Client", "configurator");
using var initial = JsonDocument.Parse(await client.GetStringAsync("/api/display"));
var display = initial.RootElement;
if (!display.GetProperty("connected").GetBoolean() || display.GetProperty("userDisconnected").GetBoolean()
    || display.GetProperty("deviceId").GetString() != expectedId)
    throw new InvalidOperationException("The verified display must already be connected to PulseDeck.");
var portName = display.GetProperty("port").GetString()!;
ValidatePort(portName);
// Keep the existing panel image in memory only; do not save user media/calendar data.
using var background = SKBitmap.Decode(await client.GetByteArrayAsync("/api/preview.png"));
if (background is null || background.Width != 1920 || background.Height != 480)
    throw new InvalidOperationException("Expected the current 1920x480 preview.");
using var bitmap = new SKBitmap(1920, 480, SKColorType.Bgra8888, SKAlphaType.Premul);
using var canvas = new SKCanvas(bitmap);
using var fontFace = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);
using var font = new SKFont(fontFace, 20);
using var paint = new SKPaint { IsAntialias = true };
var transfers = new List<double>();
bool released = false;
try
{
    // Mark restoration necessary before the request: a timeout may follow a successful release.
    released = true;
    using var disconnected = await client.PostAsync("/api/display/disconnect", null);
    disconnected.EnsureSuccessStatusCode();
    using var confirmation = JsonDocument.Parse(await client.GetStringAsync("/api/display"));
    if (confirmation.RootElement.GetProperty("connected").GetBoolean()) throw new IOException("Agent did not release display.");
    ValidatePort(portName);
    using var serial = new SerialPort(portName, 115200) { ReadTimeout = 2000, WriteTimeout = 5000, Handshake = Handshake.RequestToSend };
    serial.Open(); serial.DiscardInBuffer();
    void Write(byte[] packet) => serial.Write(packet, 0, packet.Length);
    string Read(int count)
    {
        var data = new byte[count]; var offset = 0; var watch = Stopwatch.StartNew();
        try
        {
            while (offset < count)
            {
                serial.ReadTimeout = Math.Max(1, 2000 - (int)watch.ElapsedMilliseconds);
                if (watch.ElapsedMilliseconds >= 2000) throw new TimeoutException("Display reply timed out.");
                var read = serial.Read(data, offset, count - offset);
                if (read == 0) throw new IOException("Display connection closed.");
                offset += read;
            }
            return Encoding.ASCII.GetString(data).Trim('\0', '\r', '\n', ' ');
        }
        finally { serial.ReadTimeout = 2000; }
    }
    Write(TurzxProtocol.Packet(Convert.FromHexString("01EF6900000001000000C5D3")));
    if (Read(23) != expectedId) throw new IOException("Device identity changed; no graphics sent.");
    Write(TurzxProtocol.StopVideoCommand()); Write(TurzxProtocol.StopMediaCommand()); Read(1024);
    void Frame(float elapsed, string label, bool measure = false)
    {
        canvas.Clear(SKColors.Black); canvas.DrawBitmap(background, 0, 0);
        MailAnimation.Draw(canvas, elapsed, fontFace);
        paint.Color = new SKColor(8, 18, 25); canvas.DrawRoundRect(new SKRect(600, 8, 1170, 58), 8, 8, paint);
        paint.Color = SKColor.Parse("#a9ff69"); canvas.DrawText(label, 885, 40, SKTextAlign.Center, font, paint);
        var pixels = new byte[1920 * 480 * 4]; Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        var watch = Stopwatch.StartNew();
        Write(TurzxProtocol.Packet(Convert.FromHexString("86EF6900000001")));
        Write(TurzxProtocol.Packet([0x2c], 0x2c));
        Write(TurzxProtocol.FullFrameCommand()); Write(TurzxProtocol.FullFrame(pixels));
        if (!Read(1024).Contains("full_png_sucess", StringComparison.OrdinalIgnoreCase)) throw new IOException("Full frame was not acknowledged.");
        Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001")));
        if (!Regex.IsMatch(Read(1024), @"\bneedReSend:0\b", RegexOptions.IgnoreCase)) throw new IOException("Display did not accept the frame.");
        if (measure) transfers.Add(watch.Elapsed.TotalMilliseconds);
    }
    for (var count = 3; count > 0; count--) { Frame(0, $"PROVA NOTIFICA FRA {count}…"); await Task.Delay(1000); }
    for (var cycle = 1; cycle <= 3; cycle++)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < 4)
        {
            // Use wall time and drop missed frames: never build a USB animation queue.
            var frameStart = watch.Elapsed.TotalMilliseconds;
            Frame((float)watch.Elapsed.TotalSeconds, $"PROVA NOTIFICA {cycle}/3 · 3 EMAIL SIMULATE", true);
            var remaining = 1000 / 30.0 - (watch.Elapsed.TotalMilliseconds - frameStart);
            if (remaining > 0) await Task.Delay(TimeSpan.FromMilliseconds(remaining));
        }
        Frame(4, $"PROVA NOTIFICA {cycle}/3 · BADGE FINALE");
        await Task.Delay(1800);
    }
    Frame(0, "PROVA COMPLETATA · RIPRISTINO PULSEDECK");
    var sorted = transfers.Order().ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new { acknowledgedAnimationFrames = sorted.Length,
        averageTransferMs = Math.Round(sorted.Average(), 1), p95TransferMs = Math.Round(sorted[(int)((sorted.Length - 1) * .95)], 1),
        transferLimitedFps = Math.Round(1000 / sorted.Average(), 1), fullFramesOnly = true }));
}
finally
{
    // SerialPort has left its using scope before the agent is allowed to reconnect.
    if (released)
    {
        using var restored = await client.PostAsync("/api/display/connect", null);
        restored.EnsureSuccessStatusCode();
        using var state = JsonDocument.Parse(await restored.Content.ReadAsStringAsync());
        if (!state.RootElement.GetProperty("connected").GetBoolean()) throw new IOException("PulseDeck display reconnection needs attention.");
        Console.WriteLine("PulseDeck display reconnected; agent and collectors were not restarted.");
    }
}

static void ValidatePort(string port)
{
    var vendor = Process.GetProcessesByName("TURZX");
    try { if (vendor.Length > 0) throw new IOException("TURZX must not own the display."); }
    finally { foreach (var process in vendor) process.Dispose(); }
    using var query = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_0525&PID_A4A7%'");
    using var devices = query.Get();
    if (!devices.Cast<ManagementObject>().Any(d => (d["Name"]?.ToString() ?? "").Contains($"({port})", StringComparison.OrdinalIgnoreCase)))
        throw new IOException("The configured port is not the verified awake USB device.");
}
