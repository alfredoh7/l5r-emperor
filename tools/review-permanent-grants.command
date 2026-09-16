#!/bin/bash
# Explicit read-only artwork examples; normal matches never inject these grants.
set -euo pipefail
l5r_review_root="$(cd "$(dirname "$0")/.." && pwd)"
exec /bin/bash "$l5r_review_root/tools/run.sh" --resolution 1920x1080 res://PermanentGrantProbe.tscn -- --interactive
