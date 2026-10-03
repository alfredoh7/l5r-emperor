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
# L5R_GODOT points every checkout/worktree at one shared Godot install, so a new worktree needs no
# copy or junction of .tools/. An explicit override wins over the in-tree locations below.
if [ -n "${L5R_GODOT:-}" ]; then
  export GODOT="$L5R_GODOT"
elif [ ! -x "$GODOT" ]; then
  # nullglob (issue #272): without it, an unmatched glob (no Godot installed yet, e.g. a fresh
  # --skip-client checkout) iterates once over the LITERAL pattern string; the loop's own last
  # command then evaluates false, and since this is the last statement env.sh itself executes, a
  # caller running under `set -e` (tools/check.sh) silently exits 1 right after sourcing this file,
  # with no error message at all -- reproducible on any machine with no Godot editor present.
  shopt -s nullglob
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
  shopt -u nullglob
fi
# Opt-in guard for scripts that need Godot. Sourcing env.sh must stay non-fatal (check.sh --skip-client
# has no Godot), but a script that goes on to run "$GODOT" with it missing used to die silently under
# set -e, with the error buried in a redirected log. Call this right after sourcing.
l5r_require_godot() {
  if [ -x "$GODOT" ]; then return 0; fi
  echo "Godot not found at: $GODOT" >&2
  echo "Install it under .tools/godot, or set L5R_GODOT to an existing Godot executable shared by all worktrees." >&2
  return 1
}
true
