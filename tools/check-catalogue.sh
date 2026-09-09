#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/catalogue-checks
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --editor --import --quit > artifacts/catalogue-checks/import.log 2>&1
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1672x941 res://CatalogueProbe.tscn -- --out="$L5R_PROJECT_ROOT/artifacts/catalogue-checks" > artifacts/catalogue-checks/native.log 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception|WARNING:' artifacts/catalogue-checks/import.log artifacts/catalogue-checks/native.log; then cat artifacts/catalogue-checks/native.log; exit 1; fi
rg 'CATALOGUE PASS' artifacts/catalogue-checks/native.log
echo 'Native captures available in artifacts/catalogue-checks; visual inspection remains required.'
