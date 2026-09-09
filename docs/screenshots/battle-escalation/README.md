# Native battle escalation review — 2026-09-09

These are real native Godot Mac captures from published Lion/Crane practice matches, not generated gameplay images. Selected concept sources and derivation prompts are in `assets/ui/battle-escalation/`. The source comparison itself uses the actual runtime renderer on an inspection canvas.

- [Source / runtime comparison](source-comparison.png): original corner pigment at equal scale, live court material, Daisho, broad arrow and frayed perimeter. Corner proportions and source texture remain raster art; only plain runs resize. The right court is orthographic inspection, not the perspective game table.
- [Assignment preview](assignment-preview.png): proposed paths and landing positions, with no blood before confirmation.
- [First absorption](blood-first.png), [soaking](blood-soaking.png), [settled courts](blood-settled.png): same confirmed attack, taken at approximately 0.25 seconds, 0.8 seconds and after settling. The last shot uses the normal Focus Battle camera for detail.
- [Blood animation recording](blood-first-attack.mp4): fixed 24 FPS native capture; the test uses a closer camera to make absorption visible. It submits a real legal assignment. No interpolation or generated frames.
- [Named action defeat](named-defeat.png): Crushing Strength destroys a real attachment. The tracker says Applying battle action and the public journal records Fate discard.
- [Named battle death](battle-death.png): real resolution destroys a Personality and attachments; the journal distinguishes Dead from Fate discard and surviving units returning Home bowed.
- [Aftermath / frayed provinces](aftermath.png): original slots and live numbers survive; empty frames cannot receive assignments.
- [Reduced motion](reduced-motion.png): settled art and textual outcome without waiting for animation or a pulsing border.
- [Ultrawide camera](ultrawide.png): 2560×1080 native launcher and camera/input verification.

Validation: 275 deterministic checks passed (37 collaboration, 107 content, 68 core, 33 published, 8 deck format, 13 AI, 5 server, 4 transport). Core/published/deck were repeated after adding the final public card identity used for aftermath thumbnails. Client build/import and all eight cases in `tools/check-battle-presentation.sh` passed, including native journal wheel/click routing and no accidental camera/state changes. The development launcher was refreshed, its plist/executable/checkout path checked, and its real shell entry used for the ultrawide native capture.

Native Windows graphical playtesting, expert rules acceptance and real network burst/reconnect cinematic behavior are not claimed. Local vulnerability-feed NU1900 warnings did not prevent compilation. GitHub CI is separate from native gameplay evidence. Audio remains a future pass.
