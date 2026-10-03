#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
output="$L5R_PROJECT_ROOT/artifacts/duel-followups"
mkdir -p "$output"
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > "$output/build.log" 2>&1
variants=("$@")
if [ "$#" -eq 0 ]; then variants=(concede concede-lion discard discard-reduced discard-opponent discard-decline attach straighten destroy effect effect-draw multiple crowded); fi
for variant in "${variants[@]}"; do
  args=(); resolution=1920x1080
  scenario="choice-$variant"
  case "$variant" in
    concede) scenario=concede ;;
    concede-lion) scenario=concede; args+=(--duel-match-lion --duel-match-reduced) ;;
    discard-reduced) scenario=choice-discard; args+=(--duel-match-reduced); resolution=1120x700 ;;
    discard-opponent) scenario=choice-discard; args+=(--duel-match-lion --duel-match-reduced) ;;
    discard-decline) scenario=choice-discard; args+=(--duel-choice-decline --duel-match-reduced) ;;
    effect-draw) scenario=choice-effect; args+=(--duel-choice-draw --duel-match-reduced) ;;
    crowded) resolution=1440x900; args+=(--duel-match-reduced) ;;
  esac
  "$GODOT" --path src/L5R.Client --always-on-top --position 10,50 --resolution "$resolution" -- --profile="duel-check-$variant" --duel-match-review --duel-followup-review --duel-match-scenario="$scenario" --capture="$output/$variant" --capture-mode=match ${args[@]+"${args[@]}"} > "$output/$variant.log" 2>&1
  if rg -n 'ERROR:|SCRIPT ERROR:|SHADER ERROR:|Unhandled exception' "$output/$variant.log"; then exit 1; fi
  rg -q 'DUEL_FOLLOWUP_CHECK: completed' "$output/$variant.log"
  echo "PASS: $variant"
done
