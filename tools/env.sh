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
    # An `if` here, not a bare `[ -f ... ] && ... && break`: under a sourcing script's own
    # `set -e` (check.sh always has one active), a plain "&&" chain's own false left-hand side
    # aborts the WHOLE script right here whenever .tools/godot/ has no match yet (e.g. a fresh
    # checkout, or `--skip-client` in an environment with no Godot installed at all) -- silently,
    # with zero output, since a failed `[ -f ]` test prints nothing. That previously made
    # `check.sh --skip-client` itself unusable without Godot ever being installed, defeating the
    # whole point of `--skip-client`.
    if [ -f "$l5r_editor" ]; then export GODOT="$l5r_editor"; break; fi
  done
fi
