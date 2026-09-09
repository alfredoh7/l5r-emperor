# Compact layout and source-derived battle court — 2026-09-09

Follow-up to issue #75 / PR #76. Real native Godot captures, not imagegen mockups.

- `compact-board.png`: normal Dynasty play restores 218-world-unit province pitch and table-facing Home rows. Holdings remain left.
- `connected-court.png`: actual battle, attachments and bowed Home units; the source-derived neck connects the court to its province.
- `opponent-court.png`: opposite-seat court at 1280×800.
- `source-comparison.png`: approved B source and actual renderer at matching court/neck scale. The live province retains previously approved C artwork; this is not a claim of full-screen pixel equivalence. The source includes illustrative card silhouettes; the renderer comparison intentionally omits cards.

Native validation: six battle fixtures (compact ordinary play, court, assignment, opposite defense, province fall and reduced-motion fall); ink lifecycle and native hover/select/deselect/attachment-tray checks; ultrawide camera and HUD input checks. Client compilation passed. The integrated deterministic suite passed 273 tests (37 collaboration, 107 content, 66 core, 33 published, 8 deck, 13 AI, 5 server, 4 transport). A final court capture passed after small ornamental border alignment corrections.

The 35% shorter ink timing, assignment previews and fractured/extinguished province presentation are retained. Screenshots are separate deterministic scenarios, not identical-match before/after images. Native Windows playtesting and expert rules acceptance remain outside this validation. Mendon shared review is required before merge.
