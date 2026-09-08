# Tabletop HUD refinement — issue 34

User-supplied art is preserved in `src/L5R.Client/Art/clan-reference.png` (seven Clan marks) and `koku-reference.png` (the complete top coin). Runtime atlas regions exclude the cut-off coin. The native `aged-emblem.gdshader` desaturates and warms the supplied artwork without inventing replacement sigils. The original alpha is preserved; white surrounding the coin and inside its central hole is removed by the shader.

Table tools and the Honor fan are custom native engraved motifs (`TableIconButton.cs`), not Unicode glyphs. Force/Chi use the existing approved metal bitmap and live Musashi Brush numerals. No new image-generation assets were needed for this pass.

This change follows pending PRs 32 and 33. Its comparison branch is PR33, so the diff includes the not-yet-merged token dependency. The playable combined preview additionally includes menu PR9; it does not imply any of these PRs have merged.
