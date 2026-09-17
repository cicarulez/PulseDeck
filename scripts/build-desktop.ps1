$ErrorActionPreference = 'Stop'
$repoDir = Split-Path $PSScriptRoot -Parent
Push-Location (Join-Path $repoDir 'apps/desktop')
try {
    npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw 'Desktop dependencies failed' }
    npm.cmd test
    if ($LASTEXITCODE -ne 0) { throw 'Desktop tests failed' }
    npm.cmd run package:win
    if ($LASTEXITCODE -ne 0) { throw 'Desktop packaging failed' }
    Copy-Item (Join-Path $PSScriptRoot 'Install-PulseDeckDesktop.ps1') (Join-Path $repoDir 'artifacts/desktop') -Force
    Copy-Item (Join-Path $repoDir 'LICENSE') (Join-Path $repoDir 'artifacts/desktop/PulseDeck-win32-x64/LICENSE.PulseDeck') -Force
    Copy-Item (Join-Path $repoDir 'THIRD-PARTY-NOTICES.md') (Join-Path $repoDir 'artifacts/desktop/PulseDeck-win32-x64') -Force
} finally { Pop-Location }
