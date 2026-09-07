# Emperor Edition: verified artwork acquisition

Downloaded on 6 September 2026 from the
[Emperor Edition printing-set search supplied by the user](https://oracleofthevoid.com/#search=type_printing_set%3Dselect%26field_printing_set%3DEmperor%2520Edition%26table%3Dl5r%26sort%3D%255B%257B%2522title.keyword%2522%253A%257B%2522order%2522%253A%2522asc%2522%257D%257D%255D).

**The search covers all 428 numbered Emperor Edition cards. Every number from
1 through 428 has its corresponding master scan saved locally.**

## What was downloaded

| Coverage | Result |
|---|---:|
| Complete search results / exact Emperor Edition printings | 574 |
| Numbered core cards, 1–428 | 428 |
| Additional unnumbered records with game-card types | 3 |
| Proxy records | 21 |
| Other records, including biographies, rules/reference material and Koku | 122 |
| Master image files | 590 |
| Image resolution | 750 × 1050 each |
| Total original image bytes | 638,679,076 (about 639 MB) |
| Missing images / failed downloads | 0 |
| Missing or ambiguous numbered core cards | 0 |

Two supplemental records, **Experience a Personality** and **The Imperial Favor**,
each reference nine image variants. Those variants explain why there are more
images than card records. All 590 image files have distinct SHA-256 hashes.
The three unnumbered game-type records are Rocky Terrain, The Governor's Estate
and The Killing Grounds.

## Where the files are

- [Original master scans](../../assets/reference/oracle/emperor-edition/images/)
- [Card index CSV](../../content/oracle/emperor-edition/card-index.csv): names, collector numbers, record roles, Oracle/printing IDs and local image paths.
- [Full reference catalogue](../../content/oracle/emperor-edition/catalogue.json): all source metadata, current text and selected printing records, including printing overrides.
- [Image manifest](../../content/oracle/emperor-edition/artwork-manifest.json): source URLs, card/printing IDs, image variants, dimensions, hashes and download receipts.
- [Numbered core coverage](../../content/oracle/emperor-edition/numbered-core-coverage.json): explicit number-by-number checks for 1–428.
- [Coverage and metadata review](../../content/oracle/emperor-edition/coverage-review.json).
- The exact API request and complete response are saved under `assets/reference/oracle/emperor-edition/`.

Filenames use stable card IDs, printing IDs and image indices. The CSV maps them
to human-readable names; no name guessing was used to select pictures. JPEG bytes
are preserved as served. These files form an acquisition dataset for the next
client-content integration pass; the existing desktop builds were not rebuilt.

## Set membership versus era legality

The supplied search selects **Emperor Edition printings**. The earlier
2,149-record snapshot selected Oracle's **Age of Conquest (Emperor) legality
label**, which spans a broader era. These are different queries.

Of the 574 set-search records, 424 are also present in that earlier era snapshot.
Most of the remaining records are supplemental items. Seven records with
game-card types have explicit Emperor Edition printings but lack membership in
the saved era query:

| Card | Emperor Edition collector number |
|---|---:|
| Caught Unawares | 283 |
| Cross-Clan Wedding | 15 |
| Kalani's Landing | 408 |
| One Koku | 326 |
| The Duty of the Crab | 362 |
| The Governor's Estate | Unnumbered |
| Well-Tended Farm | 56 |

All seven were retained, with the source labels recorded for review. Neither
query alone is treated as a historical legality certificate. Current Oracle text
and printing-specific text remain separate, and all imported records are
non-playable until their rules are implemented and reviewed. This acquisition
completes artwork for the numbered base set, not every expansion in the wider era.

## Verification

- Retrieved all 574 results and checked the reported total, timeout status,
  shard failures and unique card/printing identities.
- Selected only printings explicitly labelled `Emperor Edition`. A card's
  current primary printing may belong to another set and was not used as a shortcut.
- Downloaded every referenced image variant using the site's documented image
  URL structure. All 590 master images were available; no smaller fallback was needed.
- Fully decoded each image, checked its dimensions and recorded a SHA-256 hash.
  A subsequent offline resume verified all cached scans again without downloading
  them again. Log: `artifacts/checks/emperor-edition-resume.log`.
- Excluded the separate Proxy numbering series from the 1–428 completeness
  check, so a proxy numbered 1 cannot mask a missing core card numbered 1.
- Visually inspected sample scans, including an Emperor printing selected instead
  of the card's current Ivory primary printing, and a numbered card with the
  printed `263 / 428` collector footer.
- **59 content tests passed**, including seven new tests for exact printing
  selection, image variants, completeness, source preservation, invalid images,
  verified resume and independent proxy numbering. No game-engine code changed.

Image URL construction and size choices were checked against the site's public
[client source](https://github.com/Oracle-of-the-Void/ootv-client/blob/master/oracle.js)
and [L5R configuration](https://oracleofthevoid.com/templates/l5r.js). Requests were
read-only, with two workers, rate pacing and bounded retries.

## Recheck the saved acquisition

```sh
python3 -m pip install -r tools/content/requirements.txt
python3 tools/content/download_oracle_printings.py --retrieved-on 2026-09-06
python3 -m unittest discover -s tools/content -p 'test_*.py'
```

The downloader validates the complete saved response before working. Existing
verified scans are reused; changed cached scans are reported rather than silently
replaced. A missing source image prevents a complete-download claim.
