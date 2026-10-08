# Foothold of the Mad (issue #766)

Status: **implemented on `claude/766-foothold-of-the-mad`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Card: oracle-l5r-10562, Coils of Madness, profile `stronghold-wave-48` (`tools/content/package_stronghold_wave_48.py`). Engine: `src/L5R.Core/Foothold.cs`. Tests: `FootholdTests` (suite `foothold-of-the-mad`, 7 tests). The card was on #742's "waits on a ruling" list; this is Mendon's ruling.

## The ruling (Mendon, 2026-10-08)

- Treat Foothold of the Mad as having a starting Honor of **39** for the purposes of going first.
- After the start player is determined and before the first turn, set any player using Foothold of the Mad to **0** Honor. It is not an action or Reaction and cannot be interacted with; it is automatic. **Nothing is displayed in the UI.**
- While Foothold of the Mad is one or more of the Strongholds in play, Madness tokens are -1F tokens if that is computationally cheaper (they then aggregate like any +/- token); otherwise build a counter.

## What is wired

| Printed text | Built as |
|---|---|
| "If there is no other Foothold of the Mad player, you go first." | `FirstPlayerAtSetup`: a Foothold player counts as 39 in both setup paths (`PublishedMatch`, `CustomPractice`) |
| (ruling) Honor set to 0 | `ApplyFootholdStartHonor`, after the start player and before `StartTurn`; no event is emitted |
| "You do not lose Honor from cards you own." | `HonorLossNegated`: any loss whose source card the losing Foothold player owns |
| "Fallen Personalities join you for 2 less Gold." | P2 `EntryCostTerms`: -2 for a Fallen Personality a Foothold player recruits (Recruit and Reserve), floored at 0 by the channel |
| "Personalities and Followers have -1F for each of their Madness tokens." | `CreateToken`: while any Foothold is a Stronghold in play, a Madness token on a Personality or Follower is created as a -1F token |
| "Limited, bow: Give a target card a Madness token." | `foothold-madness` (any card; bows the Stronghold) |
| "Limited, bow: Your Holdings enter play paying 2 less Gold (this turn)." | `foothold-holdings`: a per-player turn mark (`MatchState.FootholdHoldingDiscounts`, order 23100, omitted until used) read by the same entry cost term |

## Why a -1F token, not a counter

A token created as -1F is read by the existing `StatModifier`, so a Force read costs nothing more, and no per-read scan of a card's tokens or of the two Strongholds is needed. A counter would add that scan to every Force read. The check for a Foothold happens once, when a Madness token is made.

## Readings for Mendon to confirm or correct

1. **Two Foothold players.** Both count 39, so the existing tie break decides (the seeded coin in custom matches, player 0 in the published match). The printed "if there is no other Foothold of the Mad player" is then not met by either side.
2. **Printed Honor.** The card prints Starting Honor 0, so the "set to 0" step changes nothing today; it is still performed so the rule holds if the starting value is ever read from elsewhere.
3. **Printed Province Strength 0.** The scan prints 0 for Gold, Province Strength and Starting Honor; the Foothold player's four Provinces have strength 0 as printed.
4. **The Madness penalty is the Stronghold's rule, not the action's.** The -1F is set after the Force-penalty negations are asked, so Tireless Efforts, Creating Order and Aka-beko (which negate a penalty an effect gives) do not stop the token being made or the penalty applying. A Madness token on a card that is not a Personality or a Follower stays a plain token.
5. **Tokens made before a Foothold is in play** do not exist: a Stronghold is in play from setup, so every Madness token is made after it.
6. **"Join you."** The discount covers a Recruit and a Reserve recruit. An Experienced overlay and an Invest Reaction do not read the entry channel (P2's rule).
7. **"Your Holdings enter play."** The Holdings the user recruits this turn, whichever of the two recruit routes. The mark is the turn it was made; it does not carry over, and the opponent's Holdings are not discounted.
8. **Both Limited abilities bow the Stronghold**, so one is usable per turn.

## Art pack

The new scan (`oracle-l5r-10562.jpg`) is covered by `catalogue-art-issue766-20261008-r1` (4,623 images, 617,534,950 bytes, SHA-256 `1b3ed7d50211fdf779aae4bda8e6a6d1565e7e383aca7c8fc2a47402598e73d7`), built with `--base-archive` from batch 4's pack `catalogue-art-issue735-20261008-r1` (4,622 images). Native visual and playability review has not been done.
