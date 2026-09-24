#!/usr/bin/env python3
"""Prepared Mac review apps for #448; no checkout switching or automatic input."""
from pathlib import Path
import plistlib
import shlex

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "duel-followups" / "Launchers"
SCENARIOS = {
    "01 Focus Effect - Return a Card": "discard",
    "02 Concede Match": "concede",
    "03 Focus Effect - Other Seat": "discard-opponent",
    "04 Focus Effect - Crowded": "crowded",
    "05 Focus Effect - Attach": "attach",
    "06 Focus Effect - Required Straighten": "straighten",
    "07 Focus Effect - Destroy Attachment": "destroy",
    "08 Focus Effect - Honor or Draw": "effect",
    "09 Focus Effect - Consecutive Choices": "multiple",
    "10 Focus Effect - Reduced Motion": "discard-reduced",
    "11 Concede Match - Other Seat": "concede-opponent",
}
for name, scenario in SCENARIOS.items():
    OUT.mkdir(parents=True, exist_ok=True)
    command = OUT / (name + ".command")
    command.write_text("#!/bin/bash\nset -euo pipefail\ncd " + shlex.quote(str(ROOT)) + "\nexec bash tools/run.sh --resolution 1920x1080 -- --profile=duel-review-" + scenario + " --capture-mode=duel-followup-" + scenario + "\n")
    command.chmod(0o755)
    app = OUT / (name + ".app")
    macos = app / "Contents" / "MacOS"
    macos.mkdir(parents=True, exist_ok=True)
    info = {"CFBundleName": name, "CFBundleDisplayName": name,
            "CFBundleIdentifier": "local.l5r.duel-review." + scenario,
            "CFBundleExecutable": "launch", "CFBundlePackageType": "APPL",
            "CFBundleVersion": "1", "CFBundleShortVersionString": "1.0",
            "NSHighResolutionCapable": True}
    (app / "Contents" / "Info.plist").write_bytes(plistlib.dumps(info))
    launch = macos / "launch"
    launch.write_text("#!/bin/bash\nexec /usr/bin/open -a Terminal " + shlex.quote(str(command)) + "\n")
    launch.chmod(0o755)
    print(app)
