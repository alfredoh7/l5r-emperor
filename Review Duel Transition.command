#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
exec bash tools/run.sh --resolution 1920x1080 -- --duel-transition-review --profile=duel-transition-review "$@"
