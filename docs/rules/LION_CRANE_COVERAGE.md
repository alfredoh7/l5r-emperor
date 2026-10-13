# Published Lion and Crane coverage

The `lion-crane-v1` playtest implements the saved candidate compositions: **40 Dynasty / 40 Fate per Clan**, with **40 distinct published cards** including Strongholds and starting Holdings. Every printed action and trait in this pool has an engine implementation; vanilla cards retain their printed statistics and keywords. There are no substituted training effects.

## Frozen rules and source decisions

- Rules profile: AEG Emperor Edition rulebook, printed pages 48–70 (setup, payments, action timing, once-per-turn limits, performers, Equip, Lobby and Favor), 76–87 (battle presence/location, resolution, Dynasty and End), 93–118 (keywords, duration and destruction). [Publisher rulebook, preserved PDF](https://www.aftertheboardgame.com/_downloads/2bcbbadb334d8f13b9a6853e8460f1e0).
- Every selected scan was visually inspected for rules, statistics, keywords and Focus value on 6 September 2026. Source records, exact printing IDs, download URLs and image SHA-256 values are in `content/published/lion-crane-sources.json`. Original files are preserved; the client packages copies for offline play.
- **Ikoma Shika costs 4 Gold in the selected Emperor printing**, not the 2 in the current Oracle record. This is explicitly overridden in the playable snapshot.
- Doji Shunya uses its printed Political Limited action. Defensive Stance chooses a bowed performer. Block Supply Lines bows the unit only after moving it; Incapacitated does not bow it. Later wording is not silently substituted.
- Crushing Strength retains both printed effects, using the saved Emperor Oracle targeting syntax (explicit required targets) for its older Battle of Kyuden Tonbo printing. This normalization is an explicit profile decision, not a claim that the old scan contains Emperor punctuation.
- The profile includes Emperor-era expansions from the saved candidates, rather than only the 428-card base set. Source metadata supports Emperor membership; an authenticated final tournament banlist/errata cutoff has not been certified. This is a reproducible playtest profile.

## Card implementation matrix

| Card | Selected printing | Engine behavior |
|---|---|---|
| [Akodo Kisho](https://oracleofthevoid.com/#cardid=229) | Emperor Edition · 1 | Printed stats and keywords; no activated ability |
| [Asahina Konomi](https://oracleofthevoid.com/#cardid=502) | Embers of War · 1 | Printed stats and keywords; no activated ability |
| [Asahina Nanae](https://oracleofthevoid.com/#cardid=507) | Before the Dawn · 1 | air-straighten |
| [Asahina Yasutora](https://oracleofthevoid.com/#cardid=521) | Emperor Edition · 1 | Printed stats and keywords; no activated ability |
| [Bamboo Harvesters](https://oracleofthevoid.com/#cardid=679) | Emperor Edition · 1 | 2 Gold per bow/payment; skip-first-straighten |
| [Block Supply Lines](https://oracleofthevoid.com/#cardid=1045) | Emperor Edition · 1 | block-supply |
| [Border Keep](https://oracleofthevoid.com/#cardid=1106) | Emperor Edition · 1 | 2 Gold per bow/payment; border-first; border-game |
| [Costly Feud](https://oracleofthevoid.com/#cardid=1521) | Promotional–Celestial · 1 | costly-feud |
| [Crushing Strength](https://oracleofthevoid.com/#cardid=1589) | Battle of Kyuden Tonbo · 2 | crushing-force; crushing-destroy |
| [Daidoji Kenshi](https://oracleofthevoid.com/#cardid=1645) | Emperor Edition · 1 | Printed stats and keywords; no activated ability |
| [Daidoji Soken](https://oracleofthevoid.com/#cardid=10625) | Gates of Chaos · 1 | 3 Gold per bow/payment |
| [Daidoji Tsunehiko](https://oracleofthevoid.com/#cardid=9812) | The Shadow's Embrace · 1 | Printed stats and keywords; no activated ability |
| [Defensive Stance](https://oracleofthevoid.com/#cardid=10368) | Torn Asunder · 1 | defensive-stance |
| [Doji Shunya](https://oracleofthevoid.com/#cardid=2158) | Emperor Edition · 1 | shunya-honor |
| [Doji Tatsuki](https://oracleofthevoid.com/#cardid=2166) | Emperor Edition · 1 | Printed stats and keywords; no activated ability |
| [Focus](https://oracleofthevoid.com/#cardid=2619) | Emperor Edition · 1 | Focus 5; no activated ability |
| [Gold Mine](https://oracleofthevoid.com/#cardid=2867) | Emperor Edition · 1 | 2 Gold per bow/payment; dragon-gold |
| [Halls of Memory](https://oracleofthevoid.com/#cardid=2949) | Emperor Edition · 1 | 3 Gold per bow/payment; halls-memory |
| [Ikoma Shika](https://oracleofthevoid.com/#cardid=3636) | Emperor Edition · 1 | Printed stats and keywords; no activated ability |
| [Ikoma Sido](https://oracleofthevoid.com/#cardid=3640) | Embers of War · 1 | Printed stats and keywords; no activated ability |
| [Ikoma Sugo](https://oracleofthevoid.com/#cardid=3641) | Second City · 1 | attacking-four |
| [Ikoma Takakura](https://oracleofthevoid.com/#cardid=10851) | Aftermath · 1 | Printed stats and keywords; no activated ability |
| [Incapacitated](https://oracleofthevoid.com/#cardid=3730) | Emperor Edition · 1 | incapacitated |
| [Kitsu Kouki](https://oracleofthevoid.com/#cardid=10063) | Seeds of Decay · 1 | kouki-force |
| [Kitsu Sorano](https://oracleofthevoid.com/#cardid=4395) | Emperor Edition · 1 | Printed stats and keywords; no activated ability |
| [Large Farm](https://oracleofthevoid.com/#cardid=4664) | Emperor Edition · 1 | 2 Gold per bow/payment |
| [Light Armor](https://oracleofthevoid.com/#cardid=10118) | Seeds of Decay · 1 | +3F / +0C; Armor |
| [Marketplace](https://oracleofthevoid.com/#cardid=4840) | Emperor Edition · 1 | 2 Gold per bow/payment; crane-gold |
| [Matsu Hachiro](https://oracleofthevoid.com/#cardid=4915) | Emperor Edition · 1 | attacking-two |
| [Native Guide](https://oracleofthevoid.com/#cardid=9871) | The Shadow's Embrace · 1 | 5F; PH requirement 0 |
| [Never Afraid](https://oracleofthevoid.com/#cardid=9916) | The Shadow's Embrace · 1 | never-afraid |
| [Paid Off](https://oracleofthevoid.com/#cardid=5861) | Emperor Edition · 1 | paid-bow; paid-straighten |
| [Prepared for Death](https://oracleofthevoid.com/#cardid=6051) | Emperor Edition · 1 | prepared-death |
| [Prosperous Village](https://oracleofthevoid.com/#cardid=6086) | Emperor Edition · 1 | 5 Gold per bow/payment; prosperous-discount |
| [Pure Strike Blade](https://oracleofthevoid.com/#cardid=9879) | The Shadow's Embrace · 1 | +5F / +1C; Weapon |
| [Rank & File](https://oracleofthevoid.com/#cardid=10253) | Honor and Treachery · 1 | 4F; PH requirement 0 |
| [Remote Village](https://oracleofthevoid.com/#cardid=6258) | Second City · 1 | 3 Gold per bow/payment |
| [Shinden Asahina](https://oracleofthevoid.com/#cardid=6855) | Emperor Edition · 1 | 4 Gold per bow/payment; shinden-choice |
| [Small Farm](https://oracleofthevoid.com/#cardid=7303) | Emperor Edition · 1 | 1 Gold per bow/payment |
| [Veteran Skirmishers](https://oracleofthevoid.com/#cardid=9147) | Emperor Edition · 1 | 3F; PH requirement 0 |

## Shared mechanics and interactions

- Lion starts at 7 Honor with 7-strength provinces and 3 Gold production. Crane starts at 6 Honor with 6-strength provinces and 4 Gold production, plus bowed Bamboo Harvesters; Bamboo cannot straighten before Crane’s second turn.
- Border Keep’s two refresh abilities track independent use limits. Players choose one or more province cards, specify bottom-of-deck order, and see the replacements face-up. Refresh requires an unbowed Keep but does not bow it.
- Every action validates its phase, source zone/control, source readiness, required targets and performers, costs and usage limits before committing any change. Invalid and stale requests leave state untouched. Each distinct card’s ability is normally usable once per global turn.
- Shinden Asahina works even when bowed. It bows the chosen Shugenja, suspends resolution for the opponent’s decision, and then either applies bowing plus turn-long straightening prevention or awards 1 Honor. The opponent retains the next normal action opportunity after answering.
- Halls allows printed Shugenja Battle abilities from another location while still requiring friendly battlefield presence. Its Ancestor Force penalty is contextual. Its Honor trigger applies to Combat Segment destruction, not battle resolution; a synthetic Chi-death fixture exercises this otherwise unreachable trigger, since these two decks contain no Ancestors.
- Lobby, Favor hand cycling and Favor Battle are distinct rulebook abilities, each limited once per turn. Shunya’s Honor ability is independent. Favor begins unclaimed, is visible to both players, and is discarded as a cost.
- Force changes last until turn end. Unbowed Followers contribute independently of their Personality. Items modify Force and Chi even if bowed. Armor and Weapon limits, Follower PH requirements, unit movement, attachment removal, Chi death and battle Honor accounting remain authoritative.
- Focus has no invented action. Samurai, Courtier and Shugenja keywords are retained; no card in these lists creates a dishonorable state or contains a Spell, Duel, Cavalry, Event, Region or Ring mechanic. Broader keyword actions become necessary as new cards introduce those states.

## Play and validation

Open Practice and choose **Play Crane · Shinden Asahina** or **Play Lion · Halls of Memory**. Enable the practice opponent or use the existing pass-device flow. Private rooms offer the published profile; their creator takes Crane and the joining player takes Lion. Select a source card to see its abilities. Costs, targets, performers, Border Keep selections and Shinden decisions use actual game controls.

Automated evidence lives in `tests/L5R.Core.Tests/PublishedTests.cs`, the AI suite and the client/server transport suite. The published core suite has 31 tests covering individual effects, invalid actions, source and target boundaries, use limits, duration, Favor, hidden information, offered-command validity and complete replay. Five seeded AI games with these decks complete through legal commands and reproduce through replay. Native UI and package checks are recorded in `docs/VALIDATION.md`.

Content version: `emperor-lion-crane-0.5.0`. Replays and rooms saved under an earlier content version are incompatible: the server skips them with a warning and they remain preserved on disk. 0.3.0 changed card ids to a seeded permutation (issue #611). 0.4.0 adopts the rules-validation corrections of 2026-10-13 (docs/decisions/2026-10-13-validation-report-reading.md): Reserve Weapon counts toward the focus cap, Fortitude compares total unit Force, an attached Spell acts only through a Shugenja host, dishonorable death's Honor loss is a rulebook effect, Set measures from the raw stat, prevention hides modifiers instead of deleting them, dishonor persists outside play, battlefields at one province form a line, and a played Strategy or Ring waits in the resolution area. 0.5.0 follows the rulings that came out of that work: Great Sacrifice compares total unit Force (R-084), a Spell's static effect follows its printed subject (R-085), and a stronghold's "You do not lose Honor from ... cards you own" does not prevent a dishonorable death's Honor loss (R-083). No tournament certification, native Windows gameplay acceptance, or public deployment is implied.
