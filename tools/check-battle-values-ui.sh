#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
mkdir -p artifacts/battle-values-review
"$GODOT" --headless --editor --path src/L5R.Client --import --quit > artifacts/battle-values-review/import.log 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:' artifacts/battle-values-review/import.log; then cat artifacts/battle-values-review/import.log; exit 1; fi
"$GODOT" --path src/L5R.Client --resolution 1920x1080 -- --profile=battle-values-checks --battle-values-review --battle-values-checks="$L5R_PROJECT_ROOT/artifacts/battle-values-review" > artifacts/battle-values-review/native.log 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:' artifacts/battle-values-review/native.log; then cat artifacts/battle-values-review/native.log; exit 1; fi
rg 'BATTLE_VALUES_PASS' artifacts/battle-values-review/native.log
