# Blocker categories for the unwired cards (issue #498)

Mendon asked for the unwired and blocked cards of Before the Dawn, Seeds of Decay, Embers of War, Second City, Forgotten Legacy, The Shadow's Embrace, Emperor Edition, Honor and Treachery and Emperor Edition Gempukku to be grouped by similar blocker. Source: the `Blocker` column of `docs/CARD_INVENTORY.xlsx` after PR #498 (each card now also carries a `Blocker Category` column, from `tools/content/blocker_categories.json`).

**176 unwired cards** in scope (Gempukku has none; Second City 1, Honor and Treachery 1, Emperor Edition 1). Each card has one primary category; where a card has several independent gaps the category is the one that decides whether it can ship first, and the others are in its inventory note.

| Category | Cards | What is missing |
|---|---|---|
| **WIRE** Wirable with existing primitives (deferred, not blocked) | 10 | The blocker note says no genuine engine gap remains; the card was left for lack of time or to keep a card whole. Cheapest wins. |
| **SMALL** Wirable with one small, local extension | 7 | One narrow addition (a mandatory targeted Reaction shape, a Ranged-only narrowing, a chooseable performer, a new lock kind, a per-Tactician cap). |
| **SCAN** Created card needs a Proxy scan | 12 | Mechanically wirable, but the token/Proxy it creates has no published CreatedOnly scan (see the Proxy receipts for the closest printed proxy where one exists). |
| **SCAN2** The card's own scan is missing or unpublishable | 2 | No scan in the repository, so the card cannot be packaged whatever the engine can do. |
| **CONTROL** Control of an in-play Personality changes hands | 6 | Deliberately not built (docs/decisions/2026-09-12-unique-singular-control-transfer.md); needs a rules decision plus a timed return of control. |
| **PROVINCE** Province model: gained/created provinces, two-card provinces, face-up refills, per-province marks | 10 | Provinces are a fixed, position-indexed list of four, each holding one card refilled face-down; these cards add, mark, or refill them differently. Plains of Otosan Uchi waits for Alfredo's view work (issue #499). |
| **PROVENANCE** Entry provenance and 'when did it enter play' records | 4 | Nothing records which province or path a card entered play from, or when. |
| **ZONE** Zone-model gaps: under-card zone, dead pile as an action source, unit moved to deck, actions from hand | 9 | The engine has no zone for cards under another card, cannot use a Dead card as an action source or recruit destination, or move a whole in-play unit into decks. |
| **WATCH** One-shot or persistent watches and shields on future events | 8 | 'Negate the next ...', 'after the next time ...', until-used protections and game-long triggers. The delay store models effects that already happened, not standing watches. |
| **TRIG-ANN** Announcement and interrupt windows | 8 | Reactions before an action resolves against an untargeted card, on announcing an own action, before an Event resolves, before an Honor gain, or redirects with deferred tails. |
| **TRIG-AFTER** After-event trigger points that are not narrated | 12 | No Reaction window (or fact record) for a bow, a move, an assignment, an engage at a given battlefield, a Ring/Henshin entering, an entry seen by the other player, an own-won duel with a target, a Ring bowed by an action, a start-of-turn trait. |
| **PRIORITY** First-opportunity, priority and Reaction-lockout overrides | 3 | First chance to take a Battle action, forced performers, closing the other player's side of a window, standing rules about other Reactions. |
| **ADDACT** Additional actions beyond one Battle action | 5 | Additional-action grants are consumed only by Battle actions and are scoped by card id, not by ability class, phase or performer. |
| **COST** Costs: optional, alternative, conditional and payment-source models | 16 | Optional costs that feed the effect, chosen-card-of-a-slot costs, entry costs, the Imperial Favor discard as a first-class alternative cost, Reaction gold sources that do not bow, payment-time discounts, restriction overrides. |
| **TARGET** Target and slot model: new categories, counts, comparisons, mappings | 14 | Gold-cost, keyword-count and aggregate-stat comparisons; conditional slot counts and follow-ups; two independent targets; per-item mappings; targets chosen after a resolution; multi-choice actions. |
| **TOKEN** Token model | 3 | Tokens with two stats, a new token kind, or tokens as targets. |
| **DUEL** Duel model extensions | 4 | Replacing the Focus decision, swapping the duel stat live, ending a duel without resolution, substituting a duelist, Focus Effects that rewrite costs. |
| **STAT** Stat and modifier pipeline scopes | 7 | Controller-wide or battle-wide Attack strength, defender-side Attack modifiers, army-conditional durations, Gold Cost floors, raised Honor Requirements, set-to-value province strength. |
| **COUNTER** Per-turn and per-phase records and counters | 3 | Recon actions taken, actions that put cards in hand, Dark Virtue actions this phase, two independent turn conditions, Family Honor as a branch value. |
| **HIDDEN** Hidden-information primitives | 5 | Looking at an opponent's hand, a random hidden card, revealing or addressing a face-down province card. |
| **GRANT** Granted abilities and traits beyond one controller's cards | 4 | Abilities granted to every player, timed player abilities, Kata that grant a player Reaction, granted traits, Tireless granted to another card's abilities. |
| **RULEBOOK** Changes to rulebook actions | 3 | A card alters a core action: Equip/attach from a discard pile, a second Conquest per turn, Equip timing. |
| **RULES** Rules-lane ruling or conflict with the core rules needed first | 2 | Undefined terms (custody, the Cavalry player ability), text that conflicts with the Emperor province model, or ambiguity Mendon must resolve. |
| **LOCK** Movement, assignment and phase-structure rules | 4 | A whole Attack Phase inside the Action Phase, attack obligations, assignment restrictions by side, global movement negation, assigning after engage. |
| **CREATE** Created or borrowed cards with timed removal or wider copying | 6 | Removal at end of turn or next turn start rather than end of battle, copying keyword sets, Holdings, or abilities, temporary recruits, returning a removed unit. |
| **SEARCH** Search and look extensions | 4 | Criteria against a performer's keywords or an ability's keyword, look-then-attach, conditional discard after a look, and a face-up province refill from a search. |
| **HONOR** Honor and hand-discard plumbing | 3 | Immunity from other players' hand-discard effects (needs a 'caused by' parameter), two-condition Honor-loss immunity, per-phase hand-put caps, Family Honor. |
| **EFFECT** New effect primitives | 2 | Swap locations of two Personalities; set a Province's strength to an absolute value. |

