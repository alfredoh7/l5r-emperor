# Sun and Moon app bundle: artwork inspection

Inspected on 6 September 2026:

`/Users/alfredo/Downloads/Sun and Moon - Mac/Sun and Moon.app`

The bundle contains additional card-related graphics, but no additional collection
of illustrated deck-card fronts. The 44 previously imported individual card scans
remain the available scan collection.

## What the packed files contain

Read 23 Unity serialized asset/scene files and engine resource files, and decoded
the six associated `.resS` texture streams. All 71 nonempty game textures decoded
successfully and were visually inspected, including the full sprite atlases.

| Game texture category | Texture objects |
|---|---:|
| Blank card frames for Personalities, Strongholds and other card types | 31 |
| Imperial Favor graphics: nine clan versions and one generic template | 10 |
| Dynasty/Fate card backs | 3 |
| Sprite atlases, clan emblems, interface controls, paper and table textures | 27 |
| **Total nonempty game textures** | **71** |

The 68 game Sprite records select regions from these textures; they are not
another collection of full-card scans. Four additional game textures are empty
font placeholders. There are also 39 Unity engine textures, separate from the
game's artwork.

Most card graphics live in `Contents/Resources/Data/resources.assets`, with pixel
data in `resources.assets.resS`. Additional interface textures live in
`sharedassets0.assets`, `sharedassets1.assets`, `sharedassets2.assets`,
`sharedassets5.assets` and `sharedassets8.assets`, with their companion streams.
Blank card frames are 256 × 512. Their illustration and rules areas are empty.

## Loose files

`Contents/Resources/Data/StreamingAssets/Backgrounds` contains ten background
JPEGs. The bundle also contains small Unity/Mono/PhysX logos, nine deck text files,
and the same XML database already imported from the supplied archive. The
database lists image filenames; it does not embed those card images.

## Implication for artwork coverage

These frames, card backs and Favor graphics may be useful as visual references.
They do not fill the missing Personality, Holding, Strategy or other individual
card illustrations. No new game assets or card identities were imported by this
inspection.

The app assembly contains an image-cache path and the image-host string
`https://images.oracleofthevoid.com/l5r/`, plus image-download method names.
Together with the absent scan collection, this supports the inference that
additional card pictures are retrieved externally. This inspection did not test
the old app's live download behavior or the availability of every remote image.

## Evidence

- [Texture preview sheet 1](../../artifacts/asset-inspection/texture-contact-sheet-1.jpg)
- [Texture preview sheet 2](../../artifacts/asset-inspection/texture-contact-sheet-2.jpg)
- [Texture preview sheet 3](../../artifacts/asset-inspection/texture-contact-sheet-3.jpg)
- [Machine-readable inventory](../../content/intake/sun-and-moon-app-inspection.json)
- The inspection script and decoded PNGs are in `artifacts/asset-inspection/`.

Used the documented read-only parsing/extraction APIs of
[UnityPy](https://github.com/K0lb3/UnityPy), installed separately under
`.tools/asset-inspection`, version 1.25.2. Source hashes before and after texture
decoding match. The original app was not run or modified.

One engine-only Sprite named `UnitySplash-cube` has an old-format parsing mismatch
in this reader. All game texture metadata and pixel data parsed successfully;
the engine splash record does not affect the game artwork inventory.
