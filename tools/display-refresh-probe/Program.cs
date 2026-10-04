using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.Json;
using PulseDeck.Core;
using SkiaSharp;

if (args.Length is < 1 or > 2 || args[0] != "--send" || args.Length == 2 && args[1] is not ("--thin" or "--paced" or "--batch" or "--replay" or "--batch8" or "--single" or "--batch0" or "--full4" or "--settled" or "--full8" or "--full8-priority")) return 2;
var settled = args.Length == 2 && args[1] == "--settled";
var priorityTrial = args.Length == 2 && args[1] == "--full8-priority";
var full8 = args.Length == 2 && args[1] is "--full8" or "--full8-priority";
var full = args.Length == 2 && args[1] is "--full4" or "--full8" or "--full8-priority";
using var probeProcess = Process.GetCurrentProcess();
var originalPriority = probeProcess.PriorityClass;
var priorityRaised = false;
var thin = args.Length == 2 && args[1] == "--thin";
var paced = args.Length == 2 && args[1] == "--paced";
var batch = args.Length == 2 && args[1] is "--batch" or "--batch8" or "--batch0" or "--settled";
var batch0 = args.Length == 2 && args[1] == "--batch0";
var batch8 = args.Length == 2 && args[1] == "--batch8";
var single = args.Length == 2 && args[1] == "--single";
var replay = args.Length == 2 && args[1] == "--replay";
using var api = new HttpClient { BaseAddress = new("http://127.0.0.1:5178"), Timeout = TimeSpan.FromSeconds(10) };
api.DefaultRequestHeaders.Add("X-PulseDeck-Client", "configurator");
using var current = JsonDocument.Parse(await api.GetStringAsync("/api/display"));
var display = current.RootElement;
const string identity = "chs_88inch.dev1_rom1.90";
if (!display.GetProperty("connected").GetBoolean() || display.GetProperty("deviceId").GetString() != identity) throw new IOException("Verified display must be connected.");
var portName = display.GetProperty("port").GetString()!;
using var query = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_0525&PID_A4A7%'");
using var devices = query.Get();
if (!devices.Cast<ManagementObject>().Any(d => (d["Name"]?.ToString() ?? "").Contains($"({portName})", StringComparison.OrdinalIgnoreCase))) throw new IOException("Unexpected USB device.");
var vendor = Process.GetProcessesByName("TURZX");
try { if (vendor.Length > 0) throw new IOException("TURZX is running."); } finally { foreach (var p in vendor) p.Dispose(); }
using var disconnected = await api.PostAsync("/api/display/disconnect", null); disconnected.EnsureSuccessStatusCode();
try
{
    using var serial = new SerialPort(portName, 115200) { Handshake = Handshake.RequestToSend, ReadTimeout = 2000, WriteTimeout = 10000 };
    serial.Open(); await Task.Delay(150); serial.DiscardInBuffer();
    void Write(byte[] packet) => serial.Write(packet, 0, packet.Length);
    string Read(int count = 1024)
    {
        var bytes = new byte[count]; var pos = 0;
        while (pos < count) { var n = serial.Read(bytes, pos, count - pos); if (n == 0) throw new IOException("Closed serial stream."); pos += n; }
        return Encoding.ASCII.GetString(bytes).Trim('\0', '\r', '\n', ' ');
    }
    Write(TurzxProtocol.Packet(Convert.FromHexString("01EF6900000001000000C5D3"))); var id = Read(23); if (id != identity) throw new IOException($"Unexpected panel identity: {id}");
    Write(TurzxProtocol.StopVideoCommand()); Write(TurzxProtocol.StopMediaCommand()); Read();
    async Task<byte[]> Preview()
    {
        using var bitmap = SKBitmap.Decode(await api.GetByteArrayAsync("/api/preview.png"));
        if (bitmap.Width != 1920 || bitmap.Height != 480 || bitmap.ColorType != SKColorType.Bgra8888) throw new IOException("Unexpected preview format.");
        return bitmap.Bytes;
    }
    var previous = await Preview();
    Write(TurzxProtocol.Packet(Convert.FromHexString("86EF6900000001")));
    Write(TurzxProtocol.Packet([0x2c], 0x2c)); Write(TurzxProtocol.FullFrameCommand()); Write(TurzxProtocol.FullFrame(previous));
    if (!Read().Contains("full_png_sucess")) throw new IOException("No full acknowledgement.");
    Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001"))); Console.WriteLine(Read());
    uint counter = 0; var watch = Stopwatch.StartNew(); var frames = 0;
    var transferTimes = new List<(double Encode, double Write, double Ack, double Status, bool Raised)>();
    if (thin)
    {
        foreach (var height in new[] { 1, 2, 3, 4, 8, 16 })
        {
            for (int i = 0; i < 100; i++)
            {
                var rect = new FrameRect(1728, 372, 140, height);
                var (header, payload) = TurzxProtocol.PartialFrame(previous, rect, 90, counter++);
                Write(header); Write(payload); Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001")));
                var status = Read();
                if (!status.StartsWith("needReSend:0")) { Console.WriteLine(JsonSerializer.Serialize(new { height, i, counter, status })); throw new IOException("Thin rectangle rejected."); }
                await Task.Delay(20);
            }
            Console.WriteLine(JsonSerializer.Serialize(new { height, accepted = 100 }));
        }
    }
    if (replay)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PulseDeck", "display-refresh-probe");
        var header = File.ReadAllBytes(Path.Combine(root, "rejected-partial.header"));
        var payload = File.ReadAllBytes(Path.Combine(root, "rejected-partial.bin"));
        if (header.Length != 250 || !header.AsSpan(0, 4).SequenceEqual(new byte[] { 0xcc, 0xef, 0x69, 0 }) || payload.Length > 4000000 || payload.Length % 250 != 0) throw new IOException("Invalid saved partial.");
        for (int trial = 0; trial < 5; trial++)
        {
            await Task.Delay(500);
            if (trial > 0)
            {
                Write(TurzxProtocol.Packet(Convert.FromHexString("86EF6900000001")));
                Write(TurzxProtocol.Packet([0x2c], 0x2c)); Write(TurzxProtocol.FullFrameCommand()); Write(TurzxProtocol.FullFrame(previous));
                if (!Read().Contains("full_png_sucess")) throw new IOException("No full acknowledgement.");
                Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001"))); Read();
                await Task.Delay(500);
            }
            Array.Clear(header, 10, 4);
            Write(header); Write(payload); Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001")));
            Console.WriteLine(JsonSerializer.Serialize(new { trial, status = Read() }));
        }
    }
    while (!thin && !replay && watch.Elapsed.TotalSeconds < (single || full || settled ? 300 : batch8 || batch0 ? 180 : 90))
    {
        if (priorityTrial && !priorityRaised && watch.Elapsed.TotalSeconds >= 150)
        {
            probeProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
            priorityRaised = true;
            Console.WriteLine(JsonSerializer.Serialize(new { phase = "AboveNormal", frames, seconds = watch.Elapsed.TotalSeconds }));
        }
        var cycleStarted = watch.Elapsed.TotalMilliseconds;
        var pixels = await Preview(); var regions = TurzxProtocol.ChangedRegions(previous, pixels);
        // Stress acknowledgements independently of the installed preview's 4 Hz cap.
        // Repeat current spectrum pixels when nothing changed; never synthesize readings.
        if (settled && regions.Length == 0) regions = [new(1380, 234, 492, 140)];
        if (single && regions.Length > 0) regions = [regions.Aggregate(TurzxProtocol.Union)];
        if (full && regions.Length > 0)
        {
            var step = Stopwatch.StartNew();
            var payload = TurzxProtocol.FullFrame(pixels);
            var encodeMs = step.Elapsed.TotalMilliseconds; step.Restart();
            Write(TurzxProtocol.Packet(Convert.FromHexString("86EF6900000001")));
            Write(TurzxProtocol.Packet([0x2c], 0x2c)); Write(TurzxProtocol.FullFrameCommand()); Write(payload);
            var writeMs = step.Elapsed.TotalMilliseconds; step.Restart();
            if (!Read().Contains("full_png_sucess")) throw new IOException("No full acknowledgement.");
            var ackMs = step.Elapsed.TotalMilliseconds; step.Restart();
            Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001")));
            var status = Read(); if (!status.StartsWith("needReSend:0")) throw new IOException("Full frame rejected.");
            transferTimes.Add((encodeMs, writeMs, ackMs, step.Elapsed.TotalMilliseconds, priorityRaised));
        }
        foreach (var rect in full ? [] : batch && regions.Length > 0 ? new[] { regions.Aggregate(TurzxProtocol.Union) } : regions)
        {
            var (header, payload) = batch ? TurzxProtocol.PartialFrameRegions(pixels, regions, 90, batch0 ? 0 : counter) : TurzxProtocol.PartialFrame(pixels, rect, 90, counter);
            Write(header); Write(payload);
            if (settled) { serial.BaseStream.Flush(); await Task.Delay(20); }
            Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001")));
            var status = Read();
            var report = new { seconds = watch.Elapsed.TotalSeconds, counter, rect, rawBytes = batch ? regions.Sum(r => r.Width * (5 + 4 * r.Height)) : rect.Width * (5 + 4 * rect.Height), encodedBytes = payload.Length, status };
            if (counter < 12 || !status.StartsWith("needReSend:0")) Console.WriteLine(JsonSerializer.Serialize(report));
            if (!status.StartsWith("needReSend:0"))
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PulseDeck", "display-refresh-probe", "rejected-partial.bin");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, payload);
                File.WriteAllBytes(Path.ChangeExtension(path, ".header"), header);
                File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonSerializer.Serialize(report));
                throw new IOException("Partial rejection; see report.");
            }
            counter++;
            if (paced) await Task.Delay(20);
        }
        previous = pixels; if (regions.Length > 0) frames++;
        if (regions.Length > 0 && frames % 100 == 0) Console.WriteLine(JsonSerializer.Serialize(new { frames, counter, seconds = watch.Elapsed.TotalSeconds }));
        var rest = batch8 || full || settled ? Math.Max(1, (full8 ? 125 : full ? 250 : settled ? 100 : 125) - (watch.Elapsed.TotalMilliseconds - cycleStarted)) : 20;
        await Task.Delay((int)Math.Ceiling(rest));
    }
    foreach (var phase in transferTimes.GroupBy(t => t.Raised))
        Console.WriteLine(JsonSerializer.Serialize(new { phase = phase.Key ? "AboveNormal" : originalPriority.ToString(), count = phase.Count(),
            encodeMs = phase.Average(x => x.Encode), writeMs = phase.Average(x => x.Write),
            acknowledgementMs = phase.Average(x => x.Ack), statusMs = phase.Average(x => x.Status) }));
    Console.WriteLine(JsonSerializer.Serialize(new { frames, counter, seconds = watch.Elapsed.TotalSeconds, fps = frames / watch.Elapsed.TotalSeconds,
        encodeMs = transferTimes.Count > 0 ? transferTimes.Average(x => x.Encode) : 0,
        writeMs = transferTimes.Count > 0 ? transferTimes.Average(x => x.Write) : 0,
        acknowledgementMs = transferTimes.Count > 0 ? transferTimes.Average(x => x.Ack) : 0,
        statusMs = transferTimes.Count > 0 ? transferTimes.Average(x => x.Status) : 0 }));
}
finally
{
    if (priorityRaised) probeProcess.PriorityClass = originalPriority;
    using var restored = await api.PostAsync("/api/display/connect", null); restored.EnsureSuccessStatusCode();
    Console.WriteLine("Agent display connection restored.");
}
return 0;
