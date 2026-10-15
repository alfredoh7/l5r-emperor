# Bot tournament: champion (challenger) vs champion

- Mode **dev** (ordered pairs among the development decks; development seed range), pairing **independent**, 6 seed(s) (from index 6) x 72 ordered deck pairs x 2 seats = 864 games.
- Held-out decks (SHA-256 order, fixed): Allan Lion MCA, Mantis Manipulation, Unicorn Maidens.
- Decided 859 of 864; no winner 5. Wall clock 0:15:56 on 10 cores (0.90 games/s, 9.4 s per game on one core).

## Win rate

**Challenger won 425, champion won 434** = 49.5% (95% Wilson CI 46.1-52.8%). Exact two-sided sign test p = 0.7849; one-sided (challenger better) p = 0.6335.
- Paired by seed: challenger won both games of a pair 67, lost both 72, split 288. Exact McNemar (both vs neither) two-sided p = 0.7345.
- **Seat-0 advantage:** seat 0 won 408 of 859 decided games = 47.5% (95% CI 44.2-50.8%); subtract -2.5% from a seat-0 win rate to correct it. Challenger as seat 0: 202/430; as seat 1: 223/429.

## Beyond win rate (challenger minus champion, decided games)

- Provinces left margin +0.08; Honor margin -0.1; Personalities in play margin +0.08.
- Game length: mean turn 14.8 (challenger wins 14.6, losses 14.9); turn-limit games 0.
- Victory reasons: Military victory: 757; Honor victory at the start of the turn: 70; Dishonor defeat at the beginning of the End Phase: 32.
- **Soundness:** stalls 5, illegal 0, exceptions 0, decision cap 0, setup failures 0, out of time 0; games with a refused-then-retried action 72 (251 refusals, informational). The bar for promotion is zero in the first six.

## Per deck (challenger piloting the deck)

| Deck | Games decided | Challenger wins |
| --- | --- | --- |
| Crab Control | 93 | 72 (77%) |
| Crane Dominance | 96 | 57 (59%) |
| Crane preset | 96 | 42 (44%) |
| Dragon Destruction | 94 | 56 (60%) |
| Lion preset | 96 | 30 (31%) |
| Phoenix Talons | 96 | 17 (18%) |
| Scorpion Lethargy | 96 | 71 (74%) |
| Scottish Lion Deck | 96 | 22 (23%) |
| Spider Conquer | 96 | 58 (60%) |

## Power note

Independent games, exact binomial, two-sided alpha .05, 80% power: detect 55% needs about **783** decided games; 60% needs about **194**; 52% needs about 4904. At this run's throughput (0.90 games/s, 99% decided) that is 0:14:31 and 0:03:35 of wall clock. `same` pairing removes deck, seat and shuffle noise, so it needs fewer games when the bots differ; the A/A with `independent` pairing is the honest noise floor.
- FINDING Crab Control vs Dragon Destruction, seed 1060019, challenger seat 0: stall turn 11 seat 0 had no command: No complete legal action was supplied for this decision.
- FINDING Dragon Destruction vs Crab Control, seed 1560034, challenger seat 1: stall turn 11 seat 1 had no command: No complete legal action was supplied for this decision.
- FINDING Crab Control vs Dragon Destruction, seed 1590019, challenger seat 1: stall turn 7 seat 0 had no command: No complete legal action was supplied for this decision.
- FINDING Dragon Destruction vs Crab Control, seed 1600034, challenger seat 1: stall turn 21 seat 1 had no command: No complete legal action was supplied for this decision.
- FINDING Dragon Destruction vs Crab Control, seed 1110034, challenger seat 0: stall turn 7 seat 1 had no command: No complete legal action was supplied for this decision.
