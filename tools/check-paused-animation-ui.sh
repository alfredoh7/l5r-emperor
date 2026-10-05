#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/paused-animation
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/paused-animation/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/paused-animation/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=paused-animation-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/paused-animation/$mode.png" --capture-mode="published-paused-animation-$mode" > "artifacts/checks/paused-animation/$mode.log" 2>&1
  rg -q 'PAUSED_ANIMATION_PASS' "artifacts/checks/paused-animation/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/paused-animation/$mode.log"; then cat "artifacts/checks/paused-animation/$mode.log";exit 1;fi
  echo "Paused Animation native: $mode passed"
done
