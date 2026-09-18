# Approved Fallen artwork

Alfredo approved A’s attached counter and B’s inspection panel on 2026-09-16. The unchanged approved PNGs live in `src/L5R.Client/Art/Fallen/`; `concept-prompts.json` retains their provenance.

`FallenArt` samples the original frames, ornamental corners, kabuto, chevron, fan icons, divider diamonds and B’s broken-fan medallion directly. Only plain frame runs resize. Semantic labels and counts are native Caudex; headings use the established uppercase WeatheredQuill.

| Element | Source rectangle (x, y, width, height) |
| --- | --- |
| A counter | 75, 343, 454, 103 |
| A kabuto | 119, 353, 90, 76 |
| A intact fan | 665, 568, 52, 44 |
| A broken fan | 951, 568, 57, 44 |
| B gallery | 567, 94, 1077, 590 |
| B broken medallion | Circle centered 1487, 292; radius 76 |

The source counter’s overlapping tooltip pointer is omitted using an unobscured section of its bottom border. The gallery’s top middle run uses only the first 14 pixels of its source height; the full 22-pixel corners remain intact. This excludes the original title’s letter tips above the live heading. The inherited approved lacquer texture fills resizable interiors; it is not a newly drawn substitute for the source ornaments. Texture resources are held for the lifetime of the renderer, including idle opponent counters.

`honorable-medallion-v1.png` is a built-in image_gen derivative of approved B: an intact ivory/gold fan in the same black lacquer/gold medallion family. It replaces enlargement of a tiny source icon. The exact prompt, tool mode and output path are in `honorable-medallion-prompt.json`. Generated source: `exec-d1b93f5d-1bb2-4867-b6a7-3014ee2e5754.png`; 1254 × 1254 RGBA, preserved unchanged with alpha. Godot generates mipmaps for its small UI use. This is a 2D sprite, not a new 3D frame.

The native/source comparison is `docs/screenshots/fallen/comparison.png`. Complete period card scans deliberately replace the cropped/illustrative card faces in imagegen. Live fonts, card aspect ratios, pile contents and responsive scaling make this a faithful implementation with actual source ornaments, not a claim of exact full-screen pixel identity.
