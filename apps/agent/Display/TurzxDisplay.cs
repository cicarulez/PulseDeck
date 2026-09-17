using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using PulseDeck.Core;

namespace PulseDeck.Agent.Display;

public sealed class TurzxDisplay(ILogger<TurzxDisplay> logger) : IDisposable
{
    private readonly object gate = new();
    private SerialPort? serial;
    private byte[]? previous;
    private int rom;
    private uint counter;
    private volatile bool stopping;
    private DisplaySnapshot status = new(false, "", null, "disconnected");
    public DisplaySnapshot Status { get { lock (gate) return status; } }

    public static string[] Ports() => SerialPort.GetPortNames().Order().ToArray();

    public DisplaySnapshot Connect(string port)
    {
        lock (gate)
        {
            if (stopping) return status with { Connected = false, Status = "stopping" };
            Disconnect();
            try
            {
                if (Process.GetProcessesByName("TURZX").Length > 0) throw new IOException("Close TURZX, including its tray icon, before connecting.");
                EnsureAwake(port);
                if (stopping) throw new IOException("PulseDeck is stopping.");
                serial = new SerialPort(port, 115200) { ReadTimeout = 2000, WriteTimeout = 10000, Handshake = Handshake.RequestToSend };
                serial.Open(); serial.DiscardInBuffer();
                Write(Convert.FromHexString("01EF6900000001000000C5D3"));
                var id = Encoding.ASCII.GetString(ReadExactly(23)).Trim('\0', '\r', '\n', ' ');
                var match = Regex.Match(id, @"^chs_88inch\.dev\d+_rom\d+\.(\d+)$");
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out rom) || rom is < 80 or > 100)
                    throw new IOException($"Unsupported device response: {id}");
                WritePacket(TurzxProtocol.StopVideoCommand());
                WritePacket(TurzxProtocol.StopMediaCommand());
                ReadStatus();
                status = new(true, port, id, "connected");
                counter = 0;
            }
            catch (Exception e)
            {
                serial?.Dispose(); serial = null;
                status = new(false, port, null, "error", e.Message);
            }
            return status;
        }
    }

    private static bool IsAwakePort(string port)
    {
        using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_0525&PID_A4A7%'");
        using var devices = searcher.Get();
        return devices.Cast<ManagementObject>().Any(d =>
            (d["Name"]?.ToString() ?? "").Contains($"({port})", StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureAwake(string port)
    {
        if (IsAwakePort(port)) return;
        // ScreenOff re-enumerates the tested 8.8-inch panel as CT88INCH. Opening
        // its standby serial interface with RTS/CTS wakes it; do not send image
        // commands to this interface or infer identity from a generic COM number.
        using var searcher = new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_1A86&PID_CA88%'");
        using var devices = searcher.Get();
        var standbyPorts = devices.Cast<ManagementObject>()
            .Where(d => TurzxProtocol.IsSupportedStandbyDevice(d["PNPDeviceID"]?.ToString() ?? ""))
            .Select(d => Regex.Match(d["Name"]?.ToString() ?? "", @"\((COM\d+)\)$", RegexOptions.IgnoreCase))
            .Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct().ToArray();
        if (standbyPorts.Length != 1) throw new IOException("The selected port is not an awake TURZX, and no unique supported standby device was found.");
        if (stopping) throw new IOException("PulseDeck is stopping.");
        using (var wake = new SerialPort(standbyPorts[0], 115200) { Handshake = Handshake.RequestToSend }) wake.Open();
        logger.LogInformation("Opened standby interface {StandbyPort}; waiting for TURZX on {Port}.", standbyPorts[0], port);
        var elapsed = Stopwatch.StartNew();
        while (!stopping && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (IsAwakePort(port)) return;
            Thread.Sleep(250);
        }
        throw new IOException("TURZX is waking up; retry the connection and check its configured COM port.");
    }

    public void Send(byte[] pixels)
    {
        lock (gate)
        {
            if (stopping || serial is null || !serial.IsOpen) return;
            try
            {
                if (previous is null)
                {
                    Write(Convert.FromHexString("86EF6900000001"));
                    Write([0x2c], 0x2c);
                    var command = TurzxProtocol.FullFrameCommand();
                    serial.Write(command, 0, command.Length);
                    var payload = TurzxProtocol.FullFrame(pixels);
                    serial.Write(payload, 0, payload.Length);
                    var response = ReadStatus();
                    if (!response.Contains("full_png_sucess", StringComparison.OrdinalIgnoreCase)) throw new IOException($"Frame was not acknowledged: {response}");
                }
                else if (TurzxProtocol.ChangedRegion(previous, pixels) is { } rect)
                {
                    var (header, payload) = TurzxProtocol.PartialFrame(pixels, rect, rom, counter++);
                    serial.Write(header, 0, header.Length); serial.Write(payload, 0, payload.Length);
                }
                else return;
                Write(Convert.FromHexString("CFEF6900000001"));
                var state = ReadStatus();
                if (!state.Contains("needReSend:0", StringComparison.OrdinalIgnoreCase)) throw new IOException($"Display requested a resend: {state}");
                previous = pixels;
            }
            catch (Exception e)
            {
                serial?.Dispose(); serial = null; previous = null;
                status = status with { Connected = false, Status = "error", Error = e.Message };
            }
        }
    }

    private void Write(byte[] bytes, byte padding = 0)
    {
        var packet = TurzxProtocol.Packet(bytes, padding);
        serial!.Write(packet, 0, packet.Length);
    }
    private void WritePacket(byte[] packet) => serial!.Write(packet, 0, packet.Length);

    public void Shutdown()
    {
        // Stop new frames before waiting for an in-flight USB transfer. Windows may
        // terminate us as soon as WM_ENDSESSION returns; do not defer this to a task.
        stopping = true;
        if (!Monitor.TryEnter(gate, TimeSpan.FromMilliseconds(1500)))
        {
            logger.LogWarning("Display shutdown skipped: USB transfer did not finish within 1500 ms.");
            return;
        }
        try
        {
            if (serial is null || !serial.IsOpen) return;
            serial.WriteTimeout = 400;
            serial.ReadTimeout = 400;
            WritePacket(TurzxProtocol.StopVideoCommand());
            WritePacket(TurzxProtocol.StopMediaCommand());
            ReadStatus();
            WritePacket(TurzxProtocol.ScreenOffCommand());
            logger.LogInformation("TURZX screen-off command sent on {Port}.", status.Port);
            status = status with { Connected = false, Status = "off", Error = null };
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not send the TURZX screen-off command.");
            status = status with { Connected = false, Status = "error", Error = e.Message };
        }
        finally
        {
            try { serial?.Dispose(); }
            catch (Exception e) { logger.LogWarning(e, "Could not close the display during shutdown."); }
            serial = null; previous = null;
            Monitor.Exit(gate);
        }
    }
    private byte[] ReadExactly(int length)
    {
        var result = new byte[length];
        int offset = 0;
        var timeout = serial!.ReadTimeout;
        var elapsed = Stopwatch.StartNew();
        try
        {
            while (offset < length)
            {
                var remaining = timeout - (int)elapsed.ElapsedMilliseconds;
                if (remaining <= 0) throw new TimeoutException("Timed out waiting for the display response.");
                serial.ReadTimeout = remaining;
                var read = serial.Read(result, offset, length - offset);
                if (read == 0) throw new IOException("Display connection closed while reading.");
                offset += read;
            }
            return result;
        }
        finally { serial.ReadTimeout = timeout; }
    }
    private string ReadStatus() => Encoding.ASCII.GetString(ReadExactly(1024)).Trim('\0', '\r', '\n', ' ');
    public void Disconnect()
    {
        lock (gate)
        {
            serial?.Dispose(); serial = null; previous = null;
            status = status with { Connected = false, Status = "disconnected", Error = null };
        }
    }
    public void Dispose() => Disconnect();
}
