# Wave batch 7: turn boundaries, rule flags and the Events Phase (issue #738)

Status: **implemented on `claude/738-wave-batch-7`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Batch 7 of [the wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md) (umbrella #742). Engine: `src/L5R.Core/Wave738.cs`. Packager: `tools/content/package_wave_738.py`. Tests: `tests/L5R.Core.Tests/Wave738Tests.cs` (suite `wave-738`, 17 tests).

## What is wired

16 of the batch's 18 cards, in six profiles: personality-wave-175 (2), item-wave-75 (2), strategy-wave-159 (3), holding-wave-88 (1), event-wave-56 (7), celestial-wave-53 (1). Each card was read against its own scan.

**The scans do not print the P5 clause.** The plan placed nine cards on P5 (province-reveal Events) because the catalogue (Oracle) text of A Dangerous Game, An Empire of Madness, Open Recruitment and Rejected Mediation opens "After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in one of them, ...". The period scans print only the effect, so each is an ordinary Event that resolves when it is revealed and is discarded; the P5 "stays face up and fires every turn" flag is not used for any card in this batch. The packager carries the scan's wording as the card's `Text`, and Open Recruitment's and Rejected Mediation's durations follow the scans ("your Events Phase two turns from now", "your second turn from now"). Suck the Marrow's scan also differs ("after each Events Phase begins", not "after each turn begins").

| Card | Built as |
|---|---|
| Doji Tatsuki - exp | a P4 turn-begin callback (`AnotherPlayers`): she straightens as the other player's turn begins |
| Shosuro Takazaki | `w738-takazaki-engage` (a Political Reaction after engaging) sets a P13 battle-scope `no-destruction-honor-for-others` flag for her controller; Expendable is the printed keyword |
| Dagger of Crystal Wind | a P4 turn-begin callback (`Yours`): a +1F Obsession token on its host (P24's multi-stat token), then he is destroyed when he has more Obsession tokens than his printed Chi |
| Lantern Oil | the P13 engage bar, read from the Item: `EngageActionBarred` also asks whether a Lantern Oil is attached to a Personality at the Engage window's battlefield |
| Drain of Effort | `w738-drain-watch` records the effect (`MatchState.Wave738Drains`); an after-action trait queues a forced discard for each other action's player, at most twice, while the phase it was played in lasts; `w738-drain-draw` draws a card |
| Suck the Marrow | `w738-marrow-curse` (Maho Open: bow and destroy the performing Shugenja, a -1F/-1C Poison token on the target, lose 3 Honor); a P4 standing rule naming the target; a call as each Events Phase begins gives another token |
| Temple Grounds | `w738-temple-battle` / `w738-temple-engage` (Battle, and a Reaction after engaging: replace the Terrain there, put this one into play, and set a P13 battlefield `negate-battle-action-without-units` flag that lasts while it stays in play) |
| Abundant Farmlands | a P4 turn-begin callback (`EachPlayers`); `GoldProduction` 1 |
| A Dangerous Game | an Event decision (yes/no): discard the hand and draw four |
| An Empire of Madness | P4 `GrantEachPlayerAbility` (a player-level Open: your Personality with a Madness token commits Seppuku) until the game ends; a P13 game-scope `madness-loss` flag at 20 |
| Journey's End Siege | a P13 turn-scope `defenders-may-not-play-terrains` flag; the battle resolution calls `Wave738ProvinceDestroyed` once a Province was destroyed |
| Open Recruitment | a P4 standing rule until the owner's second turn from now; a P6 turned-face-up trait grants the Clan alignment (P10) |
| Rejected Mediation | the Imperial Favor is released; a P7 player-level negation watch of any player's Political action, once each turn |
| The Jade Championship | an Event decision (a Shugenja, none asked when there is one); `MatchState.Wave738JadeShugenja`; a Force term for Shadowlands Personalities and Followers; an after-action trait |
| The Madness Spreads | the P13 `limited-as-open-once-per-turn` flag (P4's `LimitedAsOpen` rule) for its owner until his second turn from now |
| Chagatai's Wrath | `w738-chagatai-end-phase` (Limited, discard this Celestial as the cost): the P13 `end-action-phase` flag |

## Not wired (2), with blocker notes in `tools/content/pe_wave_738_blockers.json`

None is published, so no printed effect is dropped.

- **Revelations**: the look at the top five Dynasty cards needs another player to discard one of the shown cards and the owner to reorder the rest; P20's "other player chooses" belongs to a search, and the look has no such step (batch 8's decision-UI work).
- **The Onyx Championship**: the permanent keyword and ability grant exist, but its search recruits "paying all costs" from the provinces, and P20's only province recruit ignores costs.

## Shared lines edited

- `src/L5R.Core/RuleFlags.cs`: `EngageActionBarred` also asks `Wave738LanternOilBars` (the Item's bar follows its host, so it needs no stored battlefield), and no longer returns before it when the flag store is empty.
- `src/L5R.Core/EmperorRemainingFEvents.cs`: one line in each default arm of `ResolveEventChoiceCommand` and `EventChoiceActions` (A Dangerous Game's yes/no and The Jade Championship's Shugenja).
- `src/L5R.Core/MatchEngine.cs`: one call in `EventsPhase` (`Wave738EventsPhaseBegins`, Suck the Marrow) and one in the battle resolution beside `WlGrantVoidRecruit` (`Wave738ProvinceDestroyed`, Journey's End Siege).
- `src/L5R.Core/ShadowsEmbraceBatch4.cs`: one term in `TerrainPlaysThroughOwnAbility` (Temple Grounds).
- New persisted state, omitted until used: `MatchState.Wave738Drains` (order 22940) and `MatchState.Wave738JadeShugenja` (order 22941).

## Readings for Mendon to confirm or correct

1. **The scan, not the catalogue, governs the four "after you reveal" Events.** A Dangerous Game, An Empire of Madness, Open Recruitment and Rejected Mediation resolve once, when revealed, and are discarded; the effect that lasts "until the game ends" or "until your second turn" is stored by the Event and outlives it. If the Emperor Edition errata did make them stay face up, P5 would be the right primitive and these four would be re-wired on it.
2. **Doji Tatsuki, Abundant Farmlands, the Dagger** fire as the turn begins, before the Straighten Phase (P4's reading 1): a bowed Farmlands straightens on both players' turns, Tatsuki on the other player's only.
3. **Dagger of Crystal Wind**: the destruction is the card's effect on its controller's turn (not a cost); "printed Chi" is the host's printed Chi, so the second Obsession token on a 2 Chi Personality is safe and the third destroys him. If the Dagger is not attached to a Personality nothing happens. The token's Force is an ordinary +1F token (negations of Force bonuses apply).
4. **Shosuro Takazaki**: "your cards in this battle's resolution" is the units her controller owns that the resolution destroys (P13's reading); the Engage action is a Reaction like every "Engage:" action, available to either of her controller's windows while the Engage window is open, and is not limited to a battle she is at.
5. **Lantern Oil**: "the Attacker may not take Engage actions at this Item's battlefield" bars the Attacker's Engage Reactions (including "After engaging" Reactions, as in P13's reading) when the Item's host Personality is at the battle, whoever controls the Item.
6. **Drain of Effort**: "after each action other than this one resolves, its player discards a card" counts every action the engine records as resolved (either player's) and the Drain's own action never counts. A player with no card to discard owes nothing and does not use up one of the two discards; the discard is owed as the next safe point opens (the forced-discard queue), and the player chooses the card. "Until this phase ends" is the phase (and turn) it was played in. Each Drain keeps its own count, so two Drains in play each take their two.
7. **Suck the Marrow**: the scan's "Maho" is a keyword of the Strategy (treated as an action keyword); "Bow and destroy your performing Shugenja" are two costs of the performing Shugenja, destroyed as a cost like Ring of Fire's; "Lose 3 Honor" is part of the effect, so it is lost even if the token is negated. The target may be any Personality in play. Each later token is given as an Events Phase begins (either player's), for as long as the target is in play; the rule does not end with the Strategy.
8. **Temple Grounds**: "Battle/Engage" is two abilities (a Battle action and an Engage Reaction), each putting the Terrain into play at the current battlefield from the hand, replacing a Terrain already there ("destroy a Terrain (if able)"). "Units here" and a Cavalry unit follow P13's readings. The flag ends when the Terrain leaves play.
9. **A Dangerous Game**: "if you did" is: choosing yes discards the hand (even if empty) and draws four. It is asked of the Event's owner when it is revealed.
10. **An Empire of Madness**: the ability is a player-level Open, taken by the player who has it, with an own Personality that has a Madness token as its target, regardless of whose turn it is (Open); it follows the ordinary seppuku rules for "commits Seppuku" (rehonor, then destroy, with the seppuku fact), not the rulebook action's restrictions. The loss is checked as the player's turn finishes, P13's reading, for each player.
11. **Journey's End Siege**: the "Limited" of a revealed Event is its resolution on reveal; the Terrain bar is the whole turn. "The first time you destroy a Province in battle this turn" is the first battle resolution this turn, after the Event resolved, in which the siege's player destroys a Province; the Provinces are counted after the destruction and the gained Province is added to the left of his leftmost (as every Province gained at any time). With more Provinces than the Defender he gains none and the siege is spent.
12. **Open Recruitment**: "your Events Phase two turns from now" is read as "until your second turn from now begins" (no card turns face up between a turn's beginning and its Events Phase). "Unaligned" is the engine's no-Clan-alignment test and "Human" is not Nonhuman. It applies to any Personality turning face up (a reveal or a face-up refill) in one of the owner's provinces, including one that was gained after the Event resolved, until the rule ends.
13. **Rejected Mediation**: "any player who controls the Imperial Favor discards it" is the Favor leaving its controller, not a cost (no "after you discard the Imperial Favor" Reaction). "The first Political action taken each turn" is the first of either player's, negated whole (its costs are paid) unless Protection stops it. Turns alternate, so "until your second turn from now begins" covers this turn and the next three.
14. **The Jade Championship**: "his Spells' actions" is read as an action from a Spell attached to the chosen Shugenja; the Force penalty is -1F for each chosen Shugenja at the card's location (a Follower's location is its host's), of either player and including the Shugenja himself if he is Shadowlands; a chosen Shugenja who leaves play keeps nothing (the list is read only for cards in play). With one Shugenja in play no decision is asked; with none the Event does nothing.
15. **The Madness Spreads**: P4's "Limited as Open" reading 7 applies unchanged (an opportunity on the other player's turn, spent as it is announced).
16. **Chagatai's Wrath**: "Discard this Celestial" is paid as the Limited action's cost, so it is discarded even if ending the Action Phase has nothing to end; outside the Action Phase the action is not offered.
