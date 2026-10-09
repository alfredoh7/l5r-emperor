# P20: search and look extensions (issue #728)

Status: **built, engine, bot and client, no card wired** (The Onyx Championship, Iweko Seiken, Dark Audience, Jade Pearl Inn, Unbound Essence, Asako Usohara, Revelations, Hantei XVI - inexperienced and The False Path stay unwired; no packager, wave, scan, art pack, inventory change or blocker removal). Lane: rules/shared. Source: primitive P20 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/SearchExtensions.cs` (the feature), `SearchExtensionsShowcase.cs` (review positions), `DeckSearch.cs` and `DeckLook.cs` (the data and the hooks into the flow); client `DeckSearchPanel.cs`, `Main.DeckSearch.cs`, `Main.SearchCapture.cs`; tests `tests/L5R.Core.Tests/SearchExtensionsTests.cs` (suite `search-extensions`) and `BotPolicyTests.SearchExtensionsBot`.

## What it is

The deck search of `2026-09-18-deck-search-primitive.md` and the look of `DeckLook.cs`, extended with the pieces the nine blocked cards need. Everything is **unused by default**: a search or look that uses none of it saves, shows and behaves exactly as before (no new saved field exists until it is used; the full Core suite passed unchanged).

| Piece | API | Behaviour |
|---|---|---|
| Source `OwnProvinces` | `new SearchDeckOption(SearchDeck.OwnProvinces, criteria, effect)` | The searcher's own province cards, **face-up or face-down**. Only the searcher sees the list (`MatchView.PendingSearch` is null for the other seat). The other seat learns that a search happened and sees a card only when it is shown. Nothing is reshuffled. A card that leaves a province is replaced from the Dynasty deck, face down. |
| Sources `AnyPlayerFateDiscard`, `AnyPlayerDynastyDiscard` | same | Either player's Fate (or Dynasty, dead pile included) discard pile. Never reshuffled. A card of the other player's can only be put into play, attached or removed from the game (see readings). |
| `SearchSpec.ChooseOne` | `ChooseOne: true` | The searcher picks the source. Several sources already meant that choice; the flag states it. `Ordered` still forces the first source (R-011) and **wins** over `ChooseOne`. |
| Criterion `GoldProducing` | `new SearchCriteria("Holding", [CardType.Holding], GoldProducing: true)` | `HasGoldProducingTrait`: a printed Gold production or a Gold-producing trait. |
| Criterion `DifferentTitles` | `DifferentTitles: true` | A condition on the **set** taken: no two cards with the same title. A second pick with the same title is refused ("Each card must have a different title."). |
| `SearchSpec.SelectCount` | `SelectCount: new(min, max)` | Take between `Min` and `Max` cards in **one command** (`selectSearchCards`, `CardIds` = definition ids in the order taken; `selectSearchCard` with one id is a set of one). Every card is shown, in order. When fewer than `Min` can be taken (not enough qualifying cards, or not enough different titles) the most that can be taken is required, so a search is never impossible. `Min` 0 lets the searcher take none (the deck is still shuffled). |
| Disposition `Bottom` | `SearchEffect.Bottom` | A deck source: the card stays in the deck, which is shuffled as every search ends, and only then is it put last. A province or discard source: it goes to the bottom of its owner's Dynasty or Fate deck at once (a province is refilled). |
| Disposition `AttachToPersonality` | `SearchEffect.AttachToPersonality` | A follow-up target slot (`PendingChoice` `searchAttachTarget`, command `chooseSearchAttachTarget`, `TargetId` = the Personality). Only Personalities the searcher controls that the card legally attaches to (`AttachmentLegalTarget`, `AttachmentAllowed`, Unique/Singular) are offered; one candidate needs no decision; no candidate makes the card **ineligible** with a reason. No Gold is paid. The attachment's entering-play window opens as the search ends. |
| Disposition `CelestialIntoPlay` | `SearchEffect.CelestialIntoPlay` | Enters through the shared `EnterCelestialPlay` (#667): no province refill, the controller's other Celestials are discarded, the entry traits run. From the other player's discard pile it is controlled by the searcher while in play. |
| Disposition `RecruitFromProvinceIgnoringCosts` | `SearchEffect.RecruitFromProvinceIgnoringCosts` (source `OwnProvinces` only) | A Personality or Holding found in a province (even face down) enters play at home with **no Gold, no Honor Requirement**, the province refills, the entering-play windows open. A Unique/Singular copy already in play bars it (ineligible with a reason). |
| Disposition `PlayAsAdditionalAction` | `SearchEffect.PlayAsAdditionalAction` | The found card goes to the hand and Gozu's owed-play machinery (`SearchPlayFinishSearch`, #667, with P17's additional-action grants) takes over: a **mandatory** additional action scoped to it, or discarded when it cannot be played. |
| Dispositions `Discard`, `RemoveFromGame` | `SearchEffect.Discard`, `.RemoveFromGame` | Plain dispositions (the card goes to its owner's pile, or leaves the game). |
| Other player chooses | `OtherPlayerChooses: new(SearchEffect.Discard)` with `SelectCount(1.., 2..)` | After the picks are shown, the **other** player chooses one of them through P25's shown choice (`shownChoice`, `chooseShown`): he is offered exactly the shown definitions and sees nothing else (no deck, no instance id). The chosen card takes the `ChosenEffect`, the others the option's own `Effect`. |
| `LookSpec.RevealUntil` | `new LookSpec(id, label, targets, 0, [], LookDisposition.Bottom, Reorder: true, RevealUntil: criteria, MatchDisposition: LookDisposition.Hand)` | The cards are the top of the deck up to and including the first match (all of the deck when none matches); each is **revealed** (the public shown-cards record); the match must take `MatchDisposition` (a rule with Min = Max = 1 added automatically unless `Rules` already names it); every other card takes the `Remainder` or a rule of `Rules`. No shuffle. |

A worked registration (a card's wave registers it from a metadata-only module initializer, never from `L5R.Core.csproj`):

```csharp
// Illustrative (the card's real wording is the wave's): Dark Audience as the prompt describes it, "two attachments with different titles; the other player chooses one to remove; the other goes to hand".
static readonly SearchSpec DarkAudience = new("dark-audience", "Search your Fate deck for two attachments with different titles",
    [new(SearchDeck.Fate, new("attachment", [CardType.Follower, CardType.Item, CardType.Spell], DifferentTitles: true), SearchEffect.ToHand)],
    SelectCount: new(2, 2), OtherPlayerChooses: new(SearchEffect.RemoveFromGame));
