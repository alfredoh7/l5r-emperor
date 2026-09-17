# Duel result and mat refinement

Accepted 2026-09-16 in issue #205 / PR #220: A's quiet verdict, card inspection consistent with the main game, and on-demand aftermath. The user explicitly replaced concept coins with Musashi Brush numerals. Values and headings remain live UI, never baked into artwork. Final numbers are engine-provided resolved totals; the stat tooltip and aftermath retain the stat contribution breakdown.

`approved-layout.png` preserves the approved composition source, including the now-superseded coin treatment. Runtime `campaign-mat-filigree.png` was derived with built-in imagegen from that source and the original campaign mat, replacing the angular border with Japanese floral/wave ornament. This is a derived texture, not an exact screen match. See `provenance.json` for the exact prompt.

`DuelScreen` reuses `CardTile.CreateReadingPreview` / `CardPreview.Create` directly, preserving the main game's full card face, rules typography, materials and private-view boundary. No separate duel inspector skin exists.

The mat camera recedes farther; all four corners remain on screen. Cards and piles sit within its border; the result footer sits below. Result portraits are retained public duel history, not resurrected/moved gameplay cards. Actual Fate discards and fallen cards remain separate inspectable piles. The hand retracts only once results can be continued; aftermath reaction hands remain available.
