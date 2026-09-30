#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/attack-targets
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/attack-targets/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/attack-targets/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=attack-targets-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/attack-targets/$mode.png" --capture-mode="published-attack-targets-$mode" > "artifacts/checks/attack-targets/$mode.log" 2>&1
  rg -q 'ATTACK_TARGET_PASS' "artifacts/checks/attack-targets/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/attack-targets/$mode.log"; then cat "artifacts/checks/attack-targets/$mode.log";exit 1;fi
  echo "Attack targets native: $mode passed"
done
