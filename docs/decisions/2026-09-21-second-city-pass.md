# Second City pass: the remaining unwired cards re-checked (issue #264)

Requested by Mendon: review the Second City cards still unwired, wire what can be wired, and review and update the blockers of the rest. Lane: rules. Status: readings proposed, awaiting Mendon's confirmation (the list below). Issue: #264. Affected files: `src/L5R.Core` (CardAbilities, AbilityCosts, AlternativeCosts, AbilityGrants, ActionLock, DelayedDestruction, ForcePenalties, TargetDestruction, TargetedActionReactions, EffectChoice, DeckLook, Dueling, GoldQuotes, MatchEngine, Models), `content/published` (nine waves), `tools/content/build_card_inventory.py`.

At the start of the pass **52 of the 162 Second City cards were unwired**. Every one was checked against the engine as it now stands, not against the notes written when it was first triaged: several of those notes named gaps that later work has closed (Open-timed keyword Performers, the look primitive #322, action locks and Gold-cost stat changes #336, alternative costs #337, created cards and copies #387/#399, permanent grants #388). **29 cards are wired here** (24 in the first pass, five more after Mendon's rulings the same day), and Basic Lesson by #412. The other 22 keep an updated, specific blocker in `tools/content/build_card_inventory.py`, and the inventory workbook is regenerated from it.

## Wired

| Wave | Cards |
|---|---|
| strategy-wave-33 | Spirit of the Berserker, Duelist, Paragon, Scout, Yojimbo |
| strategy-wave-34 | Interference |
| strategy-wave-35 | Great Sacrifice, On the Hunt, Tireless Efforts, Costly Opportunity, Silent Terror, Goju Plot, Discovering a Conspiracy, Insulting Gesture, Barred Passage; after the rulings: A Fugitive Apprehended, Dislodge the Foe, Know No Fear, To First Blood |
| spell-wave-12 | Control Your Destiny, Katana of Fire, Taming the Beast |
| spell-wave-13 | Rage of the Spirits, Essence of Undeath |
| item-wave-7 | Flame Katana (the Item Katana of Fire creates; created-only) |
| celestial-wave-3 | Jurojin's Blessing |
| personality-wave-42 | Yoritomo Emoto, Doji Dainagon |
| stronghold-wave-10 | Kyuden Kitsune |

Doji Dainagon's selected printing is the Promotional-Emperor one (printing 2); the scan is its own receipt's.

**Scan reading.** Every one of the 29 scans was read against the catalogue text. The rules are the same everywhere; the wording differs on five, and the published `Text` keeps the catalogue's: Interference (the scan prints "Open: Target a Stronghold: Its abilities may not be used. If it is your turn, bow it." with Political on the keyword line), Costly Opportunity ("Destroy your performing unbowed Samurai and target an enemy card without attachments"), Insulting Gesture (the Favor half prints no "Favor" tag: "Battle: Discard the Imperial Favor and target an enemy Personality"), Barred Passage ("...and target an enemy unit: Bow it" with Heroic on the keyword line) and Doji Dainagon ("You may lobby as an Open action if Dainagon performs it.").

## New primitives (each small, each with its own test)

- **Permanent stat modifiers.** `AppliedModifier.Permanent`: `FinishTurn` clears every modifier except these, and they are reset with the card when it goes back to a hand or deck. Spirit of the Berserker's "permanently give him +2F". Existing lifecycle: like a permanent keyword, it stays through death.
- **Delayed destruction** (`DelayedDestruction.cs`). "Delay his destruction from the action's effects until before this turn ends": a per-card negation kind consumed by the first destruction the held action would inflict (`DestroyUnits`, `DestroyAttachment`); the card is put in `MatchState.PendingDelayedDestructions` and destroyed at the top of `EndTurn` if still in play (Overconfident's draw, if it opens, is answered first and the player ends the phase again).
- **Wielder as a cost subject** (`CostSubject.Wielder`) and **"unless he is [keyword]"** (`ActionCost.UnlessKeyword`, read against the source or the Personality an attached source is attached to): "Bow this Shugenja", "Bow this Shugenja unless he is Void", "Bow this card unless this Personality is Fire". Declared costs, so a negated action still pays them; `ValidateWielderCosts` requires the wielder unbowed unless exempt. `Sacrifice` of an attachment now goes through `DestroyAttachment` (as the alternative-cost destroy always did), so a Spell destroyed as a cost detaches and loses its grants.
- **`SourceUnitMayBeAway`** ("Even if this unit is not at the current battlefield"): waives only the two source-at-the-battlefield checks; the acting player still needs a unit there and the target and performer keep their own location rules.
- **A locked Stronghold refuses every ability printed on it** (`ActionLock.cs`): the action lock (#336) barred an actor; a Stronghold, Holding, Region or Celestial performs nothing itself, so a lock on it bars its abilities whoever they name as performer (Shinden Asahina's is performed by a Shugenja).
- **Force penalty negation** (`ForcePenalties.cs`): the negative Force modifiers on a card are removed and, for the turn, `ApplyModifier` and `CreateToken` apply none (`CardInstance.ForcePenaltiesNegatedTurn`).
- **A card in the current battlefield's province** (`BranchCondition.ProvinceHoldsCard`, `BranchEffect.DiscardProvinceCard`) and **the target at a battlefield** (`BranchCondition.TargetAtBattlefield`) for the conditional choice (`EffectChoice.cs`).
- **Recruit price less the performer's Gold Cost** (`GoldCostRule.TargetRecruit(minusPerformerCost: true)`), and the dead-pile recruit (`QuotePurchase(fromDeadPile)`) now accepts a Personality merely discarded from a province (`Zone.DynastyDiscard`): "in your discard pile". At Any Cost still targets only its own dead Samurai, so nothing there changed.
- **Performer and Target categories**: `own-wielder`, `own-samurai-with-attachment`, `own-shadowlands-ninja-at-battle`, `own-unbowed-follower-at-battle`; `stronghold`, `own-unit-card-with-keyword`, `enemy-personality-force-or-honor-lte-self`, `enemy-unit-within-performer-sacrifice`, `enemy-force-lte-performer`, `own-dead-personality`, `own-ninja-personality-in-discard`, `own-nonunique-strategy-in-discard`.
- **Reactions played from a hand** (Mendon: yes). The targeted-by-action window now takes reactors in a hand as well as in play (`TargetedActionReactionCandidates`), so it opens for a reactor holding a matching card. The opponent's view never lists the card; only the timing of the window says so, as for Unimpeachable Name. Know No Fear and To First Blood use it.
- **A duel carries its action's negations** (`PendingDuel.NegatedEffects`): a Reaction that negates "his destruction from the action's effects" against an Iaijutsu duel stands through the duel to its outcome, and To First Blood's Focus Effect negates the destruction of every loser before the outcome applies. The three older Iaijutsu duels (Daisuke, Ichizo exp, Shikei) now carry `ActionKeywords ["Iaijutsu"]`.
- **Performing slots** (`TargetSlot.Performing`; Mendon: "N Tacticians may be designated as performers"): the cards of such a slot perform the action and are not its targets -- no reaction window opens for them, they are not the primary target, a lock on one bars the action, and `MoveSpec.PerformingSlot` moves them. The slot categories can read the cards chosen in another slot (the selections stay on the command), so Dislodge the Foe's opposing Personality is judged where the chosen Tacticians stand.
- **Kyuden Kitsune's movement negation** (`KitsuneNegation.cs`): `MoveToBattlefield` (which every printed "move to a battlefield" effect funnels through) asks whether the Defender controls a card with the Effect; the first movement of an Attacker's card into a battle at its province is negated once per battle, unless the Defender takes the action. The card stays where it was; the few effects that go on to straighten it regardless are unchanged.
- **Iaijutsu is read from the ability's own `ActionKeywords`** as well as the old id list (`IsIaijutsuAction`), so a granted duel is an Iaijutsu action.

## Readings and assumptions (Mendon to confirm or correct; each is one line to change)

1. **Spirit of the Berserker.** +2F is permanent and only on the ability branch (the keyword branch gives none). The reaction is offered when another player's action targets him and could destroy him (an Attack always could; a Personality-destroying ability is on the `DestroysTarget` list); only the destruction the held action would inflict is delayed, every other effect applies.
2. **Interference.** "May not be used" lasts this turn (the engine's default for an effect with no duration); it bars the Stronghold's printed abilities, not its Gold production or a player ability. "If it is your turn, bow it" is judged when it resolves.
3. **Control Your Destiny.** "You may put one or two ... on the bottom" is zero, one or two (the same option the other looks give). The deck is the player's own Fate or Dynasty deck.
4. **Katana of Fire / Flame Katana.** Once per game. A bowed Katana cannot be used even by a Fire wielder (abilities on a bowed card may not be used); a Fire wielder simply does not bow it.
5. **Taming the Beast.** "Your defending Personality" is a Personality of its controller at the battlefield while its controller is not the active player. "If this Shugenja is Earth" is checked when the reaction is offered. Its action's target must be a Personality or Follower of the acting player's carrying Nonhuman.
6. **Tireless Efforts.** "Negate all current and new Force penalties" lasts the turn; it removes modifiers, not other stats' penalties, and a bonus stays.
7. **Costly Opportunity.** "Abilities on other cards now in its unit" is the unit of the card it targets (the Samurai it destroys takes his own unit with him); the lock lasts the turn. For a bare Personality target the unit has no other card.
8. **Silent Terror.** "A card in the current battlefield's province" is the Dynasty card in the Defender's province at that battlefield, face up or down (the discard pile is public); the province is refilled as after any discard. Asked only when a card is there.
9. **Essence of Undeath.** The Shugenja is the performer (so its location is known after the Spell is destroyed as the cost); the Personality is paid for at the dead-pile recruit price, enters at home or at the battlefield his Shugenja stands at, may not be proclaimed (as At Any Cost), and its controller loses 2 Honor.
10. **Goju Plot.** "In your discard pile" is dead or discarded from a province; the price is the recruit price less the performer's current Gold Cost, to a minimum of 0; the additional Battle action is granted even when an entry window opens.
11. **Great Sacrifice.** Total Force is the unit's battle contribution (Personality and attachments); the reach (Force + Personal Honor + 1 if defending) is read as the action is announced, before the performer is destroyed as the cost.
12. **On the Hunt.** "4 less Gold" is to a minimum of 0. Its Open half needs a Samurai with an attachment, at any location and in any bow state.
13. **Discovering a Conspiracy.** "Clan Alignment keywords" are his Clan affiliations and the printed keywords that name them ("Crane Clan"); he becomes Unaligned. The honor question is asked only when he is at a battlefield (as Yoshitada's is). The Focus Effect pays only when its controller's Personality won.
14. **Insulting Gesture.** Order: dishonor, bow, move home (bowed, as Jakuei's), then the controller's 2 Honor loss.
15. **Doji Dainagon.** "Dainagon may Lobby as an Open action" is Lobby with Dainagon as the Personality who bows: the same Family Honor condition, at least 1 Personal Honor, an Open action, and it does not use up the Limited Lobby.
16. **Barred Passage.** "Bow his unit" bows the Personality and every card attached to him. "The only Personality in his army" counts the acting player's Personalities at this battlefield.
17. **Yoritomo Emoto.** The performing Follower is any unbowed Follower of the acting player's at the battlefield, not only one in Emoto's unit (Tensin's category is the unit one).

18. **A Fugitive Apprehended** (Mendon). The performer is "your performing unbowed Personality"; the first target (an enemy card without attachments and with a lower current Gold Cost than the performer's) is bowed; only a Magistrate may name the second, optional, enemy card, whose Gold Cost becomes 0 (a modifier of exactly its current cost, for the turn). The second target never makes the first legal.
19. **Dislodge the Foe** (Mendon). The Tacticians (any unbowed or bowed Tactician of the acting player's at one battlefield other than the current one) perform it; the one target is an enemy Personality at that same battlefield; the Tacticians move to the current battlefield (each movement negatable on its own); the acting player needs no unit at the current battlefield.
20. **Know No Fear.** The "performing Samurai" is the Samurai the action targets. The first Reaction answers an action that bows or moves him (a curated list, as every bow and move classification is); the second answers any Fear action and negates it as a whole.
21. **To First Blood.** The Reaction answers another player's Iaijutsu action against a Personality of its controller's. The Focus Effect applies to the loser or losers of the duel whoever focused it, and pays the winner's controller.
22. **Kyuden Kitsune.** "Your forests are maddening" is flavor (Mendon). The negation is once per battle (turn and province), for an Attacker's card only, and not for the Defender's own action; the Limited action bows an unbowed Forest of the acting player's (the Stronghold itself has the keyword).

## Cards not wired, and what each needs

The blocker notes in `build_card_inventory.py` are the record; the families are:

- **The other player decides inside your action:** Dragon's Breath Castle, Know Their Minds, Scandalous Rumors.
- **Windows after a battle's resolution or end:** Redoubled Attack, Tales of Dishonor, Never Safe.
- **Locks that span turns:** Hibernation, Visitation, A Quiet Transaction, Strength of the Tsunami.
- **Terrain statics:** Ancient Feud (a province strength cap), Hasty Fortifications (a stacking, removable province bonus and an after-every-Battle-action trait), Shiotome Plains (Cavalry and Infantry Force).
- **Single gaps, each with a card of its own:** Blessings of the Shi-Tien Yen-Wang, Disreputable Deal (a choice inside a Focus Effect), Engulfing Flames (an optional target plus a conditional Attack), Mastering the Elements (copy everything permanently), Quagmire (loosening a shared function), Red Crane Dojo, Scavenging Party (its Limited half), Skipping the Puddle, Superior Reach.
- **Basic Lesson** is wired in PR #412.

Rulings Mendon gave on 2026-09-21 (all four now applied): a Reaction may be played from a hand to an action that targets your card; A Fugitive Apprehended's Magistrate clause is a second, later target and not a way round "lower Gold Cost"; Dislodge the Foe's Tacticians are performers, N of them, with one opposing Personality; "Your forests are maddening" is flavor.

## Verification

The suites `second-city-spirits` (10), `second-city-pass` (12) and `second-city-battle` (19) and `second-city-rulings` (8) drive every card through `QueryActions` and the commands it offers; each new primitive has a test that fails when it is removed (mutation checks recorded in the PR). `PublishedShape` tests read each printed value back from the catalogue and check the shipped art and the source receipt.
