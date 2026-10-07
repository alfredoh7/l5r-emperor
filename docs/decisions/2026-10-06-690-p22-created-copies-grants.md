# P22: created cards, copies and grants (issue #690)

Status: **built, engine only** (no card wired, no scan, no content wave, no art pack). Lane: rules (mdornbrook). Source: primitive P22 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668), narrowed by the coordinator's brief for #690. Code: `src/L5R.Core/CreatedCopies690.cs` (new), `CreatedCards.cs`, `CreatedCardEntry.cs`, `AttachmentTransfers.cs`, `CardCopy.cs`; tests: `tests/L5R.Core.Tests/CreatedCopies690Tests.cs` (suite `created-copies-690`, 24 tests).

Every action in the tests is a fixture ability whose id starts with `training-p22-` (declared in `CreatedCopies690.cs`, on no card). Each fixture is the one call a wired card would make, so the fixture cases in `RunCreatedCopies690` are the worked examples for the wave.

## What was built, and the call for each piece

All engine members below are `internal` or `private` members of `MatchEngine`; a card's feature file (a `partial class MatchEngine`) calls them directly.

### 1. Removal of a created card, declared with the creation

`CreatedCardSpec.RemoveWhen` takes a `CreatedRemoval`:

| Value | Printed text | Mechanism |
|---|---|---|
| `CreatedRemoval.TurnEnds` | "remove it from the game when the turn ends" | `ScheduleTimedRemoval(..., TimedRemovalWhen.TurnEnds, ...)` as the cards are made |
| `CreatedRemoval.NextTurnBegins` | "remove it from the game after your next turn begins" | `ScheduleTimedRemoval(..., TimedRemovalWhen.PlayersNextTurnBegins, creating player, ...)` |
| `CreatedRemoval.WhenSourceStraightensOrLeaves` | "After the next time this game this Spell straightens or leaves play, remove the Illusion from the game" | a `CreatedRemovalWatch` in `MatchState.P22.RemovalWatches` on the action's source card |

```csharp
all["constructing-the-self"] = new("Limited", "...", "none", ActionKeywords: ["Air"], Costs: ActionCost.BowSource,
    Creates: MinorIllusion with { RemoveWhen = CreatedRemoval.WhenSourceStraightensOrLeaves });
```

No `RunEffects` case is needed: the default arm runs `ExecuteCreate`, which now arranges the removal for the cards it minted (also for a created copy, `CopyOfTarget`, whose tooltip gets the condition). The removal is a removal from the game of the whole unit (`RemoveUnitFromGame`), never a discard or a destruction.

The watch: `Straighten` (the one place a card straightens, by the Straighten Phase or by an effect) marks a watch due as its bowed card straightens; after every command a due watch, a watch whose card is no longer in play, or one whose card is found unbowed where it was last seen bowed, removes its created card. So the removal happens at the end of the command in which the card straightened or left, not in the middle of the effect. A watch is spent once and dropped if its created card has already left play.

Existing creators were not migrated to `RemoveWhen` (Komori Miyano, Amazing Feat, Yajinden and the others keep their `ScheduleTimedRemoval` calls). They can be, one line each, in the wave that touches them.

### 2. Values read from current stats and from a discarded card

