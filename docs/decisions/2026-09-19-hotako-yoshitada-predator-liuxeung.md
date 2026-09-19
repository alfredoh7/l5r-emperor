# Hotako exp3, Yoshitada, Sea Troll Predator, Liu-Xeung exp, Bonds of Coin, Gold and Steel, Fear Me! (personality-wave-32, strategy-wave-21)

Status: implemented; the readings marked **Mendon** are his rulings, quoted from the task brief of 2026-09-19 and from his answers to the open questions on
PR #348 (**confirmed by Mendon**, recorded as "Rulings on PR #348" below). Every other reading is a decision of this change, recorded with its alternative;
none is still open. Lane: rules (mdornbrook; cross-lane edits under his standing authority). Issue: #336. UI follow-ups for Alfredo are separate issues (below).

The cards, all read from their scans (Second City 60 / 24 / 73 / 69 / 108, Emperor Edition 302, Before the Dawn 118; working copies checked
against their receipts) and published whole:

> **Daigotsu Hotako, Experienced 3** (oracle-l5r-1726) -- Force 7, Chi 4, no Honor Requirement, Cost 11, Personal Honor 0, Spider Clan / Shadowlands /
> Samurai / Naval / Paragon / Experienced 3 / Unique / Obsidian Champion. Enters play paying 2 less Gold if you are a Lion Clan player. After Hotako
> enters play: Lose 2 Honor. Fear Open: Target a Personality: He may not perform actions this turn.
>
> **Doji Yoshitada** (2179) -- Force 3, Chi 4, Honor Requirement 6, Cost 7, Personal Honor 3, Crane Clan / Samurai / Duelist / Magistrate. Political
> Battle: Target an enemy Personality: Bow him. If he is dishonorable, choose a player who gains or loses 2 Honor.
>
> **Sea Troll Predator** (6522) -- Force 6, Chi 4, no Honor Requirement, Cost 8, Personal Honor 0, Unaligned, Shadowlands / Nonhuman / Troll / Earth /
> Tactician. After Predator enters play: Lose 3 Honor. Battle: Target a Personality opposing Predator at the current battlefield: Move the Personality and
> Predator to the same adjacent unresolved battlefield.
>
> **Utaku Liu-Xeung, Experienced** (9085) -- Force 4, Chi 3, Honor Requirement 4, Cost 9, Personal Honor 4, Unicorn Clan / Samurai / Battle Maiden /
> Cavalry / Paragon / Experienced / Unique. After the resolution of an action that gave a Force penalty to an enemy card at Liu-Xeung's battlefield: Give
> Liu-Xeung +1F. Battle: Target an enemy Personality with lower Personal Honor: Move him home. Gain 1 Honor.
>
> **Bonds of Coin** (1092, Focus 4) -- Political Open: Destroy one to three target attachments with a 0 Gold Cost. Favor Political Limited: Bow your
> Stronghold and discard the Imperial Favor to draw two cards. **Gold and Steel** (2865, Focus 2) -- Battle/Open: Target one to three attachments with a Gold
> Cost of 0: Destroy them. Battle: Choose your performing unbowed Personality and target an enemy Personality with an equal or lower Gold Cost: Bow him. You
> may move him home. **Fear Me!** (2493, Focus 4, Fear / Iaijutsu) -- Battle: Choose your performing unbowed Duelist and target an enemy Personality with
> equal or lower Chi: Move him home. If he moved, choose a player, who gains or loses 2 Honor.

Published text is the catalogue text. Two differences from the period scans, both harmless: Bonds of Coin's scan prints "Political" as its keyword line and no
"Favor" on the Limited action (the catalogue says "Favor Political Limited", and so does this change: discarding the Imperial Favor is a Favor action), and it
prints "Open: Target one to three attachments with a 0 Gold Cost: Destroy them" for the catalogue's "Political Open: Destroy one to three target attachments
with a 0 Gold Cost" -- the same action. Published count: +7 (409 -> 416 on the 7f306655 base; 421 after merging origin/main 8b378a8d, which brought #337's five to 414; **424** after
merging origin/main 05cc2493, whose pinned count is 417 after #344's three: Fukuzo, Yoritomo Sakuma, Unfortunate Hesitation -- recounted from the merged tree, 417 + 7).

## Mendon's rulings (the task brief, verbatim where it matters)

- **"X Champion" keywords:** "Keyword 'Obsidian Champion' is not mechanical but is conditional for the attachment of some items and event effects (this is
  generally true of all 'X Champion' keywords)." Nothing in the engine treats a Champion keyword specially; `AttachRequiresKeyword` and `RequiredKeywords`
  already test any keyword string. Proven with a fixture Item that attaches only to an Obsidian Champion and a fixture Strategy whose performer must be one
  (`ChampionKeywordIsOrdinary`): both accept Hotako and no other Personality. Event conditions on a Champion keyword are the same test and are not exercised
  (no such Event is wired).
- **Fear Open:** "this is a Fear Action. The target Personalities may not perform actions (this includes any actions on items, spells, or on the Personality as
  well as any actions that the player would name the Personality as the performer). They may still be the target of actions. This effect should reset during
  the End phase, after which the Personality may perform actions again."
- **Multiple independent targets:** "requires an action primitive that has multiple independent targets, the first (for Yoshitada) is a Personality, the
  second, a player. These targets may be any two card types (or player) listed on the action. ... The player activating Yoshitada's (and subsequent
  multiple-target actions) should be prompted to select targets. A viewer may need to be drafted ... there is a strategy card that allows a player to destroy
  'Up to three 0 Gold cost attachments'."
- **Movement:** "Most movement cards specify the movement to location of cards. In the case of Troll Predator, this is to an adjacent province (already defined
  in the engine). For other cards, it moves Personalities to the current battlefield, and still others to a battlefield of a player's choice. Using the
  assignment primitive (with arrows) allow players to choose the appropriate location based on the destination defined by the card."
- **Stat penalties:** "A stat penalty is any modifier that is applied to a statistic that is negative. Giving a Personality -4F is a Force penalty. Giving a
  Personality -1C is a Chi penalty. Giving a Personality a -2 Gold Cost (or reducing their gold cost to zero) is a Gold cost penalty, and giving a Personality
  a -1 Personal Honor is a Personal Honor penalty (dishonoring a Personality is not a Personal Honor penalty). For Utaku Liu-Xeung, this is a trait, and any
  time a Personality opposing her at a battlefield where she's present is given a Force penalty, she gains +1F (this occurs automatically, is not optional, and
  occurs every time a Force penalty is applied)."

