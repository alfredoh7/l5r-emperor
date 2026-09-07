# Supplied material: intake report

**Emperor Edition artwork acquisition:** the supplied Oracle search has now been
archived separately: 574 records and 590 master scans at 750 × 1050, including
complete coverage of the numbered base set, 1–428. See the
[acquisition report](EMPEROR_EDITION_ARTWORK.md). The original 44-image inventory
below records the Sun and Moon uploads; the larger download is a separate dataset
for the next client-content integration pass.

**Database addition:** the later Sun and Moon XML archive now provides unique
source-path associations for all 44 scans, plus 12,033 dated card records.
The original inventory below is preserved; its initially unmapped images now
have supplemental mappings in `content/legacy/artwork-mappings.json`. See
[the database report](SUN_AND_MOON_DATABASE.md) for current coverage and limits.

**Additional image archive reviewed on 6 September 2026:** `images.zip` contains
the same 44 scans already incorporated, with identical full source paths,
SHA-256 hashes and 428 × 600 dimensions. All 44 also match the packaged client
images. There are no new scans, changed images or higher-resolution replacements.
ZIP integrity and path checks passed; nine directory entries and 57 macOS metadata
files were skipped. No duplicate assets or rebuild were needed. The archive hash
and per-image comparisons are recorded in
[`content/intake/images-archive-audit.json`](../../content/intake/images-archive-audit.json).

Imported from the user-provided archive and four separate text files. The
original bytes of each imported text and artwork file are preserved under
`assets/reference`; the archive and member SHA-256 hashes appear in
`content/source-manifest.json`. No Sun and Moon code is used.

## Files and identities

- **44 full-card JPEG images**, all **428 × 600**, with 44 distinct content hashes.
- **Nine deck lists labeled Ivory** inside the archive.
- **Four separately supplied deck lists labeled Emperor**.
- **412 distinct source name/version references** across all thirteen decks.
- **2,149 canonical Emperor-tagged metadata records** retrieved from the public
  [Oracle catalogue](https://oracleofthevoid.com/), with request and response provenance.
- All 154 entry lines in the four Emperor-labeled decks resolve to canonical
  records after the explicit spelling alias below. No imported deck is yet playable.

| Supplied deck | Dynasty cards | Fate cards | Stronghold + starting Holdings | Entry lines |
|---|---:|---:|---:|---:|
| Crane Dominance | 40 | 42 | 3 | 38 |
| Crab Control | 40 | 40 | 3 | 41 |
| Unicorn Maidens | 41 | 40 | 3 | 34 |
| Dragon Destruction | 42 | 43 | 3 | 41 |

These are reference partitions using resolved catalogue types. They are not a
legality certificate. The importer preserves quantities as supplied instead of
trimming a Fate/Dynasty deck to 40 cards.

`Utaku Liu Xeung - exp` in Unicorn Maidens resolves to Oracle's
[`Utaku Liu-Xeung - exp`, identity 9085](https://oracleofthevoid.com/#cardid=9085).
This one punctuation difference is explicitly recorded in
`content/oracle/name-aliases.json`; original deck text remains unchanged. Other
unresolved references, principally from Ivory lists searched against an
Emperor-only snapshot, remain visible rather than being guessed.

## Artwork inventory

| Archive folder label | Images |
|---|---:|
| AM | 4 |
| AMoH | 3 |
| GoC | 2 |
| Ivory | 17 |
| Promo | 1 |
| TCW | 8 |
| TwentyFestivals | 9 |

All files retain their supplied paths under
`assets/reference/sun-and-moon/images/cards`. Filename/set-folder matching has
not been used to claim Emperor legality. At initial intake, the
artwork manifest recorded `mapping_status: unmapped` and null card
references; that manifest is preserved, with later source evidence stored separately.
This is a partial collection and is not sufficient artwork coverage
for the supplied decks or the full Emperor catalogue.

## Data boundaries

The archive's instructions, if any, are inert reference content. The import
allowlist includes only text and image formats; macOS metadata and other files
are ignored. The external catalogue is for identity, text and source research.
It does not grant engine support: all decks and catalogue records remain
`playable: false` until the rules engine covers their full behavior.
