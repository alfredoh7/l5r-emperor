# Catalogue viewer — #120

A's full-card gallery plus B's on-demand inspection panel replaces the old text-list entry. Card Library opens inside the existing window, without starting a game. The imported-deck/source archive remains available under More filters.

## Browsing

Search every word across names, clans, types, keywords and Oracle text. Combine clan, Dynasty/Fate/Strongholds, type, keyword, printing set and engine-support filters. Sort by name, Gold, Force or Chi; absent numeric values sort last. Adjust card size independently. Click again or Escape to dismiss inspection and restore the prior scroll position. Ctrl/Cmd+F focuses search. Dropdowns support incremental keyboard search. The right pane offers full scans, printing selection and expandable rules/provenance. Inspecting an alternate printing never changes the game's supported definition.

## Data and scan delivery

- `Content/library.json`: 2,149 Age of Conquest (Emperor) reference identities. It matches the tracked reference catalogue; no reference entry becomes playable merely because a scan exists.
- `Content/catalogue-printings.json`: 2,485 exact scan variants covering all 2,149 identities, derived from the current original and expansion manifests. `python3 tools/content/package_catalogue.py` regenerates it; normal `package_client.py` also refreshes it.
- Playability comes from non-scenario published definitions in `L5R.Core.CardCatalog`. Supported cards offer their pinned packaged face as **Game printing**. Other scans remain separately named reference variants. This is software support, not historical tournament certification.
- **Every catalogue face, including the pinned game printing, uses WebP quality 85 at its original resolution.** The complete mandatory artwork pack is installed before a development launch and bundled inside exported games. There is no in-game download choice, cached-only switch, JPEG fallback or HTTP client in the viewer.
- `Content/catalogue-art.json` ties each optimized image hash and dimensions to its original receipt hash; `content/catalogue-art-pack.json` pins the complete private release archive. Original scan receipts and authoritative printing data remain unchanged. Original scans remain archival sources, not runtime catalogue downloads.
- `tools/run.sh` and `tools/export.sh` invoke `tools/ensure_catalogue_art.py` automatically. It verifies and reuses the installed/shared pack, downloads WebP only when missing or damaged, and installs atomically. Existing valid artwork survives a failed update. New or changed printings without optimized coverage fail packaging explicitly.
- Development clones use their existing GitHub CLI repository access for the first private-release download. Exported games already contain the entire pack; players need no GitHub sign-in or internet connection to browse. Bulk artwork stays out of Git.
- The viewer reads individual WebP members from the local ZIP, verifies their optimized hashes/dimensions, decodes with three workers, and keeps a bounded 80-texture cache. It does not load the complete catalogue into GPU memory. Virtual rows still bound the number of card controls.

The first complete pack contains **2,486 images**, including pinned game faces: **340,657,202 bytes (325 MiB)** versus **1,922,867,169 bytes (1.79 GiB)** of source JPEGs, about **82% smaller**. It covers all 2,149 catalogue identities and their indexed variants.

## Updating the artwork pack

This is an agent/publisher task, never a player preference. Run `tools/content/build_catalogue_art.py --cache PATH --output artifacts/L5R-Catalogue-WebP85.zip --source ORIGINAL_CHECKOUT`. It requires `cwebp` and the pinned Pillow dependency, verifies originals, preserves their dimensions, encodes at quality 85, and refuses incomplete packs. Verified intermediate sources/encodes are cached for a resumed build. Publish the resulting archive to the versioned private release named in `content/catalogue-art-pack.json`; commit the matching metadata, never the ZIP. For a new edition of the pack use a new release tag, preserve previous releases, and test the automatic installer and exported game. `--archive PATH` installs a locally prepared pack for publisher validation before release.

The old `install_catalogue_scans.py` helper is archival tooling only; the catalogue no longer displays its JPEG cache. After a WebP is verified and decoded, the viewer can retire that exact hash-matching old cache entry. Unrelated files, mismatched contents and archival/published source images remain untouched.

## Verification

Run `bash tools/check-catalogue.sh` on a native graphical desktop. Captures include gallery, inspection, compact layout, rules, advanced filters, empty results and missing artwork. The probe uses keyboard search and actual mouse coordinates for clan/family/support filters and selection/deselection. It checks that the inspector cannot overlap the grid, that scroll position is restored exactly, and that node/texture counts stay bounded. It also checks metadata and support filtering. Missing-art injection exists only in the probe. The probe also requires complete optimized coverage, loads pinned faces from the WebP pack and rejects a deliberately corrupted ZIP member without an HTTP fallback.

Python regressions cover original/expansion manifest formats and provenance, mandatory pack integrity, atomic repair, offline relaunch, untrusted release metadata and missing optimized coverage. Existing content, Core, AI, server and transport suites must stay green. Native Windows and expert rules acceptance are separate from these checks.

The material and arrangement were compared to both approved images at 1672 × 941, then inspected at 1280 × 800. Real scanned faces, existing clan emblems and the actual Weathered Quill font intentionally replace Imagegen's illustrative facsimiles. Do not claim a pixel-identical reproduction of those generated card contents.
