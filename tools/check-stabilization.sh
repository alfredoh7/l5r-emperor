#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks artifacts/stabilization
python3 tools/ensure_catalogue_art.py
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo > artifacts/checks/stabilization-build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/stabilization-import.log 2>&1
specs=('1672x941 brash' '1280x800 brash-decline' '1280x800 brash-opponent' '1672x941 bow' '1280x800 before' '1280x800 before-pass' '1672x941 equip' '1280x800 equip-free')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=stabilization-check --capture="$L5R_PROJECT_ROOT/artifacts/stabilization/$mode.png" --capture-mode="published-stabilization-$mode" > "artifacts/checks/stabilization-$mode.log" 2>&1
  rg -q STABILIZATION_NATIVE_PASS "artifacts/checks/stabilization-$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/stabilization-$mode.log"; then cat "artifacts/checks/stabilization-$mode.log";exit 1;fi
  echo "Stabilization native: $mode passed"
done
