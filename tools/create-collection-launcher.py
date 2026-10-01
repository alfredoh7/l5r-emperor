#!/usr/bin/env python3
"""Create prepared, isolated My Collection review launchers for this checkout."""
from pathlib import Path
import plistlib
import shlex

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/my-collection-review'
out.mkdir(parents=True, exist_ok=True)
for label, mode in [('My Collection', 'my-collection-review'), ('Deck Builder and Collection', 'deck-collection-review')]:
    command = out / ('Review ' + label + '.command')
    command.write_text('#!/bin/bash\nset -euo pipefail\ncd ' + shlex.quote(str(root)) +
                       '\nexec bash tools/run.sh --resolution 1672x941 --profile=my-collection-review-20261001-r1 -- --' + mode +
                       ' --capture=' + shlex.quote(str(out / (mode + '.png'))) + '\n')
    command.chmod(0o755)
    app = out / ('L5R ' + label + ' - Review.app')
    macos = app / 'Contents/MacOS'
    macos.mkdir(parents=True, exist_ok=True)
    (app / 'Contents/Info.plist').write_bytes(plistlib.dumps({
        'CFBundleName': 'L5R ' + label + ' - Review',
        'CFBundleIdentifier': 'local.l5r.' + mode,
        'CFBundleExecutable': 'launch', 'CFBundlePackageType': 'APPL',
        'CFBundleVersion': '1', 'NSHighResolutionCapable': True,
    }))
    launcher = macos / 'launch'
    launcher.write_text('#!/bin/bash\nexec /usr/bin/open -a Terminal ' + shlex.quote(str(command)) + '\n')
    launcher.chmod(0o755)
    print(app)
