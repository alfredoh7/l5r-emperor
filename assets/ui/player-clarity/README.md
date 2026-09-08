# Player clarity artwork

Approved direction: [A — Inlaid strips](A-inlaid-strips.png). B and C are archived alternatives, not active designs. Generated concept card text and numbers are illustrative; shipped card faces remain the period scans.

Production assets:
- `src/L5R.Client/Art/stat-inlays-v2.png`: two-square amber/graphite sprite atlas. Replaces the rejected narrow, busy v1 strip material. Quiet recessed centers, shallow aged frames, dynamic numerals rendered by the client. No values are baked into the asset.
- `src/L5R.Client/Art/clan-mantis.png`: aged gold Mantis relief.
- `src/L5R.Client/Art/clan-spider.png`: aged black Spider relief.

All images were generated with the built-in image_gen tool, using the user's sigils/existing emblems or approved concept as references. `prompts.json` records emblem/concept iterations; `inlay-v2-prompt.json` records the final production atlas prompt. `inlay-prompts.json` records the rejected first strip-material attempt, retained as provenance, not the shipped artwork.

Mipmaps are enabled on production assets for the small on-board presentation. Artwork is committed to the private repository; no runtime dependency on a personal generated-images folder.
