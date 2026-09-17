#!/usr/bin/env python3
"""Package pinned Windows gaming dependencies; no installers or services are run."""
import hashlib
import io
import json
from pathlib import Path
import sys
import urllib.request
import zipfile

output = Path(sys.argv[1]).resolve()
manifest = json.loads(Path(__file__).with_name('gaming-dependencies.json').read_text())
for item in manifest:
    data = urllib.request.urlopen(item['url'], timeout=60).read()
    if hashlib.sha256(data).hexdigest() != item['sha256']:
        raise SystemExit('Dependency checksum mismatch: ' + item['url'])
    for file in item['files']:
        source, target = file['source'], file['target']
        destination = output / target
        destination.parent.mkdir(parents=True, exist_ok=True)
        if source:
            with zipfile.ZipFile(io.BytesIO(data)) as archive:
                destination.write_bytes(archive.read(source))
        else:
            destination.write_bytes(data)
print('PresentMon and Discord Voice dependencies verified and packaged.')
