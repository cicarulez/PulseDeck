# Own GUIDs only; never restarts ASUS or changes global DCOM permissions.
param(
    [ValidateSet('Install','Remove','Status','Client')][string]$Action = 'Status',
    [string]$ResultPath
)
$ErrorActionPreference='Stop'
$folder=Join-Path $env:ProgramW6432 'PulseDeck Aura Probe'
$guid='{702D21B6-3A25-4D2C-9F73-F64C78E212A8}'
$classPath='SOFTWARE\Classes\CLSID\'+$guid
$appPath='SOFTWARE\Classes\AppID\{2BD83B9F-96A6-49EC-88F6-26C4E2975677}'
$categoryPath='SOFTWARE\Classes\CLSID\{109DC3E4-B9FF-4AF3-9008-AB13705D4E5F}\Instance\{E9BBD754-6CF4-492E-BA89-782177A2771B}\Instance\'+$guid
$server=Join-Path $folder 'PulseDeck.AuraHal.exe'
$manifestPath=Join-Path $folder 'installation.json'
$files=@('PulseDeck.AuraHal.exe','PulseDeck.AuraProbe.exe','Test-HalDiscovery.ps1','Manage-InstalledProbe.ps1')
function Save-Json($Value,[string]$Path) {
    [IO.File]::WriteAllText($Path+'.tmp',($Value | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath ($Path+'.tmp') -Destination $Path -Force
}
function Run-Native([string]$Arguments) {
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $folder 'PulseDeck.AuraProbe.exe'),$Arguments)
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $process=[Diagnostics.Process]::Start($start)
    try {
        $out=$process.StandardOutput.ReadToEndAsync(); $err=$process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(10000)) { throw 'Native check timed out.' }
        if ($process.ExitCode) { throw ('Native check failed: '+$out.Result+' '+$err.Result) }
        $out.Result | ConvertFrom-Json
    } finally {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
}
if ($Action -eq 'Client') {
    try {
        $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
        if (-not $identity.IsSystem) { throw 'Client requires the temporary SYSTEM task.' }
        $before=@(Get-Process -Name PulseDeck.AuraHal -ErrorAction SilentlyContinue | Where-Object SessionId -eq 0).Count
        if ($before) { throw 'SYSTEM HAL already running: cannot prove on-demand activation.' }
        $direct=Run-Native '--remote-contracts'
        $sdk=& (Join-Path $folder 'Test-HalDiscovery.ps1') -RemoteServer -TimeoutSeconds 10
        # The x86 server's SYSTEM profile is redirected to SysWOW64.
        $systemLocal=Join-Path $env:windir 'SysWOW64\config\systemprofile\AppData\Local'
        $diagnostics=Join-Path $systemLocal 'PulseDeck\aura-installed-probe\session-0.json'
        Start-Sleep -Seconds 1
        $report=Get-Content -LiteralPath $diagnostics -Raw | ConvertFrom-Json
        if ($report.activations -lt 2 -or $report.session -ne 0) { throw 'Missing SYSTEM server activation evidence.' }
        $deadline=(Get-Date).AddSeconds(25)
        while (Get-Process -Id $report.pid -ErrorAction SilentlyContinue) {
            if ((Get-Date) -ge $deadline) { throw 'On-demand server did not exit after client release.' }
            Start-Sleep -Milliseconds 200
        }
        $final=Get-Content -LiteralPath $diagnostics -Raw | ConvertFrom-Json
        if ($final.state -ne 'idle-exit') { throw 'Missing clean idle exit.' }
        Save-Json @{IsSystem=$true; Session=(Get-Process -Id $PID).SessionId; Direct=$direct; Sdk=$sdk; Report=$final; Diagnostics=$diagnostics; IdleExit=$true} $ResultPath
    } catch { Save-Json @{Failure=$_.Exception.Message} $ResultPath; exit 1 }
    exit
}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$principal=[Security.Principal.WindowsPrincipal]::new($identity)
if ($Action -ne 'Status' -and -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run Install/Remove from an elevated PowerShell.'
}
$machine=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry32)
function Has-Key([string]$Path) {
    $key=$machine.OpenSubKey($Path)
    if (-not $key) { return $false }; $key.Dispose(); return $true
}
function Remove-OwnedInstallation {
    # Refuse unrelated registry replacements; the admin-protected manifest proves ownership.
    $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.Guid -ne $guid -or $manifest.Directory -ne $folder) { throw 'Invalid installation manifest.' }
    foreach ($path in @($categoryPath,$classPath)) {
        $key=$machine.OpenSubKey($path)
        if ($key) {
            try { if ($key.GetValue('PulseDeckOwner') -ne $manifest.Owner) { throw 'Registration ownership changed; refusing removal.' } }
            finally { $key.Dispose() }
        }
    }
    foreach ($path in @($categoryPath,$classPath)) { $machine.DeleteSubKeyTree($path,$false) }
    [IO.File]::WriteAllText((Join-Path $folder 'uninstall.stop'),'stop own HAL')
    $deadline=(Get-Date).AddSeconds(10)
    do {
        $running=@(Get-CimInstance Win32_Process -Filter "Name='PulseDeck.AuraHal.exe'" | Where-Object ExecutablePath -eq $server)
        if (-not $running.Count) { break }
        if ((Get-Date) -ge $deadline) { throw 'Own HAL still running; registrations removed, files retained for retry.' }
        Start-Sleep -Milliseconds 200
    } while ($true)
    # Never recursively remove unexpected user files.
    foreach ($file in $files + @('installation.json','uninstall.stop')) {
        $path=Join-Path $folder $file
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
    if (@(Get-ChildItem -LiteralPath $folder -Force).Count -eq 0) { [IO.Directory]::Delete($folder) }
}
try {
    if ($Action -eq 'Status') {
        $reports=@(); $unreadable=@()
        $roots=@((Join-Path $env:LOCALAPPDATA 'PulseDeck\aura-installed-probe'),
            (Join-Path $env:windir 'SysWOW64\config\systemprofile\AppData\Local\PulseDeck\aura-installed-probe'))
        foreach ($root in $roots) {
            try {
                foreach ($file in @(Get-ChildItem -LiteralPath $root -Filter 'session-*.json' -ErrorAction Stop)) {
                    $reports += @{Path=$file.FullName; Report=(Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json)}
                }
            } catch { $unreadable += $root }
        }
        @{Installed=(Test-Path -LiteralPath $manifestPath); ClassPresent=(Has-Key $classPath); CategoryPresent=(Has-Key $categoryPath); Directory=$folder; Reports=$reports; UnreadableOrAbsent=$unreadable} | ConvertTo-Json -Depth 6
    } elseif ($Action -eq 'Remove') {
        Remove-OwnedInstallation
        @{Removed=$true; ClassPresent=(Has-Key $classPath); CategoryPresent=(Has-Key $categoryPath)} | ConvertTo-Json
    } else {
        if (Test-Path -LiteralPath $folder) { throw 'Installation directory already exists; refusing replacement.' }
        foreach ($path in @($classPath,$appPath,$categoryPath)) { if (Has-Key $path) { throw 'Probe registration already exists; refusing replacement.' } }
        if (Get-Process -Name PulseDeck.AuraHal,PulseDeck.AuraProbe -ErrorAction SilentlyContinue) { throw 'Close existing probe processes before installation.' }
        foreach ($file in $files) { if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $file))) { throw "Missing package file: $file" } }
        $run=Join-Path $env:LOCALAPPDATA ('PulseDeck\aura-investigation\installed-runs\'+[Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($run) | Out-Null
        $task='PulseDeck-AuraProbe-'+[Guid]::NewGuid().ToString('N')
        $taskOwned=$false; $owned=$false
        try {
            [IO.Directory]::CreateDirectory($folder) | Out-Null
            # Explicit ACL: SYSTEM/admin full control; users execute/read only.
            $acl=[Security.AccessControl.DirectorySecurity]::new()
            $acl.SetAccessRuleProtection($true,$false)
            $acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
            foreach ($entry in @(@('S-1-5-18','FullControl'),@('S-1-5-32-544','FullControl'),@('S-1-5-32-545','ReadAndExecute'))) {
                $rule=[Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($entry[0]),$entry[1],'ContainerInherit,ObjectInherit','None','Allow')
                $acl.AddAccessRule($rule)
            }
            Set-Acl -LiteralPath $folder -AclObject $acl
            $owner=[Guid]::NewGuid().ToString('N')
            Save-Json @{Guid=$guid; Owner=$owner; Directory=$folder; Experimental=$true} $manifestPath
            $owned=$true
            foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $folder }
            $hashes=@{}; foreach ($file in $files) { $hashes[$file]=(Get-FileHash -LiteralPath (Join-Path $folder $file) -Algorithm SHA256).Hash }
            Save-Json @{Guid=$guid; Owner=$owner; Directory=$folder; Experimental=$true; Hashes=$hashes} $manifestPath
            $key=$machine.CreateSubKey($classPath)
            try { $key.SetValue('','PulseDeck experimental passive HAL'); $key.SetValue('PulseDeckOwner',$owner) } finally { $key.Dispose() }
            $key=$machine.CreateSubKey($classPath+'\LocalServer32')
            try { $key.SetValue('','"'+$server+'"'); $key.SetValue('ServerExecutable',$server) } finally { $key.Dispose() }
            # No AppID/RunAs: COM uses the launching client's identity, including session 0.
            $clientResult=Join-Path $run 'system-client.json'
            $arguments='-NoProfile -NonInteractive -File "'+(Join-Path $folder 'Manage-InstalledProbe.ps1')+'" -Action Client -ResultPath "'+$clientResult+'"'
            $taskAction=New-ScheduledTaskAction -Execute "$PSHOME\powershell.exe" -Argument $arguments
            $account=New-ScheduledTaskPrincipal -UserId SYSTEM -LogonType ServiceAccount -RunLevel Highest
            $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Seconds 50) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
            Register-ScheduledTask -TaskName $task -Action $taskAction -Principal $account -Settings $settings | Out-Null
            $taskOwned=$true; Start-ScheduledTask -TaskName $task
            $deadline=(Get-Date).AddSeconds(45)
            while (-not (Test-Path -LiteralPath $clientResult)) {
                if ((Get-Date) -ge $deadline) { throw 'SYSTEM verification timed out.' }
                Start-Sleep -Milliseconds 200
            }
            $result=Get-Content -LiteralPath $clientResult -Raw | ConvertFrom-Json
            if ($result.Failure -or -not $result.IdleExit -or -not $result.IsSystem) { throw ('SYSTEM verification failed: '+$result.Failure) }
            # Publish only after automatic launch, SDK metadata and idle exit pass.
            $key=$machine.CreateSubKey($categoryPath)
            try {
                $key.SetValue('PulseDeckOwner',$owner); $key.SetValue('Name','PulseDeck Virtual Probe')
                $key.SetValue('Manufacturer','PulseDeck'); $key.SetValue('Version','0.4.0-probe')
                $key.SetValue('Pluging',0,[Microsoft.Win32.RegistryValueKind]::DWord)
            } finally { $key.Dispose() }
            $inventory=Run-Native '--registered-hal-info'
            if ($inventory.probeMatches -ne 1) { throw 'SDK registration inventory did not match.' }
            $summary=@{Installed=$true; Directory=$folder; AuditDirectory=$run; SystemTest=$result; Inventory=$inventory}
            Save-Json $summary (Join-Path $run 'installation-result.json')
            $summary | ConvertTo-Json -Depth 8
        } catch {
            if ($owned) { Remove-OwnedInstallation }
            throw
        } finally {
            if ($taskOwned) { Stop-ScheduledTask -TaskName $task; Unregister-ScheduledTask -TaskName $task -Confirm:$false }
        }
    }
} finally { $machine.Dispose() }
