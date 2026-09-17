using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using PulseDeck.Core;

namespace PulseDeck.Agent.Display;

public sealed class TurzxDisplay : IDisposable
{
    private readonly ILogger<TurzxDisplay> logger;
    private readonly object gate = new();
    private readonly TurzxFrameDelivery delivery;
    private SerialPort? serial;
    private int rom;
    private volatile bool stopping;
    private long commandGeneration, connectionGeneration;
    private DisplaySnapshot status = new(false, "", null, "disconnected");
    public DisplaySnapshot Status { get { lock (gate) return status; } }

    public TurzxDisplay(ILogger<TurzxDisplay> logger)
    {
        this.logger = logger;
        delivery = new(WritePacket, ReadStatus, () =>
        {
            Open(status.Port, allowWake: false, expectedId: status.DeviceId);
            return rom;
        }, ClosePort, IsCancelled);
    }

    private bool IsCancelled() => stopping || Interlocked.Read(ref commandGeneration) != connectionGeneration;

    public static string[] Ports() => SerialPort.GetPortNames().Order().ToArray();

    public DisplaySnapshot Connect(string port, bool startup = false)
    {
        // A startup retry must not supersede a manual disconnect or an active recovery.
        // The same gate makes this check atomic with starting the connection.
        if (startup)
        {
            lock (gate)
            {
                if (status.UserDisconnected || status.Status == "recovering" || status.Connected || stopping) return status;
                return Connect(port);
            }
        }
        var generation = Interlocked.Increment(ref commandGeneration);
        lock (gate)
        {
            if (stopping || generation != Interlocked.Read(ref commandGeneration)) return status;
            connectionGeneration = generation;
            delivery.Cancel();
            ClosePort();
            try
            {
                var id = Open(port, allowWake: true);
                status = new(true, port, id, "connected");
                delivery.Start(rom);
            }
            catch (Exception e)
            {
                ClosePort();
                status = new(false, port, null, "error", e.Message);
            }
            return status;
        }
    }

    private string Open(string port, bool allowWake, string? expectedId = null)
    {
        CheckCancellation();
        var vendorProcesses = Process.GetProcessesByName("TURZX");
        try
        {
            if (vendorProcesses.Length > 0) throw new IOException("Close TURZX, including its tray icon, before connecting.");
        }
        finally { foreach (var process in vendorProcesses) process.Dispose(); }
        if (allowWake) EnsureAwake(port);
        else if (!IsAwakePort(port)) throw new IOException("The configured awake TURZX is unavailable; automatic recovery will not wake standby devices.");
        CheckCancellation();
        serial = new SerialPort(port, 115200) { ReadTimeout = 2000, WriteTimeout = 10000, Handshake = Handshake.RequestToSend };
        serial.Open(); serial.DiscardInBuffer();
        CheckCancellation();
        Write(Convert.FromHexString("01EF6900000001000000C5D3"));
        var id = Encoding.ASCII.GetString(ReadExactly(23)).Trim('\0', '\r', '\n', ' ');
        var match = Regex.Match(id, @"^chs_88inch\.dev\d+_rom\d+\.(\d+)$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out rom) || rom is < 80 or > 100
            || (expectedId is not null && id != expectedId))
            throw new IOException($"Unsupported or changed device response: {id}");
        CheckCancellation();
        WritePacket(TurzxProtocol.StopVideoCommand());
        WritePacket(TurzxProtocol.StopMediaCommand());
        ReadStatus();
        CheckCancellation();
        return id;
    }

    private void CheckCancellation()
    {
        if (IsCancelled()) throw new OperationCanceledException("Display connection cancelled.");
    }

    private void ClosePort()
    {
        var port = serial;
        serial = null;
        try { port?.Dispose(); }
        catch (Exception e) { logger.LogWarning(e, "Could not close the display port."); }
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
        CheckCancellation();
        using (var wake = new SerialPort(standbyPorts[0], 115200) { Handshake = Handshake.RequestToSend }) wake.Open();
        logger.LogInformation("Opened standby interface {StandbyPort}; waiting for TURZX on {Port}.", standbyPorts[0], port);
        var elapsed = Stopwatch.StartNew();
        while (!IsCancelled() && elapsed.Elapsed < TimeSpan.FromSeconds(5))
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
            if (IsCancelled() || status.Status is "disconnected" or "error" or "off") return;
            var recoveries = delivery.Recoveries;
            var attempts = delivery.Attempts;
            delivery.Send(pixels);
            var next = status with
            {
                Connected = delivery.State == "connected", Status = delivery.State,
                Error = delivery.State is "recovering" or "error" ? delivery.LastError : null,
                RecoveryAttempts = delivery.Attempts, Recoveries = delivery.Recoveries,
                AcknowledgedFrames = delivery.AcknowledgedFrames,
                LastTransportError = delivery.LastError, LastAcknowledgedAt = delivery.LastAcknowledgedAt
            };
            if (next.Status != status.Status || attempts != delivery.Attempts || recoveries != delivery.Recoveries)
                logger.LogInformation("TURZX {Port}: {State}; recovery attempts {Attempts}/2, recoveries {Recoveries}, acknowledged frames {Frames}; last error: {Error}",
                    next.Port, next.Status, next.RecoveryAttempts, next.Recoveries, next.AcknowledgedFrames, next.LastTransportError);
            status = next;
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
            delivery.Cancel();
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
            ClosePort();
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
        Interlocked.Increment(ref commandGeneration);
        lock (gate)
        {
            delivery.Cancel();
            ClosePort();
            status = status with { Connected = false, Status = "disconnected", Error = null, UserDisconnected = true };
        }
    }
    public void Dispose() => Disconnect();
}
