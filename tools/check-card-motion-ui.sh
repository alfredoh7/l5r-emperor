#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/card-motion
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/card-motion/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/card-motion/import.log 2>&1
"$GODOT" --path src/L5R.Client --resolution 1920x1080 -- --profile=card-motion-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/card-motion/card-motion.png" --capture-mode=published-card-motion > artifacts/checks/card-motion/card-motion.log 2>&1
rg -q 'CARD_MOTION_PASS' artifacts/checks/card-motion/card-motion.log
if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/checks/card-motion/card-motion.log; then cat artifacts/checks/card-motion/card-motion.log; exit 1; fi
echo "Card motion native: passed"
