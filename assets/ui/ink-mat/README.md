# Ink-washed mat borders

Approved direction: C from the border-only concepts, selected by Alfredo on 2026-09-08. Runtime artwork is slightly brighter champagne/ochre to remain readable on the navy mat. The PNGs were generated using the built-in imagegen tool; no game card artwork was generated or replaced.

## Sources

- `approved-concept-C.png`: generated concept selected in conversation.
- `src/L5R.Client/Art/mat-ink-edge.png`: original generated edge bitmap (alpha retained).
- `src/L5R.Client/Art/mat-ink-cloud.png`: original generated cloud bitmap (alpha retained).
- Marco Polo title reference: https://www.youtube.com/watch?v=6onwcVx8BDg . Muted browser playback sampled the opening through 0:50 of 1:37. Observed directional uneven ink fronts, dense cores/transparent fringes, substrate grain, movement that settles. This implementation takes those motion properties as inspiration; it does not reproduce the title artwork.

The renderer crops transparent padding non-destructively through AtlasTexture regions using a small alpha threshold, since generated files contain stray almost-transparent pixels. Decorative motifs retain fixed size; edge grain repeats rather than stretching the ornament. The latest follow-up replaces permanent box labels with generated gold emblems and hover names; ceremonial live province numbers sit beneath their cards. See [EMBLEMS.md](EMBLEMS.md) for the exact production prompts and rendering details. Corner ornaments are capped at 54 world units (half the initial implementation size). The reference sheet is not a production atlas.

## Exact generation prompts

### Edge

Create one production game UI bitmap asset on a genuinely transparent background with alpha: a long HORIZONTAL aged champagne-gold sumi-ink brushstroke, slender continuous pigment core, soft organic feathering and dry-brush pores along edges. It should look like luminous warm ochre ink absorbed into cloth, not metallic gold leaf or a vector line. Worn sophisticated old Japanese ink, slightly brighter champagne than dull bronze, tiny pale gold grains, no glow. This is a repeatable edge texture for dynamic section borders on a navy play mat. Image landscape extremely wide about 8:1 if possible; one single stroke occupies central horizontal band, extends fully from left edge to right edge with EVEN thickness and repeatable endpoints, NO tapered point on either end, plenty of transparent margin above/below. Actual ink width approximately 15 percent of image height; very thin translucent bleeding around it. No text, no frame, no paper background, no checkerboard pattern, no extra splashes or objects. Rich physical pigment texture and fine detail, suitable for being drawn 3-5 pixels wide on a game table.

### Cloud

Production bitmap sprite for a Japanese tabletop game's subtle ink-washed section borders. A SINGLE small horizontal Japanese cloud-scroll flourish drawn in worn champagne-gold sumi ink, like the elegant little cloud curls on an antique Japanese textile. Genuinely transparent background with alpha. Three flowing rounded curls combine into ONE compact horizontal motif, tapering softly at right into a short fine brush line. Pigment is brighter warm pale ochre/champagne gold with dry brush pores and translucent feathering, not opaque metal or metallic hardware, no shadow or glow. Directly flat top-down, no perspective. Motif should span most of image width with transparent padding, approximately 2.5:1 width to height. This will be shown about 35 world units wide in a navy play mat, so bold readable silhouette but restrained fine grain. No frame, no text, no sample cards, no coins, no extra motifs, no paper or cloth background, no checkerboard. One isolated cloud accent only.

Weathered lantern artwork, exact prompt and runtime compositing details: [LANTERNS.md](LANTERNS.md).
