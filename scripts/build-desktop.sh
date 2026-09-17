#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir/apps/desktop"
ELECTRON_SKIP_BINARY_DOWNLOAD=1 npm ci
npm test
npm run package:win
cp "$repo_dir/scripts/Install-PulseDeckDesktop.ps1" "$repo_dir/artifacts/desktop/"
cp "$repo_dir/LICENSE" "$repo_dir/artifacts/desktop/PulseDeck-win32-x64/LICENSE.PulseDeck"
cp "$repo_dir/THIRD-PARTY-NOTICES.md" "$repo_dir/artifacts/desktop/PulseDeck-win32-x64/"
