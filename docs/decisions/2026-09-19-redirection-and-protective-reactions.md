# Redirection and protective reactions: Mirumoto Ezuno, Blade of Champions, Goju Yurishi, Yasuki Jinn-Kuen exp2, Hida Mochitoko exp

Status: implemented. The rulings quoted from Mendon are his, given with the task on 2026-09-19; every other reading is a decision of
this change, marked with its alternative, and the ones still needing his eye are listed under "Open questions". Lane: rules (mdornbrook;
cross-lane edits under his standing authority). Issue: #335. Waves: `personality-wave-30` (Ezuno, Yurishi, Jinn-Kuen, Mochitoko) and
`item-wave-4` (Blade of Champions). Published count 409 -> 414.

> **Mirumoto Ezuno** (oracle-l5r-5086, Second City) -- Force 4, Chi 3, Honor Requirement 3, Cost 7, Personal Honor 3, Dragon Clan / Samurai /
> Duelist / Yojimbo. Reaction: When another player's action would target one of your Shugenja at Ezuno's location, it targets Ezuno
> instead, if legal.
>
> **Blade of Champions** (oracle-l5r-1004, Second City) -- Item, Weapon, Unique, +3 Force, +1 Chi, Cost 5, Focus 3. Reaction: When a Battle
> action would target a card in this unit, bow this card: The action targets another of your cards instead, if legal.
>
> **Goju Yurishi** (oracle-l5r-2862, Promotional Emperor printing 2) -- Force 4, Chi 3, no Honor Requirement, Cost 7, Personal Honor 0,
> Spider Clan / Shadowlands / Ninja / Dragon Child. After Yurishi enters play: Lose 3 Honor. Reaction: After another player's Battle
> action targets Yurishi: He disappears into the shadows. Move him home. If he is no longer a legal target for the action, negate its
> effects on him.
>
> **Yasuki Jinn-Kuen, Experienced 2** (oracle-l5r-9445, Second City) -- Force 2, Chi 4, no Honor Requirement, Cost 7, Personal Honor 1, Crab
> Clan / Courtier / Daimyo / Kolat / Merchant / Loyal / Unique / Master Coin. Political Open: Target a Holding or Stronghold: Straighten it.
> Political Reaction: After an action targets your Merchant, pay 2 Gold: Negate the action's effects.
>
> **Hida Mochitoko, Experienced** (oracle-l5r-3152, Second City) -- Force 6, Chi 3, Honor Requirement 0, Cost 9, Personal Honor 2, Crab Clan /
> Samurai / Commander / Hero / Jade / Unique. Reaction: Twice per turn, after an action targets your Follower at Mochitoko's location:
> Negate its bowing and destruction from that action's effects.

All five scans were read by eye and their WebP working copies sha256-checked against the committed receipts; the published text is the
catalogue text and matches the scans exactly. Each card is published whole. Yurishi's `Clan` is `Spider`: the catalogue lists his clans as
`Ninja, Spider` and packaging takes the first, but Ninja is only his printed keyword (the correction wave 18 made for Takasho); his
"After Yurishi enters play: Lose 3 Honor" carries no Reaction label, so it is the mandatory `EntryHonorLoss` 3, applied automatically like
every other Honor-loss entry and never offered as a choice. Art: `src/L5R.Client/Content/Published/oracle-l5r-<id>.jpg`.

## What was built (general pieces, not per-card hacks)

