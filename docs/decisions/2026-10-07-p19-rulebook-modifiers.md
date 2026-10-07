# P19: rulebook-action modifiers (issue #717)

Status: **built, engine only** (no card wired, no scan, no art pack, no inventory or blocker change: the cards this unlocks are wired together in a follow-up wave). Lane: rules/shared. Source: primitive P19 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/RulebookModifiers.cs`; tests: `tests/L5R.Core.Tests/RulebookModifierTests.cs` (suite `rulebook-modifiers`).

## What it is

Two pieces, both inert until a card registers something:

* **The channel** `EngineHooks.RulebookModifier`: `Func<MatchEngine, RulebookContext, RulebookModifier?>`, sealed at startup (`EngineHookStartup.Build`). It is asked with `(AbilityId, PlayerId, PerformerId, Command)` and answers a `RulebookModifier` or `null`. Registrants run in priority then ordinal order like every channel and their answers merge: amounts add, the first non-null timing override wins, every flag ORs. With nothing registered the question is one array-length check.
* **The store** `MatchState.PlayerTimingGrants`: card-free timing grants (once per turn, one turn, or game-long) read beside the card-keyed `EffectTimingGrants` of `TimingGrants.cs` (that table is kept as it is; the new store is the card-free form the prompt asked for, not a replacement of Foothills Keep's or the Plain of Glass's grants). Both new `MatchState` properties are omitted from the saved game until used (orders 3200 and 3201), so a match that never used them saves exactly as before.

## The API a card wave calls

```csharp
// A card's feature file, in its module initializer (priority >= 10000):
EngineHooks.RulebookModifier.Register("AkodoKanoTactical", 12900, static (engine, context) =>
    context.AbilityId == "tactical-advantage" && engine.IsAkodoKano(context.PerformerId) ? new RulebookModifier(AmountDelta: 2) : null);   // IsAkodoKano lives in the card's own partial MatchEngine file

EngineHooks.RulebookModifier.Register("FrontierFarmerDiscard", 12901, static (engine, context) =>
    context.AbilityId == "equip" && engine.IsFrontierFarmer(context.Command?.CardId) ? new RulebookModifier(SourcePileAllowed: RulebookSourcePile.Discard) : null);
