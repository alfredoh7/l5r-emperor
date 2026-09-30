# Gameplay control designs — accepted visual direction

Alfredo approved these combinations after reviewing the six concepts in `assets/ui/gameplay-controls/`:

- Payments: A’s card-origin grouping and independent source methods, with C’s clearer Gold/consequence receipt.
- Choices: B’s integrated candidate tray, A’s compact source-card context and C’s quieter selection cue. Keep the ordinary hand visible.

This accepts presentation only. Mendon/Nathan remain the mechanics authorities; commands, eligible candidates, production, discounts and conflicts come from the engine. A discount is distinct from producing Gold. Choosing a card or source does not commit an action; resource commitments require confirmation.

The implementation retains the existing approved lacquer/brass raster frame and original card faces. Native evidence must describe actual layout differences; concept images are not replacement card scans or a claim of pixel equivalence.

Issue #499 / PR #570. Approval does not authorize merging the game; launchable native and Windows reviews remain required.

The online projection includes the same optional candidate IDs and short labels. Protocol version 3 prevents older strict-schema peers from silently failing to deserialize the added metadata. It changes presentation data, not card rules or payment resolution.

Native checks exercise twelve real-engine seeds at 1440×900 and 1120×700, with explicit selection, confirmation, payment coverage, waiting-seat privacy and selection reset. Prepared scenarios contain training cards alongside the published source cards; they are demonstration seeds, not playable deck additions. Event draw starts at the actual engine pending-choice boundary. Original published scans are used without changes to printings. Screenshots are in `docs/screenshots/gameplay-controls/`; full scans remain available through hover/inspection.

The native tray stays above the visible hand and scrolls horizontally for a large set of candidates. Payment groups and the receipt use the existing right sidebar, with a pinned confirmation footer. The concept images establish the accepted grouping and interaction; the runtime fits the existing responsive shell rather than replacing it with a full-screen illustration.
