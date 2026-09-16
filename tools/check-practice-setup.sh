#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/practice-checks
python3 tools/ensure_catalogue_art.py
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --editor --import --quit > artifacts/practice-checks/import.log 2>&1
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1672x941 res://PracticeSetupProbe.tscn -- --out="$L5R_PROJECT_ROOT/artifacts/practice-checks" > artifacts/practice-checks/native.log 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/practice-checks/import.log artifacts/practice-checks/native.log; then tail -70 artifacts/practice-checks/native.log; exit 1; fi
rg 'PRACTICE SETUP PASS' artifacts/practice-checks/native.log
