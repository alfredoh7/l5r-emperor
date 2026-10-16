# Archetype S0 — can a deck's own cards tell the bot how it wins? (issue #963)

No bot behaviour changes in this step. `DeckArchetype.Classify` (`src/L5R.AI/DeckArchetype.cs`) reads only the bot's own decklist (printed card text and counts) and is not wired into any decision yet.
Validation data: the 1,224 bot-vs-bot games of the promoted-champion re-baseline (dev seeds 60-65, held-out seeds 60-62), counted by how each decided game ended. Realised routes depend on the current bot's play, so they
show what the bot does with each deck today, not the ceiling of the deck.

## Rule

Per 40 cards of the Dynasty and Fate decks: **H** = Honor the deck's cards gain for their controller (printed "gain N Honor"), **L** = Honor an opponent is made to lose ("controller/player loses N Honor", "gains or loses"
split in half), **C** = Honor the deck's own cards make it lose ("Lose N Honor" costs). Military is the fallback for every deck.

- Honor gained as a **rider on a Battle action** counts half (it is earned by fighting); standalone honor (holdings, Open abilities, events, the stronghold) counts in full.
- Dishonor primary: L >= 15 and L > H. Honor primary: H >= 15. Honor secondary (military primary): 5 <= H < 15. Otherwise military only.
- "Dishonor" a Personality is a control tool (it makes a Personality weaker and killable), not the dishonor victory; it is not counted. Crane Dominance dishonors a lot and never won that way.

## Result against the labels (12 pool decks; your labels are per clan, the pool has one or more lists per clan)

| Deck | H | L | C | Classified | Your label | Realised wins (mil / honor / dishonor) | Verdict |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Crab Control | 7.9 | 20.0 | 0 | Dishonor | Dishonor | 23 / 0 / **64** | agree (73% of wins) |
| Unicorn Maidens | 25.9 | 4.4 | 5.9 | Honor, then military | Honor, then military | 10 / **71** / 0 | agree (88% honor) |
| Dragon Destruction | 0.9 | 0 | 0 | Military | Military | 130 / 3 / 0 | agree |
| Mantis Manipulation | 0 | 0 | 0 | Military | Military | 72 / 1 / 0 | agree |
| Scorpion Lethargy | 0 | 3.3 | 1.9 | Military | Military | 160 / 2 / 0 | agree |
| Spider Conquer | 1.5 | 0 | **40.0** | Military | Military | 163 / 0 / 0 | agree (40 Honor of own costs per 40 cards) |
| Scottish Lion Deck | 5.7 | 0 | 0 | Military, then honor | Military, then honor | 53 / 32 / 0 | agree (38% honor) |
| Allan Lion MCA | 6.0 | 0 | 0 | Military, then honor | Military, then honor | 26 / 10 / 0 | agree |
| Lion preset | 0.5 | 0 | 0 | Military | Military, then honor | 88 / 3 / 0 | differs: the preset has almost no honor cards |
| Crane preset | 3.5 | 0 | 0 | Military | Honor, then military | 129 / 0 / 0 | differs: the preset has almost no honor cards |
| Crane Dominance | 9.8 | 11.0 | 0 | Military, then honor | Honor, then military | 84 / 30 / 0 | differs in order: military is 74% of its wins |
| Phoenix Talons | 13.7 | 4.2 | 0 | Military, then honor | Military, then honor | 41 / 29 / 0 | agree (59% military, 41% honor) |

Primary route agrees on 10 of 12 decks after the Phoenix revision (below). The two differences are a clan label that the list does not support (Crane preset) and Crane Dominance's order, both disputed in the next section and accepted by Mendon; the Lion preset differs only in having no honor secondary.

## Where I disagree with the labels, and why

1. **Crane and Lion labels are clan-level, the lists are not.** The Crane and Lion *presets* contain almost no honor-gain text (H 3.5 and 0.5) and never win on honor in the data. A bot that read the clan would
   try to sprint with a deck that cannot. Reading the list is the right unit, which is why the classifier takes cards, not names.
2. **Crane Dominance is military-first in practice.** Honor 11.5 is mid-density and 74% of its wins are military. Its large dishonor output (11.0) is a control tool and produced no dishonor victory.
3. **Phoenix** looked honor-first on paper (18.1) but wins military 59% of the time, because most of its honor is a rider on battle spells. Revision (Mendon accepted, label kept): battle-rider honor counts half, which moves Phoenix to 13.7, military with an honor secondary. Unicorn drops from 39.5 to 25.9 and stays honor primary; Scottish Lion (5.7) and Allan Lion (6.0) stay secondary only because the secondary threshold moved from 6 to 5, a fit to these decks.
4. **The sort works as a continuous score better than as primary/secondary.** Honor density (before the rider revision) against honor share of wins: Unicorn 39.5 -> 88%, Phoenix 18.1 -> 41%, Crane Dominance 11.5 -> 26%, Scottish Lion 9.4 -> 38%,
   Allan Lion 7.5 -> 28%, Crane preset 3.5 -> 0%, Dragon 0.9 -> 2%, Lion preset 0.5 -> 3%. Monotone apart from Scottish Lion. S1 should use density as a weight, not a switch.
5. **"Military" needs no metric of its own**, as you suggested: a deck with no honor or dishonor engine wins militarily. Force-per-gold was a poor signal; Scorpion (Force-light, wins 160 of 162 military) and Spider (honor-poor, 163 of 163) show
   why. The one military sub-signal the data does support is **C**: Spider's Honor cost (40 per 40 cards) means it cannot also be an honor deck, and a bot that knows that can stop treating a low Honor total as an emergency.
6. **No enlightenment deck exists in the pool**, so the classifier has no enlightenment route yet.

## Not yet validated

- Realised routes are for the current bot. A better honor or dishonor player may lift Crane Dominance or Phoenix; S1 is the test of that.
- Primary route agrees on 10 of 12 decks and the classifier output matches the realised routes except where noted; whether acting on a label helps can only be shown by held-out games, not by this table.
- 12 decks, and the thresholds (15, 6) were set while looking at these 12. Treat them as a design, not a measured result, and check them against the held-out decks' realised routes (Allan Lion MCA, Mantis Manipulation and Unicorn Maidens, which are consistent with the table above, but they were in the thresholds' view too).
