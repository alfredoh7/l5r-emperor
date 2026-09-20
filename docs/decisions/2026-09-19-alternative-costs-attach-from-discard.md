# Alternative costs, attach from a discard pile, the conditional effect choice, the Follower performer, and five cards

**Status:** implemented in the PR for issue #337 (rules lane; cross-lane authority of the rules lead); **Mendon's rulings on the open questions are
recorded in section H (issue #352).** The readings below are
mine, taken from Mendon's rulings where he gave one; every one that is a judgement call carries its alternative, and the
genuinely open ones are collected under **Open questions for Mendon**. **Issues:** #337 (engine), #339 (the one combined UI
issue for Alfredo).

Cards (all scans read by eye, working copies sha256-checked against their receipts): Kitsu Fukui `oracle-l5r-4375`, Moto Jun-Ni
`oracle-l5r-5337`, Mirumoto Yumaru `oracle-l5r-5181`, Hiruma Tensin `oracle-l5r-3332` (`personality-wave-31`, all Second City) and
Fading Shadows `oracle-l5r-2437` (`follower-wave-17`, Second City). Foothills Keep `oracle-l5r-2626` was looked at and left (see
F).

## A. Alternative cost (`AlternativeCosts.cs`)

**Mendon:** "Create a primitive allowing a player to choose cost X or cost Y to satisfy the costs of an action. In this case, the
player may bow an unbowed Personality with the Ancestor keyword or destroy a straightened or bowed Personality with the Ancestor
keyword to perform the subsequent action of targeting and bowing a card and then gaining one Honor."

An ability's cost has two independent halves: **which card pays** and **how it pays**. Until now the second half was implicit
(every cost-paying performer was bowed, inside the ability's own execution case), so a card that lets the player pay two ways had
nowhere to say so. `AbilityDefinition.CostOptions` is that place: a list of `CostOption(Id, Label, Verb, Subject)`.

- **Verbs:** `bow` (the subject must be in play and unbowed; it is bowed) and `destroy` (the subject must be in play and be a
  Personality or an attachment; it goes through the ordinary destruction path with the acting player as the destroyer).
- **Subjects:** by default the chosen performer (or the source when the ability names no Performer), or per option the source
  ("Bow this card") or the target ("bow or destroy his target Armor").
- **Each option is independently payable** (a bowed Ancestor cannot pay by bowing but can be destroyed; an unbowed one may pay
  either way), and **the action is usable when at least one is**.
- **The choice is part of the command** (`GameCommand.CostOption`): QueryActions offers one complete action per (performer, way to
  pay), each with its own label suffix, and an option that cannot be paid is listed disabled with its reason. A command with
  two or more options must name one; one with a single option needs none (so every command that predates the primitive still
  works). It is validated with everything else and paid where the Gold cost is paid (`PayCostOption`, beside `PayForAbility`).
- **The four bow-or-bow choices that predate it are expressed on it**: Xijkt, Tamori Shosei, Sutigu and The Earth's Hunger each
  carry one `bow` option, and their execution cases no longer bow the performer themselves. All their existing tests pass
  unchanged. One behaviour is tightened by that: The Earth's Hunger used to accept a **bowed** wielder as the performer (the
  "bow" was then a no-op); a bow cost needs an unbowed card, so it is now refused (the Spell may still pay when he is Earth).
  That is a rules correction I judged in scope; it is one line to revert (`CostVerb.Bow`'s check) if Mendon disagrees.

### Kitsu Fukui readings

1. **The performer is the Ancestor**, not Fukui (the printed "your performing Ancestor Personality"). Fukui is the source and
   must be at the battlefield like any Personality's Battle action; the Ancestor is chosen from your own Ancestor Personalities at
   the battlefield, bowed or not (the destroy option does not need it unbowed). *Alternative:* the Ancestor need not be at the
   battlefield (it is at Home when Kitsu Akai creates it). I read "performing" as requiring presence like every other Battle-action
   performer; the general rule is the engine's, not a Fukui special case.
2. **"Gain 1 Honor" is independent of the performer (RULED by Mendon, issue #349, superseding my first reading).** Mendon: "the
   sentences of an action are independent when they are complete sentences. ... The same is true for the bow or destroy an Ancestor
   Personality: [Fukui] has an Honor gain that is independent of whether or not an Ancestor Personality performed any action." So
   "Bow it." and "Gain 1 Honor." are separate sentences and the acting player simply gains 1 Honor: regardless of the Ancestor's
   honorable or dishonorable status (no rehonor conversion), regardless of the Ancestor being destroyed as the cost, and regardless of
   whether the target's bow succeeds or is negated. *My first reading* (the gain is the performer's action, so a dishonorable Ancestor
   rehonors instead) used `GainHonorFromPerformer`; that call was the only thing tying the gain to the performer, and it is now a plain
   `GainHonor` for the acting player. The general "a dishonorable performer's Honor gain becomes a rehonor" rule is untouched for cards
   whose performer does gain the Honor (Tamori Shosei is tested as the control). The one other Honor change that can accompany the gain
   is the ordinary Dishonorable-death penalty of a dishonorable Ancestor destroyed as the cost, which is a separate rule.
3. **A destruction paid as a cost goes through the ordinary destruction path**, so Expendable, the Dishonorable-death penalty, Lady
   Matsu's Rage and Fields of Mercy's negation all apply as they do to any destruction by the
   acting player's action (the same choice Ring of Fire's "destroy your performing Personality" already made). **RULED (issue #352): such a
   destruction can be negated, and then the whole action is negated** -- see H2.
4. **The target is any enemy card at the battlefield** (`enemy-card`: a Personality, an attached Follower, an attachment), and
   Stalwart negates only the bowing: the cost is still paid and the Honor still gained.

## B. Attach from a discard pile (`AttachAction.cs`)

**Mendon:** "[Foothills Keep's ability] should be generalized sufficiently to allow conditional attachment of attachments from the
discard pile for personalities. In general, the player will be prompted to pay costs, pay costs with a discount, or pay no costs.
If prompted to pay any costs, calculate the necessary cost and use the gold payment flow to allow the player to pay for the
attachment before retrieving it from the discard pile and attaching it to the personality."

`AbilityDefinition.Attach` is an `AttachSpec`: which card (`Card`: the chosen **target** in a discard pile, or the **source** itself
in hand), whose pile (`Pile`: your own or any player's Fate discard pile), who receives it (`Wielder`: the source, or the chosen
performer), and what it must be (`Types`, `Keywords`, `ExcludedKeywords`). Any ability with an `Attach` spec needs no execution
case of its own.

- **Cost modes are the ability's own `Cost` rule**: `GoldCostRule.AttachCost()` is "paying all costs", `AttachCost(-1, 0)` is
  "paying 1 less Gold" (never below 0), and no rule at all is "ignoring costs". All three are proved on training fixtures.
- **The cost is quoted and paid through the Gold path (#303) before the card leaves the pile.** The quote is per target
  (`Kind = derived`, `TargetGold`), the engine suggests the payment, an underpaid or unpayable command is refused with the card
  still in the pile, and the payment is logged before the `equipped` event.
- **"All costs" is the attachment's Equip cost** (`EquipGoldCost`): the printed cost less every discount that applies to this
  attachment, this wielder and this player (the Clan "enters play paying 1 less" terms). **Mirumoto Katagi's "from your hand"
  discount does not apply to an attach from a discard pile** (`fromHand`); his own line says "from your hand".
- **Legality is Equip's legality**, shared through `RequireAttachable` rather than a fifth copy: the wielder is a Personality in
  play, the attachment's own "Will only/not attach to" and Honor Requirement (`AttachmentLegalTarget`; a dishonorable Personality
  has 0 Personal Honor), the Armor/Weapon caps (`AttachmentAllowed`) and Unique/Singular.
- **A card returning from a discard pile is a new entry into play**: tokens, modifiers, granted and removed keywords,
  protections, once-per-turn stamps and bowed state do not follow it back, and its "after this enters play" effects fire (a
  Destined Follower draws; Fading Shadows loses 1 Honor).
- **An opponent's Follower may be attached ("any player's discard pile"), and control FOLLOWS Jun-Ni** (RULED, issue #352: this superseded my
  first reading that it stayed the opponent's card): the Follower is controlled by Jun-Ni's controller and returns to its OWNER's pile when it
  leaves play. See H4. `GetView` lists an attachment with its host's controller (Cultist Infiltrator on an enemy Personality had the same gap).
- **Moto Jun-Ni's "Followers in this unit have Cavalry"** is a live check in `Keyword()` (her Effect
  `unit-followers-cavalry`): an attached Follower has Cavalry exactly while it is attached to her, never a durable grant. Her unit
  is then a Cavalry unit in Cavalry Maneuvers although a Follower's own printing is Infantry (tested against a control unit).

