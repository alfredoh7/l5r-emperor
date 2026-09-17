# Duel UI follow-up · September 16, 2026

User-approved sources are copied here as approved-duel-title.png and approved-introductions.png. Issue #205 / PR #220. This follow-up changes presentation and selection feedback only; engine commands and public/private views are unchanged.

## Runtime assets

- Art/DuelUi/duel-wordmark.png: generated with built-in imagegen from the title reference, 2172×724, original output copied unaltered. Top-center overlay during introductions; fades before interactive play.
- Art/DuelUi/focus-frame.png: generated with built-in imagegen from the introduction/material reference, 1024×1536. Runtime AtlasTexture crops the exterior to (30,57,964,1420); live heading text is separate (Musashi Brush after Alfredo’s font-name clarification).
- Satoshi Bold: unmodified official Fontshare OTF with license and provenance in Art/Fonts/Satoshi-README.md.
- Existing shared SidebarSkin lacquer artwork, Caudex and WeatheredQuill serve menu/help/inspection surfaces. Musashi supplies champion names, turn/focus headings and Strike/Continue. Final totals are available in Duel details.

## Exact imagegen prompts

### Wordmark

Use case: background-extraction. Extract ONLY the golden brush-lettered word DUEL and its two graceful rough brush underlines from the top center of the attached approved screenshot. Match the reference exact gold/ivory brush grain, dynamic strokes, capital letters and elegant pointed underline. Output a production UI sprite with genuinely transparent background, tightly framed horizontal approximately 3:1 composition, all four letters fully intact. Text verbatim: DUEL. No scene, clouds, cards, people, frame, black backdrop, shadows, checkerboard or extra lettering. Keep the original title design; isolate it as clean detailed alpha artwork.

### Focus frame

Use case: ui-mockup. Create ONE game UI focus-card landing frame, a portrait rectangle 2:3, straight-on orthographic, on genuinely transparent background outside the frame. First image is the approved worn black lacquer and antique gold material reference; second is the Japanese mat ornament reference. A very thin blackened steel outer rim, restrained finely engraved gold Japanese katana-tsuba filigree with angular martial wave and pine motifs at the corners and a small crossed-blade motif centered along the top edge. Visceral yet refined feudal Japanese craftsmanship. No bolts, screws, coins, ornate European scrollwork, ribbons or thick gold block. Center is deep nearly-black scratched lacquer, quiet and empty with generous space for live two-line text. NO TEXT, letters, numbers or icons in the central area. Crisp visible corners, one single clean complete frame filling nearly the entire canvas, fine silhouette, restrained realistic patina, luxurious AAA game asset. Must read clearly even at 110x150 pixels. Do not include a game scene or cards.

## Intro construction

A disposable SubViewport renders only public participant and attachment copies through the shared CardTile, preserving original scans/current stat overlays. A low-angle perspective camera visits challenger then challenged, applies selective texture blur to the distant stack, and returns to the playable composition. No built-in depth-of-field dependency on the Compatibility renderer. The 3D intro is destroyed on completion/skip/reset; reduced motion bypasses it.

Native evidence is recorded in docs/screenshots/duel-demo/polish/.
