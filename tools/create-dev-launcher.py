#!/usr/bin/env python3
"""Create a local clickable game launcher for this checkout; never fetch or switch branches."""
from pathlib import Path
import plistlib
import shlex
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
artifacts = root / 'artifacts'


def make_macos_launcher():
    app = artifacts / 'L5R Emperor - Latest.app'
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
    return app


def make_windows_launcher():
    # Windows has no bundle format; a .lnk shortcut mirrors the Mac .app's role as a
    # single clickable, nicely named icon. Built via WScript.Shell COM through
    # PowerShell rather than a third-party dependency (e.g. pywin32).
    artifacts.mkdir(parents=True, exist_ok=True)
    shortcut = artifacts / 'L5R Emperor - Latest.lnk'
    target = root / 'Play Latest.bat'
    if not target.is_file():
        sys.exit(f'Missing {target}; cannot create the Windows launcher shortcut.')

    def ps_literal(value):
        return "'" + str(value).replace("'", "''") + "'"

    script = (
        f"$sc = (New-Object -ComObject WScript.Shell).CreateShortcut({ps_literal(shortcut)}); "
        f"$sc.TargetPath = {ps_literal(target)}; "
        f"$sc.WorkingDirectory = {ps_literal(root)}; "
        "$sc.Description = 'Launch the current L5R Emperor checkout'; "
        "$sc.Save()"
    )
    subprocess.run(['powershell', '-NoProfile', '-NonInteractive', '-Command', script], check=True)
    return shortcut


if sys.platform == 'darwin':
    print(make_macos_launcher())
elif sys.platform == 'win32':
    print(make_windows_launcher())
else:
    sys.exit('No clickable launcher is defined for this platform (macOS or Windows only).')
