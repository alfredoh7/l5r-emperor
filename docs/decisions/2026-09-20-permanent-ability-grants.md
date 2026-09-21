# Permanent predefined ability grants

Issue #386 / PR #388. Requested by Mendon; extends #338's existing `GrantedAbility` model.

`GrantAbility` attaches a catalogue ability ID to one `CardInstance`. It does not edit a shared card definition or parse rules text. The printed/granted distinction is retained. Copying calls the same helper; its independent choice and replacement rules remain in `CardCopy.cs`.

The first published consumer is **Spirit of the Monk**, Second City 138 (`oracle-l5r-7424`, Strategy wave 30). The receipt-backed scan was visually checked: 0 Gold, Focus 4. Its Limited action chooses an unbowed performing Personality. A Monk permanently gains `spirit-monk-bow`; otherwise the performer permanently gains Monk. The granted Battle action targets an enemy Personality without an Armor attachment, bowed or unbowed, at the current battlefield, and bows him through the existing bow-negation path. The Strategy then enters the discard pile. The pinned Lion/Crane decks are unchanged.

## Duration and usage

- Permanent grants survive death and turn end, following Mendon's existing #351 ruling and the existing overlay/deck-reset lifecycle.
- Turn grants expire at turn end; Battle grants expire as the battle finishes, before another battlefield is chosen (also cleared at turn end).
- Attachment grants end when the granting attachment leaves the host. Unit destruction must perform the same cleanup as removing an attachment individually.
- Copy grants retain their existing replacement/removal semantics.
- Keep different grant sources/durations independently. A permanent grant must not disappear because the same ability was already granted temporarily.
- Effective ability IDs remain distinct, following #338's accepted usage model. Duplicate grants do not supply extra action uses. Usage belongs to the receiver and the catalogue's `UsageKey`, including shared alternatives, finite limits and unlimited abilities. Granting again never clears a usage stamp. The next turn restores the normal limit.

`AbilityIdsOf` excludes transient grants on cards outside play, matching the existing copy-source filter. This prevents stale turn/attachment abilities from appearing as active on dead cards. Existing action and in-play Reaction discovery/validation read this list. Attack-Reaction discovery now checks the current turn and shared usage key rather than the mere presence of an old stamp. Granting an ability supplies its predefined behavior; it does not invent missing triggers or make source-specific code valid on incompatible source types.

## Public presentation and Alfredo handoff

Both seats receive `CardView.GrantedAbilities` for visible cards. Hidden cards retain no grant data. `GrantedAbilityView` retains its existing fields and adds optional `SourceName`, `UsesPerTurn` (0 means unlimited) and `UsesRemaining` (null for unlimited or an older payload). Usage is computed by Core, not by the client. Remaining uses describe the limit, not full timing/target legality; activation continues to use `QueryActions`.

Mouseover (`CardPreview`) and the scrollable inspector (`Main.Actions`) use `GrantedAbilityText` to list full ability text, timing, readable source, duration and remaining uses for either player. The on-card indicator is Alfredo's existing #213 work, coordinated with the copy UI #342. Extend that approved presentation to actual `GrantedAbilities`, not only `GrantedKeywords`; no duplicate visual design is introduced here.

`MatchEngine.CreateAbilityGrantShowcase()` / replay scenario `ability-grant-showcase` supplies two copies of Togashi Meiyu and Spirit of the Monk. `AbilityGrantProbe.tscn` plays the real action via a native button click, hovers the receiving card as each player and checks the unchanged duplicate. Run with `--output=<directory>`; `--stay` keeps the scene open after the checks. This is an integration fixture, not a complete match or human acceptance.

## Remaining cards

The other Spirit cards are still unwired. Their old ability-grant blocker is retired, and inventory notes name the remaining per-card behavior. Berserker's delayed destruction in particular is not implemented by granting an ability ID. No unsupported card is silently made playable.

## Verification (resumed and completed)

- Integrated current main (fce56814). Battle-duration ability grants now expire in `CompleteBattleTransition`, after main's after-battle recruit window closes (#379) and before another battlefield is chosen; turn-end cleanup keeps main's additional-action clearing. The `strategy-wave-30` wave manifest was pinned for the published-card guard.
- `bash tools/check.sh --skip-client` passed on the merged tree, including 9/9 ability-grants, 15/15 card-copy, 10/10 ring-grants and 59/59 published tests. The client project builds with 0 warnings and 0 errors, the headless Godot import and startup smoke passed.
- `AbilityGrantProbe.tscn` ran natively (Godot 4.7.2, NVIDIA GPU): the real "Play Spirit of the Monk" button, then mouseover of the receiving Togashi Meiyu as each player, and the unchanged second copy. Both screenshots were inspected: each player reads "Granted ability · Permanent", the Battle text, "Source: Spirit of the Monk" and "1 of 1 uses remaining this turn", beside printed and current stats.
- This is an integration fixture, not a human playtest or a complete match. The on-card indicator remains Alfredo's work under #213, coordinated with #342.
