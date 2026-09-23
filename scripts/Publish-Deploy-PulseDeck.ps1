param(
    [string]$InstallPath,
    [string]$Version,
    [switch]$SkipVersionPrompt
)

$ErrorActionPreference = 'Stop'
$repoDir = Split-Path $PSScriptRoot -Parent
$projectFile = Join-Path $repoDir 'apps/agent/PulseDeck.Agent.csproj'
$packageFile = Join-Path $repoDir 'apps/configurator/package.json'
$lockFile = Join-Path $repoDir 'apps/configurator/package-lock.json'
$publishedDir = Join-Path $repoDir 'artifacts/windows'
$taskName = 'PulseDeck'
$healthUrl = 'http://127.0.0.1:5178/api/health'
$wslCheckout = [regex]::Match($repoDir, '^\\\\wsl(?:\.localhost|\$)\\(?<distro>[^\\]+)\\(?<relative>.+)$', [Text.RegularExpressions.RegexOptions]::IgnoreCase)

function Fail([string]$message) { throw $message }

function Read-AgentVersion([string]$path) {
    $content = Get-Content -LiteralPath $path -Raw
    $match = [regex]::Match($content, '<Version>(?<version>[^<]+)</Version>')
    if (-not $match.Success) { Fail "Versione non trovata in $path." }
    return $match.Groups['version'].Value.Trim()
}

function Test-Version([string]$value) {
    return $value -match '^\d+\.\d+\.\d+$'
}

function Get-InstalledVersion {
    try {
        return (Invoke-RestMethod -Uri $healthUrl -TimeoutSec 2).version
    } catch { }

    $installedAgent = Join-Path $InstallPath 'PulseDeck.Agent.exe'
    if (Test-Path -LiteralPath $installedAgent) {
        return [Diagnostics.FileVersionInfo]::GetVersionInfo($installedAgent).ProductVersion
    }
    return $null
}

