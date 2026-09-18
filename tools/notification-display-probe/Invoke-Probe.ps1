param([Parameter(Mandatory=$true)][string]$ProbeDirectory)
$ErrorActionPreference = 'Stop'
$env:PSModulePath = "$PSHOME\Modules;" + $env:PSModulePath
Import-Module "$PSHOME\Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1"
$base = 'http://127.0.0.1:5178'
$headers = @{ 'X-PulseDeck-Client' = 'configurator' }
$before = Invoke-RestMethod "$base/api/display" -TimeoutSec 5
if (-not $before.connected -or $before.userDisconnected) { throw 'Display must already be connected.' }
$agentIds = @(Get-Process PulseDeck.Agent | Select-Object -ExpandProperty Id)
$output = Join-Path $ProbeDirectory 'probe.stdout.txt'
$errorOutput = Join-Path $ProbeDirectory 'probe.stderr.txt'
$probe = $null
try {
    $probe = Start-Process -FilePath (Join-Path $ProbeDirectory 'PulseDeck.NotificationDisplayProbe.exe') -ArgumentList '--send' -WindowStyle Hidden -PassThru -RedirectStandardOutput $output -RedirectStandardError $errorOutput
    # Retain the handle before waiting: Windows PowerShell can otherwise expose
    # a null ExitCode after the child exits, falsely reporting a successful probe.
    $null = $probe.Handle
    if (-not $probe.WaitForExit(60000)) {
        $probe.Kill()
        $probe.WaitForExit(5000) | Out-Null
        throw 'Probe exceeded its one-minute deadline.'
    }
    Get-Content $output
    if ($probe.ExitCode -ne 0) { Get-Content $errorOutput; throw 'Display probe failed.' }
}
finally {
    if ($probe -and -not $probe.HasExited) { $probe.Kill(); $probe.WaitForExit(5000) | Out-Null }
    $after = Invoke-RestMethod "$base/api/display" -TimeoutSec 5
    if (-not $after.connected) { $after = Invoke-RestMethod "$base/api/display/connect" -Method Post -Headers $headers -TimeoutSec 15 }
    $currentIds = @(Get-Process PulseDeck.Agent | Select-Object -ExpandProperty Id)
    [pscustomobject]@{ displayRestored=$after.connected; agentProcessPreserved=(($agentIds -join ',') -eq ($currentIds -join ',')) } | ConvertTo-Json -Compress
}
