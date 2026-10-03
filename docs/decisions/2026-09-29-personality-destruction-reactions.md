# Personality-destruction Reaction triggers

**Status:** Confirmed by Mendon, 2026-09-29. **Lane:** rules. **Example card:** Crippled Sensei (oracle-l5r-9899), The Shadow's Embrace.

## Decision

Any Reaction triggered by wording such as "when/after one of your Personalities is destroyed" is eligible when a qualifying Personality **leaves play and becomes dead**. The action type is irrelevant: a Limited, Open, Battle, or Reaction that destroys that Personality can open the window, and so can its death during battle resolution. Converting a Personality that was already discarded into a dead Personality does **not** trigger it, because the card did not leave play at that point. Each card's printed "your" or controller restriction still applies.

Wording that requires the destruction to come **from an action** or **as an effect of an action** narrows the trigger: a battle-resolution casualty does not qualify for that wording. A condition such as "another player's action destroyed" additionally requires the action to have been played by another player. These source restrictions apply on top of the play-to-dead transition.

Concrete cases: Crippled Sensei's unqualified "After one of your Personalities is destroyed" is eligible if one of your Personalities leaves play dead from a Limited action or from battle resolution. A Reaction requiring "another player's action destroyed" is not eligible from battle resolution or from your own action. A Personality that is in a discard pile and only later becomes marked dead is not eligible for either Reaction.

## Evidence and implementation consequence

Mendon's two clarifications in this task on 2026-09-29 supply the trigger rule and its general scope. Crippled Sensei's period text is recorded in `content/deck-pool.json` and in the [remaining-cards pass](2026-09-26-remaining-cards-pass.md). The current after-action Reaction window does not cover every qualifying destruction, including battle resolution. Wiring the card needs a hand-Strategy Reaction window at the play-to-dead transition across those paths; a later discard-to-dead conversion must not open it. Reactions with action-source wording must retain the cause and acting player so those narrower conditions can be checked.

The supplied Maimed Retainer image matches the created Holding's requested stats, keywords, and ability, but it is a 429×600 reference image rather than the requested 750×1050 publication asset. Crippled Sensei remains unwired until the trigger window, created Holding, and card tests are implemented. No issue or PR was opened by this rules clarification.
