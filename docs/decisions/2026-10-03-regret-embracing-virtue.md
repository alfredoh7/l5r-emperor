# Regret and Embracing Virtue (strategy-wave-105)

Status: accepted for Mendon's rulings (R-026 to R-028 in `docs/rules/RULINGS.md`); the readings below are for him to confirm or correct. Issue #619. Lane: rules, with a client contract.

Both cards were blocked since the #503 pass: Regret needed "several attachments entering play in one resolution" (an entering-play queue and a combined legality check). Both are wired from their scans (`strategy-wave-105`, `RegretEmbracingVirtue.cs`).

## Embracing Virtue (oracle-l5r-2313, War of Honor)

"Before an End Phase begins, if this card is in your discard pile: You may put it in your hand." `EndTurn` asks first (`BeginEmbracingVirtue`), before the End Phase's draw and hand-size check, so a player who takes it back on their own turn can draw above their hand size and must discard (R-026). The question is an ordinary Either/Or (`embracing-virtue-return`, options `return` and `decline`).

## Regret (oracle-l5r-9923, The Shadow's Embrace)

- **Focus Effect** (R-027): the winner's controller loses Honor equal to the cards the winner's side focused (`PendingDuel.ChallengerFocused` / `ChallengedFocused`). Kakita Gonji's "+4 instead of focusing" adds nothing to those lists, so it is not counted. A tie has no winner and costs nothing.
- **Open attach** (R-028): two slots, the cards (`attachments`: Followers and Items in your own Fate discard pile, one or more) and a repeatable list of Personalities (`personality`); the i-th Personality takes the i-th card. Priced by the new `GoldCostBasis.AttachSlotsCost`: each card's own Equip cost for its own Personality, added up and paid once. Legality is `RequireAttachable` for each pair, with the cards before it already standing where they will (`ValidateRegretAttach`), so two Weapons or two Armor for one Personality, two copies of a Unique card, or a host that refuses Followers or Items refuse the whole action with nothing changed. A Spell is not a candidate: this is not Equip (Battle), and no additional action is granted.
- **Entering play**: each attached card opens its own "after this enters play" window, one after another (`MatchState.Regret.EntryQueue`); the next opens when the previous has closed (`HandleRegisteredEntryReaction` calls `ContinueRegretEntryQueue`).
- `ExecuteAttach` now calls the new `PlaceAttachment` for everything short of the window; behaviour is unchanged.

## Readings for Mendon to confirm or correct

1. "An End Phase" is any player's: at every End Phase, each player with a copy in his Fate discard pile is asked (the active player first), once per copy per turn.
2. The cards focused are counted as the duel's focused list stands when the Focus Effect resolves; a card a Duelist replaced is no longer in it.
3. Regret's Gold is one payment for the whole attach, and it is a payment for attachments: a Holding that produces more Gold when paying for an attachment (Platinum Mine) always does, whatever number of cards are attached (Mendon: it applies to every card, on every use).
4. An attached card whose entering-play window opens later than another's does not apply its mandatory entry effects (a Destined draw, an entry Honor loss) until its own turn in the queue.
5. Regret's Open action is an Action Phase action (the engine's Open timing); it is not offered in a battle.

## Client contract (Alfredo)

The Regret option carries two slots in `ActionOption.Slots`: `attachments` (Min 1, Max 99) and `personality` (Min 1, Max 99, repeatable, the same count as the cards). The client must collect the pairing by position and read the price from `QuoteAbilityGold` for the whole selection (`GoldQuote.Kind` "derived", `Basis` names each term). Embracing Virtue needs only the existing Either/Or view (`chooseEitherOr`, spec `embracing-virtue-return`). New JSON state: `MatchState.Regret` (`EntryQueue`, `PlayerId`, property order 1130).
