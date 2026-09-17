# Bounded LocalSystem client against our interactive-user COM server.
# Aura category publication is optional, time-limited and follows successful SYSTEM checks.
# Never calls an ASUS service control method, refresh, profile change or LED setter.
param(
    [ValidateSet('Supervisor','Admin','Client')][string]$Phase = 'Supervisor',
    [switch]$InteractiveIdentity,
    [switch]$WithSdk,
    [ValidateRange(0,60)][int]$ObserveAuraSeconds = 0,
    [string]$RunId,
    [string]$RunRoot,
    [int]$SupervisorId
)
$ErrorActionPreference = 'Stop'
if ($ObserveAuraSeconds -and (-not $InteractiveIdentity -or -not $WithSdk)) {
    throw 'Aura observation requires InteractiveIdentity and WithSdk.'
}
$exe = Join-Path $PSScriptRoot 'PulseDeck.AuraProbe.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run from the native probe output directory.' }
if ($Phase -eq 'Supervisor') {
    $RunId = [Guid]::NewGuid().ToString('N'); $SupervisorId = $PID
    $RunRoot = Join-Path $env:LOCALAPPDATA 'PulseDeck\aura-investigation\system-com-runs'
}
if ($RunId -cnotmatch '^[0-9a-f]{32}$' -or $SupervisorId -le 0) { throw 'Invalid run identity.' }
if (-not $RunRoot -or -not [IO.Path]::IsPathRooted($RunRoot)) { throw 'An absolute private runtime directory is required.' }
$run = Join-Path $RunRoot $RunId
$resultPath = Join-Path $run 'client.json'
$adminPath = Join-Path $run 'admin.json'
function Write-Result($Value, [string]$Path) {
    $text = $Value | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText($Path + '.tmp', $text, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath ($Path + '.tmp') -Destination $Path
}
function Start-Native([string]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($exe, $Arguments)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    [Diagnostics.Process]::Start($start)
}
if ($Phase -eq 'Client') {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $identity.IsSystem) { throw 'This phase must run through the temporary SYSTEM task.' }
    $process = $null
    try {
        $process = Start-Native '--remote-contracts'
        $out = $process.StandardOutput.ReadToEndAsync(); $err = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit(); throw 'SYSTEM client timed out.' }
        $sdkResult=$null
        if ($WithSdk -and $process.ExitCode -eq 0) {
            $sdkResult = & (Join-Path $PSScriptRoot 'Test-HalDiscovery.ps1') -RemoteServer -TimeoutSeconds 5
        }
        Write-Result @{
            IsSystem=$identity.IsSystem; Session=(Get-Process -Id $PID).SessionId
            ExitCode=$process.ExitCode; Output=$out.Result.Trim(); Error=$err.Result.Trim(); Sdk=$sdkResult
        } $resultPath
    } catch { Write-Result @{Failure=$_.Exception.Message} $resultPath; throw }
    finally {
        if ($process) { if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }; $process.Dispose() }
    }
    exit
}
if ($Phase -eq 'Admin') {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator token required.' }
    $taskName = 'PulseDeck-AuraProbe-' + $RunId
    $classPath = 'SOFTWARE\Classes\CLSID\{702D21B6-3A25-4D2C-9F73-F64C78E212A8}'
    $appPath = 'SOFTWARE\Classes\AppID\{2BD83B9F-96A6-49EC-88F6-26C4E2975677}'
    $categoryPath = 'SOFTWARE\Classes\CLSID\{109DC3E4-B9FF-4AF3-9008-AB13705D4E5F}\Instance\{E9BBD754-6CF4-492E-BA89-782177A2771B}\Instance\{702D21B6-3A25-4D2C-9F73-F64C78E212A8}'
    $machine = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry32)
    $classOwned=$false; $appOwned=$false; $taskOwned=$false; $categoryOwned=$false
    $failure=$null; $cleanup=@(); $observations=@(); $registrationInfo=$null
    try {
        if ($InteractiveIdentity) {
            foreach ($path in @($classPath,$appPath,$categoryPath)) {
                $existing=$machine.OpenSubKey($path)
                if ($existing) { $existing.Dispose(); throw 'Probe COM registration already exists; refusing to replace it.' }
            }
            $key=$machine.CreateSubKey($appPath); $appOwned=$true
            try { $key.SetValue('RunAs','Interactive User') } finally { $key.Dispose() }
            $key=$machine.CreateSubKey($classPath); $classOwned=$true
            try { $key.SetValue('AppID','{2BD83B9F-96A6-49EC-88F6-26C4E2975677}') } finally { $key.Dispose() }
        }
        [IO.File]::WriteAllText((Join-Path $run 'admin-ready'), 'ready')
        $deadline=(Get-Date).AddSeconds(40)
        while (-not (Test-Path -LiteralPath (Join-Path $run 'server-ready'))) {
            if ((Get-Date) -ge $deadline -or -not (Get-Process -Id $SupervisorId -ErrorAction SilentlyContinue)) { throw 'Supervisor/server unavailable.' }
            Start-Sleep -Milliseconds 100
        }
        if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) { throw 'Temporary task name already exists.' }
        $clientArguments='-NoProfile -NonInteractive -File "'+$PSCommandPath+'" -Phase Client -RunId '+$RunId+' -SupervisorId '+$SupervisorId+' -RunRoot "'+$RunRoot+'"'
        if ($WithSdk) { $clientArguments += ' -WithSdk' }
        $action=New-ScheduledTaskAction -Execute "$PSHOME\powershell.exe" -Argument $clientArguments
        $account=New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
        $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Seconds 20) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
        Register-ScheduledTask -TaskName $taskName -Action $action -Principal $account -Settings $settings | Out-Null
        $taskOwned=$true
        Start-ScheduledTask -TaskName $taskName
        $deadline=(Get-Date).AddSeconds(15)
        while (-not (Test-Path -LiteralPath $resultPath)) {
            if ((Get-Date) -ge $deadline -or -not (Get-Process -Id $SupervisorId -ErrorAction SilentlyContinue)) { throw 'SYSTEM task did not return a result.' }
            Start-Sleep -Milliseconds 100
        }
        $clientResult=Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if ($ObserveAuraSeconds) {
            if ($clientResult.Failure -or $clientResult.ExitCode -ne 0 -or -not $clientResult.Sdk) {
                throw 'SYSTEM direct/SDK checks failed; refusing Aura publication.'
            }
            $key=$machine.CreateSubKey($categoryPath); $categoryOwned=$true
            try {
                $key.SetValue('Name','PulseDeck Virtual Probe')
                $key.SetValue('Manufacturer','PulseDeck')
                $key.SetValue('Version','0.1.0-probe')
                $key.SetValue('Pluging',0,[Microsoft.Win32.RegistryValueKind]::DWord)
            } finally { $key.Dispose() }
            $inventory=$null
            try {
                $inventory=Start-Native '--registered-hal-info'
                $inventoryOut=$inventory.StandardOutput.ReadToEndAsync(); $inventoryErr=$inventory.StandardError.ReadToEndAsync()
                if (-not $inventory.WaitForExit(5000)) { throw 'Registered HAL metadata read timed out.' }
                if ($inventory.ExitCode -ne 0) { throw ('Registered HAL metadata read failed: '+$inventoryErr.Result.Trim()) }
                $registrationInfo=$inventoryOut.Result | ConvertFrom-Json
                if ($registrationInfo.probeMatches -ne 1) { throw 'The SDK does not see exactly one probe entry in the real registry.' }
            } finally {
                if ($inventory) { if (-not $inventory.HasExited) { $inventory.Kill(); $inventory.WaitForExit() }; $inventory.Dispose() }
            }
            [IO.File]::WriteAllText((Join-Path $run 'aura-published'),'temporary category present')
            $deadline=(Get-Date).AddSeconds($ObserveAuraSeconds)
            while ((Get-Date) -lt $deadline) {
                if (-not (Get-Process -Id $SupervisorId -ErrorAction SilentlyContinue)) { throw 'Supervisor exited during Aura observation.' }
                $observation = & (Join-Path $PSScriptRoot 'Read-ServiceCapabilities.ps1') -TimeoutSeconds 5 | ConvertFrom-Json
                $observations += $observation
                if ($observation.ContainsProbeName) { break }
                Start-Sleep -Seconds 2
            }
        }
    } catch { $failure=$_.Exception.Message }
    finally {
        if ($taskOwned) {
            try { Stop-ScheduledTask -TaskName $taskName; Unregister-ScheduledTask -TaskName $taskName -Confirm:$false }
            catch { $cleanup += $_.Exception.Message }
        }
        foreach ($item in @(@($categoryOwned,$categoryPath),@($classOwned,$classPath),@($appOwned,$appPath))) {
            if ($item[0]) { try { $machine.DeleteSubKeyTree($item[1],$false) } catch { $cleanup += $_.Exception.Message } }
        }
        $keysAbsent=$true
        if ($InteractiveIdentity) {
            foreach ($path in @($categoryPath,$classPath,$appPath)) {
                $remaining=$machine.OpenSubKey($path)
                if ($remaining) { $keysAbsent=$false; $remaining.Dispose() }
            }
        }
        $machine.Dispose()
        Write-Result @{Failure=$failure; CleanupErrors=$cleanup; RegistryKeysAbsent=$keysAbsent; RegistrationInfo=$registrationInfo; Observations=$observations; TaskRemoved=(-not (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue))} $adminPath
    }
    exit
}