// Jade Pearl Inn's second half is the existing RefillSourceProvinceFaceUp effect with GoldProducing: new SearchCriteria("Holding", [CardType.Holding], NonUnique: true, GoldProducing: true, Comparisons: [new("GoldCost", "==", 1)]).
EngineHooks.Searches.Register("DarkAudienceSearch", 12400, static () => [DarkAudience]);
// the ability starts it exactly as any search: BeginSearch(DarkAudience, source, player.Id, "ability", context.bowedBeforeAction); return true;
```

## Commands and views (no new `GameCommand` field)

| Stage / PendingChoice | Command `Type` | Fields |
|---|---|---|
| `searchSelect`, a set of cards | `selectSearchCards` | `CardIds` = definition ids, in the order taken (offered as one enabled default selection: the most it may take, never twice a title that must differ) |
| `searchAttachTarget` | `chooseSearchAttachTarget` | `TargetId` = the Personality |
| the other player's choice | `chooseShown` (P25) | `Choice` = the shown card's `Sequence` |

`SearchView` gains trailing nullable `MinSelect`, `MaxSelect`, `DifferentTitles`, `Place` (omitted when null, so a classic search's view is unchanged). `PendingSearch` gains `PickedCardIds`, `ShownSequences`, `PutOnBottomIds` (`JsonPropertyOrder` 22800-22802, omitted until used; 22800-22899 is reserved for P20). No `MatchState` property was added. `LookSpec` gains `RevealUntil` and `MatchDisposition` (code, never saved).

**Client.** `DeckSearchPanel` draws any place by its name ("Search your provinces"), and for a set of cards a plain list of candidate tiles: a click adds a copy to a draft (at most `MaxSelect`, one copy per title when titles must differ), a click on a full card removes it, and one explicit "Confirm selection" sends the single `selectSearchCards` command built from the engine's own default action. The attach slot and the other player's choice use the generic decision screens (`searchAttachTarget` is unknown to the client by construction; `shownChoice` is P25's).

## Shared lines edited (all else is in the new feature files)

| File | Edit |
|---|---|
| `DeckSearch.cs` | `using System.Text.Json.Serialization`; constants in `SearchDeck` (3), `SearchEffect` (7), `SearchStage` (2); `SearchCriteria` +`GoldProducing`, `DifferentTitles`; `SearchSpec` +`ChooseOne`, `SelectCount`, `OtherPlayerChooses`; `PendingSearch` +3 properties; `SearchView` +4 trailing nullable properties; `IsSearchChoice` +`searchAttachTarget`; `BeginSearch` +`ValidateSearchSpec(spec)`; `ResolveSearchCommand` +1 case; `SelectOrPassSearch`: the pass requirement also allows `SearchMayTakeNone`, +1 dispatch line to `SelectManySearch`, the default effect branch +`&& !SearchExtApplyEffect(...)`; `SearchPlace`, `SearchDeckList`, `ShuffleDeck` +the new sources; `SearchEligibility` +`GoldProducing`; `SearchGroups` +`SearchExtGate`; `FinishSearch` +`SearchExtPutOnBottom`; `BuildSearchView` wraps its two returns in `SearchExtView` and reads `SearchMayTakeNone`; `SearchActions` +2 cases |
| `DeckLook.cs` | `LookSpec` +`RevealUntil`, `MatchDisposition`; `LookRules(spec)` replaces `spec.Rules` at its four uses; one line in `OpenLookArrangement`; `OpenRevealUntil` |
| `SearchPlayGozu.cs` | `SearchPlayFinishSearch`'s first condition also accepts `SearchExtPlaysAfter(search)` |
| `EmperorRemainingE.cs` | `EnterSearchedPersonality`: `TakeControl` when the card is the other player's |
| `src/L5R.AI/TrainingOpponent.cs` | one case line: `selectSearchCards` and `chooseSearchAttachTarget` score 100 (the other player's `chooseShown` already took the default first-complete-option score) |
| Client | `DeckSearchPanel.cs` (multi-select draft, place names), `Main.DeckSearch.cs` (the confirm handler accepts the draft built from the engine's default `selectSearchCards`), `Main.SearchCapture.cs` (the probe scenario) |

Hook ids and priorities: `P20SearchOtherChoice` 22800 (`ShownChoiceApply`), `P20ShowcaseSearches` 22801. No new hook channel and no `EngineHookStartup` edit.

## Readings for Mendon to confirm or correct

- **A set is taken in one command, not pick by pick.** The searcher names every card at once; the engine validates the whole set (counts, titles, copies) and refuses it unchanged. The incremental alternative would have needed an undo.
- **A search is never impossible.** When the places hold fewer qualifying cards (or fewer different titles) than `Min`, the most that can be taken is required, and a pass is legal only when nothing qualifies (or `Optional`, or `Min` is 0). So "two attachments with different titles" with only one title available takes that one.
- **`OtherPlayerChooses` with a single card found:** nothing to choose, so that card takes the chosen disposition (Dark Audience's "another player chooses one to be removed" with one attachment found removes it).
- **A card from the other player's pile.** Only dispositions that put it into the searcher's control are allowed (`PutIntoPlay`, `CelestialIntoPlay`, `AttachToPersonality`) or that remove it (`RemoveFromGame`); it is controlled by the searcher while in play and returns to its owner's pile when it leaves (the Black Pearl rule, issue #352). It is never put in the searcher's hand.
- **`AttachToPersonality` pays no Gold.** The printed text says "attach", not "pay"; a card that says "paying all costs" would need a payment stage (Region's, `QuoteRegionGold`). It still obeys the attachment's own restrictions, the Armor/Weapon limits and Unique/Singular.
- **`RecruitFromProvinceIgnoringCosts` ignores the Honor Requirement as well as the Gold** ("ignoring costs"), but keeps Unique/Singular and any province entry lock. A face-down card found is revealed by entering play.
- **Taking a card from a province refills that province at once**, face down (a province refill takes the top of the Dynasty deck, and nothing is reshuffled by a province search). The second card of a two-slot province (Otosan Uchi) is listed and can be taken, but that slot is not refilled.
- **`Bottom` from a deck is after the shuffle**, so the searcher knows where the card is (Meeting the Keepers' shape for the top).
- **A province search lists definitions, not provinces.** The list does not say which province a card is in (a bot or a human picks by card); a card that needs the province can be added to the entry view later.
- **`RevealUntil` reveals publicly** through the shown-cards record, including when the look is of the opponent's deck, and a revealed non-matching card goes to the spec's `Remainder`.
- **A reveal-until match that is also the `Remainder`** adds no rule (the match is then treated like every other card).

## Not done

- No card is wired. Jade Pearl Inn still needs its own recruit trigger and the existing `RefillSourceProvinceFaceUp`; Dark Audience its attachment criteria and the removal wording; Unbound Essence the P22 overlay as a `MatchDisposition`.
- An `AttachToPersonality` or `RecruitFromProvinceIgnoringCosts` combined with a selection of several cards is refused by `ValidateSearchSpec` (an authoring error, `InvalidOperationException`): a set only applies plain dispositions (`ToHand`, `Bottom`, `Discard`, `RemoveFromGame`).
- The deck-search viewer is a plain tile list; no per-province grouping, no multi-copy stepper.
- Replay: the new decisions are driven by ordinary commands and a game saved in the middle of each resumes identically (tested through the JSON state); a replay of the command journal needs a card that starts the search, so none is tested until a wave wires one.
- Native checks (Windows, the shared Godot, this checkout): `tools/check-deck-search-ui.sh` (normal and reduced; it now ends with the province scenario: a private list of the viewer's own provinces, a one-to-two draft with different titles, explicit confirmation, a seat switch that drops the draft, one command, no shuffle), `tools/check-either-or-ui.sh` (both modes) and `tools/check-decision-chooser.sh` (all eleven modes) passed. This is a graphical run of the checkout, not Windows CI or expert playtesting.
- Core suite 3210/3210 (`search-extensions` 15/15), AI tests 98/98 (`bot-policy` 14/14 including `SearchExtensionsBot`, `bot-soak` 3/3), Server 9/9, client transport 19/19, collection 61 assertions; `check_format.py` and `lint_comment_swallow.py` clean.
