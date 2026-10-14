# Tutorial framework (issue #935)

**Decision (Mendon, 2026-10-13):** the tutorial is offline only, against the practice bot; illegal clicks are locked out; the final lesson unlocks everything and the player finishes the game against the bot.
Lesson 1 teaches cards and card types over the first two turns; the order and content follow Mendon's outline.

**Shape.**
- Lessons are scripts over the real engine (`L5R.Core/Tutorial`), not new rules and not new engine surface: the fixture is an ordinary prepared table, the lock is a filter on the options the engine offers plus a
  check on submitted commands, and the opponent's side is played by the script. Authoritative legality stays in Core; the client presents.
- Kept inside `L5R.Core` (as a folder, `internal` helpers on `MatchEngine`) rather than a new project, so it needs no new CI registration and reaches the engine's private turn start and draw without a public API.
- You are Lion (Halls of Memory, 7 Honor) against Crane (Shinden Asahina, 6 Honor), the pinned profile's classic pairing. Lion has the higher Honor, so you go first; the opponent is second and starts with the bowed Bamboo Harvesters. The opponent buys one Personality (Daidoji Kenshi) on its own turn, scripted, and defends with him on your turn two. (Revised twice at the rules lead's direction: first a Lion deck for the player, then Lion against Crane.)
- The Personality bought on turn one is Akodo Uehara (Lion, Samurai, Tactician): textbook trait plus printed action. He is "boxable": at the Clan discount he costs 3 Gold, exactly the Stronghold's production, so he is paid for with the Stronghold alone.
- No card that Oracle lists as banned (R-040: One Koku, Kalani's Landing, Caught Unawares, Cross-Clan Wedding, The Duty of the Crab, Well-Tended Farm) appears in a tutorial deck, so new players do not expect to meet them; a test enforces it. The Reaction example is Truce.
- Cards in the lesson are wired catalogue cards, not training fixtures: Willing Spirits (Event), Fallow Lands (Region), Megumi's Guidance (Celestial), Tell the Tale, Truce, Veteran Skirmishers, Light Armor,
  Ember's Final Fire, Ring of Fire, Crushing Strength.

**Interpretations to confirm.** "Boxable" Personality was read as "inexpensive and straightforward". The Event is Willing Spirits, which resolves when revealed but whose effect (+2 strength on each of your provinces)
lands at the start of the next turn; an Event with an immediate payoff that needs no choices could replace it. The lesson stops after the first destroyed province and the Ring of Fire reaction.
