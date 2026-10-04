using System.Runtime.InteropServices;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

// Windows process-loopback: include exactly the selected process tree.
// The enclosing type must be public: Windows needs COM-visible nested callback interfaces.
public static class ProcessAudioCapture
{
    private static readonly Guid AudioClientId = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    private static readonly Guid CaptureClientId = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
    public static void Run(int pid, CancellationToken token, Action<float[], long> publish)
    {
        Marshal.ThrowExceptionForHR(CoInitializeEx(nint.Zero, 0));
        var step = "activation";
        var parameters = Marshal.AllocHGlobal(12);
        Marshal.WriteInt32(parameters, 0, 1); // PROCESS_LOOPBACK
        Marshal.WriteInt32(parameters, 4, pid);
        Marshal.WriteInt32(parameters, 8, 0); // INCLUDE_TARGET_PROCESS_TREE
        var handler = new Completion(parameters);
        var variant = new PropVariant { Type = 65, Size = 12, Data = parameters }; // VT_BLOB
        IAsyncOperation? operation = null; IAudioClient? client = null; ICaptureClient? capture = null;
        try
        {
            var iid = AudioClientId;
            var hr = ActivateAudioInterfaceAsync("VAD\\Process_Loopback", ref iid, ref variant, handler, out operation);
            if (hr < 0) { handler.FailedToStart(); Marshal.ThrowExceptionForHR(hr); }
            if (!handler.Ready.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Audio activation timed out.");
            step = "activation-result";
            client = handler.Take();
            token.ThrowIfCancellationRequested();
            var format = new WaveFormat { Tag = 1, Channels = 2, SampleRate = AudioSpectrumAnalyzer.SampleRate,
                AverageBytes = AudioSpectrumAnalyzer.SampleRate * 4, BlockAlign = 4, Bits = 16 };
            // Shared, loopback, event-driven, automatic PCM conversion.
            step = "initialize";
            Marshal.ThrowExceptionForHR(client.Initialize(0, 0x80060000, 0, 0, ref format, nint.Zero));
            var captureId = CaptureClientId;
            step = "capture-service";
            Marshal.ThrowExceptionForHR(client.GetService(ref captureId, out var service));
            capture = (ICaptureClient)service;
            using var ready = new EventWaitHandle(false, EventResetMode.AutoReset);
            step = "event-handle";
            Marshal.ThrowExceptionForHR(client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle()));
            step = "start";
            Marshal.ThrowExceptionForHR(client.Start());
            var analyzer = new AudioSpectrumAnalyzer();
            var bytes = new byte[44100 * 4];
            step = "capture";
            while (!token.IsCancellationRequested)
            {
                if (!ready.WaitOne(200)) continue;
                Marshal.ThrowExceptionForHR(capture.GetNextPacketSize(out var available));
                while (available > 0 && !token.IsCancellationRequested)
                {
                    Marshal.ThrowExceptionForHR(capture.GetBuffer(out var data, out var frames, out var flags, out _, out _));
                    try
                    {
                        var length = checked((int)frames * 4);
                        if (length > bytes.Length) throw new IOException("Unexpected audio buffer size.");
                        var silent = (flags & 2) != 0;
                        if (!silent) Marshal.Copy(data, bytes, 0, length);
                        if (analyzer.AddPcm16(bytes, length, silent) is { } bands) publish(bands, analyzer.Samples);
                    }
                    finally { Marshal.ThrowExceptionForHR(capture.ReleaseBuffer(frames)); }
                    Marshal.ThrowExceptionForHR(capture.GetNextPacketSize(out available));
                }
            }
        }
        catch (Exception e) { e.Data["stage"] = step; throw; }
        finally
        {
            handler.Abandon();
            if (client is not null) { try { client.Stop(); } catch { } }
            if (capture is not null) Marshal.ReleaseComObject(capture);
            if (client is not null) Marshal.ReleaseComObject(client);
            if (operation is not null) Marshal.ReleaseComObject(operation);
            CoUninitialize();
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct PropVariant { public ushort Type, Reserved1, Reserved2, Reserved3; public uint Size; public nint Data; }
    [StructLayout(LayoutKind.Sequential, Pack = 2)] public struct WaveFormat { public ushort Tag, Channels; public int SampleRate, AverageBytes; public ushort BlockAlign, Bits, Extra; }
    [DllImport("Mmdevapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int ActivateAudioInterfaceAsync(string path, ref Guid iid, ref PropVariant parameters,
        [MarshalAs(UnmanagedType.Interface)] ICompletion completion, out IAsyncOperation operation);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [ComVisible(true), Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICompletion { [PreserveSig] int ActivateCompleted(IAsyncOperation operation); }
    [ComVisible(true), Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAgile { }
    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAsyncOperation { [PreserveSig] int GetActivateResult(out int result, [MarshalAs(UnmanagedType.IUnknown)] out object client); }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class Completion(nint parameters) : ICompletion, IAgile
    {
        private readonly object gate = new();
        public readonly ManualResetEventSlim Ready = new(false);
        private object? value; private int error; private bool abandoned;
        public int ActivateCompleted(IAsyncOperation operation)
        {
            lock (gate)
            {
                try
                {
                    error = operation.GetActivateResult(out var result, out var client);
                    if (error >= 0) error = result;
                    if (error < 0 || abandoned) { if (client is not null) Marshal.ReleaseComObject(client); }
                    else value = client;
                }
                catch (Exception e) { error = Marshal.GetHRForException(e); }
                finally { Free(); Ready.Set(); }
            }
            return 0;
        }
        public IAudioClient Take() { lock (gate) { Marshal.ThrowExceptionForHR(error); var client = (IAudioClient)(value ?? throw new IOException("No audio client.")); value = null; return client; } }
        public void Abandon() { lock (gate) { abandoned = true; if (value is not null) { Marshal.ReleaseComObject(value); value = null; } } }
        public void FailedToStart() { lock (gate) Free(); }
        private void Free() { if (parameters != nint.Zero) { Marshal.FreeHGlobal(parameters); parameters = nint.Zero; } }
    }
    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioClient
    {
        [PreserveSig] int Initialize(int mode, uint flags, long duration, long periodicity, ref WaveFormat format, nint session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int mode, nint format, out nint closest);
        [PreserveSig] int GetMixFormat(out nint format);
        [PreserveSig] int GetDevicePeriod(out long normal, out long minimum);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(nint handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }
    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICaptureClient
    {
        [PreserveSig] int GetBuffer(out nint data, out uint frames, out uint flags, out ulong position, out ulong performance);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
}