`TargetedActionReactions.cs` -- the **targeted-by-action** Reaction family (trigger `targeted-by-action`), beside the existing
`before-action-resolves` family (Rokai, Tamoko, Champion of Thunder, Brothers of Goemon, Kakihara). It is offered in **both** windows an
action can be held in: the `beforeActionReaction` window (the whole announced command is deferred, so none of its costs or effects have
happened) and the `attackReaction` window (an Attack's costs are paid and its target fixed; only its resolution is deferred). An Attack
is offered through the attack window only, every other action through the before-action window only, never both. One vocabulary:

| Piece | What it is |
|---|---|
| `ReactionSubject` | The held action, whichever window holds it: its command, and the card it targets right now. `PendingAttack.Command` (new) keeps the declaring command so an Attack can be re-validated; for Bayushi Kahoku - exp, whose TargetId is a separate -4F target, the Attack's own target rides in `CardIds`. |
| `ActionAcceptsTarget(subject, card)` | "Would the action be accepted against THIS card?" -- the action's own target validation run again (`ValidateAbility` for an ordinary action, `RequireLegalAttackTarget` for an Attack, whose costs are already paid and whose source may already be spent). The one definition of a legal target that redirection and re-checking share. |
| `RedirectAction(subject, card, reactor)` | Make the held action target another card, if `ActionAcceptsTarget`. An Attack's strength follows the new target. |
| `RecheckTarget(subject, card)` | After a reaction changed the game, is the card still a legal target? If not, negate the action's effects on it (`NegateAllEffectsOn`). |
| `NegateEffects(card, kinds...)` / `NegateAllEffectsOn(card)` | Negate any set of effect kinds an action would apply to a card -- `bow`, `destroy`, `move`, `straighten`, or `all`, the blanket negation. **This is the reusable "negate the action's effects" (Yasuki Jinn-Kuen; Yoritomo Sakuma's Political Reaction needs the same): `MatchEngine.NegateAllEffectsOn` in `src/L5R.Core/TargetedActionReactions.cs`.** |
| `YojimboRedirectApplies` | The Yojimbo redirect (Mendon: "a common ability of cards with the Yojimbo keyword"): another player's action targets one of your [keyword] Personalities at this Personality's location, and is legal against him. Ezuno restricts it to Shugenja (`"Shugenja"`); a plain Yojimbo, Spirit of the Yojimbo's granted ability, is the same call with no keyword. |
| `AbilityDefinition.UsesPerTurn` (`AbilityLimits.cs`) | The printed per-turn limit: 1 (default), N, or `AbilityUseLimit.Unlimited`. |

Also: `AbilityCatalog` gains `holding-or-stronghold` and `redirect-target-own-card` target categories; `DestroyAttachment`, `Dishonor` and
`CreateToken` honour the negation signal (they did not), so the blanket negation reaches everything the engine applies to a card;
`ResolveAttack` clears the negations a window set for that Attack.

## Mendon's rulings, as implemented

**Ezuno / Blade.** "The reaction is an in-play trigger that monitors for actions targeting a Shugenja at Ezuno's location (home or same
battlefield). The primitive allows a player to choose to change the target of the action to Ezuno if Ezuno satisfies the criteria of the
targeting action... then Ezuno may not become the target of the action and his reaction trigger should not fire." "Blade of Champions,
like Ezuno, allows the player controlling it to select a new target for a card as long as it is a 'legal target', meaning that the new
target satisfies the targeting criteria of the action being redirected." Implemented exactly: legality is `ActionAcceptsTarget`, and the
reaction is **only offered** when a legal redirect exists (Ezuno himself must be legal; Blade needs at least one legal candidate), so no
window opens for a redirect that could not happen. Tested with Kakita Kazan's "target an enemy dishonorable Personality": an honorable
Ezuno is not offered, a dishonorable one is.

**Yurishi.** "Once Yurishi is moved, re-evaluate the effect of the action targeting him. Battle actions targeting Yurishi that require him
to be 'Opposing' or 'at this battlefield' will be negated by this move." Implemented: the reaction moves him home (a movement negated in
the window keeps him where he is, so nothing is then re-negated), then `ActionAcceptsTarget` asks the ORIGINAL action's own validation
again; if it no longer holds, `NegateAllEffectsOn(Yurishi)`. Every Battle action needs its target at the battlefield, so the move
negates it; The Earth Flows ("If you are the Defender, target a Personality: Move him to the current battlefield") does not, and
Yurishi's move is then answered by the action putting him back (tested).

**Jinn-Kuen.** "May negate any action targeting a Merchant controlled by the same player that controls Jinn-Kuen... until Jinn-Kuen
exercises the reaction or does not have the ability to pay the two Gold (including abilities and cards that would reduce the cost of this
action to 0)." Implemented: the reaction is priced by the Gold quote (`GoldCostRule.Fixed(2)`, `AbilityGoldCost`, the same quote that
validates, lists and charges every costed action), and it is offered -- and a window opened -- only when the controller can pay the
QUOTED cost, **a cost of 0 included** (`CanPayReaction`; tested with a training fixture priced at 0 and a controller with every Gold
source bowed). Any cost reducer added to the quote is therefore honoured here with no change. Negation is the blanket `all`, not the
curated destruction-or-movement negation of Rokai and Tamoko.