## Suggested order of work

1. **WIRE** (10) and **SMALL** (7): 17 cards with no or one narrow extension.
2. **SCAN** (12) and **SCAN2** (2): content only; the Proxy scans for the 12 created cards (or the closest existing printed proxy, e.g. Readied Blade for Vanquisher's Blades, Emerald Magistrate for Fruitless Search) unblock them at once.
3. The shared engine gaps that each unlock a cluster: **TRIG-AFTER/TRIG-ANN** (20 cards, new Reaction windows), **TARGET** (14, gold-cost and count primitives shared by Games of Will, Power of Strength, The Heir's Wrath), **COST** (16, the Favor discard as a first-class alternative cost), **WATCH** (8, one-shot watches).
4. Rules-lane decisions before any code: **CONTROL** (6), **RULES** (2), **RULEBOOK** (3), and Alfredo's view work for **PROVINCE** (10, issue #499).

## Categories

### WIRE: Wirable with existing primitives (deferred, not blocked) (10)

The blocker note says no genuine engine gap remains; the card was left for lack of time or to keep a card whole. Cheapest wins.

- Asako Megu (EoW, Personality)
- Bayushi Nomen - exp2 (EoW, Personality)
- Damning Ceremony (EoW, Spell)
- Extending Influence (EoW, Spell)
- Hida Bakishi (EoW, Personality)
- Hida Osote (EoW, Personality)
- Ikoma Hakige (EoW, Personality)
- Isawa Koiso (EoW, Personality)
- Itoku (EoW, Personality)
- Mara's Touch (EoW, Spell)

### SMALL: Wirable with one small, local extension (7)

One narrow addition (a mandatory targeted Reaction shape, a Ranged-only narrowing, a chooseable performer, a new lock kind, a per-Tactician cap).

- Akodo Tamisu (EoW, Personality)
- Hotei's Contentment (EoW, Celestial)
- Isawa Sanetomo (EoW, Personality)
- Ochiai's Bravery (EoW, Spell)
- Seeking the Question (EoW, Strategy)
- Tsuruchi Shichiro (EoW, Personality)
- Moto Taigo (SoD, Personality)

### SCAN: Created card needs a Proxy scan (12)

Mechanically wirable, but the token/Proxy it creates has no published CreatedOnly scan (see the Proxy receipts for the closest printed proxy where one exists).

- Kitsune Haruki (EoW, Personality)
- Fruitless Search (FL, Strategy)
- Grateful Reward (FL, Strategy)
- Kitsune Hisano - exp (FL, Personality)
- Sword for Hire (FL, Strategy)
- Humble Farm (SE, Holding)
- Kakita Saki (SE, Personality)
- Kitsune Kichi (SE, Personality)
- Natural Aviary (SE, Region)
- Our Ancestor's Call (SE, Spell)
- Vanquisher's Blades (SE, Item)
- Yari of Air - exp (SE, Spell)

### SCAN2: The card's own scan is missing or unpublishable (2)

No scan in the repository, so the card cannot be packaged whatever the engine can do.

- Perfect Sacrifice (EoW, Strategy)
- Crippled Sensei (SE, Strategy)

### CONTROL: Control of an in-play Personality changes hands (6)

Deliberately not built (docs/decisions/2026-09-12-unique-singular-control-transfer.md); needs a rules decision plus a timed return of control.

- Yoritomo Sachina - exp (BtD, Personality)
- Shadow Dragon - exp2 (EoW, Personality)
- The Dark Naga (FL, Personality)
- Black Marketeering (SoD, Strategy)
- Goju Asuka (SoD, Personality)
- Only a Shadow (SoD, Strategy)

### PROVINCE: Province model: gained/created provinces, two-card provinces, face-up refills, per-province marks (10)

Provinces are a fixed, position-indexed list of four, each holding one card refilled face-down; these cards add, mark, or refill them differently. Plains of Otosan Uchi waits for Alfredo's view work (issue #499).

- Daigotsu Susaiken (EoW, Personality)
- Illusory Defense (EoW, Spell)
- In Search of Guidance (EoW, Event)
- Shosuro Ietsuno (EoW, Personality)
- Plains of Otosan Uchi (EE, Region)
- Dark Experiments (SoD, Event)
- Mining Apparatus (SoD, Item)
- Otomo Suikihime (SoD, Personality)
- Distant Expansion (SE, Event)
- Kuni Renyu (SE, Personality)

### PROVENANCE: Entry provenance and 'when did it enter play' records (4)

Nothing records which province or path a card entered play from, or when.

- Headbutt (BtD, Strategy)
- Hida Yamadera (BtD, Personality)
- Raido no Oni (BtD, Personality)
- Unsettling Doubts (SoD, Strategy)

### ZONE: Zone-model gaps: under-card zone, dead pile as an action source, unit moved to deck, actions from hand (9)

The engine has no zone for cards under another card, cannot use a Dead card as an action source or recruit destination, or move a whole in-play unit into decks.

- Shiba Jouta - exp (BtD, Personality)
- Tsubute (BtD, Item)
- Riddle of Rebirth (EoW, Strategy)
- Tokiko (EoW, Personality)
- Blessings of the Shi-Tien Yen-Wang (SC, Celestial)
- Daigotsu Yutaka (SoD, Personality)
- Learning (SoD, Strategy)
- Step Through the Void (SoD, Spell)
- The New Foundries (SE, Stronghold)

### WATCH: One-shot or persistent watches and shields on future events (8)

'Negate the next ...', 'after the next time ...', until-used protections and game-long triggers. The delay store models effects that already happened, not standing watches.

- Drawing on the Mountain (BtD, Spell)
- Firm Censure (BtD, Strategy)
- No Time for Games (BtD, Strategy)
- Scars of War (BtD, Strategy)
- Show of Restraint (BtD, Strategy)
- Ride the Breeze (EoW, Strategy)
- The Menacing Sky (EoW, Spell)
- The Snow Has Teeth (EoW, Strategy)

### TRIG-ANN: Announcement and interrupt windows (8)

Reactions before an action resolves against an untargeted card, on announcing an own action, before an Event resolves, before an Honor gain, or redirects with deferred tails.

- Destructive Priorities (BtD, Strategy)
- Forging Destiny (BtD, Strategy)
- Hida Fujita (BtD, Personality)
- Ikoma Ayumu (BtD, Personality)
- Iron Will (BtD, Strategy)
- Request Authorization (EoW, Strategy)
- Utaku Lishan (EoW, Personality)
- Shosuro Aroru - exp2 (FL, Personality)

### TRIG-AFTER: After-event trigger points that are not narrated (12)

No Reaction window (or fact record) for a bow, a move, an assignment, an engage at a given battlefield, a Ring/Henshin entering, an entry seen by the other player, an own-won duel with a target, a Ring bowed by an action, a start-of-turn trait.

- Daigotsu Miki (BtD, Personality)
- Erosion (BtD, Spell)
- Hida Defenders (BtD, Follower)
- Utaku Tairu - exp (BtD, Personality)
- Vigilant Riders (BtD, Follower)
- Asako Kikugoro (EoW, Personality)
- Centering the Soul (EoW, Spell)
- Daidoji Akeha (EoW, Personality)
- Daigotsu Tenbatsu (EoW, Personality)
- Deep Snows (EoW, Strategy)
- Kakita Kenta (EoW, Personality)
- Easy Persuasion (SE, Strategy)

### PRIORITY: First-opportunity, priority and Reaction-lockout overrides (3)

First chance to take a Battle action, forced performers, closing the other player's side of a window, standing rules about other Reactions.

- Shosuro Toson - exp2 (BtD, Personality)
- Inexorable (EoW, Strategy)
- Thunder Dragon - exp2 (SoD, Personality)

### ADDACT: Additional actions beyond one Battle action (5)

Additional-action grants are consumed only by Battle actions and are scoped by card id, not by ability class, phase or performer.

- Beyond the Line (BtD, Strategy)
- Mirumoto Inokuchi (BtD, Personality)
- One Action, Two Strikes (BtD, Strategy)
- String of Victories (BtD, Strategy)
- Isawa Momoko (SoD, Personality)

### COST: Costs: optional, alternative, conditional and payment-source models (16)

Optional costs that feed the effect, chosen-card-of-a-slot costs, entry costs, the Imperial Favor discard as a first-class alternative cost, Reaction gold sources that do not bow, payment-time discounts, restriction overrides.

- Ascending the Ranks (BtD, Strategy)
- En'you (BtD, Personality)
- Fury of a Mob (BtD, Strategy)
- Isawa Sakonoko (BtD, Personality)
- Seaside Bazaar (BtD, Holding)
- Temple of the Seven Fortunes (BtD, Region)
- Yoritomo Singh - exp2 (BtD, Personality)
- Daigotsu Kazuko (EoW, Personality)
- Greasing the Wheels (EoW, Strategy)
- Ikoma Shika - exp (EoW, Personality)
- Kakita Kae (EoW, Personality)
- Moto Rani - exp (EoW, Personality)
- The Heir's Wrath (EoW, Strategy)
- The Host's Advantage (EoW, Strategy)
- The Marshalling Fields (EoW, Stronghold)
- Zlkyt (SE, Personality)

### TARGET: Target and slot model: new categories, counts, comparisons, mappings (14)

Gold-cost, keyword-count and aggregate-stat comparisons; conditional slot counts and follow-ups; two independent targets; per-item mappings; targets chosen after a resolution; multi-choice actions.

- Ikoma Shinohara (BtD, Personality)
- Bayushi Tenzan (EoW, Personality)
- Games of Will (EoW, Strategy)
- Gozaru no Oni (EoW, Personality)
- Pillar Tattoo (EoW, Strategy)
- Power of Strength (EoW, Strategy)
- Preceding Reputation (EoW, Strategy)
- Secret Reserve (EoW, Strategy)
- Shinjo Sarang (EoW, Personality)
- Too Much for Mortals (EoW, Strategy)
- Yojimbo Assemble (EoW, Strategy)
- Asset Denial (FL, Strategy)
- Tsuruchi Nobukatsu (FL, Personality)
- Regret (SE, Strategy)

### TOKEN: Token model (3)

Tokens with two stats, a new token kind, or tokens as targets.

- The Agony of Her Gaze (BtD, Event)
- Isawa Mina (EoW, Personality)
- Shosuro Keirei (SE, Personality)

### DUEL: Duel model extensions (4)

Replacing the Focus decision, swapping the duel stat live, ending a duel without resolution, substituting a duelist, Focus Effects that rewrite costs.

- A Champion in Court (BtD, Strategy)
- Kakita Gonji (EoW, Personality)
- Killing Intent (EoW, Strategy)
- Turning Away (SoD, Strategy)

### STAT: Stat and modifier pipeline scopes (7)

Controller-wide or battle-wide Attack strength, defender-side Attack modifiers, army-conditional durations, Gold Cost floors, raised Honor Requirements, set-to-value province strength.

- Brothers in Battle (BtD, Strategy)
- Kaiu Nakano (BtD, Personality)
- Armor of the Uruwashii (EoW, Item)
- Bayushi Kazutoshi (EoW, Personality)
- Growing Hostility (EoW, Event)
- Ikoma Yamahatsu (EoW, Personality)
- Surrounded! (EoW, Strategy)

### COUNTER: Per-turn and per-phase records and counters (3)

Recon actions taken, actions that put cards in hand, Dark Virtue actions this phase, two independent turn conditions, Family Honor as a branch value.

- Broken Cipher (EoW, Strategy)
- Daidoji Narizane (EoW, Personality)
- Indecision (EoW, Strategy)

### HIDDEN: Hidden-information primitives (5)

Looking at an opponent's hand, a random hidden card, revealing or addressing a face-down province card.

- Disrupting Communication (BtD, Strategy)
- The Price of Honor (BtD, Strategy)
- Inciting Mistrust (EoW, Strategy)
- Scouting the Pass (EoW, Strategy)
- Petty Squabbles (FL, Strategy)

### GRANT: Granted abilities and traits beyond one controller's cards (4)

Abilities granted to every player, timed player abilities, Kata that grant a player Reaction, granted traits, Tireless granted to another card's abilities.

- Kakita Nara (BtD, Personality)
- The Bite of Winter (EoW, Strategy)
- Economic Repercussions (FL, Strategy)
- Starting on the Path (SoD, Event)

### RULEBOOK: Changes to rulebook actions (3)

A card alters a core action: Equip/attach from a discard pile, a second Conquest per turn, Equip timing.

- Never Enough Soldiers (BtD, Event)
- Seek the Stain (BtD, Spell)
- The Plain of Glass (EoW, Stronghold)

### RULES: Rules-lane ruling or conflict with the core rules needed first (2)

Undefined terms (custody, the Cavalry player ability), text that conflicts with the Emperor province model, or ambiguity Mendon must resolve.

- Wanted for Questioning (FL, Strategy)
- Incredible Tragedy (SoD, Event)

### LOCK: Movement, assignment and phase-structure rules (4)

A whole Attack Phase inside the Action Phase, attack obligations, assignment restrictions by side, global movement negation, assigning after engage.

- Hunger (BtD, Item)
- Too Close to Home (BtD, Event)
- Shinjo Hayan (EoW, Personality)
- Aramasu's Legacy (SoD, Stronghold)

### CREATE: Created or borrowed cards with timed removal or wider copying (6)

Removal at end of turn or next turn start rather than end of battle, copying keyword sets, Holdings, or abilities, temporary recruits, returning a removed unit.

- Amazing Feat (BtD, Strategy)
- Daigotsu Yajinden - exp3 (BtD, Personality)
- Stolen Merchandise (BtD, Holding)
- Perplexing Guests (EoW, Strategy)
- Searching for Answers (EoW, Strategy)
- Two-Fold Virtue (SE, Strategy)

### SEARCH: Search and look extensions (4)

Criteria against a performer's keywords or an ability's keyword, look-then-attach, conditional discard after a look, and a face-up province refill from a search.

- The Tapestry Perceived (BtD, Spell)
- Delving into History (EoW, Spell)
- Gaining Advantage (EoW, Event)
- Bayushi Nitoshi - inexperienced (H&T, Personality)

### HONOR: Honor and hand-discard plumbing (3)

Immunity from other players' hand-discard effects (needs a 'caused by' parameter), two-condition Honor-loss immunity, per-phase hand-put caps, Family Honor.

- Honest Assessment (EoW, Strategy)
- Pillowbook (EoW, Item)
- The Scorpion Wall is Finished (EoW, Event)

### EFFECT: New effect primitives (2)

Swap locations of two Personalities; set a Province's strength to an absolute value.

- Spirit of the Shadows (BtD, Strategy)
- Shrine to Nimuro (EoW, Holding)
