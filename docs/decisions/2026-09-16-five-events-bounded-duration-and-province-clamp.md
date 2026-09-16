# Five more Events: bounded "until your second turn" duration, a province-strength ceiling, and the first reveal-time player choice

**Decision:** Formal Apology, Harsh Choices, Rebuilding Kyuden Hida, Times of Strife, and War
Weariness (all Second City) are wired via `tools/content/package_event_wave_2.py`. Each needed
genuinely new engine surface — none reuses Suspicions' own shape verbatim.

**Data-quality note (all five):** the local catalogue's own `current_oracle_text`/
`source_record.text` prepends a boilerplate "After you reveal cards in your Provinces at the start
of your turn, if this Event is face-up in one of them," clause to every one of these five cards —
confirmed, against each card's own scan, that none of them actually prints that clause (Suspicions,
already shipped, does print an equivalent lead-in on its own card, so this isn't a universal oracle
bug, just one affecting these five specifically). `TEXT_OVERRIDE`'d to each card's own literal text.

**"Until your second turn from now begins," a new bounded ongoing-Event duration
(`CardInstance.ExpiresAtTurnsTaken`):** Suspicions' own "until the game ends" shape needs no expiry
at all; these three cards' effects genuinely end. Zone.EventInPlay's own doc comment already named
the intended shape ("move to DynastyDiscard, same as an ordinary Event") — the only missing piece
was *when*. Counted in the owning player's own `PlayerState.TurnsTaken`, not the shared
`MatchState.TurnNumber`: "your second turn from now" is two of *your own* turns forward, which is
four raw alternating turns in this two-player engine, not two — an off-by-half-in-players bug this
project's own regression test (`RebuildingKyudenHidaDuration`) caught and corrected before it shipped
(the field briefly carried the wrong name/arithmetic, `ExpiresAtTurnNumber = TurnNumber+2`, during
implementation). Stamped once, at reveal (`player.TurnsTaken+2`, since `TurnsTaken` already reflects
*this* turn's own increment by the time `EventsPhase` runs); checked and discarded in `StartTurn`,
the one place a turn genuinely begins for a specific player, scoped to that player's own cards only.

**Formal Apology:** no new duration state at all — the card's own presence in `Zone.EventInPlay` IS
the "still watching" flag, the same living-state idiom `Suspicions`/`GrantedKeyword.Duration`
`"Permanent"` already use. `MatchEngine.CheckFormalApology` is called from two places: once
immediately when the card enters play (Honor could already be below starting at that exact moment),
and once at the tail of `GainHonor` itself — the single shared choke point *every* Honor change in
this engine already funnels through, so "may be negated, modified, or redirected by cards that
affect honor gains" (the user's own clarification) is automatically true for its own +5: it is just
another ordinary `GainHonor` call, not a special-cased one. Re-entrant safe by construction: the
watch card is moved to `DynastyDiscard` *before* the recursive `GainHonor(+5)` call, so that nested
re-entry finds nothing left to trigger.

**Harsh Choices:** the first Event ever needing a genuine player decision at reveal time — every
prior Event (Suspicions, and the four siblings above) auto-resolves unconditionally.
`MatchEngine.ResolveEvent` now returns `bool` (true = deferred to a `PendingChoice`, the same
"caller must stop and let something else resume it later" convention `low-market-equip`/
`equip-battle` already use for their own entry-reaction windows), and `EventsPhase` itself split into
a resumable `ContinueEventsReveal` so a later province in the same reveal loop isn't silently
dropped if an earlier one opens a choice. `DestroyProvince`'s own existing behavior (it discards
whatever card currently sits at `province.CardId`) handles discarding Harsh Choices for free on the
destroy branch, since `ResolveEvent` deliberately never cleared `province.CardId`/`card.ProvinceIndex`
before deferring.

**Times of Strife, a province-strength CEILING:** distinct from Fallow Lands' own additive bonus
(`RegionStrengthBonus`) — a new `ProvinceState.PrintedStrength` (captured once at setup, alongside
`Strength`, and never mutated again) gives `EffectiveProvinceStrength` something stable to clamp back
down to, since `Strength` itself is a live, already-mutated-in-place value (Ring of Earth's own
raise/lower). `Math.Min`, never a floor — a province already below its own printed strength is
untouched, matching the card's own text precisely. Global (either player's provinces, not scoped to
"your own" anywhere in its printed text), checked via a new `ControlsEventEffect` helper (mirroring
`ControlsCelestialEffect`) against both player ids.

