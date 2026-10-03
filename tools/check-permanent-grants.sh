#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks docs/screenshots/permanent-grants
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo > artifacts/checks/grant-build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/grant-import.log 2>&1
check_log() {
  rg -q "$2" "$1"
  if rg -q 'ERROR:|SCRIPT ERROR:' "$1"; then cat "$1"; exit 1; fi
}
# Sequential native windows preserve pointer/focus ownership on the Mac.
for spec in '1600x1080 comparison' '1920x1080 normal' '1280x720 small' '1920x1080 perspective' '1920x1080 long'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" res://PermanentGrantProbe.tscn -- "--$mode" --output="$L5R_PROJECT_ROOT/docs/screenshots/permanent-grants/$mode.png" > "artifacts/checks/grant-$mode.log" 2>&1
  check_log "artifacts/checks/grant-$mode.log" PERMANENT_GRANT_PASS
  echo "Permanent grants: $mode passed"
done
for spec in '1920x1080 table published-grants' '1920x1080 table-detail published-grants-detail' '1280x720 table-small published-grants-detail'; do
  read -r resolution name mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --capture="$L5R_PROJECT_ROOT/docs/screenshots/permanent-grants/$name.png" --capture-mode="$mode" > "artifacts/checks/grant-$name.log" 2>&1
  check_log "artifacts/checks/grant-$name.log" GRANT_TABLE_PASS
  echo "Permanent grants: $name passed"
done
# Reuse the existing token/attachment regressions without replacing their recorded evidence.
for spec in '1920x1080 normal' '1920x1080 bowed-attachments' '1920x1080 perspective'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" res://TokenRailProbe.tscn -- "--$mode" --output="$L5R_PROJECT_ROOT/artifacts/checks/grant-token-$mode.png" > "artifacts/checks/grant-token-$mode.log" 2>&1
  check_log "artifacts/checks/grant-token-$mode.log" TOKEN_RAIL_PASS
  echo "Existing token rails: $mode passed"
done
echo 'Native checks passed. Review the captured artwork separately.'
