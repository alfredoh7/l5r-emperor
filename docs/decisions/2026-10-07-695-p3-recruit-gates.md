# P3 (issue #695): recruitment gates and Proclaim eligibility

Engine only. No card is wired, no content wave is published, no scan is added. Feature file: `src/L5R.Core/RecruitGates695.cs`; tests: `tests/L5R.Core.Tests/RecruitGates695Tests.cs` (label `recruit-gates-695`).

## What was built

**The channel.** `EngineHooks.RecruitGate`: `Func<MatchEngine, RecruitGateContext, RecruitGateResult?>`.

- `RecruitGateContext(CardInstance Card, int PlayerId, string Clan, RecruitGateKind Kind)`; `Kind` is `Recruit`, `Reserve`, `Overlay`, `ControlChange` or `Proclaim`.
- `RecruitGateResult(string? RefuseReason, bool IgnoreHonorRequirement, bool ProclaimEligible, bool LoyalWaived)`. Callbacks return `null` when they have nothing to say. Results are merged by `RecruitGateFor`: the first refusal wins, every waiver or eligibility adds. Only Personalities are gated.
- Registered like any hook: `EngineHooks.RecruitGate.Register("P3...", 16400, static (engine, context) => ...)` in a module initializer.

**Where it is read (one call each):**

| Place | Kind | What it reads |
| --- | --- | --- |
| `QuotePurchase` (`MatchEngine.cs`) | `Recruit` or `Reserve` | refusal becomes the quote's `Error` (placed before the Loyal branch); `IgnoreHonorRequirement` joins the Honor Requirement test |
| `QuotePurchase` eligibility, `ProclaimAllowed` (`SeedsOfDecayBatch2.cs`) | `Proclaim` | `ProclaimEligible` makes a Personality of another Clan Proclaim-eligible (still active player, still never with the Clan discount, still the once-per-turn limit and lock); a `Proclaim` refusal makes the entry ineligible without refusing the recruit |
| `LoyaltyAllowsControl` (`CardAbilities.cs`) | `ControlChange` (default) or `Overlay` | refusal; `LoyalWaived`. Every reader of Loyal obeys it: `TransferUnitControl`, `QuoteOverlay`, created cards, Bunoro's overlay, a take effect |
| `TransferUnitControl` (`EmperorMechB.cs`) | `ControlChange` via `LoyaltyAllowsControl` | the event text now carries a gate's own reason (the legacy Loyal-like refusal keeps its old words) |

The Experienced overlay never reads the Honor Requirement (`QuoteOverlay` never did), so `IgnoreHonorRequirement` is never read for an overlay; a refusal of control does apply to an overlay, as Tanemono's did.

**Migration, zero behaviour change.** `ControlRefusedByClan` is deleted from `SeedsOfDecayBatch3.cs`; its initializer registers `P3TanemonoNoPhoenixControl`. It refuses with `MatchEngine.LoyalRecruitRefusal`, the exact words `QuotePurchase` always gave for him, and `TransferUnitControl` keeps its old "is Loyal and cannot be controlled" text for that reason. The existing Tanemono and Loyal tests pass unchanged.

**Generic rules (Effect strings on a card; read by the registered gates):**

| Effect | Rule | Kinds |
| --- | --- | --- |
| `will-not-join-<clan>-player` (any Clan, lower case) | refuses a player of that Clan | all but `Proclaim` |
| `will-not-join-human-controller` | refuses a player who controls another Human Personality in play (`KeywordOrHuman`) | Recruit, Reserve, ControlChange |
| `will-not-enter-honor-nonnegative` | refuses while the player's Honor is 0 or above | Recruit, Reserve |
| `<clan>-players-may-control` | waives Loyal for that Clan's player | recruit, overlay, control change |
| `<clan>-players-may-proclaim` | Proclaim eligibility for that Clan's player | Proclaim |
| `favor-ignores-honor-requirement` | ignores the Honor Requirement while the player holds the Imperial Favor | Recruit, Reserve |

A card has a single `Effect`; a card wiring two of these writes its own gate callback (the API below) rather than one Effect string.

**Reused, not rebuilt.**

