# Six cards Mendon's rulings unblocked, and the primitives they share (issue #821)

Status: **accepted** (Mendon's rulings of 2026-10-10, recorded as R-042 to R-048). Lane: rules. Engine: `src/L5R.Core/Wave821.cs`, `ArmyRestrictions.cs`, `GiftOffer.cs`; waves `personality-wave-821`, `stronghold-wave-821`, `celestial-wave-821` and `holding-wave-821` (`tools/content/package_wave_821.py`).

| Card | Wired as |
|---|---|
| Hida Kaiji - exp (oracle-l5r-10831) | `RestrictionKind.SoleUnit` (a side-of-battlefield assignment rule) and a Force term |
| Isawa Ryuzo (oracle-l5r-10476) | Fire Open (`w821-ryuzo-pair`) storing one `RestrictionKind.NoSameArmy` pair per use |
| Chuda Kaito (oracle-l5r-10320) | +3 Gold Cost while the effects of a Spell action he performed resolve; Damning Ceremony reads it |
| Ikoma Satoru (oracle-l5r-10291) | Limited gift (`w821-satoru-gift`) on the shared gift step, then a Melee Attack in the other player's home |
| Isawa Tsumaro (oracle-l5r-10863) | Air Home Engage (`w821-tsumaro-illusion`); the Illusion lends Home to the target's Spell actions |
| The Governor's Estate (oracle-l5r-8106) | a Dynasty-phase window creating a bowed Imperial Estate Holding; its Limited and Battle actions |
| Sadahako's Artistry (oracle-l5r-10186) | *not in the rulings*: its gift is the same shared step with a different outcome, so it is wired too (see reading 9) |

The Imperial Estate (`user-scan-imperial-estate`) is the Holding the Governor's Estate creates, from the scan Mendon supplied.

## What Mendon ruled (2026-10-10)

- **Governor's Estate.** Instead of playing a card face-down and treating it as a Holding, read it as *creating* a card that is a Holding. The face-down card is removed from the game (shown in the removed-from-the-game pile only if it had been revealed before), and the created Holding is put into play. Scan: the supplied "Imperial Estate", a 0-cost Holding that produces 2 Gold.
- **Hida Kaiji.** He is never assigned so that he joins a battlefield with other units on his side; the opposition does not matter. Build a primitive that counts the units of his side at a battlefield: if it is above zero he will not assign, and if it is above zero and he is already there, other units cannot assign. Other units may *Move* there during the engage or battle action segment. The opposing army is never considered.
- **Chuda Kaito.** +3GC is 3 added to his Gold Cost "while the effects of a Spell are resolving"; at least one Spell or Reaction checks a Shugenja's Gold Cost (Damning Ceremony), and then he is considered to have 5.
- **Satoru** uses the exact same mechanism as Naoharu's Gift (show a hand card to the other player, who may put it in his hand); only the outcome differs (Honor gain or cards drawn for Naoharu's Gift; a Melee Attack or cards drawn for Satoru).
- **Isawa Ryuzo** needs a primitive that blocks two Personalities from joining one another at a battlefield, like Kaiji's extended to every Personality in his army: reuse Kaiji's if efficient, limited to the two targets. Ryuzo is not Unique, so there can be three pairs, a Personality can be a member of up to three pairs and so be unable to assign with three others, and pairs are independent of one another.
- **Home Reactions and Home Engage** behave like Home Battle at their own timing (the Reaction trigger, or the Engage window).
- **Tsumaro's Illusion**, while at the battlefield, gives the original Shugenja's Spell actions Home. Destroyed, sent home or moved to another battle, the Spells lose it; returned to the battlefield they regain it. When the battle resolves they lose it, so moving the Illusion to a new battlefield does not carry Home there.

## What was built

