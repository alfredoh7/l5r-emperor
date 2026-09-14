# Approved battle presentation

User selection: Pass 1 **B · Shared battle court**, Pass 2 **B · Ink stepping stones**, Pass 3 **A + B · fractured and extinguished**. These raster concepts are approval references, not gameplay screenshots.

The runtime retains the existing full card scans and approved C province thresholds. `BattleInkArt` extracts the neutral B court frame and sword medallion from `pass-1-battlefields.png`, and an ink step from `pass-2-assignments.png`. It removes cool background cloth from the warm pigment and stretches straight runs only. It does not generate substitute card artwork. The selected court receives a restrained cool tint through the ink shader.

`fallen-threshold.png` is a dedicated blank-number sprite derived from the approved resolution reference. The runtime adds the actual province number; the low cracked marker replaces the unavailable card bay. Native gameplay evidence is in `docs/screenshots/battle-presentation/`.

Dedicated fallen sprite prompt:

```text
Create a production game UI sprite based on the LOWER LEFT fractured gold province threshold in the reference. Single isolated low Japanese ceremonial roof/torii threshold with a circular medallion centered, warm weathered gold pigment and thin brass detail, visibly fractured by a narrow irregular black crack through the medallion and right roof, like reference A. Desaturated, extinguished tarnished gold like reference B. Medallion EMPTY, NO numeral or writing anywhere; game renders its own numeral. Preserve reference silhouette and decorative style faithfully. Very wide and shallow 4:1 object aspect ratio, object centered on a SOLID pure navy background #071320, no scene, no card, no board, no text, no shadow outside the object, no second sprite. Crisp detailed high resolution worn pigment; no photoreal thick wood, no new ornament. Object occupies about 85% of image width and 45% image height. Output landscape image. Reference is style/shape input; only output the single threshold sprite.
```

Implementation and review: issue #75 / PR #76. The source image sheets are not a promise that a dense, perspective game board will have identical pixel geometry to a concept sheet. The invariant is the same artwork, visual hierarchy and interaction semantics, tested on real game states.
