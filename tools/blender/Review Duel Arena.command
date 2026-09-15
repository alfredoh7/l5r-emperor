#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/../.."
# A new process preserves any other open Blender document.
exec open -na /Applications/Blender.app --args --disable-autoexec "$PWD/assets/table/duel-arena/duel-arena-v6.blend"
