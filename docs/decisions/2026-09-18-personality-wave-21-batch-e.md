# Personality wave 21 (Batch E of the BLOCKERS audit): shared waiver, windowed province flag, nested creation

Status: accepted. Lane: shared (engine rules plus the standard per-wave shipped art). Issue: #299.

## Decisions

**One controller-clan-conditional Honor Requirement waiver.** Akodo Chiyo ("Crab Clan players may
ignore Chiyo's Honor Requirement") and Kitsu Ririko ("enters play ignoring Honor Requirement if you are
a Unicorn Clan player") share `CardDefinition.HonorRequirementWaiverClan`, read only by
`MatchEngine.QuotePurchase` -- the single check Purchase, RecruitReserve and Your Clan Needs You!
already funnel through. It compares against `PlayerState.Clan`, the same value every existing
clan-conditional passive uses. It is a data field, not an `Effect` string, because `Effect` is
single-valued. The waiver adds no Gold surcharge (Clan allegiance's separate +2 route is unrelated) and
"may ignore" is always taken (ignoring a requirement can never hurt the recruiting player).

**"Since your last turn ended" is a windowed per-player flag reset at turn END.**
`MatchState.ProvinceDestroyedSinceTurnEnd[player]` is set at `DestroyProvince`'s own choke point for the
province's owner and cleared in `FinishTurn` for the player whose turn is ending. The BLOCKERS note for
oracle-l5r-9820 suggested resetting at `StartTurn`; that would erase a destruction that happened on the
opponent's turn immediately before, which is exactly the case the printed text asks about. It is
distinct from `ProvinceEverDestroyed` (Village Guardian), which is match-lifetime and never cleared.
Ohsuki's Sparrow's condition precedes its colon, so the flag gates *using* the Limited action at all
(not merely its effect), the same way "If opposed" and Hiruma Sniper's Reconnaissance requirement do.

**Created cards made inside another card's entry window do not open a second window.**
`CreatePersonality(..., duringEntryReaction: true)` applies only the mandatory half of entering play
(Destined, EntryHonorLoss, Sensei -- now `ApplyMandatoryEntryEffects`) and queues Overconfident, without
opening a window: `PendingChoice` has no stack, so a second window would overwrite the creator's own
still-open one (silently dropping, for example, its unused Proclaim). Documented limit: a Singh
Remnants-style watcher does not get to react to the *created token's* entry when it is created this way.

**Un-negatable movement is a shared check.** `MovementCannotBeNegated` suppresses every negating
interrupt window (Tamori Tamoko's move-negation and Champion of Thunder's "all") for a command. Shinjo
Kinto's is conditional on Reconnaissance at the current battlefield, whichever side his controller
fights on (this note originally said a Defender, assumed to hold Reconnaissance only at an enemy
province, never qualifies; the rules lead's Daidoji Yuki ruling in
`2026-09-18-battle-timing-primitives.md` decision 5 overturned that, so a Defender who gained it, e.g.
through Yuki, gets un-negatable movement at their own province); Wrathful Ancestor's is unconditional. Wrathful
Ancestor was previously protected from Tamoko only by omission from `MoveOrStraightenKind`, which left
Champion of Thunder able to catch it despite the printed text; routing it here closes that gap.

**Kakita Maratai broadens the duel-won window, not a new one.** Trigger `own-personality-won-duel` reuses
the existing `duelWonReaction` PendingChoice (still scoped by `CardId` to the winning card); any in-play,
unbowed card of the winner's controller carrying the trigger is a watcher. The existing `own-won-duel`
reaction on the winning card itself is unchanged.

**"Fear Battle:" is a plain classification word**, as Batch D already found for Bow Before My Will and
Consuming Weakness -- nothing in the engine reads it.

## Affected files

`src/L5R.Core/Models.cs`, `MatchEngine.cs`, `CardAbilities.cs`, `Dueling.cs`, `PublishedContent.cs`,
`CardCatalog.cs`, `L5R.Core.csproj`; `content/published/personality-wave-21*.json`;
`tools/content/package_personality_wave_21.py`; `tests/L5R.Core.Tests/Program.cs` (`BatchEPrimitives`).
