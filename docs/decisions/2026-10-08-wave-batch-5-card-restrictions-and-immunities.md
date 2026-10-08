# Wave batch 5: card restrictions and immunities (issue #736)

Status: **implemented on `claude/736-wave-batch-5`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Batch 5 of [the wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md) (umbrella #742). Engine: `src/L5R.Core/Wave736.cs`. Packager: `tools/content/package_wave_736.py`. Tests: `tests/L5R.Core.Tests/Wave736Tests.cs` (suite `wave-736`, 15 tests).

## What is wired

14 of the batch's 21 cards, in eight profiles: personality-wave-159 (4), follower-wave-71 (1), item-wave-67 (3), spell-wave-74 (2), strategy-wave-143 (1), holding-wave-72 (1), event-wave-40 (1), celestial-wave-37 (1). Each card was read against its own scan.

| Card | Built as |
|---|---|
| Susumu Haikaro | a standing `DestroyInResolution` immunity on himself (P12) |
| The Earth's Armor | a standing `Bow` immunity on the Spell's host while he is defending (new P12 scope `AttachedHost`) |
| Thunder Bow | a standing `Prevention` immunity on its host; `w736-bow-transfer` (Battle/Open, "Transfer this card to him": your Personality at any location) |
| Path to Inner Peace | `w736-path-peace`: `NegateForcePenalties` (Tireless Efforts' helper: current and new penalties, this turn) and a `Prevention` immunity on the target |
| Harbor of Kalani's Landing | `w736-harbor-play` (Battle: put into play as the Terrain, an additional Battle action) and a standing `MovementAtBattlefield` immunity on its battlefield |
| Oracle of the Void - exp | a standing `Destroy` immunity; `w736-oracle-refill` (the next two refills this turn face-up, P21's `RefillFaceUp`) and `w736-oracle-draw` |
| P'an Ku's Madness | `w736-madness-token` (Limited, a Madness token on a target card) and `w736-madness-bar` (Open: discard the Celestial; P11 `NoAttack` and `NoDefend` on a Madness Personality) |
| Rampant Paranoia | an Event kept in play until its owner's next turn begins (`KeepEventInPlay`) and a standing `Destroy` immunity on his Personalities in a defending army (new P12 flag `EventCarrier`) |
| Ninube Onchi - exp | `EntryHonorLoss` 2; a static P11 target bar against cards in other players' units |
| Otomo Suikihime - exp | `EntryHonorLoss` 3; a static bar against other players' Political actions and a static `NoAttack`; `w736-suikihime-search` (the Fallen search, placed face-up in a province) |
| Kamalakar's Harem | a live `NoAttack` on its host; `w736-harem-dishonor` (Limited, bow: dishonor a Human Personality, destroy the Follower) |
| Map of the Northern Colonies | a standing `TerrainDestroy` immunity for the Item's controller's Terrains at its battlefield, against other players' Terrains |
| Tankoji | `w736-tankoji-bar` (Battle, bow: a P11 target bar on the wielder's unit against the target Personality's unit) |
| Yoritomo Naoto - exp | `w736-naoto-taunt` (Battle: a P11 forced-target rule for the target's unit; an additional Battle action) |

## Not wired (7), with blocker notes in `tools/content/pe_wave_736_blockers.json`

None is published, so no printed effect is dropped.

- **A Cleansing Breath** needs a ruling: "he centers himself" has no definition in the repository.
- **Asako Kaitoko - exp**: a maximum Force of 3 is a stat cap and none exists (P9 left `StatCap` out); the target bar is conditional on five keywords of the actor's Personality.
- **Isawa Ryuzo**: "will not attack or defend in the same army"; P11's pair rule judges the whole battlefield (both sides), not the army.
- **P'an Ku - exp**: control with a return when he straightens or leaves play, a seppuku bar and "may remain bowed" together.
- **Koan's Jingasa**: a Spell limit counted by attached cards (P11's `SpellLimit` counts actions) and a waiver of the bow cost of the host's Spells.
- **Hida Kaiji - exp**: "will not attack or defend with other Personalities" is an army rule (P11's pair rule would also bar him from being opposed).
- **Bonds of Hospitality**: a card in play bound to its target, a straighten bar that lasts while it does, and a granted discard ability that costs Gold.

## Shared lines edited

- `src/L5R.Core/ImmunityLedger.cs` (P12): a new scope `ImmunityScope.AttachedHost` ("this Shugenja" of a Spell, "this Personality" of an Item: the card the carrier is attached to), and `StandingImmunity.EventCarrier` (a rule whose carrier may be an Event in play; `InPlay` excludes `EventInPlay`). Both are additions; no existing rule changes.
- `src/L5R.Core/EmperorP4.cs`, `P4TargetAtAnyLocation`: one more term, `Wave736Abilities.IsAnyLocationTarget` (a Battle target category exempt from "at the current battlefield").
- `src/L5R.Core/ShadowsEmbraceBatch4.cs`, `TerrainPlaysThroughOwnAbility`: one more term, `Wave736Abilities.IsTerrainPlayAbility` (Harbor is played through its own Battle action).
- New persisted state: `MatchState.OracleFaceUpRefills` (`JsonPropertyOrder` 22920), omitted until Oracle of the Void's refill action is used.

## Readings for Mendon to confirm or correct

1. **"Other players'" is relative to the immune card's controller.** Path to Inner Peace on an enemy Personality negates effects of players other than that Personality's controller, which is the acting player's own effects against it; his own controller's locks still bind. (P12's convention.)
2. **Path to Inner Peace** targets any Personality in play (either player's). "Negate all Force penalties on him" is Tireless Efforts' reading (every penalty in force now is removed and none is applied for the rest of the turn; bonuses stay), and the prevention negation lasts the turn.
3. **The Earth's Armor** covers the Shugenja it is attached to while he is defending (a Personality of the non-active player at a battlefield); the Spell itself is not covered. Another player's card is any card whose controller is not his.
4. **Thunder Bow's Transfer** goes to your own Personality at any location (home or any battlefield), never to an enemy, and the attaching restrictions still apply (Field Messenger's rule); the immunity follows the Bow to the new wielder.
5. **Susumu Haikaro's** negation is only battle resolution's own destruction (P12's `DestroyInResolution`); an action's destruction of him still happens.
6. **Harbor of Kalani's Landing** negates movement made by a card effect to or from its battlefield, whoever moves; the rulebook's end-of-battle return and assignment are not effects and are not stopped (P12's reading). It is played from the hand as its own Battle action; the additional Battle action is unscoped, as Unstable Ground's and Deep Snows' shapes.
7. **Oracle of the Void - exp** cannot be destroyed by any card effect (own or another player's); a sacrifice paid as a cost is not an effect and is not stopped. "May be refilled face-up" is read as automatic: the next two refills this turn are face-up (the printed "may" is permission; there is no choice in the refill hook). Say if the owner should be asked each time.
8. **P'an Ku's Madness**: the Limited gives a Madness token to any card in play; the Open discards the Celestial as part of the effect (not a cost) and the target "cannot attack or defend" for the turn, as the text names no longer duration.
9. **Rampant Paranoia** is a revealed Event that stays in play until its owner's next turn begins. "Other players' cards will not destroy your Personalities in a defending army" is a negation of another player's card effects against his Personalities at a battlefield on the defending side; battle resolution's own destruction is not a card effect and still happens.
10. **Onchi and Suikihime**: "a card in another player's unit" is a Personality or an attachment of another player's Personality (a Strategy or a Holding is not a card in a unit); Suikihime's bar is on other players' Political actions only, and "may not attack" is static.
11. **Suikihime's search** finds a Personality with the Fallen keyword in the controller's Dynasty deck, puts him into a province of the controller's choice face-up, and discards the card that was there (the existing province-placement step); the Dynasty deck is reshuffled as every searched deck is.
12. **Kamalakar's Harem**: "a Human Personality" is a Personality without the Nonhuman keyword (P3's reading), either player's; the host may not attack while it is attached (live, not stored) and may again once the Follower is gone. Dishonor first, then the Follower is destroyed.
13. **Map of the Northern Colonies** protects the Item's controller's Terrains at the battlefield of the Personality carrying it, against the effects of Terrains another player controls (the effect's source must be another player's Terrain).
14. **Tankoji**: "this unit" is the wielder's unit (his attachments included); "a target Personality's unit" is either player's; the bar lasts the turn. It is a Battle action of the Weapon, so its wielder must be at the current battlefield.
15. **Yoritomo Naoto**: "must target Naoto, if legal" is a forced-target rule (P11) for actions performed by cards in the target's unit (the Personality or his attachments) for the turn; "may take an additional Battle action" is unscoped.

## Client contract

No new command, option field or view field. The Suikihime search uses the existing search and province-placement flow.

## Art pack

The 14 new scans need catalogue-art coverage. The candidate extends the published pack `catalogue-art-issue733-20261008-r1` (4,490 images): tag `catalogue-art-issue736-20261008-r1`, 4,504 images, 602,565,056 bytes, SHA-256 `3ca0c757970e3f15746f64f6cfc3cbfac0a8e4cb1406356d304412d15e406264`. Built with `tools/content/build_catalogue_art.py --base-archive`; it is published before this branch merges (the PR records the release link and the clean-download check). If another batch's pack reaches `main` first, this pack is rebuilt on top of it.
