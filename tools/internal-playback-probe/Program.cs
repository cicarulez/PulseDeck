using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using PulseDeck.Core;
using SkiaSharp;

var overlay = args.Length == 2 && args[0] == "--overlay";
var play = args.Length == 2 && args[0] is "--video" or "--overlay";
if (!play && (args.Length != 1 || args[0] != "--inspect")) return 2;
byte[]? clip = null;
if (play)
{
    var file = new FileInfo(args[1]);
    if (!file.Exists || file.Length != 26767) throw new IOException("Expected the original diagnostic clip.");
    clip = File.ReadAllBytes(file.FullName);
    if (!Convert.ToHexString(SHA256.HashData(clip)).Equals("EC81ABB9D5209EAE13229E5A21442AF7504EDBE0F46722D39326BCAAB99704B3", StringComparison.Ordinal))
        throw new IOException("Unexpected test clip hash; no device commands sent.");
}
const string identity = "chs_88inch.dev1_rom1.90";
using var api = new HttpClient { BaseAddress = new("http://127.0.0.1:5178"), Timeout = TimeSpan.FromSeconds(15) };
api.DefaultRequestHeaders.Add("X-PulseDeck-Client", "configurator");
using var state = JsonDocument.Parse(await api.GetStringAsync("/api/display"));
var current = state.RootElement;
if (!current.GetProperty("connected").GetBoolean() || current.GetProperty("deviceId").GetString() != identity
    || current.GetProperty("port").GetString() != "COM5") throw new IOException("Expected the verified connected COM5 panel.");
var vendors = Process.GetProcessesByName("TURZX");
try { if (vendors.Length > 0) throw new IOException("Close TURZX before inspection."); }
finally { foreach (var p in vendors) p.Dispose(); }
using var query = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_0525&PID_A4A7%'");
using var devices = query.Get();
if (!devices.Cast<ManagementObject>().Any(d => (d["Name"]?.ToString() ?? "").Contains("(COM5)", StringComparison.OrdinalIgnoreCase)))
    throw new IOException("The verified USB identity is not on COM5.");

