# Action resolution area (issue #917)

Status: **accepted and implemented** (Phase 1: Strategies and Rings). Lane: rules (shared client contract). Source: VALIDATION_REPORT finding 5 (EE-ACT-005, EE-Abilities-and-actions-021). Issue #917; design PR #918 (superseded by this record); implementation PR is linked from the issue. The reproduction (branch `claude/917-resolution-area-repro`) is inverted into `tests/L5R.Core.Tests/ResolutionAreaTests.cs`.

## Decisions (Mendon, final) and what was built

1. **Option A** (zone-based). `Zone.Resolving` appended last in the string-serialized `Zone` enum; `MatchState.ResolutionArea` (`ResolutionEntry { CardId, OwnerId, AbilityId }`, oldest first); the card leaves `Player.Hand` in `UseAbility` immediately after `ValidateAbility` succeeds (`EnterResolutionArea`, `ResolutionArea.cs`); public to both seats from announcement; trailing nullable `MatchView.ResolutionArea` (`[JsonIgnore(WhenWritingNull)]`, so every payload outside an open action is byte-identical); public event `cardResolving`.
2. **Phase 1 scope: Strategy and Ring only.** Events, Disciplines from a discard pile, hand Items/Followers/Personalities and deck abilities are unchanged.
3. **The Attack and Duel stopgap stays** (issue #320): a hand-played Attack or Dueling Strategy is discarded as its `attackReaction` / duel window opens. Holding it until `ResolveAttack` / `ResolveDuel` ends (rule 1914) is not done.
4. A resolving card cannot be picked by a forced or random hand discard and does not count as "has a card in hand" (rule 995 reading). Confirmed.
5. Its identity is public from announcement. Confirmed.
6. **No `ContentVersion` bump** (replays are command lists). Confirmed.

As built, beyond the original sketch:

- `PlayedFromHand(card)` (`Zone.Hand` or `Zone.Resolving`) replaces the post-announcement `source.Zone == Zone.Hand` reads (payment kind, Marshalling Fields waiver, Terrain, cost `UnlessInHand` waivers, Adroitness, Sincerity/Honesty removal, attach placement, the tail discards). Announcement-time legality stays on `Zone.Hand` (`SourceInHand`), except that re-validating the held command (a redirect, a held window) accepts the card in the area; `UseAbility` itself refuses to announce a card that is already resolving.
- Exit goes through `LeaveResolutionArea` (`DiscardFate`, `RemoveFromGame`, attach, Terrain). `ReconcileResolutionArea` runs after every command: it drops an entry whose card moved by another route, and discards a card still resolving once nothing else is pending (rule 1914). That net also spends Strategies whose action paused for a look or an Either/Or choice and whose resume path never discarded them (Lessons from the Heralds, The Power of a Word), which used to stay in the hand.
- `ResolutionArea` is in the Seppuku `DecisionStates` list. Test invariant (`RegressionAssertions.Accept`): every entry's card is `Resolving`, every `Resolving` card has one entry, and a non-empty area implies another decision is open.
- Self-reaction is gone at the root: a card in the area is neither a hand-Reaction candidate for its own action nor a hand-discard candidate (Precision could previously react to its own Attack; two tests now hold a second copy).
- AI: the card index (`TrainingOpponent.AllCards`, `BotContext.Observe`) includes the area; the `inPlay` test excludes `Zone.Resolving`.
- Client: `Main.ResolutionArea.cs` draws the area above the right end of the hand tray; `FindCard`, `VisibleCards` (flight inference hand -> area -> discard pile) and the card-play showcase read it; `CardTile` / `Tile` treat `Resolving` like the hand (no table-plane projection). The hand count and tray never include the card. An older client that does not know the `Resolving` zone string cannot deserialize the view; client and server ship from the same build and mixed versions are not supported.

## The defect

A Strategy (or Ring) played from hand stays in `Zone.Hand` and in `PlayerState.Hand` from announcement until the tail of its resolution (`SpendHandPlayedCard`, `CardAbilities.cs` ~7454). Every window that opens in between sees it as a card in hand. The reproduction announces Strategic Strike with a No Hiding Place also in hand: the announce window opens (`PendingChoice` `announceReaction`), and `GetView(0).Players[0].Hand` lists the Strike, `HandCount` is 2 for both seats (expected 1), and `Cards[strike].Zone` is still `Hand`. Reproduced on `main` at 350f9184 (first assertion, "not in its owner's hand list", fails).

Rules text (docs/rules/COMPREHENSIVE_RULES.md): step A creates "a temporary resolution area for the card and put[s] it there" when announcing an ability on a card in hand (1815); the area is "face-up but out of play" (991); a card that discards itself as a cost does so from the area (995); "When the action has ended, any resolution area created disappears, and any card still in the resolution area is discarded" (1914).

The engine already knows the card is not really in the hand: about 16 readers hand-exclude it (`h != source.Id`: `AlternativeDiscard`, `EmperorMechC`, `SearchPlay`, `WlSpellsRings`, `PromoEmperorFate`, `TfStrategiesA`, `EmperorRemainingA`, `CommandNamesCard` in `DaigotsuBofana.cs` ...). The unpatched readers are the bug: Daigotsu Bofana's forced discard at announcement, `CostVerbs.DiscardHand`, the opponent's count, the owner's hand tray.

## Where the card is "in hand" while its action is paused

All between `UseAbility` and `CompleteAbilityResolution`, all with the card still in `Player.Hand`:

1. `OpenForcedDiscardForTargetedTrait` (Bofana / Shinjo Tselu forced discard at announcement).
2. `ResolveOrDeferAdditionalAction` (`additionalActionReaction`).
3. Announce windows: `ResolveOrDeferAnnounceReaction` (Tsuruchi Yakusho, No Hiding Place, Suitengu's Torch, P3 watchers), `ResolveOrDeferEntrenchedReaction`.
4. `ResolveOrDeferBeforeActionReaction` (`beforeActionReaction`), `ResolveOrDeferUnimpeachableName`, `ChainOutOfPlayReactionWindow` (Bad Kharma, Footsteps of Madness).
5. Targeted-action negations (`TargetShieldNegates` ... `ParalyzingTouchNegates`), `NegateEveryAction`, `AbortActionForBlockedCost`.
6. Any effect that pauses mid-resolution (`PendingEffectChoice`, `PendingLook`, `PendingSearch`, `PendingEitherOr`, `PendingOpponentPayment`, forced discards, Overconfident draws) before `CompleteAbilityResolution`.
7. Nested: a Reaction played from hand inside any of these is itself such a card, so the area is a stack.

Already correct, unchanged: a deferred Attack and a hand-played Dueling Strategy are spent as their window opens (`ExecuteAbilityEffects`, `IssueChallenge`; issue #320). That is a stopgap that departs from rule 1914 (see Open questions).

## Proposed design (smallest authoritative form)

**Option A (recommended): a real zone plus an ordered list.**

- `Zone.Resolving` appended to the end of the `Zone` enum (string-serialized by `JsonStringEnumConverter`, so no numeric shift).
- `MatchState.ResolutionArea : List<ResolutionEntry>`, oldest first (a stack: a Reaction played in response sits above the action it answers). `ResolutionEntry { CardId, OwnerId, AbilityId }`. Declared beside `PendingAnnouncedAction`; `StateCloner` copies it through the JSON contract automatically.
- **Entry:** in `UseAbility`, immediately after `ValidateAbility` succeeds and before `ControlReturnsOnAnnouncement` / `OpenForcedDiscardForTargetedTrait`: if the source card is a Strategy or Ring in the acting player's hand, remove it from `Player.Hand`, set `Zone = Resolving`, `FaceUp = true`, push an entry, emit one public `cardResolving` event. `ValidateAbility` throws before this, and `ExecuteOne` restores the original state on a rejection, so an illegal announcement never moves the card ("back on illegal play" is the atomic reject, not a code path).
- **Exit (all through existing chokepoints):** `DiscardFate`, `RemoveFromGame` and the attach/enter-play paths that today do `player.Hand.Remove(source.Id)` also remove the area entry (one helper, `LeaveResolutionArea(card)`). `SpendHandPlayedCard` keys on `Resolving` (and still on `Hand`, for the unconverted paths). `CompleteNegatedAction` already spends the card, so a negated or declined-window action discards from the area. `sincerity-dragon-name` / `honesty-phoenix-name` removal reads `Resolving`. A card that fails to enter play stays in the area and is discarded when the action ends (rule 993), which matches today's tail discard.
- **Predicate:** `PlayedFromHand(card) => card.Zone == Hand || card.Zone == Resolving` (origin is always the hand in this phase). It replaces `source.Zone == Zone.Hand` in the post-announcement readers only (about 25 sites: `ConditionalProduction.cs:129` `PaymentKind.Play`, `ShadowsEmbraceBatch4` Marshalling Fields waiver, `TfStrategiesB:183`, `WlStratACEffects:158`, `Wave737:182`, `Wave738:155`, `SeedsOfDecayBatch2:349`, `TirelessHome:97`, `CardAbilities` 7456/7596/9614). Announcement-time validation (`AbilityGates.SourceInHand`, `CardAbilities` ~5345-5360) deliberately stays `Zone.Hand`, which is also what stops a resolving card offering a second action.
- **Hand count and list:** `PlayerState.Hand` and therefore `PlayerView.Hand` / `HandCount` exclude the card from announcement onward, for both seats. Hand limit, `MaxHandSize` and End Phase discard are unaffected (no action is pending in the End Phase). `HandSights` already filters `Zone == Hand`.
- **Visibility:** public to both seats from announcement. The rules make the area face-up and the announcement is public; the opponent already learns the card from the announce window, so nothing hidden is exposed. The card is listed once, in the area, never in a hand array.
- **View:** `MatchView.ResolutionArea : ResolutionEntryView[]?` as a trailing init property (`ResolutionEntryView { CardView Card, int OwnerId, string AbilityId, string AbilityLabel, int Depth }`), null when empty, `WhenWritingNull`, so every payload outside an open action is byte-identical to today and positional records do not shift. Placed last in declaration order (System.Text.Json writes in declaration order).
- **Events/journal:** one new public event `cardResolving` ("X's Strategic Strike is placed in the resolution area"). Exit reuses the existing `fateDiscarded` / `removedFromGame`. Replays are command lists, so the replay format is unchanged. Recommendation: no `ContentVersion` bump (precedent: bumped once, #611, for incompatible rooms; this changes no command's legality except the pathological ones below). Mendon to confirm.
- **Seppuku / decision state:** `ResolutionArea` is not named `Pending*`, so `HasOpenDecisionState`'s guard test does not demand it be classified. Add it to `DecisionStates` anyway (`ResolutionArea.Count > 0`) so a leaked entry fails safe, and add an invariant: after every accepted command in tests (the `Accept` helper), every entry's card has `Zone == Resolving`, every `Resolving` card has an entry, and a non-empty area implies one of the other decision states is open. An entry surviving a command boundary with nothing pending is a bug the harness reports, as the reaction probe does for windows.
- **Cloning/save:** the list is plain data; `MatchEngine.Copy`, the R4 / Honor-loss / entry-cost replays (`ExecuteOne` replays the original command from a clean base, so the card moves again from the base) and save/replay all re-derive it deterministically from `UseAbility`.

Lifecycle: `Hand` -> (announce) `Resolving` -> (action ends, negated, window declined, blocked cost) `FateDiscard`; `Resolving` -> `RemovedFromGame` for Sincerity / Honesty; `Resolving` -> `Attached`/`Home` for the existing enter-play paths (the card leaves the area on entry). Illegal: never leaves `Hand`.

**Option B (flag on the card, keep `Zone.Hand`):** every `Player.Hand` reader would have to filter the flag, which is the status quo's exclusion scatter with a name. Rejected: the 400+ readers are the problem.

**Option C (view-only):** hide cards named by the held commands (`PendingAnnouncedAction`, `PendingNegatableAbility`, `PendingAdditionalAction`, `PendingOutOfPlayActionReaction`, `SuspendedWindows[*]`) from `Hand` / `HandCount` in `GetView`. About 40 lines, fixes the client contract and the report's wording, but leaves Bofana's discard, `DiscardHand`, the engine's own counts and the opponent-effect reads wrong. Acceptable only as a fallback if A is judged too large; do not ship it as the end state.

## Blast radius (everything that reads Hand)

Counts: `\.Hand\b` 494 hits in `src`, `Zone.Hand` 260; `Hand.Count` 27 in Core; 189 test files touch hand state (most only in setup).

| Reader | Effect of the change |
|---|---|
| `MatchQueries.GetView` (hand list, `HandCount`) | Excludes the card; new `ResolutionArea` field. |
| Hand-exclusion sites (~16, above) | Become redundant, stay correct; leave them (no churn), add a comment. |
| `OpenForcedDiscardForTargetedTrait` / `PendingForcedDiscards` | The played card is no longer a discard candidate (correct; today it can be chosen). |
| `AbilityCosts` `DiscardHand`, Wave738 / PromoItems hand-discard notes | Exclude the card (correct per 995). |
| Opponent hand effects in the windows (random/choice discards, `P2HandDiscardBlocked`, Look/hand sights, Overconfident) | Cannot touch the resolving card; counts shrink by one (correct). |
| `DuelRules:204`, `EmperorP5:505`, `MoshiInoStorm`, `ShadowsEmbraceBatch3:452`, `ForgottenLegacyBatch4/5`, `WlHoldings:116` hand-nonempty gates | Evaluated against hand without the played card: a lone played card no longer satisfies "has a card to discard". Matches the rules; behavior change in rare cases, covered by tests. |
| `TargetedActionReactionCandidates`, `BattleActionReactionWatchers`, announce watchers, Stormless Fury (all scan `Zone == Hand`) | The resolving card stops being a candidate to react to its own action (fixes the #911 family at the root). |
| Post-announcement `source.Zone == Hand` (~25) | Switch to `PlayedFromHand`; each is a test target. Missing one changes Gold payment kind, waivers or discard timing. |
| Mid-effect holders of the card id (`PendingAttack`, `PendingDuel.SourceCardId`, `LastResolvedAction`, `SuspendedWindow` commands) | Hold ids only; unaffected. |
| `MatchState` replays that re-run the original command | Re-derive entry; need tests with Honor-loss, entry-cost and R4 windows. |
| AI (`L5R.AI`): `TrainingOpponent.Battle:25` (`inPlay` = owner's card not in own `Hand`), `TrainingOpponent.cs:57` and `BotContext:65` card indexes, `DuelOpponent`, `BotProfile` | The card index must include `ResolutionArea` cards or lookups of the held action's source return null; the `inPlay` test would mis-class a resolving card as in play (it only sees offered actions, so low exposure; add a guard). `Hand.Length != HandCount` checks stay consistent (both exclude). |
| Server (`L5R.Server`) | No `Hand` reads; it serializes `MatchView` with `JsonSerializerDefaults.Web`. New field null-when-empty; unknown `Zone` string `Resolving` needs client and server from the same build (they ship together). |
| Client: hand tray (`Main.Board` 38-46), HUD / `TablePile` counts, `Main.HandDiscard` (`Self.HandCount - n`) | Counts become correct automatically; the played card disappears from the tray at announcement and needs the area drawn (see Client). |
| Client: `Main.Motion:72-149` (flight of a played/discarded card is inferred from `previous.Zone == Hand`), `CardTile` `Zone == Hand` lift/scale, `Main.cs:598` projection | Must treat `Resolving` as its own source/zone for animation; a `Resolving` tile must not go through the table-plane projection by accident. |
| Captures / showcases (`Main.*Capture`, `*Showcase.cs`) | Compile unchanged; scripted positions that pause a Strategy mid-window change expected counts. |

## Client contract

New: `MatchView.ResolutionArea` (ordered, public, oldest first), `Zone.Resolving`, event `cardResolving`. Client obligations (separate UI PR, Mendon holds standing authority, Alfredo's lane by habit): render each entry as a face-up card in a short strip near the reaction presentation, keep it there through all windows, animate hand -> strip on announcement and strip -> discard when `fateDiscarded` for that id arrives; never place a `Resolving` card in the hand tray or count it as discarded early. Counts come from `HandCount`, never from `Hand.Length` of the other seat.

## Regression tests (Core, `tests/L5R.Core.Tests/ResolutionAreaTests.cs`)

1. The reproduction, inverted: announce Strategic Strike with a No Hiding Place in hand; own list excludes the Strike, `HandCount` 1 in both views, Strike in `ResolutionArea` for both seats with identity, `Zone == Resolving`; after both decline, Strike is in `FateDiscard`, area empty, hand count 1.
2. Negation: whole-action negation (Effortless Counterattack / `NegateEveryAction`) leaves the card discarded once, nothing in the area.
3. Declined and passed windows: before-action, Unimpeachable Name, Additional Action, out-of-play (Bad Kharma) windows each show the card in the area, never in hand, and end discarded.
4. Illegal announcement: rejected command leaves the card in hand and the area empty (atomicity).
5. Nesting: Reaction played from hand inside an announce window gives a two-deep area (oldest first) that unwinds in order.
6. Bofana: the forced discard at announcement offers only the other hand cards.
7. Hand-reading effects mid-window: random discard, `DiscardHand` cost, hand-nonempty gates, hand sights, Overconfident draw do not see the card; the rest of the hand is unaffected.
8. Payment kind, Marshalling Fields waiver and the ~25 post-announcement readers still treat the card as played from hand (one case per site class).
9. Sincerity / Honesty removal and a card that fails to enter play return to the area, then discard / remove at the end.
10. Replay/save: command-list replay and a mid-window save/restore reproduce the area; Honor-loss, entry-cost and R4 replays rebuild it.
11. Invariant added to `RegressionAssertions.Accept`: area entries are consistent, empty with no pending decision, and the area blocks Seppuku.
12. View: `ResolutionArea` is absent from the payload outside an open action (byte-identical JSON in the existing fixtures) and present with the same cards in both seats during it.
13. AI: the training opponent does not fault when the held action's source is in `ResolutionArea` (card index contains it).
14. Run the full Core suite with the reaction probe (`L5R_REACTION_PROBE=1`) before and after: the Reaction offers must not change except the removal of self-reaction by the resolving card.

## Risks

- **Missed post-announcement reader.** The dominant risk: a `source.Zone == Hand` left unconverted silently changes payment kind, waivers or discard. Mitigation: grep gate in the PR (no `Zone.Hand` reads on the source after `UseAbility` without `PlayedFromHand`), the 25-site test matrix, and the invariant.
- **Stale entry.** Paths that move the card with a bare `Zone =` assignment bypass `LeaveResolutionArea`. Mitigation: the Accept invariant fails tests at the first leak; `DecisionStates` makes a runtime leak block Seppuku visibly rather than corrupt play.
- **Behavior changes that are correct but visible**: opponent or own forced/random discards can no longer pick the card being played; a lone played card no longer satisfies "has a card in hand". Needs Mendon's sign-off per Open questions.
- **Replay-from-clean-base mechanisms** (Honor loss, entry cost, R4) snapshot state; any snapshot taken after the move must not be replayed through `UseAbility` again. Covered by test 10.
- **Client regressions** if the UI PR lags the engine PR: the card vanishes from the tray with nowhere to appear. Sequence: land engine and client area rendering together, or ship the engine behind the same release.
- **Unknown-enum deserialization** of `Resolving` by an old client: same-build shipping, no mixed versions supported.
- **Scope creep**: Events, Disciplines played from the discard pile, hand Items/Followers/Personalities with actions, and deck abilities (rule 876) also use resolution areas; deliberately excluded here.

## Size

Core: ~250-350 lines in about 10 files (`Models.cs`, `CardAbilities.cs`, `ActionNegation.cs`, `MatchQueries.cs`, `SeppukuTiming.cs`, the ~25 `PlayedFromHand` sites) plus ~400 lines of tests and an unknown number (estimate 10-30) of existing tests that assert mid-window hand state and need updating. Client: ~150-250 lines (strip, motion source, tile zone handling) plus a capture. AI: ~10 lines. One focused rules PR (about a day with the full Core suite and probe runs) and one client PR. Option C alone is ~40 lines plus ~4 tests.

## Open questions (all answered above)

1. Zone-based Option A, or the cheap view-only Option C first?
2. Phase 1 scope: Strategy and Ring only (recommended), or every card whose action is taken from hand?
3. Keep the Attack/Duel stopgap (discard as the window opens), or hold the card in the area until `ResolveAttack` / `ResolveDuel` ends (rule 1914)? Holding it changes what Reactions in `attackReaction` can see in the discard pile.
4. Confirm the rulings that a resolving card cannot be chosen by an opponent's random/forced hand discard and does not count for "has a card in hand" (rule 995 reading).
5. Announcement identity is public from step A (matches rule 991 and the announce window); confirm.
6. No `ContentVersion` bump.
