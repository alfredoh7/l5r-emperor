# Gold payment rulings: the second player's payment, current-cost totals, A Stain Cleansed, At Any Cost, Disgraceful Conduct

Status: accepted (Mendon, standing authority; rulings verbatim below). Lane: shared (rules engine, MatchView, an engine-owned decision the client must present). Issue #373. Extends [2026-09-18-generalized-gold-payment.md](2026-09-18-generalized-gold-payment.md), whose four open questions these answer.

## Rulings (Mendon)

1. **Virtuous Charity** (`oracle-l5r-10181`, "Open: If it is not your turn, choose another player: You come across some starving peasants in need. You, then the player, each pay zero or more Gold. Whoever paid more Gold gains Honor equal to the difference between the two amounts paid, or 4, whichever is less."): "the second player should see how much the first player paid and should be prompted to pay using holdings (they may pay any value, including 0)."
2. **Total Gold Cost of a unit or Personality**: "should be the total of the CURRENT cost of every card in the unit. Some cards have Gold cost reduction effects (including tokens and actions). These should be taken into account when summing the cost of a unit or a Personality for action effects."
3. **A Stain Cleansed** (`oracle-l5r-82`): first ruled "the current cost of all cards in a target unit less 2. So, if the unit is Daidoji Ebizo with no attachments, the cost of the action would be 7 (9-2)"; then corrected to the printed (Oracle) reading: "A 9-cost unit would be 11." The player picks the target, the engine prices the action at the target's current Gold cost (a Personality's whole unit's current total) plus 2, exactly; the player pays it through the Gold payment flow; the card or unit is destroyed.
4. **At Any Cost** (`oracle-l5r-639`): "has the same entry requirements as any recruit action (This Personality may not be proclaimed), so the restrictions hold and Singular, Unique, Sensei, Honor Requirement, and Loyal still impact recruitment." **Disgraceful Conduct** (`oracle-l5r-2020`) uses the same total (current cost, ruling 2) and the payment path.

## What was built

### The second player's variable payment (Virtuous Charity; `OpponentPayment.cs`)

A general primitive, the numeric sibling of the Either/Or choice (`EitherOr.cs`): an action whose text has the OTHER player pay "zero or more Gold".

- The acting player's amount is the ability's own variable Gold cost (`Cost: GoldCostRule.Variable()`), so it is quoted, chosen (`GameCommand.GoldAmount`) and paid exactly like every other variable cost. It is a cost of the action, so an action a reaction negates still pays it; nothing else about the second player's payment happens then. "Choose another player" is the one opponent, so nothing is asked about who.
- Once the amount is paid, the played Strategy goes to the discard pile (in the open) and a `PendingOpponentPayment` (state) with `PendingChoice.Type == "opponentPayment"` hands the decision to the other player, who holds priority. `QueryActions` offers them one action, `chooseOpponentPayment`, carrying a variable `GoldQuote` (every amount from 0 to what they can produce, each with suggested sources; `Sakuma` and every other Gold source the payment path knows are counted) and a complete default command (`GoldAmount: 0`, no sources), so a bot or any legal-actions-driven client can always answer. The answer is the same command with `GoldAmount` and `SourceIds`; it goes through `Pay`, so sources are bowed and excess production is lost as in every payment. An amount above what they can produce, a negative one, short, foreign, bowed or duplicated sources, sources for a zero amount, and any other command are rejected atomically. Only the deciding player may answer.
- When the second player can produce no Gold there is nothing to choose: they pay nothing, `opponentPaymentAutoResolved` is announced and the action finishes without a decision (the Either/Or convention).
- What the two amounts do is a per-card spec (`OpponentPaymentSpecs`, `OpponentPaymentHonor`). Virtuous Charity's: whoever paid more gains Honor equal to the difference, capped at 4; equal amounts move nothing. A fixture spec (`training-opponent-payment`, each player gains Honor equal to what the other paid) proves the seam is not Virtuous Charity's own.
- The action then finishes in the order it always would have (`CompleteAbilityResolution`, shared with the conditional effect choice), priority passing on as after any action. Seppuku is not offered and is rejected while the decision is open (as for Either/Or and copy).
- The practice opponent answers with the engine's default (paying nothing).

**Hidden information: what each viewer sees.** `MatchView.PendingOpponentPayment` (`OpponentPaymentView`) is present in both views while the decision is open.

