# Wave batch 3: the stat-term and Experienced cards (issue #734)

Status: **implemented on `claude/734-wave-batch-3`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Batch 3 of [the wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md) (umbrella #742). Engine: `src/L5R.Core/Wave734.cs`. Packager: `tools/content/package_wave_734.py`. Tests: `Wave734Tests` (`tests/L5R.Core.Tests/Wave734Tests.cs`, `Wave734Tests.More.cs`), suite `wave-734`.

## What is wired

19 of the batch's 22 cards, in six profiles: personality-wave-148 (11), item-wave-61 (3), spell-wave-68 (1), strategy-wave-132 (2), holding-wave-66 (1), event-wave-33 (1). Every card was read against its own scan; the scan wins where the catalogue text differs (Usagi Seki: the scan says "other players' **cards** are resolving", the catalogue says "actions"). The profile numbers skip 147 / 60 / 67 / 131 / 65 so a concurrent batch can take the next free numbers.

| Card | Built as |
|---|---|
| Mumoku no Oni | printed Elite; `EntryHonorLoss` 4; Effect `w734-province-cap` read by `ProvinceStrengthMax` |
| Shosuro Tanihara | the same Effect: while attacking at a battlefield, that Province reads at most its printed strength |
| Usagi Seki | `PersonalityForce`: +5F while another player's ability (or Attack) is the command resolving |
| Isawa Norimichi - exp | `PersonalityForce`: +1F to his controller's other Earth Shugenja; `w734-norimichi-raise` / `-lower`: this Province +2 / -2 strength for the turn |
| Utaku Ji-Yun - exp | the Paragon exception in `DishonorablePersonalHonorOverridden`; `w734-jiyun-lock` (Reaction, `after-engaging-at-own-battlefield`) locks an opposing Personality's actions |
| Bayushi Nitoshi - exp | `ApplyWave734EntryEffects`: every player loses 3 Honor; `w734-nitoshi-attack` (Ninja Battle: Melee 6 Attack) |
| Hantei XVI - inexperienced | P3's `favor-ignores-honor-requirement`; `w734-hantei-search` (a trait modelled with Reaction timing) searches the Fate discard pile or Fate deck for a non-Unique Political Strategy |
| Shosuro Chizuru | `w734-chizuru-force` / `-honor`: discard a hand card as the cost; the bonus equals its Focus Value; a Personal Honor ceiling of 4 on the target for the turn |
| Doji Hoturi - inexperienced | `w734-hoturi-duel`: P18 rules (refusal costs dishonor and moving home, only an honorable target may refuse); destroy the loser |
| Isawa Tsuke | `w734-tsuke-duel`: Fire Limited, bow; P18 `MinFocusEach` 1; destroy the loser |
| Iuchi Karasu - inexperienced | `w734-karasu-switch`: Home Battle, P25 slots, `SwitchLocations`, then straighten each who is neither Fallen nor Nonhuman |
| Armor of the Mad | `AttachGrantsKeyword` Fallen; a Personal Honor cap of 0 on its wielder; `w734-armor-madness` gives one or two enemy cards a Madness token |
| Kshatriya Artifacts | `PersonalityForce`: -1F to opposing Fudo and Fallen Personalities, per Item |
| Stonemason's Hammer | `PersonalityForce`: +1F to a Siege wielder; `w734-hammer-province`: this Province +2 strength |
| The Sky's Barrier | `w734-barrier`: sacrifice the Spell as the cost; the target gets Elite and contributes no Force while unbowed for the turn; Air wielder gains 2 Honor (read from the destroyed Spell's `LastHostId`) |
| Ready the Defenses | `w734-ready-defenses`: a turn record of the named Province, the player and whether the performer is a Tactician; `PersonalityForce` / `FollowerForce` read it |
| Unseemly Alliance | `w734-alliance-bow`: bows a Courtier; -1F to every Personality in an army for each distinct Clan alignment in that army, per copy played this turn |
| Well-Defended Farm | printed `GoldProduction` 2; +1F to the controller's opposed Ashigaru Personalities and Followers |
| Riding to the Rescue | the Event stays in play to its owner's next turn (`KeepEventInPlay`); +1F to that player's attacking, opposed Cavalry Personalities and Followers |

## Not wired

Each has a blocker note in `tools/content/pe_wave_734_blockers.json` and is not published, so no printed effect is dropped.

