# Rules and content evidence

Reviewed 6 September 2026. The target is **AEG Emperor Edition**, as adopted in the
project plan. The previous brief is a design reference, not a rules authority.

| Source | Status and intended use |
|---|---|
| [AEG Emperor rulebook, mirrored PDF](https://www.aftertheboardgame.com/_downloads/2bcbbadb334d8f13b9a6853e8460f1e0) | Primary document, 148 PDF pages, inspected through the web reader. Mirror hosting is not publisher hosting. Printed page references below are one-based. |
| [Oracle of the Void](https://oracleofthevoid.com/) | Maintainer-operated card catalogue. Saved Emperor query contains 2,149 unique records, the full result count returned on retrieval day. |
| [Oracle client and query notes](https://github.com/Oracle-of-the-Void/ootv-client/blob/master/oracle.api) | Primary technical source for the public catalogue API. Original application code is not incorporated. |
| [AEG Aftermath rules insert](https://www.aftertheboardgame.com/_downloads/acf35bc3d6c0a80fe153343f9bcaddb8) | Link located; contents not verified. Needed for late-arc terminology and mechanics. |
| [AEG Coils of Madness rules insert](https://www.aftertheboardgame.com/_downloads/b6a58af41b8649607eb690fffa435fab) | Link located; contents not verified. |
| [AEG Gates of Chaos rules insert](https://www.aftertheboardgame.com/_downloads/ff85a44458ca4e608284e17d872509d3) | Link located; contents not verified. |
| Emperor Comprehensive Rules / final errata and bans | A saved copy of the rules.l5r.com wiki (Emperor Edition core rules, glossary, changes from Celestial, and the card-ruling archive) is converted in [COMPREHENSIVE_RULES.md](COMPREHENSIVE_RULES.md). It is an undated wiki snapshot, not an authenticated dated period snapshot, so final errata and bans remain a release dependency. |

The normalizer records the API request, retrieval date and raw SHA-256 in
`content/oracle/emperor-catalogue.json`. The current classification does not
establish which bans, errata and bridge-set rules applied on a historical date.
Current Oracle text includes terms from later templates on some Emperor-tagged
cards. Do not treat every top-level text field as Emperor-era printed wording.

The 2014/2015 Ivory and Twenty Festivals rulebooks and tournament documents
located beside the Emperor rulebook are not the selected ruleset. The current
client does not need any of those documents to accept a command.

## Verified rules used to guide the first simulation

These are concise paraphrases of the [Emperor rulebook](https://www.aftertheboardgame.com/_downloads/2bcbbadb334d8f13b9a6853e8460f1e0), not a substitute for the complete rules.

| Printed page | Behavior |
|---|---|
| 49 | Both players start with Border Keep. The second also starts with bowed Bamboo Harvesters. |
| 50 | A producer contributes to one payment. Combine producers; discard overproduction. |
| 83–84 | Personalities require sufficient Family Honor. In-clan discount is −2 Gold; allegiance is +2 to ignore the requirement. These options combine. |
| 85 | Proclaim is an optional reaction after an in-clan Personality enters from your province during your turn without discount. Gain base Personal Honor. |
| 87 | End Phase draws a Fate card, then discards to the eight-card hand limit. |

## Economy card references

These identities were checked against the saved Oracle records, including their
Emperor printings. They remain separate from executable definitions.

| Card | Oracle identity | Verified content relevant to economy |
|---|---|---|
| [Marketplace](https://oracleofthevoid.com/#cardid=4840) | `oracle-l5r-4840` | Cost 2. Bow for 2 Gold, or 3 for a Crane player. |
| [Border Keep, basic](https://oracleofthevoid.com/#cardid=1106) | `oracle-l5r-1106` | Cost 0. Bow for 2. First-turn and once-per-game Province cycling are separate abilities. |
| [Bamboo Harvesters, basic](https://oracleofthevoid.com/#cardid=679) | `oracle-l5r-679` | Cost 0. Bow for 2. Does not straighten before its owner's second turn. |
| [Small Farm](https://oracleofthevoid.com/#cardid=7303) | `oracle-l5r-7303` | Cost 0. Bow for 1. |
| [Large Farm](https://oracleofthevoid.com/#cardid=4664) | `oracle-l5r-4664` | Cost 1. Bow for 2. |
| [Remote Village](https://oracleofthevoid.com/#cardid=6258) | `oracle-l5r-6258` | Cost 3. Bow for 3. |
| [Prosperous Village](https://oracleofthevoid.com/#cardid=6086) | `oracle-l5r-6086` | Printed cost 6, reduced by controlled Personality count. Bow for 5. |

All listed cards' other rules and keyword behavior must be represented before
they are offered as fully supported cards. Basic and Experienced versions of
the starting Holdings have different identities and behavior.

## Remaining evidence required for a legal full release

- Freeze a specific Emperor-era rules revision, ban list and errata cutoff.
- Establish period text for every enabled card, including dual-arc printings.
- Validate deck construction, Unique limits and restrictions against that cutoff.
- Map each artwork asset to an explicit catalogue identity and printing.
- Execute ability and interaction scenarios before changing support status.
