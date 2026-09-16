param([switch]$StandardUser)
$ErrorActionPreference = 'Stop'
$baseUrl = 'http://127.0.0.1:5178'
$agentPath = Join-Path $PSScriptRoot 'PulseDeck.Agent.exe'
$running = $false
try { $running = (Invoke-RestMethod "$baseUrl/api/health" -TimeoutSec 2).app -eq 'PulseDeck' } catch { }
if (-not $running) {
    if (-not (Test-Path $agentPath)) { throw 'Build PulseDeck first, then run this script from artifacts/windows.' }
    if ($StandardUser) {
        Start-Process -FilePath $agentPath -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
    } else {
        Start-Process -FilePath $agentPath -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -Verb RunAs
    }
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 500
        try {
            if ((Invoke-RestMethod "$baseUrl/api/health" -TimeoutSec 1).app -eq 'PulseDeck') { $running = $true; break }
        } catch { }
    }
}
if (-not $running) { throw 'PulseDeck did not start. Check its console window for the error.' }
Start-Process $baseUrl
