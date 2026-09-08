# Stat-token art source

`approved-concept.png` is the user-approved imagegen mockup: warm amber Force discs, cool gray Chi discs and light gray numbers. Its card text/values are illustrative and are not playable content.

The production sheet at `src/L5R.Client/Art/stat-token-sheet.png` was generated from that reference on 2026-09-08 using the built-in imagegen tool. Prompt: extract exactly two blank circular physical metal tokens, amber Force and gray pewter Chi, preserving aged grain, double rims, subtle bevels, patina and top-left illumination; remove labels/numerals for live rendering; no cards or tabletop.

The returned 1536×1024 RGB sheet has a checker-pattern background rather than alpha. Runtime `StatTokens` samples only the metal discs with a circular UV mesh (centers 414,488 and 1123,488; radius 345). Keep mipmaps enabled. Do not use the entire rectangle as a sprite. Labels and numerals use the game font at runtime, with outlines and size adjustment for contrast and multiple digits.
