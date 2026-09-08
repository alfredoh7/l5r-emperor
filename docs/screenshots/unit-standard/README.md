# Unit Standard visual review

Approved direction: Alfredo's concept C, navy spear standard + lacquer battle tracker. The game uses the published card scans, not the illustrative card paintings in the concept.

Production art: `src/L5R.Client/Art/unit-standard.png` and `battle-tracker.png`, generated with the built-in imagegen tool using the approved concept as the visual reference. The banner has alpha. The tracker is an atlas: the renderer excludes the generated outer background and samples its real medallions for compact mode. The generated text-free surfaces carry runtime values, header type, and clan-colored halo effects.

Prompts:
- Banner: recreate only the small navy cloth spear banner, aged brass horizontal rod and finials, thin worn gold embroidered edge, three vertical gold spears in the upper quarter; leave lower cloth blank for runtime numbers; flat navy weave, swallowtail bottom; transparent background; no cards, words, numbers, or scenery.
- Tracker: recreate only the worn gold/brass rectangular battle panel, near-black lacquer, subtle engraved Japanese clouds and corner studs; three neutral round medallions (flag, crossed swords, fan) connected vertically; blank header and labels for runtime text; front view, no words, numbers or scenery.

An alternate banner generation was reviewed and rejected; it is not used in the game.

Native captures in this directory are produced by actual Godot components. `comparison.png` is an explicitly illustrative presentation fixture for 2→0 Personality Force, a 5-Force Follower, and 7→5 Unit Force. `battle.png`, `expanded.png`, and `resolution.png` are real published-profile practice states. Fixtures do not change playable content.

Automated geometry checks cover upright/bowed cards, 0/1/3/6 attachments, 0/1/2/3-digit values, viewport containment and collisions. Real-board checks cover default and focused cameras, expanded attachments, native banner click/tooltip, priority labels, assignment and received resolution events, reduced motion, and 1280/1672/1920/2560 widths. Native screenshots are reviewed separately; test success alone is not visual acceptance.