[IO.Directory]::CreateDirectory($run) | Out-Null
$ready=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,"Local\PulseDeck.AuraProbe.Ready.$RunId")
$stop=[Threading.EventWaitHandle]::new($false,[Threading.EventResetMode]::ManualReset,"Local\PulseDeck.AuraProbe.Stop.$RunId")
$server=$null; $admin=$null
try {
    $adminArguments='-NoProfile -NonInteractive -WindowStyle Hidden -File "'+$PSCommandPath+'" -Phase Admin -RunId '+$RunId+' -SupervisorId '+$PID+' -RunRoot "'+$RunRoot+'"'
    if ($InteractiveIdentity) { $adminArguments += ' -InteractiveIdentity' }
    if ($WithSdk) { $adminArguments += ' -WithSdk' }
    if ($ObserveAuraSeconds) { $adminArguments += ' -ObserveAuraSeconds '+$ObserveAuraSeconds }
    $admin=Start-Process -FilePath "$PSHOME\powershell.exe" -Verb RunAs -ArgumentList $adminArguments -PassThru
    $deadline=(Get-Date).AddSeconds(45)
    while (-not (Test-Path -LiteralPath (Join-Path $run 'admin-ready'))) {
        if ($admin.HasExited -or (Get-Date) -ge $deadline) { throw 'Elevated setup did not become ready.' }
        Start-Sleep -Milliseconds 100
    }
    $server=Start-Native "--com-server $PID $RunId 120"
    $serverOut=$server.StandardOutput.ReadToEndAsync(); $serverErr=$server.StandardError.ReadToEndAsync()
    if (-not $ready.WaitOne(5000)) { throw 'Interactive COM server did not become ready.' }
    [IO.File]::WriteAllText((Join-Path $run 'server-ready'),'ready')
    $deadline=(Get-Date).AddSeconds(95)
    $announced=$false
    while (-not $admin.WaitForExit(200)) {
        if (-not $announced -and (Test-Path -LiteralPath (Join-Path $run 'aura-published'))) {
            Write-Output 'Temporary Aura category published; observing the running service.'
            $announced=$true
        }
        if ((Get-Date) -ge $deadline) { throw 'Elevated supervisor did not complete; inspect its cleanup result.' }
    }
    $adminResult=Get-Content -LiteralPath $adminPath -Raw | ConvertFrom-Json
    if ($adminResult.Failure -or $adminResult.CleanupErrors.Count -or -not $adminResult.TaskRemoved -or -not $adminResult.RegistryKeysAbsent) { throw ($adminResult | ConvertTo-Json -Compress) }
    $client=Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    [PSCustomObject]@{
        Scope='SYSTEM client and optional passive Aura discovery'
        InteractiveIdentity=$InteractiveIdentity.IsPresent; Client=$client
        ObservationCount=@($adminResult.Observations).Count
        RegistrationInfo=$adminResult.RegistrationInfo
        AuraContainsProbe=(@($adminResult.Observations | Where-Object ContainsProbeName).Count -gt 0)
        TaskRemoved=$adminResult.TaskRemoved; RegistryKeysAbsent=$adminResult.RegistryKeysAbsent
        AuditDirectory=$run
    } | ConvertTo-Json -Depth 6
} finally {
    $stop.Set() | Out-Null
    if ($server) {
        if (-not $server.WaitForExit(2000)) { $server.Kill(); $server.WaitForExit() }
        if ($serverOut.Result) { Write-Output $serverOut.Result.Trim() }
        if ($serverErr.Result) { Write-Host $serverErr.Result }
        $server.Dispose()
    }
    if ($admin) { $admin.Dispose() } # Do not kill it while its cleanup may be pending.
    $ready.Dispose(); $stop.Dispose()
}
