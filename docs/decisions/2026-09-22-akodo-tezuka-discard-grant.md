# Akodo Tezuka: generalizing the FromDiscard Additional Action grant to Limited/Open (issue #449 follow-up)

Requested by Mendon on 2026-09-22: review Akodo Tezuka (oracle-l5r-298) and wire him if able.

**Result: directly wirable, one small, precedented engine generalization.** His text: "Battle/Open: Discard a card and target a Bushido Virtue in your discard pile: Take an additional action to play it for one of its Battle, Limited, or Open actions, as appropriate to the current phase. Remove it from the game after its action ends."

His blocker note named two gaps:

1. **"Bushido Virtue as a filterable Strategy classification."** Already closed by the Matsu Rika pass earlier today (issue #449): Bushido Virtue is a plain card keyword (`Keyword(card, "Bushido Virtue")`), the exact shape "Dark Virtue" already had for Daigotsu Bofana - exp's own discard-pile target (`own-dark-virtue-in-discard`). `own-bushido-virtue-in-discard` is the same `Require`/candidate shape, keyword swapped.
2. **"An additional action is only ever a Battle action."** Bofana - exp's own `AdditionalActionGrant.FromDiscard` (issue #333) makes only the scoped card's *Battle*-timed abilities playable from the discard pile (`PlayableFromDiscard`'s `ability.Timing == "Battle"` check) -- true for her own printed text ("play it for one of its Battle actions"), but not broad enough for Tezuka's "Battle, Limited, or Open."

## The fix: `AdditionalActionGrant.AnyDiscardTiming`

Rather than widening `PlayableFromDiscard` unconditionally (which would have silently let Bofana's own existing grant play Limited/Open abilities too -- a behavior change to an already-shipped card, not asked for and not printed), the grant itself now carries which shape it is: `AnyDiscardTiming` (default `false`, preserving Bofana's Battle-only behavior byte-for-byte). Tezuka's own grant sets it `true`.

**"As appropriate to the current phase" needed no check of its own.** Once `PlayableFromDiscard` admits Limited/Open for a `AnyDiscardTiming` grant, each Timing still goes through the exact same phase/active-player legality every other use of that ability already does (`Limited`: active player only; `Open`/`Battle`: their own phase). In the Battle phase only the granted card's Battle action is ever offered; in the Action phase only its Limited and Open ones are -- for free, from existing generic legality, not a new rule. `HasUnspentBattleAction` (Bofana's own target-legality helper, checking the card has *some* unspent ability of the relevant Timing before it's even a legal target -- a card with none would only waste the grant) is generalized the same way, to `HasUnspentAction(card, params timings)`, and Bofana's own call site now passes `"Battle"` explicitly, unchanged in effect.

**The "additional action" bypass itself (`ConsumeAdditionalActionGrant`, the Battle-alternation-skipping mechanism) stays Battle-only, untouched.** It exists to let a Battle action jump the normal attacker/defender alternation without ceding priority -- a concept that only applies to Battle actions. A granted Limited or Open play needs no such bypass: Open/Limited aren't rationed by that alternation at all, so falling through to the *ordinary* action-resolution path is correct, not a gap. The grant's only job there was legality (being playable from the discard pile at all), which the broadened `PlayableFromDiscard` already supplies; the existing "an ordinary action that didn't consume a grant forfeits every pre-existing grant" rule then cleans it up exactly as it does for every other grant a player doesn't spend the additional-action way.

**The two-part cost/target shape** ("Discard a card and target a Bushido Virtue...") reuses Kakita Yasunori's own precedent (issue #431, `yasunori-create`): a `Slots` ability with an independent cost slot (`hand`, an ordinary hand card) beside the real target slot (`own-bushido-virtue-in-discard`), rather than a single `Target` a Costs rule discards directly (which only fits when the cost and the target are the same card).

## Regenerated

`docs/CARD_INVENTORY.xlsx` (2165 rows, 603 wired).