function Get-TaskInstallPath($task) {
    foreach ($action in @($task.Actions)) {
        $text = "$($action.Execute) $($action.Arguments) $($action.WorkingDirectory)"
        $match = [regex]::Match($text, '(?<launcher>[A-Za-z]:\\[^"\r\n]*Start-PulseDeck\.Background\.ps1)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($match.Success) { return Split-Path $match.Groups['launcher'].Value -Parent }
    }
    return $null
}

function Set-Version([string]$value) {
    $current = Read-AgentVersion $projectFile
    if ($value -eq $current) { return }
    $projectContent = Get-Content -LiteralPath $projectFile -Raw
    $updatedProject = [regex]::new('<Version>[^<]+</Version>').Replace($projectContent, "<Version>$value</Version>", 1)
    if ($updatedProject -eq $projectContent) { Fail "Impossibile aggiornare la versione in $projectFile." }
    $packageContent = Get-Content -LiteralPath $packageFile -Raw
    $lockContent = Get-Content -LiteralPath $lockFile -Raw
    $replacement = '$1"' + $value + '"'
    $versionPattern = [regex]::new('("version"\s*:\s*)"[^"]+"')
    $updatedPackage = $versionPattern.Replace($packageContent, $replacement, 1)
    $updatedLock = $versionPattern.Replace($lockContent, $replacement, 2)
    if ($updatedPackage -eq $packageContent -or $updatedLock -eq $lockContent) {
        Fail 'Impossibile aggiornare package.json e package-lock.json.'
    }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($projectFile, $updatedProject, $utf8)
    [IO.File]::WriteAllText($packageFile, $updatedPackage, $utf8)
    [IO.File]::WriteAllText($lockFile, $updatedLock, $utf8)
    $package = Get-Content -LiteralPath $packageFile -Raw | ConvertFrom-Json
    $lockVersions = [regex]::Matches($updatedLock, '("version"\s*:\s*)"(?<version>[^"]+)"')
    if ($package.version -ne $value -or $lockVersions.Count -lt 2 -or
        $lockVersions[0].Groups['version'].Value -ne $value -or
        $lockVersions[1].Groups['version'].Value -ne $value) {
        Fail 'Le versioni del configuratore e del lockfile non coincidono.'
    }
}

function Wait-AgentStopped([int]$timeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    do {
        $processes = @(Get-Process -Name 'PulseDeck.Agent' -ErrorAction SilentlyContinue)
        if ($processes.Count -eq 0) { return }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    Fail 'PulseDeck.Agent non si è arrestato entro il tempo previsto; deployment annullato.'
}

function Wait-TaskStopped([int]$timeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    do {
        if ((Get-ScheduledTask -TaskName $taskName -TaskPath '\').State -ne 'Running') { return }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    Fail "L’attività '$taskName' è ancora in esecuzione; aggiornamento annullato."
}

function Wait-AgentReady([string]$expectedVersion, [int]$timeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    do {
        try {
            $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 2
            if ($health.ok -and $health.app -eq 'PulseDeck' -and
                (-not $expectedVersion -or $health.version -eq $expectedVersion)) { return $health }
        } catch { }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    Fail "PulseDeck $expectedVersion non è diventato disponibile dopo l'avvio dell'attività."
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Fail 'Apri PowerShell come amministratore e rilancia lo script.'
}
if (-not (Test-Path -LiteralPath $projectFile) -or -not (Test-Path -LiteralPath $packageFile) -or -not (Test-Path -LiteralPath $lockFile)) {
    Fail "Il repository PulseDeck non è stato trovato in $repoDir. Esegui lo script dalla copia Windows del repository."
}

$task = Get-ScheduledTask -TaskName $taskName -TaskPath ([string][char]92) -ErrorAction SilentlyContinue
if ([string]::IsNullOrWhiteSpace($InstallPath)) {
    $detectedInstallPath = $null
    if ($task) {
        $detectedInstallPath = Get-TaskInstallPath $task
    }
    if (-not [string]::IsNullOrWhiteSpace($detectedInstallPath)) {
        $InstallPath = $detectedInstallPath
        Write-Host "Cartella installazione rilevata dall’attività: $InstallPath"
    } else {
        $InstallPath = Read-Host "Cartella dell'installazione Windows di PulseDeck"
    }
}
$InstallPath = [IO.Path]::GetFullPath($InstallPath)
if ($InstallPath -eq [IO.Path]::GetPathRoot($InstallPath)) { Fail 'La cartella di installazione non può essere la radice di un disco.' }
$InstallPath = $InstallPath.TrimEnd('\')
if (-not (Test-Path -LiteralPath (Join-Path $InstallPath 'PulseDeck.Agent.exe'))) {
    Fail "L'installazione esistente non è stata trovata in $InstallPath."
}
if (-not $task) { Fail "L’attività pianificata '$taskName' non esiste; installala prima con Install-PulseDeckStartup.ps1." }
$taskInstallPath = Get-TaskInstallPath $task
if (-not $taskInstallPath -or -not [IO.Path]::GetFullPath($taskInstallPath).Equals($InstallPath, [StringComparison]::OrdinalIgnoreCase)) {
    Fail "L’attività '$taskName' non punta a $InstallPath. Verifica la cartella o aggiorna l’attività con Install-PulseDeckStartup.ps1."
}
if ($task.Principal.LogonType -ne 'Interactive' -or $task.Principal.RunLevel -ne 'Highest' -or
    $task.Principal.UserId.Split('\')[-1] -ne $identity.Name.Split('\')[-1]) {
    Fail "L’attività '$taskName' deve appartenere all’utente Windows corrente ed eseguirsi come attività interattiva elevata."
}

if (-not $wslCheckout.Success) {
    $sdkList = & dotnet --list-sdks 2>$null
    if ($LASTEXITCODE -ne 0 -or -not ($sdkList -match '(^|\s)10\.0\.')) {
        Fail 'Serve il .NET SDK 10.x per questa build. Installa .NET SDK 10.0.100 o una feature band 10.0 compatibile, poi rilancia lo script.'
    }
    try {
        $nodeVersion = (& node --version).Trim().TrimStart('v')
        if ($LASTEXITCODE -ne 0) { throw }
        if ([version]$nodeVersion -lt [version]'24.15.0') { throw }
    } catch { Fail 'Node.js 24.15+ è necessario per compilare il configuratore.' }
    Write-Host "SDK .NET disponibile; Node.js $nodeVersion."
} else {
    Write-Host "Repository WSL rilevato: $($wslCheckout.Groups['distro'].Value). Build eseguita in WSL."
}

$sourceVersion = Read-AgentVersion $projectFile
$installedVersion = Get-InstalledVersion
if (-not $installedVersion) { $installedVersion = 'non rilevata' }
Write-Host "Versione nel sorgente: $sourceVersion"
Write-Host "Versione installata:   $installedVersion"

if (-not $SkipVersionPrompt -and [string]::IsNullOrWhiteSpace($Version)) {
    $Version = Read-Host "Nuova versione (Invio per mantenere $sourceVersion)"
}
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $requestedVersion = $Version.Trim()
    if (-not [string]::IsNullOrWhiteSpace($requestedVersion)) {
        if (-not (Test-Version $requestedVersion)) { Fail 'La versione deve avere il formato MAJOR.MINOR.PATCH, per esempio 0.8.1.' }
        Set-Version $requestedVersion
        $sourceVersion = $requestedVersion
        Write-Host "Versione aggiornata a $sourceVersion nel progetto e nel configuratore."
    }
}
if (-not (Test-Version $sourceVersion)) { Fail "La versione sorgente '$sourceVersion' non ha il formato MAJOR.MINOR.PATCH." }

if (Test-Path -LiteralPath $publishedDir) {
    Remove-Item -LiteralPath $publishedDir -Recurse -Force
}
Write-Host 'Build, test e publish in corso...'
if ($wslCheckout.Success) {
    $linuxRepo = '/' + $wslCheckout.Groups['relative'].Value.Replace('\', '/')
    $linuxBuild = 'if [ -x "$HOME/.dotnet-pulsedeck/dotnet" ]; then export PATH="$HOME/.dotnet-pulsedeck:$PATH"; fi; if [ -s "$HOME/.nvm/nvm.sh" ]; then . "$HOME/.nvm/nvm.sh"; nvm use 24 >/dev/null 2>&1 || true; fi; ./scripts/build.sh'
    & wsl.exe -d $wslCheckout.Groups['distro'].Value --cd $linuxRepo -- bash -lc $linuxBuild
} else {
    & (Join-Path $repoDir 'scripts/build.ps1')
}
if ($LASTEXITCODE -ne 0) { Fail "La build completa non è riuscita; l'installazione in uso non è stata modificata." }
if (-not (Test-Path -LiteralPath (Join-Path $publishedDir 'PulseDeck.Agent.exe'))) {
    Fail 'La publish non ha prodotto PulseDeck.Agent.exe.'
}

$dataDir = Join-Path $env:LOCALAPPDATA 'PulseDeck'
if ($env:PULSEDECK_DATA_DIR) {
    $dataDir = $env:PULSEDECK_DATA_DIR
}
$backupDir = Join-Path $dataDir ('startup-backups\deploy-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
Export-ScheduledTask -TaskName $taskName -TaskPath ([string][char]92) | Set-Content (Join-Path $backupDir 'PulseDeck.xml') -Encoding Unicode

$installParent = Split-Path $InstallPath -Parent
$installName = Split-Path $InstallPath -Leaf
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$stagingDir = Join-Path $installParent "$installName.pulsedeck-next-$stamp"
$previousDir = Join-Path $installParent "$installName.pulsedeck-backup-$stamp"
$failedDir = Join-Path $installParent "$installName.pulsedeck-failed-$stamp"
if ([IO.Path]::GetFullPath($publishedDir).TrimEnd('\').Equals($InstallPath, [StringComparison]::OrdinalIgnoreCase)) {
    Fail 'La cartella di installazione non può coincidere con artifacts\windows.'
}
Write-Host "Preparo la nuova installazione in $stagingDir ..."
& robocopy $publishedDir $stagingDir /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /NFL /NDL /NP
if ($LASTEXITCODE -ge 8 -or -not (Test-Path -LiteralPath (Join-Path $stagingDir 'wwwroot/index.html')) -or
    -not (Test-Path -LiteralPath (Join-Path $stagingDir 'Start-PulseDeck.Background.ps1'))) {
    Fail "Il pacchetto non è stato copiato completamente. Installazione corrente intatta; controlla $stagingDir."
}

$oldMoved = $false
$newInstalled = $false
$agentStopped = $false
$displayWasConnected = $false
try { $displayWasConnected = [bool](Invoke-RestMethod -Uri 'http://127.0.0.1:5178/api/display' -TimeoutSec 2).connected } catch { }
try {
    $agents = @(Get-Process -Name 'PulseDeck.Agent' -ErrorAction SilentlyContinue)
    foreach ($agent in $agents) {
        if (-not $agent.Path -or -not [IO.Path]::GetFullPath($agent.Path).Equals(
            (Join-Path $InstallPath 'PulseDeck.Agent.exe'), [StringComparison]::OrdinalIgnoreCase)) {
            Fail "È in esecuzione un agente PulseDeck fuori da $InstallPath; chiudilo prima dell'aggiornamento."
        }
    }
    if ($agents.Count -gt 1) { Fail 'Sono in esecuzione più agenti PulseDeck; aggiornamento annullato.' }
    if ($agents.Count -eq 1) {
        Invoke-RestMethod -Uri 'http://127.0.0.1:5178/api/stop' -Method Post `
            -Headers @{ 'X-PulseDeck-Client' = 'configurator' } -TimeoutSec 5 | Out-Null
    }
    Wait-AgentStopped
    $agentStopped = $true
    Wait-TaskStopped

    Move-Item -LiteralPath $InstallPath -Destination $previousDir
    $oldMoved = $true
    Move-Item -LiteralPath $stagingDir -Destination $InstallPath
    $newInstalled = $true
    Start-ScheduledTask -TaskName $taskName -TaskPath '\'
    $health = Wait-AgentReady $sourceVersion
    Write-Host "PulseDeck riavviato correttamente: versione $($health.version)."
    if ($displayWasConnected) {
        $displayDeadline = (Get-Date).AddMinutes(3)
        $displayConnected = $false
        do {
            try { $displayConnected = [bool](Invoke-RestMethod -Uri 'http://127.0.0.1:5178/api/display' -TimeoutSec 5).connected } catch { }
            if ($displayConnected) { break }
            Start-Sleep -Seconds 5
        } while ((Get-Date) -lt $displayDeadline)
        if ($displayConnected) { Write-Host 'Display ricollegato.' }
        else { Write-Warning 'Agent aggiornato, ma il display non si è ricollegato entro 3 minuti. Controlla la Control Room.' }
    }
    Write-Host "Installazione precedente conservata in $previousDir."
} catch {
    $deploymentError = $_
    try {
        if ($oldMoved) {
            if ($newInstalled) {
                try {
                    Invoke-RestMethod -Uri 'http://127.0.0.1:5178/api/stop' -Method Post `
                        -Headers @{ 'X-PulseDeck-Client' = 'configurator' } -TimeoutSec 5 | Out-Null
                } catch { }
                Wait-AgentStopped
                Wait-TaskStopped
                Move-Item -LiteralPath $InstallPath -Destination $failedDir
            }
            Move-Item -LiteralPath $previousDir -Destination $InstallPath
        }
        if ($agentStopped -or $oldMoved) {
            Wait-TaskStopped
            Start-ScheduledTask -TaskName $taskName -TaskPath '\'
            $oldVersion = if ($installedVersion -eq 'non rilevata') { $null } else { $installedVersion }
            Wait-AgentReady $oldVersion | Out-Null
            Write-Host 'La versione precedente è stata ripristinata e riavviata.'
        }
    } catch { Write-Warning "Ripristino automatico non riuscito: $($_.Exception.Message). Backup: $previousDir" }
    Write-Host "Backup dell’attività: $backupDir\PulseDeck.xml"
    throw $deploymentError
}