| Field | Acting player | Second player (deciding) |
| --- | --- | --- |
| card, who is acting, who decides, `FirstAmount`, `Prompt`, `Detail` (the Honor at stake in the card's terms) | yes | yes (the second player is shown the first amount before choosing) |
| `Gold` (the variable quote with suggested sources per amount) | no | yes |
| `Outcomes` (what each amount would do, in the deciding player's words) | no | yes |
| the amount they will choose | unknown until submitted | their own choice |

Nothing else is hidden information: Gold sources in play are public cards. The acting player's hand stays hidden throughout (the Strategy is already in the discard pile). Test: `GoldRulingsTests.SecondPlayerSeesFirstAmount`.

The client work (a decision frame for the second player, reusing the payment panel, which can serve unchanged: what is missing is a way in for a player who is not acting) is issue #377. The engine ships a scripted position for it: `MatchEngine.CreateOpponentPaymentShowcase`, scenario `opponent-payment-showcase` (the Crane passes, the Lion plays Virtuous Charity, the Crane pays); `GoldRulingsTests.ShowcaseFlow` plays it through and replays it.

### Total Gold cost is the current cost (ruling 2)

Already true on `main` when this work started: `MatchEngine.CardGoldCost` (issue #336) is the printed cost plus every `GoldCost` modifier and token, never below 0, and `UnitTotalGoldCost` sums it over the Personality and each attachment (Followers, Items, Spells). The line in the 2026-09-18 doc that said "printed costs" described that change's first version; it is superseded. This work pins the ruling through the derived-cost cards: Disgraceful Conduct and A Stain Cleansed are priced term by term from current costs (a modifier, a token, both, a raise, an attachment's own reduction, a cost reduced to 0 counting as 0, a Personality reduced to 0 leaving the attachments), and the price floor is applied to the total. Entry discounts ("enters play paying 1 less Gold if you are a Crane Clan player", Katagi, Hotako, Prosperous Village) are payment discounts, not changes to a card's Gold cost, and do not enter the total (`DisgracefulZeroAndFloor`). The readers that stay on the printed cost are those whose text says so ("base Gold Cost": Yasuki Tsujiken, Reckless Rush) and cards not in play (`target-printed`).

### At Any Cost (`oracle-l5r-639`, `at-any-cost-recruit`)

`Dark Virtue Battle, :gstar:: Recruit your target performing dead Samurai.` A Battle action (so it needs a unit at the current battlefield) whose target is one of your own dead Samurai (`own-dead-samurai`). The price and every restriction come from one place: `QuotePurchase(..., fromDeadPile: true)`, the recruit path's own quote, where the only difference from a province recruit is where the card is. So Unique and Singular, Honor Requirement (with no Clan allegiance bypass, as for Shinden Shorai and Your Clan Needs You!, whose recruits are "all costs"), Loyal, and every entry discount apply exactly as for any recruit, and the quote is never Proclaim-eligible. The Personality enters play at Home through the same entry sequence as a province recruit (`EnterPlayFromRecruit`, `OpenRecruitEntryWindows`, refactored out of `BringProvincePersonalityIntoPlay` with no change to it), so Sensei discards the player's other Sensei, Hotako's Honor loss applies and entry Reactions open. He returns as a new card: `ResetCardState` (also used by the Overlay transfer) clears status, modifiers, keywords, uses and tokens, so a Personality who died dishonorable and bowed returns honorable and straight. The played Strategy is spent as it is played.

### Disgraceful Conduct (`oracle-l5r-2020`, `disgraceful-conduct-bow`)

`Battle: Even if you control no units at the current battlefield, pay Gold equal to a target Personality or unit's total Gold cost minus 4: Bow it.` An `Absent` Battle action (no unit of yours needed; its target still has to be at the battlefield), either side's Personality, priced `TargetUnitTotal(-4, floor 0)`, bowing the Personality (Stalwart-style negations apply as for every bow).

### A Stain Cleansed (`oracle-l5r-82`, `a-stain-cleansed-destroy`)

`Battle, :gstar:: Target your performing unbowed Magistrate to target an enemy card without attachments with Gold Cost equal to what you paid minus 2 or an enemy Personality whose unit's total Gold Cost is equal what you paid minus 2. Destroy the card or unit.` Performer: your unbowed Magistrate at the battlefield. Target: an enemy Personality (priced by his unit's current total) or an enemy attachment (its own current cost), `TargetUnitTotal(2)`: cost + 2, exact. Each legal target ships its own quote; a target you cannot afford is not offered (and with none affordable the action is disabled). Daidoji Ebizo alone costs 11. A Personality's unit is destroyed with him; an attachment goes alone (its Personality stays).

## Behaviour changes to existing cards

None from ruling 2: `main` already summed current costs (Gold and Steel's "equal or lower Gold Cost", Bonds of Coin's "0 Gold Cost" and the unit total all read `CardGoldCost`). `GoldQuotes.TargetRecruit` now also prices a dead Personality and its basis names the discounts that applied ("Daigotsu Hotako recruit cost 9 (Daigotsu Hotako cost 11 − 2 (Lion Clan))"); the province recruit quotes are unchanged. `MatchEngine.QuotePurchase` gained one optional parameter (`fromDeadPile`); every existing caller is unchanged. Discovered and left as it is: Your Clan Needs You! returns early when its recruit opens a window, which skips the tail that discards a hand-played Strategy; At Any Cost spends its card explicitly before it can.

## Readings to confirm (Mendon)

1. **A Stain Cleansed's direction.** Implemented as ruled (price = current cost + 2, "a 9-cost unit would be 11"), which is the Oracle text ("Gold Cost equal to what you paid minus 2"). The Emperor Edition scan of this card (`oracle-l5r-82`, read in this work) prints a different sentence: "Battle: Choose your performing unbowed Magistrate, target an enemy card without attachments or an enemy unit, and pay Gold equal to the card or unit's total Gold Cost minus 2: Destroy it", that is price = cost - 2 (Ebizo 7), the arithmetic first ruled. The project convention is that published text is the current Oracle text, which is why the ruling was applied; the sign is one number, `GoldCostRule.TargetUnitTotal(2)` on `a-stain-cleansed-destroy` (`-2` gives the scan's price; `StainSignIsOneConstant` names it).
2. **Where At Any Cost puts him.** The Oracle text says only "Recruit", so he enters at Home (the ordinary recruit destination). The scan says "Bring him into play at the current battlefield, paying all costs" (and "If you control any units at the current battlefield", which is every Battle action's own requirement). Entering at the current battlefield is one argument in `BringDeadPersonalityIntoPlay` (`EnterPlayFromRecruit(player, card, _state.CurrentBattleProvince, via)`).
3. **Disgraceful Conduct's "Bow it" for a unit** bows the Personality only; the engine has no bow-a-whole-unit primitive and no card uses one. Say so if his Followers should bow too.
4. **The Virtuous Charity payment is a cost** (paid when the action is announced), the existing payment flow Mendon named. A negated Virtuous Charity therefore still costs the acting player their Gold and gives nobody Honor.