`CreatedValue` gains `Current` (read the subject's Force, Chi, Personal Honor or Gold Cost as it stands, with attachments, tokens and modifiers) and the subject `CreatedSubject.Slot` with `Slot = "<slot id>"` (the first card the command names for that slot, typically a cost-only discard slot). `CreatedStat.Focus` is a read-only stat. `Plus` and `Keyword`/`PlusWithKeyword` work as before. The value is written on the created card once (`CardInstance.Printed`) and never follows the subject afterwards.

```csharp
// "with Force equal to this Shugenja's Chi, plus 1 if he is Air"
Values = [new(CreatedStat.Force, CreatedSubject.Wielder, Read: CreatedStat.Chi, Keyword: "Air", PlusWithKeyword: 1) { Current = true }]
// "with a Force Modifier equal to the discarded card's Focus Value"
Values = [new(CreatedStat.Force, CreatedSubject.Slot, Read: CreatedStat.Focus) { Slot = "discard" }]
// Constructing the Self: "with this Shugenja's Chi and Personal Honor"
Values = [new(CreatedStat.Chi, CreatedSubject.Wielder) { Current = true }, new(CreatedStat.PersonalHonor, CreatedSubject.Wielder) { Current = true }]
```

**The Wind's Champion and Never Without a Weapon are migrated** from their hand-minted code in `CreatedProxies677.cs` to these declarations. Their existing tests (`created-proxies-677`, 22 tests) pass unchanged. The Wind's Champion keeps a two-line case (no Shugenja, no creation; destroy the Spell after the creation); Never Without a Weapon shares the "spend the Strategy, then create" case.

### 3. An ability or a trait the created card prints

Checked first: a scan's catalogue record already carries its abilities (Kichi's Shadow Badger has `proxy677-badger-melee`, Yamazaru's Monkey has `proxy677-monkey-weaken`), and `CreatedCardSpec.AbilityText` only holds the scan to the printed text. That path stays the normal one. What was missing is declaring an ability with the creation when the proxy's record has none (Yuribara's Butterflies is published with no ability):

- `CreatedCardSpec.Abilities` (ability ids) are written on the one created card beside the scan's own.
- `CreatedCardSpec.Trait` is the created card's continuous-trait id (`CardDefinition.Effect`), for a hook keyed on it.
- `CreatedCards.Verify` refuses either without `AbilityText` (the scan must print it, and the text is compared with the scan's), an ability id that is not catalogued, and a trait on a scan that already has another.

```csharp
Creates: Butterflies with { AbilityText = "After another player's Battle action targets this card: Destroy this card", Abilities = ["yuribara-butterflies-fragile"] }
```

### 4. Exchange, Copy and Set of a stat

```csharp
ExchangeStats(target, "Force", target, "Chi", source.Id, id);                    // Shifting Waves: "Switch the target's Force and Chi"
ExchangeStats(a, "Force", b, "Chi", source.Id, id, permanent: true);              // two cards, permanently
CopyStat(from: target, to: source, "Force", source.Id, id, permanent: true);      // Ninube Tsukau: "permanently copies his Force"
SetStat(card, "Chi", 2, source.Id, id);                                           // "set his Chi to 2"
int now = CurrentStat(card, "PersonalHonor");
```

Stats are `"Force"`, `"Chi"`, `"PersonalHonor"`; a Personality's (and a Follower's Force) can be set. Each is the glossary's wording: Set is a bonus or penalty sufficient to reach the value; Copy sets to the copied card's current value, read once; Exchange notes both current values and gives both bonuses or penalties worked out from the noted values. They are ordinary modifiers (`ApplyModifier` for the turn, `AddPermanentModifier` for "permanently"), so they end with the turn unless permanent, an effect that negates a bonus or penalty negates that half, and a Personality whose Chi reaches 0 is destroyed.

### 5. Overlay onto an arbitrary Personality

```csharp
if (OverlayOnto(target, shownPersonality, Definition(source).Name)) { /* overlaid */ }   // Unbound Essence
string? why = OverlayOntoError(target, newCard);
```

The new card (from a deck, hand, province or pile) takes the target's place under the target's controller through `TransferOverlayState`: bowed and dishonored state, modifiers, tokens, granted keywords and abilities (granted traits included), attachments. The target's card is removed from the game. Nothing of the new card's earlier life follows it. It does not enter play: no entering-play window and no entering-play cost. Refused, with nothing changed, when the new card is Loyal to another Clan or is a Unique/Singular card its new controller already controls under another card. A province the new card came from is left empty for the caller to refill.

### 6. Copying an ability onto an attachment for the turn

Already there: `BeginCopy` (CardCopy.cs) takes any receiver, and Thunder Resounds uses it for a Spell. Nothing was added but a test and one exclusion (a trait is not offered as an ability).

```csharp
return BeginCopy(source!, target!, id, [CopyStep.Ability], player.Id, "ability", c.bowedBeforeAction, GrantDuration.Turn);   // Watersteel Sword
```

Not offered: an ability that copies abilities, one that will not be copied (`NotCopyable`), one that triggers on its own card entering play, a trait. One candidate is taken; several open the existing `copyAbility` choice. The copying ability itself is declared `CopiesAbilities: true`.

### 7. A transfer that changes control

```csharp
Require(CanTransferTo(attachment, host, allowControlChange: true), "...");            // in the card's validation
TransferAttachment(attachment, host, Definition(source).Name, allowControlChange: true);   // Unsettling Gathering
```

Without the flag both behave as before (same controller only), so the existing callers are untouched. With it the attachment may go to any player's Personality: it is then controlled by that Personality's controller (`TakeControl`), keeps its state (no entry), and returns to its owner's pile when it leaves play. The attaching restrictions still apply (the card's own "will only attach to", Armor and Weapon limits, stored P11 restrictions).

### 8. Filter-scoped grants and traits

A rule a card prints, read live through `AbilityIdsOf` while the grantor is in play:

```csharp
// Kitsune Merihiko - exp: "Your Nonhuman Spirits have the ability, 'Battle, Bow: Ranged 3 Attack.'" -- in the feature file's module initializer
CreatedCopies690Catalog.RegisterFilterGrant(new FilterGrantRule("merihiko-exp-spirits", FilterGrantRule.OnEffect("merihiko-exp"),
    "merihiko-spirit-ranged", new GrantFilter(CardType.Personality, Keywords: ["Nonhuman", "Spirit"])));
```

A grant an action makes:

```csharp
GrantAbilityByFilter(player.Id, new GrantFilter(CardType.Personality, ["Nonhuman", "Spirit"]), abilityId, GrantDuration.Turn, Definition(source).Name);
GrantAbilityByFilter(player.Id, filter, abilityId, GrantDuration.Permanent, Definition(source).Name, sourceCardId: source.Id);   // while that card is in play
```

`GrantFilter(Type, Keywords, AnyKeywords, ExcludeSource)`: a card type (null for any), every keyword of `Keywords`, at least one of `AnyKeywords`, and `ExcludeSource` for "your other ...". Both forms reach only the player's (the grantor's controller's) cards in play, include a card that enters play later, drop a card that stops fitting, and end the moment the source is no longer in play. Nothing is written on the receiving cards. A granted ability is limited per receiving card like a printed one.

Traits. A trait is a catalogue entry with timing `"Trait"`:

```csharp
all["jade-champion-straighten"] = GrantedTraits.Ability("After the resolution of an action from a Spell, straighten this Personality");
all["jade-champion-ward"] = GrantedTraits.Ability("Shadowlands Personalities and Followers have -1F while at this Personality's location");

GrantTrait(shugenja, "jade-champion-straighten", source.Id);                 // permanently, by default
bool has = HasTrait(card, "jade-champion-ward");                              // what a Force hook asks for a continuous trait

// a triggered trait: registered once, in the module initializer
CreatedCopies690Catalog.RegisterTrait(new GrantedTraitRule("jade-champion-straighten", GrantedTraits.AfterActionResolved,
    static (engine, firing, card) => engine.Straighten(card),
    static (engine, firing, card) => firing.Source is { } from && Definition(from).Type == CardType.Spell));
```

A trait is not an ability: `AbilityIdsOf` leaves it out, so it is never offered (not even as a disabled action), cannot be played, is not copied, and is not removed by "remove its abilities". It is stored as a `GrantedAbility` record, so it follows an overlay, ends with its duration, and is listed in the card's `GrantedAbilities` view with timing `Trait`. A triggered trait fires for every card in play that has it, in card order, after any player's action resolves, bowed or not, every time, before the after-action Reaction window; the rule's condition narrows it. A filter rule may grant a trait ("your Shugenja have the trait ...").

## Which unwired cards each piece is for

| Card | Pieces |
|---|---|
| Constructing the Self (10700) | 1 (source watch), 2 (current Chi and Personal Honor of the wielder). "This Spell may remain bowed" can use the existing `choose-straighten` Effect |
| The Egg of P'an Ku - exp (10522) | 1 (`CopyOfTarget` with `NextTurnBegins`); its "will not straighten, attack, or defend until after your next turn begins" is P11 with P4's owner-turn duration; it also has an open ruling |
| Komori Miyano, Amazing Feat and other wired timed removals | 1, optional migration |
| The Wind's Champion (10923), Never Without a Weapon (10949) | 2, migrated here |
| Kitsune Yuribara (10296) | 3 for the Butterflies' traits; still blocked, see below |
| Shifting Waves (10392) | 4 (`ExchangeStats` on the target) |
| Ninube Tsukau (10319) | 4 (`CopyStat` permanent) plus the existing `BeginCopy(..., [CopyStep.Keyword, CopyStep.Ability], ..., GrantDuration.Permanent)` |
| Spiritual Coalescence (10393) | existing calls only: `AddPermanentModifier` for the Force bonus and `GrantAbility(..., GrantDuration.Permanent)` over the performer's copyable abilities; `CurrentStat` reads his Force before he is destroyed as the cost |
| Unbound Essence (10356) | 5; the "show cards from the top of the deck" half is P20 |
| Watersteel Sword (10689) | 6 |
| Unsettling Gathering (10711) | 7 |
| Kitsune Merihiko - exp (10855) | 8 (printed filter rule) |
| The Jade Championship (8163) | 8 (two granted traits: one triggered, one continuous read by a Force hook); the Event's own timing is P5 |
| The Onyx Championship (8242) | existing `GrantKeyword` and `GrantAbility` permanent; nothing new needed from P22 |

## Left out, and why

- **A "targeted by an action" trigger for traits** (Yuribara's Butterflies: "After another player's Battle action targets this card: Destroy this card"). Only the after-action-resolved trigger is built. The targeted moment has no single call site: an action's target is judged in the before-action window, in the attack window for an Attack, per target for a multi-target action, and again after a redirect (`TargetedActionReactions.cs`, `TargetWindows.cs`). It needs one call where each of those windows opens for a target, and a decision on redirected targets. Kitsune Yuribara stays unwired; her forced-target trait is a P11 forced rule added when the Butterflies are created.
- **`CopyOfTarget` for card types other than Personality.** Not in the brief's eight pieces and no listed card needs it.
- **Battle-duration filter grants, and a removal watch on a card other than the action's source.** Neither has a card or a test, so neither was built.
- **Carrying P11 restrictions across `OverlayOnto`.** They are keyed by card id, as P11's own note says for the Experienced overlay.
- **`TraitReactions.Is` and Amazing Feat's "no trait" filter** do not know the new `Trait` timing. No published card has a trait-timed ability yet; when one is copied by Amazing Feat that filter needs one more term.
- **Client work.** No command, option or view field was added. A granted trait appears in the existing `GrantedAbilities` view with `Timing: "Trait"`.

## Shared lines edited

- `CardAbilities.cs`, `FireAfterOwnActionTraits`: one call, `FireCreatedCopies690Traits`.
- `CardAbilities.cs`, `Straighten`: one call before the card is unbowed, `NoteCreatedCopies690Straightens`.
- `HonorAndTreacheryPass.cs`, `StaticGrantedAbilityIds`: one line at the top, yielding `FilterGrantedAbilityIds(card)`. The filter grants are joined here, not on the `AbilityIdsOf` line, so that line carries one P22 edit only (next item).
- `CardCopy.cs`, `AbilityIdsOf`: one appended call, `.Where(GrantedTraits.IsNot)`; `CopyableAbility`: one more term.
- `CreatedProxies677.cs`: the two migrated creators (two specs and their two cases).
- `CreatedCards.cs`, `CreatedCardEntry.cs`, `AttachmentTransfers.cs`: this primitive's own files.

State: `MatchState.P22` (`CreatedCopies690State`, `JsonPropertyOrder(22200)`): removal watches and filter grants. Nothing was added to `CardInstance`, `PlayerState` or `AbilityDefinition`. Hook registration ids start with `P22`, priority 16200.

## Readings for Mendon to confirm or correct

1. **"After the next time this Spell straightens or leaves play" is carried out when the command ends.** The Illusion is removed at the end of the command in which the Spell straightened or left play, not in the middle of the effect. A Spell left bowed by choice in the Straighten Phase has not straightened, and the Illusion stays.
2. **A created card with a removal condition leaves as a unit.** Its attachments are removed from the game with it, as for the existing timed removals. If that should be a discard for cards that are not created, say so.
3. **"With this Shugenja's Chi" is the current value, read once.** The created card is given the stat as it stands when it is created (Items, tokens and modifiers included) and keeps it; it does not follow the Shugenja afterwards. This is what The Wind's Champion already did.
4. **Exchange and Set are bonuses and penalties like any other.** An effect that negates a penalty or a bonus on the card (Tireless Efforts, Creating Order, Gift Armor, Aka-beko, "negate the action's effects on him") negates that half of an exchange and leaves the other half. If an exchange should be all or nothing, say so.
5. **A Personality whose Chi is exchanged or set to 0 is destroyed**, for a permanent set too.
6. **Force below 0.** Force is never read below 0. A Set or Exchange that raises a Force read as 0 gives a bonus sufficient to reach the stated value, even where earlier penalties had taken it further down.
7. **A copied stat does not follow the copied card.** "Tsukau permanently copies his Force" sets Tsukau's Force to the other Personality's current Force once.
8. **Stats that can be set.** Only a Personality's Force, Chi and Personal Honor and a Follower's Force. An Item's printed bonuses are not exchanged or set.
9. **An overlay by an effect is not an entry.** No entering-play window opens for the new card, no entering-play cost is paid, no Overconfident draw is offered for either card, and "after X enters play" traits of the new card do not happen. (The Dynasty-Phase Experienced overlay still opens its window, as before.)
10. **An overlay by an effect still obeys Loyal and Unique/Singular.** The new card is refused if it is Loyal to a Clan its new controller is not, or if he already controls another card of that title; nothing happens then. Its Honor Requirement and Gold Cost are ignored.
11. **The overlaid card belongs to the target's controller.** The new card is controlled by whoever controlled the target, whoever owns it.
12. **A transfer across players is refused for a Unique/Singular attachment the new controller already controls.** `TransferUnitControl` discards a duplicate attachment that arrives with a unit; here the attachment is the card whose control changes, so the general rule ("a player cannot take control of a Singular/Unique card sharing a title with one they already control") makes the Personality an illegal destination. If the transfer should happen and the attachment be discarded instead, it is one line.
13. **A transferred attachment keeps its state and is not an entry**, also when its controller changes.
14. **"Your Nonhuman Spirits"** is left to the card: the filter names a type. Merihiko's example above reaches Personalities only. If Spirit Followers should have the ability too, the filter's type is `null`.
15. **A filter grant follows the grantor's controller.** If the granting card changes hands, the cards of its new controller have the ability.
16. **A trait is not an ability.** A granted or declared trait is not copied by "copies one ability", not removed by "remove its abilities", and never offered as an action.
17. **A triggered trait fires for any player's action** unless its printed text narrows it, once for each card that has it, and a second time in the same turn if the trigger comes again.
18. **A declared ability must be printed on the scan.** A creator may not give a created card an ability its proxy does not print.

## Checks

Recorded in the PR and the coordinator's handoff: the `created-copies-690` and `created-proxies-677` suites, the full Core suite, the bot soak, the format check and the comment-swallow lint. Fifteen mutations (a rule broken, the suite run, reverted) each failed the test for that rule: the watch never firing, the straighten mark, the timed removal not scheduled, current values read as printed, the Force top-up, the second half of an exchange, the overlay's Unique check, the overlay keeping the new card's old state, the transfer not changing control, the Unique refusal on a transfer, a filter grant ignoring its source, a filter grant reaching every player, traits left among abilities, a trait offered as an ability to copy, and a trait rule ignoring its condition.

Native Godot checks are not required: nothing here touches the client.
