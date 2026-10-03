#!/usr/bin/env python3
"""Create a one-click local review tied to this checkout, without changing branches."""
from pathlib import Path
import argparse
import plistlib
import shlex

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, default=root / 'artifacts/open-battlefield-review')
parser.add_argument('--correction', action='store_true', help='Open the resolved defending-survivors scenario directly')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
correction_arg = ' --open-battlefield-correction' if args.correction else ''
command = args.output / 'Review Open Battlefield.command'
command.write_text('#!/bin/bash\nset -euo pipefail\ncd ' + shlex.quote(str(root)) +
                   '\nexec bash tools/run.sh --resolution 1920x1080 --profile=open-battlefield-review -- --open-battlefield-review' + correction_arg + '\n')
command.chmod(0o755)
app = args.output / 'L5R Open Battlefield - Review.app'
macos = app / 'Contents/MacOS'
macos.mkdir(parents=True, exist_ok=True)
(app / 'Contents/Info.plist').write_bytes(plistlib.dumps({
    'CFBundleName': 'L5R Open Battlefield - Review',
    'CFBundleIdentifier': 'local.l5r.open-battlefield.review',
    'CFBundleExecutable': 'launch', 'CFBundlePackageType': 'APPL',
    'CFBundleVersion': '1', 'NSHighResolutionCapable': True,
}))
launcher = macos / 'launch'
launcher.write_text('#!/bin/bash\nexec /usr/bin/open -a Terminal ' + shlex.quote(str(command)) + '\n')
launcher.chmod(0o755)
print(app)
