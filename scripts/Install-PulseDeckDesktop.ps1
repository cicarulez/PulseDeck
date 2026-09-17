param([string]$Source = (Join-Path $PSScriptRoot 'PulseDeck-win32-x64'))
$ErrorActionPreference = 'Stop'
$root = Join-Path $env:LOCALAPPDATA 'PulseDeck'
$destination = Join-Path $root 'desktop-app'
if (-not (Test-Path -LiteralPath (Join-Path $Source 'PulseDeck.exe'))) { throw 'Desktop package missing.' }
# Only the desktop shell is updated. The separate agent installation is untouched.
$processes = @(Get-Process PulseDeck -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $destination 'PulseDeck.exe') })
foreach ($process in $processes) { if ($process.MainWindowHandle -ne 0) { $null = $process.CloseMainWindow() } }
foreach ($process in $processes) { if (-not $process.WaitForExit(15000)) { throw 'Close the PulseDeck configurator before updating it.' } }
if (Test-Path -LiteralPath $destination) {
    $backup = Join-Path $root ('before-desktop-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    Move-Item -LiteralPath $destination -Destination $backup
}
try {
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item (Join-Path $Source '*') $destination -Recurse -Force
} catch {
    if ($backup) {
        Remove-Item -LiteralPath $destination -Recurse -Force
        Move-Item -LiteralPath $backup -Destination $destination
    }
    throw
}
$shell = New-Object -ComObject WScript.Shell
$linkPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'PulseDeck.lnk'
$link = $shell.CreateShortcut($linkPath)
$link.TargetPath = Join-Path $destination 'PulseDeck.exe'
$link.WorkingDirectory = $destination
$link.Description = 'Configuratore PulseDeck'
$link.Save()
Write-Output "Installed desktop: $destination"
Write-Output "Start menu shortcut: $linkPath"
