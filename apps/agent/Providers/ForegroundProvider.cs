using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PulseDeck.Agent.Providers;

public static class ForegroundProvider
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    public static string Read()
    {
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var id);
            return id == 0 ? "" : Process.GetProcessById((int)id).ProcessName;
        }
        catch { return ""; }
    }
}
