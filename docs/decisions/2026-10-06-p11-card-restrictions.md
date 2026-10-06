# P11: the card-restriction store and generalised target bars (issue #684)

Status: **built** (engine only; no card wired). Lane: rules. Primitive P11 of `2026-10-05-remaining-unwired-primitives.md` (issue #668). The cards that use it are wired together in one later wave.

## What it is

`src/L5R.Core/CardRestrictions.cs` (feature-owned, `MatchState.Restrictions`, `JsonPropertyOrder(1800)`).

- **Stored restrictions** (`CardRestriction`, a record with a duration `Until`: `Turn`, `Phase`, `Permanent`, `WhileInPlay`) of kinds `NoAttack`, `NoDefend`, `NoChallenge`, `NoAttach(Arg)`, `NoDishonor`, `NoPair(OtherCardId | Arg "family")`, `NoEntryFromProvince`, `ActWhileBowed`, `AssignWhileBowed`, `SpellLimit(Max, Count)`.
- **Static restrictions and bars**, read live while the card is in play: `CardRestrictionCatalog.Register(definitionId, StaticRestriction[])`, `CardRestrictionCatalog.RegisterTargetBars(definitionId, StaticTargetBar[])`, or the hook channels `EngineHooks.CardRestrictions` / `EngineHooks.CardTargetBars` for conditions that depend on the board.
- **Generalised target bars** (`TargetBarRule`, `ActorSpec`): a bar on a card (or on the unit it heads) against an actor described by player (`any`/`controller`/`others` of the target), action keyword, a named card, the unit of a card, or "a card in a unit". A **forced** rule makes matching actors target the card when it is a legal target.

"May not" removes the option (the offered lists and the validators read one predicate); "will not" lets the action be taken and the effect fails.

## API for the wave (all `MatchEngine` members in `CardRestrictions.cs`, internal)

| Call | Purpose |
|---|---|
| `AddCardRestriction(kind, card, until = Turn, arg = null, otherCardId = null, max = 0, note = null)` | store a restriction on a card |
| `AddPairRestriction(a, b, until = Turn, note = null)` | two cards may not share a battlefield (Generational Gap) |
| `AddProvinceEntryLock(ownerId, provinceIndex, until = Turn, note = null)` | Sudden Blockade |
| `AddTargetBarRule(target, ActorSpec, targetUnit = false, forced = false, until = Turn, note = null)` | a bar or forced-target rule |
| `RemoveRestriction(id)` | remove by id |
| `HasRestriction(card, kind)`, `RestrictionsOn(card)` | read |
| `SameFamilyName(a, b)` | Generational Gap's legality test (the Torn Asunder rule of thumb, `PromoFamilyName`) |
| `ProvinceEntryLocked(ownerId, provinceIndex)` | for a province-entry effect the wave adds |

Static: `NoAttach` args are `any`, `type:Follower|Item`, `keyword:Ninja`, `not-keyword:Ninja`.

## Consult points (the shared lines edited)

`MatchEngine.Assign` (two lines) and the offered attack/defend units in `MatchQueries.cs`; `MayAssignBowed` (`SeedsOfDecayBatch1.cs`); `IssueChallenge` (`Dueling.cs`); `AttachmentAllowed` and `AttachmentLegalTarget`; `Dishonor`; `QuotePurchase` and `QuoteOverlay` (every province entry: Recruit, Reserve, overlay and ability recruit read the quote); `ResolveCelestial`; `StartTurn`; `RequireNotBarredFromTargeting` and `RequireTargetNotBarred` (`TargetBars.cs`); the performer switch of `ValidateAbility` and of the offered-performer list (`CardAbilities.cs`, wrapped, no re-indentation); one `NoteRestrictionAction` beside `CountReconActionTaken`; two lines in `EngineHookStartup.cs`.

## Readings for Mendon to confirm or correct

1. **Kojinrue (his ruling).** `ActWhileBowed` waives only the "unbowed" requirement of the performer category. A Performer-subject Bow cost is judged by that same category in the engine, so the exemption is withheld for any ability whose cost bows the performer (`BowsPerformerAsCost`); a bowed performer therefore cannot pay "bow him".
2. **Sudden Blockade (his answer).** The lock stops every province entry this turn. A Celestial revealed from a locked province does not enter: it is recorded (`DelayedCelestials`) and enters when the next turn begins (`StartTurn`), and the province refills face-down, revealed in the Events Phase. Read as: the delayed entry happens at the start of the *next turn of either player*.
3. **"Will not challenge"** is a "will not": the action is taken, its costs are paid, and no duel is issued (`challengeRefused` event).
4. **Phase duration** means the same `Phase` value in the same turn.
5. **Pair restrictions** judge every card standing at the battlefield (either side) plus the cards the same command assigns there.

## Not done

- Restrictions are keyed by card id and are not carried across an Experienced overlay (the overlaid card keeps its own id); no current card needs it. `Turn`-scoped restrictions on a card that leaves play and returns the same turn persist.
- The redirect and sequenced-attack-target path (`RequireTargetNotBarred`) knows only the performing card, so keyword-scoped bars (Political) are judged at announcement only.
- `SpellLimit` counts Spell actions performed through the performing card; the wave decides which card prints it.
- No card is wired; the cards are listed in the primitive plan (Natsumi - exp, Matsu Sango, The Lost Colossus, Bayushi Jin-e, Kamalakar's Harem, Otomo Suikihime - exp, Hida Kaiji - exp, Isawa Ryuzo, Generational Gap, Mirumoto Kojinrue - exp, Asahina Umehiko, Ninube Onchi - exp, Tankoji, A Welcome Reprieve, Sudden Blockade, P'an Ku's Madness, Yoritomo Naoto - exp, and others).
