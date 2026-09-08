#!/usr/bin/env python3
"""Create a local clickable Mac app for this checkout; never fetch or switch branches."""
from pathlib import Path
import plistlib
import shlex
import sys

root = Path(__file__).resolve().parents[1]
app = root / 'artifacts' / 'L5R Emperor - Latest.app'
macos = app / 'Contents' / 'MacOS'
macos.mkdir(parents=True, exist_ok=True)
info = {
    'CFBundleName': 'L5R Emperor - Latest',
    'CFBundleDisplayName': 'L5R Emperor - Latest',
    'CFBundleIdentifier': 'local.l5r.emperor.development',
    'CFBundleExecutable': 'launch',
    'CFBundlePackageType': 'APPL',
    'CFBundleVersion': '1',
    'CFBundleShortVersionString': '1.0',
    'NSHighResolutionCapable': True,
}
(app / 'Contents' / 'Info.plist').write_bytes(plistlib.dumps(info))
launcher = macos / 'launch'
launcher.write_text("#!/bin/bash\nexec /usr/bin/open -a Terminal " + shlex.quote(str(root / 'Play Latest.command')) + "\n")
launcher.chmod(0o755)
print(app)
