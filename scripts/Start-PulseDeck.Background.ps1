$ErrorActionPreference = 'Stop'
$baseUrl = 'http://127.0.0.1:5178'
$agentPath = Join-Path $PSScriptRoot 'PulseDeck.Agent.exe'
$dataDir = if ($env:PULSEDECK_DATA_DIR) { $env:PULSEDECK_DATA_DIR } else { Join-Path $env:LOCALAPPDATA 'PulseDeck' }
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$startupLog = Join-Path $dataDir 'startup.log'
function Write-StartupLog([string]$message) { Add-Content -LiteralPath $startupLog -Value "$(Get-Date -Format o) $message" }
function Test-Agent {
    try { return (Invoke-RestMethod "$baseUrl/api/health" -TimeoutSec 2).app -eq 'PulseDeck' }
    catch { return $false }
}

$agent = $null
try {
    if (-not (Test-Path -LiteralPath $agentPath)) { throw 'PulseDeck.Agent.exe is missing from the startup folder.' }
    if (-not (Test-Agent)) {
        $agent = Start-Process -FilePath $agentPath -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $dataDir 'agent.stdout.log') `
            -RedirectStandardError (Join-Path $dataDir 'agent.stderr.log')
        Write-StartupLog "Started agent PID $($agent.Id)."
    } else { Write-StartupLog 'Using the agent already running.' }

    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if (Test-Agent) { $ready = $true; break }
        if ($agent -and $agent.HasExited) { throw 'Agent exited before becoming ready. See agent.stderr.log.' }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'Agent did not become ready within the startup timeout.' }

    # USB enumeration may lag behind login. Retry only during startup, never after
    # the user deliberately disconnects a successfully connected display.
    $connected = $false
    for ($attempt = 0; $attempt -lt 24; $attempt++) {
        try {
            $display = Invoke-RestMethod "$baseUrl/api/display" -TimeoutSec 5
            if ($display.userDisconnected) { Write-StartupLog 'Display deliberately disconnected; cancelling startup connection attempts.'; break }
            if (-not $display.connected) {
                $display = Invoke-RestMethod "$baseUrl/api/display/connect?startup=true" -Method Post `
                    -Headers @{ 'X-PulseDeck-Client' = 'configurator' } -TimeoutSec 20
            }
            if ($display.connected) { $connected = $true; Write-StartupLog 'Display connected.'; break }
        } catch { }
        Start-Sleep -Seconds 5
    }
    if (-not $connected) { Write-StartupLog 'Display unavailable after startup retries; use the configurator to connect later.' }

    # Keep the scheduled task attached to the process it owns. An intentional
    # stop exits successfully; an unexpected failure can be retried by Windows.
    if ($agent) {
        $agent.WaitForExit()
        $agent.Refresh()
        $code = $agent.ExitCode
        Write-StartupLog "Agent exited with code $code."
        exit $code
    }
} catch {
    Write-StartupLog $_.Exception.Message
    exit 1
}
