# Daigotsu Bofana and Daigotsu Bofana, Experienced (personality-wave-29)

Status: implemented; the four base-card rulings below are Mendon's, confirmed in chat on 2026-09-19 before the work finished. Every
other reading is a decision of this change, marked with its alternative, and the ones still needing his eye are listed under "Open
questions". Lane: rules (mdornbrook; cross-lane edits under his standing authority). Issue: #333.

> **Daigotsu Bofana** (oracle-l5r-1706, Second City 59) -- Force 7, Chi 4, no Honor Requirement, Cost 10, Personal Honor 0, Spider Clan /
> Shadowlands / Samurai / Dark Scion of Fire / Paragon / Loyal (not Unique).
> After the first time each phase another player's action targets Bofana: If the action was not performed by a card with (current)
> Force equal to or higher than Bofana's base Force, the player discards a card.
>
> **Daigotsu Bofana - exp** (oracle-l5r-10315, Torn Asunder 59) -- Force 7, Chi 4, no Honor Requirement, Cost 11, Personal Honor 0,
> Loyal / Unique / Experienced / Imperial Explorer / Scout, and the rest.
> After Bofana enters play: Lose 2 Honor and all other players must discard a card, or two cards if they are Crab Clan.
> Battle: Target a Dark Virtue in your discard pile: Take an additional action to play it for one of its Battle actions. After it
> resolves, remove the card from the game.

Both scans were read by eye and their WebP working copies sha256-checked against the committed receipts (`bafc81ec...` and `1ae11130...`).
The published text is the catalogue text: it matches the scans exactly. Neither card prints a "Reaction:" label on an "After ..." line, so
both are **traits** (mandatory, working while bowed, resolving before any Reaction to the same trigger), not optional Reactions. Both are
published whole: base Bofana is one Effect, exp Bofana is an Effect plus `EntryHonorLoss` 2 plus one ability. Art:
`src/L5R.Client/Content/Published/oracle-l5r-1706.jpg` / `-10315.jpg`. Published count 407 -> 409.

## Mendon's confirmed rulings (base Bofana)

1. **The limit is once per PHASE**, not the default once per turn. The phases are Straighten, Events, Action, Attack (the engine's whole
   battle sequence), Dynasty and End; a new phase resets it, including the same phase on the next turn. Keyed per Bofana card instance.
