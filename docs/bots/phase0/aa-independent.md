# Bot tournament: champion (challenger) vs champion

- Mode **dev** (ordered pairs among the development decks; development seed range), pairing **independent**, 6 seed(s) x 72 ordered deck pairs x 2 seats = 864 games.
- Held-out decks (SHA-256 order, fixed): Allan Lion MCA, Mantis Manipulation, Unicorn Maidens.
- Decided 857 of 864; no winner 7. Wall clock 0:15:29 on 10 cores (0.93 games/s, 9.0 s per game on one core).

## Win rate

**Challenger won 408, champion won 449** = 47.6% (95% Wilson CI 44.3-51.0%). Exact two-sided sign test p = 0.1718; one-sided (challenger better) p = 0.9243.
- Paired by seed: challenger won both games of a pair 56, lost both 76, split 294. Exact McNemar (both vs neither) two-sided p = 0.0978.
- **Seat-0 advantage:** seat 0 won 432 of 857 decided games = 50.4% (95% CI 47.1-53.7%); subtract +0.4% from a seat-0 win rate to correct it. Challenger as seat 0: 206/429; as seat 1: 202/428.

## Beyond win rate (challenger minus champion, decided games)

- Provinces left margin -0.07; Honor margin +0.1; Personalities in play margin -0.12.
- Game length: mean turn 14.9 (challenger wins 15.0, losses 14.9); turn-limit games 0.
- Victory reasons: Military victory: 758; Honor victory at the start of the turn: 71; Dishonor defeat at the beginning of the End Phase: 28.
- **Soundness:** stalls 7, illegal 0, exceptions 0, decision cap 0, setup failures 0, out of time 0; games with a refused-then-retried action 91 (309 refusals, informational). The bar for promotion is zero in the first six.

## Per deck (challenger piloting the deck)

| Deck | Games decided | Challenger wins |
| --- | --- | --- |
| Crab Control | 93 | 74 (80%) |
| Crane Dominance | 96 | 55 (57%) |
| Crane preset | 96 | 41 (43%) |
| Dragon Destruction | 92 | 48 (52%) |
| Lion preset | 96 | 28 (29%) |
| Phoenix Talons | 96 | 17 (18%) |
| Scorpion Lethargy | 96 | 71 (74%) |
| Scottish Lion Deck | 96 | 21 (22%) |
| Spider Conquer | 96 | 53 (55%) |

## Power note

Independent games, exact binomial, two-sided alpha .05, 80% power: detect 55% needs about **783** decided games; 60% needs about **194**; 52% needs about 4904. At this run's throughput (0.93 games/s, 99% decided) that is 0:14:09 and 0:03:30 of wall clock. `same` pairing removes deck, seat and shuffle noise, so it needs fewer games when the bots differ; the A/A with `independent` pairing is the honest noise floor.
- FINDING Crab Control vs Dragon Destruction, seed 1000019, challenger seat 0: stall turn 10 seat 0 had no command: No complete legal action was supplied for this decision.
- FINDING Crab Control vs Dragon Destruction, seed 1500019, challenger seat 1: stall turn 8 seat 0 had no command: No complete legal action was supplied for this decision.
- FINDING Crab Control vs Dragon Destruction, seed 1510019, challenger seat 1: stall turn 17 seat 0 had no command: No complete legal action was supplied for this decision.
- FINDING Dragon Destruction vs Crab Control, seed 1020034, challenger seat 0: stall turn 10 seat 1 had no command: No complete legal action was supplied for this decision.
- FINDING Crab Control vs Dragon Destruction, seed 1030019, challenger seat 0: stall turn 11 seat 0 had no command: No complete legal action was supplied for this decision.
- FINDING Dragon Destruction vs Crab Control, seed 1540034, challenger seat 1: stall turn 15 seat 1 had no command: No complete legal action was supplied for this decision.
- FINDING Crab Control vs Dragon Destruction, seed 1550019, challenger seat 1: stall turn 6 seat 0 had no command: No complete legal action was supplied for this decision.
