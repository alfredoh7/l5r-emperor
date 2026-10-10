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
# --part runs one slice of the rules checks so CI can run them as parallel jobs (issue #798): py | registration | small | quick | core | ai.
# core and ai take --shard I/N (their console runners split the selected cases into N deterministic slices).
part=''
shard=()
case "${1:-}" in
  ''|--skip-client|--client-only) ;;
  --part)
    part="${2:-}"
    case "$part" in py|registration|small|quick|core|ai) ;; *) part='bad' ;; esac
    if [ "${3:-}" = "--shard" ] && [ -n "${4:-}" ] && { [ "$part" = core ] || [ "$part" = ai ]; }; then shard=(--shard "$4")
    elif [ "$#" -gt 2 ]; then part='bad'; fi
    if [ "$part" = bad ]; then
      echo 'Usage: bash tools/check.sh [--skip-client | --client-only | --part py|registration|small|quick | --part core|ai [--shard I/N]]' >&2
      exit 2
    fi ;;
  *)
    echo 'Usage: bash tools/check.sh [--skip-client | --client-only | --part py|registration|small|quick | --part core|ai [--shard I/N]]' >&2
    exit 2 ;;
esac
l5r_python="${L5R_PYTHON:-python3}"
if ! command -v "$l5r_python" >/dev/null; then l5r_python=python; fi
run_lint() {
  # Fast, always-run comment-swallow lint (issue #498's shape: new code appended
  # after an existing trailing // lands inside the comment and silently never
  # runs). Scoped to lines this branch changed, not the whole repository.
  "$l5r_python" tools/lint_comment_swallow.py
}
run_python_tests() {
  "$l5r_python" -m unittest discover -s tools/collaboration -p 'test_*.py'
  "$l5r_python" -m unittest discover -s tools/content -p 'test_*.py'
  "$l5r_python" -m unittest discover -s tools/hooks -p 'test_*.py'
  "$l5r_python" -m unittest discover -s tools/quality -p 'test_*.py'
  "$l5r_python" -m unittest discover -s tools -p 'test_meshy.py'
  "$l5r_python" -m unittest discover -s tools -p 'test_bootstrap.py'
  "$l5r_python" -m unittest discover -s tools -p 'test_lint_comment_swallow.py'
}
# run_dotnet_project NAME [runner args...]: build one console test project, then run its dll.
run_dotnet_project() {
  local project="$1"; shift
  dotnet build "tests/$project/$project.csproj" --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
  dotnet "tests/$project/bin/Debug/net10.0/$project.dll" "$@"
}
if [ -n "$part" ]; then
  case "$part" in
    py) L5R_SKIP_REGISTRATION_BUILD=1 run_python_tests ;;
    registration)
      # The two scratch-repo builds are independent and each ~100 s: run them side by side, fail if either fails.
      "$l5r_python" -m unittest discover -s tools/content -p 'test_parallel_wave_registration.py' &
      wave_pid=$!
      "$l5r_python" -m unittest discover -s tools/content -p 'test_parallel_mechanism_registration.py' &
      mechanism_pid=$!
      registration_status=0
      wait "$wave_pid" || registration_status=1
      wait "$mechanism_pid" || registration_status=1
      [ "$registration_status" -eq 0 ] || exit 1 ;;
    core) run_dotnet_project L5R.Core.Tests ${shard[@]+"${shard[@]}"} ;;
    ai) run_dotnet_project L5R.AI.Tests ${shard[@]+"${shard[@]}"} ;;
    small)
      run_lint
      for project in L5R.Server.Tests L5R.Client.Transport.Tests L5R.Client.Collection.Tests; do run_dotnet_project "$project"; done ;;
    # quick = py + small, one CI job: both are short, so a job each would mostly pay for checkout.
    quick)
      L5R_SKIP_REGISTRATION_BUILD=1 run_python_tests
      run_lint
      for project in L5R.Server.Tests L5R.Client.Transport.Tests L5R.Client.Collection.Tests; do run_dotnet_project "$project"; done ;;
  esac
  echo "Part '$part' passed."
  exit 0
fi
run_lint
# --client-only runs just the native client build, Godot import and smoke run (CI runs it beside the rules checks, in parallel).
if [ "${1:-}" != "--client-only" ]; then
  run_python_tests
  for project in L5R.Core.Tests L5R.AI.Tests L5R.Server.Tests L5R.Client.Transport.Tests L5R.Client.Collection.Tests; do
    run_dotnet_project "$project"
  done
fi
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
