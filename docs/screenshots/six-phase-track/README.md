# Six-phase rail native evidence

2026-09-09 · Godot 4.7.2 .NET · native Mac, Apple M1 Max.

Reproduce: `bash tools/check-six-phase-track.sh`. The source row is the approved A bitmap; the live row uses the same rail pixels with actual Musashi labels and authoritative phase mapping. Source lettering remains illustrative; the live font is intentional.

- `1672x941/`: six individually captured phase states plus Events hover/focus help.
- `1280x720/`: same checks with the live HUD scaled to 66%, demonstrating that the help text retains readable physical size.
- `board-1920x1080.png`, `board-1280x720.png`: actual published assignment scene showing all six labels, Attack highlight, priority message and Pass.

Native probes passed: every current Phase enum mapping, all six active states, Events help opens on hover, keyboard focus and left/right navigation, Escape dismissal, help controls do not dispatch Pass, viewport target containment, pixel-identical reduced-motion frames and Finished leaving all phases unlit. Hover-triggered content is checked before focus is retained for stable screenshots across OS pointer movement. Earlier focus-sensitive probe attempts failed; final runs passed. The native source comparison, compact help and board captures were visually reviewed.

Build: passed, zero warnings/errors. Godot headless import: passed. `bash tools/check.sh --skip-client`: 279 passed (37 content, 107 collaboration, 72 core, 33 published, 8 deck-format, 13 AI, 5 server, 4 transport). Existing Straighten/Events engine regressions are included; this task changes no engine timing or rules execution.

The six held phase views are UI fixtures, not a claim that automatic phases pause during current published matches. Windows graphical playtesting and expert approval of help wording remain separate. PR99 requests mdornbrook review of the semantic help.
