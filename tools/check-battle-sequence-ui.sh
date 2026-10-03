#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/battle-sequence
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/battle-sequence/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/battle-sequence/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=battle-sequence-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/battle-sequence/$mode.png" --capture-mode="published-battle-sequence-$mode" > "artifacts/checks/battle-sequence/$mode.log" 2>&1
  rg -q 'BATTLE_SEQUENCE_PASS' "artifacts/checks/battle-sequence/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/battle-sequence/$mode.log"; then cat "artifacts/checks/battle-sequence/$mode.log"; exit 1; fi
  echo "Battle sequence native: $mode passed"
done
