$ErrorActionPreference = 'Stop'
$repoDir = Split-Path $PSScriptRoot -Parent
Push-Location (Join-Path $repoDir 'apps/configurator')
try {
    npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
    npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'Angular build failed' }
} finally { Pop-Location }
Push-Location $repoDir
try {
    dotnet test tests/PulseDeck.Core.Tests/PulseDeck.Core.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
    dotnet test tests/PulseDeck.Rendering.Tests/PulseDeck.Rendering.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Rendering tests failed' }
    dotnet publish apps/agent/PulseDeck.Agent.csproj -c Release -r win-x64 --self-contained true -o artifacts/windows
    if ($LASTEXITCODE -ne 0) { throw 'Agent publish failed' }
    New-Item -ItemType Directory -Force artifacts/windows/wwwroot | Out-Null
    Copy-Item artifacts/configurator/browser/* artifacts/windows/wwwroot -Recurse -Force
    Copy-Item scripts/Start-PulseDeck.ps1,scripts/Start-PulseDeck.cmd,scripts/Import-Discord.ps1,scripts/Start-PulseDeck.Background.ps1,scripts/Install-PulseDeckStartup.ps1,LICENSE,THIRD-PARTY-NOTICES.md artifacts/windows -Force
} finally { Pop-Location }
