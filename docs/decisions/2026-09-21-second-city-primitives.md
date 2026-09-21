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

## Stage 3 (#417): the after-resolution window

Wired: **Never Safe**, **Redoubled Attack**, **Tales of Dishonor** (strategy-wave-38) and **Quagmire** (region-wave-4).

### The window (AfterResolution.cs)

"After a battle's resolution ends": once the losing units are destroyed, the province is destroyed and Honor is gained or lost, and while the winners are still at the battlefield, either player may play the Reactions that name that moment, alternating (the active player first when both can), until both pass. It comes before the Conqueror's choice and the winners' return home (which is why Redoubled Attack can stop them bowing) and before the Rings' recruit window that follows a battle. It opens only when someone holds a Reaction that could be played now; with none the battle resolves exactly as before.

- A Reaction names the moment with the trigger `after-battle-resolution` (`ReactionTriggers.AfterBattleResolution`). It may be taken by a card in a hand (Redoubled Attack, Tales of Dishonor), in a discard pile (`AbilityDefinition.FromDiscard`: Never Safe) or in play (none is wired yet; the shape is the same). The window's facts are recorded when the resolution ends (`PendingAfterResolution`): who destroyed how many enemy units, whether the province was destroyed and by whom, and the battle's loser or losers; each Reaction reads them for its own condition.
- Redoubled Attack: "as many Redoubled Attacks as they have in their hand": each copy is its own play (its own performing Personalities, its own use), and a player who holds none is not offered one. Its performers are chosen, not targeted (the performing-slot primitive of #414), so they open no reaction windows.
- Never Safe: the card remembers the battle it was played in; the window offers its return from the discard pile to its player for that battle only. A card that is no longer in the discard pile, or was played in an earlier battle, is not offered.
- Tales of Dishonor: Mendon's ruling that a tie makes both players the battle's loser is built into the facts (`Losers`), so a tied battle costs both players 4 Honor when it is played, its own player included.

### Which cards need this check

Mendon asked which other cards return themselves to a hand after a battle. The catalogue holds one: **Never Safe**. The two nearest are Embracing Virtue ("Before an End Phase begins, if this card is in your discard pile: you may put it in your hand") and Move the Troops ("Open, 2 Gold: if this Strategy is in your discard pile, put it into your hand before this phase ends"): the same `FromDiscard` Reaction on another trigger, not on this window. Forty-two cards name a battle's end or resolution; what each needs of the window:

- **Already handled elsewhere:** Looting the Ruins and Vital Pathways (the province's destruction), the Ring recruits (the window after this one), and the "remove after the battle ends" cards (No Way Out, Deadly Orders, Death, Defeated, the created Personalities and Followers), which are delayed removals at the battle's close.
- **Now expressible in this window:** Redoubled Attack, Tales of Dishonor and Never Safe (wired); Spoils of Exploration and Shiba's Guidance - exp (draw after a resolution in which a province or army was destroyed), Shosuro Ryoken, Utaku Suying, Bandit Gang (a payment decision after the resolution), Treasure Hunter and Returning Home (create a Holding after a province is destroyed), Words Have Strength and Shosuro Rokuta (a loss of Courtiers or the Rokuta himself), Halls of the Forgotten (delay until the battle ends), False Ring of the Void.

Those are not wired here (they are not Second City cards and several need a decision the window does not yet ask for); each would be an ability with the trigger and a condition in `AfterResolutionApplies`.

### Quagmire

A Region's version of Togashi Hizumi's reaction ("after the resolution of an action that bowed a Personality at a battlefield at this province or an adjacent province, target him and bow this card: negate his next straightening this turn"). `OpenBowReactionWindow` now also looks for a Region: the Defender's, unbowed, whose province is the battlefield's or next to it (provinces are numbered in a row). The window's options were not listed by `QueryActions` for the bow-reaction window at all (Hizumi's reaction was only usable by a hand-built command); it now lists the watching card's reactions, one per Personality it may target, and the pass.

### Readings for Mendon (continued)

13. **The window is after destruction, the province and Honor, and before the winners go home.** "After a battle's resolution ends" for Redoubled Attack needs the winners still at the battlefield to straighten and to keep from bowing, so the window is not after the battle's close; the Rings' recruit window (a battle's aftermath) still follows it.
14. **Redoubled Attack's performers** are one or two of the player's own Personalities at the battlefield (any bow state), whoever won; "their units" is each with everything attached. Playing two copies on the same Personality is allowed.
15. **"They will not bow from the battle's resolution"** is a promise about the winners' return home, kept for the named Personalities and lapsing with the battle. A player-wide version (Akodo Dairuko) still works beside it.
16. **Tales of Dishonor's "you destroyed"** is the acting player's army destroying at least one enemy unit, or the Attacker destroying the province, in this resolution. In a tie both players are the loser, so the Tales' own player loses 4 as well.
17. **Never Safe's return** is offered to the player who played it, in the window after the battle it was played in, and to no one else.
18. **Quagmire's "this province or an adjacent province"** is the Region's province index and the two next to it; the battlefield must be at the Region's controller's province, so it is the Defender's Region that answers.

## Verification (stage 3)

Suite `after-resolution` (8 tests): the published shapes; no window without a Reaction; Redoubled Attack (each copy, one or two Personalities, no bow on return, the third refused, none for a player holding none); Tales of Dishonor (loser, tie, nothing destroyed, the defender who destroyed nothing); its Battle action; Never Safe (-4F, the return, one battle only, one player only, playable again); the trigger's exclusivity; Quagmire (own and adjacent province, not farther, not bowed, not the Attacker's).

## Stage 4 (#418): province strength maximum; Terrain statics

Wired: **Ancient Feud**, **Hasty Fortifications**, **Shiotome Plains** (strategy-wave-39).

### Current strength and a maximum (TerrainStatics.cs, `EffectiveProvinceStrength`)

A province has a CURRENT strength: its printed strength as raised and lowered by everything that has acted on it (Ring of Earth, a duel win, an Attack, Improved Defenses, a Region, a Terrain's bonus). Mendon's ruling: a maximum does not stop a raise, it is enforced on what is READ. Raises are added to the current strength and the maximum is applied after, so

- while the current strength is above the maximum every reader (the battle's resolution, the Attack rules, the client) sees the maximum;
- below it, every reader sees the current strength;
- when the limit ends (its Terrain leaves play, its Event ends) the province is back at its current strength, raises included.

`EffectiveProvinceStrength` is that read: current strength (raw strength, Region bonus, bonuses of cards still in play), then `ProvinceStrengthMax`, the lowest limit in force. Times of Strife ("all Provinces have a maximum strength equal to their base strength") was already a ceiling in that function and is now one source among the limits; Ancient Feud is the second, for the province at its battlefield while the Terrain is in play there, which is until the battle is resolved (a Terrain is discarded as the battle closes). The province view carries the limit (`ProvinceView.MaxStrength`) beside the strength that is read, so a client can show both. A minimum would be the mirror image in the same function; no card needs one, so none is built.

### Terrains (TerrainStatics.cs)

A Terrain is an ordinary card in play at a battlefield with a passive, non-optional trait; it is read live from where it stands, so nothing has to be undone when it leaves.

- **Ancient Feud**: the limit above. Playing it destroys the Terrain already at the battlefield ("all other Terrains", a battlefield holding one unless a card says otherwise), like Encircled Terrain and Looting the Ruins.
- **Hasty Fortifications**: each of its controller's Battle actions gives the battlefield's province +1 strength, and so does putting it into play ("including this card's"). The bonuses belong to the Terrain (`ProvinceState.SourceBonuses`) and are counted only while it is in play: destroyed, or discarded with the battle, they are gone.
- **Shiotome Plains**: at its battlefield every Cavalry Personality and Follower has +1F and every other one -1F, computed at the card level (`TerrainForceAdjustment`) and lost the moment the Terrain leaves play. Force is never below 0.

### Readings for Mendon (continued)

19. **A lowering acts on the current strength.** An Attack that reduces a capped province lowers the current strength (9 to 6), and what is read stays at the maximum (5) until the current strength falls below it. Say if a reduction should instead apply to the strength as read.
20. **"Your Battle actions" (Hasty Fortifications)** are the abilities with Battle timing (and a Battle/Open ability used in a battle) and the playing of the Terrain itself; an Equip taken as a Battle action and a recruit by a grant are not counted.
21. **"Infantry cards" (Shiotome Plains)** are the Personalities and Followers at the battlefield without the Cavalry keyword, each by its own keywords (a Follower is not judged by its host's unit). Items and other cards are not affected.
22. **A Terrain's effect cannot be negated yet.** Mendon's note that a negated Terrain's limit lapses is honoured for the Terrain leaving play; no card negates a Terrain's trait, so there is nothing to build for it.

## Verification (stage 4)

Suite `terrain-statics` (6 tests): the published shapes; the maximum (raised, below, other provinces, the Attacker's own province, the Terrain leaving, a farther battlefield); the battle's resolution reading the maximum; playing the Terrains (Ancient Feud replaces, the others do not); Hasty Fortifications (played, after each Battle action, stacking, the other player's action, gone with the Terrain and with the battle); Shiotome Plains (Personalities and Followers, Cavalry and not, elsewhere and at home, the floor of 0).

## Stage 5 (#419): the other player decides; a Focus Effect that pauses; sequenced targets

Wired: **Know Their Minds**, **Scandalous Rumors** (strategy-wave-40), **Dragon's Breath Castle** (stronghold-wave-11), **Disreputable Deal**, **Engulfing Flames** (strategy-wave-41).

### "Choose a player who chooses" (DeciderChoice.cs)

An action whose effect is a decision another player makes stops when it comes to that choice and hands it over: the engine offers the deciding player exactly the legal picks (`chooseDecider`: a card to add, or "done"), and when the choice is made the action carries on and finishes as any action does (the same pause-and-resume the Virtuous Charity payment uses, `PendingEffectChoice`). Mendon's ruling asked for the opposing player either to react to negate the action or to choose: the chance to react is the before-action window every action already has, which comes before the effect resolves, so by the time the choice is asked the action stands; a whole-action negation never reaches the choice, and a negation of a card's movement or dishonoring still applies to the effect that follows.

- **Know Their Minds**: the acting player chooses a player (an option per player, "you" and "your opponent"; the first Open action that chooses a player, so the pinned inventory of player-target abilities was updated); that player chooses one of his face-up Personalities in his provinces, which is turned face-down. It is revealed again by his next Events phase exactly as a card that was never revealed (the reveal loop reveals every face-down province card), with its reveal event.
- **Scandalous Rumors**: the Attacker decides, whichever player took the action. He may choose any number of his Personalities at the battlefield to move home (one pick at a time and "done"; none is a legal answer); each Personality still there afterwards costs him 1 Honor and is dishonored. A Personality whose movement failed (a negation, a movement lock) "was not moved" and counts.
- **Dragon's Breath Castle**: its Reaction ("Even if this card is bowed, after engaging at a battlefield at one of your provinces") makes the Attacker choose one of his unbowed Personalities there, which is bowed; it is played inside the Engage window, which is set aside and given back afterwards. Its trait gives its controller 2 Honor after another player's Dynasty Phase begins if that player controls any Personalities and assigned none of them to an attack this turn.
- With nothing to choose, or the one card a mandatory choice needs, nobody is asked: the log says so and the effect applies.

### A Focus Effect that needs a decision (FocusEffectChoice.cs)

A duel's Focus Effects resolve one after another in the order the active player chose; one with a real choice now stops the run at its place, its player answers (`chooseFocusEffect`), and the run goes on from the next Focus Effect and then finishes the duel exactly as before (the spending of the focused cards and everything after). `ResolveDuel` was split at that point; the duel's result is kept on `PendingDuel` while it waits. **Disreputable Deal**: a card, non-Unique, that is a Strategy in the player's Fate discard pile and has a Political ability (an ability printed with the Political action keyword, or on a card with the keyword: an ability inherits its card's keywords), goes to his hand; "may": any one or none. Mendon named the Fate deck as the place searched; the card's text says the discard pile, which is what is built (`FocusChoiceZone` is the one constant). Its Political Open action bows a performing Courtier, gives a target Personality -3F, and makes a dishonorable one's controller lose Honor equal to his printed Personal Honor.

### Sequenced targets and effects (SequencedTarget.cs)

One action, two effects, the second target chosen after the first effect has resolved: Engulfing Flames destroys an optional enemy attachment (an ordinary optional slot, named as the action is announced), and then, if its Monk has any Fire tokens, makes a Ranged 4 Attack whose target is chosen only now, because destroying the Follower that shielded a Personality from a Ranged Attack makes him a target. The Attack is an Attack in every respect (`engulfing-flames-attack`: strength, kind, target rules, reaction window), never offered as an action of its own. The acting player chooses among the cards that are legal targets now, or no target. It is one action: the costs are paid once, a whole-action negation negates both parts (neither happens), and a negation of the destruction leaves the Attack to happen (it does not depend on the destruction).

### Cards not built here and why

Dragon's Breath Castle's trait and Reaction are built; nothing else in the blocker families "the other player decides inside your action" remains.

### Readings for Mendon (continued)

23. **Know Their Minds' "choose a player"** may name either player, the acting player included; the chosen player's own face-up Personalities are the candidates.
24. **The opposing player's chance to negate** is the existing before-action window (before the effect resolves), not a second window inside the decision.
25. **Scandalous Rumors**: "the Attacker may target any of his Personalities at the current battlefield" is a choice of any number of them, chosen one at a time; a Personality that stays for any reason counts and is dishonored. Only Personalities are choices (not Followers).
26. **Dragon's Breath Castle's Reaction** needs a battle at a province of the Castle's controller (the Defender's); it is offered to him in the Engage window while the Castle is bowed. Its trait counts Personalities in play (home or at a battlefield) and reads "assigned to an attack" as the Personality having been assigned this turn.
27. **Disreputable Deal's search zone** is the discard pile (the card's text), not the Fate deck (Mendon's note).
28. **Engulfing Flames**: the second target may be none (an Attack may always be taken with no target); the card does not name the province, so unlike the training Attack it may not target one. It is judged after the destruction. "If your Monk has any Fire tokens" is judged when the first effect is done.

## Verification (stage 5)

Suites `decider-choice` (6), `focus-effect-choice` (6) and `sequenced-target` (5); the pinned player-target inventory was updated for Know Their Minds.

## Stage 6 (#420): copying a card's rules, Kata, may-not-be-targeted, dead Personalities as discarded

Wired: **Mastering the Elements** (spell-wave-15), **Skipping the Puddle**, **Superior Reach**, **Scavenging Party** (strategy-wave-42). The code is in `PermanentCopy.cs`, `KataCards.cs`, `TargetBars.cs` and `ScavengingParty.cs`.

### A permanent copy of a Spell's rules (PermanentCopy.cs)

"This card permanently copies the Spell's keywords, traits, and its base abilities that do not themselves copy abilities." `CardCopy.cs` copies one keyword or one ability for a while (Fukuzo); this takes everything but the title, for good. The copy is written into the copier's own printed values (`CardInstance.Printed`, the per-instance override every `Definition(card)` reads), so it is permanent for the game, survives the card leaving and re-entering play, and nothing that clears a card's grants or modifiers when it leaves play can undo it.

- **Keywords** are added to the card's own (the Spell's current keywords: printed, and any permanent grant).
- **Traits**: the Spell's trait is its `Effect` string, which is what drives its static bonus (Scouring Flood's +2F is keyed on the attached Spell's Effect). The Mastering card is a Spell, so once it has the trait its wielder has the bonus, as the ruling's example says.
- **Base abilities** are the abilities the Spell prints. An ability that copies abilities (`CopiesAbilities`, including Mastering the Elements' own) and one that triggers on its own card entering play (which has already happened for the copier) are not taken; nothing is added twice.
- **The Gold** is the Spell's printed Gold Cost, not its current cost (`GoldCostRule.TargetPrinted`): a Spell with a +3 Gold penalty still costs its printed 2.
- **Once per game** means once per stay in play, per Mendon's ruling: the entry window clears the stamp each time the card enters play, so the Reaction comes back when the card re-enters play, and a later copy adds abilities and keywords to what an earlier one gave. The Reaction is offered inside the entry window, so passing it forfeits it for that entry.
- **Scouring Flood example** (a test): Mastering enters play through Equip (Battle), pays 2 Gold, targets its controller's Scouring Flood, and gains the Water keyword, the +2F trait, and the Battle action that moves a defending Personality home.
- The entry window now lists an entering card's Reaction that targets a card in play (it listed only Reactions with no target or a player before): one option carrying every legal target and each target's own Gold quote.

### Kata (KataCards.cs)

A Kata is a keyword on a Strategy that is put into play instead of played and discarded: it sits in Zone.Home (at the right of home, like a Ring), stays until it is discarded or destroyed, and while it is there its controller has the action printed on it. Putting one into play discards all the player's other Kata (never the other player's). **Skipping the Puddle**: the Water Battle action (a Battle action with the keyword Water, once per turn like any action) moves a Water Shugenja to the current battlefield if the enemy has units there and grants an additional Battle action which he must perform.

### May not be targeted by (TargetBars.cs)

`TargetBar(ActorCardId, UntilTurn, WhileAttachmentKeyword)` on the protected card: no action the named card performs may target it, until the end of the turn, and while the protected card has an attachment with the keyword (Superior Reach: "while your Kensai has a Weapon"). The condition is live: a Weapon lost mid-turn ends the bar, and a Weapon attached again brings it back. It is checked where every action is validated, so a barred action is neither offered nor accepted and costs nothing. An action is performed by the performer the ability names, else the card it is on; an Item's or Spell's action is performed by its wielder.

### Dead Personalities are discarded too (ScavengingParty.cs)

**Scavenging Party**: "Target your discarded Personality: Discard a card from one of your provinces and refill it face-up with the Personality." Per Mendon, a dead Personality is also a discarded one (the same pile). The target is any of the player's Personalities in the Dynasty discard pile, dead or merely discarded; the province is chosen in the command (one of his provinces that holds a card); the card there goes to the Dynasty discard pile, and the Personality comes back as a fresh card with every permanent change cleared. The Focus Effect gives the duel's winner -3F (a tie has no winner); the Open half gives each of the player's provinces that stands +2 strength.

### Cards not built

**Blessings of the Shi-Tien Yen-Wang** (1040) is the one Second City card of the pass left unwired. It needs a move of a dead Personality to the top of its owner's deck (now that dead Personalities count as discarded, only that move is missing), a shield that outlives an action ("negate the next card effect that bows, destroys, or moves him", whose duration the text does not give), and the count of dead Personalities in all discard piles. The shield's duration is the reading Mendon needs to give.

### Readings for Mendon (continued)

29. **Mastering the Elements' target** is one of its controller's own Spells in play (attached to any of his Personalities), never itself, an enemy's or one in his hand.
30. **A trait slot**: a card has one trait, so a later copy's trait replaces an earlier one's, and a Spell without a trait leaves the card's trait alone. Keeping several traits at once would need a multi-trait model.
31. **"Base abilities"** are the abilities the Spell prints, not ones it was granted or copied; an entry ability is not copied.
32. **The copy is permanent for the game**: it stays across leaving play and re-entry (the Reaction re-triggers on each entry and adds to it).
33. **Skipping the Puddle's condition** ("if any enemy units are at the current battlefield") is a condition of taking the action: it is not offered otherwise. The performing Water Shugenja may be bowed (the text does not say unbowed) and may be anywhere, since he is moving. The additional Battle action is granted even if the movement was refused by a lock.
34. **A Kata put into play from the hand** is Zone.Home at the right of home; the other player's Kata are not discarded, and a Kata's action is its controller's.
35. **Superior Reach lasts until the end of the turn** and holds only while the Kensai has a Weapon; "actions the Personality performs" are the ones he performs himself (his own abilities, the Strategies he performs, and those of his Items and Spells). An action performed by another Personality may still target the Kensai.
36. **Scavenging Party**: a dead Personality is also discarded, for this card (a general reading of "discarded" for other cards, such as Shunsuke's "make dead", is not made: a dead Personality cannot become dead again). The province must hold a card, which goes to the Dynasty discard pile.

## Verification (stage 6)

Suite `copy-kata-targeting` (9); the move-to-battle inventory test now lists Skipping the Puddle's action.
