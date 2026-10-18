# Reactions to the same moment stack (issue #898)

Status: **accepted for review**. Lane: rules. Engine: `ActionEffectTracking.cs`, `ReactionWatcherQueue.cs`, `CardAbilities.cs` (bow, move and Nikutai openers), `EmperorStrongholdBatch2.cs` and `EmperorP3Priority.cs` (announce), `ForgottenLegacyBatch3.cs` (Entrenched Position). Probe: `tests/L5R.Core.Tests/ReactionProbe.cs`. Regressions: `ReactionStackingTests.cs`.

## What Mendon found

A Personality with two Chrysanthemum Blossoms attached was granted a Reaction window for only one of them. Both react to the same moment ("after this Personality is assigned"), so each should have given +1F and 1 Honor.

## The rule

Every legal Reaction to a moment is on offer, and playing one leaves the others on offer in the same window. Two Reactions never compete for "the" window.

## Cause and fix

1. **A played Reaction overwrote the window's record.** A Reaction is an action, so its own resolution records its facts (`MatchState.LastResolvedAction`). In the after-action window the other Reactions read that same record to decide whether they apply, so after one Blossom was played the second saw the Blossom's resolution instead of the assignment and the window closed. The window's record is now kept: given back at once when the Reaction opens no window of its own, and carried on the window it sets aside when it does (`SuspendedWindow.LastResolvedAction`). The same applies to every Reaction in that window family (Fact windows too).
2. **Grief's printed limit had been enforced by that same accident.** "You may only play one copy of Grief in reaction to any one resolution" worked because the second copy saw the first copy's record. It is now explicit (`ResolvedActionFacts.OneCopyPlayed`, `OneCopyPerResolution`). Advanced Intelligence (per battle), Bend like a Reed (per duel), Words Have Strength (per resolution) and Trade Embargo (per turn) already kept their own limits.
3. **Single-watcher windows asked only the first card they found.** The bow window (Togashi Hizumi, the Quagmire Region, Tamori Ruya), the move window (Legion of the Bat), the Nikutai window, the announce window (in-play watchers such as Tsuruchi Yakusho, Lookout and Suitengu's Torch) and the Defender's Entrenched Position / No Time for Games window each named one card, with the comment that no real scenario needed two. Each eligible watcher now gets its own window in turn (`ReactionWatcherQueue.cs`; the announce record's `Asked` list), and the action carries on after the last. A watcher that has since been spent, bowed or removed is skipped.

## The probe

`L5R_REACTION_PROBE=1` makes every command the Core suites accept through `RegressionAssertions.Accept` also run on copies of the match:

- **pair**: for every two Reactions on offer to the player holding priority, after one is played is the other still on offer in the same window?
- **duplicate**: the match is rebuilt before the command with a second copy of every card that holds a Reaction (same zone, host and province); every Reaction the real match offers, in the window the command opens or any it opens after declines, must be offered to the copy as well.

`L5R_REACTION_PROBE_OUT=file` receives the report. A finding is a lead, not a verdict. It is expected when the first Reaction ends the second's moment (it bowed, moved or destroyed the second's card, negated the held action, or used up the thing both answer), when a printed limit applies (the four cards above), and when the trigger names the card itself (a card "targeted", "entering play", "won a duel" or "performed" the action, so a copy beside it did not).

Over the whole Core suite (801 distinct Reaction windows, 222 pair and 429 duplicate checks) the probe reports 61 distinct findings after these fixes. Read against the card text, every one falls in the expected categories above except the few below, which are listed for a human look rather than called bugs: Blade of Champions and the Pearl-Encrusted Staff of the Cobras (a second copy attached to the same Personality may be barred by the one-Weapon rule the probe does not apply to its forced copy), Halls of the Forgotten, Suitengu's Torch, Kalani's Landing (already asked in turn through the announce record, so a copy that cannot pay or is not usable is simply not asked), Daidoji Yuki, The Tower of Vigilance and Mempo of the Amethyst Champion (each attaches or enters under a rule the forced copy does not meet).

## Stormless Fury (issue #900)

The "after a Battle action resolves" window named one card, the first Stormless Fury found, and an attached copy was asked only when no hand copy was left. Mendon: several can be attached in this window, to one or more Thunder Shugenja. Every Stormless Fury that can react now gets its window in turn: each in the hand first (it attaches to the performing Thunder Shugenja), then each already attached (it bows the performer). A copy that attached during the window is skipped when its turn comes, because its own errata bars it from reacting against the action that attached it. The performer is still the one Personality that performed the Battle action.

## For Mendon to confirm

- Confirmed by Mendon (2026-10-13): the order the watchers of one moment are asked in is: Hizumi-type cards, then Quagmire-type Regions, then the acting player's Ruya-type cards (each group by card id); announce watchers by card id. The printed cards do not order them, so the rules lead may prefer the controller to choose; R-039's announce-and-order procedure covers the windows both players hold a Reaction in.
