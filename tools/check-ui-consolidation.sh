#!/usr/bin/env bash
# Native combined-build acceptance. Home/Favor engine/input cases additionally
# run through tools/check-persistent-home.sh. Screenshots require visual review.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
l5r_out="$L5R_PROJECT_ROOT/artifacts/ui-consolidation-checks"
mkdir -p "$l5r_out"
python3 tools/ensure_catalogue_art.py
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import > "$l5r_out/import.log" 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:' "$l5r_out/import.log"; then cat "$l5r_out/import.log"; exit 1; fi
# Optional whitespace-separated subset for an adaptive rerun after a failure.
l5r_cases="${L5R_UI_CASES:-published-lacquer-menu published-rules published-lacquer-history published-lacquer-coverage published-lacquer-private published-lacquer-confirm published-lacquer-tooltip published-lacquer-card published-board-flow-target published-board-flow-waiting published-board-flow-outcome published-board-removed-gallery published-board-assignment-confirm published-board-defense-confirm published-board-battle-expanded published-board-target-confirm payment purchase menu-flow menu-input}"
for l5r_case in $l5r_cases; do
  echo "NATIVE_CASE: $l5r_case 1672x941"
  "$GODOT" --path src/L5R.Client --resolution 1672x941 -- --profile=ui-consolidation-check --capture="$l5r_out/$l5r_case.png" --capture-mode="$l5r_case" > "$l5r_out/$l5r_case.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "$l5r_out/$l5r_case.log"; then cat "$l5r_out/$l5r_case.log"; exit 1; fi
  test -s "$l5r_out/$l5r_case.png"
done
for l5r_case in published-lacquer-menu published-rules published-board-flow-target published-board-removed-gallery; do
  echo "NATIVE_CASE: $l5r_case 1280x800"
  "$GODOT" --path src/L5R.Client --resolution 1280x800 -- --profile=ui-consolidation-check --capture="$l5r_out/compact-$l5r_case.png" --capture-mode="$l5r_case" > "$l5r_out/compact-$l5r_case.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "$l5r_out/compact-$l5r_case.log"; then cat "$l5r_out/compact-$l5r_case.log"; exit 1; fi
  test -s "$l5r_out/compact-$l5r_case.png"
done
for l5r_probe in Catalogue DeckBuilder; do
  mkdir -p "$l5r_out/$l5r_probe"
  echo "NATIVE_PROBE: $l5r_probe"
  "$GODOT" --path src/L5R.Client --resolution 1672x941 "res://${l5r_probe}Probe.tscn" -- --out="$l5r_out/$l5r_probe" > "$l5r_out/$l5r_probe/native.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "$l5r_out/$l5r_probe/native.log"; then cat "$l5r_out/$l5r_probe/native.log"; exit 1; fi
  rg 'CATALOGUE PASS|DECK BUILDER PASS' "$l5r_out/$l5r_probe/native.log"
done
"$GODOT" --path src/L5R.Client --resolution 1672x941 res://ClanPlacardProbe.tscn -- --reference="$L5R_PROJECT_ROOT/assets/ui/clan-placard/approved-a.png" --output="$l5r_out/placard-source-native.png" > "$l5r_out/placard.log" 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "$l5r_out/placard.log"; then cat "$l5r_out/placard.log"; exit 1; fi
rg 'CLAN_PLACARD_PASS:' "$l5r_out/placard.log"
for l5r_resolution in 1672x941 1280x720; do
  l5r_extra=""; if [ "$l5r_resolution" = 1280x720 ]; then l5r_extra=--scaled; fi
  "$GODOT" --path src/L5R.Client --resolution "$l5r_resolution" res://PhaseTrackProbe.tscn -- "$l5r_extra" --output="$l5r_out/phase-$l5r_resolution" > "$l5r_out/phase-$l5r_resolution.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "$l5r_out/phase-$l5r_resolution.log"; then cat "$l5r_out/phase-$l5r_resolution.log"; exit 1; fi
  rg SIX_PHASE_PASS "$l5r_out/phase-$l5r_resolution.log"
done
echo 'UI_CONSOLIDATION_PASS: native menus, battle, removed piles, board, catalogue, deck builder, placards and phase rail. Inspect captures; run persistent Home suite separately.'
