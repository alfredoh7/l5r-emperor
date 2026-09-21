# Second City primitives: the last 22 cards (issues #415-#420)

Requested by Mendon on 2026-09-21, after the Second City pass (#413, #414) left 22 cards unwired: build the general primitive each blocker names, then wire the cards it unblocks. Lane: rules. Status: readings proposed, awaiting Mendon's confirmation. The work is split into six stages, each its own issue, branch and PR, in this order: 1 timed straighten holds and movement locks (#415), 2 variable Gold costs and conditional Holding production (#416), 3 the after-resolution window (#417), 4 province strength and Terrain statics (#418), 5 the other player decides / paused Focus Effects / sequenced effects (#419), 6 copying, Kata, may-not-be-targeted (#420). This document grows one section per stage.

## Stage 1 (#415): timed straighten holds and movement locks

Wired: **Hibernation**, **Visitation**, **Strength of the Tsunami** (strategy-wave-36, spell-wave-14); **Bamboo Harvesters** moves onto the new primitive.

### The primitive: `StraightenHold` (StraightenHolds.cs)

A hold on a card says: this card is not straightened -- by the Straighten phase or by any effect -- until a point of a turn. It names

- **Owner**: whose turn counts. A player, or none ("whoever's turn it is when the point comes": "this turn", "the next Straighten Phase").
- **Until**: the point. `TurnBegins`, `StraightenPhaseEnds`, `EventsRevealEnds` (after the reveal of provinces at the start of the Events phase), `ActionPhaseEnds`, `TurnEnds`.
- **Skip**: how many of those points must pass with the hold still standing before it is released. 0: the next one releases it. Mendon's ruling: Bamboo Harvesters starts with 1 and counts down to 0, straightening on its controller's second turn; Hibernation creates 0, so the hold stands until its controller's next Straighten phase and does not survive it.
- **WhileInPlay** (optional): the hold ends when a named card leaves play ("while this card remains in play").

A card with any hold stands bowed. The hold is checked in the one place every straighten goes through (`Straighten`), so the Straighten phase and every effect are covered; an action that would only straighten a held card is not offered against it and a hand-built command is refused; the card's view says it will not straighten. Holds are cleared when the card leaves play and follow it through an Experience overlay.

### What each "may not straighten" card needs, and what it maps to

The survey covered every card in the reference catalogue whose text stops a straightening (33 cards).

| Shape | Cards | Model |
|---|---|---|
| Until my next turn begins / after it begins | Hibernation, Aramasu's Legacy, The Egg of P'an Ku - exp | `TurnBegins`, Owner = controller |
| Until after the reveal of provinces at the start of my next turn | Visitation | `EventsRevealEnds`, Owner = controller |
| Until after my next Action Phase | Jade Mine | `ActionPhaseEnds`, Owner = controller |
| During the (controller's / this / the next) Straighten Phase | Unseasonable Weather, Goju Asuka, Only a Shadow, Yasuki Daito | `StraightenPhaseEnds`, Owner = controller or none |
| Will not straighten before your Nth turn | Bamboo Harvesters (2nd), Bamboo Harvesters - exp (3rd), Temple to Fudo (3rd) | seeded from the printed trait: `TurnBegins`, Skip N-1 at the start of the game |
| While this card remains in play | Fortress of Lidless Eyes, Bonds of Hospitality | `WhileInPlay` |
| This turn (all of it) | Basic Lesson, Shinden Asahina, Shosuro Rin, Soshi Kodanshi | the existing `StraightenLockedTurn` (a hold that ends with the turn) |
| The NEXT straightening (this turn) | Quagmire, Togashi Hizumi, Bow Before My Will, The Second Feint, Unexpected Support, Sutigu, Hidden Defenses | the existing one-shot `StraightenBlockedTurn`; it is consumed by an attempt, which a timed hold is not |
| While a state lasts | Bayushi Rentatsu (while bowed), P'an Ku - exp (until it straightens), Constructing the Self (until this Spell straightens) | not covered: the release is a card state, not a point of the turn |

The two per-turn fields were left as they are on purpose: about twenty existing tests read and write them directly, and neither is a counted hold (the one-shot is consumed by an attempt). Folding them into `StraightenHold` (TurnEnds with a consume-on-attempt mode) is a mechanical follow-up if Mendon wants one representation.

### Bamboo Harvesters

The printed trait `skip-first-straighten` (a pinned Effect string, so the Lion/Crane profile is untouched) now seeds a hold the first time anything looks at the card: `TurnBegins`, Owner = controller, Skip = N - 1 - (turns its controller has taken). At the start of the game that is 1; it counts down as the controller's first turn begins and the second releases it. A Holding that enters play after the turns it waits for has no hold at all, which the old `TurnsTaken < 2` test also gave. The seed is not created by an entry hook (a scenario or deck build never runs one), so the first look creates it -- the view reads the same answer without changing the state.

### Movement locks (MovementLocks.cs)

Strength of the Tsunami's "If he moved, negate his (future) movement" and A Quiet Transaction's "he may not be assigned, and will not move, to battlefields this turn" are both "this card's movement of kind K is negated until the end of the turn". `MovementLock(Source, Turn, Kinds)` with kinds Home, ToBattlefield, Assign. Every movement an effect makes already funnels through `MoveHome` and `MoveToBattlefield`; assignment goes through `Assign`. Mendon's ruling: "future" means after the movement Strength of the Tsunami caused, until the end of the turn. A battle ending sends its units home by the rules, not by an effect, so it is not affected (the convention Tamori Tamoko's negation follows).

### Hibernation's shield

"After the next time this game another player's Battle action targets it, negate the action's effects": a mandatory trait, fired when the action is announced and its target chosen, before any reaction window. The whole action is negated (taken, its costs paid, nothing else happens -- the #351 ruling) and the shield is spent. Only a Battle action of the other player counts; the card's own controller's action, an Open action and a Reaction do not.

### Readings for Mendon

1. **Hibernation's hold lifts as the controller's next turn begins**, so that turn's Straighten phase straightens the card; effects that would straighten it in between (either player's turn) are negated.
2. **Hibernation's second sentence negates the whole action**, not only its effects on the hibernating card, and does so at announcement, before Reactions can answer. It applies to Attacks (a Battle action) and not to Open actions or Reactions.
3. **Visitation's destruction is a cost** ("Destroy ... to bow"): it is paid, and the Ancestor is lost, even if the bow is then negated. The hold's Owner is Visitation's controller, not the target's: the target is released after *her* reveal at the start of *her* next turn, and then waits for its own controller's next Straighten phase.
4. **Visitation's Limited half** treats every player: the acting player gains 2 if they control an Ancestor Personality; each player without one (the acting player included) loses 2.
5. **Strength of the Tsunami's "you may move him home"** is offered as a choice after the straighten (straighten only, or straighten and move home), the same effect-choice prompt Mirumoto Yumaru uses. "If this Shugenja is Water" is judged when the action resolves; a Shugenja who is not Water gets nothing and is asked nothing. Its second half locks movement home and to a battlefield, not assignment.
6. **Strength of the Tsunami's +6F** applies only to the Shugenja it is attached to (the same shape as Scouring Flood's +2F).
7. **Visitation's printing differs from its Oracle text.** The scan reads "Destroy your performing Ancestor Personality *and target* a Personality: Bow him. Negate his straightening until your next Events Phase begins"; the Oracle text (which Mendon's ruling used, and the reference catalogue carries) reads "to bow a target Personality ... until after you reveal cards in your Provinces at the start of your next turn". The wired card follows the Oracle text (`EventsRevealEnds`); the scan's "and target" agrees with reading 3, that the destruction is a cost.

