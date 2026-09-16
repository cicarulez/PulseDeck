param(
    [string]$TurzxTaskName = 'TempMonitor_8',
    [string]$TurzxTaskPath = '\'
)
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this setup as administrator, under the Windows user who will use PulseDeck.'
}
Import-Module "$PSHOME\Modules\ScheduledTasks\ScheduledTasks.psd1"
$launcher = Join-Path $PSScriptRoot 'Start-PulseDeck.Background.ps1'
if (-not (Test-Path $launcher) -or -not (Test-Path (Join-Path $PSScriptRoot 'PulseDeck.Agent.exe'))) {
    throw 'Run setup from the complete published Windows app folder.'
}
$dataDir = if ($env:PULSEDECK_DATA_DIR) { $env:PULSEDECK_DATA_DIR } else { Join-Path $env:LOCALAPPDATA 'PulseDeck' }
$backupDir = Join-Path $dataDir ('startup-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
$old = Get-ScheduledTask -TaskName $TurzxTaskName -TaskPath $TurzxTaskPath -ErrorAction SilentlyContinue
if ($old -and -not (@($old.Actions | Where-Object { $_.Execute -match '(?i)(^|[\\/])TURZX\.exe"?$' }).Count)) {
    throw 'The specified legacy task does not start TURZX; no tasks were changed.'
}
$previous = Get-ScheduledTask -TaskName 'PulseDeck' -TaskPath '\' -ErrorAction SilentlyContinue
if ($previous -and -not (@($previous.Actions | Where-Object { $_.Arguments -like '*Start-PulseDeck.Background.ps1*' }).Count)) {
    throw 'A different task already uses the name PulseDeck; no tasks were changed.'
}
$oldXml = if ($old) { Export-ScheduledTask -TaskName $old.TaskName -TaskPath $old.TaskPath } else { $null }
$previousXml = if ($previous) { Export-ScheduledTask -TaskName 'PulseDeck' -TaskPath '\' } else { $null }
if ($oldXml) { $oldXml | Set-Content (Join-Path $backupDir 'TURZX.xml') -Encoding Unicode }
if ($previousXml) { $previousXml | Set-Content (Join-Path $backupDir 'PulseDeck.xml') -Encoding Unicode }
$wasEnabled = $old -and $old.Settings.Enabled
$registered = $false
try {
    $action = New-ScheduledTaskAction -Execute "$PSHOME\powershell.exe" `
        -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$launcher`"" `
        -WorkingDirectory $PSScriptRoot
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $identity.Name
    $trigger.Delay = 'PT15S'
    $runAs = New-ScheduledTaskPrincipal -UserId $identity.Name -LogonType Interactive -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero) `
        -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
    Register-ScheduledTask -TaskName 'PulseDeck' -TaskPath '\' -Action $action -Trigger $trigger `
        -Principal $runAs -Settings $settings -Description 'PulseDeck: hardware, media and Discord display in the signed-in user session.' -Force | Out-Null
    $registered = $true
    $installed = Get-ScheduledTask -TaskName 'PulseDeck' -TaskPath '\'
    if (-not $installed.Settings.Enabled -or $installed.Principal.RunLevel -ne 'Highest' -or $installed.Principal.LogonType -ne 'Interactive') {
        throw 'Scheduled task verification failed.'
    }
    if ($old) { Disable-ScheduledTask -TaskName $old.TaskName -TaskPath $old.TaskPath | Out-Null }
    [pscustomobject]@{Task='PulseDeck';User=$identity.Name;LegacyTask=$TurzxTaskName;LegacyDisabled=[bool]$old;Backup=$backupDir} |
        ConvertTo-Json | Set-Content (Join-Path $dataDir 'startup-setup.json') -Encoding UTF8
} catch {
    if ($registered) {
        if ($previousXml) { Register-ScheduledTask -TaskName 'PulseDeck' -TaskPath '\' -Xml $previousXml -Force | Out-Null }
        else { Unregister-ScheduledTask -TaskName 'PulseDeck' -TaskPath '\' -Confirm:$false }
    }
    if ($wasEnabled) { Enable-ScheduledTask -TaskName $old.TaskName -TaskPath $old.TaskPath | Out-Null }
    throw
}
Write-Output "PulseDeck startup installed for $($identity.Name). Backups: $backupDir"
