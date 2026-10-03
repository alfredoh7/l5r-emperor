#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/movement
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/movement/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/movement/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=movement-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/movement/$mode.png" --capture-mode="published-movement-$mode" > "artifacts/checks/movement/$mode.log" 2>&1
  rg -q 'MOVEMENT_PASS' "artifacts/checks/movement/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/movement/$mode.log"; then cat "artifacts/checks/movement/$mode.log";exit 1;fi
  echo "Movement native: $mode passed"
done
