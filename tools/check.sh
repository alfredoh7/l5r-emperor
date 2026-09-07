#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
export MSBUILDDISABLENODEREUSE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
if ! command -v dotnet >/dev/null || [ "$(dotnet --version)" != "10.0.400" ]; then
  echo 'Install the pinned SDK first: python tools/bootstrap.py --install' >&2
  exit 1
fi
if [ "${1:-}" != "" ] && [ "$1" != "--skip-client" ]; then
  echo 'Usage: bash tools/check.sh [--skip-client]' >&2
  exit 2
fi
l5r_python="${L5R_PYTHON:-python3}"
if ! command -v "$l5r_python" >/dev/null; then l5r_python=python; fi
"$l5r_python" -m unittest discover -s tools/content -p 'test_*.py'
for project in L5R.Core.Tests L5R.AI.Tests L5R.Server.Tests L5R.Client.Transport.Tests; do
  dotnet build "tests/$project/$project.csproj" --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
  dotnet "tests/$project/bin/Debug/net10.0/$project.dll"
done
if [ "${1:-}" != "--skip-client" ]; then
  if [ ! -x "$GODOT" ]; then
    echo 'Install the pinned Godot .NET editor: python tools/bootstrap.py --install' >&2
    exit 1
  fi
  dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
  mkdir -p artifacts/checks
  "$GODOT" --headless --editor --path "$L5R_PROJECT_ROOT/src/L5R.Client" --log-file "$L5R_PROJECT_ROOT/artifacts/checks/import.log" --import
  "$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --log-file "$L5R_PROJECT_ROOT/artifacts/checks/smoke.log" --quit-after 3
  if grep -Eq '(ERROR:|SCRIPT ERROR:)' artifacts/checks/import.log artifacts/checks/smoke.log; then
    echo 'Godot reported an error; inspect artifacts/checks/*.log.' >&2
    exit 1
  fi
fi
echo 'All requested checks passed.'
