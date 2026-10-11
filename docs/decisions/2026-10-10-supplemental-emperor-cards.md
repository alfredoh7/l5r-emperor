# Seven Emperor Edition cards the legality query left out (issue #812)

Kalani's Landing, One Koku, Caught Unawares, Cross-Clan Wedding, The Duty of the Crab, The Governor's Estate and Well-Tended Farm have explicit Emperor Edition printings but were missing from the card library, the deck pool and `docs/CARD_INVENTORY.xlsx`.

**Cause: the data, not a decision.** The library comes from one saved Oracle query, "Age of Conquest (Emperor)" legality (2,149 records). Oracle drops cards it records as banned from that legality list, so six of the seven (all but The Governor's Estate, which Oracle labels Legacy-only) never came back. `content/oracle/emperor-edition/coverage-review.json` already flagged all seven as "retained for review". No ruling, decision file or code excludes them, and no code filters on bans.

**What changed.** `tools/content/import_supplemental_emperor.py` writes `content/oracle/emperor-supplemental-catalogue.json` from the existing Emperor Edition export, in the same schema as the main catalogue. The deck pool, the client library (`library.json`, now 2,156 cards) and the inventory read it. Each card keeps its Oracle legality labels and ban note, is marked `historical_legality_status: requires_review`, and stays non-playable. The deck pool flags them `supplemental`.

**Not decided.** Whether a card Oracle lists as banned from Emperor Edition tournaments (December 2012 and September 2013 bans, both after printing) may be played in this game is Mendon's call. The import takes no position, and the blocker notes in `tools/content/pe_issue812_blockers.json` say so.

**Wiring triage** (read against the engine; the cards are not wired):

| Card | Category | Summary |
|---|---|---|
| One Koku | WIRE | Strategy in hand as a Gold payment token: Near Miss precedent (`PromoPaymentSources.cs`) with Seaside Bazaar's any-cost trigger |
| Well-Tended Farm | WIRE | Ordinary Holding: bow for Gold, Limited bow for Honor |
| Caught Unawares | SMALL | "No Personalities" condition, one-or-two targets, negate straightening this turn, draw if not your turn, "no unbowed attachments" filter |
| The Duty of the Crab | SMALL | Delay Focus Effect is an existing shape; the Battle recruit from a province or the discard pile is unchecked |
| Cross-Clan Wedding | SMALL | Control transfer and province-reveal Events exist; the other player's own choice and the "now controls" condition are new; compare the scan's text first |
| Kalani's Landing | RULES | After-announcement Reaction and a Gold Cost modifier on a card in play, with no stated duration |
| The Governor's Estate | ZONE | Needs face-down cards in play and a granted Holding trait |

The inventory's existing Unwired/Wired statuses were regenerated against `content/published/*.json`: none changed, since the committed workbook was already current. **Art is not included.** Their eight Emperor Edition scans are on disk, but adding them to `catalogue-printings.json` makes the art-pack coverage check (`test_catalogue_art`) demand a new private catalogue art release, which this change does not publish. They show in the library without catalogue art until a new pack is built and released (the `art-pack-release` skill).
