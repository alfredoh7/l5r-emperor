# Duel result and mat refinement

Accepted 2026-09-16 in issue #205 / PR #220: A's quiet verdict, card inspection consistent with the main game, and on-demand aftermath. The user explicitly replaced concept coins with Musashi Brush numerals. Values and headings remain live UI, never baked into artwork. Final numbers are engine-provided resolved totals; the stat tooltip and aftermath retain the stat contribution breakdown.

`approved-layout.png` preserves the approved composition source, including the now-superseded coin treatment. Runtime `campaign-mat-filigree.png` was derived with built-in imagegen from that source and the original campaign mat, replacing the angular border with Japanese floral/wave ornament. This is a derived texture, not an exact screen match. See `provenance.json` for the exact prompt.

`DuelScreen` reuses `CardTile.CreateReadingPreview` / `CardPreview.Create` directly, preserving the main game's full card face, rules typography, materials and private-view boundary. No separate duel inspector skin exists.

The mat camera recedes farther; all four corners remain on screen. Cards and piles sit within its border; the result footer sits below. Result portraits are retained public duel history, not resurrected/moved gameplay cards. Actual Fate discards and fallen cards remain separate inspectable piles. The hand retracts only once results can be continued; aftermath reaction hands remain available.

## Centered verdict approved 2026-09-17

`approved-centered-verdict.png` supersedes the older layout above for the held result. Source: `/Users/alfredo/.codex/generated_images/01a09ee0-1ef5-78f1-918b-fbe2432918cf/exec-a689d256-b47e-437b-b25e-9a8bfb29de2c.png`. User selected the final composition with concept #3's summary shadow and winner glow, centered text and no gray consequence icons. The native screen uses the existing approved mat, red stroke, original published scans and live fonts. Shader opacity creates the soft shadow and warm number light; no new ornamental artwork is approximated. Dynamic counts and longer engine explanations change spacing from the single-card concept.
