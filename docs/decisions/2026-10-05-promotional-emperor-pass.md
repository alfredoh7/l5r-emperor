# Promotional-Emperor pass (#653)

Status: accepted as engineering readings; the rules points below are Mendon's to confirm or overrule.

All 134 Promotional-Emperor cards that were unwired were read against their scans and the engine. **106 are wired** (item-wave-42, holding-wave-51, region-wave-22, celestial-wave-27, strategy-wave-112, spell-wave-51, event-wave-31, personality-wave-123, follower-wave-46) and **28 are blocked**, each with a note and a category in `docs/CARD_INVENTORY.xlsx` (written from `tools/content/pe_*_blockers.json` by `build_card_inventory.py`). The scan wins over the catalogue text wherever they differ (Nishima prints no Naval keyword; Tales of Valor triggers at the End Phase; Emerald's straighten is "even if bowed").

## Readings built in (not confirmed rulings)

- **Horse**: "ignoring costs" is its Gold cost only; it may attach to any other Human for 3 Gold.
- **Nishima**: any Infantry Maneuvers assignment of his gives Naval, as Attacker or Defender.
- **Treasure Hunter**: its army is the Attacker's army containing its host.
- **Family Name** (Family Sword, Family Maneuvers): the first word of a two-word title on a Clan-aligned Personality, unless it is an ordinary word (the Torn Asunder rule of thumb).
- **Championship Events and printed "Limited" Events**: resolve on reveal; "Limited:" on an Event is ignored.
- **Public Room**: +1F on each Sake token's holder for the turn. **Topaz Dojo / Topaz Championship**: "no printed abilities" means no catalogued ability, Effect or rules text.
- **Plantation**: counts payments for cards bought, equipped or played, not actions of cards in play.
- **Imminent Victory / A Great Victory**: "destroy the army" means the resolution destroyed at least one unit, or the Province.
- **Expansion, Topaz trait, Six-Ring Sword, Qamar**: negate destruction by an action only, not a battle's own casualty sweep.
- **Arcane Cloak, Emerald Armor**: the "other legal target" test covers targets named at announcement, as Goju Yurishi's does.
- **Greensnakes** (alters the rulebook Lobby), **Jade Mantle** (reactive Gold from an attached card) and **Family Sword** were wired on those precedents; re-block them as RULEBOOK or RULES if the readings are wrong.

## Blocked, with the question or gap (full notes in the inventory)

RULES questions: Jizo's Mercy ("gain 4 Honor instead"), Sadahako's Artistry (whose card is a gift), Sleep (when targets are chosen), Gaijin Sorcerer (doubling and Chi totals), Gozu (a legal but untaken Terrain), Peace Treaty (a no-op below three players), the Blade of the Balash and Isha's Yumi discard-from-hand performer, Topaz Armor (keyword choice). Engine gaps: Fusako, Mempo, Sacred Rosary, Winter Sled, Profits, The Deciding Moment, Through the Breach, Wildfire, Jade Embrace, Sea & Sky, the Jade and Onyx Championships, Hidden Weapons, Oracle of the Void - exp, Verdant Jungle, Ever-Winter Copse, The Amethyst Court. Colonial Farm and The Onyx Dueling Circle need created-card proxy scans (docs/requests/created-card-scans-needed.md rows A6 and B12).

## Known limits

- The Championship abilities for Obsidian, Ruby and Topaz are not yet in the bow/destroy classification lists that Rokai/Mimori-style Reactions read.
- Kataoka's and Tamahime's entry discounts do not apply on the Reserve/search-recruit price path.
- The catalogue art pack candidate `catalogue-art-issue653-20261005-r1` (106 new scans) is built and verified locally; it is not published as a release.
