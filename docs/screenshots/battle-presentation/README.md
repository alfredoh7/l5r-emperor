# Native battle presentation review

These are actual Godot captures, not generated concepts. Approval artwork and its prompts are in `assets/ui/battle-presentation/`.

- `assignment-planned.png`: two local ink paths and translucent landing stacks. No command has moved the real cards yet.
- `assignment-committed.png`: accepted assignments, real stacks in their courts and preview paths removed.
- `defense-small.png`: defender assignment checked at 1280×800.
- `battle-before.png`, `battle-outcome.png`, `battle-fracture.png`, `battle-after.png`: the same real practice battle before resolution, during acknowledgment, during the threshold transition and after settlement.
- `province-fall.mp4`: native Movie Maker recording at 24 FPS; the excerpt retains original timing. It shows public engine events, actual card transfers and the new threshold. The complete window is recorded at 1440×900; the sequence stills come from that same run. Assignment stills use 1920×1080 and the smaller defense fixture uses 1280×800.
- `battle.replay.json`: the practice replay for that recorded battle; no online seat credentials.

## Validation

`bash tools/check-battle-presentation.sh`: five native fixtures pass (court layout, attack preview/confirmation, defense preview/confirmation, real province fall, reduced-motion fall). They check disjoint courts, defender placement, attachment membership, actual card corners, cancellation without a version change, accepted commands, blocked actions during the outcome beat, native decoration bounds, stable surviving province slots, removed assignment controls and readable history access.

`bash tools/check-ink-mat.sh`: three native window/focus fixtures and the real 120-frame purchase film pass. Includes tabletop hover/selection/deselection, real attachment-tray expansion/folding, card clearance, border expansion/contraction, card-settlement gating and reduced motion.

`bash tools/check-table-camera.sh`: eight native cases pass, including wheel/key/drag input, 16:10/ultrawide layouts, battle framing, assignment, expanded attachments, pile hover and reduced motion.

`bash tools/check.sh --skip-client`, after integrating main's PR #64: 37 collaboration, 95 content, 62 core, 32 published, 8 deck-format, 13 AI, 5 server and 4 client transport checks pass. Client compilation and Godot import also pass. Local NuGet vulnerability-feed access emitted NU1900 warnings; it did not prevent compilation.

Visual review found and corrected disappearing decoration caused by zero control bounds, shifting surviving provinces when units returned home, overly narrow crowded courts, and overlapping/wrong-state status text. Historical fallen markers stay extinguished when another province falls. No authoritative combat calculation was added to the UI.

Native Windows play and expert rules acceptance are not claimed. PR #76 remains subject to shared review; these checks do not replace Mendon's judgment. Sound and named Personality cinematics remain future work.

## Ink timing follow-up

Alfredo requested 30–40% less delay before borders appear. The ink timing scale is now 0.65: the post-card pause is 0.104 s (was 0.160), reveal is 0.8775 s (was 1.350), and old-contour dissolve is 0.312 s (was 0.480). Card/pile movement still gates the start; movement durations and the organic shader are unchanged. Reduced motion remains immediate.

The native lifecycle check now samples the shortened fade before and after completion. Its macOS focus wait and diagnostic were improved after a foreground-window failure. Validation is recorded in the current task handoff; the earlier screenshots/movie above document the original pass, before this timing adjustment.

Follow-up checks passed: client compile/import, the native 1920×1080 ink/input fixture and a real 120-frame purchase film. `ink-faster-purchase.mp4` and its JSON contain the new timing evidence. The film kept ink absent while cards moved, then showed gradual appearance through completion.