**Times of Strife's discard trigger, and the reachability check that shaped it:** "another player who
controls no units at the current battlefield announces a Battle action" turned out to describe an
edge already carved out by an *existing* card, not a new one: the ordinary blanket Battle-timing
presence `Require` (`ValidateAbility`) already blocks every Battle-timed ability unless the acting
player controls a unit at the current battlefield, with exactly four named exceptions — Ring of
Earth's own raise/lower ("Even if you control no units at the current battlefield," its own printed
text) and Mirumoto Michi/Togashi Rikyou's own reinforcing-move abilities. This card's discard clause
is reachable *only* through one of those four; the regression test exercises it via Ring of Earth,
the simplest of the four. Queued (`MatchState.PendingTimesOfStrifeDiscards`) and drained by the
*existing* `OpenOverconfidentDrawWindow` (now serving double duty, draining this queue first) rather
than given its own copy of the roughly dozen call sites that already invoke it — the identical
"drain one queued player id, open a window, resume" shape `PendingOverconfidentDraws` already uses,
so it composes for free with every deferred-window interaction that mechanism already handles.

**War Weariness:** "after the resolution of the first action each phase that one or more
Personalities performed, bow them" — approximated as *announced* (checked at the very top of
`ExecuteAbilityEffects`, alongside Times of Strife's own identically-timed check) rather than
strictly post-resolution, since every currently published ability either already bows its own
performer as part of its own cost (harmless/idempotent alongside this) or never reads its own
performer's `Bowed` status mid-resolution — not observably different today, and recorded here rather
than silently assumed. Scoped to `useAbility`-dispatched actions only (Battle/Limited/Open/etc.);
`playStrategy`/`equip`/`recruitReserve` are the controller acting generically, not "a Personality
performed" in the rules' own technical sense, so they are correctly out of scope, not merely
unhandled. The once-per-phase gate (`MatchState.WarWearinessBowedThisPhase`) resets only when
`SetPhase` transitions into `Phase.Action`, `Phase.AttackDeclaration`, or `Phase.Dynasty` — explicitly
*not* `Phase.Battle` itself, which this same function re-enters once per battlefield
(`SelectBattle`): the user's own clarification is that the whole Attack-Declaration-through-battle-
resolution stretch counts as one "battle phase" for this effect, not each battlefield separately.

**Rebuilding Kyuden Hida needed no enforcement code at all.** Its own restriction — "the Attacker may
not announce a Battle action before the Defender's first normal opportunity to act or pass" — is
already the engine's unconditional default: `SelectBattle` is the *only* place `Phase.Battle` is ever
entered, and it always seeds `SetPhase(Phase.Battle, 1-_state.ActivePlayerId)` — the Defender's own
priority, unconditionally, every single battle. The user's own spec named a real exception case
("an attacker has the most naval cards, or plays Sneak Attack, must pass instead") — but no
Naval-count priority mechanic or Sneak-Attack-style pre-empted-first-action exists anywhere in this
engine yet (Engage timing itself is explicitly not started, issue #15), so that exception is
currently unreachable. Building a defensive `Require` to guard against a state combination that
cannot currently arise would be untestable dead code — deferred, the same "not yet needed" discipline
this project already applies elsewhere, not silently ignored: if a future card ever grants the
Attacker first priority, this restriction needs revisiting then.

**Reason and evidence:** User spec (2026-09-16), five cards' own printed texts (see
`package_event_wave_2.py`'s own module doc for each, hand-verified against
`assets/reference/oracle/second-city/images/oracle-l5r-{2660,2978,6200,8494,9222}-printing-1-image-1.jpg`).

**Lane:** Rules (mdornbrook). No client/UI change.

**Affected files:** `src/L5R.Core/Models.cs` (`CardInstance.ExpiresAtTurnsTaken`,
`ProvinceState.PrintedStrength`, `MatchState.PendingTimesOfStrifeDiscards`/
`WarWearinessBowedThisPhase`), `MatchEngine.cs` (`SetPhase`'s new reset condition, `StartTurn`'s new
expiry sweep, `GainHonor`'s new `CheckFormalApology` call, `ContinueEventsReveal`/`ResolveEvent`'s
bool-returning split, `ResolveHarshChoices`, `EffectiveProvinceStrength`'s new clamp,
`OpenOverconfidentDrawWindow`'s new queue-drain, `ResolveTimesOfStrifeDiscard`,
`ResumeAfterDeferredWindow`), `CardAbilities.cs` (`ExecuteAbilityEffects`'s new Times of
Strife/War Weariness announce-time checks), `PublishedMatch.cs`/`MatchEngine.cs`
(`ProvinceState.PrintedStrength` set alongside `Strength` at setup),
`PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (`EventWave2`), new
`tools/content/package_event_wave_2.py`.

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/259

**Status:** Accepted and implemented for all five cards. Rebuilding Kyuden Hida's own exception
clause (Naval-count/Sneak-Attack priority override) is accepted-as-future-scope but not implemented
— no mechanism exists yet that could ever make it matter.
