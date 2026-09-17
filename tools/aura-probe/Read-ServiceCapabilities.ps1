# Read-only inventory through the already-running ASUS service. No HAL registration.
param(
    [ValidateRange(1,60)][int]$TimeoutSeconds = 20,
    [ValidateSet('Capabilities','Devices')][string]$Query = 'Capabilities'
)
$ErrorActionPreference = 'Stop'
$service = Get-CimInstance Win32_Service -Filter "Name='LightingService'"
if (-not $service -or $service.State -ne 'Running') {
    throw 'LightingService must already be running; this probe does not start it.'
}
$exe = Join-Path $PSScriptRoot 'PulseDeck.AuraProbe.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the native probe first.' }
$process = $null
try {
    $argument = if ($Query -eq 'Devices') { '--service-devices' } else { '--service-capabilities' }
    $start = New-Object Diagnostics.ProcessStartInfo($exe, $argument)
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
    $document=[Xml.XmlDocument]::new()
    $document.XmlResolver=$null
    try { $document.Load($reader) }
    finally { $reader.Dispose(); $inputText.Dispose() }
    $after = Get-CimInstance Win32_Service -Filter "Name='LightingService'"
    if ($after.State -ne 'Running' -or $after.ProcessId -ne $service.ProcessId) {
        throw 'LightingService changed during the read; inspect before continuing.'
    }
    # Inventory can contain hardware identifiers: keep the full response outside Git.
    $directory = Join-Path $env:LOCALAPPDATA 'PulseDeck\aura-investigation'
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $path = Join-Path $directory ('service-' + $Query.ToLowerInvariant() + '-' + [Guid]::NewGuid().ToString('N') + '.xml')
    [IO.File]::WriteAllText($path, $stdout.Result, [Text.UTF8Encoding]::new($false))
    # The service does not preserve the HAL Name in every response. Match our own
    # manufacturer/model instead of treating an absent name as an absent device.
    $probeDevices=@()
    if ($Query -eq 'Devices') {
        foreach ($device in $document.SelectNodes('/root/devicelist/device')) {
            if ($device.manufacture -eq 'PulseDeck' -and
                $device.model -in @('Isolated test destination','PulseDeck Virtual Probe')) {
                $probeDevices += [PSCustomObject]@{
                    Type=[string]$device.type; LightingName=[string]$device.lightingname
                    Model=[string]$device.model; Manufacturer=[string]$device.manufacture
                    LedCount=[string]$device.count; Index=[string]$device.index
                }
            }
        }
    }
    [PSCustomObject]@{
        Scope='running service metadata only'
        Query=$Query
        Path=$path
        ServiceProcessId=$after.ProcessId
        ContainsProbeName=$stdout.Result.Contains('PulseDeck Virtual Probe')
        ProbeIdentityCount=$probeDevices.Count
        ProbeDevices=$probeDevices
    } | ConvertTo-Json -Depth 5
} finally {
    if ($process) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
}
