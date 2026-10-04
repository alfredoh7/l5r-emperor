# Regret and Embracing Virtue (strategy-wave-105)

Status: accepted; Mendon's rulings are R-026 to R-030 in `docs/rules/RULINGS.md`. Issue #619. Lane: rules, with a client contract.

Both cards were blocked since the #503 pass: Regret needed "several attachments entering play in one resolution" (an entering-play queue and a combined legality check). Both are wired from their scans (`strategy-wave-105`, `RegretEmbracingVirtue.cs`).

## Embracing Virtue (oracle-l5r-2313, War of Honor)

"Before an End Phase begins, if this card is in your discard pile: You may put it in your hand." `EndTurn` asks first (`BeginEmbracingVirtue`), before the End Phase's draw and hand-size check, so a player who takes it back on their own turn can draw above their hand size and must discard (R-026). The question is an ordinary Either/Or (`embracing-virtue-return`, options `return` and `decline`).

## Regret (oracle-l5r-9923, The Shadow's Embrace)

- **Focus Effect** (R-027): the winner's controller loses Honor equal to the cards the winner's side focused (`PendingDuel.ChallengerFocused` / `ChallengedFocused`). Kakita Gonji's "+4 instead of focusing" adds nothing to those lists, so it is not counted. A tie has no winner and costs nothing.
- **Open attach** (R-028, R-029): two slots, the cards (`attachments`: Followers and Items in your own Fate discard pile, one or more) and a repeatable list of Personalities (`personality`); the i-th Personality takes the i-th card. The announcement judges the whole selection as the cards will stand one after another (`ValidateRegretAttach`: two Weapons or two Armor for one Personality, two copies of a Unique card, or a host that refuses Followers or Items are refused with nothing changed). The action itself costs nothing; then each card is its own equip, in order: an Either/Or (`regret-attach-card`) offers "attach, paying N Gold" (its Equip cost, paid for that attachment, so Platinum Mine counts every time), the card attaches and its entering-play window opens and closes, and only then does the next card begin. A card that is no longer legal, or cannot be paid for when its turn comes, stays in the pile. Not Equip (Battle): no additional action, no Spell. Action Phase only (R-030).
- `ExecuteAttach` calls `PlaceAttachment` for everything short of the window; behaviour is unchanged.

## Readings, now settled (2026-10-04)

1. Any End Phase: confirmed (R-026). 2. Any "in place of focusing" ability is not a focused card: confirmed (R-027). 3. Per-card payment, as an attachment: ruled (R-029). 4. Each card attaches, then its entering-play effects, then the next: ruled (R-029). 5. Action Phase only unless an effect grants Open in a battle: ruled (R-030).

Still the implementation's own reading: a card a Duelist replaced is no longer in the focused list when Regret counts; the "reaction" to each card's equip is the window of its entering play (no separate before-action window per card).

## Client contract (Alfredo)

The Regret option carries two slots in `ActionOption.Slots`: `attachments` (Min 1, Max 99) and `personality` (Min 1, Max 99, repeatable, the same count as the cards). The client must collect the pairing by position. The command carries no payment: each card then arrives as an ordinary Either/Or (`chooseEitherOr`, spec `regret-attach-card`, option `attach` with its own `GoldQuote`, or `skip` when it cannot be done), one card at a time. Embracing Virtue needs only the existing Either/Or view (`chooseEitherOr`, spec `embracing-virtue-return`). New JSON state: `MatchState.Regret` (`Active`, `PlayerId`, `SourceId`, `Steps`, `JustAttachedId`, `Paused`; property order 1130).
