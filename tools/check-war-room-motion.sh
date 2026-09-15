#!/usr/bin/env bash
# Native motion/freeze/resume evidence. Run separately from native input tests.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
output="${L5R_ROOM_EVIDENCE_DIR:-$L5R_PROJECT_ROOT/docs/screenshots/war-room/milestone-6}"
logs="${L5R_ROOM_LOG_DIR:-$L5R_PROJECT_ROOT/artifacts/war-room-work/milestone-6}"
encoder="$(command -v ffmpeg)"
mkdir -p "$output" "$logs"
if [[ $# == 0 ]]; then set -- composition table-lantern room-lantern brazier; fi
for view in "$@"; do
    case "$view" in
        composition) frames=192 ;;
        table-lantern|room-lantern|brazier) frames=144 ;;
        *) echo "Unknown room view: $view" >&2; exit 1 ;;
    esac
    # A fresh evidence directory cannot accidentally reuse an old tail of frames.
    frame_dir="$(mktemp -d "$logs/$view-capture-XXXXXX")"
    "$GODOT" --path src/L5R.Client --resolution 1280x720 res://WarRoomReview.tscn -- \
        --clean --room-view="$view" --room-check-motion \
        --room-capture="$output/$view.png" --room-film="$frame_dir" --room-frames="$frames" \
        > "$logs/$view.log" 2>&1
    if rg -n 'SCRIPT ERROR|SHADER ERROR|ERROR:' "$logs/$view.log"; then exit 1; fi
    rg -q 'ROOM_MOTION_PASS:' "$logs/$view.log"
    rg -q "ROOM_FILM_PASS: $frames native frames at 24 fps; view=$view" "$logs/$view.log"
    rg -q "WAR_ROOM_CAPTURE: $view / 0" "$logs/$view.log"
    test -s "$output/$view.png"
    count=$(find "$frame_dir" -name 'frame-*.png' | wc -l | tr -d ' ')
    test "$count" = "$frames"
    "$encoder" -hide_banner -loglevel error -y -framerate 24 -i "$frame_dir/frame-%04d.png" \
        -c:v libx264 -crf 18 -preset medium -pix_fmt yuv420p -movflags +faststart "$output/$view.mp4"
    echo "PASS: $view; motion, exact freeze, resume and $frames complete native frames."
done
