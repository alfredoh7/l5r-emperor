# C# unhappy-path and consistency audit (issue #513)

Whole-codebase review of every C# project (Core, Server, Client, AI) and a full pass over the test projects. The audit looked for unhandled failure paths (illegal input, rejection atomicity, replays and races, empty or declined choices, hidden-information leaks, dependency failure, missing negative tests) and for deviations from the codebase's own prevailing patterns. It is a read-only review. The only code changes in this PR are eight test-only fixes (§8).

## 1. Scope and method

- **Head audited:** `830d2a4a` (origin/main at the start). Branch `claude/513-csharp-audit`. Every quote and line number below refers to `830d2a4a`, except where §8 says a trivial-fix commit changed the line.
- **Orchestration:** one Opus session orchestrated Sonnet subagents, at most five at a time, all read-only on the repository.
- **Sweep runs (47):**
  - 4 mechanical sweeps (T0 test inventory, S1 and S2 signature sweeps, X5 engine instance fields) and 4 cross-cutting sweeps (X1 pending-choice matrix, X2 command fields, X3 redaction, X4 Limitations).
  - 12 engine, server, client and AI slices (E1–E12).
  - 8 batch-card slices (B1–B8): two files per family read fully, plus signature sweeps.
  - 19 test slices (T1–T19), covering every test file in `tests/`.
- **Pattern inventory:** a Phase A inventory (C1–C13) was taken before any consistency flagging.
- **Standing probes:** P1–P4 were run by the orchestrator in a scratch project.
- **Finding schema:** every finding records its file, line and an exact quoted line, a repro of the right type, a severity and a root-cause key. Severity rules follow the plan:
  - **Blocker:** a legal command sequence gives a wrong rules outcome, corrupts state, leaks hidden information, makes a replay diverge, or loses room data.
  - **Major:** a reachable failure path goes unhandled or unreported, or a player-reachable command or choice has no rejection test at all.
  - **Minor:** anything else.
- **Mechanical quote check:** a script (no model) matched every quote against the file at `830d2a4a`. Result: 82 records, 1 dropped (1.2%), plus 3 occurrence quotes dropped. No slice was above the 25% re-sweep threshold (§2).
- **Adversarial verification:** every blocker and major went to a fresh Sonnet verifier told to try to refute it. The confirmation bar depends on the finding type:
  - Rules, atomicity, replay: a scratch repro that fails on HEAD.
  - Hidden information: the opponent's view JSON contains the hidden id.
  - Server: a hosted repro, or a hop-by-hop trace.
  - Client (Godot is not runnable here): an independent re-read of every hop.
  - Missing test: an empty grep, plus a repro showing whether the rejection works.
- **Scratch repro projects:** these live outside the repository (`AssemblyName` `L5R.Core.Tests`, referencing the built `L5R.Core.dll` and linking the shared test helpers). None is committed.
- **Minors** received only the quote or peer-reference check.
- **Out of scope (optional wave 5, not run):**
  - full reads of the remaining batch-card files;
  - `CardAbilities.cs` 154–3914 (catalogue definitions), except the mechanical ability-id coverage check in X4;
  - client presentation and harness files (`Main.*Capture.cs`, `Main.*Checks.cs`, `*Probe*.cs`);
  - Godot runtime behaviour.

## 2. Summary

| Area | Blocker | Major | Minor | Unverified (counted one level lower) |
|---|---|---|---|---|
| Core engine | 2 confirmed (AUD-01, AUD-02) | — | 6 (AUD-03, AUD-07–AUD-11) | 3 (AUD-04, AUD-05, AUD-06) |
| Server | — | 2 confirmed (AUD-12, AUD-13) | 1 (AUD-14) | — |
| Client and AI | — | 3 confirmed (AUD-15, AUD-16, AUD-17) | 2 (AUD-18, AUD-19) | — |
| Tests | — | 5 confirmed (AUD-20–AUD-24) | 7 (AUD-25, AUD-26, AUD-28–AUD-32) | 1 (AUD-27) |
| **Total** | **2** | **10** | **16** | **4** |

In total there are 32 consolidated findings: 28 confirmed or checked, and 4 unverified. Three of the unverified findings were filed as majors: AUD-04 (its verifier also lowered the claim to minor), AUD-05 and AUD-06. The fourth, AUD-27, is a minor rules question. Separately, three mixed-pattern notes await a lead decision (§5). Three findings were refuted and five were dropped by rule (§7).

**Quote-check results** (82 raw records):