## C. Conditional effect choice (`EffectChoice.cs`), Mirumoto Yumaru

**Mendon:** "If she does not have a weapon attached, both Yumaru and the targeted Personality are moved home. If she has a weapon,
the player may choose one of three effects: send Mirumoto Yumaru home, send the enemy Personality home, send both her and the
target home. ... If she does not, the player is not presented with any choices and both she and the target are sent home."

`AbilityDefinition.Branch` is a `ConditionalChoice(Condition, Options, Otherwise)`: a condition read **when the action resolves**,
the options (each a named list of effects) offered only when it holds, and the `Otherwise` effects that simply happen when it does
not. The flow: the deferred action runs after every "before this action resolves" reaction; the condition is read; false means the
Otherwise effects apply and the action ends normally; true means a public `effectChoice` decision opens for the acting player (one
`chooseEffect` action per option) and the action waits; the chosen option's effects apply and the action then finishes through the
same tail as any other action.

- **The condition is read at resolution, not announcement** (a Weapon a before-action reaction destroys means no choice; tested with a
  fixture reactor). *Alternative:* choose up front in the command (no pause, no new decision). That would let the player commit before
  a reaction changes what she has, which the printed text does not permit.
- **The three options are exactly Mendon's** (`self`, `target`, `both`). **"Neither" is not offered.** The printed "you may ... and
  you may ..." arguably allows declining both; taking his three-option statement as the ruling, adding it is one data line
  (`new EffectOption("none", "Move neither", [])`). See Open question 1.
- **"Has a Weapon" is an attached card carrying the Weapon keyword** (an Item, Armor, Follower or Spell does not count; nor does a
  Weapon on another Personality), the existing `personality-with-weapon` reading. Weapon **tokens** (which count as attachments
  for "without attachments") are not counted. See Open question 6.
