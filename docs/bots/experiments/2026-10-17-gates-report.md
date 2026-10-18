# Bot tournament: pre-gates (challenger) vs champion

- Mode **dev** (ordered pairs among the development decks; development seed range), pairing **same**, 12 seed(s) (from index 100) x 72 ordered deck pairs x 2 seats = 1728 games.
- Held-out decks (SHA-256 order, fixed): Allan Lion MCA, Mantis Manipulation, Unicorn Maidens.
- Decided 1718 of 1728; no winner 10. Wall clock 1:20:33 on 16 cores (0.36 games/s, 40.4 s per game on one core).

## Win rate

**Challenger won 853, champion won 865** = 49.7% (95% Wilson CI 47.3-52.0%). Exact two-sided sign test p = 0.7907; one-sided (challenger better) p = 0.6231.
- Paired by seed: challenger won both games of a pair 32, lost both 39, split 783. Exact McNemar (both vs neither) two-sided p = 0.4767.
- **Seat-0 advantage:** seat 0 won 785 of 1718 decided games = 45.7% (95% CI 43.3-48.1%); subtract -4.3% from a seat-0 win rate to correct it. Challenger as seat 0: 389/858; as seat 1: 464/860.

## Beyond win rate (challenger minus champion, decided games)

- Provinces left margin +0.02; Honor margin +0.1; Personalities in play margin +0.05.
- Game length: mean turn 14.5 (challenger wins 14.5, losses 14.5); turn-limit games 0.
- Victory reasons: Military victory: 1451; Honor victory at the start of the turn: 166; Dishonor defeat at the beginning of the End Phase: 101.
- **Soundness:** bot stalls 0 (engine stalls, where the engine refused the only offered action: 0), illegal 0, exceptions 0, decision cap 0, setup failures 0, out of time 10; games with a refused-then-retried action 105 (396 refusals, informational). The bar for promotion is zero bot stalls and none of the others; engine stalls are reported to the rules lane.

## Per deck (challenger piloting the deck)

| Deck | Games decided | Challenger wins |
| --- | --- | --- |
| Crab Control | 187 | 70 (37%) |
| Crane Dominance | 191 | 98 (51%) |
| Crane preset | 191 | 110 (58%) |
| Dragon Destruction | 192 | 125 (65%) |
| Lion preset | 192 | 69 (36%) |
| Phoenix Talons | 190 | 50 (26%) |
| Scorpion Lethargy | 192 | 142 (74%) |
| Scottish Lion Deck | 191 | 59 (31%) |
| Spider Conquer | 192 | 130 (68%) |

## Power note

Independent games, exact binomial, two-sided alpha .05, 80% power: detect 55% needs about **783** decided games; 60% needs about **194**; 52% needs about 4904. At this run's throughput (0.36 games/s, 99% decided) that is 0:36:43 and 0:09:05 of wall clock. `same` pairing removes deck, seat and shuffle noise, so it needs fewer games when the bots differ; the A/A with `independent` pairing is the honest noise floor.
- FINDING Phoenix Talons vs Crab Control, seed 2000042, challenger seat 0: out of time turn 29 wall budget
- FINDING Scottish Lion Deck vs Crab Control, seed 2000058, challenger seat 1: out of time turn 24 wall budget
- FINDING Crab Control vs Phoenix Talons, seed 2010020, challenger seat 0: out of time turn 25 wall budget
- FINDING Phoenix Talons vs Crab Control, seed 2020042, challenger seat 0: out of time turn 27 wall budget
- FINDING Crab Control vs Crane Dominance, seed 2030018, challenger seat 1: out of time turn 26 wall budget
- FINDING Scottish Lion Deck vs Crab Control, seed 2070058, challenger seat 0: out of time turn 32 wall budget
- FINDING Phoenix Talons vs Crab Control, seed 2080042, challenger seat 1: out of time turn 27 wall budget
- FINDING Crab Control vs Crane preset, seed 2090017, challenger seat 0: out of time turn 24 wall budget
- FINDING Crane preset vs Crab Control, seed 2110009, challenger seat 0: out of time turn 28 wall budget
- FINDING Scottish Lion Deck vs Crab Control, seed 2110058, challenger seat 1: out of time turn 26 wall budget
