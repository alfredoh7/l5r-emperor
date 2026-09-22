#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/placard-checks docs/screenshots/clan-placard
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/placard-checks/import.log 2>&1
"$GODOT" --path src/L5R.Client --resolution 1672x941 res://ClanPlacardProbe.tscn -- \
  --reference="$L5R_PROJECT_ROOT/assets/ui/clan-placard/approved-a.png" \
  --output="$L5R_PROJECT_ROOT/docs/screenshots/clan-placard/source-native.png" > artifacts/placard-checks/probe.log 2>&1
rg -q 'CLAN_PLACARD_PASS:' artifacts/placard-checks/probe.log
if rg 'ERROR:|SCRIPT ERROR:|SHADER ERROR:|Unhandled exception' artifacts/placard-checks/import.log artifacts/placard-checks/probe.log; then exit 1; fi
echo 'Native clan placard comparison and input checks passed.'
