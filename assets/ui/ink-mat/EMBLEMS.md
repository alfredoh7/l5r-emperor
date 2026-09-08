# Mat emblems and province seals

Created with the built-in ImageGen tool. These are production bitmap assets, not baked card screenshots. Runtime `MatEmblemArt` extracts gold pigment from the explicitly requested black matte, keeps its patina, trims empty margins and generates mipmaps. Live province numbers use Musashi; no labels or numerals are baked into the art.

- `src/L5R.Client/Art/mat-section-emblems.png`: four equally divided atlas cells, Holdings / Home / Provinces / Battlefield.
- `src/L5R.Client/Art/province-seal.png`: ceremonial empty surround, reused under each live province and battlefield. Surviving province identities never move when a neighbor is lost.
- Pointer-transparent icons use the existing bounded reading tooltip after the same hover delay as the board. They never capture card selection or camera input.

## Exact generation prompts

### Section atlas

Use case: stylized-concept
Asset type: production raster sprite atlas for a dark Japanese tabletop card game, not a mockup.
Create ONE 2048x512 sprite strip containing exactly four separate monochrome aged champagne-gold emblems, one per equal 512x512 cell, perfectly centered with generous empty margins. Left to right: 1 a tied bundle of three rice stalks with plump grains (Holdings/economy), 2 a low traditional Japanese residence with one broad curved roof and open doorway (Home), 3 a fortified Japanese gate with two sturdy stone posts and a tiered watchtower roof (Provinces), 4 two crossed Japanese swords (Battlefield).
All four must share the same antique gold ink style: bold immediately recognizable silhouettes, softened dry-brush edges, restrained engraved details, gentle patina and warm pale highlights. Legible when reduced to 24 pixels. No outer discs, no circular frames, no words, no letters, no numbers. Truly pure solid black background RGB 0,0,0 for runtime ink extraction. No checkerboard, no mat, no gradients or shadows outside emblems. Top down flat sprite view, not perspective, no 3D extrusion. Each emblem fully confined to the central 70% of its own cell. These will be printed into fine gold-ink borders on navy cloth.

### Province seal

Use case: stylized-concept
Asset type: production raster ceremonial province number surround for a dark Japanese tabletop card game.
Create one elegant small horizontal ceremonial seal, straight-on flat view. At center is an EMPTY circular black field reserved for a live number. Surround it with an exquisitely aged champagne-brass double ring, engraved with restrained Japanese cloud curls. Two short symmetrical ornamental wings extend left and right from the ring, like a precious insignia fixed into a game mat, with a tiny crown-like three-lobed crest above and a delicate pointed finial below. Overall silhouette approximately 2:1 wide, center circular field half the total height; generous room for one numeral. It must feel regal and worn, with warm pale gold edge highlights, dark patina, tiny scratches, no colorful gems. Keep details broad enough to read at 50px wide. Pure black background RGB 0,0,0, and pure black central field; all visible lines are aged gold, intended for additive ink extraction. No text, no letters, no numbers, no checkerboard, no environment, no cards, no drop shadow. Center the complete ornament with 12% empty margin. 1024x512 landscape bitmap.