```

`RulebookModifier(AmountDelta, TimingOverride, SourcePileAllowed, Exemptions, Absent, AnyTarget)`:

| Field | Effect | Consulted at |
|---|---|---|
| `AmountDelta` | added to Tactical Advantage's Force bonus (Focus Value + delta, never below 0) | the `tactical-advantage` effect |
| `TimingOverride` | replaces the ability's `Timing` (`"BattleOrOpen"` is the Ayumu / Tactical Advantage form; Open in the Action phase, Battle in a battle) | `ValidateAbility`, `ContinueUseAbility` (so an Open use spends an additional-action grant like any Open action), `QueryActions` |
| `SourcePileAllowed: Discard` | Equip may take the attachment from its **owner's** discard pile (never another player's) | the `equip` command and `EquipActions`; `equip-battle`'s target (Spell or Reserve) and its target list |
| `Exemptions` (`Bowed`, `Unopposed`) | Equip (Battle)'s performer may be bowed and/or unopposed; still an own Shugenja at the battlefield | the `equip-battle` performer check and listing |
| `Absent` | the action may be taken without a unit at the battlefield | `ValidateAbility` and `QueryActions`, through the swapped `AbilityDefinition` |
| `AnyTarget` | an `enemy-attacking` target (Favor (Battle)) may be any Personality at the battlefield | the `enemy-attacking` target arm, beside Mempo's `FavorMempoAllowsAnyUnit` |

Ability ids asked about: `"tactical-advantage"`, `"equip-battle"`, `"favor-battle"` (any rulebook `PlayerAbility`), and `"equip"` for the Equip (Limited) command (the command's `CardId` is the attachment, `TargetId` the wielder). `PerformerId` is the chosen performer (null while a listing asks "could anyone").

Card-free timing grants (engine methods; a card's effect calls them):

```csharp
engine.GrantPlayerTiming(playerId, ActionClass.EquipAny, "BattleOrOpen", oncePerTurn: true);        // Practicing Kata (granted while the Kata is in play; remove with RemovePlayerTimingGrant)
engine.GrantPlayerTiming(playerId, ActionClass.EquipAny, "BattleOrOpen");                            // game-long, e.g. from an Event (Harbingers of War)
engine.GrantPlayerTiming(playerId, ActionClass.EquipAny, "Open", thisTurnOnly: true);                // lapses with the turn
engine.RemovePlayerTimingGrant(id);
```

A card-free grant is used only when neither the ordinary Equip (Limited) timing nor a card's grant already allows the equip, and a once-per-turn grant is spent once the equip's cost is paid. A Spell takes no granted timing (as before: its Battle action is Equip (Battle)).

## Shared lines edited

* `EngineHookStartup.cs`: `_ = RulebookModifier.Items;`.
* `CardAbilities.cs`: `ValidateAbility` (one line swapping in the modified ability); `ContinueUseAbility` (the same, where the ability is read); the `own-unbowed-shugenja-opposed-at-battle` performer arm (one line) and its listing twin (one `Concat`); the `own-tactician-at-battle` performer arm and its listing twin (`AtBattle` becomes "at the battlefield when acting as a Battle action, in play otherwise": identical for a Battle-timed use); the `spell-or-reserve-in-hand` target validation and its target list and the `equip-battle` "not offered at all" guard (the discard pile counts); the `tactical-advantage` effect (the amount); the `equip-battle` effect (the hand bookkeeping only when the card was in the hand); the `enemy-attacking` target arm (one `||` term); the `QueryActions` per-ability loop (the ability read through `RulebookListingAbility`, and a performer filter after the performers are listed); `OpenBattleActionReactionWindow` (a Battle-timed action counts only in the Battle phase).
* `ActionEffectTracking.cs` and `TargetedActionReactions.cs`: `IsBattleAction` requires the Battle phase for a Battle-timed ability (an Open use under a timing override is not a Battle action). Unchanged for every printed use.
* `MatchEngine.cs` `Equip`: the discard-pile zone, the hand bookkeeping only when it came from a hand, `ResolveEquipTiming` in place of `EquipTimingError`, and the once-per-turn spend after the cost is paid.
* `TimingGrants.cs`: `EquipTimingError` is now `ResolveEquipTiming` (the old name remains as a wrapper) and reads the card-free grants after the card grants; `EquipActions` lists the discard-pile candidates.

## Default behaviour

With nothing registered every new branch is skipped: `RulebookModifierFor` returns null on an array-length check, the swapped ability is the printed one (`ReferenceEquals`), the Equip zone check is the old one, nothing new is serialised. The full Core suite, the AI tests and the format and comment-swallow lints pass unchanged (results in the handoff).

## Readings for Mendon to confirm or correct

* **"Open" for Tactical Advantage means Battle/Open.** Ayumu's text ("an Open action for you if you choose Ayumu") is taken as adding the Open timing; it stays a Battle action in a battle (so the performer must be at the battlefield there), and in the Action phase it is taken in either player's segment, from any Tactician in play. The one-Tactical-action-per-Tactician cap is the generic `Tactical` action-keyword cap and is untouched.
* **Equip from the discard pile is a normal Equip.** Same cost, same legality and entry effects, but it is not "attached from your hand" (Moshi Madoka's condition, Glory of the Shogun's draw, Hida Takeuchi's record are skipped). The discard pile is public in the rules; "private to the owner" here means only its owner is ever offered the option and only the owner's own pile is a source.
* **Exemptions are per condition.** `Bowed` waives "unbowed", `Unopposed` waives "opposed"; neither waives "Shugenja" or "at the battlefield". Harumi's "once per battle" is the registrant's own bookkeeping (it can decline to answer after the first use); the primitive has no per-battle counter.
* **A negative total amount clamps at zero** rather than becoming a Force penalty.
* **`Absent` is asked per player, not per performer** in the listing (a timing override is per performer). A performer whose own modifier gives no timing override is not offered an action the printed timing does not allow in this phase.

## Not done

No card is registered (Akodo Kano - exp, Ikoma Ayumu - exp, Frontier Farmer, Yoritomo Harumi, Practicing Kata, Harbingers of War, Hantei's Guidance remain unwired), no scan or art, no inventory change. Harbingers of War's draw needs P7. Favor (Battle)'s post-announcement retargeting for Hantei's Guidance is Mempo's (`FavorMempo.cs`); this primitive supplies only `Absent` and `AnyTarget`. `EffectTimingGrants` was not folded into the new store (Foothills Keep and the Plain of Glass keep working through their table). No client change: the options are ordinary `QueryActions` entries.