| Slice | Records | Dropped |
|---|---|---|
| T12 | 2 | 1 (T12-02's primary quote did not match; its 20 occurrence quotes did and are kept under AUD-26) |
| all others | 80 | 0 |

Three occurrence quotes were also dropped (T14-01 at `DuelDemoChecks.cs:192`, T19-01 at `RoomManager.cs:94`, T6-03 at `EmperorP3Priority.cs:272`).

**Standing probes** (§7.3): P1 found 0 state changes from QueryActions and GetView across 8,560 positions. P4 found 0 replay divergences in 367 checks. P3 found no real hidden-id leak. P2 found one offered-but-rejected command (AUD-03).

## 3. #510 re-check and "Extends #510"

#510's fixer, PR #512, is open and unmerged. All seven #510 items are still present on `830d2a4a`:

| #510 item | Evidence at `830d2a4a` | State |
|---|---|---|
| `ArgumentException` for player input in `CustomPractice.cs`, `DeckWorkshop.cs`, `GoldShowcase.cs` | `rg -c "throw new ArgumentException"`: 2, 3 and 1 | still present |
| `CheckFirstActionRestriction` naming | `BattleTiming.cs:129` `private void CheckFirstActionRestriction(GameCommand command)` | still present |
| Unordered choice lists at `CardAbilities.cs` ~10893 and ~10968 | both lines have no `OrderBy` | still present |
| Client `async void` | 21 sites; `Main.cs:536` `private async void Submit(GameCommand command)` | still present |
| Server `CancellationToken` | `RoomManager.cs:82/95/110` take no token | still present |
| Server rejection logging | `SubmitAsync` rejection branches log nothing (inventory C10) | still present |
| `TreatWarningsAsErrors` for Client and AI | absent from both `.csproj` files | still present |

**Extends #510** (posted as one comment on #510, not a new issue): AUD-09. The `branch-dispatch` default arms throw `InvalidOperationException` instead of `RuleViolation` at sites #510 does not list: `EffectChoice.cs:141/176`, `AbilityCosts.cs:297`, `AttackTargeting.cs:99`, `GoldQuotes.cs:209`, `AlternativeCosts.cs:103`, `CreatedCardEntry.cs:34`, and the per-batch dispatchers `ForgottenLegacyBatch1.cs:206` and `EmperorProxyX.cs:91`. All of them are unreachable wiring guards under the current catalogue.

## 4. Findings by area (most severe first)

Status key:
- **confirmed**: met its verification bar.
- **unverified**: the verifier could not meet the bar; the finding stays in the report and is counted one level lower.
- **checked**: a minor finding that passed the quote or peer-reference check.

Issue numbers for each follow-up group are in §9.

### Core engine

**AUD-01 (blocker, confirmed): a spoofed `Provinces` field satisfies Beyond the Line's province restriction.** Sources: X2-01, X2-02.
- **Where:** `src/L5R.Core/EmperorMechA.cs:281`: `Require(TargetsCurrentProvinceOnly(command), "Beyond the Line: the Recon ability must target the current battlefield's province (and only it).");`
- **Cause:** `TargetsCurrentProvinceOnly` (`EmperorMechA.cs:178`: `private bool TargetsCurrentProvinceOnly(GameCommand command)`) reads `command.Provinces`/`Province` whatever the ability's Target kind is. `ValidateAbility` rejects stray fields only for `Target=="attack"` (`CardAbilities.cs:6259-6264`), not for `"none"` or `"hand"` (`CardAbilities.cs:6248`, `6293-6295`). The grant-consumption predicate (`EmperorRemainingC.cs:583-585`) re-reads the same function.
- **Effect:** a `useAbility` for a Recon ability that targets no province can spend Beyond the Line's grant and draw its card. The ability can be Target `"none"`, or Target `"hand"` such as Daidoji Akeha's `akeha-recon-search`. `EmperorMechATests.BeyondTheLineRecon` (`:311-337`) only checks the QueryActions offer and never submits the spoofed command, so no test covers the restriction (X2-02).
- **Repro:** fails on HEAD.
  ```csharp
  var s = Table(Phase.Battle, 1);
  foreach (var id in s.Cards.Values.Where(c => c.Zone is Zone.Home or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).Select(c => c.Id).ToArray()) s.Cards.Remove(id);
  StrongholdOfFixture(s, 1, "oracle-l5r-3257");                       // Hidden Falls Dojo
  var actor = Add(s, "training-recon-actor", 1, Zone.Battlefield, province: 0);
  Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
  var strategy = Add(s, "oracle-l5r-967", 1, Zone.Hand);              // Beyond the Line
  var e = MatchEngine.FromTestState(s);
  Accept(e, Enabled(e, 1, "beyond-the-line-recon", strategy.Id).Command!);
  var spoofed = new GameCommand("useAbility", 1, e.Version, CardId: actor.Id, AbilityId: "training-recon-open", Provinces: [0]);
  Reject(e, spoofed);   // should reject; on HEAD it is accepted, the grant is consumed, and a card is drawn
  ```
  ```
  FAIL repro/X2-01 Beyond the Line's grant should be rejected for a non-province Recon ability carrying a spoofed Provinces field
  System.Exception: Expected rejection: GameCommand { Type = useAbility, PlayerId = 1, ExpectedVersion = 1, CardId = fx-154, ... AbilityId = training-re...
  PASS repro/X2-01-evidence (documents the HEAD exploit, not a correctness assertion): the spoofed command is accepted, consumes the grant, and draws a card
  ```
- **Fix:** issue (rules). Only read `Provinces` when the ability's Target kind is a province target, or reject stray command fields per Target kind in `ValidateAbility`. Add a `Reject` regression test.

**AUD-02 (blocker, confirmed; the replay-divergence part is unverified): `_runningCommand` is left stale after a sequenced-target Attack pauses for an Honor-change window.** Sources: X5-01, X5-02.
- **Where:** `src/L5R.Core/SequencedTarget.cs:78`: `_runningCommand = paused.Command;`
- **Cause:** the assignment is followed by `ResolveOrDeferAttack(...)` and a plain restore, with no `try/finally`. The four sibling save/restore sites all use `try/finally`: `EffectChoice.cs:233-236`, `EmperorFollowupsSearch.cs:107-109`, `CardAbilities.cs:7255-7259` and `EmperorP4More.cs:221-225`.
- **Real sequence:** an Engulfing Flames follow-up Attack destroys a Dishonorable target whose controller holds Solid Defense. The "Dishonorable death" loss (`MatchEngine.cs` ~2622) throws `HonorChangeWindowNeeded` inside `ResolveOrDeferAttack`, so the restore line is skipped.
- **Effect:** when the paused command later finishes, line 77 captures the already-stale value, so the field stays wrong for the rest of the session. About 25 readers consume it (attack-modifier release, No Hiding Place, `CurrentActionFacts`, `MoveToBattlefield` attribution).
- **Limits of the evidence:** the repro reads the field by reflection. A downstream wrong rules outcome was not demonstrated. X5-02's claim of a replay divergence caused by an unjournaled rejected command was not reproduced, because this trigger is accepted and journaled.
- **Repro:** fails on HEAD. Full source is in the audit scratch; this excerpt shows the key steps.
  ```csharp
  var monk = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);   // + Monk keyword, Fire token
  var followed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); followed.Dishonorable = true;
  var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: followed.Id);
  Add(s, "oracle-l5r-7329", 1, Zone.Hand);            // Solid Defense
  Add(s, "training-lion-veteran", 1, Zone.Home);
  var card = Add(s, "oracle-l5r-2342", 0, Zone.Hand); // Engulfing Flames
  var e = MatchEngine.FromTestState(s);
  Accept(e, Enabled(e, 0, "engulfing-flames-destroy", card.Id, monk.Id).Command! with { Selections = [Pick("attachment", follower.Id)] });
  e.Execute(e.QueryActions(0).First(x => x.Type == "chooseSequencedTarget" && x.CardId == followed.Id).Command!); // opens honorChangeReaction
  // decline every window until nothing is pending, then:
  Check(RunningCommand(e) is null, "...");  // fails: still {useAbility engulfing-flames-destroy}
  ```
  ```
  FAIL repro/Reproduce: X5-01: _runningCommand is restored once a sequenced-target Attack's resolution has left the pipeline, even when it paused for a Reaction on an Honor change the Attack caused
  System.Exception: once the whole exchange has genuinely finished, _runningCommand should be null again -- instead it is still {Type=useAbility, AbilityId=engulfing-flames-destroy, CardId=fx-159, PlayerId=0} (the leak from SequencedTarget.cs:81 outlives even th...
  ```
- **Fix:** issue (rules). Wrap the save/restore in `try/finally` like the siblings. Add a regression test that asserts an observable effect.

**AUD-03 (minor, confirmed; reported as major, lowered): the Duel Demo offers an enabled `playStrategy` that `Execute` rejects.** Sources: E10-01; X4-01 was refuted as a duplicate of this.
- **Where:** `MatchQueries.cs:662` lists hand Strategies with `Definition(c).ScenarioOnly`. The `training-duel-*` challenge card's `playStrategy` then fails at `MatchEngine.cs` ~2261 with "This card effect is not implemented."
- **Repro:** `OfferedCommandsValidate` over `CreateDuelDemo("refusal"/"force"/"honor",1)` fails on HEAD with 0/3 passing (`GameCommand { Type = playStrategy, ... CardId = demo-0007, ... TargetId = demo-0004 ...`).
- **Why lowered:** the shipped client never shows it. `MatchSession.cs:59-66` filters Duel Demo actions by `AbilityId`, and `DuelScreen.cs:201` whitelists the command types it shows.
- **Fix:** issue.

**AUD-04 (minor, unverified; reported as major, lowered by its verifier): `PendingChoice` reaches both players' views with no redaction chokepoint.** Source: E3-01.
- **Where:** `MatchQueries.cs:39`: `_state.Phase,_state.PendingChoice,_state.CurrentBattleProvince,_state.UnresolvedBattlefields.ToArray(),`. Every other per-player pending view (Search, Look, Copy, ProvinceSights, HandSights) gates on the viewer; `PendingChoice` has no guard and no documented visibility rule.
- **Why unverified:** all 92 `PendingChoice = new(` sites were read, and a mechanical probe covered 8,560 positions. No concrete leak was found. The search-recruit prompts name a card only after `ShowSearchedCard` has made it public (see S2-01 in §7).
- **Fix:** issue (design hardening).

**AUD-05 (minor, unverified; latent): exceptions thrown inside `Execute`'s control-flow catch bodies skip the `_state` restore.** Source: B4-01.
- **Where:** `MatchEngine.cs:264`: `catch (HonorChangeWindowNeeded need)`. Its sibling catches at `242/259` call `OpenHonorChangeWindow` and `SetAsideForEntryCost`. An exception thrown from those calls is not caught by the sibling `RuleViolation` or generic catches (`277/282`) of the same `try`.
- **Why unverified:** the verifier traced both call graphs and found no reachable throw with current content.
- **Fix:** issue (defensive hardening).

**AUD-06 (minor, unverified; latent): at-resolution payment swallows `RuleViolation` after a split payment's lead part has mutated state.** Source: S1-01.
- **Where:** `EmperorMechE.cs:304`: `catch (RuleViolation failure)`. `PaySplit` (`GoldQuotes.cs:418-424`) pays the lead part first.
- **Why unverified:** `LeadAmount>0` requires `ability.Discipline`. Neither caller of `TryPayAtResolution` (`GreasingBattle`, `kanaye-bow`) is Discipline, and none of the 9 Discipline abilities reaches it.
- **Fix:** issue.

**AUD-07 (minor, checked): a QueryActions legality probe writes an engine field.** Source: B5-01.
- **Where:** `EmperorPersonalityBatch4.cs:170`: `private void UsePaymentDiscards(GameCommand command) => _paymentDiscards = command.PaymentDiscards ?? [];`
- **Cause:** this is reachable from `AbilityRejection` (`CardAbilities.cs:10878`), which QueryActions calls for every reaction candidate (`MatchQueries.cs:104/126`).
- **Why no wrong outcome today:** every real payment site sets the field again immediately before reading it. Probe P1 saw no state change, because `_paymentDiscards` is not part of `MatchState`.
- **Fix:** issue.

**AUD-08 (minor, checked): Custom Practice and Duel Demo setup shuffles never persist `RngState`.** Source: S2-02.
- **Where:** `CustomPractice.cs:106`: `random.Shuffle(player.DynastyDeck); random.Shuffle(player.FateDeck);`. `DuelDemoScenarios.cs:82/85` has the same shape. Compare `MatchEngine.cs:102` and `PublishedMatch.cs:33`.
- **Effect:** the first mid-game shuffle of a Custom Practice match starts from `RngState` 0 whatever the seed. Replay reproduces this identically; P4 saw no divergence. No test pins it (T18).
- **Fix:** issue.

**AUD-09 (minor, checked, consistency, inventory C1 prevailing): catalogue-dispatch default arms throw `InvalidOperationException`, not `RuleViolation`.** Sources: E4-01, E9-01; batch sites listed by B7 and B8.
- **Where:** `EffectChoice.cs:141`: `_ => throw new InvalidOperationException($"Unknown branch condition '{condition}'.")`, and `AbilityCosts.cs:297`: `default: throw new InvalidOperationException($"Unknown cost verb '{cost.Verb}'.");`. The full list is in §3.
- **Why only minor:** these are unreachable wiring guards.
- **Fix:** "Extends #510" comment. A lead should decide whether wiring guards are exempt from C1.

**AUD-10 (minor, checked, latent): Stolen Merchandise's ability scan bypasses `AbilityIdsOf`.** Source: B2-01.
- **Where:** `EmperorP5Hidden.cs:252`: `private IEnumerable<string> StolenAbilityChoices(CardInstance holding) =>`. It reads the printed abilities and skips the `P5AbilitiesRemoved` gate, although `CardCopy.cs:84` documents `AbilityIdsOf` as the one scan.
- **Why unreachable today:** Unsettling Doubts can only target a Ring, and Stolen Merchandise can only target a Holding.

**AUD-11 (minor, checked): `DeckCardInfo.Implemented` is a heuristic, not a dispatch check.** Source: X4-02.
- **Where:** `CustomPractice.cs:24`: `def is { ScenarioOnly: false } && c.Id.StartsWith("oracle-l5r-", StringComparison.Ordinal), def?.CreatedOnly ?? false,`
- **Why it matters:** this flag backs the Custom Practice "implemented cards" claim. The X4 mechanical check found all 1,192 `AbilityCatalog` ids handled today, so nothing is wrong now, but the flag can drift from reality.

### Server

**AUD-12 (major, confirmed): one incompatible or corrupt room journal stops the whole server from starting.** Source: E5-01.
- **Where:** `src/L5R.Server/RoomManager.cs:23`: `throw new InvalidDataException($"Room {record.RoomCode} uses incompatible content {record.Replay.ContentVersion}.");`
- **Cause:** `LoadAllAsync`, `LoadAsync`, `ServerApplication.BuildAsync` and `Program.cs` all have no per-room catch.
- **Repro:** a hosted `ServerApplication.BuildAsync` with one valid room and one stale-content room fails on HEAD.
  ```
  ServerApplication.BuildAsync threw: InvalidDataException: Room BBBBBBBB uses incompatible content some-old-content-version.
  FAIL one incompatible room journal must not stop the whole server from starting
  0/1 checks passed.
  ```
- **Fix:** issue (shared). Skip or quarantine the bad room with a log line.

**AUD-13 (major, confirmed by code trace): a non-`RuleViolation` exception from a command escapes `SubmitAsync` and closes the socket with no message and no log.** Source: E5-02.
- **Where:** `RoomManager.cs:154`: `var result = trial.Execute(envelope.Command.BindToSeat(seat.PlayerId, envelope.ExpectedVersion));`
- **Trace:** `Execute` rethrows non-rule exceptions (`MatchEngine.cs:282-286`). `ServerApplication`'s catches (116/120/124) handle only `JsonException`, `RoomException` and the WebSocket exception family. No live trigger is known today: the candidate E7-01 was refuted (§7).
- **Fix:** issue (shared). Catch, log, and send an `ErrorMessage`.

**AUD-14 (minor, checked): no rate or flood limit on the WebSocket command channel.** Source: E5-06.
- **Where:** `ServerApplication.cs:61`: `app.MapGet("/rooms/{code}/play", HandleWebSocketAsync);`. Only `/rooms` and `/rooms/{code}/join` use `RequireRateLimiting`, and every command replays the match.

### Client and AI

**AUD-15 (major, confirmed): the practice bot can stall silently.** Sources: E5-03, E5-04.
- **Where:** `src/L5R.Client/MatchSession.cs:121`: `if (command is null) return false;`. It gives no notice, and Main's ~0.7 s timer retries forever. A rejected bot command is also resubmitted unchanged forever (`MatchSession.cs:123`).
- **Cause:** `TrainingOpponent.Prepare` (`TrainingOpponent.cs:44-50`) filters attackers with `!c.Bowed`. It ignores the engine's `MayAssignBowed` (`AssignBowedTurn`, Embracing Battle) and returns null before trusting the engine's pre-built `Assignments`.
- **Repro:** fails on HEAD. The fixture is composed with `FromTestState` (Embracing Battle's grant plus a `ForcedAttacks` entry, so `pass` is refused). Each ingredient is a real card effect, but the combined position was not reached by playing commands.
  ```
  FAIL repro/E5-03 TrainingOpponent.Choose returns a legal command when the engine offers an enabled attack for a bowed-but-assignable, forced Personality
  System.Exception: BUG reproduced: TrainingOpponent.Choose returned a null Command (reason: "No complete legal action was supplied for this decision.") even though QueryActions offers an Enabled 'Attack province 1 ...' with Command.Assignments al...
  ```
- **Fix:** issue (UI and AI).

**AUD-16 (major, confirmed by client trace): Practice builds its catalogue without a guard.** Source: E12-01.
- **Where:** `src/L5R.Client/Main.Practice.cs:20`: `var screen=new PracticeSetupScreen();_practiceLayer.AddChild(screen);`. `PracticeSetupScreen.cs:172` has the same shape. `LibraryDialog.cs:11-16` and `DeckBuilderScreen.cs:9-12` guard the same constructors.
- **Effect:** a corrupt or schema-mismatched packaged catalogue throws out of the Practice button. Because `_practiceLayer` is left attached, the button then does nothing for the rest of the session.
- **Precondition:** a broken package.

**AUD-17 (major, confirmed): the Deck Builder swallows a catalogue load failure.** Source: E12-02.
- **Where:** `src/L5R.Client/DeckBuilderScreen.cs:12`: `catch(Exception e){layer.QueueFree();GD.PushError(e.Message);}`. The player sees nothing, unlike `LibraryDialog.Show`, which shows a "could not be loaded" dialog.
- **Why it is not a C9 deviation:** C9 (client catch style) is mixed. This is reported as a silently swallowed loader failure on a player-reachable menu action, not as a pattern deviation.

**AUD-18 (minor, checked): the clan Gold bonus is implemented three times with different constants.** Source: E5-05.
- **Where:** `Main.cs:551` (client fallback, +3), `TrainingOpponent.cs:244` (+1) and `MatchEngine.cs:1211` (+1).
- **Why only minor:** display only. The engine still validates `SourceIds`.

**AUD-19 (minor, checked): `user://Decks` versus `user://decks` casing.** Source: E12-03.
- **Where:** `Main.cs:53`: `private string DecksDirectory => ProjectSettings.GlobalizePath("user://Decks");`. `DeckBuilderScreen.cs:52` and `PracticeSetupScreen.cs:28` use `user://decks`.
- **Why only minor:** `DecksDirectory` and `OpenDecksLocation` have no callers, so this is dead code, but it is a trap on case-sensitive filesystems if it is ever wired up. Not fixed here; the UI lead should decide.

### Tests

**AUD-20 (major, confirmed): 25 pending-choice kinds have no rejection test anywhere.** Sources: X1-01, T1-01, T4-01, T12-01.
- **Kinds affected:**
  - `harshChoices`, `searchRecruitPay`/`chooseSearchPayment`, `killingGroundsSelect`, `twistOfFateReaction`, `attackPhaseEndReaction`, `attackTargetSelect`, `rentatsuLoss`, `declineStraighten`, `p5BlessingsTop`/`chooseBlessings`, `honorChangeReaction`, `eventChoice`, `additionalActionReaction`;
  - the `p3*` kinds: `AroruMove`, `BeforeEventReaction`, `DarkVirtueDiscard`, `EntryReaction`, `MikiDarkVirtue`;
  - more listed in X1.
- **Correction:** `starCrossedLovers` has an ad hoc rejection at `ShadowsEmbraceBatch6Tests.cs:704` and was removed from the list.
- **Model guard:** `EmperorFollowupsAttack.cs:166`: `Require(command.Type == "chooseAttackTarget" && command.PlayerId == pending.PlayerId, "Choose the Attack's target, or none.");`
- **Repro:** for 7 kinds, a wrong-player answer and a malformed answer both reject atomically on HEAD (`6/6 repro tests passed`). This is a coverage gap only.
- **Fix:** issue (rules tests). Model the new tests on `DeckLookTests.cs`.

**AUD-21 (major, confirmed): 1,200 of 1,456 player-reachable ability ids (82.4%) have no rejection test anywhere in `tests/`.** Sources: T2-01, T2-02, T3-01, T8-01; the per-slice "untested ability ids" lists in T4–T18.
- **Example:** `Program.cs:4588`: `Accept(e,new GameCommand("useAbility",0,e.Version,CardId:advisor.Id,AbilityId:"veteran-advisor-draw"));`. This is the only test reference for the id, and it is happy-path.
- **Count:** a statement-level script over the 308 embedded content files. The script lives in the audit scratch.
- **Repro:** 8 ids across families were given illegal variants, including `tojikana-honor` with a non-Scorpion player after a destroyed province. All 8 reject atomically on HEAD (`8/8 repro tests passed`).
- **Fix:** issue (rules tests). Prioritise per-card conditions (clan, Honor, "not his own controller") over the generic `ValidateAbility` checks, which are already tested.

**AUD-22 (major, confirmed): seven network-reachable server paths have no test.** Source: T19-01.
- **Paths:** `session_replaced` (`RoomManager.cs:118`), `room_not_found` over `/play`, a command before hello (answered with `invalid_hello`), `CommandId` reuse across seats (dedup is keyed per seat), a command after the game is won, both seats disconnected, and reconnecting while the opponent holds a pending choice.
- **Repro:** all seven behave correctly on HEAD (7/7).
- **Fix:** issue (shared).

**AUD-23 (major, confirmed): an unknown `command.Type` is never tested.** Source: E1-01.
- **Where:** `MatchEngine.cs:640`: `default: throw new RuleViolation("Unsupported command.");`
- **Why major:** `WireGameCommand.Type` is unconstrained (`Protocol.cs:31`), so this is the only defence.
- **Repro:** a fresh match and four pending states all reject atomically (5/5).

**AUD-24 (major, confirmed): duel-stage wrong-player commands are never tested.** Source: E10-02.
- **Where:** `Dueling.cs:105`: `Require(command.PlayerId == duel.ChallengedPlayerId, "Only the challenged player may respond to the challenge.");`, and likewise `:111/:116/:121`.
- **Repro:** all four stages reject atomically (5/5). The rejection actually comes from the generic priority gate (`MatchEngine.cs:314`), so the `Dueling.cs` checks are defence in depth.

**AUD-25 (minor, checked): branches that test descriptions promise are never exercised.** Sources: E6-01, T13-01, T6-01, T6-02, T6-03, T13-03, T10-01, B1-01, T16-01, T4-05.
- `CardAbilities.cs:5822`: Castle of Order's once-per-battle `Require` is never tested.
- `SeedsOfDecayBatch1.cs:39`: `DestructionShielded`'s own-controller exclusion is never exercised.
- Growing Hostility's Unique exemption, Destructive Priorities' "not for a loss" and Utaku Lishan's "unopposed" case are promised by test descriptions and never exercised.
- Berserker delay: a dangling comment at `SecondCitySpiritTests.cs:315` says "His own controller's action does not." and is followed by no assertion.
- No test changes a province card mid-turn while a province sight is held (`EmperorStrongholdBatch3Tests.cs:211`).
- Ten Thousand Temples' `honorLossChoice` has no stale-version or `HiddenIdsAbsent` case.
- `chooseDecider`, `chooseFocusEffect` and `chooseSequencedTarget` have no stale-`ExpectedVersion` case.
- Three new deck-search scenarios never check the opponent's view.

**AUD-26 (minor, checked): vacuous or tautological asserts.**
- **Fixed in this PR:** T0-05, T0-07, T4-02, T4-03, T9-01 and T13-02 (§8).
- **Still open:**
  - `BeforeTheDawnCreationTests.cs:83` `Check(plain.State.Cards.Count >= 0);` and `BeforeTheDawnCardTests.cs:398`: the intended assert is not obvious.
  - `EmperorP5Tests.Zones.cs:226` `Check(honor == e.State.Players[0].Honor || true);`: the intent is unclear.
  - Fixture-id tautologies such as `EmperorStrongholdBatch2Tests.cs:204` `Check(pillars2.Id.Length > 0);`, `FoothillsKeepTests.cs:277` `Expect(w3.Id.Length > 0);`, and 20 similar lines in the BeforeTheDawn test files.
  - `tests/L5R.AI.Tests/Program.cs:48` `if(view.Phase==Phase.AttackDeclaration)`: the test can pass without asserting anything.

**AUD-27 (minor, unverified, rules question): Dragon's Favor's "second copy" assert is false without its `|| true`.** Source: T0-06.
- **Where:** `BeforeTheDawnCardTests.cs:386`: `Check(Options(de, 0, "dragons-favor-attack", favor2.Id).All(o => !o.Enabled || !(o.TargetIds ?? []).Contains(target.Id) || true), "the second copy is attached to a non-Th...`
- **What happened:** removing `|| true` makes the test fail. A copy attached to a non-Thunder Shugenja can target that Personality.
- **Open question:** either the description is wrong (the Thunder bonus is only +1 Chi) or the engine allows something the card forbids. This needs a rules ruling (label `needs-mendon-ruling`), so it was not fixed.

**AUD-28 (minor, checked; fixed): two assertion helpers were proving less than they claimed.** Sources: T0-01, T7-01.
- `EmperorRemainingFTests.cs:206` declared a local non-atomic `Reject` that shadowed the shared atomic one.
- `EmperorP5Tests.Hidden.cs:48` sent its refusal check with another engine's `Version`, so the version guard rejected it for the wrong reason.
- Both are fixed in this PR (§8).

**AUD-29 (minor, checked, consistency, inventory C8 prevailing at 81.6%): legality checks use the non-atomic `Rejection(` or `!Execute().Accepted` instead of the shared atomic `Reject(`.** Sources: T4-04, T5-01, T7-02, T8-02, T8-03, T10-03, T11-01, T14-01, T14-02.
- `EmperorRemainingETests.cs:286`: `Rejection(e2, Use(e2, 0, "kami-blessing-samurai", ...` (the file has no atomic `Reject` at all).
- `EmperorMechDTests.cs:608`: `Check(!e.Execute(EquipCommand(e, 1, spell, samurai)).Accepted, "a non-Shugenja host is refused");`
- `DuelDemoChecks.cs:55`: `Check(!e.Execute(command).Accepted,"duplicate rejected");`
- `GoldRulingsTests.cs:279`: `Rejection(e, new GameCommand("orderSeppuku", 1, e.Version, dishonorable.Id));`
- There are about 30 more sites in the source findings.

**AUD-30 (minor, checked): `EmperorRulingR4Tests` and `EmperorRulingR5Tests` have no `PublishedShape` test, unlike their R1–R3 siblings.** Sources: T0-02, T0-03.
- **Where:** `EmperorRulingR4Tests.cs:11` and `EmperorRulingR5Tests.cs:7`.

**AUD-31 (minor, checked): `Privacy()` checks only one player's view.** Source: T1-02.
- **Where:** `Program.cs:344`: `var e=MatchEngine.CreateStarterMatch(); var view=e.GetView(1); var s=e.State;`. The test never checks player 0's view, although its description says "Player snapshots".

**AUD-32 (minor, checked): helper bodies diverge (see the C13 mixed pattern in §5).** Sources: T0-08, T0-09, T0-10, T6-04.
- `EmperorP3Tests.cs:29`: `Pass()` sends `declineReaction`.
- `AfterResolutionTests.cs:46`: a ternary whose branches are identical.
- `EmperorStrongholdBatch2Tests.cs:47`: `ClosePassing`'s stop condition is much broader than its 11 siblings'.
- `EmperorP2Tests.cs:39`: `Settle()` has no fallback to the pending player.

## 5. Mixed patterns awaiting a lead decision

| Dimension | Variants (share) | Examples | Decision needed |
|---|---|---|---|
| C6 `catch (RuleViolation)` bodies (Core) | capture the message 47.6%, discard and return false 33.3%, empty 19.0% | `DuelPresentation.cs:137/143` (empty) | whether legality probes must record the reason |
| C9 client `catch (Exception)` | log only or swallow 55.6%, raw `e.Message` 22.2%, friendly text 22.2% | `DeckBuilderScreen.cs:12` (swallow) versus `LibraryDialog.cs:11-16` (friendly) | one client error-surface policy (AUD-17 depends on it) |
| C13 test pass/decline helper names | 128 definitions under about 38 names; the largest name (`Pass`) has 15.6% | `EmperorP3Tests.cs:29`, `EmperorStrongholdBatch2Tests.cs:47` | whether to consolidate into `Batch336Support` |
| C11 `user://` paths | no pattern (3 sites) | AUD-19 | UI lead |

## 6. Pattern inventory (compact)

| Dim | Question | Classification |
|---|---|---|
| C1 | `Require`/`RuleViolation` versus `throw new Argument/InvalidOperationException` (Core, PublishedContent excluded) | prevailing: `Require` 741 of 779 (95.1%); 21 throws are dispatch default arms (AUD-09) and 17 are loaders (#510) |
| C2 | `Require` message style | prevailing: sentence case with a trailing period, 26 of 26 sampled |
| C3 | ordering of player-facing lists | prevailing: `StringComparer.Ordinal` in 124 of 124 string or id orderings |
| C4 | `Pending*` lifecycle | prevailing: set, then cleared with `null` on accept and decline. `PendingBrashDraws` and `PendingOutOfPlayConsumedGrant` are value types reset by decrement and by `=false` (not bugs) |
| C5 | batch `Add*` registration chain | prevailing: 34 of 36 chained flatly at `CardAbilities.cs:3913`; P5Zones and P5Hidden are tail-delegated |
| C6 | `catch (RuleViolation)` bodies | mixed (§5) |
| C7 | `Validate*` throws, `Can*` returns bool | prevailing: 65 of 65 and 5 of 5; `Check*` means void watch triggers (8 of 10) |
| C8 | test rejection helpers | prevailing: shared atomic `Reject(` 905 of 1,109 (81.6%) |
| C9 | client catch style | mixed (§5) |
| C10 | server rejection logging | prevailing: unlogged (#510) |
| C11 | `user://` paths | no pattern (§5) |
| C12 | `PendingChoice` prompt phrasing | prevailing: two clean templates |
| C13 | test pass and decline helpers | mixed (§5) |

## 7. Appendix

### 7.1 Checked and found handled (refuted, or dropped by rule)

- **S2-01, refuted (blocker claimed).** The claim was that search-recruit prompts name a card still hidden in the deck. `DeckSearch.cs:378-381` runs `ShowSearchedCard` into the public `ShownCards` before any recruit prompt opens. The executed repro passes: after a decline, `HiddenIdsAbsent` holds over every other deck id.
- **E7-01, refuted (major claimed).** The claim was that a token is re-derived with `.First()` after reaction windows. `CardAbilities.cs:7851` `if (NegatesEffect(target!, "all")) break;` guards it, the redirect path re-runs `ValidateAbility`, and `DisciplineRulingsTests.cs:302` `KnowledgeAndPowerNegated` already covers it. `mitsu-fire-attack` and the `training-*` ids are not in the player pool.
- **X4-01, refuted as a duplicate of AUD-03.**
- **Dropped by rule:**
  - E1-02 and E6-02: consistency deviations not tied to any inventory dimension (Phase B rule).
  - E11-01: a static-loader fail-fast (do-not-report list).
  - E11-02: unreachable.
  - B4-02: an informational note about X5 regex coverage. `_p3LastHonorChange`, `_resumedAction` and `_proclaimAction` are tuple-typed fields the regex missed; each was checked and found safe.
- **Engine instance fields:** 25 of 27 are scoped safely (X5; `_runningCommand` is AUD-02). `_attachingFromPileId`, `_delayScopes` and `_paymentDiscards` are safe at their write sites (B4, B6).
- **Entry points and mechanisms probed with no finding:**
  - E2: 114 handlers (battle, Draw, Refill, `FinishTurn`, `StableRandom`).
  - E4: 12 resolvers and 17 kinds.
  - E8: 452 effect cases.
  - E9: 319 primitive methods.
  - B1: the Ten Thousand Temples honor-loss replay.
  - B3: the En'you entry cost.
  - B2 and B5: hand and province sight expiry.
  - B8: The Price of Honor.
  - E10: duel focus privacy.
  - X2: the `GoldWire` reflection test covers every wire field; `WireGameCommand` and `GameCommand` are at parity.
  - X4: 1,192 of 1,192 ability ids are handled.
  - E12: `DeckFileStore` is atomic, and offered-command edits touch only customisable fields.
  - T18: `RegressionAssertions` and `TestRunner` are sound.

### 7.2 Ledger

Every E, B, S, X and T slice finished (47 runs). The ledger rows and per-slice checked-and-handled lists are in the audit scratch notes.

Areas not covered, with the reason:
- S1 signature 4 (215 raw multiline hits) was sampled after it was found to match across method boundaries. The narrowed engine-field and query-surface subsets were clean.
- 145 of the 146 wide `[0]` hits were reasoned about via the command-field subset rather than read one by one.
- T1 checked about 1.9k lines of `Program.cs` by name and grep census, not line by line.
- T2 sampled 17 methods by their Accept/Reject ratio.
- B slices did not read the shared `DeciderChoice` and `CreatedCardSpec` machinery.
- The client was reviewed by code trace only.

### 7.3 Standing probes and unprobed pending kinds

P1–P4 ran over 13 factory fixtures, 14 Duel Demo scenarios, and AI-driven matches: starter, published and deck-search at seeds 1, 42, 831, 20260906 and 99999; four custom Crane/Lion pairs; the duel demos at seeds 1, 205 and 9876; and six showcases.

Headline output: `positions=8560 p1Diffs=0 p2Checked=6599 p2Rejected=5 p3IdLeaks=20180 p3NameHits=314 p4Checked=367 p4Diverged=0`.

- **P3 hits:** every one was a historical `provinceRevealed` event naming a card later shuffled back into its deck (public by rules), plus one Duel Demo description. None was a leak.
- **P2 hits:** 3 are AUD-03. The other 2 were a probe artifact: a gold-derived showcase target substitution in which the payment sources were computed for another target.
- **Pending kinds reached by the AI:** `discardHand`, `duelReaction`, `duelWonReaction`, `overconfidentDraw`, `proclaim`, `searchDeck`, `searchRegionProvince`, `searchSelect`, `shinden`. Every other kind (about 59 of 68) was never reached and is **unprobed** for replay parity. X1 assessed those by code and found no dependency outside `MatchState` and the command list.

### 7.4 Merged findings (undo a bad merge here)

| Merged finding | Source findings |
|---|---|
| AUD-01 | X2-01, X2-02 |
| AUD-02 | X5-01, X5-02 |
| AUD-03 | E10-01, X4-01 |
| AUD-09 | E4-01, E9-01 |
| AUD-15 | E5-03, E5-04 |
| AUD-20 | X1-01, T1-01, T4-01, T12-01 |
| AUD-21 | T2-01, T2-02, T3-01, T8-01 |
| AUD-25 | E6-01, T13-01, T6-01, T6-02, T6-03, T13-03, T10-01, B1-01, T16-01, T4-05 |
| AUD-26 | T0-04, T0-05, T0-06:398, T0-07, T4-02, T4-03, T9-01, T10-02, T12-02, T13-02, T18-01, T19-02, and `EmperorP5Tests.Zones.cs:226` |
| AUD-28 | T0-01, T7-01 |
| AUD-29 | T4-04, T5-01, T7-02, T8-02, T8-03, T10-03, T11-01, T14-01, T14-02 |
| AUD-30 | T0-02, T0-03 |
| AUD-32 | T0-08, T0-09, T0-10, T6-04 |

## 8. Verification and changes in this PR

### Trivial fixes (test files only; one commit each; 8 of the 10-fix cap)

Every fix passed the trivial-fix policy: it is confirmed, it changes at most 10 lines in at most 2 files, and it cites C8 or the rule that an assert must be able to fail. None changes engine, view, protocol, rules or content behaviour, and none touches a site #510 names.

| Finding | File | Change | Evidence |
|---|---|---|---|
| T0-01 | `EmperorRemainingFTests.cs` | removed the local non-atomic `Reject`; the call binds to the shared atomic helper | emperor-remaining-F 31/31 |
| T7-01 | `EmperorP5Tests.Hidden.cs` | the refusal check uses the fresh engine's own `Version`, with atomic `Reject` | emperor-p5/PettyReveal PASS |
| T0-05 | `BattleRulingsRound2Tests.cs` | removed `|| true` from OneSetThreeKinds | PASS |
| T0-07 | `EmperorPersonalityBatch6Tests.cs` | dropped `Eq(voidRing.Id, voidRing.Id);` | PASS |
| T4-02 | `EmperorRemainingBTests.cs` | assert the +2 Honor on the move-home branch | PASS |
| T4-03 | `EmperorRemainingCTests.cs` | assert the 3 Honor lost when an attachment is destroyed | PASS |
| T9-01 | `EmperorRulingR2Tests.cs` | removed `|| true` from HallsDefenderMovedHome | PASS |
| T13-02 | `SecondCityPassTests.cs` | dropped a redundant tautological check in EmotoDestroys | PASS |

One attempted fix was reverted because it failed: removing `|| true` at `BeforeTheDawnCardTests.cs:386`. That is now AUD-27. `EmperorRemainingFTests.cs` keeps a double blank line where the helper was removed; a follow-up edit to tidy it was not permitted in this session.

### Scratch repro results (re-run by the orchestrator at `830d2a4a`, never committed)

| Finding | Result |
|---|---|
| X2-01 | FAIL (bug), with the evidence test passing |
| X5-01 | FAIL (bug) |
| E5-03 | FAIL (bug) |
| E5-01 (server) | FAIL (bug) |
| E10-01 | 0/3 (bug) |
| X1-01 | 6/6 (coverage gap only) |
| T2-01 | 8/8 (coverage gap only) |
| E1-01 | 5/5 (coverage gap only) |
| E10-02 | 5/5 (coverage gap only) |
| S2-01 | 1/1 (refuted) |

### Checks

The `bash tools/check.sh --skip-client` result on the final branch is recorded in the PR description and the handoff. No client file changed, so no client build was needed.

### Files changed

`git diff origin/main --stat` lists only:
- `docs/audits/README.md`
- this report
- `docs/handoffs/tasks/claude-513-csharp-audit.json`
- the eight test files above

No `src/` file changed. No repro test, scratch file or `.jpg.import` file is committed.

## 9. Follow-up issues

See the "Follow-up issues" list added below after the issues were created.
