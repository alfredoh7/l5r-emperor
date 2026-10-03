#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/screenshots artifacts/checks
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --editor --import --quit > artifacts/checks/menu-import.log 2>&1
for l5r_case in menu menu-later menu-options menu-play menu-name menu-name-input menu-selection menu-reduced menu-reduced-later menu-flow menu-input menu-overlays menu-private menu-library; do
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1672x941 -- --profile=menu-acceptance-test --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/$l5r_case.png" --capture-mode="$l5r_case" > "artifacts/checks/$l5r_case.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/$l5r_case.log"; then cat "artifacts/checks/$l5r_case.log"; exit 1; fi
done
for l5r_resolution in 1280x800 2560x1080; do
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$l5r_resolution" -- --profile=menu-acceptance-test --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/menu-$l5r_resolution.png" --capture-mode=menu > "artifacts/checks/menu-$l5r_resolution.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/menu-$l5r_resolution.log"; then exit 1; fi
done
echo 'Native menu flow, settings persistence and aspect captures passed; inspect images for visual acceptance.'
