#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
source tools/env.sh
"$GODOT" --editor --single-threaded-scene --path src/L5R.Client --import
exec "$GODOT" --path src/L5R.Client --resolution 1920x1080 res://LacquerFrameReview.tscn
