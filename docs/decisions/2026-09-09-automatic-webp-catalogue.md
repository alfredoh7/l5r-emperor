# Automatic WebP catalogue artwork

Accepted by Alfredo on 2026-09-09, follow-up to #120 / PR #124.

All catalogue images use WebP quality 85 with unchanged pixel dimensions. The complete optimized catalogue is a mandatory part of the game download, not an optional offline mode. Development launchers install the pinned private release automatically; exported games bundle it and never ask the player for credentials. Browsing must not fetch JPEGs from Oracle or depend on a live connection.

Original image receipts, original scans for publishing/provenance, rules and pinned printings remain intact. Every optimized image records its source hash and output hash. A missing new printing is a packaging error, never an invisible unsupported card or a silent substitute printing. Card definitions and playability remain authoritative in Core.

This follows Alfredo's review of three unchanged-resolution JPEG/WebP-85 comparisons. Space savings were 66–89% for those samples. Faster download transfer is expected from fewer bytes; no proportional menu-speed claim is made. Full-pack size and native/exported validation belong in the task handoff.

Sources: `CatalogueScans.cs`, `Content/catalogue-art.json`, `tools/ensure_catalogue_art.py`, `tools/content/build_catalogue_art.py`. Publisher ZIPs remain private Release assets and are not committed.
