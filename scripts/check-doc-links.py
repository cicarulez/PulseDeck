#!/usr/bin/env python3
"""Check local Markdown/image targets without fetching external services."""
import re
from pathlib import Path
from urllib.parse import unquote, urlsplit

root = Path(__file__).resolve().parent.parent
files = [*root.glob('*.md'), *root.joinpath('docs').rglob('*.md'),
         *root.joinpath('tools/docs-gallery').glob('*.md')]
errors = []
for file in files:
    text = re.sub(r'```.*?```', '', file.read_text(encoding='utf-8'), flags=re.S)
    targets = re.findall(r'!?\[[^\]]*\]\(([^)]+)\)', text)
    targets += re.findall(r'(?:src|href)="([^"]+)"', text)
    for target in targets:
        target = target.strip().split(' "', 1)[0].strip('<>')
        parsed = urlsplit(target)
        if parsed.scheme or target.startswith(('#', '//')):
            continue
        path = unquote(parsed.path)
        if path and not (file.parent / path).exists():
            errors.append(f'{file.relative_to(root)}: missing {target}')
if errors:
    raise SystemExit('\n'.join(errors))
print(f'Local link targets checked in {len(files)} Markdown files. External URLs/anchors are not checked.')
