# Sends session messages only to PulseDeck's hidden window; never shuts down Windows.
# Run in elevated Windows PowerShell, with the PulseDeck startup task installed.
param([switch]$EndSession)
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this check as administrator to reach the elevated agent window.'
}
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PulseDeckSessionCheck {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr FindWindow(string className, string title);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);
}
'@
$baseUrl = 'http://127.0.0.1:5178'
$agents = @([Diagnostics.Process]::GetProcessesByName('PulseDeck.Agent'))
if ($agents.Count -ne 1) { throw 'Expected exactly one PulseDeck agent.' }
$agent = $agents[0]
$window = [PulseDeckSessionCheck]::FindWindow("PulseDeck.SessionLifetime.$($agent.Id)", 'PulseDeck session notifications')
if ($window -eq [IntPtr]::Zero) { throw 'Session notification window not found.' }
if ([PulseDeckSessionCheck]::IsWindowVisible($window)) { throw 'Session notification window must be hidden.' }
$display = Invoke-RestMethod "$baseUrl/api/display" -TimeoutSec 5
if (-not $display.connected) { throw 'Connect the display before running this check.' }
function Send-SessionMessage([uint32]$message, [uint64]$value) {
    $result = [UIntPtr]::Zero
    $sent = [PulseDeckSessionCheck]::SendMessageTimeout($window, $message, [UIntPtr]$value, [IntPtr]::Zero, 2, 4500, [ref]$result)
    if ($sent -eq [IntPtr]::Zero) { throw "Session message failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())" }
    return $result.ToUInt64()
}
if ((Send-SessionMessage 0x11 0) -ne 1) { throw 'Agent did not accept QUERYENDSESSION.' }
$null = Send-SessionMessage 0x16 0
Start-Sleep -Milliseconds 1200
$state = Invoke-RestMethod "$baseUrl/api/state" -TimeoutSec 5
if ($agent.HasExited -or -not $state.display.connected) { throw 'Cancelled session end disrupted the agent or display.' }
Write-Output 'PASS: hidden window, query accepted, cancellation leaves agent/display connected.'
if ($EndSession) {
    try {
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $null = Send-SessionMessage 0x16 1
        Write-Output "Confirmed session-end handler returned after $($timer.ElapsedMilliseconds) ms."
        if (-not $agent.WaitForExit(20000)) { throw 'Agent did not exit after session end.' }
        Write-Output 'Agent exited. Observe screen-off before restart.'
        Start-Sleep -Seconds 8
        Get-CimInstance Win32_PnPEntity | Where-Object {
            $_.PNPDeviceID -match 'VID_0525|VID_1A86|VID_1D6B'
        } | Select-Object Name, PNPDeviceID | ConvertTo-Json -Compress
    } finally {
        if ($agent.HasExited) {
            Import-Module "$PSHOME\Modules\ScheduledTasks\ScheduledTasks.psd1"
            Start-ScheduledTask -TaskName PulseDeck
            Write-Output 'PulseDeck startup task launched again.'
        }
    }
}
