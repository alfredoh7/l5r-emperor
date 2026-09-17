# Home/Favor native validation — 2026-09-16

Task #245 / PR #246, stacked on ongoing Event projection PR #242. No engine rules were changed in this pass.

## Commands and results

- `bash tools/check.sh --skip-client`: passed. 43 collaboration Python checks, 130 content Python checks; 152 core, 52 published, 9 deck-format, 10 deck-workshop, 14 AI, 5 server and 4 transport checks.
- `python3 tools/test_meshy.py`: 6 passed; Keychain/argument safeguards retained.
- C# client builds in task and integrated preview: 0 warnings/errors.
- `bash tools/check-persistent-home.sh` in `artifacts/noble-home-preview`: passed at 1920×1080 reduced motion and 1440×900 normal motion. No Godot script/error output in final native logs.
- Final asset mipmap import plus native `published-home-overview` and `published-home-detail` captures passed. Room/card projection maximum discrepancy 0.000153 px across sampled points; this measures projection agreement, not artistic similarity.

## Covered behavior

Real MatchEngine test states installed only by the isolated native probe; tested actions execute through MatchSession. Native pointer input confirms Lobby, Limited Favor, Battle Favor, Shunya's Favor spend and Ring of Air. Verify single public owner across seats, transfer, disappearance after spending, legal-action visibility, defender presence, original card destinations/bowing and persistent cards retaining normal interaction.

Suspicions is revealed through an actual Events phase: Honor changes, the effect remains in EventInPlay, both players can inspect, and no targetable CardTile is created. A pending hand-limit decision remains reachable while a reminder is inspected. New games clear old selection. Empty sections hide; fitted overview expands for newly appearing Home content without taking control of a manually positioned camera.

Artwork/capacity captures use explicitly staged public snapshots (including duplicate Rings for crowding); they are not proof of legal recruitment. The integrated preview retains approved room, #244 placard and #169 lacquer menus. Favor tooltip inherits that active skin and suppresses duplicate native popups.

## Iterations from native review

- Meshy frame replaced the overly gold imagegen-only runtime frame with carved ivory-white relief.
- Fixed reminder selection leaving stale unit highlight / inspection.
- Fixed inspected reminder hiding a new required engine decision.
- Fixed newly gained Favor being clipped by the sidebar while in fitted overview.
- Fixed duplicate/oversized hover previews; bounded preview retains Caudex and active lacquer material.
- Enabled mipmaps for the minified Home art and Favor faces.

Final clickable-launcher smoke exposed stale catalogue pack coverage. Rebuilt with the publisher tool against current pinned sources (2,736 WebP-85 scans), published an immutable v2 private pack, installed with integrity verification, and checked the actual Play Latest.command path. Original scans, dimensions and printing selection remain unchanged.

No Windows graphical playtest, human expert rules acceptance or exact full-screen pixel-equivalence claim. Mendon's shared review and dependency #242 are still required before merging PR #246.
