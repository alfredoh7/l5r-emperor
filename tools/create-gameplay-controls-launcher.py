#!/usr/bin/env python3
"""Prepared native gameplay-control review launchers for Alfredo."""
from pathlib import Path
import plistlib
import shlex
root = Path(__file__).resolve().parents[1]
output = root / 'artifacts/gameplay-controls-review'
output.mkdir(parents=True, exist_ok=True)
for title, scenario in [('Payments', 'reaction'), ('Choices', 'entry'), ('Optional Actions', 'additional')]:
    command = output / f'Review {title}.command'
    command.write_text('#!/bin/bash\nset -euo pipefail\ncd ' + shlex.quote(str(root)) + '\nexec bash tools/run.sh --resolution 1440x900 --profile=gameplay-controls-review -- --gameplay-controls-review --controls-scenario=' + scenario + '\n')
    command.chmod(0o755)
    app = output / f'L5R {title} - Review.app'
    macos = app / 'Contents/MacOS'
    macos.mkdir(parents=True, exist_ok=True)
    (app / 'Contents/Info.plist').write_bytes(plistlib.dumps({'CFBundleName': f'L5R {title} - Review', 'CFBundleIdentifier': 'local.l5r.controls.' + scenario, 'CFBundleExecutable': 'launch', 'CFBundlePackageType': 'APPL', 'CFBundleVersion': '1', 'NSHighResolutionCapable': True}))
    launcher = macos / 'launch'
    launcher.write_text('#!/bin/bash\nexec /usr/bin/open -a Terminal ' + shlex.quote(str(command)) + '\n')
    launcher.chmod(0o755)
    print(app)