2. **Mandatory.** The performing player discards, and THEY choose the card.
3. **Threshold:** the performer's CURRENT Force must be equal to or higher than Bofana's BASE Force (7); below 7 causes the discard.
4. **The performer.** An Item's or Spell's action is performed by the Personality it is attached to (that Personality's current Force). A
   Follower performs with its own current Force. A Personality performs with its own. A card that produces an action with no performer --
   Holdings, Regions, Celestials, Rings, Strategies -- counts as Force 0, so its action always causes the discard.

## Decisions and the alternative reading

| # | Decision | Alternative not taken |
|---|---|---|
| 1 | **"Another player"** is any player other than Bofana's controller (`command.PlayerId != Bofana.OwnerId`). In the two-player game that is his opponent. | -- |
| 2 | **The first targeting each phase spends the trigger whether or not the discard happens.** The Force test and an empty hand are the effect's conditions, not the trigger's: a Force 9 performer, or a player with no hand, still uses up that phase's trigger, and a second targeting the same phase does nothing. | Spend it only when a discard actually results. |
| 3 | **Base Force = the printed Force (7)**, read from the definition, ignoring every modifier on Bofana; the performer's side is `CardForce` (all modifiers, tokens, conditional bonuses). +3 on Bofana does not let a Force 8 performer be caught; -3 does not let a Force 5 one off. | Compare against Bofana's current Force. |
| 4 | **The performing card** follows Mendon's ruling (above), implemented in one function (`PerformingCardForce`). **A Strategy (or other card) whose ability names a performing card** -- "choose your performing Samurai" -- is performed by that Personality, so his Force decides; the ability's own Performer field is the engine's record of exactly that. A Strategy with no named performer, a Ring, Holding, Region, Celestial, a Stronghold and a player ability (the Imperial Favor) are Force 0. A Strategy played by a player whose army is attacking is still Force 0: an army at the battlefield performs nothing unless the card names it. | Every Strategy is Force 0 even when it names a performing Personality (Mendon's sentence "strategy cards ... always cause the discard" read literally). One line in `PerformingCardForce`. **OPEN 1.** |
| 5 | **"Targets" is `command.TargetId`**, exactly as for Akodo Kakihara's reaction: a group or mass effect that names no single target does not target anything, and a second selection carried in `CardIds` does not count -- with one exception, Bayushi Kahoku - exp, whose Attack's own target rides in `CardIds` beside a separate -4F `TargetId` and who targets both. | Treat every affected card as targeted (needs per-ability classification with no card asking for it yet). |
| 6 | **Timing: at announcement, before every Reaction window.** A trait resolves before any Reaction to the same trigger (core rules; the engine's `AfterOwnActionTraits` says the same), so the discard is asked first, then a Champion of Thunder / Kakihara window opens, then the action resolves. The Force comparison is made at that moment, before any Reaction can change it. | Queue it and ask after the action resolves (the Times of Strife shape). Rejected: a queued discard opened by a window's close would strand the action that window is holding (see Limits). |
| 7 | **"Action" excludes a Reaction**, as it does for Kakihara: a Reaction that names Bofana neither triggers the trait nor is answered by it. | -- |
| 8 | **The card being played is not in the player's hand.** The discard options are the acting player's hand except any card the announced command itself uses (its source, target, performer, chosen cards); a hand holding only the Strategy being played is an empty hand: nothing happens. (Discarding the played Strategy would leave it "played" twice.) | Offer the played card too. |
| 9 | **Empty hand: nothing happens**, the action carries straight on, and the trigger was still spent (decision 2). No event is emitted, so nothing is announced about a hand that does not exist. | -- |
| 10 | **A bowed Bofana still triggers**: a trait works while bowed (as with Times of Strife, Sensei, Destined). He must be in play; in a hand or discard pile he watches nothing. | -- |
| 11 | **Hidden information.** The discarding player alone holds priority, so only they are offered the options (`QueryActions` tells the other player to wait); the pending choice's prompt names Bofana, never a card; the discard pile is public, so the chosen card is announced when discarded, as any hand discard is; the hand's contents never cross to the other player. Tested. | -- |
| 12 | **The limit is per card instance and lives on the card's `AbilityUses`.** Two Bofanas in play each fire once per phase; a Bofana that leaves play or returns to hand starts fresh; an Experienced overlay carries the stamp with the other uses. | -- |

## The once-per-phase limit (general, `PhaseLimits.cs`)

The engine's `Phase` enum is finer than the printed phases: the Attack Phase is `AttackDeclaration`, `DefenseAssignment`,
`CavalryDeclaration`, `CavalryDefense`, `BattleSelection` and `Battle` (which `SetPhase` re-enters once per battlefield), and Dynasty's
province discard is `DynastyDiscard`. `PrintedPhases.Of` maps every value to one of the six; `PrintedPhases.Instance(turn, phase)`
numbers one phase instance. A phase use is a stamp under the key `"phase:" + key` in the card's (or player's) `AbilityUses`, holding the
instance number; it counts while it equals the current instance, so a new phase, or the same phase on a later turn, resets it with
nothing to clear, and it cannot be mistaken for a per-turn stamp (different key). `TrySpendPhaseUse` / `PhaseUseSpent` are the whole API.

