# The remaining unwired cards and the minimal primitive set (issue #668)

Status: **proposed** (an assessment, not a ruling). **Corrected 2026-10-06**: see "Correction" at the end. Lane: rules/shared. Requested by Mendon on 2026-10-05: "review every card that has not been wired, create a minimal set of additional primitives needed to wire the remaining cards, and draft a prompt to instruct a lesser model on how to craft each primitive." **No card was wired in this pass.**

Companion files:

- [Per-card appendix](2026-10-05-remaining-unwired-primitives-cards.md): every unwired card with the primitive codes it needs.
- [Implementation prompts](2026-10-05-remaining-unwired-primitives-prompts.md): one self-contained prompt per primitive, plus the common preamble every prompt relies on.

## Inputs

- `docs/rules/EMPEROR_EDITION_CORE_RULES.md` (core rules and the Torn Asunder, Coils of Madness, Gates of Chaos and Aftermath supplements) and Mendon's "End of Arc Supplement" text, which the rules doc already restates.
- `docs/rules/RULINGS.md` R-001 to R-030.
- The 491 cards in `content/deck-pool.json` not published by any `content/published/*.json` on `main` (unchanged from `de0109c2` to `727f13e5`), with their catalogue text, every blocker note in `tools/content/build_card_inventory.py` and `tools/content/*_blockers.json`, and the categories of `docs/decisions/2026-09-25-blocker-categories.md`.
- Five read-only surveys of `src/L5R.Core` made for this pass (run in the PR #670 worktree, then re-checked against `main`; see "Correction"): supplement keywords, the Gold payment pipeline, timing windows and watches, tokens/stats/keywords/restrictions/created cards/copies/duels/control, and decisions/zones/search/hidden information. Their file and line anchors are repeated in the prompts.

## Headline numbers

| Group | Cards |
|---|---|
| Unwired on `main` | 491 |
| Wired by PR #670 (`claude/667-pe-blocked-wave`, open; not counted below) | 23 |
| Appear wirable with primitives that already exist (`WIRE` in the appendix) | 206 |
| Need at least one new primitive | 238 |
| Blocked only by content or a ruling (scan, ruling, rejected, multiplayer) | 24 |

By set: Coils of Madness 123, Torn Asunder 120, Gates of Chaos 106, Aftermath 98, Promotional-Emperor 28 and Promotional-Celestial 14 (19 of those 42 remain after #670), War of Honor 2. The four end-of-arc sets had never been triaged: 397 of the 491 cards had no blocker note at all.

"Wirable today" is the surveys' finding that every piece a card prints has a built shape (often a feature-file target category or effect case). Each still needs its own reading, scan check and tests before a wave; the list is a work queue, not a promise. The 206 are listed in the appendix with `WIRE`.

## What the supplement mechanics already have

| Mechanic | Status | Where |
|---|---|---|
| Destined, Expendable (printed and granted) | built | `ApplyMandatoryEntryEffects`, `ExpendableDraw`, `GrantKeyword` |
| Elite and its grants | built | `EliteEffect`, `elite-training-grant` |
| Stalwart and its grants | built | `StalwartNegatesBow` |
| Kharmic player ability, granting Kharmic | built | `kharmic-discard` |
| Discipline | built | `AbilityDefinition.Discipline` |
| Reserve recruit; granting Reserve | built | `RecruitReserve`, `IsReserveRecruitable` reads granted keywords |
| Invest | built as an own-enters-play Reaction with a fixed Gold cost; one own-enters-play Reaction per card | `clay-kshatriya-invest` |
| Engage from any card type, first-opportunity claim | built | `after-engaging`, `ClaimFirstOpportunity` |
| Repeatable, Absent | built as flags | `UsesPerTurn = Unlimited`, `Absent` |
| Tireless, Home | **not flags**: per-id lists in `ValidateAbility` | P16 |
| Interrupt | partial: before-action windows exist, but they only predict movers and curated destroy/bow ids | P8 |
| Madness tokens | token exists; no count-based evaluator | P9 |
| Fortification | **missing** | P15 |
| "ExperiencedCoM" and "Inexperienced" | **silently wrong**: parsed as level 0, so `Keyword(card,"Experienced")` is false and "X - inexperienced" never matches X for an overlay | P23 |
| Variable Gold ("amount paid") | built (`PaidGoldAmount`), per-card effect code | none needed |
| Control transfer | **built** (`TakeControl`, `TransferUnitControl`, `ControlReturns`); `docs/decisions/2026-09-12-unique-singular-control-transfer.md` and the pass docs that cite it as "deliberately unbuilt" are stale | none needed; doc follow-up |
| Coils/Gates/Aftermath Events with a printed "Limited:" | resolve as Events Phase traits through `ResolveEvent` | none needed |

Other findings from the surveys worth a follow-up of their own: Order Seppuku records destroyed cards without calling `FinishActionEffectTracking`, so the record can leak into the next action; the `ResolveEvent` hook is not listed in `EngineHookStartup.Build`; the Terrain self-replacing path does not set `TerrainDestroyedThisBattle`.

## The minimal primitive set

Each primitive is one shared gap, ranked by the cards it unlocks. "Cards" counts every card that needs it; "sole" counts cards for which it is the only missing primitive (content and rulings aside). Several primitives are one-time edits to a shared file that open a registry; after that, every card of the shape is a feature file.

| Code | Primitive | Cards | Sole | Shared files touched once |
|---|---|---|---|---|
| P9 | Stat-term registry (conditional and count statics), card stat caps, contribution rules | 34 | 22 | `MatchEngine.cs` (PersonalityForce/FollowerForce/ArmyForces), `CardAbilities.cs` (PersonalityChi, CardPersonalHonor) |
| P2 | Entry and ability cost terms (`EntryCostTerms`, `AbilityCostTerms`, `PayGold` cost verb) | 23 | 12 | `MatchEngine.cs` QuotePurchase, `GoldQuotes.cs`, `AbilityCosts.cs`, `AlternativeCosts.cs` |
| P11 | Card-restriction store and generalised target bars | 23 | 15 | `MatchEngine.cs` Assign, `AttachAction.cs`, dishonor, duel issuance, `TargetBars.cs` |
| P24 | Small effect primitives (switch locations, adjacent move, home-target Attack, printed-Force compare, shuffle into deck, cost substitutions) | 19 | 10 | mostly feature files; `AttackTargeting.cs` once |
| P7 | Period stamps and negation watches | 20 | 7 | `PhaseLimits.cs`, `AbilityDefinition` |
| P4 | Turn-boundary hook, owner-turn durations, player-level granted abilities | 17 | 5 | `MatchEngine.cs` StartTurn, `EmperorRulingR2.cs`, `Models.cs` PlayerState |
| P22 | Created cards, copies and grants (RemoveWhen, current-stat values, Exchange, overlay onto a target, copy to an attachment, filter-scoped grants) | 17 | 6 | `CreatedCards.cs`, `CreatedCardEntry.cs`, `CardCopy.cs` |
| P1 | Payment pipeline hooks (tokens that bow or depend on co-chosen sources, production conditions with an amount function, production adjustments, lent producers, after-payment ledger, attached and in-play Strategy producers) | 13 | 10 | `EmperorMechE.cs`, `MatchEngine.cs` Producers/GoldProduced/Pay, `ConditionalProduction.cs` |
| P16 | Tireless and Home as ability flags; conditional timing; presence grants | 16 | 12 | `CardAbilities.cs` ValidateAbility |
| P6 | After-event records and windows (seppuku, Proclaim, assigned to attack, turned face up, Terrain destroyed, Celestial entry, leaves play, announce with performer) | 14 | 5 | `ActionEffectTracking.cs`, `MatchEngine.cs`, `EmperorRemainingG.cs` |
| P12 | Immunity ledger (negate-the-negation, standing shields) | 13 | 7 | `MovementLocks.cs`, `StraightenHolds.cs`, `ActionLocks`, `DestroyUnits`, bow helpers |
| P21 | Zone extensions (cards under a host, province card as an ability source, recruit from a province by ability, refill hook, history, to-deck delay) | 13 | 10 | `Models.cs`, `MatchQueries.cs` ViewCard, `CardAbilities.cs` AbilitySourceZoneAllows |
| P10 | Keyword-rule registry (conditional keywords, gain blocks, element replacement, clan changes, army/unit scopes) | 13 | 7 | `CardAbilities.cs` Keyword(), GrantKeyword |
| P3 | Recruitment gates and Proclaim eligibility | 12 | 5 | `MatchEngine.cs` QuotePurchase, `ProclaimAllowed`, `LoyaltyAllowsControl` |
| P18 | Duel rules (exclude Items, min/exact focus, first to focus, auto-lose, substitute duelist, conditional refusal with cost) | 12 | 5 | `Dueling.cs`, `PendingDuel` |
| P20 | Search and look extensions (province and any-player sources, multi-select, opponent chooses, Gold-producing criterion, bottom/attach/into-play dispositions, reveal-until) | 9 | 3 | `DeckSearch.cs`, `DeckLook.cs`, client deck viewer |
| P23 | ExperiencedCoM and Inexperienced parsing | 9 | 3 | `Experienced.cs` |
| P5 | Province-reveal Events (`AfterProvincesRevealed`, stays-face-up flag) | 8 | 1 | `MatchEngine.cs` ContinueEventsReveal/ResolveEvent |
| P13 | Phase and battle rule flags (end the Action Phase, engage bar, no-units negation, no destruction Honor, defenders may not play Terrains, Limited-as-Open per turn, victory exclusions) | 8 | 4 | one choke point each |
| P14 | Honor-change modifier registry | 8 | 2 | `MatchEngine.cs` GainHonor, `EmperorRulingR4Honor.cs` |
| P19 | Rulebook-action modifiers (Tactical Advantage amount and timing, Equip from a discard pile, Equip exemptions, timing grants not tied to a card, once-per-turn grants) | 7 | 4 | `CardAbilities.cs` rulebook ids, `TimingGrants.cs`, `MatchEngine.cs` Equip |
| P8 | Action-effect watch (Interrupt facts: movers with destination, discards) | 3 | 2 | `ActionEffectTracking.cs`, `EmperorRulingR2.cs` |
| P25 | Decision infrastructure (multi-pick Either/Or, `PendingActions` hook, cross-slot constraint) | 2 | 2 | `EitherOr.cs`, `MatchQueries.cs`, `TargetSlots.cs`, one client line |
| P17 | Additional-action scope (`ScopeAbilityIds`, Action Phase equip spends a grant) | 2 | 1 | `Models.cs` AdditionalActionGrant, `CardAbilities.cs` |
| P15 | Fortification Holdings | 1 | 0 | `MatchEngine.cs` Purchase, DestroyProvince |

Cards that need several primitives are counted under each. Sixty-two cards need two or more; the appendix shows the full list per card.

### Suggested order

1. **Wave the 206 `WIRE` cards first**, in set-sized batches, exactly as the Promotional-Emperor pass did. No engine work; they double the visible progress and will surface any misjudged card as a precise blocker note.
2. **P23** (a bug fix, nine cards, no design questions), then **P16** and **P9**: the three cheapest high-yield items, and P9 is the evaluator every later static needs.
3. **P2, P1, P3** together: the Gold and recruitment layer, all touching `QuotePurchase`/`GoldQuotes` once, so one agent should own them.
4. **P4, P5, P6, P7, P8**: the timing layer, in that order (P4's turn hook is what P5 and the owner-turn durations rest on; P7's stamps are what P6's new windows gate on).
5. **P11, P12, P10, P13, P14**: the status and rules-flag layer.
6. **P21, P20, P22, P18, P19, P24, P17, P25, P15**: the rest, each independent.

### What waits on Mendon

Rulings needed before wiring (13 cards; the question is in each card's existing blocker note where it has one): Benten's Blessing, Chuda Kaito ("+3GC"), Foothold of the Mad (going first), Ikoma Satoru and Sadahako's Artistry (a card put into another player's hand), Jizo's Mercy ("instead"), Journey's End Keep - exp ("Imperial" as a Clan alignment and the Favor at setup), Minor Blackmail, Pierce the Tapestry (seven copies and a per-battle count), Shrine to the Heavens, Stone of Remembrance, The Egg of P'an Ku, Unbearable Weight (a Strategy put into the other player's hand). Mendon has since ruled on Gozu, The Blade of the Balash, The Isha's Yumi, The Topaz Armor and Wyrmbone Katana; PR #670 wires them.

Rejected (`docs/decisions/2026-10-05-rejected-cards.md`): Peace Treaty, Wildfire, Green Nightshade Blend. Multiplayer-only, not wirable in a two-player engine: Begone, Fool!; Prideful Allies; Ningen's "Treatise on the Nezumi".

Scans needed (24 cards create a card with no CreatedOnly proxy, 2 have no scan of their own): see the appendix `SCAN`/`SCAN2` rows and `docs/requests/created-card-scans-needed.md`. About half of them create plain 0F or 1F Followers. PR #670 brings the Colonists scan (a 1F Ashigaru Follower), which already fits Exotic Farmlands; one more generic 1F Follower proxy without a keyword would unblock most of the rest.

## Reason and evidence

The Blocker-Category pass (issue #498) covered the earlier sets; the end-of-arc sets were never read against the engine, so every "needs X" above was derived card by card for this pass and then checked against what the engine actually has. The surveys are the evidence that the earlier notes had gone stale in both directions: control transfer, Expendable, Elite, Engage-from-a-Follower and variable Gold are built, while Tireless/Home and the Experienced keyword variants are not. The primitive set is "minimal" in the sense that each entry is one shared gap with its own registry or store; folding two into one would not remove a shared edit, and splitting one further would not unlock a card on its own.

## Correction (2026-10-06)

The first version of this document and of the prompts file said their engine anchors were read on `main`. The five surveys in fact ran in the worktree of PR #670 (`claude/667-pe-blocked-wave`), which was still open. Re-checked against `main` at `727f13e5`:

- All 49 file-and-line anchors and the relative `:NNNN` anchors in the prompts were compared with the source. Five pointed at a comment or a neighbouring line and are fixed (`RecordActionEffect`, `AssignedSentinel`, `OpenNextTargetReactionWindow`, `ArmDelayForHeldAction`, `DuelStatValue`). One identifier was wrong (`AssignBowed` for `AssignBowedTurn`), and two were written as if they existed (`AttackSpec`, `ControlReturnTrigger`).
- Three cited files exist only with #670: `PhaseOrder.cs`, `FavorMempo.cs`, `DuelSleepFusako.cs`. They are now marked in the prompts, and P13 is marked as requiring #670.
- The card counts treated 10 cards as wired by #670; it wires 23. Fourteen cards moved out of the primitive lists (Coin Pouch, Near Miss, Profits, Gozu, Heart of Fudo - exp2, Hyobuko, Masaya, The Sacred Rosary, The Topaz Armor, The Blade of the Balash, The Isha's Yumi, Winter Sled, Wyrmbone Katana, Lying in Ambush), Through the Breach moved back to "needs a scan" (it is built but unpublished), and Exotic Farmlands no longer needs one. The tables, the appendix and the prompts' unlock lists and demonstration cards reflect this.
- #670 also builds one-card versions of parts of P1, P8, P12, P20, P21 and P25. Each prompt now names them as the precedent to generalise.

Not re-verified: the surveys' descriptions of behaviour (what a function does, as opposed to where it is) were spot-checked only where a prompt's design depends on them.

## Not done

- No card wired, no inventory regenerated, no blocker notes changed (Mendon's instruction for this pass).
- The stale control-transfer decision doc is not edited here.
- The per-card `WIRE` judgement has not been re-verified against each card's scan.
