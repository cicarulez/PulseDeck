using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using PulseDeck.Core;

namespace PulseDeck.Agent.Display;

public sealed class TurzxDisplay : IDisposable
{
    private readonly object gate = new();
    private SerialPort? serial;
    private byte[]? previous;
    private int rom;
    private uint counter;
    private DisplaySnapshot status = new(false, "", null, "disconnected");
    public DisplaySnapshot Status { get { lock (gate) return status; } }

    public static string[] Ports() => SerialPort.GetPortNames().Order().ToArray();

    public DisplaySnapshot Connect(string port)
    {
        lock (gate)
        {
            Disconnect();
            try
            {
                if (Process.GetProcessesByName("TURZX").Length > 0) throw new IOException("Close TURZX, including its tray icon, before connecting.");
                using var searcher = new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_0525&PID_A4A7%'");
                using var devices = searcher.Get();
                if (!devices.Cast<ManagementObject>().Any(d => (d["Name"]?.ToString() ?? "").Contains($"({port})", StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("The selected port is not a TURZX 0525:A4A7 device.");
                serial = new SerialPort(port, 115200) { ReadTimeout = 2000, WriteTimeout = 10000, Handshake = Handshake.RequestToSend };
                serial.Open(); serial.DiscardInBuffer();
                Write(Convert.FromHexString("01EF6900000001000000C5D3"));
                var id = Encoding.ASCII.GetString(ReadExactly(23)).Trim('\0', '\r', '\n', ' ');
                var match = Regex.Match(id, @"^chs_88inch\.dev\d+_rom\d+\.(\d+)$");
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out rom) || rom is < 80 or > 100)
                    throw new IOException($"Unsupported device response: {id}");
                Write(Convert.FromHexString("79EF6900000001"));
                Write(Convert.FromHexString("96EF6900000001"));
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

    public void Send(byte[] pixels)
    {
        lock (gate)
        {
            if (serial is null || !serial.IsOpen) return;
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
    private byte[] ReadExactly(int length)
    {
        var result = new byte[length];
        int offset = 0;
        while (offset < length) offset += serial!.Read(result, offset, length - offset);
        return result;
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
