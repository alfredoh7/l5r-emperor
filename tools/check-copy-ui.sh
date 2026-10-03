#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/copy
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/copy/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/copy/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=copy-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/copy/$mode.png" --capture-mode="published-copy-$mode" > "artifacts/checks/copy/$mode.log" 2>&1
  rg -q 'COPY_PASS' "artifacts/checks/copy/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/copy/$mode.log"; then cat "artifacts/checks/copy/$mode.log";exit 1;fi
  echo "Copy native: $mode passed"
done
