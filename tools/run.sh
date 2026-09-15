#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
export MSBUILDDISABLENODEREUSE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
if [ ! -x "$GODOT" ] || ! command -v dotnet >/dev/null; then
  echo 'Install the project tools first: python tools/bootstrap.py --install' >&2
  exit 1
fi
l5r_python="${L5R_PYTHON:-python3}"
if ! command -v "$l5r_python" >/dev/null; then l5r_python=python; fi
"$l5r_python" tools/ensure_catalogue_art.py
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
# Keep both arrays nonempty for macOS's Bash 3.2 with nounset enabled.
l5r_engine_args=(--path "$L5R_PROJECT_ROOT/src/L5R.Client")
l5r_game_args=(--)
while [ "$#" -gt 0 ]; do
  case "$1" in
    --profile=*) l5r_game_args+=("$1") ;;
    --) shift; l5r_game_args+=("$@"); break ;;
    *) l5r_engine_args+=("$1") ;;
  esac
  shift
done
exec "$GODOT" "${l5r_engine_args[@]}" "${l5r_game_args[@]}"
