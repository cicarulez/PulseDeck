# Owns a disposable native child and its private registry tree. No ASUS service changes.
param(
    [ValidateRange(1,100)][int]$Iterations = 3,
    [switch]$EmptyDevices,
    [ValidateRange(1,60)][int]$TimeoutSeconds = 20
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'PulseDeck.AuraProbe.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the native probe and run this script from its output directory.' }
$scratchKey = 'Software\PulseDeck\AuraProbe\' + [Guid]::NewGuid().ToString('N')
$root = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($scratchKey)
$root.Dispose()
$process = $null
try {
    $mode = if ($EmptyDevices) { 'empty' } else { 'device' }
    $arguments = '"' + $scratchKey + '" ' + $Iterations + ' ' + $mode
    $start = New-Object Diagnostics.ProcessStartInfo($exe, $arguments)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) { $process.Kill(); $process.WaitForExit() }
    if ($stdout.Result) { Write-Output $stdout.Result.Trim() }
    if ($stderr.Result) { Write-Host $stderr.Result }
    if ($timedOut) { throw "Probe exceeded $TimeoutSeconds seconds." }
    if ($process.ExitCode -ne 0) { throw "Native probe failed: exit $($process.ExitCode)" }
} finally {
    try {
        if ($process -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
    } finally {
        try { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($scratchKey, $false) }
        finally { if ($process) { $process.Dispose() } }
    }
}
