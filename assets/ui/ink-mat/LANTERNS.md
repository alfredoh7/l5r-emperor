# Weathered corner lanterns

September 2026 UI follow-up to issue #52 / PR #53.

The built-in imagegen tool edited the existing tavern lantern to produce `src/L5R.Client/Art/tavern-lantern-weathered.png`. The original lantern's accurate RGBA silhouette remains the runtime alpha source. The generated edit returned RGB with a neutral checkerboard, including after a transparency retry; it is therefore used as a warm material/patina plate, not directly drawn as an opaque sprite. The runtime shader retains original pixels for neutral background samples and blends the weathered colored material inside the original silhouette. Both textures use mipmaps to avoid sparkling at table zoom.

Lantern anchors are bottom-left and top-right inside the mat corners; camera pan/zoom retains table anchoring. `candlelight.gdshaderinc` supplies the same independent candle-energy curves to each lantern, its fabric light and nearby card surfaces. Flame movement is localized inside the lantern; the metal structure remains rigid. Reduced motion freezes all of these effects.

## Exact built-in prompt

Use case: precise-object-edit. Asset type: isolated transparent raster sprite for an atmospheric Japanese tavern card game. Edit target: supplied lantern. Preserve exactly its upright silhouette, three-quarter camera angle, centered framing, complete handle/roof/feet, proportions, candle placement, and the transparent background. Change only the material condition: make this visibly timeworn, soot-darkened and humble, with dull oxidized bronze, rubbed aged edges, fine scratches, accumulated soot around the upper vent, dusty crevices, small dents, stained amber paper/glass and uneven wax drips. Reduce the shiny polished gold highlights. Warm dim candlelight remains inside, brightest only near the flame; no external bloom or floor/shadow baked outside the sprite because real-time shaders add those. Preserve readable structure, not ruined beyond use. No extra objects or text. Genuine alpha transparency; no checkerboard or solid background. Full lantern with transparent breathing room.

## Transparency retry (not selected)

Use case: background-extraction. Edit target: the supplied weathered lantern. Keep the lantern itself completely unchanged. Remove the checkerboard background and replace it with genuine transparent alpha pixels, including all openings in the ring handle. This must be an RGBA transparent cutout sprite, not a painted checkerboard, not white, not black. Preserve the full silhouette, proportions, patina and lighting. No shadow outside the object. Deliver actual alpha transparency.