var detached = false;
try
{
    detached = true; // Also restore if the HTTP response is lost after disconnecting.
    using (var response = await api.PostAsync("/api/display/disconnect", null))
    {
        response.EnsureSuccessStatusCode();
    }
    using var serial = new SerialPort("COM5", 115200) { Handshake = Handshake.RequestToSend, ReadTimeout = 5000, WriteTimeout = 5000 };
    serial.Open(); await Task.Delay(150); serial.DiscardInBuffer();
    void Write(byte[] packet) => serial.Write(packet, 0, packet.Length);
    string Read(int count)
    {
        var bytes = new byte[count]; var position = 0; var elapsed = Stopwatch.StartNew();
        while (position < count)
        {
            if (elapsed.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Reply exceeded the bounded read time.");
            var read = serial.Read(bytes, position, count - position);
            if (read == 0) throw new IOException("Serial reply ended early.");
            position += read;
        }
        return Encoding.ASCII.GetString(bytes).Trim('\0', '\r', '\n', ' ');
    }
    Write(TurzxProtocol.Packet(Convert.FromHexString("01EF6900000001000000C5D3")));
    if (Read(23) != identity) throw new IOException("HELLO identity changed; no media commands sent.");
    foreach (var directory in play ? Array.Empty<string>() : new[] { "/root/video/", "/mnt/SDCARD/video/" })
    {
        Write(TurzxMediaProtocol.ListVideos(directory));
        var reply = Read(10240);
        // Preserve only the bounded diagnostic reply in the user's runtime folder.
        Console.WriteLine(JsonSerializer.Serialize(new { directory, reply }));
        if (!reply.StartsWith("result:", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Unexpected storage reply; inspection stopped.");
    }
    if (play)
    {
        var path = $"/root/video/pulsedeck-test-{Guid.NewGuid():N}.mp4";
        Write(TurzxMediaProtocol.FileSize(path));
        if (Read(1024) != "0") throw new IOException("Test filename is not confirmed unused; upload skipped.");
        var created = false;
        try
        {
            Write(TurzxProtocol.StopVideoCommand());
            Write(TurzxProtocol.StopMediaCommand()); Read(1024);
            Write(TurzxMediaProtocol.UploadVideo(path, clip!.Length)); created = true;
            for (int offset = 0; offset < clip.Length; offset += 249)
                Write(TurzxProtocol.Packet(clip.AsSpan(offset, Math.Min(249, clip.Length - offset))));
            await Task.Delay(1200);
            Console.WriteLine(JsonSerializer.Serialize(new { uploadReply = serial.ReadExisting().Replace("\0", "") }));
            Write(TurzxMediaProtocol.FileSize(path));
            var size = Read(1024);
            if (size != clip.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw new IOException($"Upload size mismatch: {size}");
            Write(TurzxMediaProtocol.StartVideo(path));
            Console.WriteLine(JsonSerializer.Serialize(new { playbackReply = Read(1024), seconds = 90 }));
            if (!overlay) await Task.Delay(TimeSpan.FromSeconds(90));
            else
            {
                Write(TurzxProtocol.Packet(Convert.FromHexString("86EF6900000001")));
                Write(TurzxProtocol.Packet([0x2c], 0x2c));
                Write(TurzxVideoOverlayProtocol.InitialiseCommand());
                Write(TurzxProtocol.FullFrame(new byte[1920 * 480 * 4]));
                Console.WriteLine(JsonSerializer.Serialize(new { overlayInitialisation = Read(1024) }));
                Write(TurzxVideoOverlayProtocol.EmptyVisibilityCommand());
                Write(TurzxProtocol.Packet([0xef, 0x69]));
                await Task.Delay(300); serial.ReadExisting();
                Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001")));
                var emptyStatus = Read(1024);
                Console.WriteLine(JsonSerializer.Serialize(new { emptyOverlayStatus = emptyStatus }));
                var initialCounter = Regex.Match(emptyStatus, @"\brenderCnt:(\d+)\b");
                if (!emptyStatus.StartsWith("needReSend:0", StringComparison.OrdinalIgnoreCase)
                    || !initialCounter.Success || !uint.TryParse(initialCounter.Groups[1].Value, out var overlayCounter))
                    throw new IOException("The overlay counter is not verified.");
                var rect = new FrameRect(80, 40, 180, 48);
                using var face = SKTypeface.FromFamilyName("Segoe UI");
                using var font = new SKFont(face, 25);
                using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                using var background = new SKPaint { Color = new SKColor(12, 20, 24) };
                for (uint frame = 0; frame < 90; frame++)
                {
                    using var live = JsonDocument.Parse(await api.GetStringAsync("/api/state"));
                    var cpu = live.RootElement.GetProperty("hardware").GetProperty("metrics").EnumerateArray()
                        .FirstOrDefault(m => m.GetProperty("id").GetString() == "cpu.load");
                    var label = "CPU --";
                    if (cpu.ValueKind != JsonValueKind.Undefined && cpu.GetProperty("value").ValueKind == JsonValueKind.Number
                        && cpu.GetProperty("value").TryGetDouble(out var value) && double.IsFinite(value))
                        label = $"CPU {value:F1}%";
                    using var bitmap = new SKBitmap(new SKImageInfo(1920, 480, SKColorType.Bgra8888, SKAlphaType.Premul));
                    using (var canvas = new SKCanvas(bitmap))
                    {
                        canvas.Clear(SKColors.Transparent);
                        canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, background);
                        canvas.DrawText(label, rect.X + 10, rect.Y + 32, font, textPaint);
                    }
                    var update = TurzxVideoOverlayProtocol.Frame(bitmap.Bytes, rect, overlayCounter);
                    Write(update.Header); Write(update.Payload);
                    Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001")));
                    var status = Read(1024);
                    Console.WriteLine(JsonSerializer.Serialize(new { frame, overlayCounter, label, status }));
                    if (!status.StartsWith("needReSend:0", StringComparison.OrdinalIgnoreCase)) throw new IOException("Overlay rejected; restoring normal display.");
                    overlayCounter++;
                    await Task.Delay(1000);
                }
            }
        }
        finally
        {
            if (created)
            {
                try
                {
                    Write(TurzxProtocol.StopVideoCommand());
                    Write(TurzxProtocol.StopMediaCommand()); Read(1024);
                    // Remove only our newly allocated, uniquely named diagnostic file.
                    Write(TurzxMediaProtocol.DeleteVideo(path));
                    await Task.Delay(300); serial.ReadExisting();
                    Write(TurzxMediaProtocol.FileSize(path));
                    var remaining = Read(1024);
                    Console.WriteLine(JsonSerializer.Serialize(new { cleanupFileSize = remaining }));
                    if (remaining != "0") throw new IOException("The diagnostic file was not removed.");
                }
                catch (Exception e) { throw new IOException("Media cleanup failed; inspect the runtime diagnostic file.", e); }
            }
        }
    }
    return 0;
}
finally
{
    if (detached)
    {
        using var response = await api.PostAsync("/api/display/connect", null);
        response.EnsureSuccessStatusCode();
        Console.WriteLine("PulseDeck reconnect requested.");
    }
}
