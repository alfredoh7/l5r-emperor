#!/usr/bin/env python3
"""Install optional offline scan receipts into the viewer's content-addressed cache.

The source directory may be a checkout or extracted private scan release. Every
file is hash-verified; no scan binaries or machine-specific paths enter Git.
"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path


def install(root, source, cache):
    cache.mkdir(parents=True, exist_ok=True)
    copied = missing = 0
    for manifest in sorted((root / 'content/oracle').glob('*/artwork-manifest.json')):
        data = json.loads(manifest.read_text())
        for scan in data.get('images', data.get('downloaded', [])):
            path = (source / scan['path']).resolve()
            path.relative_to(source.resolve())
            if not path.is_file():
                missing += 1
                continue
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            if digest != scan['sha256']:
                raise ValueError('Scan differs from receipt: ' + str(path))
            destination = cache / (digest + '.jpg')
            if not destination.exists():
                shutil.copyfile(path, destination)
                copied += 1
    return {'installed': copied, 'source_scans_unavailable': missing}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--cache', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(install(Path(__file__).resolve().parents[2], args.source, args.cache), indent=2))