## Verification (stage 1)

Suite `straighten-holds` (9 tests): the published shapes; Hibernation's timeline; who may perform it and the shield's cases; Bamboo Harvesters from the start of the game and later; a hold's refusal of effects and the view; the five points and Owner/Skip; Visitation's flow and Honor half; Strength of the Tsunami; movement lock kinds.

## Stage 2 (#416): a Gold cost from a printed number; conditional Holding production

Wired: **A Quiet Transaction** (strategy-wave-37), **Red Crane Dojo** and, as the second user of the same primitive, **Bushi Dojo** (holding-wave-5). Bushi Dojo is not a Second City card; it is the example Mendon named for the primitive, and wiring it exercises the shape with no printed production.

### The Gold cost from a stat (`GoldCostRule.StatValue`, GoldQuotes.cs)

"Pay Gold equal to [base | current] [stat] of [target | performer | source] plus or minus [z]." The rule names the card whose number is read (`CostStatSubject`: Target, Performer, Source), the stat (`CostStat`: Force, Chi, GoldCost, PersonalHonor, Focus, ProvinceStrength), whether the printed (base) or the current value is read, a modifier (added or subtracted; omitted = 0) and, as every Gold rule has, a floor and the never-below-zero clamp. The number is converted into Gold and enters the ordinary Gold pathway: it is quoted (derived, per target), validated, paid from the chosen sources and shown in the payment panel with its basis ("Kyuden's ... base Personal Honor 3 + 1"). Anything a card prints as a number can now be the price of an action; A Quiet Transaction is the first user.

