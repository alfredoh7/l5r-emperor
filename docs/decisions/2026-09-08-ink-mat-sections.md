# Ink-washed mat sections and left-side holdings

Status: visual direction C and left-side holdings accepted by Alfredo; implementation in PR #53 awaits playtest/review.

Issue: https://github.com/alfredoh7/l5r-emperor/issues/52

Alfredo selected the border-only ink-washed concept C, asked for slightly lighter pigment, internal labels and fluid expansion/contraction inspired by the Marco Polo title sequence. Generate border materials only; preserve original card faces. The gameplay mat and its existing lighting remain in place.

## Layout and behavior

- Both players' in-play holdings and Stronghold occupy a separate shelf on screen-left. This is a presentation grouping, not a change to card types or Home-zone rules. Unpurchased Holdings remain in their provinces.
- Other cards at Home occupy a separate central section. A Home Personality does not acquire an artificial permanent province affiliation.
- Province rows retain four stable positions. Existing live province numbers remain visible even when a surviving province has no card. Destroyed locations do not cause renumbering.
- During the existing battle phases, each surviving defending province gets a separate labeled battlefield. Its cards use the engine's actual owner and ProvinceIndex. The current battle receives a warmer ink emphasis.
- Boundaries use resting card footprints, including bowed widths and attachment stacks. Hover enlargement, selection and attachment inspection do not change those footprints.
- Stable section identities preserve animation across normal UI rebuilds. Resizing takes 0.76 seconds; new ink advances around its contour; obsolete ink dissolves. Mid-transition changes start from the current visible bounds. Settled ink does not shimmer continuously.
- Camera control remains manual during layout changes. Fit Table still frames the new whole layout. Live cards slide in the same projected table plane during reflow, retaining stat inlays and unit standards.
- Reduced motion makes border changes immediate. Existing reduced-motion card behavior remains available.
- Musashi labels sit inside reserved header space, rendered crisply at overview zoom. Extremely narrow battlefields abbreviate their province label instead of overlapping neighbors.

The renderer consumes public layout rectangles, labels and an active flag. It never infers legal actions, Force, Gold, payments or battle outcomes. No Core/server/content schema changed.

## Scope

This pass establishes dynamic regions and separates holdings from Personalities. Assignment ghost positions, redesigned battle-resolution sequences and cinematic destruction announcements remain later passes. Rectangular sections grow and repack; the stepped contour shown as a possible construction in the concept sheet is not used by the current layout.

Main implementation: `InkMatSections.cs`, `Main.MatLayout.cs`, `Main.Board.cs`, `Main.Motion.cs`, `TableCardReflow.cs`. Asset provenance and exact prompts: `assets/ui/ink-mat/README.md`.

Native regression entry: `bash tools/check-ink-mat.sh`. It distinguishes real match interactions from isolated renderer-geometry checks. Expert rules acceptance and Windows graphical testing require their respective reviewers/platforms.