## Rulings on PR #348 (confirmed by Mendon)

Mendon answered the six open questions of the first version of this document, and later added a seventh ruling on the action-lock. All seven are applied on the
same branch; where a section below changed because of one, it says so.

1. **No End-phase actions exist.** The engine offers no action of any kind in the End phase, so "reset during the End phase" and "until the turn ends" are the same
   thing; there is nothing for the lock to block there and no code to write. (Section 1, "Duration".)
2. **"Equal or lower Chi" compares CURRENT Chi** (printed plus modifiers, plus Items' Chi -- `PersonalityChi`, which `personality-chi-lte-self` already used), **unless the card text says
   printed or base.** The general reading, for every card: a stat named in an ability's text is the stat as it stands now (Force, Chi, Personal Honor, Gold cost), and only the words
   "printed" or "base" select the printed number. Fear Me! and Chokehold (and Ring of Fire, the third card on `enemy-personality-chi-lte-performer`) now read current Chi; each is tested
   with modifiers that flip the result (`CurrentChiFlipsTheResult`). The printed/base audit is in section 6.
3. **Conditional sentences are independent, and a conditional follow-up is prompted only when its condition holds.** Doji Yoshitada's "Bow him" always happens; "If he is dishonorable,
   choose a player who gains or loses 2 Honor" is a second sentence, so the player and the gain-or-lose choice are asked **at resolution, after the bow, and only if he is dishonorable.**
   Fear Me!'s player choice is asked only if the target moved. The multi-target primitive therefore has two kinds of slot: **up-front** slots (chosen with the command) and **conditional
   follow-up** slots (decided at resolution). Section 3.
4. **Predator's two movements are separate effects.** If the move of either Predator or the target is negated, the OTHER still moves, unless an effect also negates it, or the whole action
   is negated (then neither moves). Section 4; all three cases are tested (`EachMoveNegatedSeparately`).
5. **Multi-target reaction windows are not primary-target-only.** There is one before-action reaction window per target that could react, in slot order. Section 3.
6. **Adjacency between battlefields follows the DEFENDER's province line at every call site.** The two older sites that took it from the wrong player's line
   (`personality-at-adjacent-battlefield`, Moto Gunso's boost) are fixed. Section 4 states the behaviour change.
7. **The action-lock covers innate actions too** (ruling of the same day): "No actions means even innate actions may not be performed by a Personality who is barred from taking actions.
   The Personality may still be assigned to attack or defend, as those are not actions." Order Seppuku is barred; assigning to attack or defend, and being targeted or moved, are not.
   Section 1.

## 1. The action-lock (`ActionLock.cs`)

**Model.** `CardInstance.ActionLocks` is a list of `ActionLock(Source, Duration)` (the `GrantedKeyword`/`Protection` convention); Duration `"Turn"` (the only one
used) is swept by `FinishTurn` with the other turn-long statuses. `LockActions(card, sourceId)` places it. `CardView.ActionLocked` is public information
(true on the locked card and on each of its attachments).

**Who is blocked (the reading).** The check is `ValidateAbility` -> `RequireCanPerform`, so every action there is is covered, because they all reach the
engine as an ability command -- an ability printed on a Personality, an Item, a Spell, a Follower; a Reaction; a Strategy that names its performer. The
*performer* is the card the ability names as its performer when that is a Personality or a Follower ("choose your performing Samurai"), otherwise the card the
ability is on. A card is locked when it, or the Personality it is **attached to**, is locked (`ActionLocked`), which gives Mendon's list: the Personality's own
actions, those printed on its Items and Spells, and those printed on its Followers. Offered actions are filtered by the same method, so a blocked action is
listed disabled with "X may not perform actions this turn." (`QueryActions`), and one sent anyway is refused atomically. A Reaction listed inside a reaction
window is offered before `ValidateAbility` sees it, so `AddReaction` asks the lock too (`ActionLockRejection`).
*Alternatives considered:* (a) a locked *Follower's own* lock does not block its host's actions (yes: only an action by, or through, the locked card; the
Personality's Items and Spells are performed by the Personality, not the Follower); (b) a lock blocking the Personality but not the actions printed on its
Followers (rejected: Mendon's list names Followers).
*Cost-choice performers.* A few abilities name a "performer" that is only a cost (Xijkt's Holding to bow instead of himself, Yayu's Spell): such a performer does
not perform, so the action is still the source's and is blocked with him.
*Copied abilities.* An ability a locked Personality copied (Fukuzo's copy primitive, #338: a `GrantedAbility` with `CopiedFrom`) is barred exactly like a printed one -- the lock is
on the source card, whichever list the ability came from (`CopiedAbilityIsBarred`: listed disabled, its "copied from" label intact, refused when sent).
*What is not blocked:* traits and mandatory effects (they are not actions: Daidoji Ebizo's +1F, Bofana's discard, Liu-Xeung's +1F still work on a locked
Personality); being targeted, bowed, moved, destroyed (`LockedCanBeTargeted`, `AssignmentIsNotAnAction`); and **assigning to attack or defend** (Mendon: those are not actions --
`DeclareAttack`/`AssignDefense` never ask the lock; a locked Personality is in the assignment options, is assigned, fights with his Force counted, and is still locked and unable to act
in the battle).

**Innate actions (Mendon's ruling 7).** The first version left Order Seppuku unblocked, reading it as the player's action. Mendon: no, a Personality barred from taking actions may not
perform an innate action either. Order Seppuku (an Open action of a dishonorable Samurai, Courtier or Shugenja) is now barred: listed disabled with "X may not perform actions this
turn." and refused by `OrderSeppuku` (`InnateActionSeppukuIsBarred`; also end to end with Hotako's real Fear Open). *Inventory.* Every action type `QueryActions` offers
(`MatchQueries.cs`), by who performs it -- pinned by `InnateActionInventory`, which fails when a new type appears until it is classified here:
- **Performed by a Personality, barred:** `useAbility` (an ability on the Personality, its Item, Spell or Follower, a Reaction, a Strategy naming him as its performer, a copied
  ability) and `orderSeppuku`. These are the only two.
- **The player's own, not barred by a lock on a Personality** (no Personality performs them): `purchase`, `recruitReserve`, `overlay` (an Experienced Personality entering play over a
  lower one: the player's), `equip` (attaching from hand: the player's; the Follower or Item's *entering-play* Reaction is a Reaction and
  is barred separately when its host is locked), `playStrategy` (a Strategy naming no performer; one naming a locked performer is `useAbility` and barred), `proclaim` (the entering
  card's controller), `endDynasty`, `pass`, `wait`, `concede`, `declineReaction`, `declineStraighten`, `selectBattle`, `discardHand`, `discardProvince`, `forcedDiscard`, and the choice
  actions of a pending decision (`beforeBattleOptional`, `beforeBattleOrder`, `chooseConqueror`, `choosePreservingHonor`, `chooseRikateDetach`, `chooseRinShame`, `chooseShinden`,
  `chooseYourClanNeedsYouProvince`, `encircledOrder`, `encircledSelect`, and `chooseEffect`).
- **Not actions at all (Mendon):** `attack` and `assignDefense` -- the assignments.
*Alternative:* treating `overlay`, `equip` or `proclaim` as actions of the Personality they involve (rejected: the card's text names the *player* who does them, and the Personality being
equipped or replaced is a target, which a lock never blocks).

**Duration: "this turn" and "reset during the End phase" coincide (Mendon, ruling 1: no End-phase actions exist -- a note, no code change).** The lock lasts until `FinishTurn`, which runs at the very end of the End phase (after the
draw and the hand-limit discard). The engine offers no action of any kind in the End phase (`Open` is Action-phase only; the only pending choice is the hand
discard), so a lock that "resets in the End phase" and one that "lasts until the turn ends" are indistinguishable. `LockLastsUntilTurnEnd` drives a whole real turn
-- the Action Phase, a real battle in the Attack Phase where the locked Defender cannot act, the End Phase stopped at its hand discard (the lock still there, and
no action offered to either player) -- and shows it gone when turn 2 begins. `LockedOnTheOpponentsTurn`: an Open action taken on the *opponent's* turn ends with
**that** turn, not on the controller's next one (turn 2 is the other player's; turn 3, his own, is free too). *Alternative:* a lock lasting until its target's
controller's next turn (rejected: "this turn" is the default duration, and the card says it).
A second lock on a card is just another entry. An Experienced overlay carries locks over with the rest of the transferred state, and a card returned to a deck (or an
attachment returned to a hand) loses them -- the same handling as its Modifiers and Protections (not separately tested).

**Lock, dishonor, Bofana.** Nothing interacts: the lock is not a status the engine's other rules read.

**Unblocks / related unwired cards:** Tamori House Guard (oracle-l5r-7776, "It may not perform actions" -- the old note said the same gap as Hotako's) and Hiruma
Nikaru (3310) need nothing more; Kuon's Legacy (9913) needs its other effects. Kakita Munemori - exp (4203, "until the current phase ends") and Nerve Strike (5532,
"until this battle ends") need a phase- or battle-scoped Duration on the same record; Incense Mill (10035) needs an effect that removes locks and current Force
penalties.

## 2. Hotako's other halves

**The Lion Clan entry discount through `QuotePurchase`.** The old term existed only in `EquipGoldCost` as three hard-coded Effect names each worth 1. It is now one
general term, `ClanEntryDiscount` (`ClanEntryDiscount.cs`): Effect `"<clan>-clan-discount"` is 1 less (the three existing cards keep working unchanged),
`"<clan>-clan-discount-N"` is N less; Hotako's Effect is `lion-clan-discount-2`. Read by `QuotePurchase` (so the Purchase, the Reserve recruit, Your Clan Needs
You! and Shinden Shorai all price through it, and the payment panel's basis reads "cost 11 - 2 (Lion Clan)") and by `EquipGoldCost`. It stacks with the buyer's
own Clan discount and allegiance (which need a Personality of the buyer's own Clan, so never for a Spider Hotako), floors at 0, and is for that Clan's player only.
*Alternative:* a `CardDefinition` field pair (clan, amount) instead of an Effect name: more explicit, but every packager and the published JSON schema would change
for a term the existing three already write as an Effect. Not done. An Experienced overlay is free and carries no Gold term at all.
**"After Hotako enters play: Lose 2 Honor"** is `EntryHonorLoss 2` (a mandatory trait, no "Reaction:" label). Proven: the Lion buyer loses 2 Honor automatically and no
Reaction is offered.
**Fear is a keyword the engine knows for its actions.** `hotako-fear-lock` carries `ActionKeywords: ["Fear"]` (the #321 tag every "was that a Fear action?" check
reads via `ActionCarriesKeyword`, which also inherits the card's own keywords). Fear Me! carries Fear as a card keyword. Yoshitada and Bonds of Coin are tagged
`Political`, Bonds of Coin's Limited half `Favor` + `Political`; nothing reads those tags yet. The two inventory tests that pinned "every ActionKeywords ability" now
pin the **Recon** tag they were written about.

## 3. Multi-target actions (`TargetSlots.cs`)

**Shape.** An ability with `Slots` (Target `"slots"`) names an ordered list of `TargetSlot(Id, Category, Label, Min, Max, MustBe, PlayerDefault, Resolution)` (`Resolution` makes it a conditional follow-up, below): `Category` is any
category the single `Target` accepts, or `"player"`; `Min..Max` is the count ("one to three" is 1..3, "up to three" is 0..3). The command carries
`Selections: SlotSelection[]` (`{Slot, CardIds}` or `{Slot, PlayerId}`), one per slot, in `GameCommand`.
**Validation reuses every category.** `ValidateSlots` checks the counts, distinctness, the shape of each selection (cards vs a player), and judges **each candidate
alone** by running `ValidateAbility` again for a copy of the ability whose `Target` is the slot's category (`ValidateSlotCard`; the copy stops before the
whole-action checks and the price). So the giant category switch stays where it is, every category works in a slot, and nothing about a category is written twice.
Slots are independent: a slot's legality never reads another's. *Alternative:* a separate category evaluator extracted from `ValidateAbility` (a large move of code the
other agents' concurrent edits would have conflicted with; the recursion needs a five-line change).
**Offering.** `AbilityActions` returns one `ActionOption` with `Slots: TargetSlotOption[]` -- per slot its label, kind, Min/Max and every legal candidate (`CardIds`,
each judged alone; `PlayerIds`) -- `TargetIds` null, and a complete legal default `Command` (the first legal candidate in each required slot; the acting player for a
"gains" player slot, the opponent for "loses"). The option is disabled, with the most common refusal as its reason, when a required slot has no legal candidate.
Candidates come from the cards in play, the acting player's own hand and the public discard piles; the opponent's hand, decks and face-down cards are never
candidates, so nothing hidden reaches the options (`HandSlotIsPrivate`: no opponent hand id appears in the serialized options).
**Readers of "the target".** `UseAbility` normalizes a command so `TargetId` is the first card of the first card slot; every reader written for a single target
(Akodo Kakihara's "targets him", the before-action window) sees the primary target unchanged. `ActionTargetIds` (Bofana's trait) also yields every later card, so
Bofana triggers when a *second* card slot names him (`BofanaSeesSlotTarget`). The before-action reactions (Rokai, Tamoko, Champion of Thunder, Brothers of Goemon, Kakihara, Sakuma)
do **not** stop at the primary target: each target has its own window (below, Mendon's ruling 5).
*Alternatives for the representation:* extra typed fields on `GameCommand` (`TargetId` + `TargetPlayerId` + `CardIds` -- no way to say which is which for two card
slots), or a `Dictionary<string,string[]>` (stringly, no player kind). Rejected for `SlotSelection`.

**Two kinds of slot (Mendon's ruling 3).** A slot is **up-front** (chosen with the command, everything above) or a **conditional follow-up**: `TargetSlot.Resolution` (a
`ConditionalChoice(Condition, Options, Otherwise)`, the pending-decision shape of PR #340: `PendingEffectChoice`, PendingChoice type `effectChoice`, `chooseEffect` actions,
`BeginConditionalChoice`, `CompleteAbilityResolution`, `Branch`). A follow-up slot takes no selection with the command (`ValidateSlots` refuses one: "this is decided when the action
resolves, not chosen now") and is listed in `ActionOption.Slots` as an entry of kind `follow-up` with `Timing = "at-resolution"`, its `Condition` and its `OptionLabels`, so a viewer can
draw "if he is dishonorable: choose ..." next to the up-front pickers without offering anything to click. At resolution the ability's own effect runs first (the bow, the move), then
`BeginConditionalChoice` judges the condition **against the result**: if it holds, a public `effectChoice` opens for the acting player with the four `HonorChoice.Options` (you gain,
your opponent gains, you lose, your opponent loses -- who is relative to the acting player, so the labels read the same at either seat); if it does not, the `Otherwise` branch runs
(nothing) and **nothing is asked**. The action finishes through `CompleteAbilityResolution` either way, so once-per-turn stamps, priority, Additional-Action grants and Bofana-style
readers are exactly those of any other action that paused for a decision (the pending state survives JSON and is public: no hidden information).
**Yoshitada.** `yoshitada-bow`: an up-front `target` slot (`enemy-personality`) and the follow-up `honor` slot, `TargetDishonorable`. "Bow him" always happens (and is negatable and
reactable like any bow); the player and direction are chosen after it, only if he is dishonorable **then** (judged after the bow, by `MatchesTargetStatus`). A gain for his own controller
while he is dishonorable rehonors him instead (`ApplyPlayerHonorChoice`, Doji Shunya's precedent; a loss, or a gain for the other player, is plain), the performer being the card the
action names as performer, else its source. *Alternatives:* (a) the first version of this change asked for the player up front and made "gain" and "lose" two abilities
(`yoshitada-bow-gain` / `-lose`) -- rejected by Mendon's ruling 3, which is why they are gone, the once-per-turn key with them; (b) a `direction` choice kind of up-front slot -- no, the
sentence is conditional, and only the follow-up kind keeps the condition where the text puts it.
**Fear Me!** is the same shape, `fear-me-move`: a performing unbowed Duelist, `enemy-personality-chi-lte-performer` (current Chi, ruling 2), move him home, then the follow-up slot with
the condition `TargetMoved` -- a move that was negated (Tamoko, the whole action) changes nobody's Honor and asks nothing; the performing Duelist, not the Strategy, is the performer
for the rehonor rule.

**One before-action window per target (Mendon's ruling 5; `TargetWindows.cs`).** An action with several targets no longer reacts only to its first.
- *Which windows.* Every card of every up-front card slot, in **slot order, then the order chosen within the slot**, without repeats (`ReactionWindowTargets`; a single-target action is
  the same list of one). A player slot and a follow-up slot name no card and open none. A target for which **nobody could react** -- neither player holds a reaction that applies to
  *that* target (`OwnsBeforeActionReaction(player, command, target)`) -- opens **no window**; if no target has a possible reactor the action resolves at once, as before.
- *How each window plays.* Exactly the alternating window of #318/#327, once per target: the player whose turn it is has the first opportunity **when he holds a reaction for that target**
  (otherwise the other player does -- `FirstReactionOpportunity`), a reaction played resets the pass count and hands the opportunity over, and two passes in succession close it
  (`OpenAlternatingReactionWindow`). The window's `PendingChoice` names the target (`CardId`, and a prompt "Before this action resolves, for X: ..."); the single-target prompt is
  unchanged. Closing one window opens the next (`OpenNextRemainingTargetWindow`); when the last has closed the deferred action resolves **once**.
- *What a reaction sees.* While a window is open, "the card this action targets" for every before-action reaction (Rokai, Tamoko, Champion of Thunder, Brothers of Goemon, Kakihara,
  Sakuma, and the `training-*` fixtures) is the window's target (`ReactionAffectedId`), not `command.TargetId`; so Kakihara reacts only in *his* window, and a Champion of Thunder
  in the window of a target in his unit. `BeforeActionReactionApplies`/`OwnsBeforeActionReaction` take the target explicitly for the same reason.
- *Composition.* The negations each window sets (`MatchState.NegatedEffects`, per card) accumulate and apply together when the action resolves; a reaction that negates the **whole action**
  (`NegateDeferredAction`, Sakuma) skips the windows still to come -- there is nothing left to react to. Window state (`ReactionWindowTargetId`, `ReactionWindowRemaining`) is public and
  cleared when the deferred action resolves. Proven with a two-target action whose targets each have a different reactor, in both slot orders, a target with no reactor, no reactor at all,
  the alternation inside a window and both kinds of negation (`OneWindowPerTarget`, `PerTargetWindowsAlternate`).
- *Isolation, for PR #341* (agent R's unmerged "targeted by an action" trigger, redirect and `RecheckTarget`): all of it lives in `TargetWindows.cs` plus one call in
  `ResolveOrDeferBeforeActionReaction` and one in the `beforeActionReaction` handler; the reactions themselves changed only by reading `ReactionAffectedId(command)` where they read
  `command.TargetId`. R's trigger should be raised per window target from the same place, and a redirect should update the window's target list (`ReactionWindowRemaining`) -- the seam.

**Bonds of Coin / Gold and Steel.** One slot, `zero-cost-attachment`, 1..3: an attachment (Follower, Item, Spell) whose **current** Gold cost is 0 -- printed cost plus
every `GoldCost` modifier (`CardGoldCost`), so a Gold cost penalty can take an attachment there. They are destroyed in the order chosen; one already gone with a
Personality an earlier one destroyed is skipped. Political Open is an Open action (either player, Action Phase); Gold and Steel's is Battle/Open, so as a Battle action
only attachments at the current battlefield are targets (the shared presence rule, unchanged). Bonds of Coin's second ability bows the Stronghold, discards the Imperial
Favor and draws two; it is Limited, needs the Favor and an unbowed Stronghold, and counts as a Favor action for the Rings' "two Favor actions" condition like the other
Favor abilities. Gold and Steel's "you may move him home" is the pair `gold-and-steel-bow` / `gold-and-steel-bow-move` sharing one use (the paid-bow shape), its
Gold-cost comparison `enemy-personality-gold-cost-lte-performer` reads current cost.

**Other cards that use the shape, unwired** (found by searching the catalogue for "one to three", "one or two", "up to", two-target texts): Ikoma Shinju (3638: a Terrain
and an enemy card), Armor of the Mad (10529), Defensive Formation (10721), Chaos of Battle (10716), The Distant Reaches (8030), Striking Through the Void (7587, whose
-1F/-2F/-3F reads the order chosen), The Power of a Word's Battle half (8262), Determined Challenge (10143). Their inventory notes are updated.

## 4. Move to a destination the ability defines (`MoveDestinations.cs`)

`AbilityDefinition.Move = MoveSpec(Movers, Destination)`. Movers: `target`, `source`, `source-and-target` (Predator), `performer`, `performer-and-target`. Destination: `home`,
`current-battlefield` (no choice), `adjacent-unresolved-battlefield` (Predator) and `unresolved-battlefield` (another unresolved one), the last two a player's choice.
**The player picks with the existing assignment flow's data:** for a chosen destination `AbilityActions` puts the legal battlefields in `ActionOption.Provinces` (the
province indices the `attack`/`assignDefense` options already use, which the client draws with arrows) and one candidate command per battlefield; the command names its
choice in `Province`. `ValidateMove` refuses anything not listed. (`Assignments` was considered as the carrier -- unit to province -- and rejected: the engine already
uses `Province` for every destination an ability names, Shosuro Hawado's included, and both cards that move share ONE destination.)
**Legal battlefields.** A battlefield is identified by the **defender's** province it is fought over, so adjacency follows the **defender's** province line (a destroyed
province slides its neighbours together, `AdjacentProvinces`), never the attacker's own; "unresolved" is a battlefield still to be fought this Attack Phase (a fought one and
one nobody attacked are not offered); the current battlefield is never a destination. With none legal the action is offered disabled.
**The one place adjacency is decided (Mendon's ruling 6).** `AdjacentBattlefields(current)` = `AdjacentProvinces(1 - ActivePlayerId, current)`: the defender's line, at **every** call
site. Two older call sites had taken it from the wrong player's line and are fixed:
`personality-at-adjacent-battlefield` (the fixture `training-adjacent-boost`: "a Personality at an adjacent battlefield") took the *target owner's* line, and Moto Gunso's boost
(`moto-gunso-boost`, oracle-l5r-5320) took his *controller's* line. **Behaviour change:** the lines differ only once provinces have been destroyed on one side, and then only when the card's
owner (or the target's owner) is the *attacker*, whose own line is not the one the battle is fought over; a defender's own line was already the right one. So when the attacker's line
and the defender's differ, "adjacent" for those two now follows the defender's; with equal lines nothing changes. The only published card whose behaviour can change is Moto Gunso; the
category `personality-at-adjacent-battlefield` is used by the fixture alone. Tested on both sites and Predator with the two lines made to differ, for a card owned by the attacker and by the defender
(`AdjacencyIsTheDefendersLine`); the older `ProvinceAdjacency` test now makes the attacker's player 1, so the line it destroys is the defender's.
**Predator.** Both cards go to the chosen battlefield with their attachments (`MoveToBattlefield`), the source **first** and the target last (the arrival a "moved an enemy
Infantry Personality to this battlefield" Reaction watches is the enemy's). Each is a public `unitMovedToBattlefield` event. **Each movement is its own effect and is negatable on its
own (Mendon's ruling 4):** every mover goes through `TryMoveToBattlefield`, which asks `NegatesEffect(card, "move")` for *that* card. If the move of either Predator or the target is
negated (Tamori Tamoko's reaction, a Champion of Thunder's "all", the `training-negate-mover` fixture naming either of them -- a mover-naming reaction category,
`deferred-action-mover`), the OTHER still moves; if both are negated, neither moves; if the **whole action** is negated (Sakuma-shape: `training-negate-action`), neither moves and the
action counts as taken. Tested in the three cases plus both-negated (`EachMoveNegatedSeparately`). *Alternative:* a negated movement cancelling its partner's (rejected by the ruling:
nothing in the text ties them). He needs no target beyond an enemy Personality at the current battlefield (the shared
presence Require puts Predator there too); "opposing Predator" is read as "on the other side at the current battlefield".
**Battle state.** Nothing about a battle is stored per card: `Army()` reads positions when the battle resolves, so the current battle is decided by what is left at it and the moved
units fight the battle at their new battlefield with their Force (attachments included) counted there -- `BattleStateFollowsTheMove` drives both battles to their
`battleResolved` events (Predator 6 + Item 1 + C 1 = 8 against T 3 + Item 1 + E 1 = 5) and shows everyone home at the end.
**Other unwired cards this unblocks** (moving an enemy Personality to the current battlefield, an adjacent or another unresolved battlefield, the performer moving with the
target): Daidoji Takichi (1679, Predator's text word for word), Determined Challenge (10143), Pronouncement of Guilt (6077), The Lesson of Earth (9936), Intimidation (3790,
performer-and-target), Paralyzing Touch's Battle half (9919), Flanking Unit (2598), Befriend the Tides (10708), Shosuro Keirei (9845), Kamalakar (4253); Dislodge the Foe (2024)
also needs multiple performers.

## 5. Stat penalties (`StatPenalties.cs`)

`ApplyModifier` -- where every Force/Chi/Personal Honor/Gold cost modifier is applied -- and `CreateToken` (a stat token) call `NoteStatPenalty(card, source, effect, stat,
amount)`: a negative amount on Force, Chi, `GoldCost` or `PersonalHonor` is a penalty, announced to the registered watchers (`StatPenaltyTraits`, keyed by a card's Effect).
Reducing a Gold cost to zero is `ReduceGoldCostToZero`, a penalty of exactly the cost (a card already at 0 is not reduced). Dishonoring sets a status and never touches a
modifier, so it is not a Personal Honor penalty (`CardPersonalHonor` reads 0 while dishonorable). Positive modifiers are not penalties. Negated modifiers (Champion of Thunder's
"all") are never applied, so they are never announced. Gold cost is now a stat: `CardGoldCost` (printed plus modifiers, at least 0) is read by the two new categories
(`zero-cost-attachment`, `enemy-personality-gold-cost-lte-performer`). Every classification is proven on a fixture watcher that reacts to any penalty of any stat of any card
(`Classification`, `TokenPenalty` -- Cultist Infiltrator's -1C token at the Straighten Phase).
**Liu-Xeung's trait** follows Mendon's ruling: automatic, every time, no window, while bowed; +1F is an ordinary modifier (gone at the end of the turn, like Daidoji Ebizo's). The
trigger is a Force penalty to a **Personality opposing her at a battlefield where she is present** (an enemy of hers -- whoever gave the penalty, even the opponent to his own
Personality -- standing at the same battlefield she is at; never her own side's, never at home, never once she is dead).
**The printed text differs, and Mendon's ruling was followed:** the card says "an action that gave a Force penalty to an enemy **card**" "**After the resolution** of an
action"; the ruling says a **Personality** opposing her, and "any time ... is given a Force penalty", so an enemy **Follower** with a Force penalty gives her nothing here
(`ScopeOfTheTrait`), and the bonus arrives at the moment the modifier is applied, not after the whole action finishes (the only observable difference is a card that both gives a
penalty and reads her Force in the same action). Her Battle half is `enemy-personality-lower-honor-than-self` + move home + gain 1 Honor, exactly Shamate Keep's shape (a dishonorable Liu-Xeung is rehonored
instead of gaining Honor).

## 6. Current stats unless the text says printed or base (Mendon's ruling 2): the audit

**The general reading.** A statistic an ability names is its **current** value -- printed plus every modifier (and, for Chi, an Item's Chi: `PersonalityChi`) -- unless the card text says
"printed" or "base". Applied: Fear Me! and Chokehold (and Ring of Fire, oracle-l5r-6335, the third user of the category) compare current Chi, tested with +1/-1 Chi on either card
(`CurrentChiFlipsTheResult`).
**Audit.** Every place a wired ability, category or effect reads `Definition(...).Force / Chi / PersonalHonor / GoldCost` instead of the live value was checked against its card text:
- **Fixed:** Rumormonger (oracle-l5r-6404, "Force penalty equal to his base Personal Honor") read `CardPersonalHonor`, which is **0 while the target is dishonorable** -- a dishonorable
  target took no penalty at all. It now reads the printed Personal Honor (`RumormongerReadsBasePersonalHonor`: a dishonorable target takes the full penalty). `UnitTotalGoldCost` (the unit
  price a fixture-only path reads) summed printed Gold costs; it now reads `CardGoldCost`, so a Gold cost penalty shows in it.
- **Printed/base by the card's own words, left:** Strength of the Bear (oracle-l5r-10748, "5 or more printed Force"), Charge Into Danger (oracle-l5r-10366, "base Force is 7 or higher"),
  Winds of Dismissal (oracle-l5r-9344, "his base Personal Honor"), Yasuki Tsujiken (oracle-l5r-9469, "base Gold Cost is 4 or lower"), Daigotsu Bofana's trait ("(current) Force ... equal to or
  higher than Bofana's base Force": the performer's current Force against his printed one, as worded).
- **Printed because no modifier can exist:** the `TargetPrinted` Gold-cost basis (Low Market, oracle-l5r-4777; Soshi Neiru, oracle-l5r-7360; the `training-at-any-cost` fixture) prices a card
  in a hand or a discard pile, where it has only its printed cost.
- **Rules, not card text, left printed:** the Honor gain of **Proclaim** (`Definition(card).PersonalHonor`) and the Honor lost when a **dishonorable Personality dies**
  (`GainHonor(owner, -Definition(card).PersonalHonor)`). These are the rulebook's words, not an ability naming a stat; if Mendon reads them as current Personal Honor too (a -1 Personal
  Honor penalty would then reduce a Proclaim), they are one-line changes and the only two left.
No other printed/base comparison was found in the wired abilities.

## Open questions for Mendon

None. The six questions of the first version were answered on PR #348 (see "Rulings on PR #348"). One point is stated for completeness rather than asked: the two rules-not-text sites in
section 6 (Proclaim's gain and a dishonorable death's loss) keep the printed Personal Honor; say if they should read current.

## UI issues for Alfredo (none of the three is built in the client)

Multi-target viewer (#345, updated: it says which slots are up-front and which are conditional follow-ups asked at resolution, and describes the per-target reaction windows), move-destination picker
with arrows (#346, updated: adjacency is the defender's line, each mover's move is negatable), and the "may not perform actions" indicator (#347, updated: Order Seppuku is listed disabled, assignment
is unaffected). Today the client ignores `ActionOption.Slots` and `ActionOption.Provinces` for abilities, so a multi-target or moving action submits the engine's default choice (the first legal
candidate of the up-front slots) -- functional, not chosen; a follow-up decision is a pending `effectChoice` with `chooseEffect` actions, which any client that lists offered actions already shows.

## Merge with origin/main

The branch has merged origin/main three times (never rebased, never force-pushed): #337 alternative costs (8b378a8d), then #340/#344 (05cc2493: either/or choices, Fukuzo and the copy primitives,
Yoritomo Sakuma's whole-action negation, Unfortunate Hesitation's forced pass). Conflicts were in the shared files each time, all resolved by keeping both sides:
- `CardAbilities.cs` -- `AbilityDefinition` gained `Slots`/`Move` beside F's `CopiesAbilities`/`PaymentProduction` (both kept); the option label is one `AbilityOptionLabel(id, ability, performer, source, costOption)` that
  carries the cost-option suffix, the performer suffix and F's " · copied from X"; the new `training-*` fixtures and reaction cases sit beside F's; F's `NegateDeferredAction` is what the per-target windows
  consult (`DeferredActionNegated`).
- `MatchEngine.cs` -- the leave-play sites clear `ActionLocks` with `GrantedAbilities` (F's) and `Modifiers`; `FinishTurn` sweeps both; the `beforeActionReaction` handler opens the next target's window before F's
  negated-action resolution.
- `MatchQueries.cs` / `Models.cs` -- `CardView`'s trailing arguments are `..., GoldSourceProduction(card), ActionLocked(card)` after F's fields; `GameCommand` ends `CostOption`, then `Selections`; `MatchSession.ToWire` and
  `Protocol.cs` map both.
- `L5R.Core.csproj`, `CardCatalog.cs`, `PublishedContent.cs` -- the embedded resources, loaders and the final concat list took both sides' new waves (personality-wave-32 and strategy-wave-21 from this branch; personality-wave-33 and strategy-wave-22 from main).
- `Program.cs` / `PublishedTests.cs` -- both suite registrations kept; the pinned count is **recounted, not summed**: 417 on main (distinct non-ScenarioOnly ids) + 7 = **424**, re-verified from the merged
  `content/published/*.json`.
- `tools/content/build_card_inventory.py` -- two conflicting BLOCKERS hunks: this branch's edits (the seven #336 cards' notes removed, the multi-target/movement/lock notes on the still-blocked cards
  updated) were kept, and the stale note for oracle-l5r-9624 (Yoritomo Sakuma, wired on main by #344) dropped from them; the workbook was regenerated (424 wired).
Re-run after the merge: `bash tools/check.sh --skip-client`, and the mutation checks below on the merged tree (including the ones touching F's areas: a copied ability exempt from the lock, whole-action negation ignored).
Lock and F's copy primitive: an ability a locked Fukuzo copied is barred (`CopiedAbilityIsBarred`, mutation-checked).

## Validation

`bash tools/check.sh --skip-client` on the merged tree, all green: the Python tool suites (43, 130), 216 core, 57 published, `gold-parity` 18, `gold-quote` 20, `battle-timing` 23, `recon-action` 11,
`discipline-absent` 15, `deck-format` 9, `deck-workshop` 10, `deck-search` 22, `deck-look` 15, `daigotsu-bofana` 19, `alternative-costs` 12, `attach-from-discard` 14, `fading-shadows` 9, `effect-choice` 10,
`follower-performer` 8, `card-copy` 15, `either-or` 17 and the four suites of this change -- `action-lock` 14, `multi-target` 21, `move-destination` 10, `stat-penalty` 10 -- then `L5R.AI.Tests` 25,
`L5R.Server.Tests` and `L5R.Client.Transport.Tests` 7 (the new command and option fields cross the wire). `dotnet build src/L5R.Client/L5R.Client.csproj` builds (the hand-resolved `Selections`
mapping in `MatchSession.ToWire`); Godot is not available on this machine, so the client's import/smoke is CI's. Pinned published count **424**: the distinct non-ScenarioOnly ids across `content/published/*.json`
of the merged tree (origin/main 05cc2493 has 417, after #337 and #344; plus these seven). Inventory workbook regenerated (424 wired).

## Mutation checks

Each mutation was applied to the engine source (or the published JSON) alone, the suites rebuilt and run, and the source restored; the runs are throwaway scripts, not committed. Run on the **merged**
tree, after the six rulings and the innate-action ruling: the new mutations (locks, current/base stats, follow-ups, per-mover negation, per-target windows, adjacency) run against the suites of this change
plus `published`, `gold-`, `daigotsu`, `card-copy`, `either-or`, `effect-choice`, `follower-performer`, `battle-timing` and the older core tests that touch them; the mutations of the first version of this document
that still apply were re-run the same way (three of them changed shape with the rulings: the gain/lose ability pair and the "player default" of a slot no longer exist, and the Yoshitada/Fear Me! condition
mutations are now the follow-up ones). Two mutations first survived and led to tests: "window state survives the action" (now asserted in `OneWindowPerTarget`) and "unit total Gold cost reads printed costs"
(`UnitTotalGoldCostIsCurrent`); "player slot not required" survived once no shipped card had an up-front player slot and is now killed through the `training-slots-mixed` fixture.

| Mutation | Result |
|---|---|
| lock ignores the host of an attachment | `LockBlocksEveryKindOfAction`, `LockedCanBeTargeted`, `LockBlocksReactions` |
| lock ignores the performer a Strategy names | `LockBlocksEveryKindOfAction` |
| lock never refuses | `LockBlocksEveryKindOfAction`, `LockBlocksReactions`, `LockLastsUntilTurnEnd`, `AssignmentIsNotAnAction`, `CopiedAbilityIsBarred` |
| a copied ability is exempt from the lock | `CopiedAbilityIsBarred` |
| turn end does not clear the lock | `LockLastsUntilTurnEnd`, `LockedOnTheOpponentsTurn` |
| a Reaction listing ignores the lock | `LockBlocksReactions` |
| Order Seppuku not refused for a locked Personality | `InnateActionSeppukuIsBarred` |
| Order Seppuku listed enabled for a locked Personality | `InnateActionSeppukuIsBarred` |
| assigning is refused for a locked Personality (engine) | `LockLastsUntilTurnEnd`, `LockedOnTheOpponentsTurn`, `AssignmentIsNotAnAction` |
| a locked Personality is not offered in the assignment | `LockLastsUntilTurnEnd`, `AssignmentIsNotAnAction` |
| Chi comparison reads printed Chi | `CurrentChiFlipsTheResult` |
| Rumormonger reads current Personal Honor | `RumormongerReadsBasePersonalHonor` |
| unit total Gold cost reads printed costs | `UnitTotalGoldCostIsCurrent` |
| follow-up asked although the target is not dishonorable | `YoshitadaBowsThenAsksOnlyIfDishonorable` |
| follow-up asked although he did not move | `FearMeMoves` |
| a follow-up slot may be chosen up front | `YoshitadaValidation` |
| Predator's own move ignores a negation | `MovesBothToTheChosenBattlefield`, `EachMoveNegatedSeparately`, `EveryRuleAndMover` |
| the target's negated move stops Predator too | `EachMoveNegatedSeparately` |
| Predator's negated move stops the target too | `EachMoveNegatedSeparately` |
| a whole-action negation is ignored | `PoliticalReactionNegated`, `CannotPayAutoNegates`, `NegatedActionIsStillTaken`, `ReactionsThenBowThenDecision`, `PerTargetWindowsAlternate`, `EachMoveNegatedSeparately` |
| windows collapse to the first target | `OneWindowPerTarget`, `PerTargetWindowsAlternate` |
| window opened for a target nobody can react to | `FollowerWave3Cards`, `KeywordMechanics`, `SuitengusPathNavalGrant`, `ExpendableFollowers`, `Shinden`, `ShindenHonor`, and 104 more |
| reactions judged on the command's own target | `OneWindowPerTarget`, `PerTargetWindowsAlternate` |
| the later windows are never opened | `OneWindowPerTarget`, `PerTargetWindowsAlternate` |
| a negated action still opens the later windows | `PerTargetWindowsAlternate` |
| window state survives the action | `OneWindowPerTarget` |
| adjacency along the attacker line | `ProvinceAdjacency`, `LegalDestinationRules`, `DefenderUsesPredator`, `AdjacencyIsTheDefendersLine` |
| adjacent-battlefield target uses its owner's line | `AdjacencyIsTheDefendersLine` |
| Moto Gunso uses his controller's line | `AdjacencyIsTheDefendersLine` |
| the current battlefield is a destination | `MovesBothToTheChosenBattlefield`, `LegalDestinationRules`, `Validation`, `DefenderUsesPredator`, `EveryRuleAndMover`, `AdjacencyIsTheDefendersLine` |
| entry discount is always 1 | `LionClanDiscountThroughQuotePurchase`, `ClanDiscountIsGeneral` |
| entry discount for any Clan | `LionClanDiscountThroughQuotePurchase`, `ClanDiscountIsGeneral` |
| purchase quote has no entry discount | `LionClanDiscountThroughQuotePurchase` |
| slot count maximum unchecked | `YoshitadaValidation`, `BondsOfCoinDestroys`, `UpToThreeIncludingNone` |
| slot candidates judged as any card | `YoshitadaValidation`, `YoshitadaOffered`, `BondsOfCoinDestroys`, `GoldAndSteelDestroys`, `FearMeMoves`, `CurrentChiFlipsTheResult`, and 4 more |
| player slot not required | `MixedSlots` |
| command not normalized (no TargetId) | `YoshitadaBowsThenAsksOnlyIfDishonorable`, `SentencesAreIndependent`, `YoshitadaRehonorRule`, `YoshitadaOffered`, `TheDecisionIsAnsweredOnlyByTheActingPlayer`, `ReactionsThenBowThenDecision`, and 3 more |
| targeting readers see only TargetId | `BofanaSeesSlotTarget` |
| hand slot lists every hand | **survived** (equivalent, see below) |
| destination need not be unresolved | `LegalDestinationRules`, `Validation` |
| Predator does not move himself | `MovesBothToTheChosenBattlefield`, `DefenderUsesPredator`, `EachMoveNegatedSeparately`, `BattleStateFollowsTheMove`, `EveryRuleAndMover`, `OfferedDestinationsValidate` |
| destination not validated | `LegalDestinationRules`, `Validation`, `EveryRuleAndMover` |
| a named battlefield accepted for a move that names none | `EveryRuleAndMover` |
| only Force is a penalty stat | `Classification`, `TokenPenalty` |
| Liu-Xeung trait not location-scoped | `ScopeOfTheTrait` |
| Liu-Xeung trait counts Followers | **survived** (equivalent, see below) |
| ApplyModifier announces no penalty | `TraitFires`, `EveryTimeAndForOneTurn`, `Classification` |
| a stat token announces no penalty | `TokenPenalty` |
| a zero cost is reduced again | **survived** (equivalent, see below) |
| a bonus is a penalty | `ScopeOfTheTrait` |
| the trait bonus does not stack | `EveryTimeAndForOneTurn` |
| zero-cost attachment uses the printed cost | `BondsOfCoinDestroys`, `GoldAndSteelDestroys`, `GoldCostIsReal` |
| Gold and Steel: lower only | `GoldAndSteelBows`, `GoldCostIsReal` |
| Bonds of Coin does not bow the Stronghold | `BondsOfCoinDraws` |
| Bonds of Coin needs no Imperial Favor | `BondsOfCoinDraws` |
| conflicting TargetId accepted | `YoshitadaValidation` |
| duplicate cards in a slot accepted | `BondsOfCoinDestroys` |
| Hotako Honor loss missing (packaging) | `PublishedShape`, `LionClanDiscountThroughQuotePurchase` |

55 of 58 mutations are killed.

The three survivors are equivalent mutants, not gaps: (1) "hand slot lists every hand" -- the candidate list is a superset that `ValidateSlotCard` filters by each category's own ownership rule, so the
visible options are identical (the privacy test checks the serialized options); (2) "Liu-Xeung trait counts Followers" -- a Follower is `Zone.Attached`, so the "at a battlefield where she is present" clause
already excludes it and the Personality test is belt and braces; (3) "a zero cost is reduced again" -- a `-0` modifier is not a penalty (the amount is not negative), so nothing observable changes.
"Window opened for a target nobody can react to" is killed by a large part of the core suite as well as by `OneWindowPerTarget` (every action with a reactor in play opens extra windows), which is expected.

## Affected files

New: `src/L5R.Core/ActionLock.cs`, `TargetSlots.cs`, `TargetWindows.cs`, `MoveDestinations.cs`, `StatPenalties.cs`, `ClanEntryDiscount.cs`; `content/published/personality-wave-32*.json`, `strategy-wave-21*.json` and
the seven art files; `tools/content/package_personality_wave_32.py`, `package_strategy_wave_21.py`; `tests/L5R.Core.Tests/ActionLockTests.cs`, `MultiTargetTests.cs`, `MoveDestinationTests.cs`,
`StatPenaltyTests.cs`, `Batch336Support.cs`.
Changed: `EffectChoice.cs` (the follow-up conditions and `HonorChoice`, `BeginConditionalChoice`'s `targetMoved`), `CardAbilities.cs` (the abilities and effect cases, the per-window `BeforeActionReactionApplies`, `deferred-action-mover`, `AbilityDefinition.Slots`/`Move`, `ValidateAbility`'s lock/slot/move hooks and target categories, `UseAbility`, `ApplyModifier`/`CreateToken`
hooks, `AbilityActions`' slot and destination offers and the extracted `AbilityOptionLabel`, the Favor lists, nineteen `training-*` abilities), `Models.cs` (`ActionLock`, `CardInstance.ActionLocks`,
`GameCommand.Selections`, `ActionOption.Slots`, `CardView.ActionLocked`), `MatchEngine.cs` (`FinishTurn`, overlay transfer, `QuotePurchase`), `GoldQuotes.cs` (`EquipGoldCost`), `MatchQueries.cs`
(the view flag, `AddReaction`), `DaigotsuBofana.cs` (`ActionTargetIds`), `CardCatalog.cs` (fourteen `training-*` fixture cards), `PublishedContent.cs`, `L5R.Core.csproj`, `src/L5R.Server/Protocol.cs` and
`src/L5R.Client/MatchSession.cs` (the two new wire fields), `tools/content/build_card_inventory.py` and `docs/CARD_INVENTORY.xlsx`, `BattleTimingTests.cs`/`ReconActionTests.cs` (the Recon-tag inventories),
`PublishedTests.cs` (count), the Server and Transport tests (the wire samples).
