# Bonds of Hospitality and Marriage of the Emerald Champion (issue #765)

Status: **implemented on `claude/765-bonds-marriage`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Follow-up to [the wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md) (umbrella #742): the two cards batch 2 (#733) and batch 5 (#736) deferred, which the card-inventory audit (PR #764) found buildable on primitives merged since. Engine: `src/L5R.Core/Wave742.cs`. Packager: `tools/content/package_wave_742.py`. Tests: `tests/L5R.Core.Tests/Wave742Tests.cs` (suite `wave-742`, 5 tests). Waves: strategy-wave-199, event-wave-80. Nothing here is a confirmed ruling.

## What is wired

Both cards, each read against its own scan; the scans match the catalogue text.

| Card | Built as |
|---|---|
| Bonds of Hospitality | `w742-bonds-play` (Limited, 2 Gold, target a bowed Personality): the Strategy goes from the hand to `Zone.EventInPlay` (the place Uncovering Treasures stays), a `StraightenHold` tied to it (`whileInPlay`, never released by a point of any turn, Fortress of Lidless Eyes' shape) holds the target bowed, and the target's controller is granted the player ability `w742-bonds-discard` (Open, Pay 4 Gold, target a Bonds of Hospitality in play) for as long as the card stays |
| Marriage of the Emerald Champion | an Event decision at reveal (the Jade Championship's shape): the owner names one of his Personalities in play, recorded in `EventTargets`; `KeepEventInPlay(turnBeginsIn: 2)` discards it as the owner's second turn from now begins; a P11 `NoDishonor` static restriction on the named Personality while the Event stays; a P14 Honor-change registrant that reduces a loss of the owner's to 0 when another player's action named the Personality as a target |

## Shared code touched

- `KeptInPlay(card)` (new, `Wave742.cs`): in play, or kept in play in the Event zone. `StraightenHolds.cs` (two reads of a hold's `WhileInPlay` card) and `TurnBoundaryGrants.cs` (`PlayerGrantLive`) now ask it instead of `InPlay`, so a hold or a grant tied to a Strategy or Event kept in play lives as long as it does. No existing hold or grant names such a card, so nothing else changes.
- `CardAbilities.cs`: one validation branch and one candidate term for the new out-of-play-zone target category `w742-bonds-card`, beside Suikihime's Sanction's (`w735-...`).
- `EmperorRemainingFEvents.cs`: one line in each default arm of the Event-choice resolver and its action list, beside the batch 7 ones.
- `MatchEngine.BringProvincePersonalityIntoPlay` (an ability recruit from a province) now finds the card's slot with `FindProvinceSlot`, so a card in the second slot of Otosan Uchi's two-card province no longer throws. The bot soak found it once the two new cards shifted its seeded decks (Masaya's recruit from a second slot); it is the limitation OtosanUchiPlains.cs documents. `MasayaSecondSlot` in `Wave742Tests` fails without the fix.
- No new saved-state field: the hold lives on the card, the grant in `PlayerAbilityGrants`, the Event's target in `EventTargets`.
- Guard tests: the "deferred, not published" lists of `Wave733Tests` and `Wave736Tests` no longer name Marriage and Bonds. Both cards leave their old blocker notes.

## Readings for Mendon to confirm or correct

1. **Bonds stays in play until discarded.** "While this card remains in play" is read literally: it stays when its target dies, and the target is held bowed only for as long as the card is in play. A target that left play does not end the card.
2. **Bonds lives in the Event zone.** A Strategy "put into play" has no printed place; it sits where Uncovering Treasures does and does not attach to the target. It therefore does not count as an attachment of the Personality and is not destroyed with him.
3. **"His controller" is the target's controller when the card is played.** The grant is made to that player and does not follow a later change of control.
4. **The granted discard names any Bonds of Hospitality in play.** The ability is "Discard *a* Bonds of Hospitality", so its holder may discard any copy, including another player's. Two copies grant separately, and the ability is held while either lives. The card goes to its owner's discard pile.
5. **Bonds' target may be any player's bowed Personality**, including the player's own; the text names no owner.
6. **"Negate the target's straightening"** is the straighten hold: his controller's Straighten Phase and every effect that would straighten him are refused while the card stays. When it leaves play he straightens at the next opportunity (nothing straightens him at the moment of the discard).
7. **Marriage resolves when revealed.** The printed "Limited: Put this Event into play" is the Event's reveal, as every Event wired so far. With no Personality of the owner's in play it is simply discarded.
8. **"After your second turn from now begins" is the engine's `turnBeginsIn: 2`**, the same as Distant Expansion and Incredible Tragedy's "second turn from now".
9. **"Actions that have targeted him"** is the P14 "targeted-by-other" reading: another player's action whose command names the Personality as its target (or one of its chosen cards). An action that hurts the owner without naming him (an area effect, or an effect aimed at his unit but not him) is not stopped. The Honor loss is reduced to 0, not redirected.
10. **Marriage's two protections ignore each other's sources.** "He will not be dishonored" holds against every source, the owner's own cards included; "you will not lose Honor" applies only to other players' actions.
11. **Only the Event's owner is protected from the Honor loss**, not the Personality's later controller if control changes.
