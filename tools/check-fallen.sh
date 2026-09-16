#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks docs/screenshots/fallen
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo > artifacts/checks/fallen-build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/fallen-import.log 2>&1
check_log() {
  rg -q "$2" "$1"
  if rg -q 'ERROR:|SCRIPT ERROR:' "$1"; then cat "$1"; exit 1; fi
}
# Keep native windows sequential so pointer/focus ownership is deterministic.
for spec in '2240x990 comparison' '1920x1080 normal' '1920x1080 honorable' '1280x720 small' '1920x1080 orientation' '1920x1080 many' '1920x1080 empty'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" res://FallenProbe.tscn -- --mode="$mode" --output="$L5R_PROJECT_ROOT/docs/screenshots/fallen/$mode.png" > "artifacts/checks/fallen-$mode.log" 2>&1
  check_log "artifacts/checks/fallen-$mode.log" FALLEN_PASS
  echo "Fallen native: $mode passed"
done
for spec in '1920x1080 table published-fallen-table' '1920x1080 gallery published-fallen-gallery' '1280x720 gallery-small published-fallen-gallery' '1920x1080 seat-1 published-fallen-seat-1-table' '1920x1080 overlap published-fallen-overlap-table' '1280x720 overlap-small published-fallen-overlap-table' '1920x1080 overlap-seat-1 published-fallen-overlap-seat-1-table'; do
  read -r resolution name mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --capture="$L5R_PROJECT_ROOT/docs/screenshots/fallen/$name.png" --capture-mode="$mode" > "artifacts/checks/fallen-$name.log" 2>&1
  check_log "artifacts/checks/fallen-$name.log" FALLEN_TABLE_PASS
  echo "Fallen full game: $name passed"
done
# Card rotation shares projection with existing attachments and token rails.
for mode in normal bowed-attachments perspective; do
  "$GODOT" --path src/L5R.Client --resolution 1920x1080 res://TokenRailProbe.tscn -- "--$mode" --output="$L5R_PROJECT_ROOT/artifacts/checks/fallen-token-$mode.png" > "artifacts/checks/fallen-token-$mode.log" 2>&1
  check_log "artifacts/checks/fallen-token-$mode.log" TOKEN_RAIL_PASS
  echo "Existing token rails: $mode passed"
done
echo 'Native checks passed. Review the source/native captures separately.'
