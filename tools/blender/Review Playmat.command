#!/bin/bash
# Native asset review; intentionally independent of catalogue installation.
set -euo pipefail
l5r_mat_root="$(cd "$(dirname "$0")/../.." && pwd)"
source "$l5r_mat_root/tools/env.sh"
cd "$l5r_mat_root"
if [ ! -x "$GODOT" ]; then
  echo 'Install the project tools first: python3 tools/bootstrap.py --install' >&2
  exit 1
fi
if ! compgen -G 'src/L5R.Client/.godot/imported/navy-mat-separated.glb-*.scn' >/dev/null ||
   ! compgen -G 'src/L5R.Client/.godot/imported/navy-mat-topaz-4x.webp-*.ctex' >/dev/null; then
  "$GODOT" --editor --path "$l5r_mat_root/src/L5R.Client" --import
fi
exec "$GODOT" --path "$l5r_mat_root/src/L5R.Client" res://MatAssetReview.tscn --resolution 1600x1000 "$@"
