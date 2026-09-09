# Approved YOUR TURN — Ink Bloom

Alfredo selected A (Ink Bloom) on 2026-09-09. Reuse the generated transparent image verbatim: `src/L5R.Client/Art/turn-ink-bloom.png`. Prompts are recorded alongside this file; generated with built-in Imagegen. Weathered Quill uses the supplied original TTF, uppercase with compact spacing.

The Godot canvas shader follows the approved preview: center-out textured absorption .03–.95s; text .52–.92s; readable hold; lettering fades 2.5–3.15s; ink dissolves 2.65–3.5s. Reduced motion uses an ordinary fade and clears at 2.6s. The bitmap and text live in screen space above the central table, with no dimmer, buttons, focus or input interception.

Announce once per local turn (`ActivePlayerId` and `TurnNumber`). Priority, phase, duplicate snapshots, same-turn reconnects and menu resumes do not retrigger. New practice games reset the local observation history. A different room has a separate identity. Changing a practice viewing seat is not a turn transition. Cancel for menu/disconnection, opponent turn and victory. No rules commands or timing are changed.

Native visual and integration probes: `tools/check-turn-bloom.sh`. The probes are review fixtures, not playable card content.
