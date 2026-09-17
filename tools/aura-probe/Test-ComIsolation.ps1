# Own server/client only. No persistent COM registration or ASUS category changes.
param(
    [switch]$WithSdk,
    [switch]$TerminateServer,
    [ValidateRange(0,10)][int]$ObserveSeconds = 0,
    [ValidateRange(1,10)][int]$TimeoutSeconds = 5
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'PulseDeck.AuraProbe.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the native probe first.' }
$suffix = [Guid]::NewGuid().ToString('N')
$ready = [Threading.EventWaitHandle]::new($false, [Threading.EventResetMode]::ManualReset, "Local\PulseDeck.AuraProbe.Ready.$suffix")
$stop = [Threading.EventWaitHandle]::new($false, [Threading.EventResetMode]::ManualReset, "Local\PulseDeck.AuraProbe.Stop.$suffix")
$server = $null
$client = $null
function Start-Probe([string]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($exe, $Arguments)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    [Diagnostics.Process]::Start($start)
}
try {
    $server = Start-Probe "--com-server $PID $suffix"
    $serverOut = $server.StandardOutput.ReadToEndAsync()
    $serverErr = $server.StandardError.ReadToEndAsync()
    if (-not $ready.WaitOne($TimeoutSeconds * 1000)) { throw 'COM server did not become ready.' }
    $client = Start-Probe '--remote-contracts'
    $clientOut = $client.StandardOutput.ReadToEndAsync()
    $clientErr = $client.StandardError.ReadToEndAsync()
    if (-not $client.WaitForExit($TimeoutSeconds * 1000)) { throw 'COM client exceeded its deadline.' }
    if ($clientOut.Result) { Write-Output $clientOut.Result.Trim() }
    if ($clientErr.Result) { Write-Host $clientErr.Result }
    if ($client.ExitCode -ne 0) { throw "COM client failed: exit $($client.ExitCode)." }
    if ($WithSdk) { & (Join-Path $PSScriptRoot 'Test-HalDiscovery.ps1') -RemoteServer -TimeoutSeconds $TimeoutSeconds }
    if ($ObserveSeconds) { Start-Sleep -Seconds $ObserveSeconds }
    # Deliberately kill only our native server to check registration cleanup on crash.
    if ($TerminateServer) {
        if ($server.HasExited) { throw 'Server exited before the forced-termination check.' }
        $server.Kill(); $server.WaitForExit()
    }
} finally {
    if ($client) {
        if (-not $client.HasExited) { $client.Kill(); $client.WaitForExit() }
        $client.Dispose()
    }
    $stop.Set() | Out-Null
    if ($server) {
        if (-not $server.WaitForExit(2000)) { $server.Kill(); $server.WaitForExit() }
        if ($serverOut.Result) { Write-Output $serverOut.Result.Trim() }
        if ($serverErr.Result) { Write-Host $serverErr.Result }
        $code = $server.ExitCode
        $server.Dispose()
    }
    $ready.Dispose(); $stop.Dispose()
}
if ($code -ne 0 -and -not $TerminateServer) { throw "COM server failed: exit $code." }
$absent = $null
try {
    $absent = Start-Probe '--remote-absent'
    $absentOut = $absent.StandardOutput.ReadToEndAsync()
    $absentErr = $absent.StandardError.ReadToEndAsync()
    if (-not $absent.WaitForExit($TimeoutSeconds * 1000)) { throw 'COM cleanup check exceeded its deadline.' }
    if ($absentOut.Result) { Write-Output $absentOut.Result.Trim() }
    if ($absentErr.Result) { Write-Host $absentErr.Result }
    if ($absent.ExitCode -ne 0) { throw 'COM class remains available after server exit.' }
} finally {
    if ($absent) {
        if (-not $absent.HasExited) { $absent.Kill(); $absent.WaitForExit() }
        $absent.Dispose()
    }
}