Reset on phase change is therefore by comparison, not by clearing; the observable behaviour is the requested one (spent in the Action
Phase, unspent in the Attack Phase, unspent in the next turn's Action Phase). The war-weariness flag (`WarWearinessBowedThisPhase`) is a
separate, hand-cleared once-per-phase boolean and was left alone.

**Who else can use it.** Unwired catalogue cards whose text says "once per phase" or "the first time each phase" (searched over the whole
catalogue): Oriole Imperial Vanguard (10337: "after the first time each phase a Political action targets this Personality, negate its
remaining effects" -- needs the limit, an announce-time targeting trait, a Political-action classification and a way for a trait to negate
the announced action), Hida Tadama (10618: "after the first time each phase Tadama is destroyed, bring him into play" -- needs a
destroy-replacement), Steel Soul Dojo (7484: the first Dark Virtue action you took each phase: draw -- a player-level limit; `TrySpendPhaseUse`
takes any `AbilityUses` dictionary, so `PlayerState.AbilityUses` works), Yamazaki - exp (9853: once per phase, before you lose exactly 1
Honor, negate it -- needs an Honor-loss interception). The same limit is not yet an ability flag: an ability-level `OncePerPhase` (the
per-turn dedupe in `ValidateAbility` reading the phase stamp instead of the turn) is the next small step for Yamazaki, and was not built
because no card wired here asks for it. The once-per-TURN targeting relatives (Hida Osote 3165, Hida Kaiji 10040, Kamalakar 4253, Six-Ring
Sword 9968) would reuse the announce-time hook with the ordinary turn stamp.

## The forced discard (general)

Times of Strife's queued discard (`PendingTimesOfStrifeDiscards`, a list of player ids drained by `OpenOverconfidentDrawWindow`) is
generalized, not duplicated: `PendingForcedDiscards` is a list of `(player, reason)`, the choice type is `forcedDiscard`, the command is
`forcedDiscard` with the chosen hand card as `TargetId`, and `QueryActions` now lists it (before this change the discard could be answered
in the engine but was not offered to a client or the practice opponent). Two ways to open one, one way to answer:

- **Queued** (Times of Strife; exp Bofana's entry): drained at every point the engine hands priority back, in order, skipping a player
  whose hand is empty by then. Unchanged behaviour for Times of Strife (its one test now names the new type).
- **At announcement, holding the action** (base Bofana): the pending choice records the announced command
  (`MatchState.ForcedDiscardContinuation`); answering it calls `AnnounceAbility` again with that command, which is the rest of
  `UseAbility` (Additional Action, announce/before-action/Unimpeachable windows, execution). `UseAbility` was split into
  `ValidateAbility` + `AnnounceAbility` + `ContinueUseAbility` for this; nothing else changed in it.

The practice opponent answers a forced discard with its least useful card (same scoring as the discard after a Terrain search).

## Daigotsu Bofana - exp

### First ability (wired)

"After Bofana enters play" has no Reaction label: a mandatory entry effect like Destined's draw and `EntryHonorLoss`. The Honor loss is
the card's `EntryHonorLoss` 2; the discards are the Effect `bofana-entry-discard` (`ApplyMandatoryEntryEffects`), queued for each other
player: two entries for a Crab Clan player, one otherwise, each resolved as its own choice by that player, skipped when their hand has
run out (so a Crab player with one card discards it, with none discards nothing). **Crab Clan** is `PlayerState.Clan == "Crab"`, the Clan
of the player's Stronghold, the same reading every other "is this player X Clan" already uses (Kitsuki Minori's "any other player is Lion
Clan", Matsu Yoshito's "the Defender is Dragon Clan"). Order follows the text: Honor first, then the discards. It fires from every entry path
(Purchase, Overlay, Reserve, an ability that recruits).

**A pre-existing bug this exposed and this change fixes.** `Purchase`, `RecruitReserve` and `BringProvincePersonalityIntoPlay` opened a
Proclaim window for a Proclaim-eligible entry (a Personality of the buyer's own Clan, not bought with the Clan discount, by the active
player, once per turn) and never applied the mandatory entry effects -- `ApplyMandatoryEntryEffects` was only reached through
`OpenEntryReactionWindow`, which the Proclaim branch bypasses. A Spider player buying exp Bofana at full cost was exactly that case. The
three branches now apply them before opening the window. **Behaviour change to already-wired cards** (nine; each was silently skipping its
mandatory effect on a Proclaim-eligible purchase): Mirumoto Kazuya, Togashi Noboru, Daigotsu Shikenuro and Mirumoto Bojan (Honor loss
on entry), Mirumoto Bojan, Kitsu Ririko, Hida Kurabi and Matsu Ryohei (Destined draw), Mirumoto Hikaro and Yozo exp2 (Sensei discard).
A regression test buys Kazuya and Kurabi both ways.

### Second ability: investigation, and the decision to wire it

*Is there a "play a card from your discard pile" primitive?* Only Discipline (#331): an ability flag, from a discard pile, priced by the
Discipline cost, removed from the game after; nothing lets one card's action play a *different* card from a pile. The Additional Action
grant exists (`AdditionalActionGrant`, scoped to specific card ids, consumed by a Battle action, Effortless Counterattack can negate it).

*What are the Dark Virtue cards?* The catalogue keyword "Dark Virtue" is on nine Strategies (another nine cards mention it in text:
Akodo Hotaka, Daigotsu Aya, Ishibashi, Miki, Murota, Tenbatsu, Yutaka, Steel Soul Dojo, and Bofana himself). **None of the nine is
wired.** Their actions: At Any Cost (639, "Dark Virtue Battle"), Cast Aside the Weak (1225, an Open and a Battle), Perfect Sacrifice (5923,
"Battle/Limited") and Revenge (6302, Battle) have a Battle action -- four; Know Their Minds (4487, Open), Vigilant Eyes (9156, "Dark
Virtue Open"), Martial Manipulation (10157, Limited), Methodical Questioning (9915, Limited) and Without Mercy (10405, Reaction) do not.
So the second ability can be tested only against training fixtures until a Dark Virtue is wired; that was judged no reason to publish half
the card, and it is not half the card: both abilities are wired and tested end to end.

*Decision: wire it, because the addition is small and general, not a new primitive.* Added:

- `AdditionalActionGrant.FromDiscard` (one defaulted field): the grant is the play of the single scoped card from a discard pile.
- Target category `own-dark-virtue-in-discard` (a Strategy carrying "Dark Virtue" in your own Fate discard pile **that has a Battle
  action** -- a card with none would only waste the grant, so it is not offered as a target) and the ability `bofana-exp-dark-virtue`,
  whose effect is `GrantAdditionalAction(player, [target], fromDiscard: true)`.
- `PlayableFromDiscard` in `ValidateAbility`'s zone check and in `AbilityActions`' candidate sources: a discard-pile card is a legal
  source for a **Battle-timed** ability of exactly the card a live `FromDiscard` grant of that player names ("one of its Battle actions":
  a Battle/Open card's Open half is not one, and the once-per-turn stamp the card earned earlier this turn is not the object played).
  Which Battle action is the player's choice: every one is offered, each validated like any action (target, performer, cost, presence).
- `ConsumeAdditionalActionGrant` spends a `FromDiscard` grant first, so an unscoped Again! grant is never spent in its place.
- `PlayedFromDiscard` generalizes `DisciplineFromDiscard`'s removal: a card whose non-Reaction ability is played from a discard pile is
  removed from the game once it has resolved (the early-return paths for an Attack and a challenge included). The Discipline *price* still
  keys on the Discipline flag; a non-Discipline Dark Virtue pays its own printed cost from the discard pile as it would from hand.

The grant is spent or forfeited exactly like every other: passing or taking any other action ends the burst and the card stays in the
discard pile; nothing about a card in a discard pile is playable without it (tested). Bofana's own action is an ordinary once-per-turn
Battle action.

**Decisions and alternatives.** (a) The chosen card's ability is *its* action, so the Force/Honor/target rules are its own, not Bofana's.
(b) A hand-played Dark Virtue discarded earlier this turn can be played again from the discard pile the same turn (Discipline's "not the
same turn as played from hand" is a Discipline rule, not repeated here); alternative: keep the card's once-per-turn stamp. **OPEN 2.**
(c) A Discipline-flagged Dark Virtue played through Bofana would pay the Discipline cost (the pile play prices by the flag); no such card
exists. (d) The grant obeys "no new additional actions this battle" (Two Hands), like every grant.

**What this unlocks.** The same grant, with a different target category, is what Akodo Tezuka (298, Bushido Virtue; also needs its
"discard a card" cost and Battle, Limited or Open by phase) and Fubuko (10318, a Kiho Strategy Fubuko must perform) print; Agasha Kurou
(9835) is the same shape for a Spell. Their BLOCKERS notes were updated where they existed.

## Limits (documented, not silent)

- **A queued discard drained by a window's close can strand a deferred action.** `PassReaction` drains the queue when a reaction window
  closes, before the deferred command of a `beforeActionReaction` / `attackReaction` / `additionalActionReaction` window executes; if a
  discard is queued at that moment the discard's window opens over the still-pending action. This predates this change (Overconfident and
  Times of Strife queue the same way) and only matters for the queued entry discards when Bofana - exp enters play *inside* such a window.
  Base Bofana does not use the queue for exactly this reason.
- **Negated additional actions.** A negated Additional Action (Effortless Counterattack) is never executed; the trait fires at
  announcement, so Bofana's trigger has already been spent by an action that was then negated. Effects negated by a Reaction (Champion of
  Thunder) do not un-target him either.
- A Reaction-windowed action targeting him through an Engage window is not an action (decision 7).
- The client has no special UI for the forced discard: the engine offers it as ordinary options (`forcedDiscard`), and an inspector that
  lists offered actions shows it; a bespoke presentation is UI-lane work. (The same was already true of Times of Strife, which was not
  offered at all before.)

## Open questions for Mendon

1. **A Strategy that names a performing Personality** (Unexpected, Strength of the Divine, ...): I read your "Strategy cards count as Force
   0" as applying to a Strategy with no performer, so a Strategy that says "choose your performing X" is performed by X and X's current
   Force decides. If you meant every Strategy, it is one line (`PerformingCardForce`, the `ability.Performer` test).
2. **Playing a Dark Virtue from the discard pile the same turn it was played from hand** is allowed here (the grant is the permission).
   Say if it should follow Discipline's "not the same turn".
3. **Proclaim-eligible entries now apply their mandatory entry effects** (list above): confirm the change to the nine already-wired cards
   is what the printed cards mean (it is what their text says).

## Mutation checks

Each mutation was applied to the engine source alone, the suite rebuilt and run (`--filter daigotsu-bofana`), and the source restored; every one
fails at least one named test (the run is a throwaway script, not committed):

| Mutation | Tests that fail |
|---|---|
| Once-per-phase never resets (the spent check ignores the phase) | `OncePerPhaseAndPerInstance`, `RealTurnAcrossPhases` |
| Once per TURN instead of per phase | `RealTurnAcrossPhases` |
| The Battle step counts as its own phase (the Attack Phase split) | `SixPrintedPhases` |
| Force comparison strictly higher instead of equal or higher | `PersonalityForceThreshold`, `BaseNotCurrentForce`, `AttachmentUsesWielderForce`, `FollowerUsesOwnForce`, `NoPerformerCountsAsZero` |
| The another-player test removed | `OnlyAnotherPlayersActions` |
| Base vs current Force: compared with Bofana's CURRENT Force | `BaseNotCurrentForce`, `KahokuAttackTargetsBofana` |
| Attachment performer: an Item/Spell uses its own Force, not its wielder's | `AttachmentUsesWielderForce` |
| Zero-Force rule: a card with no performer counts as strong | `StrategyTargetsBofana`, `NoPerformerCountsAsZero` |
| Follower performer: the wielder's Force instead of its own | `FollowerUsesOwnForce` |
| A Reaction counts as an action | `OnlyAnotherPlayersActions` |
| The trigger is spent only when the discard condition is met | `PersonalityForceThreshold`, `OncePerPhaseAndPerInstance` |
| A Crab Clan player discards one card, not two | `ExpEntryDiscards` |
| The Proclaim path skips the mandatory entry effects again | `ExpEntryDiscards`, `ProclaimEligibleEntryAppliesMandatoryEffects` |
| The played Strategy counts as a card in hand | `EmptyHandNothingHappens` |
| A card played from a discard pile is not removed from the game | `ExpPlaysDarkVirtueFromDiscard`, `ExpGrantIsForfeitable` |
| Bofana - exp may target a Dark Virtue with no Battle action | `ExpPlaysDarkVirtueFromDiscard` (the first run of this mutation passed: the test only checked the listing, so it now also submits the illegal targets directly) |
| Kahoku's Attack target does not count as targeting | `KahokuAttackTargetsBofana` |
| Bofana - exp's entry discards never queued | `ExpEntryDiscards` |
| A discard-pile card is playable without the grant | `ExpPlaysDarkVirtueFromDiscard`, `ExpGrantIsForfeitable` |

## Affected files

`src/L5R.Core/DaigotsuBofana.cs` (new: the targeting trait, `PerformingCardForce`, `ActionTargetIds`, entry discards, the discard-pile grant
helpers), `PhaseLimits.cs` (new: `PrintedPhases`, the phase-use helpers), `CardAbilities.cs` (`UseAbility` split, `bofana-exp-dark-virtue`,
the `own-dark-virtue-in-discard` target and candidate list, `PlayableFromDiscard`/`PlayedFromDiscard` in the zone check, once-per-turn
exemption, candidate sources, label, removal, grant creation and consumption, the Times of Strife queue entry), `MatchEngine.cs` (the
generalized drain and `ResolveForcedDiscard`, the entry-effect hook, the three Proclaim branches), `MatchQueries.cs` (the `forcedDiscard`
listing), `Models.cs` (`ForcedDiscard`, `PendingForcedDiscards`, `ForcedDiscardContinuation`, `AdditionalActionGrant.FromDiscard`),
`PublishedContent.cs` / `CardCatalog.cs` / `L5R.Core.csproj` (personality-wave-29, plus eleven `training-*` fixtures),
`src/L5R.AI/TrainingOpponent.cs` (the discard score), `tools/content/package_personality_wave_29.py`,
`content/published/personality-wave-29*.json`, the two art files, `tools/content/build_card_inventory.py` (Bofana's stale BLOCKERS
entry removed, two notes updated) and `docs/CARD_INVENTORY.xlsx` (regenerated, 409 wired), `tests/L5R.Core.Tests/DaigotsuBofanaTests.cs`
(new, 18 tests), `Program.cs` (the Times of Strife test, the suite registration), `PublishedTests.cs` (pinned count 409),
`tests/L5R.AI.Tests/Program.cs`.
