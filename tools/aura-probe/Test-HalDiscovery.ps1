# Isolated discovery experiment. Never registers a HAL with the running ASUS service.
param([switch]$EnumerateDevices, [switch]$Child, [string]$ScratchKey)
$ErrorActionPreference = 'Stop'
$probeGuid = '{702D21B6-3A25-4D2C-9F73-F64C78E212A8}'
if (-not $Child) {
    $ScratchKey = 'Software\PulseDeck\AuraProbe\' + [Guid]::NewGuid().ToString('N')
    $root = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($ScratchKey)
    $root.Dispose()
    $process = $null
    try {
        $exe = "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
        $arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '" -Child -ScratchKey "' + $ScratchKey + '"'
        if ($EnumerateDevices) { $arguments += ' -EnumerateDevices' }
        $start = New-Object Diagnostics.ProcessStartInfo($exe, $arguments)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $process = [Diagnostics.Process]::Start($start)
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(20000)) { $process.Kill(); $process.WaitForExit(); throw 'Probe exceeded 20 seconds.' }
        Write-Host $stdout.Result
        if ($stderr.Result) { Write-Host $stderr.Result }
        if ($process.ExitCode -ne 0) { throw "Isolated probe failed: exit $($process.ExitCode)" }
    } finally {
        if ($process -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($ScratchKey, $false)
        if ($process) { $process.Dispose() }
    }
    return
}
if ([IntPtr]::Size -ne 4 -or $ScratchKey -notmatch '^Software\\PulseDeck\\AuraProbe\\[a-f0-9]{32}$') {
    throw 'Child requires an isolated scratch key and 32-bit PowerShell.'
}
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ProbeRegistry {
    [DllImport("advapi32.dll")]
    public static extern int RegOverridePredefKey(IntPtr key, IntPtr replacement);
}
'@
# Activate only the SDK development facade before redirecting registry lookups.
# No hardware enumeration, mode changes, or LED methods are requested here.
Write-Host 'Probe: creating SDK facade.'
$sdk = New-Object -ComObject 'asus.aura'
Write-Host 'Probe: compiling the private HAL.'
Add-Type -Path (Join-Path $PSScriptRoot 'DiscoveryHal.cs')
Write-Host 'Probe: registering a process-local class factory.'
$cookie = [DiscoveryRegistration]::Register([Guid]$probeGuid)
Write-Host 'Probe: creating the private registry view.'
$root = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($ScratchKey, $true)
$classes = $root.CreateSubKey('Classes')
$category = 'CLSID\{109DC3E4-B9FF-4AF3-9008-AB13705D4E5F}\Instance\{E9BBD754-6CF4-492E-BA89-782177A2771B}\Instance'
$entry = $classes.CreateSubKey("$category\$probeGuid")
try {
    $entry.SetValue('Name', 'PulseDeck Virtual Probe')
    $entry.SetValue('Manufacturer', 'PulseDeck')
    $entry.SetValue('Version', '0.0.1')
    $entry.SetValue('Pluging', 0, [Microsoft.Win32.RegistryValueKind]::DWord)
} finally { $entry.Dispose() }
$hkcr = [IntPtr](-2147483648)
$redirected = $false
try {
    $errorCode = [ProbeRegistry]::RegOverridePredefKey($hkcr, $classes.Handle.DangerousGetHandle())
    if ($errorCode -ne 0) { throw "Registry redirection failed: $errorCode" }
    $redirected = $true
    $halInfo = $sdk.EumerateHalInfo()
    # Discovery is complete; restore COM type-library lookups before activation.
    $errorCode = [ProbeRegistry]::RegOverridePredefKey($hkcr, [IntPtr]::Zero)
    if ($errorCode -ne 0) { throw "Registry restoration failed: $errorCode" }
    $redirected = $false
    $guids = @()
    $deviceCount = $null
    for ($i = 0; $i -lt $halInfo.Count; $i++) {
        $item = $halInfo.Item($i)
        $guids += $item.Guid
        if ($item.Guid -eq $probeGuid) {
            Write-Host 'Probe: SDK discovered the private HAL entry; activating our in-process class.'
            $hal = $item.CreateHal()
            Write-Host "Probe: HAL activation returned; callbacks=$([DiscoveryFactory]::ActivationCalls)."
            if ($EnumerateDevices) {
                $devices = $hal.EumerateDevices()
                $deviceCount = $devices.Count
                [void][Runtime.InteropServices.Marshal]::ReleaseComObject($devices)
            }
            [void][Runtime.InteropServices.Marshal]::ReleaseComObject($hal)
        }
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($item)
    }
    [pscustomobject]@{
        Scope = 'Isolated SDK discovery; not Armoury Crate registration'
        ExpectedGuid = $probeGuid
        EnumeratedGuids = $guids
        Detected = ($guids -contains $probeGuid)
        HalActivations = [DiscoveryFactory]::ActivationCalls
        HalEnumerations = [DiscoveryHal]::EnumerationCalls
        DeviceEnumerationRequested = [bool]$EnumerateDevices
        DeviceCount = $deviceCount
    } | ConvertTo-Json -Depth 3
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($halInfo)
    if ($guids.Count -ne 1 -or $guids[0] -ne $probeGuid -or [DiscoveryFactory]::ActivationCalls -ne 1) {
        throw 'The SDK did not discover and activate exactly the private probe HAL.'
    }
} finally {
    if ($redirected) { [void][ProbeRegistry]::RegOverridePredefKey($hkcr, [IntPtr]::Zero) }
    [DiscoveryRegistration]::CoRevokeClassObject($cookie)
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($sdk)
    $classes.Dispose()
    $root.Dispose()
}
