# 2026-10-15 reactions (pre-registration, round 4)

- Issue / branch / arm: #963, `claude/963-bot-reactions` (stacked on rounds 1-3; their code is present but off), arm `reactions` (`BotContext.UseSureReactions`, default off).
- Champion: `main` (flag off).
- **Hypothesis.** A Reaction that is not a negation, not an entering-play Reaction and does not harm a card is played or declined by a deterministic roll with probability `0.3 + 0.15 x rank` (Help 45%, Penalize 60%,
  Bow 75%, Destroy 90%); Reactions that harm an opponent's card (destroy/bow) are already never declined (score 1400). BOT_LOGIC section 7 calls the roll "deliberate variety". A Reaction that helps or
  penalizes costs the player nothing to take (its costs and Honor are priced by the existing scores, and a harm to its own card scores below a pass), so declining it by chance gives up value. This is a strength
  question for a practice opponent that also has a "variety" purpose; this experiment measures the strength side only, and a promotion would be Mendon's decision on that trade-off.
- **Change (general; no card or deck named).** With the flag on, Reactions of rank Help or higher are always played when worth more than declining (weight 1.0); Neutral Reactions keep the roll. Reads only the seat's view.
- **Expected effect:** +1 to +2 points against the champion; most in decks with many Reactions.
- **Decisive test.** Mode `dev`, pairing `same`, seeds 48-59 (fresh block) = 864 pairs, 1,728 games, about 30 minutes. Primary: pairs won both vs lost both, exact one-sided sign test, **p < 0.025**. Also: win rate >= 51%;
  zero bot stalls and no more of any other soundness finding than the champion; no deck (>= 40 decided games piloted) below 35% for the challenger where the champion is at 50% or more; provinces-left and
  Personalities-in-play margins not negative (the round 3 amendment). Fixed sample, no extension.
- **Confirmation (only if dev passes):** mode `holdout`, pairing `same`, seeds 33-42 (600 games): one-sided p < 0.10, win rate >= 50%, zero bot stalls.
- **Decision rule:** promote-candidate only if dev and confirmation both pass; otherwise recorded as negative or inconclusive and the flag stays off.

## Result (fill in after the single run; the plan above is not edited)
