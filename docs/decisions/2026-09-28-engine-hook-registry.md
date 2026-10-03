# Feature-owned engine hooks — PR ladder Rung 4b

Status: proposed implementation in [PR #531](https://github.com/alfredoh7/l5r-emperor/pull/531), for [issue #509](https://github.com/alfredoh7/l5r-emperor/issues/509). Mechanics/shared lane; requested by Mendon. The card-wiring freeze remains until this PR merges.

Each feature registers its own typed callbacks in a module initializer. Initialization registers metadata only; the first engine construction seals all dispatch arrays. Catalogues may seal their own arrays earlier when first read. Registration order does not determine dispatch order: numeric priority ascending, then stable ID using ordinal comparison. Duplicate IDs and registration after sealing fail immediately. Arrays and metadata are built once; the command/query loops use delegates and readonly value contexts, without reflection or LINQ. Reflection is used only by the test that compares the frozen legacy order.

Legacy callbacks retain their exact old positions below. A new feature uses a unique ID and an explicit priority of at least 10000, after the reserved legacy priorities. No central registration list is edited to add a feature to an existing channel. Abilities, looks and searches construct their catalogues only when read; their extension factories preserve base-first order and dictionary duplicate failures.

First-answer channels stop at the first non-null result, including an empty candidate list. Predicates stop at the first true result. Force and province bonuses sum every callback. Validation channels invoke every callback at the same three checkpoints; intervening special-case validations remain in place. RunEffects stops at the first non-null result: false means handled, true means handled with a pending window. Stronghold batch 1 deliberately translates any non-null result to false, preserving the old caller's discarded result. PendingChoice stops at the first handler returning true; its old generic fallback and surrounding cost/duel gates stay in place.

Either/Or and decider core switch arms remain first. Extension defaults preserve their old feature order. Unknown RemainingB performer IDs now return null so new handlers can run; the old `[null]` fallback is applied after the registry. RemainingG Either/Or extensions are guarded by the original known spec ID so unknown IDs still reach the same final exception after future hooks. Unknown decider application still proceeds with the old no-op/default continuation.

## State and save compatibility

MatchEngine.Copy uses System.Text.Json's default serializer for detached snapshots, command rollback, and suspended state. RoomStore serializes PersistedRoom using Protocol.Json; the persisted payload holds ReplayRecord (seed, scenario, commands and optional custom decks), and RoomManager restores it with MatchEngine.Replay. These formats and their version checks remain unchanged. Server persistence/replay tests and the Client.Transport suite validate those paths; the old raw-state fixtures additionally cover the properties moved into partial feature files.

MatchState is partial. Seven existing tail container properties moved into their owning feature files with their original names, types, defaults, and explicit JsonPropertyOrder 1001–1007. All older scalar properties remain in Models.cs. New features can add a partial MatchState property in their own file, with a unique explicit order above 1007. They must classify new Pending properties with DecisionState or NonDecisionState, using nameof(MatchState.Property); decision predicates participate in Seppuku timing. The original pinned Seppuku guard tests were not rewritten.

Fixtures were captured from pre-refactor main aeee1097 (merged #530). `rung4b-legacy-state.json` contains populated feature containers and pending fields. `rung4b-legacy-resume.json.gz` contains a real seed-831 DeckSearchShowcase save paused at searchDeck, its chooseSearchDeck command, and the old engine's resulting state. Tests load and reserialize old fields in their original order, then execute that captured command and compare the old resulting fields exactly. Projection lets a future additive root property coexist with the unchanged historical fixture. The gzip header uses mtime zero. The frozen hook mapping is independently SHA-pinned and checks only legacy IDs, allowing future registrations without changing the shared test.

## Explicit remainders

The large base per-card switches, curated base ability ID lists, individual switch cases that directly call effect helpers, older scalar state fields, and specialized lifecycle/event/watch flows remain compatibility code. Generic pending-choice wrappers whose implementation remains in the core live in PendingCoreHooks.cs. Nested dispatch helpers inside a feature were not recursively generalized. Removing these would require separate semantic work; this migration does not claim to eliminate every central rules switch. New mechanisms can use the registered generic extension paths and feature-owned state without editing a shared chain.

The temporary-repository mechanism test makes two branches from one base, adds one independent C# feature file per branch, merges them, builds the real core, executes both abilities in priority order, and round-trips their state. It checks no conflicts, no shared-file edits, and unchanged card catalogue data.

## Exact legacy mapping

Validation: full `tools/check.sh --skip-client` passed before and after. Core 2109 → 2114 (five added registry/compatibility checks), AI 74 → 74, Server 8 → 8, Client.Transport 13 → 13, Python content 138 → 139. Inventory remains 2183 cards / 1419 wired; workbook entries match apart from generated timestamps. All six ordered fingerprints and 19 printed same-seed AI outcomes match. AI wall time was 338.8s before and 277.1s after; the standalone baseline overlapped a Core run, so these observations are not a controlled benchmark. The independent mechanism branches merged without conflicts and both executed. Godot client build/import/smoke remains for CI.

Priority is the explicit equivalent of the old left-to-right position within each channel. StateJSON priorities are original JSON tail order, not callback execution order.

| Channel | Priority / old position | Legacy callback or property | Feature file |
|---|---:|---|---|
| Abilities | 100 | `AddEmperorPersonalityBatch2` | `EmperorPersonalityBatch2.cs` |
| Abilities | 200 | `AddEmperorStrongholdBatch1` | `EmperorStrongholdBatch1.cs` |
| Abilities | 300 | `AddEmperorStrongholdBatch3` | `EmperorStrongholdBatch3.cs` |
| Abilities | 400 | `AddEmperorRemainingA` | `EmperorRemainingA.cs` |
| Abilities | 500 | `AddEmperorRemainingB` | `EmperorRemainingB.cs` |
| Abilities | 600 | `AddEmperorRemainingC` | `EmperorRemainingC.cs` |
| Abilities | 700 | `AddEmperorRemainingD` | `EmperorRemainingD.cs` |
| Abilities | 800 | `AddEmperorRemainingE` | `EmperorRemainingE.cs` |
| Abilities | 900 | `AddEmperorRemainingF` | `EmperorRemainingF.cs` |
| Abilities | 1000 | `AddEmperorRemainingG` | `EmperorRemainingG.cs` |
| Abilities | 1100 | `AddEmperorProxyX` | `EmperorProxyX.cs` |
| Abilities | 1200 | `AddEmperorProxyY` | `EmperorProxyY.cs` |
| Abilities | 1300 | `AddEmperorProxyZ` | `EmperorProxyZ.cs` |
| Abilities | 1400 | `AddEmperorRulingR1` | `EmperorRulingR1Cards.cs` |
| Abilities | 1500 | `AddEmperorRulingR2` | `EmperorRulingR2Cards.cs` |
| Abilities | 1600 | `AddEmperorRulingR3` | `EmperorRulingR3.cs` |
| Abilities | 1700 | `AddEmperorThreeS` | `EmperorThreeS.cs` |
| Abilities | 1800 | `AddEmperorFollowups` | `EmperorFollowups.cs` |
| Abilities | 1900 | `AddEmperorMechB` | `EmperorMechB.cs` |
| Abilities | 2000 | `AddEmperorMechA` | `EmperorMechA.cs` |
| Abilities | 2100 | `AddEmperorMechC` | `EmperorMechC.cs` |
| Abilities | 2200 | `AddEmperorMechE` | `EmperorMechE.cs` |
| Abilities | 2300 | `AddEmperorMechD` | `EmperorMechD.cs` |
| Abilities | 2400 | `AddEmperorP1` | `EmperorP1.cs` |
| Abilities | 2500 | `AddEmperorP2` | `EmperorP2.cs` |
| Abilities | 2600 | `AddEmperorP4` | `EmperorP4.cs` |
| Abilities | 2700 | `AddEmperorP4Watches` | `EmperorP4Watches.cs` |
| Abilities | 2800 | `AddEmperorP4Units` | `EmperorP4Units.cs` |
| Abilities | 2900 | `AddEmperorP4More` | `EmperorP4More.cs` |
| Abilities | 3000 | `AddEmperorP5` | `EmperorP5.cs` |
| Abilities | 3100 | `AddEmperorP6` | `EmperorP6.cs` |
| Abilities | 3200 | `AddEmperorP3` | `EmperorP3.cs` |
| Abilities | 3300 | `AddEmperorP3Priority` | `EmperorP3Priority.cs` |
| Abilities | 3400 | `AddEmperorP3Entries` | `EmperorP3Entries.cs` |
| ApplyDecider | 100 | `ApplyEmperorRemainingBDeciderChoice` | `EmperorRemainingB.cs` |
| ApplyDecider | 200 | `ApplyMechDDeciderChoice` | `EmperorMechD.cs` |
| ApplyDecider | 300 | `ApplyKeireiPoison` | `EmperorP2Keirei.cs` |
| ApplyDecider | 400 | `ApplyP5DeciderChoice` | `EmperorP5Zones.cs` |
| ApplyEitherOr | 100 | `ApplyEmperorP1EitherOr` | `EmperorP1.cs` |
| ApplyEitherOr | 200 | `ApplyP5EitherOr` | `EmperorP5Zones.cs` |
| ApplyEitherOr | 300 | `ApplyEmperorP6EitherOr` | `EmperorP6.cs` |
| ApplyEitherOr | 400 | `ApplyRemainingGEitherOr` | `EmperorRemainingG.cs` |
| AttackKind | 100 | `MechCAttackKind` | `EmperorMechC.cs` |
| AttackKind | 200 | `EmperorP1AttackKind` | `EmperorP1.cs` |
| AttackKind | 300 | `P2AttackKind` | `EmperorP2.cs` |
| AttackKind | 400 | `P4AttackKind` | `EmperorP4.cs` |
| AttackKind | 500 | `P5AttackKind` | `EmperorP5.cs` |
| AttackKind | 600 | `EmperorRemainingEAttackKind` | `EmperorRemainingE.cs` |
| AttackKind | 700 | `RemainingGAttackKind` | `EmperorRemainingG.cs` |
| AttackStrengthAfter | 100 | `MechCAttackStrength` | `EmperorMechC.cs` |
| AttackStrengthAfter | 200 | `EmperorP1AttackStrength` | `EmperorP1.cs` |
| AttackStrengthAfter | 300 | `P2AttackStrength` | `EmperorP2.cs` |
| AttackStrengthAfter | 400 | `P4AttackStrength` | `EmperorP4.cs` |
| AttackStrengthBefore | 100 | `P3AttackStrength` | `EmperorP3Priority.cs` |
| AttackStrengthBefore | 200 | `EmperorRemainingEAttackStrength` | `EmperorRemainingE.cs` |
| AttackStrengthBefore | 300 | `EmperorRemainingGAttackStrength` | `EmperorRemainingG.cs` |
| AttackStrengthBefore | 400 | `ShadowsEmbraceBatch3AttackStrength` | `ShadowsEmbraceBatch3.cs` |
| AttackStrengthBefore | 500 | `ForgottenLegacyBatch5AttackStrength` | `ForgottenLegacyBatch5.cs` |
| AttackStrengthBefore | 600 | `EmperorPersonalityBatch1AttackStrength` | `EmperorPersonalityBatch1.cs` |
| AttackStrengthBefore | 700 | `ShadowsEmbraceBatch2AttackStrength` | `ShadowsEmbraceBatch2.cs` |
| AttackStrengthBefore | 800 | `RemAAttackStrength` | `EmperorRemainingA.cs` |
| AttackStrengthBefore | 900 | `P5AttackStrength` | `EmperorP5.cs` |
| BowsTarget | 100 | `RemABowsTarget` | `EmperorRemainingA.cs` |
| BowsTarget | 200 | `P5BowsTarget` | `EmperorP5.cs` |
| BowsTarget | 300 | `RulingR2BowsTarget` | `EmperorRulingR2Cards.cs` |
| BowsTarget | 400 | `MechCBowsTarget` | `EmperorMechC.cs` |
| BowsTarget | 500 | `P2BowsTarget` | `EmperorP2.cs` |
| BowsTarget | 600 | `MechEHostsBattleBows` | `EmperorMechE.cs` |
| BowsTarget | 700 | `P4BowsTarget` | `EmperorP4.cs` |
| DeciderIdAfter | 100 | `IsFirstDecider` | `EmperorRemainingB.cs` |
| DeciderIdAfter | 200 | `KeireiDeciderOptions` | `EmperorP2Keirei.cs` |
| DeciderIdBefore | 100 | `P5DeciderId` | `EmperorP5Zones.cs` |
| DeciderIdBefore | 200 | `MechDDeciderId` | `EmperorMechD.cs` |
| DeciderOptions | 100 | `EmperorRemainingBDeciderOptions` | `EmperorRemainingB.cs` |
| DeciderOptions | 200 | `MechDDeciderOptions` | `EmperorMechD.cs` |
| DeciderOptions | 300 | `KeireiDeciderOptions` | `EmperorP2Keirei.cs` |
| DeciderOptions | 400 | `P5DeciderOptions` | `EmperorP5Zones.cs` |
| DeciderPrompt | 100 | `EmperorRemainingBDeciderPrompt` | `EmperorRemainingB.cs` |
| DeciderPrompt | 200 | `MechDDeciderPrompt` | `EmperorMechD.cs` |
| DeciderPrompt | 300 | `KeireiDeciderPrompt` | `EmperorP2Keirei.cs` |
| DeciderPrompt | 400 | `P5DeciderPrompt` | `EmperorP5Zones.cs` |
| DeciderVerb | 100 | `MechDDeciderVerb` | `EmperorMechD.cs` |
| DeciderVerb | 200 | `KeireiDeciderPrompt` | `EmperorP2Keirei.cs` |
| DeciderVerb | 300 | `P5DeciderVerb` | `EmperorP5Zones.cs` |
| DestroysTarget | 100 | `P5DestroysTarget` | `EmperorP5.cs` |
| DestroysTarget | 200 | `RulingR2DestroysTarget` | `EmperorRulingR2Cards.cs` |
| DestroysTarget | 300 | `MechCDestroysTarget` | `EmperorMechC.cs` |
| DestroysTarget | 400 | `MechEDestroysTarget` | `EmperorMechE.cs` |
| DestroysTarget | 500 | `P4DestroysTarget` | `EmperorP4.cs` |
| EitherOrOptions | 100 | `EmperorP1EitherOrOptions` | `EmperorP1.cs` |
| EitherOrOptions | 200 | `P5EitherOrOptions` | `EmperorP5Zones.cs` |
| EitherOrOptions | 300 | `EmperorP6EitherOrOptions` | `EmperorP6.cs` |
| EitherOrOptions | 400 | `RemainingGEitherOrOptions` | `EmperorRemainingG.cs` |
| EitherOrPrompt | 100 | `EmperorP1EitherOrPrompt` | `EmperorP1.cs` |
| EitherOrPrompt | 200 | `P5EitherOrPrompt` | `EmperorP5Zones.cs` |
| EitherOrPrompt | 300 | `EmperorP6EitherOrPrompt` | `EmperorP6.cs` |
| EitherOrPrompt | 400 | `RemainingGEitherOrPrompt` | `EmperorRemainingG.cs` |
| FinishEitherOr | 100 | `FinishEmperorRemainingDEitherOr` | `EmperorRemainingD.cs` |
| FinishEitherOr | 200 | `FinishShadowsEmbraceEitherOr` | `ShadowsEmbraceBatch3.cs` |
| FinishEitherOr | 300 | `FinishRemainingGEitherOr` | `EmperorRemainingG.cs` |
| FinishEitherOr | 400 | `FinishEmperorP1EitherOr` | `EmperorP1.cs` |
| FinishEitherOr | 500 | `FinishP5EitherOr` | `EmperorP5Zones.cs` |
| Looks | 100 | `EmperorRemainingFLooks` | `EmperorRemainingF.cs` |
| Looks | 200 | `EmperorP1Looks` | `EmperorP1.cs` |
| Looks | 300 | `EmperorP2Searches` | `EmperorP2Search.cs` |
| Looks | 400 | `EmperorP5Looks` | `EmperorP5Hidden.cs` |
| Looks | 500 | `EmperorP3Looks` | `EmperorP3.cs` |
| PendingChoice | 100 | `HandleRegisteredSearchChoice` | `DeckSearch.cs` |
| PendingChoice | 200 | `HandleRegisteredAttackTargetSelect` | `EmperorFollowupsAttack.cs` |
| PendingChoice | 300 | `HandleRegisteredLookChoice` | `DeckLook.cs` |
| PendingChoice | 400 | `HandleRegisteredCopyChoice` | `CardCopy.cs` |
| PendingChoice | 500 | `HandleRegisteredEitherOr` | `EitherOr.cs` |
| PendingChoice | 600 | `HandleRegisteredOpponentPayment` | `OpponentPayment.cs` |
| PendingChoice | 700 | `HandleRegisteredDeciderChoice` | `DeciderChoice.cs` |
| PendingChoice | 800 | `HandleRegisteredFocusEffectChoice` | `FocusEffectChoice.cs` |
| PendingChoice | 900 | `HandleRegisteredSequencedTarget` | `SequencedTarget.cs` |
| PendingChoice | 1000 | `HandleRegisteredShinden` | `PendingCoreHooks.cs` |
| PendingChoice | 1100 | `HandleRegisteredPreservingHonor` | `PendingCoreHooks.cs` |
| PendingChoice | 1200 | `HandleRegisteredRikateDetach` | `PendingCoreHooks.cs` |
| PendingChoice | 1300 | `HandleRegisteredRinShame` | `PendingCoreHooks.cs` |
| PendingChoice | 1400 | `HandleRegisteredYourClanNeedsYouProvince` | `PendingCoreHooks.cs` |
| PendingChoice | 1500 | `HandleRegisteredDeclineStraighten` | `PendingCoreHooks.cs` |
| PendingChoice | 1600 | `HandleRegisteredRentatsuLoss` | `EmperorPersonalityBatch4.cs` |
| PendingChoice | 1700 | `HandleRegisteredP5BlessingsTop` | `EmperorP5Zones.cs` |
| PendingChoice | 1800 | `HandleRegisteredInfiltratorStraighten` | `EmperorMechB.cs` |
| PendingChoice | 1900 | `HandleRegisteredP3DarkVirtueDiscard` | `EmperorP3.cs` |
| PendingChoice | 2000 | `HandleRegisteredP3BeforeEventReaction` | `EmperorP3Windows.cs` |
| PendingChoice | 2100 | `HandleRegisteredP3MikiDarkVirtue` | `EmperorP3Windows.cs` |
| PendingChoice | 2200 | `HandleRegisteredP3AroruMove` | `EmperorP3Priority.cs` |
| PendingChoice | 2300 | `HandleRegisteredP3EntryReaction` | `EmperorP3Entries.cs` |
| PendingChoice | 2400 | `HandleRegisteredConqueror` | `PendingCoreHooks.cs` |
| PendingChoice | 2500 | `HandleRegisteredBeforeBattleOrder` | `PendingCoreHooks.cs` |
| PendingChoice | 2600 | `HandleRegisteredBeforeBattleOptional` | `PendingCoreHooks.cs` |
| PendingChoice | 2700 | `HandleRegisteredBeforeBattleReaction` | `BeforeResolution.cs` |
| PendingChoice | 2800 | `HandleRegisteredEngageReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 2900 | `HandleRegisteredEncircledOrder` | `PendingCoreHooks.cs` |
| PendingChoice | 3000 | `HandleRegisteredEncircledSelect` | `PendingCoreHooks.cs` |
| PendingChoice | 3100 | `HandleRegisteredTwistOfFateReaction` | `EmperorRemainingD.cs` |
| PendingChoice | 3200 | `HandleRegisteredKillingGroundsSelect` | `EmperorRemainingD.cs` |
| PendingChoice | 3300 | `HandleRegisteredBrashDraw` | `PendingCoreHooks.cs` |
| PendingChoice | 3400 | `HandleRegisteredOverconfidentDraw` | `PendingCoreHooks.cs` |
| PendingChoice | 3500 | `HandleRegisteredHarshChoices` | `PendingCoreHooks.cs` |
| PendingChoice | 3600 | `HandleRegisteredOfferingReverence` | `SeedsOfDecayBatch4.cs` |
| PendingChoice | 3700 | `HandleRegisteredStarCrossedLovers` | `ShadowsEmbraceBatch6.cs` |
| PendingChoice | 3800 | `HandleRegisteredEventChoice` | `EmperorRemainingFEvents.cs` |
| PendingChoice | 3900 | `HandleRegisteredTestedLandProvince` | `SeedsOfDecayBatch4.cs` |
| PendingChoice | 4000 | `HandleRegisteredForcedDiscard` | `PendingCoreHooks.cs` |
| PendingChoice | 4100 | `HandleRegisteredEffectChoice` | `EffectChoice.cs` |
| PendingChoice | 4200 | `HandleRegisteredProclaimNegateReaction` | `SeedsOfDecayBatch2.cs` |
| PendingChoice | 4300 | `HandleRegisteredProclaim` | `PendingCoreHooks.cs` |
| PendingChoice | 4400 | `HandleRegisteredBowReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 4500 | `HandleRegisteredMoveReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 4600 | `HandleRegisteredNikutaiReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 4700 | `HandleRegisteredEntryReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 4800 | `HandleRegisteredAttackReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 4900 | `HandleRegisteredAdditionalActionReaction` | `ActionNegation.cs` |
| PendingChoice | 5000 | `HandleRegisteredBeforeActionReaction` | `EmperorRulingR4Honor.cs` |
| PendingChoice | 5100 | `HandleRegisteredHonorChangeReaction` | `EmperorRulingR4Honor.cs` |
| PendingChoice | 5200 | `HandleRegisteredUnimpeachableNameReaction` | `ActionNegation.cs` |
| PendingChoice | 5300 | `HandleRegisteredDuelReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 5400 | `HandleRegisteredAfterResolutionReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 5500 | `HandleRegisteredAttackPhaseEndReaction` | `EmperorRemainingC.cs` |
| PendingChoice | 5600 | `HandleRegisteredAfterActionResolvedReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 5700 | `HandleRegisteredBattleRecruit` | `PendingCoreHooks.cs` |
| PendingChoice | 5800 | `HandleRegisteredDuelWonReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 5900 | `HandleRegisteredBattleActionReaction` | `PendingCoreHooks.cs` |
| PendingChoice | 6000 | `HandleRegisteredAnnounceReaction` | `ActionNegation.cs` |
| PerformerAllows | 100 | `RemAPerformerAllows` | `EmperorRemainingA.cs` |
| PerformerAllows | 200 | `EmperorRemainingBPerformerAllows` | `EmperorRemainingB.cs` |
| PerformerAllows | 300 | `RemainingDPerformerAllows` | `EmperorRemainingD.cs` |
| PerformerAllows | 400 | `RulingR2PerformerAllows` | `EmperorRulingR2Cards.cs` |
| PerformerAllows | 500 | `ThreeSPerformerAllows` | `EmperorThreeS.cs` |
| PerformerAllows | 600 | `MechEPerformerAllows` | `EmperorMechE.cs` |
| PerformerAllows | 700 | `P5PerformerAllows` | `EmperorP5.cs` |
| PerformerAllows | 800 | `EmperorP4PerformerAllows` | `EmperorP4.cs` |
| PerformerCandidates | 100 | `P5PerformerCandidates` | `EmperorP5.cs` |
| PerformerCandidates | 200 | `RemAPerformerCandidates` | `EmperorRemainingA.cs` |
| PerformerCandidates | 300 | `RemainingDPerformers` | `EmperorRemainingD.cs` |
| PerformerCandidates | 400 | `RulingR2Performers` | `EmperorRulingR2Cards.cs` |
| PerformerCandidates | 500 | `ThreeSPerformers` | `EmperorThreeS.cs` |
| PerformerCandidates | 600 | `MechEPerformers` | `EmperorMechE.cs` |
| PerformerCandidates | 700 | `EmperorP4Performers` | `EmperorP4.cs` |
| PerformerCandidates | 800 | `EmperorRemainingBPerformers` | `EmperorRemainingB.cs` |
| PersonalityForce | 100 | `GinasFoxForce` | `EmperorProxyY.cs` |
| PersonalityForce | 200 | `EmperorRemainingEForce` | `EmperorRemainingE.cs` |
| PersonalityForce | 300 | `ThreeSForce` | `EmperorThreeS.cs` |
| PersonalityForce | 400 | `EmperorP1Force` | `EmperorP1.cs` |
| PersonalityForce | 500 | `P3Force` | `EmperorP3Priority.cs` |
| PersonalityForce | 600 | `P5Force` | `EmperorP5.cs` |
| PersonalityForce | 700 | `P2Force` | `EmperorP2.cs` |
| PersonalityForce | 800 | `ForgottenLegacyBatch5Force` | `ForgottenLegacyBatch5.cs` |
| PersonalityForce | 900 | `EmperorBatch5Force` | `EmperorPersonalityBatch5.cs` |
| PersonalityForce | 1000 | `EmperorPersonalityBatch3Force` | `EmperorPersonalityBatch3.cs` |
| PersonalityForce | 1100 | `ShadowsEmbraceBatch5Force` | `ShadowsEmbraceBatch5.cs` |
| PersonalityForce | 1200 | `ShadowsEmbraceBatch2Force` | `ShadowsEmbraceBatch2.cs` |
| PersonalityForce | 1300 | `ShadowsEmbraceBatch1Force` | `ShadowsEmbraceBatch1.cs` |
| PersonalityForce | 1400 | `EmperorRemainingGForce` | `EmperorRemainingG.cs` |
| ProvinceBonus | 100 | `P4StaticProvinceBonus` | `EmperorP4Watches.cs` |
| ProvinceBonus | 200 | `P3StaticProvinceBonus` | `EmperorP3.cs` |
| RunEffects | 100 | `RunEmperorRemainingA` | `EmperorRemainingA.cs` |
| RunEffects | 200 | `RunEmperorRemainingC` | `EmperorRemainingC.cs` |
| RunEffects | 300 | `RunEmperorRemainingE` | `EmperorRemainingE.cs` |
| RunEffects | 400 | `RunEmperorStrongholdBatch1` | `EmperorStrongholdBatch1.cs` |
| RunEffects | 500 | `RunEmperorStrongholdBatch3` | `EmperorStrongholdBatch3.cs` |
| RunEffects | 600 | `RunShadowsEmbraceBatch6` | `ShadowsEmbraceBatch6.cs` |
| RunEffects | 700 | `RunShadowsEmbraceBatch3` | `ShadowsEmbraceBatch3.cs` |
| RunEffects | 800 | `RunShadowsEmbraceBatch4` | `ShadowsEmbraceBatch4.cs` |
| RunEffects | 900 | `RunForgottenLegacyBatch5` | `ForgottenLegacyBatch5.cs` |
| RunEffects | 1000 | `RunShadowsEmbraceBatch5` | `ShadowsEmbraceBatch5.cs` |
| RunEffects | 1100 | `RunEmperorRemainingB` | `EmperorRemainingB.cs` |
| RunEffects | 1200 | `RunEmperorRemainingD` | `EmperorRemainingD.cs` |
| RunEffects | 1300 | `RunEmperorRulingR2` | `EmperorRulingR2Cards.cs` |
| RunEffects | 1400 | `RunEmperorP1` | `EmperorP1.cs` |
| RunEffects | 1500 | `RunEmperorThreeS` | `EmperorThreeS.cs` |
| RunEffects | 1600 | `RunEmperorMechA` | `EmperorMechA.cs` |
| RunEffects | 1700 | `RunEmperorFollowups` | `EmperorFollowups.cs` |
| RunEffects | 1800 | `RunEmperorMechB` | `EmperorMechB.cs` |
| RunEffects | 1900 | `RunEmperorMechC` | `EmperorMechC.cs` |
| RunEffects | 2000 | `RunEmperorMechE` | `EmperorMechE.cs` |
| RunEffects | 2100 | `RunEmperorP4` | `EmperorP4.cs` |
| RunEffects | 2200 | `RunEmperorP5` | `EmperorP5.cs` |
| RunEffects | 2300 | `RunEmperorMechD` | `EmperorMechD.cs` |
| RunEffects | 2400 | `RunEmperorP2` | `EmperorP2.cs` |
| RunEffects | 2500 | `RunEmperorP6` | `EmperorP6.cs` |
| RunEffects | 2600 | `RunEmperorP3` | `EmperorP3.cs` |
| Searches | 100 | `EmperorRemainingESearches` | `EmperorRemainingE.cs` |
| Searches | 200 | `EmperorFollowupsSearches` | `EmperorFollowupsSearch.cs` |
| Searches | 300 | `EmperorP2Searches` | `EmperorP2Search.cs` |
| Searches | 400 | `EmperorP6Searches` | `EmperorP6.cs` |
| Searches | 500 | `EmperorP3Abilities` | `EmperorP3.cs` |
| StateJSON | 1001 | `MechD` | `EmperorMechD.cs` |
| StateJSON | 1002 | `P1` | `EmperorP1.cs` |
| StateJSON | 1003 | `P2` | `EmperorP2.cs` |
| StateJSON | 1004 | `P4` | `EmperorP4.cs` |
| StateJSON | 1005 | `P5` | `EmperorP5.cs` |
| StateJSON | 1006 | `P6` | `EmperorP6.cs` |
| StateJSON | 1007 | `P3` | `EmperorP3.cs` |
| TargetAllows | 100 | `ShadowsEmbraceBatch2TargetAllows` | `ShadowsEmbraceBatch2.cs` |
| TargetAllows | 200 | `EmperorRemainingCTargetAllows` | `EmperorRemainingC.cs` |
| TargetAllows | 300 | `RemainingDTargetAllows` | `EmperorRemainingD.cs` |
| TargetAllows | 400 | `EmperorRemainingETargetAllows` | `EmperorRemainingE.cs` |
| TargetAllows | 500 | `EmperorProxyXTargetAllows` | `EmperorProxyX.cs` |
| TargetAllows | 600 | `RulingR1TargetAllows` | `EmperorRulingR1Cards.cs` |
| TargetAllows | 700 | `RulingR2TargetAllows` | `EmperorRulingR2Cards.cs` |
| TargetAllows | 800 | `ThreeSTargetAllows` | `EmperorThreeS.cs` |
| TargetAllows | 900 | `FollowupsTargetAllows` | `EmperorFollowups.cs` |
| TargetAllows | 1000 | `MechBTargetAllows` | `EmperorMechB.cs` |
| TargetAllows | 1100 | `EmperorMechCTargetAllows` | `EmperorMechC.cs` |
| TargetAllows | 1200 | `EmperorP1TargetAllows` | `EmperorP1.cs` |
| TargetAllows | 1300 | `EmperorP2TargetAllows` | `EmperorP2.cs` |
| TargetAllows | 1400 | `EmperorP4TargetAllows` | `EmperorP4.cs` |
| TargetAllows | 1500 | `P5TargetAllows` | `EmperorP5.cs` |
| TargetAllows | 1600 | `EmperorP6TargetAllows` | `EmperorP6.cs` |
| TargetAllows | 1700 | `P3TargetAllows` | `EmperorP3.cs` |
| TargetedReaction | 100 | `RemATargetedActionReactionApplies` | `EmperorRemainingA.cs` |
| TargetedReaction | 200 | `EmperorRemainingCTargetedReactionApplies` | `EmperorRemainingC.cs` |
| TargetedReaction | 300 | `RulingR2TargetedReactionApplies` | `EmperorRulingR2Cards.cs` |
| TargetedReaction | 400 | `MechBTargetedReactionApplies` | `EmperorMechB.cs` |
| TargetedReaction | 500 | `MechCTargetedReactionApplies` | `EmperorMechC.cs` |
| TargetedReaction | 600 | `MechETargetedReactionApplies` | `EmperorMechE.cs` |
| TargetedReaction | 700 | `P4TargetedReactionApplies` | `EmperorP4Watches.cs` |
| TargetedReaction | 800 | `P5TargetedReactionApplies` | `EmperorP5.cs` |
| TargetedReaction | 900 | `P3TargetedReactionApplies` | `EmperorP3.cs` |
| ValidateEarly | 100 | `ValidateForgottenLegacyBatch5` | `ForgottenLegacyBatch5.cs` |
| ValidateEarly | 200 | `ValidateShadowsEmbraceBatch5` | `ShadowsEmbraceBatch5.cs` |
| ValidateEarly | 300 | `ValidateShadowsEmbraceBatch6` | `ShadowsEmbraceBatch6.cs` |
| ValidateEarly | 400 | `ValidateEmperorStrongholdBatch1` | `EmperorStrongholdBatch1.cs` |
| ValidateEarly | 500 | `ValidateEmperorStrongholdBatch3` | `EmperorStrongholdBatch3.cs` |
| ValidateEarly | 600 | `ValidateEmperorPersonalityBatch2` | `EmperorPersonalityBatch2.cs` |
| ValidateEarly | 700 | `ValidateEmperorBatch4` | `EmperorPersonalityBatch4.cs` |
| ValidateEarly | 800 | `ValidateEmperorRemainingB` | `EmperorRemainingB.cs` |
| ValidateEarly | 900 | `ValidateEmperorRemainingE` | `EmperorRemainingE.cs` |
| ValidateEarly | 1000 | `ValidateEmperorRulingR1` | `EmperorRulingR1Cards.cs` |
| ValidateEarly | 1100 | `ValidateEmperorRulingR5` | `EmperorRulingR5.cs` |
| ValidateEarly | 1200 | `ValidateEmperorP4` | `EmperorP4.cs` |
| ValidateEarly | 1300 | `ValidateEmperorMechC` | `EmperorMechC.cs` |
| ValidateLate | 100 | `ValidateSeedsOfDecayBatch3` | `SeedsOfDecayBatch3.cs` |
| ValidateLate | 200 | `ValidateForgottenLegacyBatch1` | `ForgottenLegacyBatch1.cs` |
| ValidateLate | 300 | `ValidateShadowsEmbraceBatch2` | `ShadowsEmbraceBatch2.cs` |
| ValidateLate | 400 | `ValidateShadowsEmbraceBatch1` | `ShadowsEmbraceBatch1.cs` |
| ValidateLate | 500 | `ValidateShadowsEmbraceBatch3` | `ShadowsEmbraceBatch3.cs` |
| ValidateLate | 600 | `ValidateEmperorPersonalityBatch3` | `EmperorPersonalityBatch3.cs` |
| ValidateLate | 700 | `ValidateEmperorBatch5` | `EmperorPersonalityBatch5.cs` |
| ValidateLate | 800 | `ValidateEmperorBatch6` | `EmperorPersonalityBatch6.cs` |
| ValidateLate | 900 | `ValidateEmperorRemainingF` | `EmperorRemainingF.cs` |
| ValidateLate | 1000 | `ValidateEmperorRemainingG` | `EmperorRemainingG.cs` |
| ValidateLate | 1100 | `ValidateEmperorP3` | `EmperorP3.cs` |
| ValidateLate | 1200 | `ValidateEmperorProxyX` | `EmperorProxyX.cs` |
| ValidateMiddle | 100 | `ValidateForgottenLegacyBatch4` | `ForgottenLegacyBatch4.cs` |
| ValidateMiddle | 200 | `ValidateShadowsEmbraceBatch4` | `ShadowsEmbraceBatch4.cs` |
| ValidateMiddle | 300 | `ValidateEmperorRemainingA` | `EmperorRemainingA.cs` |
| ValidateMiddle | 400 | `ValidateEmperorRemainingC` | `EmperorRemainingC.cs` |
| ValidateMiddle | 500 | `ValidateEmperorRemainingD` | `EmperorRemainingD.cs` |
| ValidateMiddle | 600 | `ValidateEmperorRulingR2` | `EmperorRulingR2Cards.cs` |
| ValidateMiddle | 700 | `ValidateEmperorThreeS` | `EmperorThreeS.cs` |
| ValidateMiddle | 800 | `ValidateEmperorMechB` | `EmperorMechB.cs` |
| ValidateMiddle | 900 | `ValidateEmperorMechA` | `EmperorMechA.cs` |
| ValidateMiddle | 1000 | `ValidateEmperorMechE` | `EmperorMechE.cs` |
| ValidateMiddle | 1100 | `ValidateEmperorMechD` | `EmperorMechD.cs` |
| ValidateMiddle | 1200 | `ValidateEmperorP1` | `EmperorP1.cs` |
| ValidateMiddle | 1300 | `ValidateEmperorP2` | `EmperorP2.cs` |
| ValidateMiddle | 1400 | `ValidateEmperorP5` | `EmperorP5.cs` |
| ValidateMiddle | 1500 | `ValidateEmperorP6` | `EmperorP6.cs` |