- **Bayushi Kachiko.** "After the next time this turn a target Personality attacks, his controller loses 2 Honor" needs a ruling (does "attacks" mean he is declared into an attacking army, or that he makes an Attack action?) and a one-shot turn watch. Her -1F static term is plain P9 data once the reading is chosen.
- **Kokujin.** "If Kokujin wins, you may destroy one of your Personalities (at any location) to take control of the loser" is a choice made after the duel resolves, among one option per Personality plus plain destruction. Duel outcomes apply directly here; a deferred outcome decision needs the P1 after-action trait queue extended or a new post-duel decision hook.
- **Warded Premises.** "Negate all Force bonuses from actions to cards at this battlefield" needs the engine to tell an action's Force modifier from a token, an Item, a Permanent modifier and a static term, and a ruling on whether a permanent bonus is negated for good.

## Shared lines edited

- `src/L5R.Core/TerrainStatics.cs`, `ProvinceStrengthMax`: one more ceiling, `Wave734ProvinceCapped`.
- `src/L5R.Core/MatchEngine.cs`: `ApplyMandatoryEntryEffects` calls `ApplyWave734EntryEffects`; `CardPersonalHonor` is now the capped read of `CardPersonalHonorUncapped` (identical when no cap applies); `DishonorablePersonalHonorOverridden` also asks `Wave734ParagonsOverride`; the `Contribution` local in `ArmyForces` also asks `Wave734ContributesNothing`.
- `src/L5R.Core/Dueling.cs`, `ApplyDuelOutcome`: one case line for Hoturi's and Tsuke's destruction of the loser.
- `src/L5R.Core/EmperorRulingR4.cs`, `TraitReactions.Is`: `Wave734Abilities.IsTrait` (Hantei's search prints no "Reaction:" designator).
- New persisted state: `MatchState.Wave734HonorCaps`, `Wave734Defenses`, `Wave734Barriers`, `Wave734AllianceTurns` (`JsonPropertyOrder` 22910 to 22913), omitted until a card writes them and pruned to the current turn whenever one is added.

## Readings for Mendon to confirm or correct

1. **Seki, "other players' cards are resolving".** Read as: an ability of another player's card (an action, a Reaction, or an Attack it made) is the command being resolved. An Event, a trait or a rulebook action does not count.
2. **Hoturi and Tsuke target an enemy Personality.** The printed "a target Personality" is read as the usual duel pairing, a Personality of another player.
3. **Ji-Yun's "may not use his abilities".** Read as the engine's existing action lock ("may not perform actions") until the end of the turn. Traits are not locked.
4. **Ready the Defenses.** "The province's battlefields" is the battlefield of that Province while its owner is the Defender; "there" is the same battlefield. The effect lasts the turn it is played in. "Other players'" is every unit the Defense's player does not control.
5. **Unseemly Alliance.** An army's Clan alignments are the distinct alignments of its Personalities (a Personality with two alignments counts both); each copy played this turn counts. Followers are not Personalities, so they are not reduced; a Personality at home is in no army.
6. **The Sky's Barrier.** "He does not contribute Force while unbowed" lasts the turn. Elite makes a bowed Personality contribute, so the pair means he contributes only while bowed. The Honor is gained when the Spell's host, read from the destroyed Spell, is Air.
7. **Shosuro Chizuru.** The player chooses Force or Personal Honor by choosing which of the two offered abilities to use; the Personal Honor ceiling of 4 lasts the turn and applies to the target whichever bonus was chosen.
8. **Armor of the Mad.** The cap is a maximum on the current Personal Honor, not a replacement: a Personality with less keeps it.
9. **Karasu.** The two Personalities are straightened whether or not they moved, unless Fallen or Nonhuman; a unit's Followers are not straightened.
10. **Riding to the Rescue.** Its printed "Limited:" is ignored as for every Gates of Chaos Event (the supplement ruling already applied to the Aftermath Events); it resolves when revealed and lasts the owner's turn.
11. **Isawa Norimichi.** "This Province" is the current battlefield's Province, as for Expensive Achievement.

## Art pack

The new scans are covered by `catalogue-art-issue734-20261007-r1` (4,497 images, 601,836,978 bytes, SHA-256 `b9f34b360de7f470aa74df19b305d4a8c63f61efd0f10410038a18f59ea56e1c`), built from the batch 1 pack `catalogue-art-issue732-20261007-r1` (4,478 images) with `--base-archive`. Native visual and playability review has not been done.
