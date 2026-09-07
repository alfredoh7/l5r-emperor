# Additional Sun and Moon database intake

The supplied `l5r codex attachment 3.zip` contains one useful XML database,
`StreamingAssets/Database/database.xml`, one Unity built-in resource and macOS
metadata. Only the database is incorporated. The original uploaded ZIP is not
modified or deleted, and no Sun and Moon executable code is used.

The database declares:

> Oct 4, 2015 Kamisasori no Kaisho (Complete), updated Nov 28, 2017 by Kempy 1.3.4

This is a source-declared version, not independent verification of its contents
or the correct rules text for an earlier Emperor printing.

## Useful coverage

| Source evidence | Count |
|---|---:|
| Card records with unique source IDs | 12,033 |
| Image-path references across printings | 16,681 |
| Records carrying the source's Emperor label | 2,165 |
| Previously uploaded scans associated with exactly one XML image entry | 44 / 44 |
| Strictly corroborated links to the existing Oracle identities | 1,883 |
| Same-name candidates retained for further review | 226 |
| Source records without an exact display-name match in this Emperor-only snapshot | 9,924 |
| Same-name records with conflicting shared statistics | 8 |
| Ambiguous image paths elsewhere in the database | 4 |

The archive contains no additional card-image files. Its image references make
the existing 44 scans much more useful: filenames such as `AM009.jpg` now have
source-linked names such as **Exotic Market**, with edition labels and dated
source text. All 44 original image hashes remain unchanged.

## Matching boundaries

Artwork is associated through the **complete XML image path**, not a guessed
card name, shared basename, database ID or inferred set abbreviation. All supplied
paths are unique in the XML. The four ambiguous paths elsewhere stay recorded as
conflicts and do not affect these 44 scans.

A canonical identity link additionally requires an exact normalized display
name, matching card type and experienced notation, a unique source title/type,
the source's Emperor label, agreement on shared recorded statistics, and at least
one matching numbered Oracle printing using an explicit reviewed set-code map.
Candidates do not populate canonical client references. Source IDs remain
distinct from Oracle IDs.

Of the 44 scans, 27 source records have such corroborated canonical identities,
one remains a candidate, and 16 have no exact match in the Emperor-only snapshot.
Only 25 individual image paths have numbered-printing corroboration. A resolved
card identity does not resolve every one of its image printings:

- `AMoH042.jpg` identifies **Defensive Formation**, but its filename number does
  not agree with the Oracle printing number. Its image printing remains unverified.
- `TCW050.jpg` identifies **Ritual Preparation**; that printing lacks a matching
  recorded number. Another source printing can corroborate identity without
  validating this image's printing.
- `Promo564.jpg` identifies **The Topaz Dojo**, but an unnumbered/unreviewed Promo
  printing is insufficient for the strict canonical link.
- `TCW` is explicitly mapped to **The Currency of War**, not The Coming Storm.

Old versions of Bayushi Kachiko, Isawa Tsuke and Shiba Tsukimi demonstrate why
names alone are unsafe: their names match later records while statistics and
source eras disagree. Other conflicts include focus values on Running Battle,
Fair Warning and Aberrations. Those values remain separate source observations;
neither source is silently declared correct.

## Stored and displayed data

- `assets/reference/sun-and-moon/database/database.xml`: original bytes.
- `content/legacy/source-manifest.json`: ZIP/XML hashes, source version, counts and ignored members.
- `content/legacy/sun-and-moon-catalogue.json`: all normalized records, original text, string-valued statistics, images and source labels; never executable.
- `content/legacy/canonical-links.json`: full matching evidence, candidates, statistic conflicts and per-image printing issues.
- `content/legacy/artwork-mappings.json`: exact associations for the supplied scans, without claims of verified image printing or legality.
- `content/legacy/canonical-card-references.json`: the corroborated subset for optional comparison in the client.

The native library retains the 2,149-card Oracle pool and all 13 supplied decks.
It adds searchable source-linked artwork names and dated database text, plus a
collapsible legacy reference alongside current Oracle text where corroboration
passes. The entire XML and 12,033-record catalogue are kept outside the game
package. All imported cards and decks remain non-playable until their abilities
and historical legality are implemented and reviewed.

## Reproduce

```sh
python3 tools/content/import_sun_moon_database.py --archive '/path/to/l5r codex attachment 3.zip'
python3 tools/content/package_client.py
python3 -m unittest discover -s tools/content -p 'test_*.py'
```

Import is offline and validates paths, limits, duplicate IDs and XML structure.
It rejects DTD/entity declarations and never runs instructions contained in the
database. Repeating intake preserves the original source and deterministic outputs.
