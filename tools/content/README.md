# Content intake

This pipeline preserves supplied card scans and deck text as reference material.
It does not reuse Sun and Moon code and does not turn English card text into live
game behavior. Local archive and metadata intake use Python's standard library.
Downloading/validating Oracle scans and running the complete content test suite
also require Pillow and openpyxl (for the card inventory workbook), pinned in
`tools/content/requirements.txt`. `python3 tools/bootstrap.py` reports whether they are
installed but never installs them.

Run from the project root:

```sh
python3 -m pip install -r tools/content/requirements.txt
python3 -m unittest discover -s tools/content -p 'test_*.py' -v
python3 tools/content/import_sources.py \
  --archive '/Users/alfredo/Downloads/Sun and Moon - Mac/Archive.zip' \
  --deck '/Users/alfredo/Downloads/Crane Dominance.txt' \
  --deck '/Users/alfredo/Downloads/Crab Control.txt' \
  --deck '/Users/alfredo/Downloads/Unicorn Maidens.txt' \
  --deck '/Users/alfredo/Downloads/Dragon Destruction.txt'
python3 tools/content/normalize_oracle.py \
  --response assets/reference/oracle/emperor-response.json \
  --retrieved-on 2026-09-06
python3 tools/content/package_client.py
```

The additional Sun and Moon XML archive supplies dated card text and exact
image-path associations. Import it separately before packaging:

```sh
python3 tools/content/import_sun_moon_database.py \
  --archive '/Users/alfredo/Downloads/Sun and Moon - Mac/Sun and Moon.app/Contents/Resources/Data/l5r codex attachment 3.zip'
python3 tools/content/package_client.py
```

`content/legacy/` contains a separate catalogue, source manifest, full identity
crosswalk, artwork-path mappings and the corroborated per-card reference subset.
The XML is preserved byte-for-byte under `assets/reference/sun-and-moon/database/`.
Unity resources and macOS metadata are not extracted. DTD/entity declarations and
unsafe paths are rejected. See the [database intake report](../../docs/rules/SUN_AND_MOON_DATABASE.md)
for matching requirements, conflicts and the distinction between identity and
historical printing verification.

The user-supplied **Emperor Edition printing-set search** is a separate dataset
from the broader **Age of Conquest (Emperor) legality query**. Its complete saved
response and request are under `assets/reference/oracle/emperor-edition/`.
The downloader selects exact `Emperor Edition` printing records, preserving all
their image variants, independently of each card's current primary printing:

```sh
python3 tools/content/download_oracle_printings.py --retrieved-on 2026-09-06
```

This command downloads missing images from the public image host. On repeat runs,
it verifies cached files against their receipts and avoids new image requests.
It uses two download workers, rate pacing, retries and the site's master/detail
image sizes. Full decoding and SHA-256 checks reject invalid or changed files.
The exact source JPEGs remain unchanged. `content/oracle/emperor-edition/` holds
the full reference catalogue, image manifest and numbered-core coverage report.
Proxy numbering is explicitly excluded from the core 1–428 completeness check.
This acquisition dataset is ready for the next client-content integration pass;
the existing native library/package pipeline is unchanged by the download.
See [the acquisition report](../../docs/rules/EMPEROR_EDITION_ARTWORK.md).

Both scripts run offline. The Oracle response was obtained using the public API
implemented by [Oracle's client](https://github.com/Oracle-of-the-Void/ootv-client),
whose maintainer [describes API use for game engines](https://www.patreon.com/oracleofthevoid/about).
The exact request, retrieval date, response hash, and completeness checks live in
the normalized catalogue's `source` object. No external application code is copied.

| Output | Consumer contract |
|---|---|
| `content/source-manifest.json` | Input provenance, SHA-256 hashes, image dimensions, archive paths and source labels |
| `content/reference-card-index.json` | Stable name/version references; null fields mean unknown, not zero |
| `content/decks/*.json` | Original line number, quantity, source era claim, validation issues |
| `content/oracle/emperor-catalogue.json` | Canonical `oracle-l5r-<id>` identities and reference metadata for the saved query |
| `content/oracle/reference-resolutions.json` | Exact title/version matches and unresolved or ambiguous names |
| `content/deck-library.json` | Decks with display names, quantities, canonical IDs, card/deck types and source partitions |

The `cards` arrays are reference catalogues, not the engine's supported-card
registry. Every imported deck remains `playable: false`. `declared_era` is the
source's claim; `catalogue_membership_only` is Oracle's current classification;
historical legality and executable support must be established separately.

The final command creates `src/L5R.Client/Content/library.json` (about 2.8 MB with legacy references)
and copies the 44 original scans into `Content/Art`. It verifies source hashes
and resolved catalogue IDs before packaging. The native client opens the browser
with `LibraryDialog.Show(parentNode)`. JSON is read through Godot `FileAccess`
from `res://Content/library.json`, and artwork is loaded through Godot resources;
exported applications do not need access to the original host paths. The existing
export presets include JSON and all imported resources. Re-run packaging after
changing normalized content, then import the project before exporting. If the
optional legacy mapping/reference files are absent, packaging retains the original
unmapped-artwork behavior. The full legacy catalogue and XML are not bundled into
the game; only the useful reference subset is included.

The library offers deck selection with quantities and identity resolution,
catalogue search across names/clans/keywords/text plus a card-type filter, and
an artwork gallery searchable by source-linked card name and filtered by original
source folder. Gallery details retain filenames, database version and source text.
Canonical card details can expand a corroborated legacy reference alongside
current Oracle text. Library entries remain
separate from the engine's executable card registry. Source links open only when
the user presses the source button; browsing the library itself works offline.

Deck quantities accept `3 Card`, `3x Card`, `3xCard`, or an unquantified single copy.
`- exp` means the explicitly requested Experienced 1 version; `- exp2` means
Experienced 2. No suffix remains unspecified until a unique basic catalogue
entry resolves it. Original lines and spelling are preserved. A narrow reviewed
spelling correction is recorded in `content/oracle/name-aliases.json`, never
applied by fuzzy matching. The scripts do not infer card types from card names.

Archive intake validates paths, rejects symlinks/encryption/oversized inputs,
ignores macOS metadata and executable files, and only imports text and image
formats. JPEG/PNG dimensions are read without executing or decoding embedded
code. The original artwork manifest remains an unchanged intake record. Optional
legacy mappings add dated source-path associations; they do not infer printing
legality or silently replace canonical artwork.

An image's folder label does not establish its edition legality. Oracle's
current text can include later wording; `printings[].text_override` and original
period sources must be reviewed before implementing a historical ability.

## Publishing a card wave

A wave adds its `content/published/<wave>.json`, an `EmbeddedResource` in `src/L5R.Core/L5R.Core.csproj`, a loader in `PublishedContent.cs` and a link in the `CardCatalog.cs` chain. Then run `python3 tools/content/pin_wave_ids.py` and commit the new `tests/L5R.Core.Tests/PublishedWaves/<wave>.txt`, the wave's own card-id manifest. There is no card total to update: `PublishedTests` derives the expected catalogue from `content/published/*.json` and compares each wave with its manifest (see `docs/decisions/2026-09-19-published-count-guard.md`). Change an existing wave's cards deliberately with `pin_wave_ids.py --update`.
