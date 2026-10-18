# Bot tournament: pre-laststand (challenger) vs champion

- Mode **dev** (ordered pairs among the development decks; development seed range), pairing **same**, 8 seed(s) (from index 200) x 90 ordered deck pairs x 2 seats = 1440 games.
- Held-out decks (SHA-256 order, fixed): Allan Lion MCA, Mantis Manipulation, Unicorn Maidens.
- Decided 1440 of 1440; no winner 0. Wall clock 0:24:10 on 10 cores (0.99 games/s, 8.6 s per game on one core).

## Win rate

**Challenger won 717, champion won 723** = 49.8% (95% Wilson CI 47.2-52.4%). Exact two-sided sign test p = 0.8952; one-sided (challenger better) p = 0.5732.
- Paired by seed: challenger won both games of a pair 1, lost both 4, split 715. Exact McNemar (both vs neither) two-sided p = 0.3750.
- **Seat-0 advantage:** seat 0 won 709 of 1440 decided games = 49.2% (95% CI 46.7-51.8%); subtract -0.8% from a seat-0 win rate to correct it. Challenger as seat 0: 353/720; as seat 1: 364/720.

## Beyond win rate (challenger minus champion, decided games)

- Provinces left margin -0.01; Honor margin -+0.0; Personalities in play margin +0.02.
- Game length: mean turn 14.6 (challenger wins 14.6, losses 14.6); turn-limit games 0.
- Victory reasons: Military victory: 1217; Honor victory at the start of the turn: 141; Dishonor defeat at the beginning of the End Phase: 82.
- **Soundness:** bot stalls 0 (engine stalls, where the engine refused the only offered action: 0), illegal 0, exceptions 0, decision cap 0, setup failures 0, out of time 0; games with a refused-then-retried action 30 (76 refusals, informational). The bar for promotion is zero bot stalls and none of the others; engine stalls are reported to the rules lane.

## Per deck (challenger piloting the deck)

| Deck | Games decided | Challenger wins |
| --- | --- | --- |
| Crab Control | 144 | 63 (44%) |
| Crane Dominance | 144 | 80 (56%) |
| Crane preset | 144 | 81 (56%) |
| Dragon Destruction | 144 | 97 (67%) |
| Lion Legacy | 144 | 37 (26%) |
| Lion preset | 144 | 59 (41%) |
| Phoenix Talons | 144 | 41 (28%) |
| Scorpion Lethargy | 144 | 113 (78%) |
| Scottish Lion Deck | 144 | 38 (26%) |
| Spider Conquer | 144 | 108 (75%) |

## Power note

Independent games, exact binomial, two-sided alpha .05, 80% power: detect 55% needs about **783** decided games; 60% needs about **194**; 52% needs about 4904. At this run's throughput (0.99 games/s, 100% decided) that is 0:13:08 and 0:03:15 of wall clock. `same` pairing removes deck, seat and shuffle noise, so it needs fewer games when the bots differ; the A/A with `independent` pairing is the honest noise floor.
