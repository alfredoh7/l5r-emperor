# Look at the top N cards: view, arrange, put back

Status: accepted for the engine (Mendon, standing authority; the rules lead's own specification, issue #322). The rules interpretations under "Rules questions" were decisions the engine had to make to be complete; **Mendon has answered every one (issue #368)**, and the section records each answer. Lane: shared (rules engine, wire contract; the UI is Alfredo's #323). Issue #322. Stacked on the deck search primitive (#312 / PR #328).

## Decision

One reusable, engine-authoritative flow serves every "look at the top N cards" card. It is private to the looking player, has **no shuffle** and never uses the random generator, and always runs in this order:

1. **Choose the deck** (only when the card offers more than one: Signs and Portents offers either player's Fate deck).
2. **Arrange**: the viewer is shown the top N cards, top first (fewer when the deck is short), each with the dispositions that card may take, and the count each disposition must respect across the whole group.
3. The viewer submits **one arrangement** (`arrangeTopCards`): a disposition for every viewed card, plus the order the cards that go back into the deck take. The engine checks all of it; an illegal arrangement is rejected and the state is unchanged.
4. The engine applies it: "show" dispositions append to the public shown-cards record, cards go to the hand, a discard pile or the bottom, the rest return to the top, and the action resolves (priority to the opponent for a Limited action; the Events Phase reveal resumes for an Event).

A deck with no cards left resolves the look at once (a public "no cards remain" event, nothing to answer). The viewed count is `min(N, cards left)`, and the engine never sends more than N cards.

## Data model (declarative, on the ability)

`src/L5R.Core/DeckLook.cs`.

- `LookSpec(Id, Label, Targets[], Count, Rules[], Remainder, Reorder)`: the whole look. `AbilityDefinition.Look` carries it for an ability; an Event looks it up by its `Effect` (`LookCatalog.ForEventEffect`).
- `LookTarget(Deck, Opponent)`: a searchable deck: `Fate` or `Dynasty`, the viewer's or (`Opponent = true`) the opponent's. Own Fate, own Dynasty, own choice of the two, ANY Fate deck and any of the four decks are all just lists of targets.
- `LookRule(Disposition, Min, Max, Criteria?)`: an optional or required disposition. `Criteria` is the **same** `SearchCriteria` the deck search uses ("a Political Strategy", "an Item", stat comparisons, titles), evaluated by the same `SearchEligibility`.
- `Count`: N. A caller may pass a different N to `BeginLook(..., count:)` for a conditional count ("three, or four if you bowed a Ninja"): the ability decides, the spec carries the default.
- `Remainder`: where a viewed card no rule claims goes: `top` (the usual "put them back") or `bottom` ("put the other three on the bottom").
- `Reorder`: the cards that go back into the deck may be put in any order. When false each group keeps its viewed order and any other order is rejected.

Dispositions: `top` (keep on top), `bottom` (bottom of the deck), `hand` (put in your hand), `show` (show it, publicly, and put it in your hand), `discard` (its owner's discard pile). **Hand and show are legal only for the viewer's own deck**; a card taken from the opponent's deck may be discarded, kept on top or sent to the bottom. A rule's effective min and max are capped by how many of the viewed cards can legally take it (a rule that requires "one Political Strategy" is not impossible when none of the three cards qualifies: it is then 0 to 0).

Deck layout afterwards: the `top` group in the order given, then the rest of the deck untouched, then the `bottom` group in the order given (the last one is the very bottom).

Dispositions **not built**: effect hand-offs where the looked-at card is recruited, attached or played into the existing flow. See "Reaching the other cards".

## Commands (no new `GameCommand` field)

The look reuses existing fields, so `WireGameCommand`, `MatchSession.ToWire` and the persisted journal are unchanged and the #307 reflection tests still hold. `LookViewsCrossTheWire` (transport) and `LookSurvivesRestart` (server) prove it.

| Stage / PendingChoice type | Command `Type` | Fields | Notes |
| --- | --- | --- | --- |
| start | `useAbility` (Public Records, Isawa Tamaki, Banish All Doubt, Bayushi Kahoku: their own `AbilityId`) or an Event reveal | `CardId` | A Limited action; Public Records bows the Holding as its cost |
| `lookDeck` | `chooseLookDeck` | `Choice = "Fate"` or `"Dynasty"`, `TargetPlayerId` = the deck's owner | Both fields together name one deck, so two decks of the same kind are unambiguous |
| `lookArrange` | `arrangeTopCards` | `CardIds` = one token per viewed card, `"<position>:<disposition>"` | Position 0 is the top of the deck. The order of the tokens is the order the cards go back in |

Example, top four cards of the opponent's Fate deck (Signs and Portents), keep the third on top, discard the first, put the second at the bottom, keep the fourth: `["2:top", "0:discard", "3:top", "1:bottom"]`. Cards are named by **position**, never by instance id: an instance id is a handle into a hidden deck.

## What each player sees

| Information | Viewer | Everyone else (deck owner, opponent) |
| --- | --- | --- |
| The viewed cards, in order, with their definitions (`MatchView.PendingLook.Cards`) | yes, and only the top N | **no** (`PendingLook` is null in their view) |
| The same when the viewer looks at the OPPONENT's deck | yes | **no**: the deck owner is told only by public events that a look happened and how many cards |
| The remaining deck order beyond the top N | never | never |
| A viewed card's instance id | never (positions only) | never |
| The deck choice (`DeckChoices`) | yes: whose deck, its size (already public) | no |
| "A look happened" (`topCardsLooked`, `topCardsArranged` events) | yes | yes, with counts and no card names |
| A discarded card (`cardDiscarded` event) | yes | yes: a discard pile is public, so the event names the card and carries its id |
| A shown card (`MatchView.ShownCards`, `cardShown`) | yes | yes (deliberately), no instance id |
| The RNG state | never | never (and a look does not touch it) |

`topCardsArranged` reports counts only ("2 returned to the top, 1 put on the bottom, 1 discarded"), never which card went where, so it cannot reveal the order.

## Fields for the UI (Alfredo, #323)

All on the `MatchView` the client already receives (`MatchView.PendingLook`, `LookView`, trailing optional parameter after `ShownCards`; no `MatchSession` mapping is needed and `LookViewsCrossTheWire` fails if a `MatchView` property is ever missing from the wire). The UI reads these and computes nothing.

`MatchView.PendingLook` (`LookView`, non-null only for the viewing player while a look is open). `MatchView.PendingChoice.Type` is `lookDeck` or `lookArrange`.

- `Stage`: `ChooseDeck` or `Arrange`.
- `SourceCardId`, `SourceName`, `Label` ("Look at the top four cards of any Fate deck"), `Prompt`: which card started it and what is being asked.
- Stage `ChooseDeck`: `DeckChoices[]` = `{Deck, OwnerId, OwnDeck, Label}` (deck sizes are no longer sent; see 2026-10-13-no-deck-counts). No contents yet. The choice command is `chooseLookDeck` with `Choice = Deck` and `TargetPlayerId = OwnerId`.
- Stage `Arrange`:
  - `Deck`, `DeckOwnerId`, `OwnDeck` (false when looking at the opponent's deck: the UI should say so), `Requested` (N); `Cards.Length` is smaller than `Requested` when the deck is short (the deck size itself is not sent).
  - `Cards[]` = `{Position, DefinitionId, Definition, Dispositions[]}`, top first. `Definition` is the full `CardDefinition` for the hover/large card. `Dispositions[]` = `{Disposition, Label, Legal, Reason}`: every disposition the spec offers for the look, and whether **this** card may take it (`Reason` is the short disabled-tooltip text, null when legal, e.g. "Not a Strategy", "Only cards from your own deck may be put in your hand").
  - `Rules[]` = `{Disposition, Label, Min, Max}`: how many cards must and may take each optional disposition, already capped by the viewed cards. "Choose exactly one to put in your hand" is `Min = Max = 1`. `Min = 0, Max = 1` is "you may". A rule with `Max = 0` cannot be used on this look and should not be shown.
  - `Remainder` (`top` or `bottom`): where a card goes when the player names no other disposition for it. `CanReorder`: whether the cards going back into the deck may be dragged into a different order (when false, sending them in the viewed order is the only legal arrangement).
- The command to send is built by the client from the view: one token per card, `"<Position>:<Disposition>"`, ordered as the player arranged them. **Confirm is a client step**; the engine receives exactly one `arrangeTopCards`.
- Actions (`QueryActions`): `chooseLookDeck` (one per deck, `Command.Choice` and `Command.TargetPlayerId`) and, at the arrangement, `arrangeTopCards` options: every legal arrangement that keeps each group's viewed order (bounded at 64, simplest first: the whole set back on top in the given order comes first when that is legal). These exist so a bot or any legal-actions-driven client can always answer; a full UI that lets the player reorder builds its own command from `PendingLook` and the engine validates it. An illegal command comes back as `illegal_action` with the reason and no state change.
- `MatchView.ShownCards` for a "show" disposition: as for a search (`Sequence`, `ShownByPlayerId`, `Definition`, `SourceCardId`, `SourceName`, `Deck`, `Context` "Bayushi Kahoku, Fate deck look", ...). A new entry is the "something was shown" signal.

Until #323 lands the shipped client can open a look (the ability is offered on the card, Signs and Portents resolves in the Events Phase) but cannot answer it: an unknown pending choice renders as "REACTION WINDOW" with the engine's prompt and no buttons for these action types. A look started by an Event therefore stalls the shipped client's Events Phase until the UI lands, as Imperial Gift's search does; none of the five cards is in a pinned deck.

## The cards wired (one for every parameter)

Every card was read against its scan (the WebP working copies' sha256 match the receipts) and every wave number is free on origin/main and in every open PR's file list (personality 24/25/26/27, follower-wave-15 and event-wave-3 are claimed; strategy-wave-16 is merged).

| Card | Wave | Deck | N | Dispositions |
| --- | --- | --- | --- | --- |
| **Public Records** (oracle-l5r-6095, Singular Holding, cost 2, "Bow this card: Produce 2 Gold. Limited: Bow this card: Look at the top three cards of your Fate deck. Put them back in any order.") | `holding-wave-2` | own Fate | 3 | keep on top, **reorderable** (the pure reorder case). The catalogue has no GoldProduction for it, so the packager sets 2 from the scan |
| **Isawa Tamaki** (oracle-l5r-3918, Force 4, Chi 3, HR 5, cost 7, PH 2, "Limited: Look at the top card of your Fate deck. You may put it at the bottom of the deck.") | `personality-wave-28` | own Fate | 1 | keep on top or **bottom** (one) |
| **Banish All Doubt** (oracle-l5r-690, Tactical Strategy, Focus 3, "Limited: Choose your performing unbowed Tactician: Look at the top four cards of your Fate deck. Put one of them in your hand and put the other three on the bottom of your deck in any order.") | `strategy-wave-17` | own Fate | 4 | **exactly one to hand**, the **remainder to the bottom, reorderable** |
| **Bayushi Kahoku, Experienced 2** (oracle-l5r-10479, Coils of Madness, Force 5, Chi 4, cost 9, Unique, "Look at the top three cards of your Fate deck. You may show one of them that is a Political Strategy and put it in your hand. Put the remaining cards back in any order.") | `personality-wave-28` | own Fate | 3 | **show and take** one with a Type and Keyword criterion, rest on top in any order |
| **Signs and Portents** (oracle-l5r-7253, Promotional Celestial Event, Unique, "Look at the top four cards of any Fate deck. You may discard one of them. Put the rest back in any order.") | `event-wave-4` | **any Fate deck, including the opponent's** | 4 | **discard** (one), the rest back on top in any order; the Event resolves during the Events Phase reveal and is then discarded |

Text overrides (the scan differs from the catalogue): Tamaki (no "Void" prefix; "Limited:" is printed), Banish All Doubt, Public Records and Signs and Portents (the catalogue's "After you reveal cards ..." boilerplate is not on the scan). All five are verified; Kahoku's text matches the catalogue.

Banish All Doubt is played from the hand, so it is discarded when it is played (the look defers the action and skips the normal tail); the Tactician it names is the unbowed performing Personality with the Tactician keyword, through the existing performer-selection path.

Training fixtures (never in a deck): `training-look-source` with abilities covering own Dynasty; any of the four decks with discard, up to two to the bottom and one to hand together; a required Item to hand; a fixed order (no reordering); and a caller-supplied N (spec N = 3, ability N = 2). These carry the parameters ahead of any published card needing them.

## Reaching the other cards (25 in the catalogue; 5 wired here, 20 not)

Every card whose text says "look at the top ..." was read from the catalogue. Nothing below is wired. "Unblocked" means the look is a `LookSpec` plus an ordinary Limited ability (or the cost and trigger machinery that already exists); "needs a piece" names exactly one thing that is missing and is **not** a look question unless it says so.

**Unblocked (the look and its ability are expressible today):**

| Card | Look spec |
| --- | --- |
| Akodo Uehara (305) | own Fate, N = 1, discard 0-1 and bottom 0-1 (leave, discard or bottom). Its +1 Force against a Phoenix opponent is separately wirable |
| Jurojin's Blessing (4070) | own Fate, N = 1, discard 0-1. Its Open half (retrieve a non-Unique Strategy from the Fate discard pile) is a separate discard-pile retrieval |
| Lantern of Revelation (10413) | own Fate or Dynasty (two targets), N = 1, bottom 0-1 |
| Divination Bowl (10690) | own Fate, N = 3, reorder only, bow as the cost. Its second Limited (draw a card, destroy the Item) is separate |
| Lessons from the Heralds (10736) | own Fate, N = 3, hand exactly 1 (no criteria), rest back in any order. Needs its condition "one of your Personalities has won a duel this turn", which is not tracked anywhere yet |
| It Was a Cat (10415) | any of the four decks, N = 3 (4 if a Ninja was bowed: the caller passes the count), discard 0-1, rest any order. Needs its cost (bow a target unbowed Personality or Follower) and the Ninja Open timing |

**Look expressible; the trigger or condition is what is missing:**

| Card | What is missing |
| --- | --- |
| Asako Kikugoro (556) | own Dynasty, N = 2, bottom 0-1. Needs the reaction "after your Ring or Henshin enters play or overlays" |
| Witness the Untold (10357) | own Fate or Dynasty, N = 3, reorder. Needs "after your Ring enters play" |
| Doji Kazuo, Experienced (10282; published in personality-wave-13 with no ability for this text) | any Fate deck, N = 3, reorder. Needs "after your End Phase begins, if Kazuo won a duel or you gained 4 or more Honor this turn" |
| Daigotsu Tenbatsu (1778) | own Fate, N = 3, reorder. Needs "after engaging at Tenbatsu's battlefield" and the follow-up "you may discard a Dark Virtue; if you did, draw a card" |
| Rokkaku, Master of Scrolls (10483) | own Fate, N = 4, bottom 0-1, rest any order. Needs the Void condition and "enters play ignoring Gold Cost if you control the Ring of the Void" |
| Tonbo Inuyama (10478) | any of the four decks, N = 1, bottom 0-1. Needs the Void condition |
| Seeking the Question (6565) | any of the four decks, N = 1, bottom 0-1 (`TrainingAny`'s shape). Needs the Kata plumbing ("if this card is in play", "put this card into play, discard your other Kata") |
| Methodical Questioning (9915) | any Fate deck, N = 3, reorder. Needs the optional bow of the performing Samurai and "put this card into your hand from your discard pile" |
| The Tapestry Perceived (8371) | any Fate deck, N = 1, discard 0-1. Needs the discard to be linked to a side effect ("you may destroy this card; if you do, discard the card you looked at") and the Void hand-size static |

**Needs one more piece for the look itself:**

| Card | What is missing |
| --- | --- |
| Control Your Destiny (1489) | the look is a `LookSpec` (own Fate or Dynasty, N = 3, bottom 0-2, the rest on top in any order); the cost "Bow this Shugenja unless he is Void" and a Limited ability on an attached Spell whose performer is its wielder are separate |
| Kitsuki Daisuke, Experienced 2 (10452) | an Iaijutsu classification criterion (the same gap Gaining Advantage has for Political) and a follow-up "then discard a card" stage after the show (the deck search has the same follow-up for Shizuka's Terrain) |
| Temples of Gisei Toshi (7898) | "name Follower, Item or Spell" before the look: the show rule's criterion must be chosen at runtime (a pre-choice stage feeding `LookRule.Criteria`). It also needs `GoldProduction = 2`, which the packager can set as it does for Public Records |

**Effect hand-off dispositions (recruit, attach, play the looked-at card)**: Searching for Answers (6526: recruit a Personality from the Dynasty top four, ignoring Gold cost, Absent Battle Defender only), Bayushi Nitoshi (10248: attach an attachment to a Personality at his location, or take an additional action to play a Strategy). Not built: the looked-at card must enter the existing recruit / attach / play flow after the look, and the Absent Battle and Defender timing needs plumbing that does not exist. The shape to add is a further disposition (`recruit`, `attach`, `play`) whose application opens the existing pending flow for that one card, exactly as the search's `attach-region` effect does for a Region. Nothing in the primitive prevents it.

Corrections to the #312 decision doc's note on these cards: it lists Signs and Portents under searches; it is a look and is wired here (event-wave-4).

## Rules questions: answered by Mendon (issue #368)

1. **Hand and show only from your own deck** -- **acknowledged, no change.** A card looked at in the opponent's deck may only be kept, bottomed or discarded.
2. **Discarding a card from the opponent's deck** puts it in its owner's Fate discard pile, publicly -- **correct, no change.**
3. **An empty deck resolves the look at once; fewer than N shows what is left** -- **correct, no change.**
4. **N is conditional through the caller** -- **noted, no change.**
5. **Every card must be named in the arrangement** -- **correct, no change.**
6. **Signs and Portents resolves when revealed** -- **correct, no change.**
7. **Order Seppuku** -- **answered, CHANGED (and generalized beyond a look or search).** See "Order Seppuku" below.
8. **A look does not shuffle; a search shuffles** -- **correct, no change.** If a look card ever instructs a shuffle, the shuffle happens after everything before it in the text has resolved (no wired card does; a look would then call `ShuffleDeck` at the end of `ApplyArrangement`, before the action resolves).

The three questions this work had carried over from the deck search (#312) are answered in that decision (`2026-09-18-deck-search-primitive.md`): an unaffordable Region is discarded (correct), Imperial Gift resolves on reveal (correct), and the Terrain just found may NOT be discarded to its own "discard a card" (changed).

### Order Seppuku (issue #368)

Seppuku is an OPEN action, available only as an action on a player's own turn to act during the Action phase (unless a card allows it in another phase). It may not interrupt other players' actions or the current player's action. Before, the engine let it "cut through every other pending choice" and refused it only while a search or look was open.

**One predicate, `MatchEngine.SeppukuTimingReason(playerId)` in `src/L5R.Core/SeppukuTiming.cs`** (null when it is that player's time, otherwise the reason). It is available only when ALL of these hold:

- **(a)** the phase is the Action phase. No wired card lets Seppuku be ordered in another phase (no published card mentions seppuku at all; a tripwire test fires the day one does, so its text is read and its allowance added beside the phase test);
- **(b)** the player holds priority: it is their turn to act (the Action phase alternates the opportunity; the active player is not the test);
- **(c)** nothing is pending: `PendingChoice` (every reaction window and every Overconfident / Brash / Harsh Choices / Times of Strife / search / look / copy / Either/Or / Engage / before-resolution decision) and the other decision-holding states named in `DecisionStates` (a pending Attack, Conqueror or duel, a deferred action or announced action, a queued draw or discard, a set-aside window ...). A guard test fails when a new `Pending*` field is added to `MatchState` without being classified in that file.

The three entry points all use it, so what is offered is what is accepted: the command handler (`MatchEngine.OrderSeppuku`), the offered actions (`QueryActions`; an unavailable Seppuku is not listed, while the card-level reasons stay listed-disabled as before) and, through `QueryActions`, the training AI (which never picks it) and the client. What is judged separately, unchanged: the card (Dishonorable, Samurai / Courtier / Shugenja, in play, not locked from acting -- #336) and the pass owed to Unfortunate Hesitation (#351/#356), which is checked beside the predicate, never instead of it.

**Consequence to know about:** as an ACTION, Seppuku now hands the opportunity to the other player once it has resolved (an Overconfident window it triggers closes to that player), exactly as every other action does. It used to leave priority where it was, which let a player order any number of Seppukus in a row on one opportunity.

**Existing tests adapted because the ruling demands it** (each listed in the PR): `core/OrderSeppukuIsOpenAction` (was "available without priority"), `negated-costs/SeppukuBarred` (the without-priority case and the after-the-pass case), `action-lock/InnateActionSeppukuIsBarred` (the lifted-lock case needs the turn to act back), `deck-search/SeppukuCannotInterruptASearch` (it was "available again once the search is over"). The descriptions of the deck-look, either-or and card-copy Seppuku tests were reworded; their assertions did not change.

## Things found on the way (not changed)

- **holding-wave-1** packages four Holdings that print "Bow this card: Produce N Gold" (Archery Range, Slanderer, Temple of Hotei, Tranquil Garden) with GoldProduction 0. That is an existing content gap, not fixed here; Public Records sets its own GoldProduction from the scan.
- `MatchQueries.cs` still offers nothing for ten pending-choice types (thirteen when #312 was written; #324 covered beforeBattleOrder, encircledOrder and encircledSelect; the rest are listed in the #312 decision). The look and search choices are fully covered.
- `ValidateAbility` already requires the Performer to be in play, so Banish All Doubt needs no extra in-play check (the enumeration in `AbilityActions` is slightly looser, which is harmless because the command validator rejects it).

- **Later windows (after #324 and #327 merged into main).** The multi-reaction before-action window (#327) opens *before* a Limited search or look resolves (Unimpeachable Name is offered against any action): the whole command is deferred, nothing is bowed, paid or looked at while it is open, and the same command re-executes into the search or look once both players pass. `BeforeActionWindowAheadOfLookAndSearch` pins this for a look, for a look after a reaction was used, and for a search (Shizuka's bow cost is paid only after the window). The Engage window (#324) is a Battle Phase step; every search and look ability is Limited (Action Phase) or an Event reveal, so the two never overlap.
- **Known gap, not changed:** the shared tail of a deferred search or look (`FinishDeferredAction`) opens the bow-reaction window, the Overconfident window and hands priority on, but does not run the other steps at the end of `RunAbilityEffects`: "after the resolution of your [keyword] action" traits (Daidoji Ebizo, Master Scouts), Apprentice Shinobi's "Ninja action" bonus, forfeiting pre-existing Additional Action grants, and the Legion of the Bat / Nikutai windows. A deferred Attack and a duel have the same shape (see the #325 handoff). None of the seven cards this branch wires carries the Recon or Ninja keyword, so nothing observable changes; a future search or look card that does would need those steps called from the tail (the ability id would have to be stored on `PendingSearch`/`PendingLook`).

## Verification

`dotnet run --project tests/L5R.Core.Tests` (15 deck-look tests, each mutation-checked: min/max not enforced, hand allowed from the opponent's deck, order restriction ignored, the look visible to the opponent, one card too many sent, a shuffle added, the bottom group put on top, the show not recorded); `dotnet run --project tests/L5R.AI.Tests` (the training AI answers every stage for five abilities, four decks and three seeds, including the opponent's decks; withholding the arrangement actions stalls it and fails the test); `dotnet run --project tests/L5R.Server.Tests` (`LookSurvivesRestart`: a real server, restarted mid-look at the OPPONENT's deck, the arrangement accepted over the wire and surviving a second restart, the owner's envelope never carrying the cards); `dotnet run --project tests/L5R.Client.Transport.Tests` (every `MatchView` property, `chooseLookDeck` and `arrangeTopCards` over the server envelope, the client's deserialization identical to the engine's, and a `WireGameCommand` round trip); `bash tools/check.sh --skip-client`.

The pinned published count is 407: 400 on origin/main (ddb58847, counted from the JSON files) plus 7 from this branch (2 from the deck search, 5 from the look). It must be re-derived from the actual base whenever main moves.

Files: `src/L5R.Core/DeckLook.cs`, `DeckSearch.cs`, `MatchEngine.cs`, `MatchQueries.cs`, `CardAbilities.cs`, `Models.cs`, `CardCatalog.cs`, `PublishedContent.cs`, `L5R.Core.csproj`, `src/L5R.AI/TrainingOpponent.cs`, `content/published/{event-wave-4,personality-wave-28,strategy-wave-17,holding-wave-2}.json`, `tools/content/package_{event_wave_4,personality_wave_28,strategy_wave_17,holding_wave_2}.py`, `tests/L5R.Core.Tests/DeckLookTests.cs`.
