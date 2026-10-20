# Tutorial curriculum (issue #935)

**Where to pick this up:** the tracking issue is [#1035](https://github.com/l5r-emperor-dev/l5r-emperor/issues/1035), a checklist with one task issue per lesson (each with what it covers and how it starts). Play → Tutorial lists every lesson below; built ones start, the rest are dimmed placeholders that show what they will cover. The menu's own list is `src/L5R.Core/Tutorial/TutorialCurriculum.cs`: when you add a lesson, give it the same id there and in `TutorialLessons.All`.

The combined list of everything the tutorials must cover: Mendon's two lists, deduplicated, with the breakouts and additions agreed in discussion (2026-10-20). **Everything is unlocked from the start**; the menu groups lessons by track and marks a suggested start, it never locks one. Each lesson stands alone, so each brings its own table (`TutorialSetup`: decks, Stronghold, Honor, cards at home, deck tops, who goes first; see [TUTORIAL.md](TUTORIAL.md)) and opens with a one-screen "you need to know" recap of the vocabulary it assumes.

Budget per lesson: 15-30 steps, 3-5 minutes of play, headless play-through of 2 seconds or less. Stage the position instead of playing up to it. Teach one idea; split when a lesson wants more. "Source": **M** = on Mendon's list, **S** = suggested in review and not yet accepted.

Status: **done** = built and in a PR. Whether the engine supports every card a lesson stages must be checked when the lesson is written; the setup test checks only that the catalogue holds them.

## Track 1: The core loop

| # | Lesson | Covers | Staged table / decks | Source | Status |
|---|---|---|---|---|---|
| 1.1 | The first turn and a half | Turn sequence, Dynasty and Fate cards, Holdings and Gold, recruiting, Proclaim, Clan Discount, equipping, Limited/Open actions, attacking one province, battle, Incapacitated, destroying a province | Pinned Lion vs pinned Crane, from turn 1 | M | done (PR #1025) |
| 1.2 | Defense | Being attacked: assigning defenders, keeping one home, the counter-attack, first action of a battle | Pinned Crane defending against a Lion attack, mid-game | M | |
| 1.3 | Battle in depth | Force against Chi, destroying Personalities and the Dead pile, what happens when a province falls (Harsh Choices), end of battle | Staged battle with several units | S | |
| 1.4 | Hand limit and the End phase | Hand size, End-phase discard, drawing, Dynasty and Fate discard piles | Lion holding a full hand at its End phase | M | |
| 1.5 | Gold and payment | Gold production, excess Gold lost, Reaction Gold, Clan Allegiance, alternative costs | Stronghold plus mixed Holdings | S | |
| 1.6 | The Dynasty phase and province refresh | Refilling provinces, discarding a province card, face-up and face-down, the Border Keep refresh | Turn 1 position | S | |

## Track 2: Reading cards

| # | Lesson | Covers | Staged table / decks | Source | Status |
|---|---|---|---|---|---|
| 2.1 | Reading a card | Cost, Force, Chi, Honor Requirement, Personal Honor, Focus Value, text box, keywords, traits | Hand and board of mixed card types | S | |
| 2.2 | Traits and Abilities | Printed traits, abilities, ability types (Open, Limited, Reaction, Battle) | Cards with traits that change numbers | M | |
| 2.3a | Keywords: attack | Cavalry, Naval, Conqueror, Scout, Tactician | Attack with one of each | M | |
| 2.3b | Keywords: card | Kensai, Armor, Weapon, Duelist, Spell elements | Attachments and duelists | M | |
| 2.3c | Keywords: clan and rank | Clan keywords, Samurai/Courtier/Shugenja, Ancestor, Magistrate | Mixed Personalities | M, S | |
| 2.4 | Items and Followers | Attaching, Armor/Weapon limits, Followers' Force, destruction of attachments | Personality with equipment in hand | S | |
| 2.5 | Unbowed attachments on bowed cards | Why an attachment can stay unbowed when its host bows, and what that allows | Bowed Personality with attachments | M | |
| 2.6 | Created Cards | What a created card is, where it goes when it leaves play | A card that creates one | M | |

## Track 3: Special card types

| # | Lesson | Covers | Staged table / decks | Source | Status |
|---|---|---|---|---|---|
| 3.1 | Events | Revealed in the Events phase, resolved, discarded | An Event in a province | M | |
| 3.2 | Regions | Attached to a province, ongoing effect, what removes it | A Region revealed | M | |
| 3.3 | Celestials | Entering play, staying, their effects | A Celestial revealed | M | |
| 3.4 | Retainers | The Retainer card type and how it differs from a Personality | A Retainer recruited | M | |

## Track 4: Shugenja, Spells and Rings

| # | Lesson | Covers | Staged table / decks | Source | Status |
|---|---|---|---|---|---|
| 4.1 | Shugenja and Spells: attaching | Only Shugenja carry Spells, element keywords, cost | Shugenja in play, Spell in hand | M | |
| 4.2 | Shugenja and Spells: casting | Using a Spell in a battle, targeting | Staged battle | M | |
| 4.3 | Rings | How a Ring enters play (condition), discarding for Focus | A battle that satisfies a Ring's condition | M | |
| 4.4 | Enlightenment Victory | Controlling all five Rings at the start of your turn | Four Rings in play, one to go | M | |

## Track 5: Timing

| # | Lesson | Covers | Staged table / decks | Source | Status |
|---|---|---|---|---|---|
| 5.1 | Priority and passing | Who may act, what two passes in a row end, Open actions on the opponent's turn | Action phase, both players holding actions | S | |
| 5.2 | Reactions: the basics | A Reaction answers a trigger ("before", "after", "when") | An opponent's recruit with a Reaction in hand | M | |
| 5.3 | Reactions: answering | Responding in a battle, ordering, what a Reaction cannot do | Battle with a Reaction on each side | M | |
| 5.4 | Imperial Favor | The Favor, "Lobby", what holding it does | Favor claimed on a Lion turn | M | |

## Track 6: Honor

| # | Lesson | Covers | Staged table / decks | Source | Status |
|---|---|---|---|---|---|
| 6.1 | Honor: gain and loss | Where Honor comes from and goes, the Honor counter | Mid-game Honor swings | M | |
| 6.2 | Honor Requirement and Personal Honor | Which Personalities you can recruit, Proclaim revisited | Honor 4 against two recruits | S | |
| 6.3 | Dishonor, Rehonor and Seppuku | Dishonorable Personalities, restoring Honor, seppuku | Dishonored Personality in play | M | |
| 6.4 | Honor Victory and dishonor defeat | 40 Honor wins; -20 or lower loses at your next End phase, even if you recover | One player at 38 and one at -18 | M | |

## Track 7: Further rules (suggested)

| # | Lesson | Covers | Staged table / decks | Source | Status |
|---|---|---|---|---|---|
| 7.1 | Duels | Focus, Duelist tie-breaks | A duel offered in a battle | S | |
| 7.2 | Terrain, Kata, Overlays and Experienced Personalities | Only if the cards the engine implements use them | To be decided | S | |
| 7.3 | The Strongholds | One short lesson per clan: Gold, Province Strength, starting Honor, ability | Each clan's pinned Stronghold | S | |
| 7.4 | Winning and losing | Military, Honor, Enlightenment, Dishonor, conceding | One staged position per ending | S | |

## Track 8: Using the app

| # | Lesson | Covers | Source | Status |
|---|---|---|---|---|
| 8.1 | Card Library | Searching and reading the catalogue | M | |
| 8.2 | Deck Builder | Building and saving a deck, legality | M | |
| 8.3 | Starting a game | Practice against the bot, private rooms, Concede | S | |
| 8.4 | Settings and help | Reduced motion, Paused Animation, the rules help screens | S | |

## Open questions

1. Imperial Favor (5.4): taught before or after Honor? It affects play but is simple; it could move to Track 1.
2. Rings and the Enlightenment Victory (4.3, 4.4): kept as two lessons here because a natural five-Ring win cannot be staged in one.
3. Which Track 7 items are wanted at all (7.2 depends on the shipped cards)?
4. Track 8 runs in the app, not a match: does it use the coach column, or a separate guided tour?
