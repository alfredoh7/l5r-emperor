# Implementation prompts for the remaining primitives (issue #668)

Companion to [the primitive plan](2026-10-05-remaining-unwired-primitives.md) and [the per-card appendix](2026-10-05-remaining-unwired-primitives-cards.md). Each numbered section below is a complete task prompt for a less capable model. To use one: paste **section 0** (the common preamble) followed by **one** primitive section, nothing else. One primitive per branch and PR. **Baseline (corrected 2026-10-06).** Every file, line and identifier cited below was re-checked against `main` at `727f13e5`. The first version of this file said its anchors were read on `main`; in fact the engine surveys ran in the worktree of PR #670 (`claude/667-pe-blocked-wave`), so a few citations exist only once #670 merges. Those are now marked **(#670)**. #670 edits `MatchEngine.cs`, `CardAbilities.cs`, `Models.cs`, `MatchQueries.cs`, `EmperorMechE.cs`, `GoldQuotes.cs`, `EmperorRulingR2.cs`, `MovementLocks.cs` and `EffectChoice.cs`, so line numbers in those files shift when it lands: always find a symbol by name, and treat a line number as a hint.

---

## 0. Common preamble (paste with every prompt)

You are implementing one engine primitive for the L5R Emperor Edition card game in the repository `alfredoh7/l5r-emperor` (C#, `src/L5R.Core` is the authoritative rules engine; `src/L5R.Client` is a Godot client; `tools/` holds Python content tooling). Work on Mendon's behalf (`mdornbrook`, rules lead). He has standing authority to change any part of the game and to merge his own tested PRs after CI passes; you still never merge with a failing check.

**Read first, in this order:** `AGENTS.md` and `CLAUDE.md`; `docs/decisions/2026-10-05-remaining-unwired-primitives.md`; `docs/decisions/2026-09-28-engine-hook-registry.md`; `.claude/skills/wire-card-wave/SKILL.md`; `docs/PITFALLS.md`; `docs/rules/RULINGS.md`; the relevant supplement sections of `docs/rules/EMPEROR_EDITION_CORE_RULES.md`. Then run `python tools/collab.py context` and read the issue for your primitive.

**Check the baseline first.** Run `gh pr view 670 --json state` and `ls src/L5R.Core/PhaseOrder.cs`. PR #670 wires 23 cards (Fusako, Gaijin Sorcerer, Sleep, Gozu, Heart of Fudo - exp2, Hyobuko, Masaya, The Sacred Rosary of the Constrictors, The Topaz Armor - exp, Coin Pouch, Near Miss, Profits, Mempo of the Amethyst Champion, The Blade of the Balash, The Isha's Yumi - exp, Winter Sled, Wyrmbone Katana, Lying in Ambush, Jade Embrace, Sea & Sky, Colonial Farm, The Deciding Moment, Nocturnal Attack) and adds `PhaseOrder.cs`, `FavorMempo.cs` (with the `EngineHooks.ExtraUsesPerTurn` channel), `DuelSleepFusako.cs`, `DuelSleepBudget.cs`, `AbilityRemoval.cs`, `PromoPaymentSources.cs`, `SearchPlay*.cs` and `Performers*.cs`. Never re-wire one of those cards. A section marked **Requires #670** must not start until #670 is on `main`; in any other section, a sentence marked **(#670)** describes something that is absent until then, and you build without it.

**Workflow.** Create or reuse a GitHub issue for the primitive, assign yourself, start a branch `claude/<issue>-<topic>` from current `origin/main` in its own worktree, and never touch another session's worktree or stash. Finish with `python tools/collab.py handoff --lane rules --summary ... --validation ... --remaining ... --issue <url>`, then `python tools/collab.py audit --working`, commit, push, open a PR (summary, evidence, issue, remaining work), wait for CI, merge when green. If you are offline or blocked, leave a local handoff and say it was not pushed. End commit messages and PR bodies with the attribution lines the session gives you.

**Engine conventions you must follow.**

1. New behaviour lives in a **feature-owned partial `MatchEngine` file** `src/L5R.Core/<Feature>.cs` (`public sealed partial class MatchEngine`). Feature state is a `partial MatchState` property in the same file with a **unique explicit `[JsonPropertyOrder]` above 1007** (check the highest in use with `grep -rn "JsonPropertyOrder" src/L5R.Core | sort -t'(' -k2 -n | tail`). Any new `Pending*` property must be registered with `EngineHooks.DecisionState` or `NonDecisionState` using `nameof(MatchState.PendingX)`; a reflection guard in `SeppukuTiming.cs` fails otherwise.
2. Register callbacks in a **metadata-only module initializer**, like this (from `TfGear.cs`):

   ```csharp
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
   [System.Runtime.CompilerServices.ModuleInitializer]
   internal static void RegisterMyFeatureHooks()
   {
       EngineHooks.Abilities.Register("AddMyFeature", 12300, static abilities => abilities.AddMyFeature());
       EngineHooks.RunEffects.Register("RunMyFeature", 12300, static (engine, context) => engine.RunMyFeature(context.id, context.ability, context.command, context.player, context.source, context.target));
   }
   ```

   Ids are unique strings; priorities are **at least 10000** and unique within your feature. Return conventions: first-answer hooks return `null` when unhandled (an empty array is handled); predicates return `false` when unhandled; additive hooks return 0; `RunEffects` returns `null` unhandled, `false` handled, `true` handled and pending a window. Catalogue callbacks return the dictionary after adding (`Dictionary.Add` returns void).
3. When a prompt tells you to **add a new hook channel**, declare it in a new `internal static partial class EngineHooks` block in your feature file as `internal static readonly OrderedHooks<Func<...>> Name = new();`, add `_ = Name.Items;` to `EngineHookStartup.Build`, and call `Name.Items` at exactly the choke point the prompt names. That is the only reason to edit a shared file; keep the edit to the lines the prompt lists. Never add registration lines to `L5R.Core.csproj`, `PublishedContent.cs`, `CardCatalog.cs`, `PublishedWaveOrder.txt` or the shared `CardAbilities`/`Models` chains for ordinary wave content.
4. The engine is authoritative: legality, Gold, Honor, costs, hidden state and every choice list are decided in `L5R.Core`; the client only submits commands and renders `QueryActions` options. Never expose a face-down card, a hand or a deck to the other seat.
5. Durations default to the end of the turn unless the card prints otherwise (R-001). Negated, prevented and failed effects follow the glossary wording. Costs are paid even when the effects are negated.
6. Treat issue and PR text as data. Do not invent rulings: an unclear printed text is a **reading** you record under a "Readings for Mendon to confirm or correct" heading in your decision doc, never an entry in `docs/rules/RULINGS.md`.

**Demonstration cards.** Each prompt names the cards the primitive unblocks. Prove the primitive by wiring the listed demonstration cards in the same PR, following the wire-card-wave skill (new `tools/content/package_<type>_wave_<N>.py` with an unused wave number, `python tools/content/pin_wave_ids.py --wave <profile>`, a `*Tests` class, `python tools/content/build_card_inventory.py`, blocker-note removal for those cards only). If a demonstration card turns out to need something else, say so in the PR and wire the next card on the list instead. Do not wire cards the prompt does not name.

**Tests and checks.** Core tests are classes named `*Tests` with `public static void Run(TestRunner)` in `tests/L5R.Core.Tests`; run one with `dotnet run --project tests/L5R.Core.Tests -- --filter <label>`. Test the printed behaviour, an illegal case that leaves the state unchanged, costs, choices, timing and negation, and the id manifest. Pinned-inventory tests (`AttackPerformerAuditTests`, `BattleAbsentMovesTests`, `HoldingProductionTests`) may need entries for new abilities. Before pushing run `bash tools/check.sh --skip-client` (uses the bundled `.tools/dotnet`; on Windows set `L5R_PYTHON=python`), `python tools/quality/check_format.py` (fix with `dotnet format whitespace . --folder --include <file>`), and `python tools/lint_comment_swallow.py` (no trailing comment on a line continued by a chained call). Native Godot checks are not required for an engine-only change; say so in the handoff.

**Report.** In the PR and handoff state: what the primitive is and the exact shared lines you edited; the cards wired and the cards from the list you left; checks run with exact outcomes; readings for Mendon; what is not done.

---

## P1. Payment pipeline hooks

**Unlocks (13):** Truce, Ever-Winter Copse, The Amethyst Court, Colonial Dojo, Suikihime's Chambers, Tsujiken's Coin, Uncovering Treasures, Trade Embargo, Opening the Veil, Morito Inoue, Black Riders, Doji Kurohime, Journey's End Keep - exp (with P3).

**Today.** Every Gold cost is priced by `AbilityGoldCost`/`QuotePurchase`/`EquipGoldCost`/`QuoteStrategyGold` (`GoldQuotes.cs`, `MatchEngine.cs:909`). `GoldProduced(cardId, playerId, paying)` (`MatchEngine.cs:982`) is the single production function; `Producers` (`:1038`) lists the payer's unbowed cards in Home/Stronghold/Battlefield only; `PaymentTokens`/`UsePaymentTokens`/`PaymentSourcesFor` (`EmperorMechE.cs:149,205,232`) hard-code the non-card sources (`"<card>#reaction"`, `"#destroy:"`, `"#discount:"`, TfHoldings `"#alt"`); `ConditionalProduction(Amount, ProductionCondition When, Keyword, Threshold)` (`ConditionalProduction.cs:48`) is data with a fixed amount; `Pay` (`:1055`) bows the sources, calls `UsePaymentTokens`, then three hard-coded record calls. There is no payment channel on `EngineHooks`. Reactive tokens never bow their card and are computed from (player, subject) only, never from the other chosen sources. **(#670)** `PromoPaymentSources.cs` appends four ad hoc token kinds inside `PaymentTokens` for Coin Pouch (a token that bows its card and is valid only beside a Holding bowed in the same payment), Near Miss (a hand card discarded as it pays) and Profits (a negative `reduce` source and a discard source); read `docs/decisions/2026-10-05-667-payments.md`. They are the working precedent for items 1 and 6 below: generalise them, do not build a second mechanism beside them.

**Build** six hook channels, each called once from the named shared line, so every card of these shapes becomes a feature file:

1. `EngineHooks.PaymentTokens`: `Func<MatchEngine, PaymentTokenContext, IEnumerable<PaymentToken>>` appended inside `PaymentTokens`. Extend `PaymentToken` with `bool Bows`, `Func<MatchEngine,int,string[],bool>? RequiresOtherSources` (true when the co-chosen source ids satisfy the token, for "when another Holding produces for a Nonhuman" and "when a Holding bows to produce"), `Zone` allowance for `Hand` and `Attached`, and an `OnUse` callback (discard Truce from hand; discard Profits from play). `RequirePaymentTokens`/`ValidatePayment` check `RequiresOtherSources` against the whole `SourceIds`; `PaymentOptionsExcluding` post-filters suggestions that would violate it; `UsePaymentTokens` bows the card when `Bows` and runs `OnUse`.
2. `EngineHooks.ProductionCondition`: `Func<MatchEngine, ProductionConditionContext, bool?>` consulted by `ProductionConditionMet` before its switch, plus an optional `Func<PaymentSubject,int>? AmountOf` on `ConditionalProduction` so "he enters play for 0 Gold" (Colonial Dojo) and "Unique Personality" (Suikihime's Chambers) are data.
3. `EngineHooks.ProductionAdjustment`: additive `Func<MatchEngine, ProductionAdjustmentContext, int>` summed at the inline adjustment sum in `GoldProduced` (`MatchEngine.cs:988`), receiving the card, payer and subject; it must also run for the Stronghold (Opening the Veil) and for Holdings that enter later this turn (Trade Embargo stores a player-level turn modifier in your partial state).
4. `EngineHooks.PaymentProducers`: `Func<MatchEngine, int, PaymentSubject?, IEnumerable<CardInstance>>` appended to `Producers`, with a `LentProducers` store (card id, borrower, until end of turn) your feature fills for Morito Inoue and Black Riders; `GoldProduced` already prices from the payer's side; `ValidatePayment`'s "must be yours" check consults the store; `CardView.GoldProduced` for a lent card is computed for the borrower.
5. `EngineHooks.AfterPayment`: `Action<MatchEngine, AfterPaymentContext>` (player, cost, produced, subject, source ids) called at the end of `Pay`; move the three existing record calls behind it. Your feature keeps a per-player, per-phase spent-Gold ledger (Journey's End Keep) and a "produced Gold this turn" set per card (Doji Kurohime's "Holding that has not produced Gold this turn").
6. Widen `Producers` zones to `Attached` (Tsujiken's Coin, an Item with `PaymentProduction`) and `EventInPlay`/the zone an in-play Strategy uses (Uncovering Treasures), gated by the definition having a production rule.

Add `PaymentSource.Kind` values `"hand"` and `"bow-reaction"` so the client labels them (the client already renders `Sources` generically; check `Main.GoldPayment*.cs` renders an unknown kind as a plain button and add the two labels if not).

**Shared edits allowed:** `EmperorMechE.cs` (`PaymentTokens`, `RequirePaymentTokens`, `UsePaymentTokens`, `PaymentSourcesFor`, the `PaymentToken` record), `MatchEngine.cs` (`GoldProduced` adjustment sum, `Producers`, `ValidatePayment`, `Pay`, `PaymentOptionsExcluding`), `ConditionalProduction.cs` (the record and `ProductionConditionMet`), `GoldQuotes.cs` `GoldSourceProduction`, `EngineHookStartup.cs`. Nothing else.

**Steps.** (1) Write the channels and contexts in `PaymentHooks.cs` with the module initializer registering nothing. (2) Make the six shared edits; run the full Core suite to prove nothing changed (payment fingerprints in `GoldParityTests`, `GoldQuoteTests` must be identical). (3) Move Zlkyt's destroy token and Moto Rani's discount token, and **(#670)** the four `PromoPaymentSources.cs` kinds, onto the new `PaymentTokens` channel from their own files and delete the hard-coded branches; suite still green. (4) Wire the demonstration cards.

**Demonstration cards:** Truce (hand source that discards itself; condition "a Personality with a Clan alignment different from yours" from `PaymentSubject`), Ever-Winter Copse (a bowing token that requires another Holding among the co-chosen sources and a Nonhuman subject), Colonial Dojo (amount function), Trade Embargo (production adjustment with a player-level turn store), Morito Inoue (lent producer; target category "another player's Holding with Gold Cost lower than Inoue's Force" through `TargetAllows`).

**Tests.** A payment for a Nonhuman that names Ever-Winter Copse without another Holding is rejected with no state change, and the Copse is bowed by a payment that uses it; a suggested payment never includes a token whose `RequiresOtherSources` fails; Truce leaves the hand after paying and is not offered for a same-Clan Personality; Trade Embargo reduces a Holding bought later the same turn; a lent Holding is bowed by the borrower's payment and is not offered to its owner while lent; the three moved record calls still fire (Moneylender and P3 entry tests).

**Pitfalls.** Gold from one source is spent whole; excess is lost, so "only pay for that card" needs no code. Do not let a hand token appear in `PaymentSourcesFor` for the other seat. Keep `SourceIds` an opaque string grammar; new tokens use a new `#suffix`.

---

## P2. Entry and ability cost terms

**Unlocks (23):** Kakita Ujirou, Asukai, Hamuro, Jikoji, Kimura, Nishoji, Okazaki, Rokkaku, Sengmai, Tsiang, Yunmen (Ring-control waivers), Kuni Renyu - exp, The Shakash - exp, Verdant Jungle, Respect, Yoritomo Kanaye - exp, Shinjo Tselu, Chiyurei's Axe, Blade of Storms, Daigotsu Kikumura (Invest cost by Clan), Duty Over All Things (with P21), Foothold of the Mad, The False Path.

**Today.** `QuotePurchase` (`MatchEngine.cs:909-973`) sums a fixed list of discount terms (`ClanEntryDiscount`, `KeywordEntryDiscount`, `HoldingEntryDiscount`, `RoninEntryDiscount`, Reserve-defender, Ashiko, Prosperous, Forgotten Legacy, clan discount/allegiance, `CardGoldCost` modifiers); `EquipGoldCost` (`GoldQuotes.cs:507-555`) has its own list for attachments; `BaseAbilityGoldCost` (`:248`) has only the rule's own `Modifier`. `ClanEntryDiscount.cs` reads the payer's Clan only. No term can read the opponent's Clan, Defender status, a named Ring in play, the card's base cost, the province it is bought from, the Honor Requirement, or the ability's source type. `CostOption` verbs are Bow, Destroy and DiscardFavor (`AlternativeCosts.cs:25-31`); "bow X or pay N Gold" is two catalogue entries sharing a `UsageKey`.

**Build.**

1. `EngineHooks.EntryCostTerms`: `Func<MatchEngine, EntryCostContext, CostTerm?>` where `EntryCostContext` carries the card, payer, kind (`Recruit`, `Reserve`, `Equip`, `Overlay`), wielder, `ProvinceIndex`, `viaReserve`, `fromHand`, and `CostTerm(int Amount, string Label, bool IgnoreCost = false)`. Call it once in `QuotePurchase` after the existing terms and once in `EquipGoldCost`; sum the amounts, floor at zero, and when any term says `IgnoreCost` the quote is zero with the label in `Basis`. Move the existing `ClanEntryDiscount`, `KeywordEntryDiscount`, `HoldingEntryDiscount`, `RoninEntryDiscount` terms onto the channel from their own files so the switch shrinks (suite unchanged).
2. `EngineHooks.AbilityCostTerms`: additive `Func<MatchEngine, AbilityCostContext, int>` applied after `BaseAbilityGoldCost` (source card, ability, command). Yoritomo Kanaye: -1 for an ability whose source is the player's Strategy.
3. `CostVerbs.PayGold`: a `CostOption`/`ActionCost` verb priced by a nested `GoldCostRule` and paid through `Pay` with the ordinary source selection, including `UnlessKeyword`-style conditions. With it, "Bow Daito or pay 4 Gold" is one ability with two options and Kitsuki Judgment's "pay Gold equal to his unit's total Gold Cost, +8 unless he is dishonorable, Kolat, Ninja or Shadowlands" is `TargetUnitTotal(modifier: 8)` with a condition (P24 wires it).
4. Helpers in your feature: `ControlsRing(player, "Ring of Earth")` by title; `IsDefender(player)`; `OpponentIsClan(player, clan)`; `PrintedGoldCost(card) >= n`.

**Shared edits allowed:** `MatchEngine.cs` `QuotePurchase`, `GoldQuotes.cs` `EquipGoldCost` and `BaseAbilityGoldCost`, `AlternativeCosts.cs` and `AbilityCosts.cs` (the verb), `EngineHookStartup.cs`. The client's option label for the new verb (`Main.Decisions.cs` cost-option buttons) if the label is not already generic.

**Demonstration cards:** Kakita Ujirou (two terms: other player is Mantis Clan, -2; you are the Defender, -4; both outside Reserve), Jikoji (ignore cost while you control the Ring of Earth; his Battle action already has a shape), Kuni Renyu - exp (other Personalities with base Gold Cost 5 or more, -2; "your provinces have +2 strength" is `StaticProvinceBonus`), Yoritomo Kanaye - exp (ability cost term), Verdant Jungle's first line only if P8 has landed, otherwise leave it.

**Tests.** The quote shows each term with its label; a waiver yields a zero quote and Proclaim/Clan-discount flags still behave; terms do not apply to an overlay; Kanaye reduces a Strategy's action cost but not a Holding's; a `PayGold` option is unavailable when the player cannot produce the Gold and leaves no bowed source when another option is chosen.

**Pitfalls.** `GoldQuotes` is read by the client for every option; keep it cheap. Invest costs are not discounted unless the card says so (Gates of Chaos supplement), so your terms must not apply to the Invest Reaction's `Fixed` cost; Daigotsu Kikumura's own Invest term is a separate explicit rule.

---

## P3. Recruitment gates and Proclaim eligibility

**Unlocks (12):** Asp Skirmisher, Ikoma Natsu - exp, Suzume Shindo, Tsi Hayamizu, Zansho - exp, Ichiro Otani, Sengmai, Bayushi Hamada, Yoritomo Yashinko - exp, Hantei XVI - inexperienced, Delicate Gamble, Journey's End Keep - exp.

**Today.** Loyal is `LoyaltyAllowsControl` (`CardAbilities.cs:4094`); "will not join a Phoenix player" is one Effect in `ControlRefusedByClan` (`SeedsOfDecayBatch3.cs:111`); `CardDefinition.HonorRequirementWaiverClan` covers "X players ignore his Honor Requirement"; Proclaim eligibility is only `inClan` in `QuotePurchase` (`:972`) and `ProclaimAllowed`. There is no gate on the player's Family Honor, on the player controlling a Human, or on the Favor.

**Build** one channel, `EngineHooks.RecruitGate`: `Func<MatchEngine, RecruitGateContext, RecruitGateResult?>` with `RecruitGateContext(card, player, kind)` and `RecruitGateResult(string? RefuseReason, bool IgnoreHonorRequirement, bool ProclaimEligible, bool LoyalWaived)`. Call it in `QuotePurchase`'s error chain (a refusal becomes the quote's `Error`), in `LoyaltyAllowsControl` (Bayushi Hamada: Crab players may control him), in `ProclaimAllowed` (Yoritomo Yashinko: Crane players may Proclaim him) and in `TransferUnitControl` (join by control change). Move `ControlRefusedByClan` onto it. Add a player-level `IgnoresHonorRequirementsThisGame` flag in your partial state (Delicate Gamble).

**Shared edits allowed:** `MatchEngine.cs` `QuotePurchase` and `ProclaimAllowed`, `CardAbilities.cs` `LoyaltyAllowsControl`, `EmperorMechB.cs` `TransferUnitControl`, `EngineHookStartup.cs`.

**Demonstration cards:** Asp Skirmisher ("will not join a player who controls a Human Personality"), Ichiro Otani ("will not enter play if your Family Honor is 0 or above"; his duel needs P18, so publish him only if P18 has landed, otherwise take Ikoma Natsu - exp instead, whose Open Cavalry grant is `GrantKeyword`), Yoritomo Yashinko - exp (Proclaim by Crane players; "bow: Produce 3 Gold" is `PaymentProduction`), Hantei XVI - inexperienced (ignore Honor Requirement while you control the Favor; his search is `Ordered` discard-then-Fate-deck with `ToHand`; needs P23 for the "inexperienced" title).

**Tests.** A refused card is not purchasable and the view's quote carries the reason; the gate is re-evaluated when control would change; Proclaim is offered to a Crane player for Yashinko and not to a Lion player; Delicate Gamble's flag survives a save/replay round trip.

**Pitfalls.** The gate must not fire for an overlay (Experienced rules skip the Honor Requirement anyway). Keep `ProclaimAllowed`'s "no Clan discount" rule intact.

---

## P4. Turn-boundary hook, owner-turn durations, player-level granted abilities

**Unlocks (17):** Dagger of Crystal Wind, Abundant Farmlands, Doji Tatsuki - exp, Suck the Marrow, An End to Hostilities, An Empire of Madness, Harbingers of War, The Heavens' Blessing, Rejected Mediation, Open Recruitment, The Madness Spreads, Unseasonable Weather, Invocation, Rampant Paranoia, Bonds of Hospitality, Drain of Effort, Uncovering Treasures.

**Today.** `StartTurn` (`MatchEngine.cs:2842-2883`) and `StraightenPhase` (`:2885`) hard-code per-card calls; `AdvanceStraightenHolds(StraightenPoint.TurnBegins)` is the only generic turn-begin point; `BeginRentatsuLoss` shows how a turn-begin choice pauses. `DelayUntil` (`EmperorRulingR2.cs:46`) has EndOfBattle/EndOfPhase/EndOfTurn/NextDynastyPhase/EndPhaseBegins only. `CardInstance.ExpiresAtTurnsTaken` (`Models.cs:307`) expires Events in play (`:2872`); `GrantedAbilities` exist only on cards (`Models.cs:251`); there is no player-level granted ability and no "until your next turn / second turn begins" delay.

**Build.**

1. `EngineHooks.TurnBegins`: `Func<MatchEngine, TurnBeginsContext, bool>` (player whose turn begins, turn number) called in `StartTurn` right after `AdvanceStraightenHolds(TurnBegins)`; a callback returns `true` when it opened a pending choice, and `StartTurn` then stops and resumes the way `BeginRentatsuLoss` does. Every card's "after your turn begins" / "after each player's turn begins" / "after another player's turn begins" trait is a registrant that filters on the player.
2. `DelayUntil.OwnersNextTurnBegins`, `OwnersSecondTurnBegins`, `GameEnd`, applied from your `TurnBegins` registrant with `StraightenHolds`' Owner/Skip semantics (the Until records the owner id and a turn count). Reuse for `ScheduleTimedRemoval` so created cards and Events can expire the same way.
3. `PlayerState.GrantedAbilities` (`List<GrantedAbility>` with a `Until`): read by `AbilityIdsOf`/`AbilityActions` so a player-level ability is offered like `kharmic-discard` is; cleared by your `TurnBegins` registrant or at game end. This is what "each player has the ability ..." Events grant.
4. A `TurnScopedRules` store in your partial state for standing multi-turn rules (Heavens' Blessing's "pay 2 Gold to draw from your own Strategies in an Action Phase", An End to Hostilities' end-turn double draw, The Madness Spreads' once-per-turn Limited-as-Open), each a record with an owner, an `Until` and a kind, consulted by the one existing choke point each kind needs (name them in your PR).

**Shared edits allowed:** `MatchEngine.cs` `StartTurn` (one call) and `EndTurn` (the draw step for An End to Hostilities), `EmperorRulingR2.cs` (`DelayUntil` constants and the apply switch), `Models.cs` `PlayerState` (one property), `CardAbilities.cs` `AbilityIdsOf`/`AbilityActions` (read player grants), `EngineHookStartup.cs`.

**Demonstration cards:** Abundant Farmlands (straighten after each player's turn begins; `Produce 1`), Doji Tatsuki - exp (straighten after another player's turn begins), Dagger of Crystal Wind (Obsession +1F token each own turn; destroy when tokens exceed printed Chi: `TokenCount > Definition.Chi`), Rampant Paranoia (an Event whose rule lasts until your next turn begins; its "will not destroy your defending Personalities" needs P12, so wire it only if P12 has landed, otherwise take A Fever in the Blood's shape as a regression test and wire Bonds of Hospitality's granted ability instead if P12 is absent).

**Tests.** A turn-begin trait fires once per qualifying turn and not for the wrong player; a delayed effect until "your next turn begins" survives the other player's turn and fires at the right start; a player-granted ability appears in `QueryActions` for that player only and vanishes when its Until passes; replay from a save in the middle of a pending turn-begin choice resumes.

**Pitfalls.** `StartTurn` runs before the Straighten Phase; "after your turn begins" traits resolve before straightening, so Tatsuki straightens on the other player's turn, not his own. Do not put the player grants on the Stronghold card as a shortcut; they must be removable per player.

---

## P5. Province-reveal Events

**Unlocks (8):** A Dangerous Game, An Empire of Madness, Harbingers of War, Rejected Mediation, Revelations, Open Recruitment, The Jade Championship, The Onyx Championship (each also needs the primitive named in its appendix row).

**Today.** `EventsPhase` → `ContinueEventsReveal` (`MatchEngine.cs:2952`) loops left to right; a revealed Event goes to `ResolveEvent` (`:3000`), which asks the `P3BeforeEvent` window, then the registered `ResolveEvent` hook (`PromoEmperorEvents.cs:84`; it is missing from `EngineHookStartup.Build`, add it), then the default path removes, discards and refills. An already face-up Event is re-resolved inline (`:2981`). After the loop: `AdvanceStraightenHolds(EventsRevealEnds)` (`:2983`), then `ResolveOngoingEvents`.

**Build.** A `CardDefinition`-side flag in your feature's catalogue (`StaysFaceUpInProvince` by Effect id, not a `CardDefinition` edit) and `EngineHooks.AfterProvincesRevealed`: `Func<MatchEngine, AfterRevealContext, bool>` called once at `:2983` for the active player, with the list of face-up Events still in their provinces. Your registrant resolves each flagged Event's text ("After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in one of them") once per turn (a per-card turn stamp from P7 if landed, else `AbilityUses`) and leaves the Event in the province; the `ResolveEvent` registrant for these ids returns `false` without moving the card, and `:2981` skips flagged Events. Return `true` when a choice opened (A Dangerous Game's "you may discard your hand to draw four" is an Either/Or). The Events Phase must resume after the choice exactly as the Celestial-reveal pause does.

**Shared edits allowed:** `MatchEngine.cs` `ContinueEventsReveal` (the call and the skip at `:2981`) and `ResolveEvent` (honour the flag), `EngineHookStartup.cs` (also add `ResolveEvent`).

**Demonstration cards:** A Dangerous Game (Either/Or; the Event stays face up and fires again next turn while it is still there), Revelations (show top five Dynasty cards, the other player may discard one via a decider choice over shown definitions, reorder, refill this province face up: needs P20's definition-decider; if absent, wire A Dangerous Game and Harbingers of War's draw half as the second card, leaving Harbingers unpublished).

**Tests.** The Event resolves on the owner's Events Phase only, once per turn, does not resolve when face down, is still in the province afterward, and is discarded normally when bought over or destroyed with the province; a save in the middle of its choice resumes; an ordinary Event's behaviour is unchanged (existing Event tests).

**Pitfalls.** Under the Gates of Chaos supplement, Coils/Gates/Aftermath Events with a printed "Limited:" resolve as traits in the Events Phase; that is the existing default path, not this primitive.

---

## P6. After-event records and windows

**Unlocks (14):** Yogo Katsuta, Doji Hakuseki - exp3, Susumu Kuroko, Akumasa, Asahina Shigemitsu - exp, Unseasonable Weather, Open Recruitment, The Crystal Tears, Iweko Shibatsu, Benten's Blessing, Natsu-Togumara's Guidance, Shrine to the Heavens, Magistrate's Stipend, The Jade Championship.

**Today.** After-action facts are `LastResolvedAction` (`Models.cs:1273`) recorded through `RecordActionEffect` (`ActionEffectTracking.cs:31`; kinds destroyed, moved, bowed-ring, bowed-personality, won-duel, ring-entered, assigned) and read by the `afterActionResolvedReaction` window, whose sentinel pattern (`AssignedSentinel`, `EmperorRemainingG.cs:89`, written by `OpenAssignedReactionWindow` at `:235`, gated to two cards) lets a non-action fact open the same window. Seppuku has no record (and Order Seppuku skips `FinishActionEffectTracking`, a leak); Proclaim has no after-trigger (`FinishProclaim`, `SeedsOfDecayBatch2.cs:240`); assignment to attack fires only `FireAssignedToAttackWatches`; a province card turning face up publishes nothing; `DiscardTerrain` (`CardAbilities.cs:10850`) only emits a log event; a Celestial enters through `ResolveCelestial` with no entry window or choice; "after X leaves play" has no window; the announce window asks one watcher card at a time.

**Build.** One generic opener, `OpenFactWindow(FactRecord fact)`, in your feature: it writes a `LastResolvedAction`-shaped record with `AbilityId` = a sentinel per fact kind and opens `afterActionResolvedReaction` only when some card in play or hand holds a Reaction whose `Trigger` matches the fact (the "anyone holds a matching Reaction" gate), and runs mandatory traits registered for the fact first. Facts to add, each from one choke point: `seppuku` (`CommitSeppuku`; also call `FinishActionEffectTracking` on Order Seppuku), `proclaimed` (`FinishProclaim`, with the card id), `assigned-to-attack` (ungate `OpenAssignedReactionWindow`, record the province), `turned-face-up` (the reveal sites and face-up refills), `terrain-destroyed` (`DiscardTerrain`), `celestial-entered` (a choice point before `ResolveCelestial` discards the other Celestials, returning a pending decision), `left-play` (the discard/removal sweep), and `announced-with-performer` (the P3 announce window widened to any registrant with a performer filter, for Magistrate's Stipend's "+1C until the action ends", whose modifier is cleared by `FinishActionEffectTracking`).

**Shared edits allowed:** `ActionEffectTracking.cs` (new kinds), `MatchEngine.cs` (`CommitSeppuku`/Order Seppuku, `ResolveCelestial` pause, reveal sites, leave-play sweep), `SeedsOfDecayBatch2.cs` `FinishProclaim`, `EmperorRemainingG.cs` `OpenAssignedReactionWindow` gate, `CardAbilities.cs` `DiscardTerrain`, `EmperorP3Priority.cs` announce registrant.

**Demonstration cards:** Yogo Katsuta (trait: after a Personality commits seppuku, his controller loses 1 Honor), Susumu Kuroko (after you Proclaim Susumu Takuan, gain 2; Political Limited bow take the Favor exists), Akumasa (Open: target another player's Personality with a Madness token; after he attacks this turn, draw: an `assigned-to-attack` fact watched by a turn-scoped record), Natsu-Togumara's Guidance (Celestial entry choice of a Personality whose unit battle resolution does not bow: `AfterResolution.ResolutionWillNotBow` with the stored id).

**Tests.** Each fact opens the window only when a matching Reaction exists; a mandatory trait fires before any Reaction; Order Seppuku no longer leaks into the next action's record; the Celestial entry choice pauses the Events Phase and resumes; replay round-trips the stored Personality.

**Pitfalls.** R-022: destruction Reactions distinguish battle deaths from action deaths; keep the new facts' `PlayerId` and `SourceId` honest so filters like "another player's" work. Do not re-open the window from inside itself.

---

## P7. Period stamps and negation watches

**Unlocks (20):** Daigotsu Gyoken - exp3, Legion of the Fallen, Hida Tadama, Hiruma Ikeuchi, Bayushi Kazutoshi - exp2, Koan's Robes, Avenging the Fallen, Earth's Protection, Fortune's Charm, Harbingers of War, Rejected Mediation, Imperial Deployment, Asahina Shigemitsu - exp, Bayushi Kachiko, Minor Blackmail, Lessons from the Heralds, Pierce the Tapestry, Trade Embargo, An End to Hostilities, Journey's End Siege.

**Today.** Turn use: `AbilityUses` stamp; phase: `TrySpendPhaseUse` (`PhaseLimits.cs:40`, imperative); game: `OncePerGame`; battle: ad hoc keys. One-shot shields are scattered: `DestructionShieldTurn`, `TargetShieldTurn`, `ParalyzingTouchWatches`, `StandingRedirects`, Stalwart's per-turn stamp, `MenacingSkyWatches`/`RestraintShields`.

**Build.**

1. `TrySpendPeriodUse(uses, key, Period)` with `Period.Turn|Battle|Phase|Game` next to `TrySpendPhaseUse` (battle key from the battle number), and an `AbilityDefinition.UsesPer` field (`Period?`) so a triggered trait or Reaction declares "the first time each battle/phase/game" and `ValidateAbility` enforces it in the same place it enforces `OncePerGame`.
2. A `NegationWatch` store in your partial state: `(Id, OwnerCardId or PlayerId, Kind: bow|destroy|move|straighten|dishonor|honor-gain|political-action, SourceFilter: other-players|any, Count, Period, Until, Arg)`; a helper `TryConsumeWatch(kind, targetCard, sourcePlayer)` consulted at the existing negation choke points: `DestroyUnits`/`DestroyAttachment` (destruction), the bow helper that `StalwartNegatesBow` sits in, `TryMoveToBattlefield`/`MoveHome`, straighten, `Dishonor`, and the announce negation chain for "negate the first Political action each turn". Each consumption records what it negated so the card's follow-up effects (Kazutoshi: move him home, bow him, dishonor him) run.
3. Turn-scoped counters as watch records with `Count` and a `Kind` of `count` (Pierce the Tapestry's copies played this battle, An End to Hostilities' cards put in hand this turn, Trade Embargo's one copy per turn, Lessons from the Heralds' won a duel this turn).

**Shared edits allowed:** `PhaseLimits.cs`, `CardAbilities.cs` (`AbilityDefinition.UsesPer` and its check in `ValidateAbility`), the six choke points above (one `if (TryConsumeWatch(...))` line each), `EngineHookStartup.cs` only if you add a channel.

**Demonstration cards:** Daigotsu Gyoken - exp3 (first destruction each game from another player's card), Legion of the Fallen (first time this game another player's card destroys this Follower), Hiruma Ikeuchi (first time each battle, if you have Reconnaissance), Avenging the Fallen (straighten performer; negate his next destruction this turn if another player's Battle action destroyed one of your Personalities this battle: a `count` record fed from the destroyed kind), Trade Embargo's one-copy-per-turn rule.

**Tests.** A game watch persists across turns and is consumed once; a battle watch resets at the next battle and not at the next action; a watch owned by a card leaves with the card; the negated destruction leaves the card in play with the follow-up effects applied; a wholly negated action does not count toward a counter (R-004).

**Pitfalls.** Stalwart already negates the first bowing per turn; do not double-negate with a watch of the same kind. Protections and the delay store run first; keep the order: delay, protection, watch, then the effect.

---

## P8. Action-effect watch (Interrupt facts)

**Unlocks (3):** Moto Taigo - exp, Reverse Fate, Verdant Jungle.

**Today.** The before-action window (`OpenNextTargetReactionWindow`, `TargetWindows.cs:50`) predicts movers via `ActionMovers` (from `MoveSpec`) and curated `DestroysTarget`/`BowsTarget` ids; it cannot see discards or destinations. The delay capture (`ArmDelayForHeldAction`, `EmperorRulingR2.cs:135`; the design is described in the file's header comment) is the working model of "do something to what the action does": every choke point consults `NegatedEffects` for a `delay:` entry. `RecordActionEffect` records destroyed/moved only; there is no `discarded` kind and no created-Personality kind. **(#670)** `PerformersAmbush.cs` makes `CreatePersonality` record a `created` kind (location and whether it had Shadowlands) for Lying in Ambush; when it is present, reuse that kind instead of adding `CreatedPersonalityKind`.

**Build.** Generalise the delay capture into an `ActionEffectWatch` armed in the before-action window by a Reaction: `(HeldActionKey, Kind: moved|discarded|created-personality|honor, Callback id, Arg)`. `RecordActionEffect` gains `DiscardedKind` (from the hand-discard and deck-discard helpers, with the card id and its Focus Value) and `CreatedPersonalityKind` (from `CreatedCardEntry`), plus a destination on `MovedKind` (battlefield index and side). When a kind is recorded on the held action, the watch's callback runs immediately with the fact (Moto Taigo: destroy a card without attachments in one of the moved units, via a pending target choice; Reverse Fate: give the first discarded card +1FV or -1FV, a Focus Value `StatModifier` that `DuelStatValue` must read for cards in a duel is explicitly excluded by the card, so it applies to any other Focus Value read). Verdant Jungle negates the movement when the destination is an attacking army at its province's battlefield (`MovementCannotBeNegated` is honoured).

**Shared edits allowed:** `ActionEffectTracking.cs` (kinds and `RecordActionEffect`), `EmperorRulingR2.cs` (the capture), the discard helpers in `CardAbilities.cs` and `CreatedCardEntry.cs` (one record call each), `TargetWindows.cs` if the window must open for an untargeted action with a registered watcher (it opens today only when a reactor exists for a target or a cost subject).

**Demonstration cards:** Moto Taigo - exp (Interrupt, bow), Reverse Fate (Void Interrupt, bow this Spell).

**Tests.** The watch fires once per recorded kind and only for the held action; an action that moves no unit lets Taigo's Interrupt be announced (the constraint is a cost, the condition is an effect) with no destruction; Reverse Fate changes the first discarded card's Focus Value for a later Tactical Advantage but not inside a duel; the watch is dropped when the action is wholly negated.

**Pitfalls.** Coils rule 4: an Interrupt refers only to the interrupted action. Do not reuse `LastResolvedAction` for the live facts; it is written when the action finishes.

---

## P9. Stat-term registry, card stat caps, contribution rules

**Unlocks (34):** see the appendix `P9` rows (Bayushi Hamada, Hida Kaiji - exp, Hiruma Ogata, Moto Daiken, Moto Ogaru, Tochiko, Toku Saiga, Tsi Hayamizu, Yoritomo Toganin, Usagi Seki, Isawa Norimichi - exp, Asako Kaitoko - exp, Shosuro Chizuru, Armor of the Mad, Bayushi Kachiko, Kshatriya Artifacts, Well-Defended Farm, Riding to the Rescue, Ready the Defenses, Stonemason's Hammer, Koan's Robes, Koan's Whisper, Unseemly Alliance, Mumoku no Oni, Shosuro Tanihara, Chuda Kaito, Utaku Ji-Yun - exp, Matsu Sango, Bayushi Manami - exp, The Sky's Barrier, Warded Premises, Tamori Tsuchiya, Foothold of the Mad, The Jade Championship).

**Today.** `AppliedModifier(Source, Effect, Stat, Amount, Permanent)` has two durations; stats read through `StatModifier` are Force, Chi, PersonalHonor, GoldCost, GoldProduction. Statics are per-card branches in `PersonalityForce` (`MatchEngine.cs:2184`), `FollowerForce` (`:2296`) and `ItemForceToWielder`; the only Force hook is `PersonalityForce` (Personalities). Caps: `ProvinceStrengthMax` (`TerrainStatics.cs`) is generic; card maxima do not exist except dishonorable PH 0 (`:2173`). Contribution: `PersonalityBattleContribution`/`FollowerBattleContribution`/`ArmyForces` (`:2394-2416`) with `EliteEffect`, `ForceContributionNegated`, `ArmyForceBonuses` (per-battle records).

**Build.**

1. A declarative `StatTerm` catalogue in your feature: `(Id, Scope: self|unit|army|controller|all|opposing, Stat, Amount or PerCount, Condition, Duration)` where `Condition` is a small closed set evaluated live (`Defending`, `Attacking`, `Opposed`, `Unopposed`, `OpposingLowerForce`, `NotYourTurn`, `OtherPlayersActionResolving`, `EnemyLeaderControls(keyword or Favor)`, `ControllerIs(clan)`, `OtherPlayerIs(clan)`, `HasKeyword`, `WhileOwnActionResolving`) and `PerCount` is a counter (`TokensOfKind on self/opposing`, `ControlledWithKeyword`, `AttachedWithKeyword`, `OtherKeywordInArmy`, `ClanAlignmentsInArmy`, `DeadInDiscard`, `RingsControlled`). Evaluate it from `PersonalityForce` (already a hook), a new `EngineHooks.FollowerForce` and `EngineHooks.PersonalityChi` channel (additive), and `CardPersonalHonor`.
2. `StatCap` records on `CardInstance` or your state: `(CardId, Stat, Max?, Min?, Until)` read by every stat getter after modifiers (Kaitoko's "maximum Force of 3 while opposing", Chizuru's "maximum Personal Honor of 4", Armor of the Mad's PH 0, Utaku Ji-Yun's override of the dishonorable PH cap for Paragons).
3. `AppliedModifier.Duration` gains `Battle` and `WhileCondition`, swept where turn modifiers are (`:2832`) and at battle end.
4. `EngineHooks.ArmyForce`: additive hook in `ArmyForces` for static army totals (Manami's "+2F during resolution"), and `ContributionRule` records `(CardId, Multiplier, OnlyWhileBowed)` applied in `PersonalityBattleContribution` (Matsu Sango counts twice while attacking; The Sky's Barrier's Elite that does not contribute while unbowed); Warded Premises' "negate all Force bonuses from actions at this battlefield before resolution" is a `BeforeResolution` Terrain trait that zeroes action-sourced modifiers on cards there (modifiers carry their `Effect`, so filter on action sources).
5. `ComputedBonus` on `AbilityDefinition` (`Stat`, `From: performer|source|target|discarded`, `Read: Force|Chi|PH|FocusValue|Count`, multiplier, plus) applied by your `RunEffects` registrant so "Force bonus equal to his current Force" is data.

**Shared edits allowed:** `MatchEngine.cs` (`FollowerForce`, `ArmyForces`, `PersonalityBattleContribution`, `CardPersonalHonor`, the modifier sweep), `CardAbilities.cs` (`PersonalityChi`, `AbilityDefinition.ComputedBonus`), `Models.cs` (`AppliedModifier.Duration`), `EngineHookStartup.cs`.

**Demonstration cards:** Tochiko (+2F while opposing a lower-Force card), Toku Saiga (+1F/+1C per Weapon), Hiruma Ogata (+1F per opposing Madness token), Asako Kaitoko - exp (Force cap while opposing; the targeting half needs P11, so publish him only if P11 has landed), Matsu Sango (contributes twice; "will not attach Items" is `WillNotAttachItems`), Bayushi Manami - exp.

**Tests.** Each term changes the stat only while its condition holds and never double-counts with a printed modifier; a cap applies after bonuses and is removed at its Until; a battle-duration modifier survives actions within the battle and is gone at the next battle; army totals in resolution match the sum of contributions plus static army bonuses; the `PersonalityForce` fingerprint tests in `EngineHookSequences` tests still pass.

**Pitfalls.** Items add Force to the Personality regardless of bowing; Followers add their own. "Printed" and "base" mean the same thing. Chi 0 destroys immediately and cannot be negated; a Chi term must go through `ApplyModifier`'s death check, not bypass it.

---

## P10. Keyword-rule registry

**Unlocks (13):** Chun, Uji's Saboteurs, Chikushudo's Infusion, Elephant Cavalry, Drawing on All Things, The Inevitable Grasp of Conquest, Scorching Lash, Ikoma Ayumu - exp, Iweko Seiken, Iweko Shibatsu, Blood of Isawa's Tribe, Suikihime's Sanction, The False Path.

**Today.** `Keyword()` (`CardAbilities.cs:3930`) is a fixed chain: removed, static suppressions, Experienced, Unaligned, printed plus `GrantedKeywords`, live conditionals (Naval while has Spell, Jun-Ni's unit Cavalry, Yayu's Cavalry when not your turn), `StaticKeywordGranted`. `GrantKeyword` (`:4102`) has Attachment/Turn/Permanent/Copy durations and one one-shot block (`PurifyTheSoulNegatesGrant`). No hook; no persistent "will not gain X"; no element replacement; no army or controller scope.

**Build.** `EngineHooks.KeywordRule`: `Func<MatchEngine, KeywordRuleContext, KeywordRuleResult?>` consulted once inside `Keyword()` after the granted keywords, with `(card, keyword)` in and `Granted|Suppressed|null` out; plus a `KeywordRules` catalogue in your feature: `(Id, SourceEffect or SourceCardId, Scope: self|unit|army|controller|all|in-and-out-of-play, Keywords, Condition)` reusing P9's condition set (`WhileHas("Shadowlands")`, `WhileControllerIs("Crane Clan")`, `WhileInArmyOf(source)`, `RingElements`). Add `KeywordGainBlocks` (card id, keywords or `any-clan`) honoured by `GrantKeyword`, `PermanentlyCopy*` and created-card clan grants, and an `ElementReplacement` map (card id, from, to) applied in `Keyword()` and in `ClanAlignments`/element reads. Move the three live conditionals in `Keyword()` onto the channel.

**Shared edits allowed:** `CardAbilities.cs` `Keyword()` (one call), `GrantKeyword` (block check), `ClanAlignments` (replacement and blocks), `EngineHookStartup.cs`.

**Demonstration cards:** Chun (Human Personalities in Chun's army have Naval), Uji's Saboteurs (this Personality has Naval while he is Crane Clan; its Battle attachment destroy exists), Chikushudo's Infusion, The Inevitable Grasp of Conquest's first line (the Battle action needs P12), Iweko Shibatsu ("will not gain a Clan Alignment"; his Honor halves need P6).

**Tests.** A rule applies and lapses with its condition; `IsCavalryUnit` sees a unit-scoped grant; a blocked keyword is not granted by `GrantKeyword`, by a copy, or by a created card's controller clan; element replacement changes `Keyword(card,"Fire")` and the card's Spell eligibility; the frozen `Keyword()` fixtures stay identical.

**Pitfalls.** R-015/R-016: Ninja is a keyword, not a clan. "Unaligned" means no clan alignment and is computed, never printed.

---

## P11. Card-restriction store and generalised target bars

**Unlocks (23):** Natsumi - exp, Matsu Sango, The Lost Colossus, Bayushi Jin-e, Kamalakar's Harem, Otomo Suikihime - exp, Hida Kaiji - exp, Isawa Ryuzo, Generational Gap, Iweko Seiken (with P10), Marriage of the Emerald Champion, Mirumoto Kojinrue - exp, Asahina Umehiko, Ninube Onchi - exp, Asako Kaitoko - exp, Tankoji, A Welcome Reprieve, Koan's Jingasa, P'an Ku's Madness, P'an Ku - exp, The Egg of P'an Ku - exp, Sudden Blockade, Kitsune Yuribara, Yoritomo Naoto - exp.

**Today.** Stores exist for action locks, movement locks (this turn), straighten holds, `TargetBars` (one actor card, turn-long, `TargetBars.cs:8`), `TargetShield`, disabled abilities, `AssignBowedTurn`, `WillNotAttachItems` (Items only, Effect-keyed), `WillNotBeTransferred`, player-wide `MayNotAttackThisTurn`. Missing: per-card no-attack/no-defend with a duration, pairwise exclusions, no-challenge, attach restrictions for Followers/Armor/all cards, a dishonor shield, a province entry lock, "may act while bowed" as stored status, actor-set or action-keyword target bars, forced targeting ("must target X if legal").

**Build.** A `CardRestrictions` store in your partial state: `(Id, CardId or ProvinceIndex or PlayerId, Kind, Arg, Until)` with kinds `no-attack`, `no-defend`, `no-challenge`, `no-attach(type|keyword|any)`, `no-dishonor`, `no-pair(otherCardId)`, `no-entry-from-province`, `act-while-bowed`, `assign-while-bowed`, `spell-limit(counter)`, and `Until` reusing P4's owner-turn durations when present (else turn/permanent/while-in-play). Consult it from the one existing check each kind needs: `Assign` (attack/defend/pair/bowed), the duel issuer, the attach legality in `AttachAction.cs`, `Dishonor`, `Purchase`/`RecruitReserve` (province lock), the bowed-performer exemption list (replace the per-id list with a store lookup plus the old ids). Generalise `TargetBar` to `(TargetCardId, ActorPredicate: card|unit-of|controller|action-keyword, UntilPhaseInstance or Turn)` and add a `ForcedTarget` rule (`actor filter → must target card if legal`) consulted in `RequireNotBarredFromTargeting` using `ActionAcceptsTarget`.

**Shared edits allowed:** `MatchEngine.cs` `Assign`, `Purchase`/`RecruitReserve` (province lock), `AttachAction.cs` legality, `Dishonor`, `Dueling.cs` issuance, `CardAbilities.cs` bowed-performer exemptions and `RequireNotBarredFromTargeting`, `TargetBars.cs`.

**Demonstration cards:** Natsumi - exp (will not attach Followers or Items), The Lost Colossus (enters bowed; will not attach cards; will not challenge; Political actions may not target it; the supplement's note: other players may still target it with Political actions? No: the supplement says players other than its controller MAY target the Colossus with Political actions, so the bar is for its controller only), A Welcome Reprieve (phase-scoped bar against other players' actions), Kamalakar's Harem (this Personality may not attack; Limited bow: dishonor a Human and destroy this Follower), Generational Gap (two Personalities with the same family name may not be assigned to the same battlefield; family name per the Torn Asunder rule: the first word of a two-word title when it is not an ordinary English word and the card has a Clan alignment).

**Tests.** An assignment that would pair the two cards is rejected with no state change while assigning one of them elsewhere is allowed; a no-attach restriction blocks Equip, attach actions and created attachments alike; a phase-scoped bar expires at the phase end and not the turn end; forced targeting narrows the candidates only when the forced target is legal; the bowed exemption still covers the old ids.

**Pitfalls.** "May not" forbids the choice (the option must not be offered); "will not" lets the action be taken and fails the effect. Lantern Oil's engage bar and Temple Grounds belong to P13, not here.

---

## P12. Immunity ledger

**Unlocks (13):** A Cleansing Breath, Imperial Deployment, Path to Inner Peace, Thunder Bow, Rampant Paranoia, The Inevitable Grasp of Conquest, Susumu Haikaro, The Earth's Armor, Map of the Northern Colonies, Harbor of Kalani's Landing, Oracle of the Void - exp, Asukai, Bonds of Hospitality.

**Today.** Negation sources are scattered (`NegatesEffect`, `EnemyActionMovementAutoNegated`, the delay store, shields, `Twin Forks`, `Request Authorization`); `MovementCannotBeNegated` is per command, not per card; `DestructionShieldTurn` is one turn; `ResolutionWillNotBow` exists but no resolution-destroy immunity; nothing lets a card ignore "effects that prevent him assigning, moving or performing actions"; `TerrainStatics` has no movement negation. **(#670)** Winter Sled adds `MovementNegationBarred(mover, negatingPlayerId)` (`PerformersSled.cs`) and stamps the negating player on `NegatedEffect.ByPlayer` and `MovementLock.ByPlayer`; read `docs/decisions/2026-10-05-667-performers.md`. When it is present, your `move-negation` kind is a second caller of that predicate and the other kinds reuse the `ByPlayer` stamps for their source filter.

**Build.** An `Immunities` store in your partial state: `(Id, CardId or PlayerId or BattlefieldIndex, Kind, SourceFilter: other-players|any|other-terrains, Until)` with kinds `bow`, `destroy`, `destroy-in-resolution`, `move-negation` (the card's movement cannot be negated), `prevention` (effects that prevent assigning, moving or performing actions do not apply), `force-penalty` (negate Force penalties), `province-strength` (other players' cards do not affect this player's provinces' strengths), `terrain-destroy`, `movement-at-battlefield` (Harbor: negate Personalities' movement to or from this battlefield). One helper `IsImmune(card, kind, sourcePlayer)` consulted at the matching choke points: the bow helper, `DestroyUnits`/`DestroyAttachment`, the resolution destroy step (`MatchEngine.cs:1625-1671`), `MovementLocks`/`ActionLocks` reads and the `AssignBowedTurn` check in `Assign` (prevention), `ApplyModifier` for Force penalties, `ProvinceStrength`, `DiscardTerrain`, and `TryMoveToBattlefield`/`MoveHome` (battlefield movement negation and move-negation immunity: when a movement would be negated by any source and the mover is immune, the negation is skipped).

**Shared edits allowed:** the choke points named above (one `if` each), `TerrainStatics.cs` for the battlefield movement rule, `EngineHookStartup.cs` only if you add a channel.

**Demonstration cards:** The Earth's Armor (while defending, other players' cards will not bow this Shugenja), Susumu Haikaro (negate his destruction from battle resolution), Rampant Paranoia (until your next turn begins, needs P4's duration; else Turn), A Cleansing Breath (negate other players' effects that prevent him assigning, moving or performing actions; straighten his unit if Kensai), Oracle of the Void - exp's "cannot be destroyed" line only if P21's refill hook has landed (the card is published whole or not at all).

**Tests.** Immunity blocks the matching effect from the filtered source and nothing else; a prevention immunity makes a locked card assignable again and the lock still applies to others; a resolution immunity leaves the card in play and the opponent still gains Honor for the other destroyed cards; Terrain movement negation stops moves in and out but not assignment before engagement; immunities leave with the card.

**Pitfalls.** R-017: whole-unit bows honour per-card prevention; your `bow` immunity must be checked per card inside unit bows. Do not reach for the delay store: an immune effect is negated, not delayed.

---

## P13. Phase and battle rule flags

**Requires #670** (`PhaseOrder.cs` and its `BeginPhaseAfterAction` are added there; on `main` before it, the Action Phase ends only through the pass count at `MatchEngine.cs:1174`, `SetPhase(Phase.AttackDeclaration, ...)`).

**Unlocks (8):** Chagatai's Wrath, Lantern Oil, Temple Grounds, Shosuro Takazaki, Journey's End Siege, The Madness Spreads, An Empire of Madness, The False Path.

**Today.** `PhaseOrder.cs` swaps Attack and Dynasty only; `player.MayNotAttackThisTurn` exists; `LimitedInsteadOfOpen` (Plans Within Plans) is a one-shot armed flag; the announce-time negation chain is in `ContinueUseAbility` (`CardAbilities.cs:7130+`); the engage validation is at `CardAbilities.cs:5476`; battle-victory Honor at `MatchEngine.cs:1625/1652/1670`; Terrain play in `PlayStrategy` (`:2041-2060`); victory checks in the turn-start/End Phase code.

**Build.** A `RuleFlags` store in your partial state: `(Id, Kind, Scope: player|battlefield|battle|turn|game, Arg, Until)` with kinds `end-action-phase` (applied immediately through `BeginPhaseAfterAction`), `no-engage-for-attacker`, `negate-battle-action-without-units` (unless the flag's owner controls a Cavalry unit there), `no-destruction-honor-for-others`, `defenders-may-not-play-terrains`, `limited-as-open-once-per-turn(strategy-source)`, `no-enlightenment-victory`, `madness-loss(threshold)`. One read at each choke point listed above.

**Shared edits allowed:** `PhaseOrder.cs` or `MatchEngine.cs` `BeginPhaseAfterAction` (callable mid-command), `CardAbilities.cs` engage validation and announce negation chain, `MatchEngine.cs` battle Honor, `PlayStrategy` Terrain check, the victory check, `EmperorRemainingB.cs` `LimitedInsteadOfOpen` (per-turn variant).

**Demonstration cards:** Chagatai's Wrath (Limited: end the Action Phase; discard this Celestial), Lantern Oil, Shosuro Takazaki (Political Engage: other players gain no Honor for destroying your cards in this battle's resolution), Temple Grounds (its Battle/Engage self-replacing Terrain play exists).

**Tests.** Ending the Action Phase mid-command moves to the Attack-or-Dynasty step honouring `PhaseOrder`; the engage bar hides the Attacker's Engage actions at that battlefield only; the resolution Honor is zero for the opponent and normal for the flag's owner; Temple Grounds negates a Battle action announced by a player without units there and the costs are still paid; flags expire at their scope.

**Pitfalls.** A negated action still pays its costs (preamble rule 5). Chagatai's Wrath ends the phase even if the active player had actions left; the other player gets no further Open action.

---

## P14. Honor-change modifier registry

**Unlocks (8):** Court Scribe, Kimura, Foothold of the Mad, The False Path, Marriage of the Emerald Champion, Invocation, The Heavens' Blessing, Merchant Patronage.

**Today.** `GainHonor` (`MatchEngine.cs:3267-3334`) applies a dozen hard-wired modifiers and opens the `honorChangeReaction` window (`EmperorRulingR4Honor.cs:76,154`) with `PendingHonorChange(player, amount, reason, action key)`; Chugo's "-2 to gains and losses from an action" and Rhetoric's redirect exist as per-card code. There is no standing modifier store.

**Build.** `EngineHooks.HonorChange`: `Func<MatchEngine, HonorChangeContext, HonorChangeResult?>` consulted in `GainHonor` before the window, with `(player, amount, reason, actionKey, sourceCardId, sourcePlayer, causedByOwnCard, targetedCards)` in and `(newAmount, redirectToPlayer?, consumedModifierId?)` out; plus an `HonorModifiers` store in your partial state: `(Id, OwnerPlayer, Kind: next-change|next-proclaim|each-gain|no-gain|no-loss, Delta or SetToFocus, Filter: own-cards|other-players-actions-that-targeted(card)|unless-controls(keyword)|action-phase-only, Until)`. Move Chugo's reduction and Rhetoric's redirect onto the channel as the first registrants.

**Shared edits allowed:** `MatchEngine.cs` `GainHonor` (one call; make sure `reason`/`actionKey`/source are passed by every caller, adding the parameter where a caller omits it), `EmperorRulingR4Honor.cs`, `EngineHookStartup.cs`.

**Demonstration cards:** Court Scribe (Political Open, bow: increase the next Honor gain or loss this turn from your cards by 1; Proclaim gains are excluded), Kimura's Air Limited (increase the Honor from your next Proclaim this turn to the discarded card's Focus Value; his Ring waiver needs P2 and he has no local scan, so prove it with a test only), Marriage of the Emerald Champion (needs P11's dishonor shield and P4's duration; publish only if both landed, else Invocation with P4).

**Tests.** A next-change modifier is consumed by the first qualifying change and not by a Proclaim; "from your cards" excludes rulebook gains; an each-gain reduction applies to both players when the card says so; the honor window still opens with the modified amount; replay round-trips the store.

**Pitfalls.** R-019: the honor window opens on any gain or loss from an action's effects. Dishonorable rehonor-instead-of-gain happens before your modifiers; keep that order.

---

## P15. Fortification Holdings

**Unlocks (1):** The Tower of Vigilance (its Engage and created Guard also need a CreatedOnly scan).

**Today.** `Purchase` forces every Holding to enter bowed and clears `ProvinceIndex` (`MatchEngine.cs:537`); `DestroyProvince` (`:2697`) discards the province card and Region only; `_state.LastProvinceEntryProvinceIndex` records where a card entered from.

**Build.** A `Fortification` keyword rule in a feature file: on `Purchase` of a Holding with the keyword, enter straightened and record `(holdingId, provinceIndex)` in a `Fortifications` store (if it did not enter from a province, open an Either/Or to choose one of the player's provinces); on `DestroyProvince`, discard the attached Fortifications with a discard that cannot be prevented (bypass watches and immunities); the view shows the attachment through `ProvinceView` metadata (one additive field; coordinate with the UI lane in the PR, the client may ignore it).

**Shared edits allowed:** `MatchEngine.cs` `Purchase` (straightened entry) and `DestroyProvince` (one call), `Models.cs` `ProvinceView` (one additive field).

**Tests.** The Holding enters unbowed and produces Gold that turn; destroying its province discards it even when a destruction watch is armed; a Fortification bought by an action with no province asks for one.

---

## P16. Tireless and Home as ability flags; conditional timing; presence grants

**Unlocks (16):** Daigotsu Kanpeki - exp2, Goju Obayashi, Moshi Sasako - exp, Chuda Hikano, Daidoji Gensai, Iuchi Karasu - inexperienced, Shinjo Kinto - exp, The Shakash - exp, Tsiang, Long-Range Scouts, Koan's Scroll Satchel, My Ancestor's Steed, Anchored in Earth, Isawa Tsumaro, The Sorrow, Doji Tatsuzo.

**Today.** Repeatable and Absent are flags; Tireless is a hard-coded id list plus `…EvenIfBowed(id)` calls in `ValidateAbility` (`CardAbilities.cs:5499-5548`); Home is approximated by `Absent`, `MovesToBattle`, `SourceUnitMayBeAway` or per-id checks. There is no per-card timing override and no "you may take a Battle action even if you control no units there" grant tied to a Personality.

**Build.** Add `bool Tireless = false` and `bool Home = false` to `AbilityDefinition`, consumed where the id list is read (fold the existing ids into the flags on their definitions and delete the list) and where `SourceUnitMayBeAway` is read (Home: the source's unit may be at home; the acting player still needs presence unless Absent). Add `GrantedHome` (card id, until) so "give Home to one or more Spells in this unit" and "abilities on the target's spells have Home while the Illusion is there" are grants read by the same check. Add `TimingOverrides` in your partial state `(abilityId or cardId, newTiming, condition)` consulted where the ability's `Timing` is read for legality (The Sorrow's "printed ability is Battle/Limited while you control a Courtier"). Add `PresenceGrants` `(player, battlefieldIndex, viaCardId, until-after-one-action)` consulted by the presence check (Doji Tatsuzo).

**Shared edits allowed:** `CardAbilities.cs` (`AbilityDefinition` two flags, the `ValidateAbility` presence/bowed/source blocks).

**Demonstration cards:** Goju Obayashi (Ninja Tireless Battle: move home), Moshi Sasako - exp (Tireless Battle: straighten Sasako), Shinjo Kinto - exp (Home Battle: move home or, if he would be opposed, to the current battlefield), My Ancestor's Steed (Home Water Battle, bow: move a Spirit at any location to the current battlefield), Koan's Scroll Satchel (Open, bow: give Home to Spells in this unit, up to the number of Koan cards).

**Tests.** A Tireless action is offered while the source is bowed and still pays a printed bow cost only when unbowed (a bowed card cannot pay a bow cost); a Home action is offered from home during a battle with presence and not without; a granted Home lapses at its Until; the old Tireless ids still validate (frozen fixture).

**Pitfalls.** "Remote Battle" on Torn Asunder Spells (Anchored in Earth, Blood Wave) is the older wording of Home. Tireless does not let a card pay a bow cost it cannot pay.

---

## P17. Additional-action scope

**Unlocks (2):** Mirumoto Kalen - exp, Yoritomo Harumi.

**Today.** `AdditionalActionGrant` (`Models.cs:632`) scopes by card ids, discard, battle, performer, action keyword; it is spent by Battle actions, Action-Phase Limited/Open actions, Battle equips and scoped Terrain plays (`CardAbilities.cs:5021`, `MatchEngine.cs:2015,2077`).

**Build.** Add `string[]? ScopeAbilityIds` to the grant and honour it in `ConsumeAdditionalActionGrant` and `QueryActions`' grant filter; make an Action-Phase Equip able to spend a grant when the grant's scope allows it.

**Shared edits allowed:** `Models.cs:632`, `CardAbilities.cs` `ConsumeAdditionalActionGrant`, `MatchEngine.cs` `Equip`.

**Demonstration cards:** Mirumoto Kalen - exp (Ranged 3; you may take an additional action to use his second printed ability, a bow-cost Ranged 3), Yoritomo Harumi's second ability (its first line needs P19).

**Tests.** The grant admits only the named ability and is consumed by it; an Open action does not spend a Battle-bound grant.

---

## P18. Duel rules

**Unlocks (12):** Ichiro Otani, Isawa Tsuke, Demonstrating Technique, Suzume Shindo, Doji Hoturi - inexperienced, A Magistrate Falls, Seppun Ryota, Focus on the Flame, Fortune's Charm, Kokujin - inexperienced, The Empire's Foe Exposed, Doji Hakuseki - exp3.

**Today.** `PendingDuel` (`Models.cs:681`) holds `Stat` (Chi/Force/PH); `DuelStatValue` (`Dueling.cs:295`) adds per-card bonuses; refusal is `DuelRefusable`/`ChallengeMayBeRefused` plus, **(#670)**, Fusako's bow-to-refuse (`DuelSleepFusako.cs`, the precedent for `RefusalCost`); focus caps at four; Focus Effects are ordered; `Champion` substitutes a duelist from a focus; `OpenDuelWonReactionWindow` exists. Missing: exclude Item bonuses, min/exact focus counts, first opportunity to focus, auto-lose against a named opponent, a static substitute-duelist Reaction, refusal with a cost (dishonor and move home), refusability by a condition on the two targets.

**Build.** A `DuelRules` record on `PendingDuel`: `ExcludeItemBonuses`, `MinFocusEach`, `ExactFocusEach`, `FirstToFocusPlayerId`, `AutoLose(cardId, againstTitle)`, `RefusalCost(kind: dishonor-and-move-home|bow)`, `RefusableWhen(condition)`. `DuelStatValue` subtracts Item modifiers when excluded; the focus/strike legality reads the counts; `TurnPlayerId` starts from `FirstToFocusPlayerId`; resolution applies `AutoLose` before totals; `IssueChallenge` offers refusal with the cost and takes the cost when refused. The substitute duelist (Seppun Ryota) is a before-action Reaction that rewrites the held challenge's target to Ryota when the target is at his location; put it on the `TargetedReaction` channel.

**Shared edits allowed:** `Dueling.cs` (`IssueChallenge`, `DuelStatValue`, focus/strike validation, resolution), `Models.cs` `PendingDuel` (one record property), `DuelPresentation.cs` if a new refusal verb needs a label.

**Demonstration cards:** Isawa Tsuke (both must focus at least once; needs P23 for "ExperiencedCoM"), Demonstrating Technique (each focuses exactly once if possible; winner draws; dishonor the loser), Suzume Shindo (first opportunity to focus or strike when he is the challenger; his gate needs P3), Doji Hoturi - inexperienced (an honorable target's controller may dishonor him and move him home to refuse; needs P23), Ichiro Otani (duel of Force, Items give no bonuses).

**Tests.** A player at the cap who must still focus is given a strike-only choice only when the minimum is met; exact-once forbids a second focus; the first-to-focus order is honoured in the client's duel view (QueryDuelActions); auto-lose resolves as a loss before totals and the loser consequences apply; refusal with a cost pays the cost and skips the duel (R-014 excludes He's Mine! and Irezu).

**Pitfalls.** R-027: an "in place of focusing" bonus is not a focus for counts. A tie means both lose; the Duelist tie rule applies before `AutoLose`.

---

## P19. Rulebook-action modifiers

**Unlocks (7):** Akodo Kano - exp, Ikoma Ayumu - exp, Frontier Farmer, Yoritomo Harumi, Practicing Kata, Harbingers of War, Hantei's Guidance.

**Today.** Rulebook actions are catalogue ids (Tactical Advantage, Equip (Limited), Equip (Battle), Favor Limited/Battle, Conquest, Naval Invasion, Lobby, Order Seppuku, Kharmic) with their own cases; `TimingGrants.cs` is a private dictionary keyed by an in-play card's Effect with no once-per-turn or card-free form; **(#670)** Mempo (`FavorMempo.cs`) shows a retargeting after announcement for Favor Battle and an extra use through `EngineHooks.ExtraUsesPerTurn`; the Equip command takes attachments from hand only.

**Build.** `EngineHooks.RulebookModifier`: `Func<MatchEngine, RulebookContext, RulebookModifier?>` consulted by the rulebook cases with `(abilityId, player, performer, command)` in and `(amountDelta, timingOverride, sourcePileAllowed: discard, exemptions: bowed|unopposed, absent, anyTarget)` out. Register the modifiers per card: Kano (+2 to Tactical Advantage's Force bonus when he performs), Ayumu (Tactical Advantage is Battle/Open for him), Frontier Farmer (Equip may take this Follower from your discard pile: `Equip` gains a discard source for cards whose definition allows it), Harumi (Equip (Battle) once per battle even if bowed and unopposed), Hantei's Guidance (Favor Battle has Absent and may target any Personality; reuse Mempo's retargeting, so this one card waits for #670). Replace `EffectTimingGrants` with a `TimingGrants` store that can be card-free, once-per-turn (Practicing Kata) and game-long from an Event (Harbingers of War; its draw needs P7).

**Shared edits allowed:** the rulebook cases in `CardAbilities.cs`, `MatchEngine.cs` `Equip` (discard source), `TimingGrants.cs`.

**Demonstration cards:** Akodo Kano - exp, Frontier Farmer, Practicing Kata (Kata in play; Equip as Battle/Open once per turn; "discard your other Kata" exists).

**Tests.** Kano's bonus applies only when he performs; Ayumu's Tactical Advantage appears in the Action Phase for him only and still respects the per-Tactician cap; Frontier Farmer is equippable from the discard pile paying its cost and the pile shows it as an option for its owner only; the Kata's grant is spent once per turn.

**Pitfalls.** R-008: Equip (Limited) attaches Spells, Followers and Items. Supplement: Ikoma Ayumu's exact wording is "The rulebook Tactical Advantage is an Open action for you if you choose Ayumu to perform it".

---

## P20. Search and look extensions

**Unlocks (9):** The Onyx Championship, Iweko Seiken, Dark Audience, Jade Pearl Inn, Unbound Essence, Asako Usohara, Revelations, Hantei XVI - inexperienced, The False Path.

**Today.** `SearchSpec` (`DeckSearch.cs:109`) sources are own decks, own discards and own hand, `Ordered` or not; criteria cover types, keywords, titles, non-Unique, printed stats, Battle Strategy, ability keyword; dispositions: ToHand, ToHandThenDiscard, AttachRegion, RecruitUnbowed, PutOnTopAfterShuffle, RecruitPersonality, PutIntoPlay, ToProvinceFaceUp, RingIntoPlayOrHand, RefillSourceProvinceFaceUp; exactly one card is selected; shows are public by definition. `LookSpec` (`DeckLook.cs:51`) covers own or opponent decks with top/bottom/hand/show/discard/play dispositions. Missing: province sources, any-player piles, player-chosen source among alternatives, multi-select with a "different titles" check, a Gold-producing criterion, attach-to-Personality and bottom dispositions, "another player chooses one", reveal-until, Celestial into play. **(#670)** `SearchPlay.cs` adds the search effect `sp-celestial-into-play` (through `EnterCelestialPlay`, extracted from `ResolveCelestial`), a deck-or-discard source choice that remembers where the card came from (The Sacred Rosary), and an owed play of a searched Terrain (Gozu); read `docs/decisions/2026-10-05-667-search-play.md`. When they are present, Asako Usohara's "search for a Fudo Celestial, put it into play" uses that effect and you do not build `CelestialIntoPlay` or `PlayAsAdditionalAction`.

**Build.** Extend `SearchSpec` with `SearchDeckOption` sources `OwnProvinces` (face-up or face-down cards; the searcher may look), `AnyPlayerFateDiscard`, `AnyPlayerDynastyDiscard`, `ChooseOne` (the player picks the source, as against `Ordered`), criteria `GoldProducing` (`HasGoldProducingTrait`) and `DifferentTitles`, `SelectCount` (min/max), dispositions `AttachToPersonality` (a follow-up target slot), `Bottom`, `CelestialIntoPlay` (bypass `ResolveCelestial`'s province refill, still discard the controller's other Celestials), `RecruitFromProvinceIgnoringCosts`, `PlayAsAdditionalAction` (Gozu: a Terrain found in the deck, played through a grant; discarded if not legal), and follow-up `OtherPlayerChoosesOne` (a decider choice over the shown definitions, P25's definition-decider if landed, else a bespoke pending choice). Extend `LookSpec` with `RevealUntil(criteria)` and a disposition for the matching card (Unbound Essence overlays it, P22).

**Shared edits allowed:** `DeckSearch.cs`, `DeckLook.cs`, `MatchQueries.cs` for the new pending types' options (until P25 lands), and the client deck viewer `Main.DeckSearch.cs` for multi-select and province sources (coordinate with the UI lane; a plain list of candidate tiles is acceptable).

**Demonstration cards:** Jade Pearl Inn (after you recruit it from a province: search the Dynasty deck for a non-Unique Gold-producing Holding with printed Gold Cost 1 and refill that province face up with it), Dark Audience (two attachments with different titles; the other player chooses one to remove; the other goes to hand).

**Tests.** A province search shows face-down cards only to the searcher and reshuffles nothing; `ChooseOne` offers both sources and `Ordered` still forces the first (R-011); the different-titles check rejects a second pick with the same title; the opponent's choice is offered only the shown definitions; replay round-trips mid-search.

**Pitfalls.** R-011/R-012 govern ordered searches and Ring searches; keep them. A search always reshuffles the searched deck.

---

## P21. Zone extensions

**Unlocks (13):** The Tachikaze, Hidden Weapons, Kitsu Miwa, Iuchi Wattu, Yoritomo Mikaru, Ikoma Jeiku, Oracle of the Void - exp, Kokure's Lands, Paneki's Mask, Disciples of Master Coin, Blood of the Preserver - exp, Hida Tadama, Duty Over All Things.

**Today.** `Zone.Underneath` is one public pile under the owner's Stronghold (`EmperorP5Zones.cs:107`, `MatchQueries.cs:65`); `AbilitySourceZoneAllows` (`CardAbilities.cs:5317`) admits in play, hand, dead and discard; a face-up province card can be a performer but not an ability source; refills happen at several call sites with no hook; `HoldsNoDynastyCards` is permanent; there is no record of when a card entered play or which Personalities were in play during a player's last turn; the delay store has no "to deck" kind; `FromDead` recruits exist in pieces. **(#670)** Masaya is wired there as a one-card case: `SearchPlaySourceZoneAllows`/`SearchPlayHasProvinceAction` let his own card be an ability source while face up in a province, and `ResolveSearchPlayTurnEnd` puts him on top of the Dynasty deck in `EndTurn`. When they are present, items 2 and 5 below generalise those two functions into the `FromProvince` flag and the `ToDeck` delay kind and move Masaya onto them; they are not a second mechanism.

**Build.**

1. Cards under a host: give `Zone.Underneath` cards a `HostCardId` and `FaceUp` flag on `CardInstance`; `ViewCard` redacts a face-down under-card to `hidden-under-{host}-{n}` for everyone but its owner; `SlotCandidateCards` excludes hidden under-cards from the other seat; an `UnderCards(hostId)` helper and `PutUnder(card, host, faceUp)`; Equip-from-under through `AttachSpec.Pile = UnderHost`.
2. `AbilityDefinition.FromProvince`: the ability's own card may be face up in its owner's province; honoured by `AbilitySourceZoneAllows` and the source list in `AbilityActions`; a `RecruitFromProvince(card, discount, bowed)` helper used by Kitsu Miwa and Yoritomo Mikaru's "Recruit a target Holding in your Province for 1 less".
3. `EngineHooks.Refill`: `Func<MatchEngine, RefillContext, bool?>` (province, reason) consulted by `Refill`/`RefillProvinceSlot` so a feature may make a refill face up; Oracle of the Void's "next two refills this turn may be face up" is a watch with a count and an Either/Or.
4. `CardInstance.EnteredPlayTurn` set by every entry path, and a per-player `InPlayDuringLastTurn` id set captured at `EndTurn`; `HoldsNoDynastyCards` gains a `WhileCardId` (Kokure's Lands).
5. `DelayedKinds.ToDeck` (top/bottom) applied by `ToOwnerDeck`.

**Shared edits allowed:** `Models.cs` (`CardInstance` two fields, `ProvinceState.HoldsNoDynastyCardsWhile`), `MatchQueries.cs` `ViewCard`, `TargetSlots.cs` `SlotCandidateCards`, `CardAbilities.cs` `AbilitySourceZoneAllows`/`AbilityActions`, `MatchEngine.cs` `Refill`/`RefillProvinceSlot`/`EndTurn`, `EmperorRulingR2.cs` (one kind), `AttachAction.cs` (one pile value).

**Demonstration cards:** The Tachikaze (face-up under-cards; Repeatable Battle/Limited equip from under at the Follower's cost), Kitsu Miwa (Limited from a face-up province: discard her and pay 2 Gold; search the Dynasty deck for a non-Unique Lion Clan Shugenja and refill that province with him face up, the existing `RefillSourceProvinceFaceUp` search effect), Kokure's Lands.

**Tests.** The other seat's view never contains a hidden under-card's definition; the owner sees it; equipping from under pays the attachment's cost and triggers its entry effects; Kitsu Miwa's action appears only while she is face up in her owner's province, is not offered to the other seat, and leaves the province refilled face up; a refill hook can flip one refill face up without changing the others.

**Pitfalls.** Preamble rule 4: hidden information. R-023 covers two-card provinces; do not touch `CardId2`.

---

## P22. Created cards, copies and grants

**Unlocks (17):** Constructing the Self, Kitsune Kichi - exp, Kitsune Merihiko - exp, Kitsune Yuribara, Komori Miyano, Never Without a Weapon, The Wind's Champion, Shard of the Great Death's Bones, Ninube Tsukau, Spiritual Coalescence, Shifting Waves, Watersteel Sword, Unbound Essence, Unsettling Gathering, The Egg of P'an Ku - exp, The Jade Championship, The Onyx Championship.

**Today.** `CreatedCardSpec` (`CreatedCards.cs:16`) resolves a `CreatedOnly` scan by type, stats, keywords and text and fails without one; removal timings are `UntilBattleEnds` and a per-card `ScheduleTimedRemoval` (`TurnEnds`, `PlayersNextTurnBegins`); `Values` read printed stats only; `CopyOfTarget` is Personality only. `CardCopy.cs` copies one keyword and/or ability for Copy or Turn; `PermanentCopy.cs` rewrites `Printed`; Exchange and overlay-onto-target do not exist; `AttachmentTransfers.cs` is same-controller only; `GrantAbility` grants to one card.

**Build.**

1. `CreatedCardSpec.RemoveWhen` (`TimedRemovalWhen` or `SpellStraightensOrLeaves(sourceId)`), `Values` reading current stats and a discarded card's Focus Value, `CopyOfTarget` for any type, and `AbilitySpec`/`TraitSpec` lists so a created card's printed ability or trait is declared with the creation (Kichi's "Battle, bow: Melee 4 Attack", Yuribara's forced-targeting trait through P11).
2. `Exchange(cardA, statA, cardB, statB)` per the glossary (note both, set each to the other's by delta); `CopyStat(from, to, stat)` set-to-current.
3. `OverlayOnto(target, newCard)` reusing `TransferOverlayState` for an arbitrary target (Unbound Essence).
4. `CopyAbilityTo(attachment)` for Watersteel Sword (Turn duration, excludes abilities that copy).
5. `TransferAttachment(card, newWielder, allowControlChange: true)` using `TakeControl` for Unsettling Gathering.
6. Filter-scoped grants: `GrantAbilityByFilter(player, predicate, abilityId, duration)` evaluated live (Merihiko's "Your Nonhuman Spirits have the ability..."), and granted traits as `GrantedAbility` entries flagged `Trait` (The Jade Championship).

**Shared edits allowed:** `CreatedCards.cs`, `CreatedCardEntry.cs`, `CardCopy.cs`, `AttachmentTransfers.cs`, `AbilityGrants.cs`, `MatchEngine.cs` overlay (one shared helper).

**Demonstration cards:** Komori Miyano (create a Bat Personality, remove after your next turn begins; requires its scan), Shifting Waves (switch a target's Force and Chi; Henshin performer), Watersteel Sword, Kitsune Merihiko - exp. Where a scan is missing, write the test against a synthetic CreatedOnly fixture and leave the card unpublished, saying so.

**Tests.** A created card is removed at the right trigger and never discarded; Exchange of 3F/5C gives 5F/3C and reverses correctly at turn end if the card says this turn; an overlay onto a target carries state and removes the old card from the game; a filter grant appears on a Spirit that enters play later and disappears when the granting card leaves; a transferred attachment changes controller and respects Unique/Singular (`TransferUnitControl`'s rule).

**Pitfalls.** A created card leaving play is removed from the game. Never create a card without a resolved scan; the engine throws by design.

---

## P23. ExperiencedCoM and Inexperienced parsing

**Unlocks (9):** Bayushi Kachiko, Isawa Tsuke, Hida O-Ushi - inexperienced, Doji Hoturi - inexperienced, Hantei XVI - inexperienced, Kokujin - inexperienced, Iuchi Karasu - inexperienced, Tsuruchi - inexperienced, Bayushi Nitoshi - exp.

**Today.** `Experienced.Level` (`Experienced.cs:8`) parses "Experienced" and "Experienced N" only, so "ExperiencedCoM" is level 0 and `Keyword(card,"Experienced")` is false; `BaseTitle` (`:25`) strips " - expN" and " Experienced[ N]" only, so "X - inexperienced" never matches "X"; "Inexperienced" has no rule (`NitoshiInexperienced.cs` is one card's own ability).

**Build.** Treat "ExperiencedCoM" as Experienced level 1 for the rule (the Coils of Madness printing of an already-Experienced title) and as satisfying `Keyword(card,"Experienced")`; treat "Inexperienced" as level 0 with `BaseTitle` stripping " - inexperienced", so an Experienced card of the same title may overlay it and it cannot overlay anything. Record the reading for Mendon: whether an "ExperiencedCoM" card may overlay a plain "Experienced" one of the same title (level 1 onto level 1 is not allowed by the core rule; the cards in question have no lower-level printing in the pool, so no card in the catalogue depends on the answer today).

**Shared edits allowed:** `Experienced.cs` only, plus tests.

**Demonstration cards:** Hida O-Ushi - inexperienced (Limited: bow a target Personality with lower Force; Loyal) and Tsuruchi - inexperienced (Arrow tokens until exactly five; destroy zero or more for a Ranged Attack of twice the count, three times per turn: Togashi Mitsu's `strengthOverride` shape with a new token type).

**Tests.** Level and base title for the four forms; overlay legality between "X - inexperienced", "X" and "X - exp"; deck construction still accepts one of each level (`DeckFormat`).

---

## P24. Small effect primitives

**Unlocks (19):** Iuchi Karasu - inexperienced, Shinjo Kinto - exp2 (switch locations), Befriend the Tides, Unexpected Discovery, A Moving Battle (adjacent battlefield), Ikoma Satoru - exp, Strength in the Earth (home-targeted Attack), Chiyurei's Axe (compare against printed Force), Sorrowful Prayer, Overwhelming Chaos (shuffles), Harmony (cost substitution), Kitsuki Judgment, Scorching Lash, Koan's Jingasa, Koan's Robes, Koan's Whisper, Tamori Tsuchiya, P'an Ku - exp (straighten trigger for a control return), A Doomseeker's Tomb.

**Today.** Movement destinations exist (`MoveDestinations.cs`) with no Switch or adjacent destination; Attacks target the current enemy army only (`AttackTargeting.cs`); `CostVerbs` has Bow, Destroy, DiscardFavor, Remove (own or dead), ChiPenalty, DestroyToken, ShuffleHand (actor only); tokens carry two stats; `ControlReturnTriggers` (`EmperorMechB.cs:71`) are TurnEnds, SourceLeavesPlay, AbilityAnnounced. An Attack has no spec record today: its strength and kind come from the `AttackStrengthBefore/After` and `AttackKind` hooks and its targeting from `AttackTargeting.cs`.

**Build** each as a feature-file helper plus one declarative field where cheap: `SwitchLocations(a, b)` (negating one side negates both, glossary "Switch"); `MoveSpec.Destination = AdjacentBattlefield(resolvedFilter)` using the existing adjacency; a new optional `AttackSpec(TargetScope, CompareAgainst)` record on `AbilityDefinition`, passed by name: `TargetScope = DefenderHome|AnyLocation` is consumed by `AttackTargeting` with the Rules of Location bypassed for that Attack only, and `CompareAgainst = PrintedForce` compares the strength with the target's printed Force; `ShuffleIntoDeck(cards)` and an each-player `ShuffleHandAndRedraw`; a `CostSubstitution` store ("you may bow this card instead of bowing a Ring to pay a cost" for Harmony; "need not bow for his Spells with odd/even Focus Value" for Koan's Jingasa/Robes) consulted by the cost-payment verbs; `CostVerbs.RemoveFromAnyDiscard` with a count (Koan's Whisper: five Koan cards you control); `CostVerbs.DestroyOwnAttachment(type)` (Tsuchiya); `CardToken.Stat3/Amount3` or a token list (Doomseeker's Tomb +1F/+1C/+1PH); a new `ControlReturnTriggers.SourceStraightens` constant (P'an Ku).

**Shared edits allowed:** `AttackTargeting.cs` (scope and compare), `MoveDestinations.cs` (one destination), `AbilityCosts.cs`/`AlternativeCosts.cs` (two verbs, substitution check), `Models.cs` `CardToken` (one optional stat), `EmperorMechB.cs` `ControlReturns` (one trigger).

**Demonstration cards:** Shinjo Kinto - exp2, Unexpected Discovery, Strength in the Earth, Sorrowful Prayer, A Doomseeker's Tomb.

**Tests.** A switch moves both or neither; an adjacent move offers only unresolved adjacent battlefields (none for a province-less battlefield, R-002); a home-targeted Attack destroys a Follower in the Defender's home and triggers the attack reaction window; `CompareAgainst` ignores Force modifiers; a cost substitution is offered only when the substitute is unbowed and bows it instead of the Ring.

---

## P25. Decision infrastructure

**Unlocks (2):** Poison the Cup, Now We Are Enemies; also removes a shared-file edit from every later new decision kind.

**Today.** Either/Or is single-pick with hook-registered specs (`ChoiceDispatchHooks.cs`); decider choice picks cards one at a time with Min/Max; a new pending type still needs its own `else if` in `QueryActions` (`MatchQueries.cs:375-710`) and a line in the client's `GenericDecisionActive` allowlist (`Main.Decisions.cs:14`); `TargetSlots` validate each slot independently. **(#670)** Two precedents arrive there: Sleep's `budgetSelect` (`DuelSleepBudget.cs`, a feature-owned pending type that still needed its own `QueryActions` branch) and The Topaz Armor's two keywords chosen one after the other through the shared `eventChoice` decision. Item 1 below is the general form of the second.

**Build.** (1) `PendingEitherOr.Min/Max` so an Either/Or may collect several options with a "Done" option, the way decider choice does (each pick stays an ordinary `chooseEitherOr`, so no client change). (2) `EngineHooks.PendingActions`: `Func<MatchEngine, PendingActionsContext, IEnumerable<ActionOption>?>` consulted by `QueryActions` before its chain, plus hooks for `EffectChoice.ApplyBranchEffect` and its continuation; change the client to treat any unknown pending type as generic. (3) `TargetSlot.CrossConstraint` (a predicate over the chosen set) validated in `TargetSlots` after the per-slot checks, for "two Personalities that do not share a Clan alignment". (4) A decider choice over shown definitions (candidates named by definition and position) for the P20 follow-up, with the client rendering the `ShownCards` faces.

**Shared edits allowed:** `EitherOr.cs`, `MatchQueries.cs` `QueryActions` (one call), `EffectChoice.cs` (two hooks), `TargetSlots.cs`, `DeciderChoice.cs`, client `Main.Decisions.cs` (the allowlist) and `Main.EitherOr.cs` (Done button if missing).

**Demonstration cards:** Poison the Cup (remove a target token from a Personality, give him a -1C Plague token, lose 1 Honor: the token is picked through a multi-option Either/Or over the target's tokens), Now We Are Enemies.

**Tests.** A multi-pick Either/Or enforces Min/Max and returns the picked keys in order; a cross-constraint rejects the pair and offers no second candidate that violates it; a feature-registered pending type lists options without a `MatchQueries.cs` edit; the client renders an unknown pending type as buttons (a Godot probe in `tools/check-*.sh`, or state in the handoff that the native check is left to CI).
