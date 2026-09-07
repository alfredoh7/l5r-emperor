#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
export MSBUILDDISABLENODEREUSE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
if ! command -v dotnet >/dev/null; then
  echo 'Install the project SDK first: python tools/bootstrap.py --install --dotnet-only' >&2
  exit 1
fi
dotnet build src/L5R.Server/L5R.Server.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
exec dotnet src/L5R.Server/bin/Debug/net10.0/L5R.Server.dll --L5R:StoragePath "$L5R_PROJECT_ROOT/.cache/server/rooms-0.2" "$@"