- "<Clan> players ignore his Honor Requirement": `CardDefinition.HonorRequirementWaiverClan` already works in `QuotePurchase` (`HonorRequirementWaived`), tested again here with a fixture.
- Player-level "ignores Honor Requirements for the rest of the game": the existing `PlayerState.IgnoresHonorRequirements` (Alter History), already serialised and already read by `QuotePurchase`. New helper `MatchEngine.IgnoreHonorRequirementsForRestOfGame(playerId, via)` sets it once and emits `honorRequirementsIgnored`. No new store, so no new `JsonPropertyOrder` and no `Pending*` property.
- P11's province entry lock (`ProvinceEntryLockError`) is a lock on a province, not on who may recruit; it stays as it is and is read before this gate.
- P2's entry cost terms already fold costs into the quote; no cost is touched here.

## API for a card-wiring agent

```csharp
// A card-specific gate: Bayushi Hamada-style "Crab players may control him", written as a callback.
EngineHooks.RecruitGate.Register("P3HamadaCrabControl", 16410, static (engine, context) =>
    CardCatalog.Get(context.Card.DefinitionId).Effect == "hamada-effect" && context.Clan == "Crab" && context.Kind != RecruitGateKind.Proclaim
        ? new RecruitGateResult(LoyalWaived: true) : null);

// "Yoritomo Yashinko: Crane players may Proclaim him" -- or just give the card the Effect "crane-players-may-proclaim".
new RecruitGateResult(ProclaimEligible: true)            // when context.Kind == RecruitGateKind.Proclaim and context.Clan == "Crane"

// "Will not join a player who ...": a refusal; the reason is the quote's Error.
new RecruitGateResult("Asp Skirmisher will not join a player who controls a Human Personality.")

// "Ignore his Honor Requirement while you control the Imperial Favor"
context.Kind is RecruitGateKind.Recruit or RecruitGateKind.Reserve && engine.State.FavorPlayerId == context.PlayerId ? new RecruitGateResult(IgnoreHonorRequirement: true) : null

// Delicate Gamble's resolution: for the rest of the game
engine.IgnoreHonorRequirementsForRestOfGame(playerId, "Delicate Gamble");
```

Callbacks run inside `MatchEngine`'s partial classes may call private members (`Player`, `Definition`, `InPlay`) directly. Use the `Effect` strings above where one matches; otherwise register a callback keyed on the card's id or Effect.

## Cards each piece is for

- Asp Skirmisher: `will-not-join-human-controller`.
- Ichiro Otani: `will-not-enter-honor-nonnegative` (his duel needs P18).
- Bayushi Hamada: `crab-players-may-control` (his other half is P9).
- Yoritomo Yashinko - exp: `crane-players-may-proclaim`.
- Hantei XVI - inexperienced: `favor-ignores-honor-requirement` (his search is P20, the title P23).
- Delicate Gamble: `IgnoreHonorRequirementsForRestOfGame`.
- Ikoma Natsu - exp, Suzume Shindo, Tsi Hayamizu, Zansho - exp, Sengmai, Journey's End Keep - exp: the prompt lists them under P3 without saying which piece; the printed text must be read against the gate table above (a refusal, a waiver of Loyal, an Honor Requirement waiver, or Proclaim eligibility). Whatever it asks that is not in the table is not built.

## Left out, and why

- Per-Clan lists in one result (for example "Crab or Lion players may Proclaim"): the callback already knows the player's Clan, so it returns one bool for any set it likes.
- A gate on Experienced overlay's Honor Requirement: there is none to gate.
- `Proclaim` refusals are read only where eligibility is decided (the quote); the Proclaim command itself is still guarded by `ProclaimRejection`, which is unchanged.
- No client changes: the refusal reason travels in the existing quote `Error`/disabled-reason fields.

## Verification note

State is board-derived; no new store exists, so save and replay cannot diverge. The flag test round-trips `MatchState` through JSON and through `FromTestState(e.State)`. Fixture rules use per-instance `Printed` definitions with ids `training-p3-*`.

## Readings for Mendon to confirm or correct

1. "Family Honor" is the player's `Honor`; "0 or above" means Honor >= 0, so 0 refuses.
2. "Will not join a player who controls a Human Personality" counts another Personality in play, controlled by that player, without the Nonhuman keyword; it applies to a recruit and to a change of control, not to an Experienced overlay (the overlaid title is that player's own).
3. "Crab players may control him" waives Loyal only; it does not waive an Honor Requirement, Unique or Singular.
4. A refusal of control (the Tanemono shape) also bars an overlay, as it always did.
5. The Favor condition is read when the quote is asked (the Favor's holder at that moment).
6. "Will not enter play" conditions apply to an entry from a province or Reserve, not to taking control of a card already in play.
