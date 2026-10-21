# Tutorial curriculum (issue #935)

**Where to pick this up:** the tracking issue is [#1035](https://github.com/l5r-emperor-dev/l5r-emperor/issues/1035), a checklist with one task issue per lesson. Play → Tutorial lists every lesson below; a built one starts, the rest are dimmed placeholders. Hovering or focusing an entry shows its subtitle (the part after the colon below). The menu's own list is `src/L5R.Core/Tutorial/TutorialCurriculum.cs`: when you add a lesson, give it the same id there and in `TutorialLessons.All`.

The list was reset on 2026-10-21 by Mendon: the earlier 38-entry draft was dropped and only the first lesson kept; this is the new framework. **Everything is unlocked from the start.** Each lesson stands alone, so each brings its own table (`TutorialSetup`: decks, Stronghold, Honor, cards at home, deck tops, who goes first; see [TUTORIAL.md](TUTORIAL.md)). Budget: 15-30 steps, 3-5 minutes of play, headless play-through of 2 seconds or less; stage the position instead of playing up to it.

## Playing the Game

| Lesson (id) | Subtitle | Status |
|---|---|---|
| The Basics (`first-turn`) | Take a first turn as Lion against Crane: the phases, recruiting, Proclaim, equipping, attacking and destroying a province. | built (PR #1025) |

## Getting Started

| Lesson (id) | Subtitle |
|---|---|
| Events, Regions, and Celestials (`dynasty-deck`) | The rest of the Dynasty Deck |
| Recruiting Personalities (`recruiting`) | Clan Alignment, Honor Requirement, Recruitment Effects, Overlays |
| Spells and Shugenja (`spells`) | Attaching and using spells in the Action and Battle Phases |
| Attachments (`attachments`) | Using actions on attachments |
| Keywords (`keywords`) | Functional vs Non-functional Keywords |
| Traits and Actions (`traits-actions`) | Timing and limitations of Traits and Actions |
| Cost and Effect (`cost-effect`) | The difference between paying costs and applying effects |
| Special Actions - Attacks (`special-attacks`) | Melee and Ranged Attacks |
| Special Actions - Duels (`special-duels`) | Refusability, the Duel flow, and Focus Effects |
| Special Actions - Dishonor (`special-dishonor`) | Dishonorable status, Dying Dishonorable, Dishonorable Dead, Rehonor, Seppuku |
| Rings and Enlightenment (`rings`) | How to play a ring, How to win an Enlightenment Victory |
| Reactions (`reactions`) | Timing, Triggers, Effects |
| Imperial Favor (`imperial-favor`) | Lobbying and Using the Imperial Favor |

## Building a Deck

| Lesson (id) | Subtitle |
|---|---|
| Card Library (`card-library`) | |
| Deck Builder Overview (`deck-builder`) | How to use the Deck Builder |
| Anatomy of a Deck (`deck-anatomy`) | What is required and what is good practice |
| Using Your Designed Deck (`using-your-deck`) | |

The Building a Deck lessons that teach a screen (Card Library, Deck Builder Overview) run in the app rather than in a match; how they are presented (coach column or guided tour) is not decided.
