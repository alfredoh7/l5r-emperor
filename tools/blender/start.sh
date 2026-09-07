#!/usr/bin/env bash
set -euo pipefail
L5R_BLENDER_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
exec /Applications/Blender.app/Contents/MacOS/Blender --factory-startup --python "$L5R_BLENDER_ROOT/tools/blender/bootstrap.py"
