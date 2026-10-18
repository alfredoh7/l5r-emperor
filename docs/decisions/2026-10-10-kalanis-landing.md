# Kalani's Landing (issue #816)

Kalani's Landing (oracle-l5r-4252, Emperor Edition #408), the Mantis Port Stronghold imported by issue #812, is wired in `stronghold-wave-816` (`src/L5R.Core/Wave816.cs`). The scan is the receipt-verified Emperor Edition printing; the printed text, Strength 7, production 4 and starting Honor 2 were read from it.

> This card will not produce Gold when paying for an attachment. / After each phase begins: Straighten this card. / **Reaction:** After an action is announced, target a card *(in play)*: Give it -2 Gold Cost.

## Mendon's ruling (2026-10-10, recorded as R-041)

- **Target.** A card in play, meaning a card in a player's home (holdings, Personalities, Rings and other permanent-effect cards) that is not explicitly out of play, or a card at a battlefield, with a **non-zero Gold Cost**. A card with no printed Gold Cost has a Gold Cost of 0 and cannot be targeted.
- **Timing.** The Reaction can be taken after any action is announced. Once it has been used, the Reaction window is not opened for that player again until the next turn.
- **Effect.** The target's current Gold Cost is reduced by 2 until the end of the turn.

## What was built

- **The Gold Cost modifier on a card in play was not missing.** `ApplyModifier(card, ..., "GoldCost", -2)` and `CardGoldCost` already exist (Wind of the Moon, the Training fixtures); the blocker note in `pe_issue812_blockers.json` was wrong about that. A turn's end already removes every non-permanent modifier. What was missing was the legality and the timing, now ruled:
  - **Target category** `w816-gold-cost-card-in-play`: `InPlay(target)` and `CardGoldCost(target) > 0`, read *after* modifiers, so a card Kalani's Landing already brought to 0 is no longer a target and a cost never goes below 0.
  - **Window trigger** `AnnounceTriggers`-style `w816-announce-any-action`: the announce window (P3/P6) for the controller of a card holding such a Reaction after **either** player's announced action. It opens only when the Reaction can be taken (a legal target, not spent). The existing registered announce watchers only serve the announcing player's own cards.
  - **Once per turn** is the ability's default `UsesPerTurn` of 1, spent when the Reaction resolves. Declining the window spends nothing.
  - **New channel `EngineHooks.PhaseBegins`** ("after each phase begins"), fired from `SetPhase` once per rules phase (the Attack Phase's segments are one phase). Reusable by any such trait.
- **"Will not produce Gold when paying for an attachment"**: a `ConditionalProduction(0, PayingForAttachment)` (Platinum Mine's condition), so it still produces its printed 4 for a Personality, a Holding or an action.
- **"After each phase begins: Straighten this card"**: through the shared `Straighten`, so a hold or negation on straightening still applies. It runs at the start of every phase of either player's turn.

## Readings for Mendon to confirm

1. **"An action" excludes Reactions.** Elsewhere in this engine a Reaction is an action (ruling of 2026-09-19). A Reaction played inside an announce window would announce into a window of its own, and the Reaction's own use is what the once-per-turn limit counts, so Reactions are not announced into this window (the same exclusion Magistrate's Stipend's window has). Limited, Open and Battle abilities, a Strategy played, an Equip and a Reserve recruit are announced. Tests cover a Limited/Open ability, a Strategy and an Equip; a Reserve recruit uses the same route and has no dedicated test.
2. **"Until the next turn" is once per game turn number** (the engine's `TurnNumber`), the same clock every other once-per-turn Reaction uses, so each player's turn is a turn.
3. **A card with a Gold Cost reduced to 0 by Kalani's Landing is no longer targetable** (non-zero is judged on the current cost).
4. **Ring cards and Events kept in play** are not listed as targets: the engine's in-play zones (Home, Stronghold, Battlefield, Attached, Region, Celestial) are used, and those cards print no Gold Cost.

## Shared code touched

Five small hooks into shared files, each commented with the issue: the trigger check in `ValidateAbility` (CardAbilities.cs), the announce watcher chain and the non-ability announce guard (EmperorP3Priority.cs), the announce-window listing (MatchQueries.cs), `SetPhase` and the hook-seal list (MatchEngine.cs, EngineHookStartup.cs). With no Kalani's Landing in the match they change nothing: every new branch returns before doing any work.

## Art

The scan is bundled as `src/L5R.Client/Content/Published/oracle-l5r-4252.jpg`. Its original is already in the art pack built for #812 (`catalogue-art-issue812-20261010-r1`), so no new image was needed: `build_catalogue_art.py --base-archive` (extension mode) added only the `resources` entry for the bundled file to `catalogue-art.json`, and the receipt in `content/catalogue-art-pack.json` is left as #812 wrote it. That pack is still to be published as a private release (a step of #815).
