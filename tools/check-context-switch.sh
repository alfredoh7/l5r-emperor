#!/usr/bin/env bash
# Issue #1103: rapid switching between the menu, Practice, deck builder, library, tutorial and private match, with the bot playing both seats.
# Fails on a crash, a hang (no step for 90 s), a leftover duel, a replaced Practice or any logged error. Reproduce one run with the seed it prints.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
out="$L5R_PROJECT_ROOT/artifacts/checks/context-switch"; rm -rf "$out"; mkdir -p "$out"
dotnet build src/L5R.Client --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
dotnet build src/L5R.Server/L5R.Server.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path src/L5R.Client --editor --import --quit > "$out/import.log" 2>&1

port=$(( 5300 + RANDOM % 400 ))
dotnet src/L5R.Server/bin/Debug/net10.0/L5R.Server.dll --urls "http://127.0.0.1:$port" --L5R:StoragePath "$out/rooms" > "$out/server.log" 2>&1 &
server=$!
trap 'kill $server 2>/dev/null || true' EXIT
for _ in $(seq 1 60); do curl -s -o /dev/null "http://127.0.0.1:$port/" && break; sleep .5; done

run() { # name seed steps [probe args]
  local name=$1 seed=$2 steps=$3; shift 3
  local log="$out/$name.log"
  L5R_STRESS_SERVER="http://127.0.0.1:$port" "$GODOT" --path src/L5R.Client --resolution 1440x900 res://ContextStressProbe.tscn -- \
    --seed="$seed" --steps="$steps" --output="$out" "$@" > "$log" 2>&1 &
  local game=$! last=-1 still=0
  while kill -0 $game 2>/dev/null; do
    sleep 5; local n; n=$(grep -c STRESS_STEP "$log" || true)
    if [ "$n" = "$last" ]; then still=$((still + 5)); else still=0; last=$n; fi
    if [ $still -ge 90 ]; then kill -ABRT $game; sleep 3; kill -9 $game 2>/dev/null || true; echo "HANG in $name (seed $seed) after step $n; see $log"; exit 1; fi
  done
  if ! wait $game; then cat "$log"; echo "FAILED: $name (seed $seed)"; exit 1; fi
  if rg 'ERROR:|SCRIPT ERROR:|Unhandled exception' "$log"; then echo "FAILED: $name (seed $seed) logged errors"; exit 1; fi
  rg CONTEXT_STRESS_PASS "$log"
}

run duel-leave-practice 10 3 --only=duel-leave-practice
run online-then-practice 14 2 --only=online-then-practice
run random 6 ${L5R_CONTEXT_STEPS:-40}
echo 'Context-switch stress checks passed.'
