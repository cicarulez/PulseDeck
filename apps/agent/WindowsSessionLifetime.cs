using System.ComponentModel;
using System.Runtime.InteropServices;
using PulseDeck.Agent.Display;

namespace PulseDeck.Agent;

// A top-level invisible window receives session broadcasts. A message-only window
// does not; console shutdown handlers are also insufficient once user32 is loaded.
public sealed class WindowsSessionLifetime(TurzxDisplay display, IHostApplicationLifetime lifetime,
    ILogger<WindowsSessionLifetime> logger) : IHostedService
{
    private const uint QueryEndSession = 0x0011, EndSession = 0x0016, Close = 0x0010, Destroy = 0x0002;
    private readonly TaskCompletionSource<nint> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WindowProc? callback;
    private nint window;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var thread = new Thread(MessageLoop) { IsBackground = true, Name = "PulseDeck Windows session events" };
        thread.Start();
        await ready.Task.WaitAsync(cancellationToken);
        logger.LogInformation("Windows session shutdown listener ready.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        display.Shutdown();
        var handle = Interlocked.CompareExchange(ref window, 0, 0);
        if (handle != 0) PostMessage(handle, Close, 0, 0);
        await finished.Task.WaitAsync(cancellationToken);
    }

    private void MessageLoop()
    {
        var className = $"PulseDeck.SessionLifetime.{Environment.ProcessId}";
        var instance = GetModuleHandle(null);
        ushort atom = 0;
        try
        {
            callback = ProcessMessage;
            var windowClass = new WindowClass { Instance = instance, Procedure = callback, ClassName = className };
            atom = RegisterClass(ref windowClass);
            if (atom == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            var handle = CreateWindowEx(0, className, "PulseDeck session notifications", 0,
                0, 0, 0, 0, 0, 0, instance, 0);
            if (handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            Interlocked.Exchange(ref window, handle);
            ready.TrySetResult(handle);
            int result;
            while ((result = GetMessage(out var message, 0, 0, 0)) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
            if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch (Exception e)
        {
            ready.TrySetException(e);
            logger.LogError(e, "Windows session listener failed.");
            lifetime.StopApplication();
        }
        finally
        {
            var handle = Interlocked.Exchange(ref window, 0);
            if (handle != 0) DestroyWindow(handle);
            if (atom != 0) UnregisterClass(className, instance);
            GC.KeepAlive(callback);
            finished.TrySetResult();
        }
    }

    private nint ProcessMessage(nint handle, uint message, nuint wParam, nint lParam)
    {
        switch (message)
        {
            case QueryEndSession:
                // Accept immediately. Another app can still cancel shutdown.
                return 1;
            case EndSession:
                if (wParam != 0)
                {
                    try
                    {
                        logger.LogInformation("Windows confirmed session end; powering off the display.");
                        display.Shutdown();
                    }
                    catch (Exception e) { logger.LogError(e, "Display shutdown handler failed."); }
                    finally { lifetime.StopApplication(); }
                }
                return 0;
            case Close:
                DestroyWindow(handle);
                return 0;
            case Destroy:
                Interlocked.Exchange(ref window, 0);
                PostQuitMessage(0);
                return 0;
            default:
                return DefWindowProc(handle, message, wParam, lParam);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProc(nint window, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style;
        public WindowProc Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? MenuName;
        public string ClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Window;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X, Y;
        public uint Private;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string className, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string name, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetMessage(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DispatchMessage(ref Message message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);
}
