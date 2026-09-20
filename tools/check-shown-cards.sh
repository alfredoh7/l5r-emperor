#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/shown
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/shown/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/shown/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=shown-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/shown/$mode.png" --capture-mode="published-shown-$mode" > "artifacts/checks/shown/$mode.log" 2>&1
  rg -q 'SHOWN_HISTORY_PASS' "artifacts/checks/shown/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/shown/$mode.log"; then cat "artifacts/checks/shown/$mode.log";exit 1;fi
  echo "Shown history native: $mode passed"
done
