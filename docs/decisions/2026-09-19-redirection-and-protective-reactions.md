# Redirection and protective reactions: Mirumoto Ezuno, Blade of Champions, Goju Yurishi, Yasuki Jinn-Kuen exp2, Hida Mochitoko exp

Status: implemented; six rulings by Mendon applied and recorded below ("Mendon's six rulings"). The rulings quoted from Mendon are his,
given with the task and after review on 2026-09-19; every other reading is a decision of this change, marked with its alternative, and the one
question still with him is listed under "Open questions". Lane: rules (mdornbrook; cross-lane edits under his standing authority). Issue: #335.
Waves: `personality-wave-30` (Ezuno, Yurishi, Jinn-Kuen, Mochitoko) and `item-wave-4` (Blade of Champions). Published count 417 -> 422 (on
origin/main 05cc2493; recounted from the wave ids).

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

## Mendon's six rulings (confirmed by Mendon, applied on this branch)

1. **Jinn-Kuen may cancel actions of his own controller** (as built): "After an action targets your Merchant" means any action, the controller's own
   included. (Was open question 1; Mochitoko is the same: any initiator.)
2. **Mochitoko negates bowing and destruction regardless of who initiated the action.** If the bow or destroy he blocks is a **cost** of the action
   (Hiruma Tensin's "bow your performing Follower" is the wired example), the **whole action is negated and pays nothing**. Implemented behind one
   predicate, `CostPaymentBlockedByNegation(command)` (below).
3. **An Honor-loss cost is paid when an action is negated, unless a negation blocks a cost (then everything is ignored); an Honor loss that is an
   EFFECT is negated with the effects.** (Was open question 2.) The effect half is built here: an "all" negation of a card also stops the Honor its
   controller would lose to ANOTHER player's running action (`HonorLossNegatedWithEffects`, checked in `GainHonor`); the acting player's own Honor cost is
   a cost, so it is never stopped by it. The cost half (a negated action still pays its costs) is agent F's atomic cost payment, #351.
4. **Blade of Champions may redirect to itself if that is legal.** (Was open question 3 and decision 7.) Legality is judged with the Blade bowed, since
   bowing it is the cost of the reaction (`RedirectTargetAccepted`).
5. **A trait that fires when an action targets a card (Daigotsu Bofana's) fires once per action per Bofana, across redirects.** (Was open question 4 and
   decision 3.) An action redirected away from a Bofana that already fired does not fire again; one redirected ONTO a Bofana that was not yet judged is
   judged then (below).
6. **Ranged and Melee Attacks need no target and may target a province** (where the ability says so). Every card-target check treats "no target" and
   "province" as **no card targeted**: no reaction that keys on a card being targeted is offered, and no window opens for the Attack.

### How each is built

- **Whole-action negation (agent F, `ActionNegation.cs`, #338) and per-card negation (this change, `NegateEffects`) coexist.** They answer different
  printed texts. *Whole action* -- "negate the action's (remaining) effects" (Yoritomo Sakuma, Yasuki Jinn-Kuen): `NegateHeldAction` -> F's
  `NegateDeferredAction` for a deferred action (sets `DeferredActionNegated`; `ResolveNegatedAction` then takes the action without its effects), and
  `PendingAttack.Negated` for an Attack in the attack window (`ResolveAttack` then hits nothing). *Per card* -- "negate its effects on him" / "negate its
  bowing and destruction" (Yurishi's re-check, Mochitoko): a `NegatedEffects` entry `(kind, card)` consulted by each effect that applies to that card, the
  action itself still resolving on everything else. Jinn-Kuen was built per card first and is **now whole-action** (ruling 1's companion: "Negate the
  action's effects"), so he needs nothing but F's primitive. `ResolveDeferredAction` clears the per-card list at the same place F's flag is consumed, so
  neither signal survives its action. Protection (#212) stops both.
- **The cost-block predicate (`CostPaymentBlockedByNegation`, `TargetedActionReactions.cs`).** The ONE place the rule "is this action's cost payment
  blocked by a negation?" is asked: true when the card the action would bow or destroy as its cost (its chosen cost option's subject, `CostSubjectOf`) has
  had that bowing or destruction negated. Mochitoko's window is opened before the cost is paid -- for an ordinary action the whole command is held, for
  an Attack-pipeline action such as Tensin's the Attack is held cost-only with no card target until the cost is settled -- and `ExecuteAbilityEffects`
  asks the predicate first; when it is true `AbortActionForBlockedCost` spends the use, pays nothing and applies nothing. F's follow-up (#351) makes
  cost payment atomic and names the same extension point (`CostPaymentBlockedByNegation`): it calls this predicate before paying anything and, when it is
  false, pays every cost even for a negated action. Nothing in Mochitoko's code needs to change when it lands; only the caller moves.
- **Bofana once per action** (`OpenForcedDiscardForTargetedTrait`, `MatchState.AnnounceTraitEvaluated`): the Bofanas an action has been judged for are
  recorded (reset when the next action is announced). A redirected action is judged for a Bofana not yet on the list when its reaction window CLOSES and
  before it resolves (resume `"action"` for a deferred action, `"attack"` for an Attack -- `ForcedDiscardResume`), so the window itself never holds a second
  decision; a Bofana already judged is skipped whatever the number of redirects.
- **Attacks without a card target** (`ResolveOrDeferAttack`, `ReactionSubject`, `AttackTargetsProvince`, `ActionOption.NoTarget` / `Provinces`): an Attack
  with `TargetId` null resolves at once with no window (a province Attack passes the province to the ability's own effect, `ResolveProvinceAttack`);
  `SubjectForAttack` yields no subject when no card is targeted, so every card-target reaction is off. An Attack ability whose cost is a Follower
  (Tensin) is held with the cost only. Legality: no target or a card (unchanged rules), or `command.Province` only when the ability allows it and
  only the current battlefield's. The legal-action list offers "no target" on every Attack (`NoTarget`) and the province where allowed (`Provinces`).
  No printed card in the reference catalogue (`content/oracle/catalogue-index.json`, current Oracle text) has an Attack that targets a province. Searched
  (case-insensitive): `(Ranged|Melee)[^.]{0,80}provinc` (three hits, none a province target: Inciting Mistrust reveals a card in the province, Kaiu Onizuka's
  Attack takes its strength FROM the province's strength, Yoritomo Tansen's is an either/or with a separate "-3 strength" effect on the province),
  `provinc[^.]{0,80}(Ranged|Melee)` and `Attack[^.]{0,60}target[^.]{0,30}provinc` (no hits), and `target ... province` (eleven hits, all non-Attack actions:
  Merchant Outpost, Ready the Defenses, Shosuro Hawado exp2, Shrine to Nimuro, Sudden Blockade, Two Fronts, Yoritomo Mikaru...). Shrine to Nimuro is a Limited
  action, not an Attack. A training fixture (`training-province-attack`) covers the primitive until a card needs it. **UI gap: #355** (the picker cannot yet
  choose "no target" or a province).
- **Sakuma compatibility** (`sakuma-political-negate`, the `before-action-resolves` family, #338). Both families read the same held command
  (`PendingNegatableAbility`), so after a redirect Sakuma's applicability is judged on the NEW target (a Political action redirected onto Ezuno, one of his
  controller's Personalities, still lets him react; a redirect off a Personality of his controller's takes the reaction away). His "the player may pay 2
  Gold" decision suspends and restores the window (`EitherOr.cs`) without touching the held command, so a redirect before or after it is unaffected.
  Once the action has been negated as a whole (Sakuma's or Jinn-Kuen's), the targeted-by-action family offers nothing further (`HeldActionNegated`): a
  redirect or a second negation of an action that will not resolve would be an empty reaction. Tested with Yogo Adi's Political action (Ezuno redirect
  and Sakuma's pay/negate in both orders).

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
| `NegateEffects(card, kinds...)` / `NegateAllEffectsOn(card)` | Negate any set of effect kinds an action would apply to a card -- `bow`, `destroy`, `move`, `straighten`, or `all`, the blanket negation. This is the per-CARD negation (Yurishi, Mochitoko); the whole-action one (Jinn-Kuen, Sakuma) is `NegateHeldAction` over F's `ActionNegation.cs` (see "How each is built"). |
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
| 3 | **Announce-time traits fire once per action per Bofana (ruling 5, confirmed by Mendon).** Daigotsu Bofana's mandatory trigger (issue #333) is judged when an action is announced; an action redirected onto a Bofana not yet judged is judged when the window closes, before it resolves; one redirected away already was. My first reading (never fire on the new target) is superseded. | -- |
| 4 | **A Reaction cannot be redirected.** "An action" excludes a Reaction (as for Kakihara): a Reaction played inside an open window that names a card would open a nested window over the one it is played in. Also unchanged: an action taken as an **Additional Action** (non-Attack) and any action inside the Engage window are not held in a reaction window at all, so these reactions cannot answer them; an Additional-Action Attack is, because it reaches the attack window like any other. | Give the windows a stack (out of scope). |
| 5 | **Ezuno is for another player's action, at his location, on his controller's own Shugenja, never himself.** "Location" is the engine's own (`SameLocation`): a player's Home is that player's, a battlefield is shared by both. | -- |
| 6 | **Blade of Champions answers ANY Battle action** (its text says "a Battle action", not "another player's"), against any card in its unit -- the wielder, one of his attachments (Follower, Item, Spell), or the Blade itself -- and the new target is one of its controller's other in-play cards for which the action is legal. The Blade is bowed as the cost and legality is re-checked after it. | Only another player's action (adds a restriction the text does not print). |
| 7 | **The Blade IS a candidate for its own redirect if legal (ruling 4, confirmed by Mendon).** It is bowed as the cost, so legality is judged with it bowed (`RedirectTargetAccepted`), and only a target that stays legal is offered. My first reading (never a candidate) is superseded. | -- |
| 8 | **Yurishi needs to be at a battlefield to react.** Moving home from Home is nothing, and a Battle action reaches a target at Home only through a few exempt categories. Only another player's Battle action (Timing Battle, or Battle/Open used in a battle). | Offer it at Home: an empty reaction. |
| 9 | **Jinn-Kuen and Mochitoko answer any action**, whoever initiated it (their printed text says "an action"), including their controller's own -- confirmed by Mendon (rulings 1 and 2). | Another player's only. |
| 10 | **"Your Merchant / your Follower".** Any card its controller controls with the Merchant keyword (Jinn-Kuen himself qualifies); a Follower the controller controls whose host stands where Mochitoko does. The Merchant keyword is read live (printed or granted). | -- |
| 11 | **Two negations, by printed text.** "Negate the action's effects" (Jinn-Kuen, Sakuma) is the whole action (F's `ActionNegation.cs`; my first per-card reading of Jinn-Kuen is superseded). "Negate its effects on him" (Yurishi) is per CARD: every effect the action applies to that card -- bow, straighten, move, destruction, Force/Chi/Personal Honor changes, dishonor, tokens placed on it -- plus, by ruling 3, the Honor its controller would lose to the action as an EFFECT. It does not reach other cards the action also affects, or the acting player's own costs (a cost is not an effect). | -- |
| 12 | **Use limit.** `AbilityUses[key]` still holds the turn of the latest use for every ability; an ability limited above once adds `count:<key>` = turn x 1024 + uses that turn. Spent = the count reaches the limit. The ten `id`-list exemptions in `ValidateAbility` (Tactical Advantage, Equip (Battle), Ashigaru Elite and its training twin, Elite Sentry, Kharmic, Soshi Neiru's equip, Naval Invasion, Ring of Air/Water) are now `UsesPerTurn: Unlimited` on their own definitions; a test pins that every other ability is 1 and Mochitoko's is 2. | -- |

## Open questions for Mendon

Questions 1-4 of the first version of this note are closed by Mendon's six rulings above (1: rulings 1 and 2; 2: ruling 3; 3: ruling 4, and the Blade answers any
Battle action as printed -- his rulings did not change that; 4: ruling 5). One remains:

5. **Carpenter Castle (existing behaviour, surfaced by this work):** "Ranged 5 Attack. If this targeted a card, gain 2 Honor." The engine gains the 2 Honor only when the Attack HITS. Redirected or dodged Attacks (Ezuno, Yurishi) make the difference common, and ruling 6 makes it commoner still (an Attack with no target targets no card: no Honor at all, which the printed text supports). The printed text reads "if this targeted a card". Not changed here; still with Mendon.

## Known limits

- The in-play family only: a reaction played from a hand or discard pile ("Reaction: After another player's action targets your Personality: ..." on a Strategy -- Caught in the Act, Know No Fear, Near Miss, Well Protected, Ancestral Protection, Undetectable Enemy, Snake Tattoo) is not offered by these windows yet; the candidate scan (`TargetedActionReactionCandidates`) reads in-play cards, exactly as `AnyOutOfPlayReactionAvailable` is a separate scan for `before-attack-resolves`. Extending it is a candidate scan over the hand, no new rule.
- A negation already played in a window stays live for the rest of that window (`NegatedEffects` is consumed by the first matching effect, "all" not at all), so a second reaction that puts an effect on the same card in the same window is negated too. Kakihara's bonus avoids it by adding its modifier directly (issue #318); the same care applies to any future reaction that puts an effect on a card a negation names.
- Effects the per-card negation does not reach: granted keywords and Protection, moved tokens, and discards. (An Honor loss that is an effect IS stopped, ruling 3.)
- The classification lists (`BowsTarget`, `DestroysTarget`, `DestroysAttachmentTarget`) are curated, as Rokai's and Brothers of Goemon's are; a new action that bows or destroys a Follower is added to them, and until it is Mochitoko is simply not offered for it.

## UI

The redirect and negation windows need no new UI: the window is the existing before-action / attack reaction window, each reaction is a `useAbility`
option with the engine's own label and Gold quote (Jinn-Kuen's payment panel is the ordinary one), Blade of Champions' choice of new target is one option
per legal candidate labelled with the card's name -- the shape Champion of Thunder's "choose a unit member" already ships -- and passing is the existing
pass option. The engine puts nothing new into any player's view: the redirect and the negation are in the public event log (`actionRedirected`,
`effectsNegated`, `actionNegated`), and no card of a hand is ever named (tested with `HiddenIdsAbsent`).

**One gap, ruling 6: the Attack target picker cannot express "no target" or "a province"** (`RenderTargeted` in `Main.Actions.cs` lists only `TargetIds` and
`Choose` defaults to the first). The engine says what is legal (`ActionOption.NoTarget`, `ActionOption.Provinces`); the client work is issue **#355** for Alfredo.

## Tests

`tests/L5R.Core.Tests/TargetedActionReactionTests.cs` (21): published shapes; the use-limit parity pin; Ezuno on a non-Attack action (window
opens for his controller, nothing has happened yet, the acting player answers, the action resolves on Ezuno), on an Attack, and every
"not offered" rule (own action, himself, non-Shugenja, other location, bowed, spent, not in play, illegal for Ezuno); costs; the chain
(Champion of Thunder before/after a redirect, a negation that stays with the old card); Blade with every candidate, illegal choices
rejected, its applicability rules and an Attack whose strength follows the new target; Yurishi negating, still-legal, against an Attack and
his entry Honor loss; Jinn-Kuen paying through the quote, unpayable, free, blanket over eight kinds of effect and the Open half; Mochitoko's
twice-per-turn count and reset, bow-only, destroy-only, both, an Attack, and every "not offered" rule; and every enabled offer validated on a
fresh engine. Mutation checks (each mutation applied, the suite run, a named test failed, source restored) are listed in the PR.

## Unblocked by the same work (unwired cards the primitives now cover)

Found by scanning the catalogue text for redirect and "after an action targets" shapes; none is wired here, and each still needs its own
`TargetedActionReactionApplies` case, ability entry and effect case (and a fresh reading of its scan).

- **Redirect** (`RedirectAction` / `ActionAcceptsTarget` / `YojimboRedirectApplies`): Bayushi Wakui (10310, Magistrates at his battlefield), Shiba Sansesuke (6803,
  any action, bow him), Riku (10323, a named Personality), Hida Kisada (Emperor) (3127, chooses another Personality at his location, Blade's shape),
  Chugo Seido (1373, a Holding, destroy it), Ryoshun's Guidance (6425) and Benten's Blessing (959) (Celestials, discard), Shifting Terrain (10168, a
  Terrain), Shosuro Aroru - exp2 (7089, plus a move afterwards), The Castle of Order (10185, once per battle). Standing or granted redirects need a
  hook that rewrites every later action (Ruthless Onslaught 10164, Yoritomo Naoto 10300) or a granted ability (Spirit of the Yojimbo 7430, Ceaseless Vigil 10139).
- **Move, re-check, negate** (Yurishi's shape): Snake Tattoo (7310, a Strategy, "move him to an adjacent battlefield").
- **Blanket or several-kind negation** (`NegateEffects`): O-Yoroi Troops (10107, exactly Champion of Thunder's ability), Ancient Armor of the Qamar - exp (9970),
  Vigilant Riders (9158), Isawa Norimichi (3881), Isawa Sakonoko (3891), Stone of Remembrance (7501), Kakita Kae (4179, bowing, destruction and movement),
  Know No Fear (4481, bowing and movement), Caught in the Act (1238), Near Miss (5523), Well Protected (9288), Social Grace (9928), Undetectable Enemy (8961),
  Ancestral Protection (370), Hundred-Fold Cut (3513), Fudo - exp (2721, "twice per turn" as well), Yasuki Ikke (9809, negate or make the player lose Honor: a choice).
- **Per-turn counter** (`UsesPerTurn`): Fudo - exp (2721: twice per turn) and Matsu Rika (9827: three times per turn; her other gap is the Bushido Virtue classification).

Those played from a hand (every Strategy above) also need the trigger offered from a hand: `TargetedActionReactionCandidates` reads in-play cards only. Cards whose
trigger is a Political, Limited or Iaijutsu action need that classification first (Kamalakar 4253, Taishuu 7733, Moto Shigeru 5370, Astonishing Accusation 637,
Oriole Imperial Vanguard 10337, To First Blood 8500).
