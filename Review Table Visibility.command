#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p artifacts/table-visibility-review
exec bash tools/run.sh --profile=table-visibility-review --resolution 1920x1080 -- \
  --capture="$PWD/artifacts/table-visibility-review/preview.png" \
  --capture-mode=published-board-camera-coverage-review
