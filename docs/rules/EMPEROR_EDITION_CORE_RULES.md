# Emperor Edition core rules

A working reference compiled from the AEG *Legend of the Five Rings* Emperor Edition rulebook, covering **"Overview of the Game" through "Rules-Relevant Keywords"** (printed pages 20–131). Front matter, credits, the "A Saga of Conquest" fiction (pages 6–19, including the "What's New in Emperor Edition" changelist), and the card-set checklist appendix (page 132 on) are intentionally excluded.

This is a rules reference, not engine documentation — it describes the tabletop game as published, independent of what this codebase currently implements. Content is restated in reference-document form (organized and reworded for clarity and lookup), not transcribed verbatim from the rulebook; all numbers, procedures, and exceptions are preserved precisely. Where this document's structure follows the rulebook's own table of contents, section headers below are matched to it so a page citation can still be traced back to source.

**This document is a compilation target for supplements.** More rules material (errata, FAQ rulings, later expansions' rules text, tournament policy, etc.) is expected to be layered on over time — see `docs/rules/SOURCES.md` for other rulebook-adjacent material already gathered in this repo, and `docs/rules/LION_CRANE_COVERAGE.md` for the existing per-card engine-implementation notes that already cite specific pages of this same rulebook.

No engine-code changes accompany this document; it is reference material only.

---

## Overview of the Game (p. 20–22)

Each player starts with a Clan Stronghold and one or two Holdings, plus four face-down provinces — the "hand" from which Dynasty cards enter play. Early turns typically bring more Holdings into play from provinces to build Gold production, and Personalities to build board presence.

The Fate hand (drawn from the Fate deck) supplies Followers, Items, and Spells that Personalities attach to strengthen themselves, plus Strategies that provide edges in battle or elsewhere.

Over the course of a game, players gain and lose Family Honor through court and battle, and fight over provinces. A game can be won through military conquest, accumulating Honor, driving down Honor, or pursuing mystical enlightenment (see **Winning the Game**, p. 88).

---

## Core Rules — The Cards (p. 23–39)

A play deck consists of a **Stronghold**, **starting Holdings**, a **Dynasty deck** (black-backed), and a **Fate deck** (green-backed). The Stronghold and starting Holdings never go into either deck.

### Stronghold

Represents a player's base of operations. It never enters a deck; it starts the game in play. A Stronghold has:
- **Card title**, in the white title bar.
- **Clan Mon**, to the left of the title (blank if unaligned).
- **Base Province Strength** — the topmost of three icons on the right side; the default strength of that player's provinces against attack.
- **Gold Production** — the middle icon (a coin); how much Gold the Stronghold produces when bowed.
- **Starting Family Honor** — the bottom icon; the player's starting Honor score.
- A text box (see **Reading the Cards**, below).

### Dynasty card types

**Holding.** Represents a resource in a player's lands. Has a Gold Cost (round coin icon) paid to bring it into play. **Holdings always enter play bowed.**

**Personality.** Represents a notable character. Has:
- **Force (F)** — lighter icon, top left.
- **Chi (C)** — darker icon, top right. *Special rule: if a Personality's Chi is ever zero, destroy him immediately; this destruction cannot be negated.*
- **Honor Requirement** — leftmost of three middle icons (square banner); a dash (–) means no requirement. Family Honor must normally be ≥ this value to bring the Personality into play. In-Clan Personalities can bypass this via Clan Allegiance (extra Gold — see Dynasty Phase).
- **Gold Cost** — middle of the three icons.
- **Personal Honor (PH)** — rightmost icon (fan); the Personality's own honor value.
- **Units**: a Personality plus everything attached to him form a **unit**. If the Personality leaves play, the whole unit leaves play the same way. Targeting, moving, or assigning a unit targets/moves/assigns its Personality (see Keywords, unit-keyword rules, below).

**Event.** Represents an unpredictable happening. Appears from a province, resolves, and is discarded — only during the Events Phase.

**Region.** Identifies a province with a specific area of Rokugan. Attaches to the province it's revealed in during the Events Phase; has its own traits/abilities once attached.

**Celestial.** Represents the favor of a supernatural being. Enters play from a province whenever revealed (not only in the Events Phase), discarding any other Celestial that player controls.

### Fate card types

**Strategy.** Played from hand, then discarded (unless it enters play). Has a Gold Cost (often 0) that is part of the cost of any action taken on it, and a Focus Value (used in duels).

**Ring.** Represents one of the five mystic elements. Enters play when its stated condition is met, or can be discarded from hand for a one-shot effect.

### Attachments (Follower, Item, Spell)

Followers, Items, and Spells are all attachment cards. They enter play by attaching to a Personality the controller controls, with the Gold Cost paid first.

- **Follower.** Represents troops, creatures, or advisors. Has Force (contributes to unit Force) and an Honor Requirement (minimum Personal Honor the host Personality needs).
- **Item.** Represents objects/artifacts. Has a **Force modifier** and a **Chi modifier** that adjust the host Personality's Force/Chi directly — Items do not have separate Force that's summed like a Follower's; use the modifier value when checking an Item's Force/Chi.
- **Spell.** Represents magic scrolls; no Force/Chi bonus. Attaches only to a Shugenja. Actions cannot be taken on a Spell that has somehow become attached to a non-Shugenja.

**Unit Force total** = Personality's Force + each attached Follower's Force (regardless of bowed state) + each attached Item's Force modifier (regardless of bowed state).

---

## Core Rules — Reading the Cards (p. 40–47)

### Stats

Force, Chi, Province Strength, Gold Cost, etc. are stats (numeric values). Stats can gain bonuses/penalties, and can be given minimums/maximums (applied on top of existing bonuses/penalties). Most stats floor at zero; only Honor Requirement, Family Honor, and +/− modifiers can go negative.

### Text box — the Cardinal Rule

> If the text of any card or ability contradicts a more general rule in the rulebook, follow the card or ability's text, not the rulebook.

(Example given: 'Heavily Engaged' can be used with no Personalities in play even though the Rule of Presence normally requires a unit at the battlefield — the card's text wins.)

A text box may contain, in order: Keywords, Traits, Abilities, Flavor Text.

### Keywords

A keyword is one or more words at the top of a text box, above a dividing line (e.g. "Phoenix Clan", "Samurai"). Some keywords are rules-relevant (see **Rules-Relevant Keywords**, p. 130); others exist only for other cards' effects to reference. Keywords are separated by "•"; keywords on different lines are separate. A multi-word keyword (e.g. "Dragon Clan") is a single phrase — a Dragon Clan card is not also a "Dragon" card.

A reference to "a unit's keyword" (e.g. "a Cavalry unit") requires the Personality **and every Follower** in the unit to have that keyword; one non-Cavalry Follower breaks a Cavalry unit.

### Traits

Plain-print phrases describing effects/restrictions. A trait may start with a **trigger** — text ending in a colon describing something happening in the game (e.g. "After this Personality enters play:") — optionally including costs that must be paid for the trait's effect to occur.

A trait with no trigger and no colon (e.g. "Your Samurai each have +1F") is a **continuous effect**, "always on" while the card is in play and its stated conditions hold. While out of play, a non-triggered trait only affects the card it's printed on.

Traits/abilities may reference a card's own title by name (e.g. "Bow Rentatsu:") — this always means that specific physical card, not any other copy sharing the name.

### Abilities

A block of text describing an action a player can take. Starts with one or more boldface **designators**: `Limited:`, `Open:`, `Battle:`, `Reaction:`. An ability with two designators (e.g. "Battle/Open:") can be used at either time and counts as both types.

An ability inherits all the keywords of the card it's on (a "Battle:" ability on a Ninja Personality is a Ninja ability). Some abilities have their own keyword printed before the designator (e.g. "Political Limited:") — that keyword describes only the ability, not the whole card.

Structure: `[keywords] Designator: [constraints block]: [effects block].`
- The **constraints block** (between the designator and the next colon) states what must happen for the action to be legally taken — conditions, required targets, and costs.
- The **effects block** (after the last colon) states what happens when the action resolves.
- Descriptive/flavor phrasing inside an ability (e.g. "Kanpeki snaps him like a twig") has no rules effect; only phrasing referencing defined terms or other cards is rules-relevant.

### Flavor text / Rarity / Border text

Flavor text (italic, bottom of text box) has no game effect. Rarity symbols: Rare, Uncommon, Common, Fixed, Promo, Premium. Border text (set abbreviation, card number, rarity symbol, artist) and any storyline-credit side text have no game effect.

---

## Setting Up the Game (p. 48–53)

1. Separate out Strongholds and any Holdings titled **'Bamboo Harvesters'** or **'Border Keep'** — none of these go in a deck. Split the rest into Fate (green) and Dynasty (black) decks.
2. Each player starts with their Stronghold in play.
3. **Going first**: the player with the higher Starting Family Honor on their Stronghold goes first; ties are broken at random.
4. **Starting Holdings**: every player starts with one **'Border Keep'** in play. The player who is *not* going first also starts with one **'Bamboo Harvesters'** in play and bowed; the player going first keeps their copy out of the game entirely.
5. **Bowing/straightening**: a bowed card is turned 90° right, showing it has spent its effort. Costs/effects can bow an unbowed card or straighten a bowed one.
6. **Paying Gold**: a Stronghold and most Holdings bow to produce Gold (amount from the Stronghold's Gold Production stat, or a Holding's own text). Gold from a single source must be spent entirely on one cost — excess is lost and cannot carry over — but multiple sources' Gold can be combined for one payment.
   - *Worked example*: an unbowed Stronghold producing 4, plus a Marketplace producing 3 and a Border Keep producing 2 (both unbowed), buying an 8-Gold card: bowing all three produces 9 (any two of them cap at 7), and the extra 1 is lost.
7. Shuffle both decks separately (opponent may cut/shuffle), Fate deck to the player's right, Dynasty deck to the left. Discards: Dynasty discard pile to the left of the Dynasty deck, Fate discard pile to the right of the Fate deck. A **destroyed** Personality is dead, not merely discarded — mark this by turning the card 90° sideways in the discard pile or keeping a separate "dead" pile.
8. **Provinces**: take the top 4 cards of the Dynasty deck, lay face down left-to-right between the two decks — one province each. If a province is ever without a card, refill immediately face-down from the top of the Dynasty deck (face-down cards, even a player's own, may not normally be looked at). A province that cannot be refilled (empty deck) still exists conceptually. Each province's Province Strength starts at the Stronghold's base value.
9. **Hand**: draw 6 cards from the Fate deck. Hand cards are considered face-down but the holder may look at their own.
10. Other equipment: a way to track Family Honor (paper/pencil, dice), and an object to represent the Imperial Favor.

---

## The Turn Sequence (p. 54–87)

The starting player takes the first turn, then play passes left, repeating until the game ends.

### 1. Straighten Phase (p. 54)

Straighten all of the active player's bowed cards, including the Stronghold. ('Bamboo Harvesters' has its own trait overriding this on the controller's first turn — it stays bowed.)

### 2. Events Phase (p. 54–56)

Starting with the leftmost province and moving left to right, reveal each face-down province card, then:
- **Holding or Personality**: nothing happens now.
- **Event**: remove it from the province; its effects resolve immediately (before refilling); discard it once resolved. Events resolve *only* in the Events Phase.
- **Region**: must attach to the province it was revealed in (moved partway up in the province stack; region's title stays visible after the province refills). Any entering-play effects resolve now. A province can hold only one Region at a time; a Region that fails to attach is discarded immediately. Regions enter play *only* in the Events Phase. Once attached, a Region's traits/abilities are usable by its province's owner.
- **Celestial**: discard any other Celestial that player controls, then put it into play in that player's home. (Celestials can also enter play outside the Events Phase whenever revealed elsewhere.)

### 3. Action Phase (p. 56–70)

Players alternate taking a Limited or Open action (or passing), starting with the active player, then each other player clockwise gets a chance at an Open action, and so on — an **action round**. A player who passes may still act again later in the same phase. The Action Phase ends only when all players pass consecutively.

**When can an action be taken?**
- **Limited** actions: only by the active player, only in the Action Phase.
- **Open** actions: by any player, only in the Action Phase.
- **Battle** actions: only during a battle's Combat Segment.
- **Reaction** actions: only at the specific triggering point in time stated on the ability.

If multiple Reactions are eligible on the same trigger, resolve them in an action round of their own: active player has first opportunity, then the other player, then back to the active player, etc., until both pass consecutively.

**Constraints-block vs. effects-block matters**: things named between the two colons of an ability are requirements to *legally take* the action (must actually happen/be possible). Things named after the last colon do not need to be possible in order to take the action.
- *Example*: `"Limited: Bow your target Personality: Gain 1 Honor."` — the Personality must actually be unbowed and bowable (it's between the colons) to take the action.
- `"Limited: Target your Personality: Bow him and gain 1 Honor."` — bowing is after the last colon, so an already-bowed Personality can still be legally targeted (the action just doesn't re-bow him further, or simply has no bow to apply — either way the action is takeable).
- `"Limited: Bow a Personality and gain 1 Honor."` (single colon, no constraints) — legal even with zero Personalities in play, since bowing one and gaining Honor are independent effects, not dependent on each other.

**A. Check Conditions.** Evaluate any stated conditions (e.g. "If you control a Shugenja..."). Additional universal limits:
- Abilities on bowed cards normally cannot be used.
- A card must normally be in play (or a Strategy/Ring played from hand) to use its abilities; an ability that implies otherwise (e.g. "Reaction: After this card is destroyed:") is legal to use from the discard pile.
- Each individual card's ability, and each player ability, may normally be used only once per turn. Two different physical cards sharing a title can each use their copy of the ability once.

**B. Announce.** State the card and ability being used to the opponent; reveal it if it's coming from hand (Strategy/Ring).

**C. Choose Performers.** If the constraints block names a performer (e.g. "your performing Samurai with 3+ Force"), choose all named performers in the order mentioned; performers must normally be in play. Regardless of whether the ability names a performer: an action on a Personality or Follower is always performed by that card itself; an action on an Item or Spell is always performed by the Personality it's attached to. (A Personality who "may not perform actions" can't use his own abilities or his attachments' abilities, and can't be chosen as a performer for anything else.)

**D. Required Targeting.** Choose all required targets named in the constraints block, in order. All must be legally choosable to take the action. Targeting phrased with "may" is optional. Targeting named in the *effects* block is not required up front — it happens during resolution. Targets must normally be in play.

**E. Pay Costs.** Pay every cost named in the constraints block, in order. Anything in the constraints block that changes game state is a cost (e.g. "Bow one of your Samurai:"). A Strategy's Gold Cost is a cost of all its actions and is paid first. A cost must *fully* happen to be paid — e.g. you cannot pay "give him −4C" using a Personality who only has 3 Chi to lose (floored at his minimum). Only cards in play can normally pay costs; cost payments are not effects.

**F. Action Resolution.** If targets are met and costs paid, apply effects in the order listed. Once resolution starts, all effects apply regardless of what subsequently happens to their source card. If an action brings a card into play, its own costs must be paid at that point or it does not enter play. An effect can be individually prevented (e.g. by a trait like "Negate all effects that bow this Personality") without invalidating the rest of the action — the action stays legal and its other effects still happen.

**G. Tidy Up.** If the action came from a Strategy in hand, discard it (unless it entered play).

**Traits and effects** (p. 66–68): a triggered trait works like a Reaction (constraints block, trigger, colon, effects) but differs in four ways: (1) mandatory, not optional, whenever triggered; (2) usable even while the card is bowed; (3) not limited to once per turn; (4) resolves *before* any Reaction to the same trigger gets a chance. A non-triggered trait's continuous effect is "always on" while its conditions hold, in and out of play (per the out-of-play caveat above).

**Duration**: instantaneous effects (bowing, tokens, Honor changes) are physically marked and don't wear off on their own. Ongoing effects (stat changes, granted/negated abilities/traits/keywords/conditions) are not physically marked and last **until the end of the current turn unless a different duration is stated**.

**Universal player abilities available in the Action Phase** (p. 68–71):

- **Equip (Limited)**: `Limited: Any number of times per turn, choose your performing Personality and target an attachment card in your hand: Attach it to him, paying all costs.`
- **Equip (Battle)**: `Battle: Any number of times per turn, choose your performing unbowed opposed Shugenja and target a Spell in your hand: Attach it to him, paying all costs. You may take an additional action to use a Battle ability on that Spell.`
- **Lobby**: `Political Limited: If you have higher Family Honor than each other player, bow your performing Personality with 1 Personal Honor or higher: Take the Imperial Favor.`
- **Imperial Favor**: controlled by at most one player at a time; starts the game uncontrolled. Discarding it (giving up control) is a cost of certain actions, including:
  - **Favor (Limited)**: `Favor Political Limited: Discard the Imperial Favor and a card: Draw a card.` ("discard a card" defaults to a Fate card from hand.)
  - **Favor (Battle)**: `Favor Political Battle: Discard the Imperial Favor and target an attacking enemy Personality: Move him home.`
- **Order Seppuku** (see also Glossary — Seppuku, p. 114): `Open: Any number of times per turn, target a dishonorable Courtier, Samurai, or Shugenja Personality you control: He commits seppuku.` Note: this action is *not itself performed* per the Choose Performers rules.

### 4. Attack Phase — optional (p. 71–82)

The active player may declare an Attack Phase.

**A. Declaration Segment.** The declaring player is the **Attacker**, the other is the **Defender**. A **battlefield** is created for each of the Defender's provinces. One or more units on the same side at a battlefield form an **army**.

**B. Infantry Maneuvers Segment.** The Attacker simultaneously assigns any number of unbowed-Personality-led units from home to attacking sides of battlefields (one battlefield each, or split across several, or keep some/all home). Then the Defender assigns defending units the same way. **Assigning** places a unit at a battlefield and is distinct from "moving." A unit led by a bowed Personality cannot be assigned.

**C. Cavalry Maneuvers Segment.** Same process as Infantry Maneuvers (Attacker then Defender), but restricted to **Cavalry units** — a unit whose Personality and every Follower (if any) all have the Cavalry keyword.

**D. Fight Battles.** The Attacker picks a battlefield; a battle is fought there; when it ends, the Attacker picks the next battlefield; repeat until every battlefield has been fought exactly once (even a battlefield with zero units on both sides still has its one battle). No battlefield gets a second battle even if units later arrive there.

**Battles** (most arise from an Attack Phase, but some effects create a battle outside one). A battle has a **Combat Segment**, then a **Resolution Segment**.

**Combat Segment**: an action round of Battle actions, starting with the Defender.

*When can a Battle action be taken?*
- **Rule of Presence**: to take any Battle action, the acting player must control ≥1 unit at the current battlefield.
- **Rules of Location**: to take a Battle action from a card in a unit, that unit must be at the current battlefield; to *perform* a Battle action, the performing card must be at the current battlefield; to be *legally targeted* by a Battle action, all cards in the target's unit must be at the current battlefield.
- **Exception**: a Battle action that itself *moves* a unit/Personality to the current battlefield (or brings a Personality into play there) is legal to take as long as Presence/Location would be satisfied *once that movement/entry happens* — even if the acting/performing/targeted unit isn't there yet when the action is announced. (Example given: a Follower at home with "Battle: Move this card's unit to the current battlefield. Gain 1 Honor." may perform this even with zero units currently at that battlefield, because the action's own effect is what gets it there. If some other card's effect prevents the movement, the action was still legal to take — the Exception only cares what the ability's own text says, not whether the movement actually succeeds.)
- **No Second Battle**: a Personality already in an attacking army during a battle resolution earlier in the *current* Attack Phase will not move to a(nother) battlefield for the rest of that Attack Phase.

Once both players pass consecutively, the Combat Segment ends.

**Resolution Segment**: total each army's Force (sum of Force of all *unbowed* Personalities and Followers in it — remember Items add Force to their Personality directly regardless of bow state; see Follower/Item rules above). A side with no units has zero Force.

- Higher Force wins. The winner destroys every card in the losing side's army.
- If the Attacker won **and** attacking Force exceeds defending Force by more than the battlefield's province's Province Strength, the Attacker **also** destroys the province: discard everything in it (and any attached Region), then close up the gap in that player's province row/decks.
- **Tie**: if both sides have ≥1 unit and equal Force, each side destroys the *other* army entirely. If both sides are tied at **zero** Force (i.e. either or both sides have no units), the battle has **no outcome** at all.
- **Honor from a battle**: a clean winner gains Honor equal to **twice** the number of enemy cards destroyed by resolution. In a tie, each player gains Honor equal to the number of enemy cards *they* destroyed (i.e. 1 per card, not 2).
- **Aftermath**: all attacking units at that battlefield bow, then return home (this return is *not* "movement" for rules purposes). Defending units stay put and only return home at the end of the whole Attack Phase, without bowing. Any Terrain at the battlefield is discarded (see Glossary — Terrain). The battle then ends.
- Once the Attack Phase's last battle ends, the Attack Phase itself ends, battlefields cease to exist, and any units still at a battlefield (a corner case) bow-and-return-home if attacking, or just return home if defending.

### 5. Dynasty Phase (p. 83–87)

**a. Bring cards into play from provinces.** In any order, any number of times: bring a face-up Holding or Personality into play from a province.
- **Holding**: pay its Gold Cost; always enters play bowed.
- **Personality**: Family Honor must be ≥ his Honor Requirement, and his Gold Cost must be paid. If he shares the controller's Clan alignment, any combination of:
  - **Clan Discount**: pay 2 less Gold. (Does not apply to Personalities created mid-game.)
  - **Clan Allegiance**: pay 2 more Gold and ignore his Honor Requirement entirely.
  - *(These two combine: a 6-cost, Honor-Requirement-6 in-Clan Personality bought at Family Honor 3 can be bought for 6 total — +2 for Allegiance to ignore the requirement, −2 for Discount.)*
  - **Proclaim** (universal Reaction): `Reaction: After you bring a Personality in your province with your Clan alignment into play without Clan discount, if it is your turn: Gain Honor equal to his base Personal Honor.` Combinable with Clan Allegiance (pay the extra 2 Gold, ignore Honor Requirement, *and* still gain Honor via Proclaim) — but never combinable with Clan Discount, since Proclaim explicitly requires *not* using the discount.

**b. Discard face-up cards.** Once done buying, any face-up province card may be discarded one at a time, refilling face-down before discarding the next.

### 6. End Phase (p. 87)

Draw a Fate card. If the hand exceeds the maximum hand size of **8**, discard down to 8. The turn then ends and passes to the next player.

---

## Winning the Game (p. 88–89)

- **Military Victory**: opponent loses immediately when their last province is destroyed.
- **Dishonor Victory**: if a player's Family Honor ever reaches **−20 or below**, they lose at the start of their *next* End Phase — even if their Honor recovered to −19 or higher in the meantime (the loss is latched in at the moment it dropped to −20, delivered at the next End Phase).
- **Honor Victory**: a player wins if they start their turn at **40 or more** Family Honor.
- **Enlightenment Victory**: a player wins if they start their turn controlling all **five Rings** (Air, Earth, Fire, Water, Void — one each), each of which *most recently* entered play via its own printed text (not, e.g., overlaid or granted by another card's effect).

---

## Deck Construction — Emperor 40/40 format (p. 89–92)

- Dynasty deck and Fate deck must each contain **at least 40 cards**. The Stronghold and the starting 'Border Keep'/'Bamboo Harvesters' do not count toward this and may not be included as additional copies in either deck.
- 'Border Keep' and 'Bamboo Harvesters' each have more experienced versions (see Glossary — Experienced); the deck builder must choose exactly one version of each, plus a Stronghold.
- Every card in the play deck (Stronghold included) must be Emperor Edition legal — i.e. carry the Emperor Edition legality symbol on its **most recent printing (MRP)**. Older printings may physically be used as proxies, but a card's rules stats/text are always those of its MRP in English; using an older physical card is the player's responsibility to track correctly.
- **Card copy limits**: at most **3 copies** of any card (by title) across a player's decks; at most **1 copy** of a card with the **Unique** keyword (by title). Experienced cards are an exception to both of these limits (see Glossary — Experienced). Individual cards or Strongholds may explicitly override deck-construction rules.

*(Deckbuilding guidance from the rulebook's own "Learning to Play" tips: keep decks close to the 40-card floor; Dynasty deck around 13–17 Holdings plus a handful of Celestials/Events/Regions, the rest mostly in-Clan Personalities affordable off the Stronghold (or Stronghold + one Holding), with a few pricier Personalities for the midgame; Fate deck mostly 0-Gold Strategies/Rings, sparing use of Gold-cost attachments, and Spells only worthwhile with 6+ Shugenja in the deck.)*

---

## Rules Glossary (p. 93–119)

Alphabetical; only terms not already fully covered above. Cross-references point back to this document's own sections where the term is covered in depth (e.g. Battle, Duel, Deck Construction) rather than repeating them.

- **Active player**: the player whose turn it is.
- **Ally**: multiplayer-only term (see Online Rules / multiplayer variants, not covered by this document's page range).
- **Armor**: keyword on some Items; a Personality can have at most one Armor attached.
- **Base**: a stat/keyword/text value as printed on a card's MRP, unmodified. For a created card, "base" means the value it was created with.
- **Challenge / Duel** (full procedure, p. 100–104): a challenge creates a duel between two Personalities controlled by different players — a **challenger** and a **challenged**. "A duels B" = "A issues an unrefusable challenge to B." Some challenges may explicitly be refusable; if refused, no duel happens (possibly with stated consequences); otherwise the challenged player accepts and the duel proceeds:
  1. The **challenged** player has the first option each round to **focus** or **strike**. To focus: choose a card from hand, or take the top card of the Fate deck blind, and place it face-down in a personal "focused cards" area (not "playing" the card) — once there, its owner may look at it and their other focused cards, but never an opponent's face-down focused cards. Then the other player gets the same choice, alternating, until someone strikes. **Focusing caps at 4 per player per duel** (Duelist is an exception — see below); traits/actions that substitute "instead of focusing" count against this cap. A player who can't focus (at the cap) must strike.
  2. Once struck: reveal all focused cards face-up. Any focused card with a trait triggered by **"As a Focus Effect:"** resolves now, in an order chosen by the player whose turn it is (traits without that exact trigger are ignored; abilities on focused cards cannot be used at all).
  3. **Resolution**: each player totals their focused cards' Focus Values plus their dueling Personality's **duel stat** (Chi, unless the duel specifies otherwise). Higher total wins; lower total loses; stated consequences apply to winner and/or loser. A tie means **both players lose** and take the loser's consequences.
  4. Discard all focused cards; any Focus Value changes end.
  - **Duelist** keyword: in a duel, a Duelist (a) wins ties against a non-Duelist opponent; (b) before Focus Effects resolve, may discard one focused card and focus one additional card (this happens before any Reaction timed to "before Focus Effects resolve"; if both duelists have Duelist, the active player does this first).
- **Clan alignment**: a keyword ending in "Clan," one of the nine: Crab, Crane, Dragon, Lion, Mantis, Phoenix, Scorpion, Spider, Unicorn.
- **Compare**: see Ranged/Melee Attacks, below.
- **Conqueror / Conquest**: Conqueror is a Personality keyword enabling the universal **Conquest** Reaction: `Reaction: After a battle resolution ends, choose your performing attacking Conqueror Personality at that battlefield: Straighten his unit. It will not bow from the battle's resolution.`
- **Contribute**: a Personality/Follower "contributes Force" when its Force is summed into its army's total (during battle resolution or any other Force total calculation).
- **Control**: gained when a card enters play; only in-play cards are "controlled." Normally retained while in play, with two standing exceptions — an attachment is always controlled by its Personality's controller, and a Region is always controlled by the owner of the province it's attached to. When control of a card changes hands mid-game: a Personality at a battlefield moves to the new controller's side there; an attachment transfers to one of the new controller's Personalities anywhere who could legally attach it; anything else enters the new controller's home.
- **Copy** (stats/keywords/traits): copying a stat sets it to the *current* value of the copied card's stat (see Set, below). A card never copies its own text/stats.
- **Courtier**: may be targeted by Order Seppuku (see Seppuku, below, and Action Phase).
- **Created cards**: cards an effect creates beyond a deck's contents (tracked with paper, spare face-down cards, or official proxies). Any stat not given a value by the creating effect defaults to zero, *except* a created Personality's Honor Requirement, which defaults to "–" (none). Created attachments/Regions attach as part of their creation effect; other created cards enter the creating player's home unless stated otherwise. A created card is removed from the game (not discarded) when it leaves play.
- **Dead / Destroy**: a destroyed Personality enters the discard pile **dead** — mark this physically (turned 90°, or a separate pile).
- **Discard**: puts a card into its Dynasty/Fate discard pile. "Discard a card" alone defaults to "discard a Fate card from hand." Discarding the Imperial Favor as a cost/effect means relinquishing control — it becomes uncontrolled, not literally moved to a pile.
- **Discarded**: the state of being in a discard pile without being dead.
- **Dishonor** (full mechanics, p. 98–100): dishonoring a Personality turns the card 180° upside-down. A dishonorable Personality's **Personal Honor is capped at zero**. When a dishonorable Personality is destroyed, the player who last controlled him **loses Honor equal to his base Personal Honor** — and he remains "dishonorably dead" even in the discard pile. Whenever a dishonorable Personality would otherwise cause its controller to *gain* Honor, that gain is replaced: the Personality is **rehonored** (restored to honorable status) **instead of** the Honor being gained. This substitution applies specifically:
  - before a player gains Honor from an action/trait that targeted, came from, or was performed by one of their dishonorable Personalities;
  - before a player gains Honor from attaching a card to one of their dishonorable Personalities;
  - before a player with ≥1 dishonorable Personality in their battle army gains Honor from destroying enemy cards in that resolution — and in a **tied** battle specifically, *all* dishonorable Personalities in an army are rehonored before being destroyed, and that army's leader gains **no** Honor from the tie at all.
- **Do not / Does not / Will not**: means the described effect simply fails to happen.
- **Draw**: move the top card of the Fate deck to hand.
- **Element keyword**: Air, Earth, Fire, Water, or Void.
- **Enemy**: the opposing player in a battle ("enemy leader") and cards in units they control. Cards not in units (Holdings, Regions) are never "enemy cards."
- **Engage**: the moment a battle begins.
- **Entering-play**: an effect triggered by its own card entering play.
- **Exchange**: note both stats' current values, then simultaneously set each to the other's noted value.
- **Experienced** (full mechanics, p. 105–106): a Personality with the Experienced keyword (optionally followed by a number) has an experience level (no keyword = level 0; "Experienced" alone = level 1; "Experienced N" = level N). Multiple single copies of the same title at different experience levels may all be in one deck. During the Dynasty Phase, an Experienced Personality can either enter play normally, **or overlay** onto an in-play Personality of the same title at a *lower* experience level: overlaying skips Honor Requirement and entering-play costs, but other requirements/restrictions (e.g. Loyal) still apply. The overlaying card sits on top of the less-experienced one (which is removed from the game) without itself "entering play," and inherits all state, ongoing effects, attachments, and tokens — the two are considered the same card throughout. Some card effects overlay onto a Personality that isn't a less-experienced version, following these same mechanics. Non-Personality Experienced cards follow Experienced deck-construction rules but never overlay.
- **Fail**: when an activated cost/effect does not happen — due to negation, other "will not" prevention, or lack of a legal object to apply to.
- **Focus / Focus Effect**: see Challenge/Duel, above.
- **"From"**: an effect/action/targeting is "from" the card it's printed on (or the rulebook, if rulebook-derived). Three exceptions: (1) a Ranged/Melee Attack's targeting and destruction are "from" the card that created the Attack, not the rulebook; (2) a Personality's 0-Chi destruction is "from" whichever effect last inflicted a Chi penalty on him, or the rulebook if none did; (3) traits/abilities granted to, or copied onto, other cards (or printed on created cards) are "from" the card they're now on.
- **Home**: the location where cards normally enter play, as distinct from a battlefield.
- **Honorable**: a Personality not in the dishonorable state. "Honorably dead" = dead and honorable.
- **Human**: a Personality/Follower without the Nonhuman keyword. (Not itself a keyword.)
- **Ignore**: a cost/requirement so ignored is no longer required at all.
- **Infantry**: a Personality/Follower without the Cavalry keyword. (Not itself a keyword.)
- **Kensai**: may attach two Weapons (normal limit is one — see Weapon, below).
- **Leader**: the Attacker or Defender in a battle (greater relevance in multiplayer variants).
- **Location**: an area holding cards in play — each battlefield, and each player's home, is its own location.
- **Look**: inspect a face-down card privately without changing its position.
- **Loyal**: a Personality who will not be controlled by a player without a shared Clan alignment.
- **May not**: forbids a choice outright — unlike "Do Not/Will Not" or Negate, the forbidden thing can never even be selected/attempted in the first place.
- **Melee Attack**: see Ranged/Melee Attack, below.
- **Move**: change a unit's location. A move to a battlefield always places the unit on its controller's side there.
- **Naval**: enables the universal **Naval Invasion** Reaction: `Reaction: After engaging, once per battle, if you are the Attacker and your current army has more Naval cards than the side opposing it, choose your performing Naval Personality: You have the first opportunity to take a Battle action, which he must perform.`
- **Negate**: while active, makes another (new or ongoing) effect fail whenever it would occur, unless stated otherwise.
- **"On"**: determined the same way as "From" (above), with one difference — an ability granted *to a player* is "on" that player but still "from" its originating card/rulebook.
- **Opposed**: a card/side/army is opposed at a battlefield if the enemy army there has ≥1 unit.
- **Opposing**: cards at the same battlefield on different sides.
- **Overlay**: see Experienced, above.
- **Own**: a player owns every card originally in their play deck plus anything they bring into play from outside the game (including created cards). Owned cards can never end up in another player's deck or discard pile — they always return to their owner's.
- **Perform**: see Choose Performers, above.
- **Permanent**: an effect lasting until the game ends — can still be negated/ended/changed by other effects.
- **Ranged Attack / Melee Attack** (p. 112–113): "Ranged X Attack" is shorthand for *"Target a Follower or a Personality without Followers in the current enemy army. If its Force is ≤ X, destroy it."* If a specific effect compares against a stat other than Force, compare that stat to the Attack's strength instead. **This targeting is not required up front** — it's resolved as the effect happens, so an action containing a Ranged Attack can be announced with no valid target in sight. Melee Attacks follow identical rules but are not themselves considered "Ranged Attacks" (relevant to effects that specifically reference one or the other).
- **Reconnaissance**: a state a player can hold at one or more battlefields, granted only by other effects (e.g. "You have Reconnaissance at the current battlefield").
- **Rehonor**: see Dishonor, above.
- **Remove from the game**: set aside entirely, outside all game areas, permanently (not a discard).
- **Reveal**: turn a face-down card face-up.
- **Samurai**: may be targeted by Order Seppuku.
- **Search**: look through a deck/area for a card matching stated criteria; the deck must be reshuffled afterward.
- **Seppuku**: ordering a Personality to commit seppuku applies *"Rehonor the Personality, then destroy him; these effects will not be negated."* Every player also has the universal ability: `Open: Any number of times per turn, target a dishonorable Courtier, Samurai, or Shugenja Personality you control: He commits seppuku.`
- **Set**: give a stat a bonus/penalty sufficient to reach the stated value.
- **Show**: display a face-down card's face to everyone, then return it face-down.
- **Shugenja**: only Shugenja can attach Spells; may be targeted by Order Seppuku.
- **Singular / Unique**: a player cannot bring into play, or take control of, a Singular/Unique card sharing a title with one they already control (Experienced is an exception — see above). Taking control of a unit carrying a duplicate Singular/Unique attachment discards the new copy. Unique also restricts deck construction (see Deck Construction, above).
- **"Soul of …"**: a keyword naming an older Personality, marking the current card as a descendant/disciple. Equivalent to the named older card for deck-construction purposes; the older physical card may be used as a proxy in play, but differences in stats/wording must be disclosed to the opponent.
- **Switch**: two units simultaneously swap locations; negating one side's movement negates the other's too.
- **Tactical / Tactician**: a Tactical-keyword action can only be performed by a Tactician, and each individual Tactician may perform at most **one** Tactical action per turn. Universal ability: `Tactical Battle: Any number of times per turn, discard a card and choose your performing Tactician Personality: Give him a Force bonus equal to the Focus Value of the discarded card.` (— note the per-Tactician cap still limits how often any one Personality can use it, even though the ability text itself has no stated limit.)
- **Terrain**: a Strategy with the Terrain keyword represents the battlefield's ground; typically has a Battle ability putting it into play at the current battlefield if no Terrain is already there. Any Terrain at a battlefield is discarded once that battle ends.
- **Token**: a physical marker (beads, paper, etc.), not a card. May carry a keyword (e.g. Corruption) and/or a stat bonus/penalty; that bonus/penalty is considered to come from whichever effect most recently placed the token. Tokens are removed from the game if their card leaves play.
- **Transfer**: move an attachment or token from one Personality/province to another; no need to re-pay its cost, but attaching restrictions still apply.
- **Unaligned**: a Personality/player/Stronghold with no Clan alignment. ("Unaligned" is not itself a keyword.)
- **Unique**: see Singular, above.
- **Weapon**: keyword on some Items; a Personality can have at most one Weapon attached (Kensai is the stated exception, allowing two).

---

## Online Rules appendix (p. 119–131)

The rulebook points to `rules.l5r.com` for errata, edge-case rulings, and multiplayer/draft/sealed variants, and the (now-historical) L5R CCG Rules Questions forum for official Q&A. Not itself rules content — noted here for provenance.

### Turn Sequence — summary card (p. 120–121)

A condensed restatement of the six phases already detailed above (Straighten → Events → Action → Attack (optional) → Dynasty → End); included in the rulebook as a quick-reference card. No new content beyond the full Turn Sequence section above.

### Sequence of Play: Battle — summary card (p. 122–123)

A condensed restatement of the Attack Phase / battle procedure already detailed above (Declaration → Infantry Maneuvers → Cavalry Maneuvers → Battles [Combat Segment action round, then Resolution: Force totals, winner destroys loser's side +2 Honor/card (ties: 1 Honor/card each, both sides destroyed), province destroyed if the margin exceeds its strength] → attacking units bow and return home, ineligible to fight again this Attack Phase → next battle → defenders return home once all battles are done). No new content beyond the full Attack Phase section above.

### Player Abilities — Action Phase & General (p. 124–125)

Consolidated summary card listing the universal Action-Phase abilities already detailed above: Equip (Limited), Lobby, Favor (Limited), Order Seppuku, Proclaim.

### Player Abilities relevant to Battle (p. 126–127)

Consolidated summary card listing the universal Battle-relevant abilities already detailed above: Tactical Advantage, Equip (Battle), Favor (Battle), Naval Invasion, Conquest.

### Card Type Icons (p. 128–129)

Visual-reference page (not rules text): the printed icon for each card type (Event, Celestial, Holding, Personality, Region, Follower, Item, Ring, Strategy), the nine Clan Mon icons, and the five Ring/element icons. No rules content beyond what's already stated per card type above.

### Rules-Relevant Keywords — index (p. 130–131)

A cross-reference index of every keyword that has an actual rules effect, each pointing to where its rule lives. Reproduced here as a checklist against this document's own coverage above:

| Keyword | Rule | Covered above |
|---|---|---|
| Armor | At most one per Personality | Glossary — Armor |
| Cavalry | Assigns during the Cavalry Maneuvers Segment | Attack Phase — C. Cavalry Maneuvers |
| Clan alignment | Enables Clan Discount / Clan Allegiance / Proclaim on entry | Dynasty Phase — a. |
| Conqueror | May perform Conquest | Glossary — Conqueror/Conquest |
| Courtier | May be targeted by Order Seppuku | Glossary — Seppuku |
| Duelist | Wins duel ties vs. non-Duelists; may re-focus before Focus Effects | Glossary — Challenge/Duel |
| Experienced | Deck-construction exception; enables overlay | Glossary — Experienced |
| Kensai | May attach two Weapons | Glossary — Kensai |
| Loyal | Only playable/controllable by a player sharing its Clan alignment | Glossary — Loyal |
| Naval | May perform Naval Invasion | Glossary — Naval |
| Samurai | May be targeted by Order Seppuku | Glossary — Seppuku |
| Shugenja | May be targeted by Order Seppuku; may attach Spells; may perform Battle Equip | Core Rules — Spell; Action Phase — Equip (Battle); Glossary — Seppuku |
| Singular | At most one copy in play per player | Glossary — Singular/Unique |
| "Soul of …" | Deck-construction equivalence with the named older card | Glossary — "Soul of …" |
| Tactician | May perform Tactical Advantage; capped at one Tactical action/turn | Glossary — Tactical/Tactician |
| Terrain | Discarded when its battle ends | Glossary — Terrain |
| Unique | At most one copy in a player's deck | Glossary — Singular/Unique; Deck Construction |
| Weapon | At most one per Personality (Kensai excepted) | Glossary — Weapon |

---

## Rules Supplements

Post-Emperor-Edition rulebook expansions each carried their own rules-relevant changes and card-specific rulings, layered on top of the core rules above rather than replacing them. Compiled here in release order, in the same reference style as the rest of this document — restated for lookup, not transcribed verbatim.

### Torn Asunder Supplement

**Stalwart** (new keyword). While a Personality has Stalwart, negate the first time each turn another player's card would bow him. If a Personality gains Stalwart after the turn has already started (i.e. he did not have it at the start of the turn), "first bowing" counts only from the point he gained the keyword onward — a bowing effect that already resolved against him earlier that turn is not retroactively negated.

**Family names.** Rule of thumb for every Emperor-legal card: if the first word of a Personality's two-word title is not an ordinary English word (e.g. "the," "Master") and the Personality carries a Clan alignment or a keyword ending in "Clan," that first word is the Personality's family name.

**Notes on specific cards:**
- **Blood Wave, Spiritual Coalescence**: the destroyed Personality's Force is counted *after* he is destroyed, so it excludes any Force bonus from his (now also gone) Items.
- **Earth's Protection**: the option to apply a bonus to your own province strength is not available while you are defending.
- **In the Right**: "up to half your Courtier's Chi, rounded down" limits the *number of attacking cards you may target*, not any other value.
- **Mirumoto Kojinrue**: his second trait does not override the general rule that abilities on bowed cards cannot be used.
- **Path to Inner Peace**: this is a new, distinct card — not a most-recent-printing update of the old "The Path to Inner Peace" (Forbidden Knowledge). The Tao recognizes more than one path to enlightenment; both cards exist independently.
- **Sycophants**: its second use is not granted to abilities that are already restricted to, e.g., once per phase, once per battle, or once per game — those caps still apply as printed.
- **The Tachikaze**: by the rules, you may inspect your own face-down cards under this card at any time.
- **Truce**: it can pay the cost for a Personality who holds your Clan alignment plus a second, different Clan alignment.

### Coils of Madness Supplement

Coils of Madness is the first set using Ivory Edition's "bugged" card format and terminology, previewing changes ahead of Ivory Edition itself. Unless noted, these rules apply only to cards from Coils of Madness and later sets — earlier cards keep working exactly as described in the core rules above.

**Major changes:**

1. Actions are now printed as `[Keywords] Designator, required costs: Effects block.` Both the action itself and its printed form are called "actions" — the term "ability" is no longer used on new cards, but existing rules-text that refers to "abilities" still applies to actions on cards printed under these rules.
2. The `Reaction:` designator is replaced by two new designators, **Engage** and **Interrupt**.
3. **Engage** plays exactly like a Reaction to the trigger "After engaging."
4. **Interrupt** plays exactly like a Reaction to the trigger "Before an action's effects resolve." When an Interrupt's text refers to "an Action," it means only the specific action being interrupted — e.g. `Interrupt: Negate the destruction of cards in this unit from another player's action` only negates destruction caused by the action being interrupted, not destruction from any other source.
5. Required costs are now shown graphically, and there are only two kinds: **bow this card** (a bowing-person icon) and **pay X Gold** (a coin icon with a number). The old Gold Cost icon on Strategies is superseded by this pay-Gold cost icon — a Strategy no longer needs its own separate Gold Cost stat once printed this way.
6. Everything else in the effects block (other than targeting) is a plain effect: failing to happen does not stop the rest of the action from resolving. Exception: if an effect needed some piece of information to occur and that information isn't available (e.g. the Focus Value of the top card of an empty deck, or the Force of a target that couldn't be legally targeted), every later effect in that action that depends on that same information also fails.
7. Required targeting has moved back into the effects block. Any target not marked "may" is still required, and — per Emperor Edition procedure — required targeting is still resolved during the targeting step, before costs are paid.
8. Conditional phrasing (e.g. "He may bow one of his Rings to draw a card") means the stated action is optional, and if taken, the following effect happens. In this example the Ring-bowing is not a cost — but if it fails for some reason, the dependent effect ("draw a card") does not happen either.
9. New keywords condense common modifier phrases, printed with reminder text in Coils of Madness:
   - **Absent**: the action may be taken even without controlling any unit at the current battlefield (i.e. without Presence).
   - **Home**: the action may be taken even while the card is at home rather than at the current battlefield.
   - **Repeatable**: the action may be taken any number of times per turn.
   - **Tireless**: the action may be taken even while the card is bowed.
   - **Expendable**: after an Expendable card is destroyed, its controller draws a card.
   - **Kharmic**: every player gains the universal ability `Repeatable Limited, 2 Gold: Discard a Kharmic card from your hand, or from your province if it's face-up. If you discarded a card from your hand, draw a card; otherwise, refill the province face-down.`
10. An effect with a bow-cost icon but no designator (including payment-reduction effects) triggers optionally, at the moment you produce Gold.

**Minor changes:**

11. Starting with Coils of Madness, Followers are printed with no Honor Requirement stat; under Emperor Edition rules such Followers have an Honor Requirement of zero.
12. "Equip," when it appears in an action's text (rather than as a Limited-Equip-referencing trait), means this sequence: target your Personality, target an attachment in your hand, and attach it to him, paying all costs. The Personality being equipped does not perform this action himself — but targeting him is still required and still resolved during the targeting step. Further action text can modify this base sequence (e.g. altering the cost).
13. Example: **Recruitment Station**'s action `Battle/Open, * Gold, [bow]: Equip a Follower, paying 2 less Gold` means: target your performing Personality and a Follower in your hand, pay that Follower's cost minus 2 Gold, and attach it to the Personality.
14. Triggered traits now end their trigger clause with a comma instead of a colon.
15. As a preview of Ivory Edition, rules-relevant keywords are printed in **boldface** in a card's keyword line; keywords with no rules effect remain unbolded.
16. "Base" (as a term) is replaced by "printed," with identical rules meaning.
17. "Attack" and "defend," applied to a Personality or unit, mean to be assigned to, enter play at, or move to a battlefield on the attacking or defending side respectively (attacking side = the side of the player who declared the attack; defending side = the side controlling the provinces under attack).
18. "Join" means to come under a player's control, whether by entering play or by a change of controller.
19. Negation/prevention language is now general: "cannot," "may not," "will not," "negate," and "it has no effect" all mean the described effect simply cannot happen for its stated duration. Scope still depends on exact wording — e.g. "He will not straighten" blocks every effect that would straighten him, while "the action's effects will not straighten him" blocks only that one action's straightening effect.
20. If an action "does not resolve," its effects never begin — they are not "prevented" or "negated," they simply never occur.
21. "Duel of (stat)" means a duel where both Personalities use the named stat as their duel stat (instead of the default Chi).
22. The Emperor Edition legality symbol moves from the lower-left to the lower-right of the card. Cosmetic only.

### Gates of Chaos Supplement

Under the coming Ivory Edition, Events will resolve differently than under Emperor Edition rules. As an interim rule, until the next base set becomes legal: every Event printed in Gates of Chaos, Coils of Madness, and (by extension) Aftermath resolves as though its text were a plain triggered trait with normal resolution effects — **ignore any printed `Limited:` designator on these Events.**

Additionally, for the Event **A Fever in the Blood** to function correctly under Emperor Edition rules, its text is temporarily treated (as a technical Most Recent Printing) as: *"Put this Event into play. Discard it after your next turn begins. While it is in play, Personalities and Followers have −1F while attacking."* This reverts to its originally printed text once the next base set becomes legal.

In the coming Ivory Edition, every card with a Gold Cost will enter play via an action, paying that cost through the action's own variable Gold-cost icon. Until then, for cards printed in Coils of Madness or later that bring a non-created card into play using the terms "equip" or "recruit," **ignore the variable Gold-cost icon** on that action (i.e. don't double-pay: pay the card's own Gold Cost as normal, not again via the icon).

**Recruit**: introduced in Gates of Chaos on the card **Yoritomo Mikaru**, this term means "bring a Dynasty card into play" — tied to the Gold-cost-via-action transition described above.

**New rules:**
- **Destined**: after your Destined card enters play, draw a Fate card. This draw is mandatory, not optional.
- **Fortification**: a Fortification Holding, when brought into play, attaches to the province it entered from — or, if it did not enter from a province, to any province you control. It still pays costs to enter play normally, but enters play **unbowed** (straightened), unlike other Holdings. It remains located in your home. If the province it's attached to is destroyed, the Fortification is discarded as part of that destruction, and this discard cannot be prevented.
- **Invest (X Gold)**: a special trait containing a Gold icon. The icon and colon do not mean the trait itself has a cost — they describe an optional additional cost you may pay after the card enters play. This additional cost is paid separately from the card's own entering-play cost, and can only be paid once per time the card enters play; if paid, the Invest trait's stated effect applies. A card that produces Gold restricted to a specific purpose (e.g. "for a Follower") can also use that Gold toward a card's Invest cost, if the Invest cost matches that purpose. Cost reductions do not apply to an Invest cost unless they specifically say so (as, for example, Daigotsu Kikumura's first trait does).

**Notes on specific cards:**
- **The Lost Colossus**: players other than its controller may target it with Political actions.
- **Ikoma Ayumu**: his trait reads, "the rulebook Tactical Advantage is an Open action for you if you choose Ayumu to perform it."

### Aftermath Supplement

**Reserve** (new keyword). Every player gains two universal abilities relevant to Reserve cards:
- `Repeatable Absent Battle: If it would be opposed, bring a face-up Reserve Personality from your province into play in your army at the current battlefield, paying all costs.`
- `Repeatable Battle: If it would be opposed, attach a Reserve attachment from your hand to your opposed target Personality at the current battlefield, paying all costs.`

**Discipline** (new special trait on Strategies, printed with a Gold-cost icon). You may play a card with Discipline from your discard pile to use one of its actions, paying the icon's Gold cost as an additional cost of that action (or, if the action already has its own Gold cost, adding the Discipline cost to it). After the action ends, remove the card from the game. Reminder: abilities/actions can normally only be used once per turn, so a Discipline card generally cannot be played from the discard pile on the same turn it was played from hand.

**Terminology**: "recruit" continues to mean "bring into play" (see Gates of Chaos, above). "FV" is shorthand for Focus Value, introduced in this set. On a Battle action, "this Province" means the current battlefield's province.

**Notes on specific cards:**
- **Ningen's "Treatise on the Nezumi"**: introduces the "multiplayer" keyword as a reminder that the ability is primarily useful in games with more than two players. The keyword itself has no rules effect.

### The Shadow's Embrace Supplement

**Notes on specific cards:**
- **Matsu Rika**: "After your Bushido Virtue action resolves, three times per turn: Give Rika +1F." **Bushido Virtue** is a plain printed keyword some Strategy cards carry (e.g. My Enemy's Mercy, alongside Compassion) — it is not a Designator-style action-type prefix like Political/Tactical. Whether an action "is a Bushido Virtue action" follows the same rule already stated for Political/Tactical/Recon/etc. (see Open Items, below, and Reading the Cards — Abilities, above): either the acting ability itself is expressly printed with that keyword before its designator, or the performing card carries the keyword itself, since an ability inherits all the keywords of the card it's on. Her bonus is capped at three times per turn — a per-turn cap on an otherwise mandatory-worded trigger, the same shape as Hida Mochitoko's own "twice per turn, after an action targets your Follower..." trait (Second City).

### Before the Dawn Supplement

**Notes on specific cards:**
- **Akodo Tezuka**: "Battle/Open: Discard a card and target a Bushido Virtue in your discard pile: Take an additional action to play it for one of its Battle, Limited, or Open actions, as appropriate to the current phase. Remove it from the game after its action ends." Unlike a narrower "take an additional action to play it for one of its Battle actions" grant (contrast Daigotsu Bofana - exp, Torn Asunder), this one admits any of the target card's actions, not Battle only. "As appropriate to the current phase" is not a special exception — each granted action still needs its own ordinary timing to be legal when actually taken: a Limited action only during the Action Phase as the active player, an Open action only during the Action Phase, a Battle action only during a battle's Combat Segment (see The Turn Sequence, above). So in practice, only the target's Battle action (if any) is ever usable while a battle is underway, and only its Limited/Open actions (if any) are usable outside one — the phase itself does the filtering, with nothing extra to track.

---

## Open items for future supplements

Noted while compiling this document, for whoever adds the next supplement:

- This rulebook's own **Contents** page (p. 4–5) lists further Online Rules sub-sections beyond p. 131 (multiplayer "Ally," draft/sealed variants) that live past this document's page-131 cutoff and were not compiled here.
- The rulebook frequently cross-references errata/FAQ content hosted at `rules.l5r.com`, which is not included in this compilation and would need its own source.
- Several mechanics described here by name only (Terrain, Reconnaissance, Political/Tactical/Recon/Ninja/Iaijutsu/Kiho/Thunder/Dark Virtue/Bushido Virtue action-type keywords, Clan Alignment as "Imperial," Madness tokens, Henshin) are defined at the *keyword* level in this rulebook but get their full behavioral texture from the specific cards that use them — a useful next supplement would be a per-mechanic deep-dive sourced from representative card text, similar to how `docs/rules/LION_CRANE_COVERAGE.md` already documents specific implemented cards.
- The **Rules Supplements** section above (Torn Asunder, Coils of Madness, Gates of Chaos, Aftermath, The Shadow's Embrace, Before the Dawn) was supplied directly by the rules-lane owner rather than sourced from this compilation's own rulebook PDF; later supplements (e.g. Ivory Edition itself, once the "coming edition" changes previewed above take final effect) are expected to land the same way.
