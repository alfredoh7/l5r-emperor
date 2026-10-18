# Bot tournament: champion (challenger) vs champion

- Mode **holdout** (ordered pairs with at least one held-out deck; held-out seed range), pairing **independent**, 3 seed(s) (from index 0) x 60 ordered deck pairs x 2 seats = 360 games.
- Held-out decks (SHA-256 order, fixed): Allan Lion MCA, Mantis Manipulation, Unicorn Maidens.
- Decided 360 of 360; no winner 0. Wall clock 0:05:30 on 10 cores (1.09 games/s, 7.7 s per game on one core).

## Win rate

**Challenger won 185, champion won 175** = 51.4% (95% Wilson CI 46.2-56.5%). Exact two-sided sign test p = 0.6353; one-sided (challenger better) p = 0.3177.
- Paired by seed: challenger won both games of a pair 32, lost both 27, split 121. Exact McNemar (both vs neither) two-sided p = 0.6029.
- **Seat-0 advantage:** seat 0 won 165 of 360 decided games = 45.8% (95% CI 40.8-51.0%); subtract -4.2% from a seat-0 win rate to correct it. Challenger as seat 0: 85/180; as seat 1: 100/180.

## Beyond win rate (challenger minus champion, decided games)

- Provinces left margin -0.02; Honor margin +0.5; Personalities in play margin -0.14.
- Game length: mean turn 13.9 (challenger wins 13.8, losses 14.0); turn-limit games 0.
- Victory reasons: Military victory: 269; Honor victory at the start of the turn: 86; Dishonor defeat at the beginning of the End Phase: 5.
- **Soundness:** stalls 0, illegal 0, exceptions 0, decision cap 0, setup failures 0, out of time 0; games with a refused-then-retried action 55 (98 refusals, informational). The bar for promotion is zero in the first six.

## Per deck (challenger piloting the deck)

| Deck | Games decided | Challenger wins |
| --- | --- | --- |
| Allan Lion MCA (held out) | 66 | 11 (17%) |
| Crab Control | 18 | 16 (89%) |
| Crane Dominance | 18 | 13 (72%) |
| Crane preset | 18 | 11 (61%) |
| Dragon Destruction | 18 | 13 (72%) |
| Lion preset | 18 | 8 (44%) |
| Mantis Manipulation (held out) | 66 | 39 (59%) |
| Phoenix Talons | 18 | 8 (44%) |
| Scorpion Lethargy | 18 | 10 (56%) |
| Scottish Lion Deck | 18 | 7 (39%) |
| Spider Conquer | 18 | 14 (78%) |
| Unicorn Maidens (held out) | 66 | 35 (53%) |

## Power note

Independent games, exact binomial, two-sided alpha .05, 80% power: detect 55% needs about **783** decided games; 60% needs about **194**; 52% needs about 4904. At this run's throughput (1.09 games/s, 100% decided) that is 0:11:58 and 0:02:58 of wall clock. `same` pairing removes deck, seat and shuffle noise, so it needs fewer games when the bots differ; the A/A with `independent` pairing is the honest noise floor.
