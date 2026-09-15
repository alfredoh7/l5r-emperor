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
# Finder/Terminal may put Apple's Python 3.9 first even when the required runtime is installed.
# Use an installed supported interpreter; never install or change the user's system Python here.
l5r_python=""
if [ -n "${L5R_PYTHON:-}" ]; then
  l5r_python_candidates=("$L5R_PYTHON")
else
  l5r_python_candidates=(python3 python3.14 python3.13 python3.12 /opt/homebrew/bin/python3 /usr/local/bin/python3 python)
fi
for l5r_candidate in "${l5r_python_candidates[@]}"; do
  if command -v "$l5r_candidate" >/dev/null 2>&1 && "$l5r_candidate" -c 'import sys; sys.exit(sys.version_info < (3, 12))' >/dev/null 2>&1; then
    l5r_python="$l5r_candidate"; break
  fi
done
if [ -z "$l5r_python" ]; then
  echo 'Python 3.12 or newer is required. Install it, or set L5R_PYTHON to its executable.' >&2
  exit 1
fi
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
