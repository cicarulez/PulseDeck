#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir/apps/configurator"
npm ci
npm run build
cd "$repo_dir"
dotnet test tests/PulseDeck.Core.Tests/PulseDeck.Core.Tests.csproj -c Release
dotnet test tests/PulseDeck.Rendering.Tests/PulseDeck.Rendering.Tests.csproj -c Release
dotnet publish apps/agent/PulseDeck.Agent.csproj -c Release -r win-x64 --self-contained true -o artifacts/windows
mkdir -p artifacts/windows/wwwroot
cp -a artifacts/configurator/browser/. artifacts/windows/wwwroot/
cp scripts/Start-PulseDeck.ps1 artifacts/windows/
cp scripts/Start-PulseDeck.cmd artifacts/windows/
cp scripts/Import-Discord.ps1 artifacts/windows/
cp scripts/Start-PulseDeck.Background.ps1 scripts/Install-PulseDeckStartup.ps1 artifacts/windows/
cp LICENSE THIRD-PARTY-NOTICES.md artifacts/windows/
