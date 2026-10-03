#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/screenshots
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path "$L5R_PROJECT_ROOT/src/L5R.Client" --import
for l5r_mode in table payment purchase library published published-refresh published-refresh-confirm published-choice published-choice-confirm; do
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1440x900 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/$l5r_mode-1440.png" --capture-mode="$l5r_mode"
done
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1280x720 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/payment-1280.png" --capture-mode=payment
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1920x1080 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/table-1920.png"