- **Current** is the value with every modifier and token: current Force is `PersonalityForce`/`FollowerForce`, current Chi `PersonalityChi`, current Gold Cost `CardGoldCost`, current Personal Honor `CardPersonalHonor` (0 while dishonorable), Focus Value the printed value plus modifiers. **Base** is the printed value.
- **"Strength"** (Mendon's list): read as Force for a unit and Province Strength for a Stronghold, which are the two printed "strength" numbers. A Stronghold's current Province Strength is its printed one plus modifiers; the province's own tracked strength arrives with stage 4.

### Conditional production (ConditionalProduction.cs)

Every Gold payment is for one thing: a Personality or Holding recruited, an Item or Follower equipped, a card played from a hand, or an action of a card in play. A payment now carries that (`PaymentSubject`: the card, the kind, the ability), and a Holding whose Effect names a `ConditionalProduction` produces that amount when the payment meets its condition and its printed production otherwise. The subject is passed through the quote, the validation and the payment, so the three cannot disagree.

- Red Crane Dojo: 4 instead of 3 when paying for a card with the keyword Ninja (recruited, equipped or played; never for an action of a Ninja card already in play, which is not "paying for a card").
- Bushi Dojo: 6 when paying for a Follower equipped or for an action that creates a Follower; nothing otherwise.
- "Which may only pay for that card / for one action": no rule of its own is needed. Gold does not carry over from one payment to the next, so a source's whole production always pays for the single card or action it was chosen for and any excess is lost, exactly as before.

### Temporary removal (TemporaryRemovals.cs)

Red Crane Dojo's Reaction removes Ninja and Shadowlands "until the action ends". Mendon's ruling: applied after targeting, so an effect conditional on the target being Ninja or Shadowlands ("if the target is Shadowlands, destroy it") finds he is not; given back once the action has resolved; if it never resolves, given back at the end of the turn. Implemented as a record of exactly what was taken (`MatchState.TemporaryRemovals`), given back when the deferred action or Attack resolves, and swept at the end of the turn. Shadowlands is a Clan alignment, so it goes the way Discovering a Conspiracy's permanent removal does (the alignment and its "Shadowlands Clan" keyword), but temporarily. An action paused for a choice or a duel has not ended, so nothing is given back until it does.

### Readings for Mendon (continued)

8. **"Strength" in the cost list** is Force (units) and Province Strength (Strongholds). Say if it means something else.
9. **A Quiet Transaction's lock** covers assignment to a battlefield and any effect that moves him to one, for the rest of the turn; not movement home (Strength of the Tsunami's lock covers both). The lock is made when the action resolves, so an assignment already made this turn stands.
10. **Red Crane Dojo's Reaction follows the Oracle text**: "Ninja Tireless Reaction, bow this card". The scan prints only "Even if this card is bowed"; both agree it may be taken while bowed, and the wired one bows the Holding if it is not already. It is offered whenever an action (either player's) targets the controller's Personality who is Ninja or Shadowlands, and never for another player's Personality.
11. **Bushi Dojo's Oracle text** ("an action that Equips or creates a Follower ... which may only pay for one action") differs from the scan ("a Follower or an action that creates a Follower"). One condition serves both: a Follower being equipped, or an ability that creates a Follower.
12. **"Paying for a Ninja card"** includes recruiting, equipping and playing the card; it does not include the actions of a Ninja card already in play.

## Verification (stage 2)

Suite `gold-stat-cost` (8 tests): the published shapes; every stat, base and current, modifier and floor; quotes per target and performer and their payment; A Quiet Transaction's cost, lock and tokens; conditional production by payment kind; a Ninja recruit and a Follower equip paid by their Holding alone (and a non-Ninja / an Item not); Red Crane Dojo's Reaction while bowed and its removal and return; the end-of-turn fallback.
