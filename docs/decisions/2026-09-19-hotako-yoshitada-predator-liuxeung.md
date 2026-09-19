# Hotako exp3, Yoshitada, Sea Troll Predator, Liu-Xeung exp, Bonds of Coin, Gold and Steel, Fear Me! (personality-wave-32, strategy-wave-21)

Status: implemented; the readings marked **Mendon** are his rulings, quoted from the task brief of 2026-09-19. Every other reading is a decision of
this change, recorded with its alternative, and the ones still needing his eye are listed under "Open questions". Lane: rules (mdornbrook;
cross-lane edits under his standing authority). Issue: #336. UI follow-ups for Alfredo are separate issues (below).

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
with a 0 Gold Cost" -- the same action. Published count 409 -> 416.

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
*What is not blocked:* traits and mandatory effects (they are not actions: Daidoji Ebizo's +1F, Bofana's discard, Liu-Xeung's +1F still work on a locked
Personality); being targeted, bowed, moved, destroyed; actions that are the *player's* rather than a card's (Equip, Recruit, Proclaim, Order Seppuku, the
Imperial Favor's rulebook actions, a Strategy played with `playStrategy`, which names no performer).

**Duration: "this turn" and "reset during the End phase" coincide.** The lock lasts until `FinishTurn`, which runs at the very end of the End phase (after the
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

**Shape.** An ability with `Slots` (Target `"slots"`) names an ordered list of `TargetSlot(Id, Category, Label, Min, Max, MustBe, PlayerDefault)`: `Category` is any
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
Bofana triggers when a *second* card slot names him (`BofanaSeesSlotTarget`). **Limit:** the before-action reactions (Rokai, Tamoko, Champion of Thunder, Brothers of
Goemon, Kakihara) still see only the primary target of a multi-target action; making them read all targets is a follow-up for whoever wires a card that needs it.
*Alternatives for the representation:* extra typed fields on `GameCommand` (`TargetId` + `TargetPlayerId` + `CardIds` -- no way to say which is which for two card
slots), or a `Dictionary<string,string[]>` (stringly, no player kind). Rejected for `SlotSelection`.

**Yoshitada.** Slots: `target` (`enemy-personality`) and `player`. "Gains or loses" is the pair `yoshitada-bow-gain` / `yoshitada-bow-lose` sharing one UsageKey (Kitsuki
Horume's established shape), so the acting player chooses the direction by which ability he plays. The Honor change is conditional through the existing
`IfTarget` status (`TargetStatus.IsDishonorable`), judged **after the bow**, per qualifying target (a later multi-target card can fire it several times), by
`EffectConditionMet`. A gain for his own controller while he is dishonorable rehonors him instead (`ApplyPlayerHonorChoice`, Doji Shunya's precedent; a loss, or a gain
for the other player, is plain). *Alternatives:* (a) a third `direction` slot (a choice kind) instead of two abilities -- cleaner offer, but a new slot kind for one
card family and an odd UI; (b) choosing the player only **at resolution**, and only if the target is dishonorable -- fewer pointless prompts, but Mendon's brief is that
the activating player selects both targets up front, and "choose" at resolution has no other consumer here. The player slot is therefore always required (1..1), even
against an honorable target, where the choice is simply unused. **Fear Me!** is the same shape (`fear-me-gain` / `fear-me-lose`, a performing unbowed Duelist, `enemy-personality-chi-lte-performer`, which compares
**printed** Chi exactly as Chokehold's identical clause already does -- flagged below), with the Honor change only if the target actually moved (a negated move changes nobody's
Honor), and the performing Duelist, not the Strategy, as the performer for the rehonor rule.

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
one nobody attacked are not offered); the current battlefield is never a destination. With none legal the action is offered disabled. *Observation (not changed):* two older
call sites take adjacency from *their own* player's line (`personality-at-adjacent-battlefield` uses `target.OwnerId`, Moto Gunso's boost `player.Id`); the two lines
only differ once provinces have been destroyed on one side, and nothing in this change depends on them -- worth a look by whoever owns those cards.
**Predator.** Both cards go to the chosen battlefield with their attachments (`MoveToBattlefield`), the source **first** and the target last (the arrival a "moved an enemy
Infantry Personality to this battlefield" Reaction watches is the enemy's). Each is a public `unitMovedToBattlefield` event. **If the target's movement is negated (Tamori
Tamoko) only the target stays; Predator still goes** -- the two movements are separate effects, and a source moving itself is not something a reaction names. *Alternative:*
a negated target cancels the whole action (rejected: nothing in the text ties them). He needs no target beyond an enemy Personality at the current battlefield (the shared
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

## Open questions for Mendon

1. **Duration of the lock:** "this turn" and "reset during the End phase" coincide only because the engine offers no action in the End phase. If an End-phase action or Reaction is ever
   added, does the lock still block it (the current reading: it lasts until the turn ends), or does it end when the End phase begins?
2. **Fear Me! compares printed Chi** (it reuses Chokehold's category `enemy-personality-chi-lte-performer`, which reads `Definition(...).Chi`), so a Chi modifier or an Item's Chi does not
   change who is a legal target. Should "equal or lower Chi" compare current Chi (as the Liu-Xeung ruling reads Force penalties as current statistics)? It would change Chokehold and Ring of Fire
   too.
3. **The player slot is required even against an honorable Yoshitada target.** Is that acceptable UX, or should the player be asked only when the target is dishonorable (the alternative above)?
4. **A negated target movement (Predator):** Predator still moves. Confirm.
5. **Before-action reactions read only the primary target of a multi-target action** (Rokai, Tamoko, Champion of Thunder, Brothers of Goemon, Kakihara). Fine until a card needs otherwise?
6. **Liu-Xeung and Followers:** the printed "enemy card" would count a Follower with a Force penalty; the ruling says Personality. Followed the ruling.

## UI issues for Alfredo (none of the three is built in the client)

Multi-target viewer, move-destination picker with arrows, and the "may not perform actions" indicator: see the issues linked from the PR. Today the client ignores `ActionOption.Slots` and
`ActionOption.Provinces` for abilities, so a multi-target or moving action submits the engine's default choice (the first legal candidate) -- functional, not chosen.

## Validation

`dotnet run --project tests/L5R.Core.Tests` (216 core, 57 published, and the new suites: `action-lock` 10, `multi-target` 16, `move-destination` 9, `stat-penalty` 8),
`L5R.AI.Tests`, `L5R.Server.Tests` and `L5R.Client.Transport.Tests` (the new command and option fields cross the wire), `bash tools/check.sh --skip-client`. Pinned published count
409 -> 416: recounted as the distinct non-ScenarioOnly ids across `content/published/*.json` on origin/main 7f306655 (409) plus these seven. Inventory workbook regenerated (416 wired).

## Mutation checks

Each mutation was applied to the engine source (or the published JSON) alone, the suites rebuilt and run (the four new suites plus `published`, `gold-` and `daigotsu`), and the source
restored; the run is a throwaway script, not committed.

| Mutation | Result |
|---|---|
| lock ignores the host of an attachment | `LockBlocksEveryKindOfAction`, `LockedCanBeTargeted`, `LockBlocksReactions` |
| lock ignores the performer a Strategy names | `LockBlocksEveryKindOfAction` |
| lock never refuses | `LockBlocksEveryKindOfAction`, `LockBlocksReactions`, `LockLastsUntilTurnEnd` |
| turn end does not clear the lock | `LockLastsUntilTurnEnd`, `LockedOnTheOpponentsTurn` |
| a Reaction listing ignores the lock | `LockBlocksReactions` |
| entry discount is always 1 | `LionClanDiscountThroughQuotePurchase`, `ClanDiscountIsGeneral` |
| entry discount for any Clan | `LionClanDiscountThroughQuotePurchase`, `ClanDiscountIsGeneral` |
| purchase quote has no entry discount | `LionClanDiscountThroughQuotePurchase` |
| slot count maximum unchecked | `YoshitadaValidation`, `BondsOfCoinDestroys`, `UpToThreeIncludingNone` |
| slot candidates judged as any card | `YoshitadaValidation`, `YoshitadaOffered`, `EveryOfferedCombination`, `BondsOfCoinDestroys`, `GoldAndSteelDestroys`, `FearMeMoves`, `MixedSlots`, `UpToThreeIncludingNone`, `HandSlotIsPrivate`, `GoldCostIsReal` |
| player slot not required | `YoshitadaValidation` |
| command not normalized (no TargetId) | `YoshitadaResolves`, `YoshitadaRehonorRule`, `YoshitadaOffered`, `EveryOfferedCombination`, `BofanaSeesSlotTarget`, `FearMeMoves`, `SelectionsSurviveJson` |
| targeting readers see only TargetId | `BofanaSeesSlotTarget` |
| hand slot lists every hand | **survived** (equivalent, see below) |
| default player is always the actor | `YoshitadaOffered` |
| Yoshitada condition ignored | `YoshitadaResolves` |
| Yoshitada gain and loss swapped | `YoshitadaResolves`, `YoshitadaRehonorRule`, `SelectionsSurviveJson` |
| adjacency along the attacker line | `LegalDestinationRules`, `DefenderUsesPredator` |
| destination need not be unresolved | `LegalDestinationRules`, `Validation` |
| Predator does not move himself | `MovesBothToTheChosenBattlefield`, `DefenderUsesPredator`, `NegatedTargetMove`, `BattleStateFollowsTheMove`, `EveryRuleAndMover`, `OfferedDestinationsValidate` |
| a negated target move still happens | `NegatedTargetMove` |
| the current battlefield is a destination | `MovesBothToTheChosenBattlefield`, `LegalDestinationRules`, `Validation`, `DefenderUsesPredator`, `EveryRuleAndMover` |
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
| Fear Me! Honor change even when not moved | `FearMeMoves` |
| Yoshitada directions do not share a use | `PublishedShape`, `YoshitadaResolves` |
| conflicting TargetId accepted | `YoshitadaValidation` |
| duplicate cards in a slot accepted | `BondsOfCoinDestroys` |
| Hotako Honor loss missing (packaging) | `PublishedShape`, `LionClanDiscountThroughQuotePurchase` |

38 of 41 mutations are killed. The three survivors are equivalent mutants, not gaps: (1) "hand slot lists every hand" -- the candidate list is a superset that
`ValidateSlotCard` filters by each category's own ownership rule, so the visible options are identical (the privacy test checks the serialized options); (2) "Liu-Xeung trait counts
Followers" -- a Follower is `Zone.Attached`, so the "at a battlefield where she is present" clause already excludes it and the Personality test is belt and braces; (3) "a zero cost is reduced
again" -- a `-0` modifier is not a penalty (the amount is not negative), so nothing observable changes.

## Affected files

New: `src/L5R.Core/ActionLock.cs`, `TargetSlots.cs`, `MoveDestinations.cs`, `StatPenalties.cs`, `ClanEntryDiscount.cs`; `content/published/personality-wave-32*.json`, `strategy-wave-21*.json` and
the seven art files; `tools/content/package_personality_wave_32.py`, `package_strategy_wave_21.py`; `tests/L5R.Core.Tests/ActionLockTests.cs`, `MultiTargetTests.cs`, `MoveDestinationTests.cs`,
`StatPenaltyTests.cs`, `Batch336Support.cs`.
Changed: `CardAbilities.cs` (the abilities and effect cases, `AbilityDefinition.Slots`/`Move`, `ValidateAbility`'s lock/slot/move hooks and target categories, `UseAbility`, `ApplyModifier`/`CreateToken`
hooks, `AbilityActions`' slot and destination offers and the extracted `AbilityOptionLabel`, the Favor lists, nineteen `training-*` abilities), `Models.cs` (`ActionLock`, `CardInstance.ActionLocks`,
`GameCommand.Selections`, `ActionOption.Slots`, `CardView.ActionLocked`), `MatchEngine.cs` (`FinishTurn`, overlay transfer, `QuotePurchase`), `GoldQuotes.cs` (`EquipGoldCost`), `MatchQueries.cs`
(the view flag, `AddReaction`), `DaigotsuBofana.cs` (`ActionTargetIds`), `CardCatalog.cs` (fourteen `training-*` fixture cards), `PublishedContent.cs`, `L5R.Core.csproj`, `src/L5R.Server/Protocol.cs` and
`src/L5R.Client/MatchSession.cs` (the two new wire fields), `tools/content/build_card_inventory.py` and `docs/CARD_INVENTORY.xlsx`, `BattleTimingTests.cs`/`ReconActionTests.cs` (the Recon-tag inventories),
`PublishedTests.cs` (count), the Server and Transport tests (the wire samples).
