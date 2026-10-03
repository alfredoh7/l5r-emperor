#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p artifacts/interaction-review
exec bash tools/run.sh --resolution 1440x900 -- --profile=interaction-review --capture="$PWD/artifacts/interaction-review/published-interactions-handoff-review.png" --capture-mode=published-interactions-handoff-review
