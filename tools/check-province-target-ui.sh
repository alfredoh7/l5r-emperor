#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/province-target
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/province-target/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/province-target/import.log 2>&1
specs=('1920x1080 normal' '1280x800 compact')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=province-target-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/province-target/$mode.png" --capture-mode="published-province-target-$mode" > "artifacts/checks/province-target/$mode.log" 2>&1
  rg -q 'PROVINCE_TARGET_PASS' "artifacts/checks/province-target/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/province-target/$mode.log"; then cat "artifacts/checks/province-target/$mode.log";exit 1;fi
  echo "Province target native: $mode passed"
done
