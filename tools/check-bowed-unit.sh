#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks artifacts/screenshots
# Export public Army views from the exact regression, never recalculate Force in UI.
L5R_UNIT_VIEW_FIXTURE="$L5R_PROJECT_ROOT/artifacts/checks/bowed-unit-views.json" dotnet run --project tests/L5R.Core.Tests --no-restore > artifacts/checks/bowed-core.log 2>&1
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path "$L5R_PROJECT_ROOT/src/L5R.Client" --import > artifacts/checks/bowed-import.log 2>&1
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1672x941 res://UnitStandardProbe.tscn -- --engine-views="$L5R_PROJECT_ROOT/artifacts/checks/bowed-unit-views.json" --output="$L5R_PROJECT_ROOT/artifacts/screenshots/bowed-unit-correction.png" > artifacts/checks/bowed-native.log 2>&1
rg -q 'UNIT_STANDARD_PASS' artifacts/checks/bowed-native.log
if rg -n 'ERROR:|SCRIPT ERROR:' artifacts/checks/bowed-import.log artifacts/checks/bowed-native.log; then exit 1; fi
echo 'Bowed-unit authoritative regression and native display check passed.'
