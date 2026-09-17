# Read-only inventory through the already-running ASUS service. No HAL registration.
param([ValidateRange(1,60)][int]$TimeoutSeconds = 20)
$ErrorActionPreference = 'Stop'
$service = Get-CimInstance Win32_Service -Filter "Name='LightingService'"
if (-not $service -or $service.State -ne 'Running') {
    throw 'LightingService must already be running; this probe does not start it.'
}
$exe = Join-Path $PSScriptRoot 'PulseDeck.AuraProbe.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the native probe first.' }
$process = $null
try {
    $start = New-Object Diagnostics.ProcessStartInfo($exe, '--service-capabilities')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.Encoding]::UTF8
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill(); $process.WaitForExit()
        throw "Service inventory probe exceeded $TimeoutSeconds seconds."
    }
    if ($process.ExitCode -ne 0) { throw "Inventory read failed: $($stderr.Result.Trim())" }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $inputText = [IO.StringReader]::new($stdout.Result)
    $reader = [Xml.XmlReader]::Create($inputText, $settings)
    try { while ($reader.Read()) {} }
    finally { $reader.Dispose(); $inputText.Dispose() }
    $after = Get-CimInstance Win32_Service -Filter "Name='LightingService'"
    if ($after.State -ne 'Running' -or $after.ProcessId -ne $service.ProcessId) {
        throw 'LightingService changed during the read; inspect before continuing.'
    }
    # Inventory can contain hardware identifiers: keep the full response outside Git.
    $directory = Join-Path $env:LOCALAPPDATA 'PulseDeck\aura-investigation'
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $path = Join-Path $directory ('service-capabilities-' + [Guid]::NewGuid().ToString('N') + '.xml')
    [IO.File]::WriteAllText($path, $stdout.Result, [Text.UTF8Encoding]::new($false))
    [PSCustomObject]@{
        Scope='running service capabilities only'
        Path=$path
        ServiceProcessId=$after.ProcessId
        ContainsProbeName=$stdout.Result.Contains('PulseDeck Virtual Probe')
    } | ConvertTo-Json
} finally {
    if ($process) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
}
