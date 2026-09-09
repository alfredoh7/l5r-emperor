# Roadmap

A living checklist of missing rules-engine mechanics and open visual/UI feature requests. Unlike `docs/handoffs/tasks/` (one record per task branch) and `docs/decisions/` (one record per durable decision), this file is edited in place as items ship — it is not regenerated per branch and does not itself carry task evidence. `docs/PROJECT_STATUS.md` remains the source of truth for current shared milestone status; `docs/NEXT_PHASE.md` for the broad phase sequence. This document is the granular feature/mechanic level underneath both.

Update this file as part of the PR that ships (or meaningfully advances) an item: move it to Done with the issue/PR that closed it, or add a new row when a gap is identified. Issue: [#86](https://github.com/alfredoh7/l5r-emperor/issues/86).

Status values: **Not started** / **In progress** / **Done**.

## Rules-engine mechanics

Confirmed by direct source audit of `src/L5R.Core` on 2026-09-09, not by absence of a card that would need them.

### Not yet implemented

| Mechanic | Status | Notes |
|---|---|---|
| Dishonor / Rehonor as general actions | Partial ([#90](https://github.com/alfredoh7/l5r-emperor/pull/91)) | `Rehonor(card)` and the shared `MarkDead`/`MakeDead` primitives now exist (dishonorable-death Honor loss centralized; a discarded card can become honorably dead independently of destruction) — but no action yet *dishonors* a Personality outright beyond the entry-time `enters-dishonored` Effect, and the "rehonor instead of gaining Honor" substitution (core rules p. 98–100) still has no engine hook. |
| Reactions to Ranged/Melee Attacks | Done ([#90](https://github.com/alfredoh7/l5r-emperor/pull/91)) | The Attack primitive now opens a real Reaction window (mutable `PendingAttack.Strength`, either side may raise/lower/negate) before resolving — but only when a reaction is actually possible; no published card provides one yet, only training fixtures. |
| Dueling | Not started | No performer/target selection, duel stat, Focus mechanism, Focus Effects, Duelist keyword, or resolution/winner/consequence application exists anywhere in the engine. This is one of the largest single gaps — several already-triaged Strongholds and many catalogue Personalities need it. |
| Terrain | Not started | No Terrain Strategy entry-into-play-at-a-battlefield, per-battlefield Terrain state, or end-of-battle Terrain discard exists. |
| Tactician keyword / Tactical ability | Not started | No `Tactical Battle: discard a card, choose your performing Tactician: +Force equal to its Focus Value` universal ability, and no per-Tactician once-per-turn cap. |
| Conqueror keyword | Not started | No universal `Reaction: After a battle resolution ends, choose your performing attacking Conqueror: straighten his unit` ability. |
| Kensai keyword | Not started | The two-Weapon exception to the one-Weapon-per-Personality limit is not implemented (only the base one-Weapon/one-Armor limit is, via `AttachmentAllowed`). |
| Equip (Battle) for Shugenja/Spells | Not started | Only the Limited-timing universal Equip exists; the Battle-timing version (`choose your performing unbowed opposed Shugenja and target a Spell in your hand: attach it, paying all costs; you may take an additional Battle action to use a Battle ability on it`) does not. |
| Cavalry Maneuvers Segment | Not started | Attack Phase only implements a single Maneuvers-style assignment step; the separate Cavalry-only Maneuvers Segment (core rules Attack Phase, step C) does not exist. Tracked loosely by [#20](https://github.com/alfredoh7/l5r-emperor/issues/20). |
| Engage reaction-timing window | Not started | See [#15](https://github.com/alfredoh7/l5r-emperor/issues/15) — no window exists for "After engaging" triggers generally (Naval Invasion currently short-circuits this by resolving inline rather than through a real Reaction window). |
| Events Phase: Region/Celestial-specific resolution | Partial | Region attachment and Celestial entry are described in the core rules doc; the Events Phase itself (Straighten→Events ordering) is implemented, but Region/Celestial card types have no published examples yet to validate against. |
| Reconnaissance | Not started ([#93](https://github.com/alfredoh7/l5r-emperor/issues/93)) | No mechanism at all — not a keyword flag, not a face-down-peek action, not a Battle/Reaction ability shape. The single highest-frequency blocker found while triaging the not-yet-imported card pool for the generalized Ranged/Melee Attack primitive (below): Daidoji Kitahime, Hiruma Akio, Hiruma Nitani, Hiruma Sniper, Hiruma Tensin, Toritaka Chokichi, Yoritomo Haruhiko, Strategic Strike, Spirit of the Scout, and The Otoro Estate (Stronghold) all need it, among others in the wider reference pool. |

### Built as general primitives, narrowly exercised so far

These exist as reusable engine shapes, not just one-off card hacks — but only 1–4 published cards currently exercise each, so edge cases beyond what those cards need are untested.

| Primitive | Status | Built for | Notes |
|---|---|---|---|
| Token primitives (create/move/destroy) | Done (primitive) | [#79](https://github.com/alfredoh7/l5r-emperor/issues/79)/[#80](https://github.com/alfredoh7/l5r-emperor/pull/80) | `CreateToken`/`MoveToken`/`DestroyToken`, persistent stat bonus distinct from turn-cleared Modifiers, cleanup on leaving play. Only "Fire" tokens are exercised by published cards so far (Monastery of New Thought, Togashi Meiyu, Togashi Torazu). No UI rendering yet — see Open visual/UI feature requests below. |
| Ranged/Melee Attack primitive | Done (primitive) | [#63](https://github.com/alfredoh7/l5r-emperor/issues/63)/[#64](https://github.com/alfredoh7/l5r-emperor/pull/64), extended [#82](https://github.com/alfredoh7/l5r-emperor/issues/82)/[#83](https://github.com/alfredoh7/l5r-emperor/pull/83), [#84](https://github.com/alfredoh7/l5r-emperor/issues/84)/[#85](https://github.com/alfredoh7/l5r-emperor/pull/85), generalized [#90](https://github.com/alfredoh7/l5r-emperor/pull/91) | Deferred (not-required-up-front) targeting, `AttackStrength` per ability id with conditional bonuses, `AttackKind` (Ranged/Melee) tracked per ability, and a real Reaction window (mutable strength, skip-if-nothing-can-react) covering the full A-G resolution procedure. Wiring a new Attack card is now just: strength/kind lookup entries plus a case calling `ResolveOrDeferAttack`. |
| Entering-play Reaction window | Done (primitive) | [#82](https://github.com/alfredoh7/l5r-emperor/issues/82)/[#83](https://github.com/alfredoh7/l5r-emperor/pull/83) | `Timing:"Reaction"` + `Trigger:"own-enters-play"`, composes correctly with Proclaim in the same window. Only this one trigger is generalized — other Reaction triggers ("After engaging," "Before an action's effects resolve," "After a duel," etc.) would each need their own window built when a card needs one. |

## Open visual/UI feature requests

| Feature | Status | Issue | Notes |
|---|---|---|---|
| Token artwork, sizing, placement/stacking | Not started | [#81](https://github.com/alfredoh7/l5r-emperor/issues/81) | Exhaustive token-type catalogue already compiled in the issue. Gates several rules-lane scenario validations (e.g. the Torazu Crane-deck test). |
| In-game rules reference viewer | Not started | [#58](https://github.com/alfredoh7/l5r-emperor/issues/58) | Content source (`docs/rules/EMPEROR_EDITION_CORE_RULES.md`) already exists and keeps growing as supplements are added. |
| Duel visual-resolution flow | Not started | *(not yet filed)* | A presentation analogous to the existing battle-resolution flow — focus/strike choices, revealed focused cards, Focus Effects, winner/loser outcome — once dueling itself lands in the engine (see Dueling above). Blocked on the engine mechanism, not just art. |
| Reviewable game log on the play screen | Not started | [#88](https://github.com/alfredoh7/l5r-emperor/issues/88) | Every action the computer (or either player) takes, logged in simplified human-readable form: who acted, the action, who performed it, who it targeted, and the outcome. Purely a UI/QoL presentation of events the engine already emits (`GameEvent`/`Emit`) — no new engine state needed, just a client-side reviewable log view. |
| Highlight active/priority player during decision phases | Not started | [#11](https://github.com/alfredoh7/l5r-emperor/issues/11) | |
| Force/Chi itemized breakdown during combat | Not started | [#18](https://github.com/alfredoh7/l5r-emperor/issues/18) | |
| Dishonorable status display (180° rotation, Fallen badge) | Not started | [#21](https://github.com/alfredoh7/l5r-emperor/issues/21) | Depends on Dishonor/Rehonor actually being reachable in play beyond entry-time (see above). |
| Deck builder screen | Not started | [#25](https://github.com/alfredoh7/l5r-emperor/issues/25), [#67](https://github.com/alfredoh7/l5r-emperor/issues/67), [#68](https://github.com/alfredoh7/l5r-emperor/issues/68) | Browse/edit in memory, save/reload from the local deck directory. |
| Pre-game manual deck reshuffle window | Not started | [#26](https://github.com/alfredoh7/l5r-emperor/issues/26) | |
| Custom-deck match setup (Practice and multiplayer) | Not started | [#29](https://github.com/alfredoh7/l5r-emperor/issues/29), [#69](https://github.com/alfredoh7/l5r-emperor/issues/69), [#70](https://github.com/alfredoh7/l5r-emperor/issues/70) | |
| Tabletop HUD, clan emblems, individual card piles refinement | In progress | [#34](https://github.com/alfredoh7/l5r-emperor/issues/34) | |
| Lightweight local username | Not started | [#37](https://github.com/alfredoh7/l5r-emperor/issues/37) | |
| Fortress provinces and border-mounted mat icons | In progress | [#56](https://github.com/alfredoh7/l5r-emperor/issues/56), open PR [#57](https://github.com/alfredoh7/l5r-emperor/pull/57) | |
| Present all six turn phases with contextual help | Not started | [#65](https://github.com/alfredoh7/l5r-emperor/issues/65) | |
| Authoritative battle stage and next-player-action display | Not started | [#66](https://github.com/alfredoh7/l5r-emperor/issues/66) | |
| Connected battle courts, ink assignments, province fall sequences | In progress | [#75](https://github.com/alfredoh7/l5r-emperor/issues/75), open PR [#76](https://github.com/alfredoh7/l5r-emperor/pull/76) | |
| Private project board integration | In progress | [#62](https://github.com/alfredoh7/l5r-emperor/issues/62), open PR [#78](https://github.com/alfredoh7/l5r-emperor/pull/78) | |

## Acceptance / process items (tracked here for completeness, owned by `docs/NEXT_PHASE.md`)

| Item | Status | Issue |
|---|---|---|
| Accept complete Lion and Crane matches on the pinned profile | Not started | [#71](https://github.com/alfredoh7/l5r-emperor/issues/71) |
| Accept native Windows gameplay with the distributed client | Not started | [#72](https://github.com/alfredoh7/l5r-emperor/issues/72) |
| Accept a private external Mac–Windows multiplayer match | Not started | [#73](https://github.com/alfredoh7/l5r-emperor/issues/73) |
| Scope short beginner lessons after dependable-match acceptance | Not started | [#74](https://github.com/alfredoh7/l5r-emperor/issues/74) |
| Events Phase turn-structure modeling | Done | [#10](https://github.com/alfredoh7/l5r-emperor/issues/10) |
| Battle sub-phase structure (Maneuvers, Engage, Battle Actions, Resolution) | Not started | [#20](https://github.com/alfredoh7/l5r-emperor/issues/20) |

## Content coverage

Published (playable) card count as of 2026-09-09: **110** — pinned Lion/Crane 40, Vanilla Wave 1 (61 ability-free), Stronghold Waves 1–3 (7 total), Personality Waves 1–2 (2 total). The full Emperor-legal reference catalogue (`content/deck-pool.json`) has 2,143 cards; the wider catalogue is reference-only until a card is explicitly wired up (`docs/rules/LION_CRANE_COVERAGE.md`, `AGENTS.md` — never silently turn an unsupported effect into a blank playable card).

Next content candidates should be chosen by which mechanic they'd exercise, per the "build the rules engine as we go" approach — e.g. the recurring "you do not lose Honor from [category] cards you own" pattern (6+ Strongholds) is the next highest-leverage primitive by card-count once picked up again.
