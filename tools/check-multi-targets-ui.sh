#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/multi-targets
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/multi-targets/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/multi-targets/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=multi-targets-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/multi-targets/$mode.png" --capture-mode="published-multi-targets-$mode" > "artifacts/checks/multi-targets/$mode.log" 2>&1
  rg -q 'MULTI_TARGETS_PASS' "artifacts/checks/multi-targets/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/multi-targets/$mode.log"; then cat "artifacts/checks/multi-targets/$mode.log";exit 1;fi
  echo "Multiple-target chooser native: $mode passed"
done
