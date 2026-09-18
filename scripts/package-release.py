#!/usr/bin/env python3
"""Package a clean committed build; no runtime data or credentials are permitted."""
import argparse
import hashlib
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--tag', required=True, help='Version label, e.g. v0.7.3')
args = parser.parse_args()
version = ET.parse(root / 'apps/agent/PulseDeck.Agent.csproj').findtext('.//Version')
if not re.fullmatch(r'v\d+\.\d+\.\d+', args.tag) or args.tag != 'v' + version:
    raise SystemExit('Tag must match the agent version.')
def git(*arguments):
    return subprocess.check_output(['git', '-C', str(root), *arguments], text=True).strip()
if git('status', '--porcelain'):
    raise SystemExit('Commit/review the working tree before packaging corresponding source.')
commit = git('rev-parse', 'HEAD')
published = root / 'artifacts/windows'
required = ['PulseDeck.Agent.exe', 'PulseDeck.Agent.dll', 'PulseDeck.Agent.deps.json',
            'wwwroot/index.html', 'tools/PresentMon.exe', 'libdave.dll', 'licenses',
            'LICENSE', 'THIRD-PARTY-NOTICES.md', 'Start-PulseDeck.cmd', 'youtube-extension/manifest.json']
for name in required:
    if not (published / name).exists():
        raise SystemExit(f'Missing package component: {name}. Run the complete build script first.')
# Publish metadata catches the common stale-version output mistake. Build from
# a clean tag in the release workflow to also ensure same-version source parity.
import json
runtime_target = json.loads((published / 'PulseDeck.Agent.deps.json').read_text())['libraries']
if f'PulseDeck.Agent/{version}' not in runtime_target:
    raise SystemExit('Published agent version does not match source.')
files = sorted(p for p in published.rglob('*') if p.is_file())
for file in files:
    name = file.name.lower()
    if ('.credentials' in name or name == '.env' or name.startswith('.env.') or name.endswith('.log')
        or name in {'config.json', 'game-library.json'} or any(part in {'game-artwork', 'lyrics', 'logs'} for part in file.relative_to(published).parts)):
        raise SystemExit(f'Runtime/private file in package: {file.relative_to(published)}')
output = root / 'artifacts/release'
output.mkdir(parents=True, exist_ok=True)
# Do not silently mix old-version artifacts into a new release upload.
if any(output.iterdir()):
    raise SystemExit('artifacts/release is not empty. Move the previous package before continuing.')
windows = output / f'PulseDeck-{args.tag}-windows-x64.zip'
with zipfile.ZipFile(windows, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for file in files:
        archive.write(file, 'PulseDeck/' + file.relative_to(published).as_posix())
    archive.writestr('PulseDeck/BUILD-INFO.txt', f'PulseDeck {args.tag}\nSource commit: {commit}\nWindows x64, self-contained .NET\n')
    archive.writestr('PulseDeck/START-HERE.txt', 'Run Start-PulseDeck.cmd from this complete folder.\nSetup: https://github.com/cicarulez/PulseDeck/blob/main/docs/getting-started.md\n')
source = output / f'PulseDeck-{args.tag}-source.zip'
subprocess.run(['git', '-C', str(root), 'archive', '--format=zip', '--prefix=PulseDeck-source/', f'--output={source}', commit], check=True)
checksums = []
for file in [windows, source]:
    with file.open("rb") as stream:
        digest = hashlib.sha256()
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    checksums.append(f"{digest.hexdigest()}  {file.name}")
(output / 'SHA256SUMS').write_text('\n'.join(checksums) + '\n')
print(f'Packaged {args.tag} from {commit[:12]} in {output}. No tag or release was created.')
