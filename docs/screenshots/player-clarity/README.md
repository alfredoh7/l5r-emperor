# Native visual and input evidence

Captured on macOS / Godot 4.7.2 Compatibility. These are native renders, not imagegen mockups.

- `inlays.png`, `inlays-changing.png`: isolated public-view fixtures with 0/1/3/6 attachments, upright/bowed, Force increase/decrease, Chi increase/decrease, zero and double digits.
- `bowed-engine.png`: actual public Army views exported by the existing Core bowed-unit regression; Personality remains 3/3 while unit contribution changes 0/13.
- `battle.png`, `battle-1280.png`: published-match battle framing at 1672×941 and 1280×800. Existing whole-table/pan/zoom controls remain available; at a distant whole-table view fine statistics still require zoom/inspection.
- `preview.png`, `expanded.png`: actual board inlay click and expanded attachment inspection.
- `player-clarity.png`: same-clan fixture separating turn owner and action priority, public status examples and all four old/new emblem samples.
- `name.png`: local name entry after deck selection.

Run `bash tools/check-player-clarity.sh` for client compilation, native keyboard Pass, disabled required-choice/disconnected Pass, hidden status, new-game/name/Enter/persistence/hotseat/cancel/reduced-motion checks, and inlay animation/click/expiry fixtures. Native tests verify behavior; screenshots are reviewed separately for appearance. This is not expert rules acceptance or Windows graphical playtesting.

The first strip attempt was rejected by Alfredo for readability. It was replaced by the wider, quiet bitmap panels shown here; the numeral font uses lining figures to avoid uneven old-style digits. No assertion of pixel-identical generated art or universal legibility at arbitrarily distant zoom is made.
