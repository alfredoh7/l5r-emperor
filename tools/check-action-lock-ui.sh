#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/action-lock
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/action-lock/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/action-lock/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=action-lock-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/action-lock/$mode.png" --capture-mode="published-action-lock-$mode" > "artifacts/checks/action-lock/$mode.log" 2>&1
  rg -q 'ACTION_LOCK_PASS' "artifacts/checks/action-lock/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/action-lock/$mode.log"; then cat "artifacts/checks/action-lock/$mode.log";exit 1;fi
  echo "Action restriction native: $mode passed"
done