- **Negations survive the pause**: a before-action reaction's negation (Tamori Tamoko's movement, Champion of Thunder's "all") is
  cleared when the deferred command returns, so the pending decision carries it and restores it while the chosen effects apply.
  Tested with Tamoko: Yumaru's own move is not negated, the target's stays negated, with and without a Weapon.
- **The action finishes exactly like any other**: an Additional Action taken this way keeps priority while grants remain,
  after-resolution traits (Daidoji Ebizo's) fire after the choice and not before, and so on. To make that true I moved the tail of
  `RunAbilityEffects` (after-resolution traits, hand-played card discard, grant forfeiture, the "after this action" reaction
  windows and the hand-over of priority) **unchanged** into `CompleteAbilityResolution`, called at the end of the switch and after
  a choice. It is a pure extraction.

## D. Follower performer (Hiruma Tensin)

**Mendon:** "Hiruma Tensin's second battle ability: bow your performing Follower in Tensin's unit is as simple as being available
if he has an unbowed Follower; he may target that Follower and it performs the Ranged 4 Attack."

- **New Performer category `own-unbowed-follower-in-source-unit`**: an unbowed Follower attached to the source's own unit (the
  source's host, when the source is itself an attachment). It is the first Performer that is not a Personality. Its location is its
  host's, so the shared presence checks cover it.
- **The Follower's bow is its cost option** (a single `bow`, so the Follower must be unbowed), and **the Follower performs the
  Attack**: `PendingAttack.SourceId` is the Follower (the same convention Legulus's Legion already uses). For the Follower-vs-
  Personality exceptions the performer is resolved to its host, as `RequireLegalAttackTarget` always did.
- **Tensin's first ability is the published Sniper's `hiruma-sniper-attack`** (a card's ability id may be shared: uses are per card).
  Its error text no longer names "Hiruma Sniper". The two abilities are independent once-per-turn uses.
- The category is deliberately narrow (the source's own unit): Yoritomo Emoto's Follower is any of yours at the battlefield and Devoted
  Yojimbo's performer is any unit card of the army, so each is one more category (see the unblocked list).

## E. Fading Shadows

**Mendon:** "Fading Shadows effectively has the reserve ability (without having the Reserve keyword printed on it). It may be
attached as a Battle action and follows the same flow as Equip (Limited). It also should not trigger any notes in the deck builder
about having an illegal quantity unless the count of Fading Shadows is > 10."

- **The Battle action is an attach with the SOURCE as the attachment** (`AttachSource.Self`, source in hand only) and **the performing
  Ninja as the wielder** ("target your performing Ninja and Equip it to him": one choice, so the Ninja is the performer and there is
  no separate target). The Ninja is any Ninja Personality at the battlefield, bowed or not, either printed or granted Ninja. The
  "Ninja Battle" prefix needs no `ActionKeywords` tag: the card is itself a Ninja and an ability inherits its card's keywords.
- **The flow is Equip's**: cost (`AttachCost`, 0 here), attach, the entry effects (the mandatory 1 Honor loss is `EntryHonorLoss`),
  hand-over of priority. **No Additional Action is granted** (that is Equip (Battle)'s Spell rule).
- **Why the Reserve primitive is not reused literally.** Reserve for Fate cards is the player ability `equip-battle`, whose wielder
  is fixed to an **opposed unbowed Shugenja** and which grants an Additional Action for a Spell. Fading Shadows prints its own
  action naming a performing **Ninja**. So it shares Reserve's *idea* (a Fate card attached as a Battle action) but is its own
  ability, and it does **not** get the Reserve keyword, so nothing that reads the keyword sees it. *Alternative:* extend `equip-battle`
  to any card flagged "equippable as a Battle action" with a per-card wielder rule; more general, but the wielder rule would still
  be per card, and it would put a printed-ability card behind a player ability.
- **"Will only attach to a Ninja"** is `AttachRequiresKeyword`; the performer gate is the same test, so a Personality with a granted
  Ninja keyword qualifies.
- **The deck copy limit is data on the card** (`CardDefinition.DeckCopyLimit = 10`), carried into `DeckCardInfo.CopyLimit` by the
  client's draft builder and read by `DeckFormat.CheckLegality`'s optional `copyLimitFor`. It **replaces the usual 3 for that card
  only**: 10 copies raise no note; 11 raise "exceeds the card's own limit of 10"; every other card keeps 3 (1 if Unique), and a
  caller that never passes the limit keeps the old behaviour. Tested through `DeckWorkshop`, a deck file round trip and
  `DeckFormat` directly. Three other catalogue cards print such a line (Bandit's Sanctuary 5, Pierce the Tapestery 7, Yodotai
  Legionnaire "any number"); when they are published the field is one JSON value each, but "any number" is not an `int?` today.
- **Scan versus oracle text.** The Second City scan prints the older wording ("Battle: If this card is in your hand, choose your
  performing Ninja: Attach it to him", "After this card enters play"). The published text is the catalogue's current oracle
  text, like every other wave; Attach versus Equip differ only in costs and this card costs 0.

## F. Foothills Keep `oracle-l5r-2626` (WIRED in issue #352, `stronghold-wave-5`)

Left unwired in #340 because its first line changes the timing of the ordinary Equip command; wired now after Mendon's ruling. See H5.

## G. Other changes this needed

- `GameCommand.CostOption` and `WireGameCommand.CostOption` / `MatchSession.ToWire` (the server and client wire records are an explicit
  field list; the transport tests compare them with `GameCommand` by reflection and failed until both carried it).
- The performer listing in `AbilityActions` gained `own-personality-with-keyword-at-battle`, which existed for validation (Again!'s
  shape) but was never enumerated, so an ability using it could only be played by a hand-built command. Again! is now offered.
- The training AI answers a `chooseEffect` decision (the first option offered).
- `GetView` lists an attachment with its host's controller (see B).

## H. Mendon's rulings on the open questions (issue #352)

Confirmed by Mendon; the first two lines of each item are his ruling, then what was built and the alternative not taken.

**H1. Yumaru: "neither" is not a legal choice** once the action has been selected. That is what was built (`EffectChoice.cs`: three options);
closed, no change.

**H2. A destruction paid as a cost CAN be negated** (Fields of Mercy and the like). If it is, the rest of the action is negated too: the whole
action does not resolve, and costs of the action that were not yet paid are not paid. This matches the Hida Mochitoko ruling in PR #341 (a
blocked cost negates the entire action; an Honor-loss cost is then ignored; a negated action whose costs were all paid still keeps those costs) and
PR #344's whole-action negation (`ActionNegation.cs`). Built on what is on main, with a named seam for the atomic cost payment of #344's follow-up:

- **There is ONE predicate: `CostPaymentBlockedByNegation(command)`** (`AbilityCosts.cs`, agent F's atomic cost payment, #356), which folds in agent R's
  Hida Mochitoko rule (#341). It asks, before anything is paid, every cost the action would pay -- the chosen alternative cost (`CostOptions`) and each declared
  `Costs` entry -- and is true when the card a cost would bow or destroy (sacrifice included) is in play and has that negated. It is verb-specific: a negated bow
  (or "all") blocks a bow cost, a negated destruction (or "all") blocks a destroy or sacrifice, a negated destruction alone never blocks a bow, and verbs no
  negation stops (discard, remove, token, deck, Honor loss) are never blocked. This document's destroy-as-cost cases (Kitsu Fukui's chosen alternative cost and the
  four older `CostOptions` abilities) are the "chosen alternative cost" half of it, through the same `ChosenCostOption` / `CostSubjectCard`. My own separate
  seam, `CostOptionDestructionNegated`, is gone; what it added on top of the shared check is one clause in the predicate: **Fields of Mercy's allowance being
  used up blocks the destruction of a Personality paid as a cost** (`FieldsOfMercyWouldNegatePersonalityDestruction`, the read-only half of the existing check:
  it never counts a destruction). A future "this cost is blocked" rule is added there and nowhere else.
- **Asked once, BEFORE anything of the action is paid**, by `ExecuteAbilityEffects` (an action that is not negated) and by `ResolveNegatedAction` (one that
  is): a blocked cost resolves the action through `AbortActionForBlockedCost` -- still TAKEN (its use is spent, a hand-played card leaves the hand, grants are
  forfeited, priority passes, logged `actionNegated`) but none of its costs are paid (no Gold, no Favor, no bow or destroy) and none of its effects happen.
  Otherwise a **reaction-negated action** (Sakuma's) pays what it can -- the Gold **and the non-Gold cost option** (`PayCostOption`) and the declared costs.
- **Fields of Mercy's first destruction still goes through and is counted; the second is what it negates** (its existing rule); a blocked destroy
  cost leaves the Ancestor **and its attachments** alone (a cost that cannot be paid pays nothing: the attachments-still-destroyed clause of Fields of
  Mercy is about an effect, not a cost -- the alternative reading, destroying the unit's attachments anyway, would need the cost to be half-paid).
- **The order of costs is the engine's, not a printed one** (Gold, then the cost option). No published card has both a Gold cost and a destroy option, so the
  order is only exercised by a training fixture; when one exists the printed order should decide.
- **Changed by the fold**: a bow paid as a cost option is now blocked by a negated bow of the subject (Mochitoko's "negate its bowing"), as F's predicate has it; before, only a destroy option could be blocked. The four cost-choice abilities that predate the primitive keep working and are covered the same way. A blocked cost is logged `actionNegated` ("paying one of its costs ... was negated"), no longer the separate `costBlocked` + "(negated)" pair.

**H3. Fukui: closed by PR #350** (the Honor gain is independent of the performing Ancestor; the dishonorable death penalty is a separate Honor loss and
both occur). No change here.

**H4. Control FOLLOWS the Personality / the recruiting player**, for all attachments and for all recruitment from an ENEMY discard or dead pile: the
card is controlled by the Personality's / player's controller, and returned to its OWNER (owner's discard or dead pile) when destroyed or otherwise
leaves play. Built (`AttachAction.cs`, `CardInstance.OriginalOwnerId`):

- The engine reads `OwnerId` as "controller" for every card in play (every "you control" test), so taking a card into another player's control sets its
  `OwnerId` to the controller's and remembers the real owner in `OriginalOwnerId` (`TakeControl`, logged `controlTaken`). Everything the controller can do
  with it then follows with no further change: its abilities are his to use (and its owner's to refuse), Unique/Singular copies count as his, it is in his unit
  and his view, and its "after each turn begins" style effects are his.
- **It returns to its OWNER the moment it is no longer in play**, by any path: `ReturnCardsOutOfPlayToTheirOwners` runs after every command (destroyed,
  discarded, removed from the game), and `ReturnToHand` / `ToOwnerDeck` restore the owner first because they name a hand or deck. Effects of the exit itself
  (an Expendable draw, the Dishonorable-death penalty) have already resolved for the controller, correctly. Tested for a Follower destroyed alone, destroyed
  with Jun-Ni, and put into a hand (Asako Rikate): it goes to its OWNER's hand, never the controller's (see Open question 10).
- **What else takes an opponent's card into play?** I searched every path that attaches, equips or recruits: the ordinary Equip and Low Market, Equip (Battle),
  Soshi Neiru, Singh Remnants and Stormless Fury take from the player's OWN hand or own pile; a Region found by deck search comes from the player's own
  deck; Cultist Infiltrator is the player's own card attached to an enemy Personality and is **deliberately left as it was** (it prints "will not be
  transferred", and its controller stays whoever equipped it, so it already returns to its owner); Consuming Weakness, Shosuro Hawado and At Any Cost
  reach into ANY discard or dead pile but only remove a card from the game or count its cost. **Moto Jun-Ni's discard attach is therefore the only path
  that changed**, and no unrelated behaviour did. Any future recruit or attach from an enemy pile gets the rule for free by going through `TakeControl`.
- The older decision `2026-09-12-unique-singular-control-transfer.md` asked that a control-transfer primitive carry the Unique/Singular check from day one:
  one now exists for the attach path, and the check is applied at the take (`RequireAttachable` before `TakeControl`).

**H5. Foothills Keep: wired** (`stronghold-wave-5`). Mendon: "During Battle, the Personality must be at the current battlefield and the Foothills Keep
unbowed. Battle/Open actions may be performed as Battle Actions or as Open actions. This does not replace the player's Equip (Limited) ability, though it
may be used during a player's action segment (as Open actions may be used during ANY player's Action segment)."

- **(a) A player-level timing grant** (`TimingGrants.cs`): a card in play gives its controller an ADDITIONAL timing for an existing class of action.
  `EffectTimingGrants` maps the granting card's Effect (`equip-weapons-battle-open`) to `TimingGrant(ActionClass.EquipWeapon, "BattleOrOpen", ...)`; the
  ordinary `equip` command asks `EquipTimingError`: Equip (Limited) as ever, or an Open equip of a Weapon in the Action phase whenever the player holds priority
  (any player's segment), or a Battle equip (a unit at the battlefield, the wielder AT the current battlefield, the Keep unbowed). QueryActions lists them
  (`EquipActions`, labelled " · Open" / " · Battle"). A Battle equip is a Battle action like any other: it uses an unscoped Additional Action grant, is barred by
  the first-action must-pass limit, and puts the Weapon at its wielder's battlefield. **It does not replace Equip (Limited)** (tested unchanged, with and
  without the Keep).
- **(b) "Battle/Open" is the existing `BattleOrOpen` timing**: a Battle action in a battle (presence, the performer at the battlefield), an Open action in the Action
  phase in ANY player's segment (Open is `Phase.Action` with priority, not the active player) -- tested in battle, out of battle, and as the non-active player.
- **(c) The second ability** (`foothills-equip-weapon`): performer your Kensai (at the current battlefield in a battle), the Keep unbowed (the engine's
  bowed-source rule), a Weapon in YOUR pile, full Equip cost through the Gold flow, the Weapon joins its wielder. **The "[gold star]" symbol**: the Emperor Edition
  scan prints no cost symbol at all -- its cost is exactly "paying all costs" -- and the catalogue's `:gstar:` is the same "pay Gold" the other attach abilities
  use (Border Market prints it beside a separate `:bow:`, so it is not a bow). So **bowing the Keep is not a cost**; the Keep is bowed only when the player chooses
  to pay with its own production, and the requirement that it be unbowed is the ordinary "abilities on a bowed card cannot be used". The published text is the
  scan's ("Attach it to him, paying all costs"), not the catalogue's older "Equip a target Weapon ... to him".
- **Published like any Stronghold**: `stronghold-wave-5` (waves 1-3 exist and 4 is unclaimed; the offset puts this at 5), Province Strength 7, Gold Production 4,
  Starting Honor 5, Dragon. The pipeline needed nothing new.
- **Which reading of "the Keep unbowed" for the grant** is Open question 9 (I applied it to the Battle timing only).
- **Checked against main's battle-timing work (#359, #363)**: the equip is an ordinary command, not an ability, so `Absent` and `MovesToBattle` (which waive the
  presence check for abilities that reinforce a battle) do not touch it, and it keeps its own rule that a Battle equip needs a unit AT the battlefield and a
  wielder there. `ForcedPass` and `FirstActionRestriction` already bar an `equip` in a battle like any other Battle action, and it draws on an Additional
  Action burst exactly as an ability does (uses an unscoped grant and keeps priority while more remain, else forfeits them), which the tests hold. Control
  follows Jun-Ni for reactions too: a reaction that guards "his controller's Follower" (Mochitoko) is the taker's, never the owner's
  (`TakenFollowerIsProtectedByItsControllersReactions`).

**H6. Fading Shadows: confirmed as built** -- the performer is a Personality at a battlefield with the Ninja keyword. No change.

**Mutation checks for this change** (each a one-line break of the named rule; the suite rebuilt and the named tests fail; every break reverted):

| Rule broken | File | Named tests that fail |
|---|---|---|
| the predicate reads Fields of Mercy | `AbilityCosts.cs` | `CostDestroyNegatedByFieldsOfMercy`, `BlockedCostPaysNothingAfterIt`, `NegatedActionKeepsPayableCosts` |
| the predicate reads a destroy negation set for the card | `AbilityCosts.cs` | `CostDestroyNegatedByFieldsOfMercy`, `CostBlockIsPerVerb` |
| the predicate asks the chosen cost option | `AbilityCosts.cs` | `CostDestroyNegatedByFieldsOfMercy`, `BlockedCostPaysNothingAfterIt`, `NegatedActionKeepsPayableCosts`, `CostOptionIsPaidAndAtomic`, `MochitokoBlocksAFollowerCost`, `CostBlockIsPerVerb` |
| Fields of Mercy blocks only a destruction, never a bow | `AbilityCosts.cs` | `CostDestroyNegatedByFieldsOfMercy` |
| the Fields of Mercy read is pure (never counts a destruction) | `AbilityCosts.cs` | `CostDestroyNegatedByFieldsOfMercy` |
| a blocked cost is checked before the action is paid | `CardAbilities.cs` | `CostDestroyNegatedByFieldsOfMercy`, `BlockedCostPaysNothingAfterIt`, `AtomicCostPayment`, `CostOptionIsPaidAndAtomic`, `BlockingPredicateSeesEveryCost`, `MochitokoProtectsADeclaredCost`, `CostBlockIsPerVerb`, `MochitokoBlocksAFollowerCost`, `CostBlockIsPerVerb` |
| a reaction-negated action is checked for a blocked cost | `ActionNegation.cs` | `NegatedActionKeepsPayableCosts`, `AtomicCostPayment`, `CostBlockIsPerVerb` |
| a reaction-negated action keeps the cost option it can pay | `ActionNegation.cs` | `NegatedActionKeepsPayableCosts`, `CostOptionIsPaidAndAtomic` |
| taking control changes OwnerId | `AttachAction.cs` | `ControlFollowsJunNi`, `TakenFollowerReturnsToItsOwner`, `EnemyFollowerAbilitiesBelongToTheNewController`, `TakenFollowerIsProtectedByItsControllersReactions` |
| a card leaving play returns to its owner | `AttachAction.cs` | `TakenFollowerReturnsToItsOwner` |
| a card going to a hand goes to its owner's | `CardAbilities.cs` | `TakenFollowerReturnsToItsOwner` |
| the Open timing works in any player's segment | `TimingGrants.cs` | `OpenEquipInAnySegment`, `BattleEquip` |
| the Battle timing needs the granting card unbowed | `TimingGrants.cs` | `BattleEquip` |
| the Battle wielder is at the current battlefield | `MatchEngine.cs` | `BattleEquip` |
| Equip (Limited) is not replaced by the grant | `TimingGrants.cs` | `KatagiDiscountOnlyFromHand`, `EquipLimitedIsUnchanged` |
| a Battle equip uses an Additional Action grant | `MatchEngine.cs` | `BattleEquipIsABattleAction` |
| a Battle equip is barred by the first-action limit | `MatchEngine.cs` | `BattleEquipIsABattleAction` |
| Foothills' ability is Battle/Open | `CardAbilities.cs` | `PublishedShape`, `AttachAsOpenInAnySegment`, `KeepBowsOnlyWhenItPays`, `OwnWeaponControl` |
| a Battle equip stands at its wielder's battlefield | `MatchEngine.cs` | `BattleEquip` |
| only Weapons are granted the timing | `TimingGrants.cs` | `OpenEquipInAnySegment`, `BattleEquip` |

**Cards the player-level timing grant unblocks.** One table row (and an action class) each: **Glory of the Shogun `oracle-l5r-2824`** ("Equipping is a Battle/Open
action (for all players), and after the first time each turn each player Equips a card with a printed Gold Cost of 5 or more, that player may draw a card")
and **Harbingers of War `oracle-l5r-10260`** ("Equipping Followers and Items from your hand with a printed Gold Cost of 1 to 4 is a Battle/Open action for you and,
after the first time each turn you do so, draw a card"). Both are Events that must first stay in play as an ongoing effect for the rest of the game (the Zone the
engine already has for Suspicions) and add a once-per-turn draw after the equip, so the grant is the smaller half; neither had a BLOCKERS note. Floating Reserve
(`10896`, published) is a reminder text of the separate Reserve rule and needs nothing. Foothills Keep itself is the first consumer.

## Rulings recorded, and the tests that hold them

| Rule | Test |
|---|---|
| Fukui offers bow and destroy as two complete commands; naming none, or one that is not offered, is refused | `alternative-costs/EachWayToPayIsOffered` |
| Bow: the Ancestor bows and stays; destroy: it and its Followers are destroyed | `BowOption`, `DestroyOption` |
| Fukui's Honor gain is the acting player's, independent of the Ancestor and of the bow (issue #349) | `HonorGainIsIndependentOfThePerformer` |
| Each option is independently payable | `EachOptionIsPayableOnItsOwn` |
| Which card pays is per option | `TheSubjectIsPerOption` |
| The four older bow-or-bow choices are one bow option (and a bowed wielder cannot pay The Earth's Hunger) | `ExistingBowChoicesAreOneOption` |
| Jun-Ni's targets, per-target quote, pay-then-attach, opponent's Follower, legality, fresh entry, entry effects, live Cavalry | `attach-from-discard/*` |
| Cost modes and the Foothills Keep shape; Katagi's discount only from hand | `CostModes`, `FoothillsShape`, `KatagiDiscountOnlyFromHand` |
| Yumaru: no Weapon means no decision; a Weapon means exactly three options; condition read at resolution; negation survives; finishes like any action | `effect-choice/*` |
| Tensin: one offer per unbowed Follower of his unit; the Follower bows, then performs a Ranged 4 Attack | `follower-performer/*` |
| A destruction paid as a cost is negatable and negates the whole action; a blocked cost pays nothing after it; a negated action keeps the costs it can pay (issue #352) | `CostDestroyNegatedByFieldsOfMercy`, `BlockedCostPaysNothingAfterIt`, `NegatedActionKeepsPayableCosts` |
| Control follows Jun-Ni; a taken card returns to its owner on any exit; its abilities are the controller's (issue #352) | `ControlFollowsJunNi`, `TakenFollowerReturnsToItsOwner`, `EnemyFollowerAbilitiesBelongToTheNewController` |
| Foothills Keep: the Battle/Open attach, and the player-level timing grant that ADDS Battle/Open equips of Weapons without replacing Equip (Limited) (issue #352) | `foothills-keep/*` |
| Fading Shadows: from hand only, to a performing Ninja, Battle only, entry Honor loss, no additional action; 10 copies clean, 11 flagged | `fading-shadows/*` |

**Mutation checks** (each one-line break of the named rule, the suite rebuilt, the named tests fail; every break was reverted and the
suite re-run green):

| Rule broken | File | Named tests that fail |
|---|---|---|
| bow needs an unbowed subject | `AlternativeCosts.cs` | `EachOptionIsPayableOnItsOwn`, `ExistingBowChoicesAreOneOption` |
| destroy option really destroys | `AlternativeCosts.cs` | `CostAndHonorAreIndependentOfTheBow`, `DestroyOption`, `EachOptionIsPayableOnItsOwn`, `TheSubjectIsPerOption` |
| two options need a choice | `AlternativeCosts.cs` | `EachWayToPayIsOffered`, `TheSubjectIsPerOption` |
| no options refuses a named option | `AlternativeCosts.cs` | `NoOptionsMeansNoChoice` |
| the cost is paid | `CardAbilities.cs` | `BowOption`, `CostAndHonorAreIndependentOfTheBow`, `DestroyOption`, `EachOptionIsPayableOnItsOwn`, `ExistingBowChoicesAreOneOption`, `TheSubjectIsPerOption`, `AttackReactionWindow`, `AttackTargeting`, `FollowerBowsThenAttacks` |
| Fukui's gain made the performer's action again (rehonor conversion; issue #349) | `CardAbilities.cs` | `HonorGainIsIndependentOfThePerformer` |
| No gain when the target's bow is negated (issue #349) | `CardAbilities.cs` | `CostAndHonorAreIndependentOfTheBow`, `HonorGainIsIndependentOfThePerformer` |
| No gain when the Ancestor was destroyed as the cost (issue #349) | `CardAbilities.cs` | `CostAndHonorAreIndependentOfTheBow`, `DestroyOption`, `HonorGainIsIndependentOfThePerformer` |
| No gain for a dishonorable Ancestor (issue #349) | `CardAbilities.cs` | `HonorGainIsIndependentOfThePerformer` |
| subject per option | `AlternativeCosts.cs` | `TheSubjectIsPerOption` |
| attach cost is charged | `GoldQuotes.cs` | `AttachOwnFollowerPaysThenAttaches`, `CostIsPaidBeforeTheCardLeaves`, `CostModes`, `KatagiDiscountOnlyFromHand`, `OffersEveryFollowerInEitherDiscardPile` |
| attach legality is Equip legality | `AttachAction.cs` | `LegalityIsEquipLegality` |
| re-entry is fresh | `AttachAction.cs` | `ReEntryIsFresh` |
| from-hand discounts only from hand | `GoldQuotes.cs` | `KatagiDiscountOnlyFromHand` |
| own pile only | `AttachAction.cs` | `FoothillsShape` |
| attached card is at its wielder's battlefield | `AttachAction.cs` | `FoothillsShape`, `EquipsFromHand` |
| Followers in the unit have Cavalry | `CardAbilities.cs` | `CavalryGrantIsLive` |
| the branch reads the Weapon | `EffectChoice.cs` | `ConditionIsReadAtResolution`, `NegationSurvivesTheChoice`, `NoWeaponMovesBothHome`, `OnlyHerOwnWeaponCounts` |
| negations survive the deferred choice | `EffectChoice.cs` | `NegationSurvivesTheChoice` |
| the choice finishes like any action | `EffectChoice.cs` | `FinishesLikeAnyOtherAction` |
| only a listed option | `EffectChoice.cs` | `TheChoiceIsValidated` |
| Follower performer is in the source's unit | `CardAbilities.cs` | `OneOfferPerUnbowedFollowerInHisUnit` |
| the Follower's bow is its cost | `CardAbilities.cs` | `AttackReactionWindow`, `AttackTargeting`, `FollowerBowsThenAttacks`, `PublishedShape` |
| a self-attach card must be in hand | `CardAbilities.cs` | `EquipsFromHand`, `HiddenInformation`, `OfferedToNinjasFromHandInBattle`, `OnlyNinjasReceiveIt`, `TwoCopiesAreTwoOffers` |
| the deck copy limit | `DeckFormat.cs` | `DeckCopyLimit`, `DeckFileRoundTrip` |
| a unit is listed with its controller | `MatchQueries.cs` | `AttachOpponentsFollower` |
| the Limited use is spent (once per turn) | `CardAbilities.cs` | `TwoIndependentAbilities` |

## Cards the primitives unblock (not wired here; their BLOCKERS entries now say what is left)

- **Directly, one ability definition each:** Mirumoto Katagi - exp `oracle-l5r-10631` (`Battle, [Gold]: Equip a target non-Unique
  Weapon in your discard pile to Katagi` is `AttachSpec(Target, Own, Source, [Item], [Weapon], excluding [Unique])` at full cost).
- **One small extension away:** Inspirational Address `3767` and Harsh Taskmaster `10151` (a hand-**or**-discard pile; the second also
  needs a delayed destroy), On the Hunt `5704` and Daigotsu Chiboshi `10874` (a hand pile), Frontier Farmer `10681` (the source in the
  pile is the attachment; hand-only today), Border Market `1108` (a "bow this card" subject on a non-performer plus Follower-or-Item
  attachments), Shinobi Vassal `10517` and Ikoma Shinohara `3639` (the self-attach exists; their wielder is a chosen target).
- **Alternative costs:** Hundred-Fold Cut `3513` and Well Protected `9288` are "bow or destroy" costs on a Weapon / Armor (subject
  `Target` or an attached card); both are Reactions, so they also need the in-play targeted-reaction window.
- **Follower performer:** Yoritomo Emoto `9544` (any own unbowed Follower at the battlefield, one more category), Devoted Yojimbo
  `1992` (a unit card of either type in the army).

## Open questions for Mendon

Answered in issue #352 (see H): 1 (Yumaru "neither"), 2 (a negated destruction cost), 3 (Fukui's Honor gain, PR #350), 4 (control of an attached
opponent-owned Follower), 7 (Foothills Keep's timing) and 8 (Fading Shadows' wielder). Still open:

5. **"Paying all costs" for a discard-pile attach** includes the attachment's own conditional discounts (Clan "enters play paying
   1 less") but not "from your hand" ones; and never waives an Honor Requirement. Confirm.
6. **Weapon tokens** count as attachments for "without attachments"; should a Weapon token count as "has a Weapon" for Yumaru?
9. **Foothills Keep's Battle timing and a bowed Keep** (from H5): I read "During Battle, ... the Foothills Keep unbowed" as applying to the Battle
   timing of BOTH lines (the equip grant and the attach ability), and the granted Open timing as a trait that a bowed Keep still gives. Confirm.
10. **Put into a hand (Asako Rikate) and a card you control but do not own:** it goes to its OWNER's hand (H4), not the controller's.
11. **Cultist Infiltrator** is an attachment too, and "control follows the Personality" for ALL attachments would make an enemy host's controller control it.
    I left it as it was (it prints "will not be transferred" and "you may attach this card to a Personality you do not control"; its controller stays whoever
    equipped it), the one attachment that does not follow its host. Confirm.

## Affected files

`AlternativeCosts.cs`, `AttachAction.cs`, `EffectChoice.cs` (new), `CardAbilities.cs` (ability data, validation, listing,
execution, `CompleteAbilityResolution`, the live Cavalry check), `GoldQuotes.cs` (`AttachCost`), `Models.cs`
(`GameCommand.CostOption`, `MatchState.PendingEffectChoice`, `CardDefinition.DeckCopyLimit`), `MatchEngine.cs`, `MatchQueries.cs`,
`DeckFormat.cs`, `DeckWorkshop.cs`, `CardCatalog.cs` / `PublishedContent.cs` / `L5R.Core.csproj` (waves and training fixtures),
`L5R.Server/Protocol.cs`, `L5R.Client/MatchSession.cs` and `DeckBuilderScreen.cs` (one line each), `L5R.AI/TrainingOpponent.cs`,
`content/published/personality-wave-31*.json` and `follower-wave-17*.json`, the five art files,
`tools/content/package_personality_wave_31.py` and `package_follower_wave_17.py`, `tools/content/build_card_inventory.py`,
`docs/CARD_INVENTORY.xlsx`, and the tests (`AlternativeCostTests`, `AttachFromDiscardTests`, `EffectChoiceTests`,
`FollowerPerformerTests`, `FadingShadowsTests`, `Issue337Support`, the pinned published count in `PublishedTests`).
