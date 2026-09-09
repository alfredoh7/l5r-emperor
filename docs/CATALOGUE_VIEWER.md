# Catalogue viewer — #120

A's full-card gallery plus B's on-demand inspection panel replaces the old text-list entry. Card Library opens inside the existing window, without starting a game. The imported-deck/source archive remains available under More filters.

## Browsing

Search every word across names, clans, types, keywords and Oracle text. Combine clan, Dynasty/Fate/Strongholds, type, keyword, printing set and engine-support filters. Sort by name, Gold, Force or Chi; absent numeric values sort last. Adjust card size independently. Click again or Escape to dismiss inspection and restore the prior scroll position. Ctrl/Cmd+F focuses search. Dropdowns support incremental keyboard search. The right pane offers full scans, printing selection and expandable rules/provenance. Inspecting an alternate printing never changes the game's supported definition.

## Data and scan delivery

- `Content/library.json`: 2,149 Age of Conquest (Emperor) reference identities. It matches the tracked reference catalogue; no reference entry becomes playable merely because a scan exists.
- `Content/catalogue-printings.json`: 2,485 exact scan variants covering all 2,149 identities, derived from the current original and expansion manifests. `python3 tools/content/package_catalogue.py` regenerates it; normal `package_client.py` also refreshes it.
- Playability comes from non-scenario published definitions in `L5R.Core.CardCatalog`. Supported cards offer their pinned packaged face as **Game printing**. Other scans remain separately named reference variants. This is software support, not historical tournament certification.
- Packaged game faces work offline. Other visible scans load on demand from the receipt's exact HTTPS Oracle image URL, with SHA-256 verification, no redirects, an 8 MiB limit, three simultaneous requests and a bounded queue. Reused textures are limited to 80; only visible rows and a small buffer create controls.
- Verified scans are cached at Godot's `user://catalogue-scans/<sha256>.jpg`. They work offline after loading. More filters offers cached-only mode; unavailable images keep their identity and remain inspectable. No login is required.
- The repository does **not** bundle Mendon's optional bulk cache. To seed offline coverage from a downloaded private scan attachment/existing acquisition checkout, run `python3 tools/content/install_catalogue_scans.py --source /path/to/source-root --cache /path/to/Godot/user-data/catalogue-scans`. The source retains `assets/reference/oracle/<set>/images/` paths. The command validates every receipt and reports missing source files; it does not claim complete coverage when a partial pack is installed.

## Verification

Run `bash tools/check-catalogue.sh` on a native graphical desktop. Captures include gallery, inspection, compact layout, rules, advanced filters, empty results and missing artwork. The probe uses keyboard search and actual mouse coordinates for clan/family/support filters and selection/deselection. It checks that the inspector cannot overlap the grid, that scroll position is restored exactly, and that node/texture counts stay bounded. It also checks metadata and support filtering. Missing-art injection exists only in the probe.

Three Python regressions cover the original/expansion manifest formats, deterministic metadata output without support flags, untrusted image hosts, and offline cache integrity/absence reporting. Existing content, Core, AI, server and transport suites must stay green. Native Windows and expert rules acceptance are separate from these checks.

The material and arrangement were compared to both approved images at 1672 × 941, then inspected at 1280 × 800. Real scanned faces, existing clan emblems and the actual Weathered Quill font intentionally replace Imagegen's illustrative facsimiles. Do not claim a pixel-identical reproduction of those generated card contents.
