#!/usr/bin/env python3
"""Create a clickable Mac launcher for one checked-in review command in this checkout."""
from pathlib import Path
import argparse
import plistlib
import re
import shlex

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('command', help='Repository-relative .command entry point')
parser.add_argument('name', help='Display name, without .app')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
command = (root / args.command).resolve()
command.relative_to(root)
if not command.is_file() or command.suffix != '.command':
    parser.error('Choose an existing .command file in this checkout.')
if '/' in args.name or not args.name.strip():
    parser.error('Use a nonempty display name without slashes.')
app = root / 'artifacts' / (args.name + '.app')
launcher = app / 'Contents/MacOS/launch'
launcher.parent.mkdir(parents=True, exist_ok=True)
(app / 'Contents/Info.plist').write_bytes(plistlib.dumps({
    'CFBundleName': args.name, 'CFBundleDisplayName': args.name,
    'CFBundleIdentifier': 'local.l5r.review.' + re.sub('[^a-z0-9]+', '-', args.name.lower()).strip('-'),
    'CFBundleExecutable': 'launch', 'CFBundlePackageType': 'APPL',
    'CFBundleVersion': '1', 'NSHighResolutionCapable': True,
}))
launcher.write_text('#!/bin/bash\nexec /usr/bin/open -a Terminal ' + shlex.quote(str(command)) + '\n')
launcher.chmod(0o755)
print(app)
