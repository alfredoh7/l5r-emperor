# Ink-washed mat sections and left-side holdings

Status: visual direction C and left-side holdings accepted by Alfredo; implementation in PR #53 awaits playtest/review.

Issue: https://github.com/alfredoh7/l5r-emperor/issues/52

Alfredo selected the border-only ink-washed concept C, asked for slightly lighter pigment, internal labels and fluid expansion/contraction inspired by the Marco Polo title sequence. Generate border materials only; preserve original card faces. The gameplay mat remains in place. The follow-up moves and weathers the two lamps and strengthens coordinated candlelight on the mat and cards.

## Layout and behavior

- Both players' in-play holdings and Stronghold occupy a separate shelf on screen-left. This is a presentation grouping, not a change to card types or Home-zone rules. Unpurchased Holdings remain in their provinces.
- Other cards at Home occupy a separate central section. A Home Personality does not acquire an artificial permanent province affiliation.
- Province rows retain four stable positions. Live province numbers use an aged ceremonial surround beneath the resting card slot, with reserved footer space, and remain visible even when a surviving province has no card. Hovering the card never moves its seal. Destroyed locations do not cause renumbering.
- During the existing battle phases, each surviving defending province gets a separate icon-marked battlefield. Its cards use the engine's actual owner and ProvinceIndex. The current battle receives a warmer ink emphasis.
- Boundaries use resting card footprints, including bowed widths and attachment stacks. Hover enlargement, selection and attachment inspection do not change those footprints.
- Follow-up accepted direction: the settled border is retained, but its rectangle must not visibly stretch with moving cards. Obsolete ink dissolves at its old location. New ink waits for actual card flights, table reflow, bowing and pile settling, then a 0.16-second quiet pause, before spreading organically from opposing corners over 1.35 seconds. A fresh layout supersedes a pending reveal; unchanged redraws do not restart it. Settled ink does not shimmer continuously.
- Camera control remains manual during layout changes. Fit Table still frames the new whole layout. Live cards slide in the same projected table plane during reflow, retaining stat inlays and unit standards.
- Reduced motion makes border changes immediate. Existing reduced-motion card behavior remains available.
- Latest steering supersedes the requested smaller serif labels: permanent box wording is replaced by matching aged-gold bitmap emblems (rice sheaf for Holdings, residence for Home, fortified gate for Provinces, crossed swords for battlefields). Names appear through the existing reading tooltip on hover. The invisible hover regions never consume input. Emblems fade with the ink; battlefield destination numbers use the ceremonial surround at the bottom as well. Province numbers remain live Musashi text. Corner curls are half their former size and remain in the padding.

- Lanterns sit near the bottom-left and top-right mat corners with margin. A weathered material plate reduces their pristine finish. A shared irregular candle-energy curve drives each lamp and its spill onto fabric and nearby cards; the metal silhouette stays rigid. Reduced motion freezes flame motion and lighting.

The renderer consumes public layout rectangles, labels and an active flag. It never infers legal actions, Force, Gold, payments or battle outcomes. No Core/server/content schema changed.

## Scope

This pass establishes dynamic regions and separates holdings from Personalities. Assignment ghost positions, redesigned battle-resolution sequences and cinematic destruction announcements remain later passes. Rectangular sections grow and repack; the stepped contour shown as a possible construction in the concept sheet is not used by the current layout.

Main implementation: `InkMatSections.cs`, `Main.MatLayout.cs`, `Main.Board.cs`, `Main.Motion.cs`, `TableCardReflow.cs`. Asset provenance and exact prompts: `assets/ui/ink-mat/README.md`.

Native regression entry: `bash tools/check-ink-mat.sh`. It distinguishes real match interactions from isolated renderer-geometry checks. Expert rules acceptance and Windows graphical testing require their respective reviewers/platforms.
