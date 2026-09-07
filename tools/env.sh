#!/usr/bin/env bash
# Source from any working directory. All development caches stay in the project.
L5R_PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export L5R_PROJECT_ROOT
if [ -x "$L5R_PROJECT_ROOT/.tools/dotnet/dotnet" ] || [ -f "$L5R_PROJECT_ROOT/.tools/dotnet/dotnet.exe" ]; then
  export DOTNET_ROOT="$L5R_PROJECT_ROOT/.tools/dotnet"
  export PATH="$DOTNET_ROOT:$PATH"
fi
export DOTNET_CLI_HOME="$L5R_PROJECT_ROOT/.cache/dotnet"
export NUGET_PACKAGES="$L5R_PROJECT_ROOT/.cache/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export MSBUILDDISABLENODEREUSE=1
export GODOT="$L5R_PROJECT_ROOT/.tools/godot/Godot_mono.app/Contents/MacOS/Godot"
if [ ! -x "$GODOT" ]; then
  for l5r_editor in "$L5R_PROJECT_ROOT"/.tools/godot/Godot*_mono_*.exe; do
    case "$l5r_editor" in *_console.exe) continue ;; esac
    [ -f "$l5r_editor" ] && export GODOT="$l5r_editor" && break
  done
fi