- **`SideUnitCount` / `SideUnitsAt`** (ArmyRestrictions.cs): the Personalities of a card's own side at a battlefield, those standing there and those the same command assigns there. Both rules read it; nothing about the other side. `RequireArmyMayAssign` is called from the existing P11 pair check in `MatchEngine.Assign`; `ArmyAssignmentTemplate` keeps the offered "assign everyone" command legal, and Kaiji is offered "alone".
- **`NoSameArmy`** is stored per use (`AddSameArmyPair`), one row per pair, so three pairs on one Personality are three rows and a pair touches no other row. It is judged both ways round.
- **Chuda Kaito**: `ExecuteAbilityEffects` brackets the Spell's effects; `CardGoldCost` carries a +3 term for the card while it is inside the bracket. Damning Ceremony now reads `Definition(wielder).GoldCost` plus that term.
- **The gift step** (GiftOffer.cs): `BeginGift` shows the card (the public shown-cards record, no card id in the log) and opens the other player's Either/Or decision (put it in your hand / refuse); `TransferGift` moves it (OwnerId, OriginalOwnerId) and is now also what Naoharu's Gift calls. The Satoru and Sadahako outcomes are two specs on it.
- **Satoru's Attack**: a new `AttackTargetScope.EnemyHome` (an enemy Follower, or a Personality without Followers, in the other player's home; no battle needed). The target is chosen as the Attack is made (an Either/Or for the giver; one legal target is taken automatically), with every Attack target rule re-used (`RequireLegalAttackTarget`).
- **Home on a Reaction**: `Wave821Validate` applies to any Reaction with the Home flag whose source is a Personality or attachment: its unit may be at home, or at the current battlefield, never at another battlefield. Nothing changes for a Reaction without the flag.
- **Tsumaro's grant**: an `IllusionLink` (Illusion, Shugenja, battlefield, battle number) is recorded as the Illusion is created; `Wave821GrantsHome` is read by `HomeWaivesSourceLocation` and is true only for a Spell attached to the target, while the Illusion stands at the battlefield of the battle it was created in and that battle is the current one.
- **The Governor's Estate window**: `SetPhase` ends by opening a `dynastyBeginReaction` window for the active player when he controls the Estate and could take the Reaction (2 Gold, a province card). The Reaction may be used twice, the window stays open for the second, then closes; declining it closes it for the turn. A face-down province card is removed with `FaceUp` false: its log line names no card, and the other player's view of the pile shows a hidden placeholder.
- **Cleared blockers**: Hida Kaiji and Isawa Ryuzo (pe_wave_736), Isawa Tsumaro (pe_wave_735 and pe_677), The Governor's Estate (pe_issue812) and Sadahako's Artistry (pe_holdings). Chuda Kaito and Ikoma Satoru were in no blocker file.

## Readings for Mendon to confirm

1. **The Governor's Estate window.** "After your Dynasty Phase begins" is a window for its owner as the Dynasty Phase begins, after everything else that begins with it. "Twice per turn" is two uses in that window. It is not opened unless the Reaction could be taken (2 Gold payable, a province card to name).
2. **A face-down card stays hidden.** Nothing names it in the log or the other player's view; its owner sees it in his removed-from-the-game pile.
3. **Ryuzo's duration.** The text names none, so a pair holds for the turn it is made. Pairs are per use, so a second Ryuzo (he is not Unique) makes a second pair.
4. **"The same army"** is one side of one battlefield. The two may stand opposed at one battlefield, and may be assigned to different battlefields on one side.
5. **Kaiji counts Personalities.** A Follower is part of its Personality's unit, not a unit. Only the *assignment* command is restricted (Mendon: other units may Move there); an effect that assigns a Personality by another route (a Reaction that "assigns" one) is not.
6. **Kaito's +3GC covers the synchronous resolution of the Spell action's effects.** An effect that stops for a choice mid-way does not carry the +3 over that wait. Damning Ceremony resolves in one step, so it is covered.
7. **Satoru.** "Choose another player" is the other player (a two-player game has one). The Melee Attack's target is chosen at resolution, not announcement, so the other player decides without knowing it. If he cannot put the card in his hand (The Scorpion Wall is Finished) only the refusal is legal and it is taken.
8. **Tsumaro.** "Your target Air Shugenja" is any Air Shugenja of yours in play, Tsumaro himself included (he is one). The Illusion is created on his side at the current battlefield and removed after the battle. Home is granted to every Spell attached to the target.
9. **Sadahako's Artistry** is wired although the rulings do not name it, because its gift is the step Satoru shares: "Family Honor" is the player's Honor, taking the gift gains Honor equal to its Focus Value, refusing discards it and draws three, and the Celestial is discarded as the cost. Its blocker note asked whether Naoharu's Gift's ownership reading carries over (the gift stays its giver's for discard-pile purposes); Mendon's "the exact same mechanism" is taken as yes. Say if it should not be wired.
10. **"Even if this card is bowed"** on the Estate's Battle action is Tireless; "+2F or -2F" is two offered abilities that share one use.
11. **The stronghold's first printed sentence** ("Your face-down Dynasty cards in play are Holdings ...") is not implemented as printed: R-042 replaces face-down-in-play with creation.

## Not covered by a test

- Satoru's Attack when the other player holds a Reaction to it (the attack reaction window): the same deferred-Attack path every Attack uses, with a hook that leaves that window open; it is exercised only through the immediate path.
- Kaiji and Ryuzo on the *defending* assignment: it shares `Assign` with the attacking one.
- Moving a Personality to Kaiji's battlefield (Move is unrestricted by design).
- The client's presentation of the new window and of a hidden removed card: a new `dynastyBeginReaction` pending type with engine-built actions (Reaction and "Pass reaction") and a hidden-card placeholder in `RemovedFromGame`.
