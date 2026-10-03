#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/opponent-payment
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/opponent-payment/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/opponent-payment/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=opponent-payment-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/opponent-payment/$mode.png" --capture-mode="published-opponent-payment-$mode" > "artifacts/checks/opponent-payment/$mode.log" 2>&1
  rg -q 'OPPONENT_PAYMENT_PASS' "artifacts/checks/opponent-payment/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/opponent-payment/$mode.log"; then cat "artifacts/checks/opponent-payment/$mode.log";exit 1;fi
  echo "Opponent payment native: $mode passed"
done
