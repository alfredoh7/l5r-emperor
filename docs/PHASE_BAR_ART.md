# Bitmap phase-bar pass · 0.3.10

The user rejected the vector-like phase track and requested an image or rendered asset matching the original screenshot, with the game's header font. The implementation now displays actual raster artwork for the lacquer, brass frame, rails, ornament and medallions. Musashi Brush supplies all live phase headings. No procedural circles, outlines or rails are drawn.

The first two imagegen variants were rejected during comparison: one had a baked checkerboard and the next introduced excessive thickness and material texture. The selected, narrowly scoped edit removes labels and illumination from the supplied reference while preserving its shallow composition. The original source image supplies the illuminated rail and medallion. The engine selects the required image region, feathers its edges into the base art, crossfades phase changes, and gently modulates its brightness. The imagery stays fixed while the game controls its state.

The generated base includes black outer padding, so the renderer samples the relevant bar region directly. The reference's clipped left corner is completed by reusing the opposite bitmap end-cap, like a textured nine-slice. Maximum displayed width is capped to preserve physical proportions on ultrawide monitors. Labels and hover regions follow the actual medallion positions, and shadow pixels do not intercept the camera buttons below.

This is a 2D raster interface asset, not a newly modeled 3D object. The existing upright lanterns, mat, camera, rules and card presentation remain the prior baseline. The four live phases remain Action, Battle, Dynasty and End; the reference's lettering is not baked into the interface.

## Assets and provenance

- Selected base: `assets/ui/phase-track/lacquer-phase-plate.png` (built-in imagegen)
- Illuminated reference: `assets/ui/phase-track/reference-lit-rail.png` (user-supplied screenshot, original preserved)
- Exact prompt set and iteration notes: `assets/ui/phase-track/prompts.md`
- Runtime copies: `src/L5R.Client/Art/lacquer-phase-plate.png`, `reference-lit-rail.png`
- Live compositing: `src/L5R.Client/Art/phase-art.gdshader`
- Labels, phase state and hover layout: `src/L5R.Client/PhaseTrack.cs`

Native review covers the initial game, a developed battle, 16:10 and ultrawide windows, reduced motion, and a 72-frame glow recording. Logs and screenshots are in `artifacts/phase-art/`. Package resource checks require both bitmap assets and the shader. Mac delivery receives a native launch review; Windows package validation is not a native Windows playtest.

The next recommended work is complete-match and multiplayer acceptance, then the scripted tutorial, followed by additional verified decks. See `docs/NEXT_PHASE.md` for the proposed sequence and completion criteria.