**Mochitoko.** "It requires a counter... 0, 1 and it can be reused. 2 and it cannot be reused for the remainder of the turn. Some actions
include 'any number of times per turn' in which case the reuse flag can be permanently set to 0. Some effects may apply bowing, destruction,
or both (if he bowed, destroy him)... This includes melee and ranged attacks." Implemented: (a) `UsesPerTurn` -- a per-turn use count with
a configurable limit; (b) `NegateEffects(card, "bow", "destroy")` -- several kinds negated at once, each consumed by the first effect of
its kind that runs, so an action that bows and then destroys ("if he bowed, destroy him": Tokoru's Bear) finds each part negated; (c) the
reaction is offered for an Attack (which always destroys what it hits) and for any action the curated classification says could bow or
destroy the Follower (`BowsTarget`, `DestroysTarget`, and the new `DestroysAttachmentTarget` for actions that destroy an attachment); a
plain Force penalty never offers it (Brothers of Goemon's precedent).

## Decisions and the alternative reading

| # | Decision | Alternative not taken |
|---|---|---|
| 1 | **Costs already paid.** A non-Attack action is deferred whole, so none of its costs is paid when a reaction redirects it; they are paid once, when it resolves, for the card it then targets. The payment the acting player COMMITTED still has to fit, and `ActionAcceptsTarget` checks it: a redirect to a card whose price the committed payment does not cover (or that would make a paid action free) is not legal. (Tested with a target-priced action: a dearer original may be redirected to a cheaper Ezuno; a free one may not be redirected to a costlier Ezuno.) An **Attack's** costs are paid at declaration and stay paid -- nothing is refunded or re-priced. | Re-price at the new target and let the acting player choose new sources: a new decision with no UI and no printed support. |
| 2 | **Reactions key on the NEW target** (Mendon asked whether Kakihara-style reactions fire on the original or the new target). The window re-evaluates every reaction against the held action each time a player has priority, so after a redirect the reactions that apply are those of the new target's controller and unit (a Champion of Thunder in Ezuno's unit becomes available; one in the Shugenja's unit stops). What was already played against the old target -- a bonus, a negation -- stays with the old card and does not shield the new one. | Reactions stay with the original targeting. Rejected: "after an action targets X" is true of the new target from the redirect on, and a redirect that could not be answered would let it dodge every protection. |
| 3 | **Announce-time traits do not re-fire on the new target.** Daigotsu Bofana's mandatory trigger (issue #333) resolves when an action is announced, before any window, so an action redirected to Bofana does not make its player discard, and one redirected away from him already did. | Fire it again for the new target -- needs a second discard flow inside an open window (the window has no stack). |
| 4 | **A Reaction cannot be redirected.** "An action" excludes a Reaction (as for Kakihara): a Reaction played inside an open window that names a card would open a nested window over the one it is played in. Also unchanged: an action taken as an **Additional Action** (non-Attack) and any action inside the Engage window are not held in a reaction window at all, so these reactions cannot answer them; an Additional-Action Attack is, because it reaches the attack window like any other. | Give the windows a stack (out of scope). |
| 5 | **Ezuno is for another player's action, at his location, on his controller's own Shugenja, never himself.** "Location" is the engine's own (`SameLocation`): a player's Home is that player's, a battlefield is shared by both. | -- |
| 6 | **Blade of Champions answers ANY Battle action** (its text says "a Battle action", not "another player's"), against any card in its unit -- the wielder, one of his attachments (Follower, Item, Spell), or the Blade itself -- and the new target is one of its controller's other in-play cards for which the action is legal. The Blade is bowed as the cost and legality is re-checked after it. | Only another player's action (adds a restriction the text does not print). |
| 7 | **The Blade is not a candidate for its own redirect.** It is bowed as the cost, so what is legal against a bowed Blade cannot be judged before paying. | Allow it, judged after the cost: the offer list would then contain options the cost itself can make illegal. |
| 8 | **Yurishi needs to be at a battlefield to react.** Moving home from Home is nothing, and a Battle action reaches a target at Home only through a few exempt categories. Only another player's Battle action (Timing Battle, or Battle/Open used in a battle). | Offer it at Home: an empty reaction. |
| 9 | **Jinn-Kuen and Mochitoko answer any action** (their printed text says "an action" / "an action", not "another player's action"), including their controller's own. Mendon's paraphrase of Jinn-Kuen says another player; see Open questions. | Another player's only. |
| 10 | **"Your Merchant / your Follower".** Any card its controller controls with the Merchant keyword (Jinn-Kuen himself qualifies); a Follower the controller controls whose host stands where Mochitoko does. The Merchant keyword is read live (printed or granted). | -- |
| 11 | **Blanket negation is per CARD**, as Mendon states it ("the action on that target"): every effect the action applies to the targeted card -- bow, straighten, move, destruction, Force/Chi/Personal Honor changes, dishonor, tokens placed on it. It does not reach effects on the card's controller (Honor gained or lost), on other cards the action also affects, or the acting player's own costs (a cost is not an effect). | The whole action's effects: impossible without knowing, per ability, which parts are costs. |
| 12 | **Use limit.** `AbilityUses[key]` still holds the turn of the latest use for every ability; an ability limited above once adds `count:<key>` = turn x 1024 + uses that turn. Spent = the count reaches the limit. The ten `id`-list exemptions in `ValidateAbility` (Tactical Advantage, Equip (Battle), Ashigaru Elite and its training twin, Elite Sentry, Kharmic, Soshi Neiru's equip, Naval Invasion, Ring of Air/Water) are now `UsesPerTurn: Unlimited` on their own definitions; a test pins that every other ability is 1 and Mochitoko's is 2. | -- |

## Open questions for Mendon

1. **Jinn-Kuen and Mochitoko: any action, or another player's action?** Printed text: "After an action targets your Merchant" / "your Follower". Your Jinn-Kuen note says "every time another player targets a Merchant". Implemented as printed (any action, own included); if the own-action case should not offer it, it is one condition in `TargetedActionReactionApplies` (`command.PlayerId != card.OwnerId`). The own-action reading only ever opens a pass-only window for the acting player.
2. **Blanket negation and Honor.** "Negate the action's effects" negates what the action does to the targeted card. An Honor loss the action inflicts on the target's controller (Bayushi Kazan's "his controller loses 2 Honor", Training honor strike) still happens. Should a negation, and Yurishi's "no longer a legal target", also stop those (an action that has no legal target has no effects at all)? It needs each Honor-changing effect to know which negation it belongs to.
3. **Blade of Champions: itself as the new target** (decision 7), and any Battle action vs only another player's (decision 6).
4. **Announce-time traits on a redirected action** (decision 3).
5. **Carpenter Castle (existing behaviour, surfaced by this work):** "Ranged 5 Attack. If this targeted a card, gain 2 Honor." The engine gains the 2 Honor only when the Attack HITS. Redirected or dodged Attacks (Ezuno, Yurishi) make the difference common; the printed text reads "if this targeted a card". Not changed here.

## Known limits

- The in-play family only: a reaction played from a hand or discard pile ("Reaction: After another player's action targets your Personality: ..." on a Strategy -- Caught in the Act, Know No Fear, Near Miss, Well Protected, Ancestral Protection, Undetectable Enemy, Snake Tattoo) is not offered by these windows yet; the candidate scan (`TargetedActionReactionCandidates`) reads in-play cards, exactly as `AnyOutOfPlayReactionAvailable` is a separate scan for `before-attack-resolves`. Extending it is a candidate scan over the hand, no new rule.
- A negation already played in a window stays live for the rest of that window (`NegatedEffects` is consumed by the first matching effect, "all" not at all), so a second reaction that puts an effect on the same card in the same window is negated too. Kakihara's bonus avoids it by adding its modifier directly (issue #318); the same care applies to any future reaction that puts an effect on a card a negation names.
- Effects the blanket negation does not reach: Honor changes, granted keywords and Protection, moved tokens, and discards (question 2).
- The classification lists (`BowsTarget`, `DestroysTarget`, `DestroysAttachmentTarget`) are curated, as Rokai's and Brothers of Goemon's are; a new action that bows or destroys a Follower is added to them, and until it is Mochitoko is simply not offered for it.

## UI

No new UI is needed, so no issue for Alfredo. Every decision these cards need already has a prompt: the window is the existing
before-action / attack reaction window, each reaction is a `useAbility` option with the engine's own label and Gold quote (Jinn-Kuen's
payment panel is the ordinary one), Blade of Champions' choice of new target is one option per legal candidate labelled with the card's name
-- the shape Champion of Thunder's "choose a unit member" already ships -- and passing is the existing pass option. The engine puts nothing
new into any player's view: the redirect and the negation are in the public event log (`actionRedirected`, `effectsNegated`), and no card
of a hand is ever named (tested with `HiddenIdsAbsent`).

## Tests

`tests/L5R.Core.Tests/TargetedActionReactionTests.cs` (21): published shapes; the use-limit parity pin; Ezuno on a non-Attack action (window
opens for his controller, nothing has happened yet, the acting player answers, the action resolves on Ezuno), on an Attack, and every
"not offered" rule (own action, himself, non-Shugenja, other location, bowed, spent, not in play, illegal for Ezuno); costs; the chain
(Champion of Thunder before/after a redirect, a negation that stays with the old card); Blade with every candidate, illegal choices
rejected, its applicability rules and an Attack whose strength follows the new target; Yurishi negating, still-legal, against an Attack and
his entry Honor loss; Jinn-Kuen paying through the quote, unpayable, free, blanket over eight kinds of effect and the Open half; Mochitoko's
twice-per-turn count and reset, bow-only, destroy-only, both, an Attack, and every "not offered" rule; and every enabled offer validated on a
fresh engine. Mutation checks (each mutation applied, the suite run, a named test failed, source restored) are listed in the PR.

## Unblocked by the same work

See the PR description for the list of unwired catalogue cards these primitives cover, with ids and names.
