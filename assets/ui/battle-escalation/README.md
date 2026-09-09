# Battle escalation artwork

**Current feedback:** the left Battle exchanges panel and toggle have been removed after playtesting. Its art and earlier captures below are retained only as references for a possible future redesign. Named defeat notices, blood courts and ordinary history remain.

Accepted selections: 1A Frayed ink; 2B Daisho seal; 3A Crimson breathing ink (+25% blood coverage); 4A Broad ink arrow; 5A Named defeat + 5B Aftermath; 6B Battle exchanges on the left. See `docs/decisions/2026-09-09-battle-escalation.md` and issue #75 / PR #76.

## Source to runtime

| Selection | Retained source / runtime asset | Implementation |
| --- | --- | --- |
| 1A | `src/L5R.Client/Art/frayed-province-reference.png` (exact approved concept) | Actual frayed pigment; proportionally sliced corners and torn runs. The number remains live in the existing cracked seal. |
| 2B | `approved/02b-daisho-seal.png` → `src/L5R.Client/Art/daisho.png` | Imagegen-derived RGBA Japanese sword seal, cropped in Godot without flattening its alpha. |
| 3A | `src/L5R.Client/Art/crimson-reference.png` (exact approved concept) | The concept's rest close-up supplies real blood and corner pigment. Only plain runs stretch. `blood-soak.gdshader` controls initial absorption and the later pulse. |
| 4A | `approved/04a-broad-ink-arrow.png` → `src/L5R.Client/Art/battle-detail-atlas.png` | Source-derived broad arrow and stepping stone. Analytic final tangent centers the tip on its actual path. |
| 5A | `approved/05a-named-defeat.png` → detail atlas | Empty black/crimson brush banner; names and cause remain engine-driven text. |
| 5B | `approved/05b-battle-aftermath.png` | Aftermath is integrated into the left journal, with actual destinations and expandable public effects rather than baked illustrative card/result data. |
| 6B | `approved/06b-battle-exchanges.png` → `src/L5R.Client/Art/war-journal.png` | Imagegen-derived worn lacquer, sword engraving, chipped brass and dried blood. Source and destination slice margins differ so fittings do not consume reading space. Existing aged clan emblems identify the actors, and original public card faces identify aftermath entries. |

Generation used the built-in image_gen tool. Exact prompts and source paths are in `production-prompts.json` and `derivation-prompts.json`. The attempted transparent crimson asset had baked checkerboard artifacts and was rejected. The shipped crimson instead uses the approved concept's actual pixels. The journal's opaque frame interior is intentional; its outer checkerboard is excluded by the runtime crop.

Godot performs region extraction and pigment separation in `BattleEscalationArt.cs` / `BattleInkArt.cs`; no Python-painted substitute art is used. All gameplay cards remain their original scans. Concept illustrations are not game screenshots or rules evidence.

## Verification

`tools/check-battle-presentation.sh` builds and imports the client, then exercises real published practice matches: compact layout, connected courts, draft/confirmed attacks, defender assignments, action destruction, battle deaths, province falls and reduced motion. It produces screenshots/replays and logs in the ignored `artifacts/` tree. Journal input uses native viewport wheel and click gestures; reading/collapsing the journal must not mutate the match or hijack camera input.

For the source comparison, run Godot with `--capture-mode=battle-escalation-reference --capture=/absolute/path.png`. This uses the actual native raster renderer and shows the selected source plus isolated pigment at the same scale. Compare corner silhouettes, grain, proportions, joins and contrast. A dynamic projected board is not a pixel-identical copy of an entire concept illustration.

The initial blood clock belongs to confirmed province engagement. Never connect it to gold ink reveal progress, hover, selection or resize. Existing compact province spacing, full scans and attachment grouping must remain intact.

Native delivery evidence: `docs/screenshots/battle-escalation/README.md`. The source comparison shows actual extracted pigment, not a simulated UI mockup.
