#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
output="$L5R_PROJECT_ROOT/docs/screenshots/duel-transition/milestone-1"
logs="$L5R_PROJECT_ROOT/artifacts/transition-review"
mkdir -p "$output" "$logs"
for variant in reduced skip compact wide resize; do
  resolution=1920x1080
  args=()
  case "$variant" in
    reduced) args+=(--transition-reduced) ;;
    skip) args+=(--transition-skip) ;;
    resize) args+=(--transition-resize) ;;
    compact) resolution=1440x900 ;;
    wide) resolution=2560x1080 ;;
  esac
  "$GODOT" --path src/L5R.Client --resolution "$resolution" --fixed-fps 60 -- --duel-transition-review --capture="$output/$variant" --capture-mode=transition ${args[@]+"${args[@]}"} > "$logs/$variant.log" 2>&1
  if rg -n 'ERROR:|SCRIPT ERROR:|SHADER ERROR:|Unhandled exception' "$logs/$variant.log"; then exit 1; fi
  rg -q 'TRANSITION_CHECK: native hand selection works' "$logs/$variant.log"
  rg -q 'TRANSITION_CHECK: completed.*"Passed":true' "$logs/$variant.log"
  echo "PASS: $variant"
done
