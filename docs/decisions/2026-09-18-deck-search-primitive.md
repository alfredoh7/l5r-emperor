# Deck search primitive: search a deck, select a legal card, show it, apply the effect, shuffle

Status: accepted for the engine (Mendon, standing authority; the rules lead's own specification, issue #312). The rules interpretations under "Open rules questions" were decisions the engine had to make to be complete; **Mendon has answered every one (issue #368)**, and the section below records each answer. Lane: shared (rules engine, wire contract; the two UIs are Alfredo's #314 and #315). Issue #312.

> **SUPERSEDED (Mendon, 2026-10-08, issue #757, R-033):** the issue #368 ruling below that the Terrain just found cannot be the card discarded is reversed for Ikoma Shizuka. The Terrain found MAY be discarded, as for Kitsuki Daisuke - exp2, Shinjo Tselu - exp and Asahina Kitiaru - exp (`SearchSpec.DiscardMayBeFound`); with no other card in hand it is the card discarded. The text below records the original #368 answer.


## Decision

One reusable, engine-authoritative flow serves every "search a deck" card. It is private to the searching player and always runs in exactly this order:

1. **Choose the deck** (only when the card names more than one: Ikoma Shizuka).
2. **Candidate list**: the searcher is shown the remaining cards of that deck, grouped by card with a count, each with an eligibility flag and a short reason.
3. **Select** one eligible card (the client confirms, the engine receives one command), **or Pass**, which is legal **only when no card qualifies** (or the card's printed text says "you may search").
4. **Show**: the chosen card is added to a public, append-only record both players see.
5. **Apply the card's own effect** to it (Region to a province, Terrain to the hand, Item to the hand ...) including any follow-up choice (which province, which card to discard).
6. **Shuffle** the searched deck. This also happens when the player passed.
7. The action **resolves** and play continues (priority to the opponent for a Limited action; the Events Phase reveal resumes for an Event).

The engine, not the client, decides eligibility: an illegal selection is rejected with the state unchanged. A player who is the searcher always has a legal, offered answer at every stage, so a bot or any legal-actions-driven client cannot deadlock.

## Data model (declarative, on the ability)

`src/L5R.Core/DeckSearch.cs`.

- `SearchSpec(Id, Label, Decks[], Optional)`: the whole search. `AbilityDefinition.Search` carries it for an ability; an Event looks it up by its `Effect` (`SearchCatalog.ForEventEffect`).
- `SearchDeckOption(Deck, Criteria, Effect)`: one searchable deck, the criteria its candidates must meet, and what happens to the chosen card. Ikoma Shizuka lists two (Dynasty/Region/attach-region, Fate/Terrain/to-hand-then-discard).
- `SearchCriteria(Noun, Types, Keywords (all of), ExcludedKeywords, NonUnique, Titles, Comparisons)` with `SearchComparison(Stat, Op, Value)`; Stat is Focus, GoldCost, Force, Chi or PersonalHonor, Op is `<=`, `>=`, `<`, `>`, `==`. `Noun` names what is searched for ("Region", "Terrain", "Item") and is used in prompts and in the per-entry reason ("Not a Region", "Unique cards cannot be chosen", "GoldCost 4, needs <= 2").
- Effects built: `to-hand`, `to-hand-then-discard`, `attach-region`. Adding one is a `case` in `SelectOrPassSearch` plus, if it needs a follow-up decision, a stage.
- `SearchSpec.Optional` (the printed "you may search"): Pass is legal even when a card qualifies. No published card is wired with it; the flag is tested through the training fixtures.

Training fixtures (never in a deck): `training-search-source` (three Limited searches: a cheap non-Unique Follower or Item, a low-Focus Strategy, an optional Weapon) and `training-costed-region` (a Region costing 2 Gold; every published Region costs 0).

## Commands (no new `GameCommand` field)

Deck search reuses existing fields, so `WireGameCommand`, `MatchSession.ToWire` and the persisted journal are unchanged and the #307 reflection tests still hold.

| Stage / PendingChoice type | Command `Type` | Fields | Notes |
| --- | --- | --- | --- |
| start | `useAbility` (Shizuka: `AbilityId = "shizuka-search"`) | `CardId` | Bows Shizuka as the cost |
| `searchDeck` | `chooseSearchDeck` | `Choice = "Dynasty"` or `"Fate"` | Both decks are always choosable, even an empty one |
| `searchSelect` | `selectSearchCard` | `Choice = the card's definition id` | Definition id, not an instance id: the list is grouped, and an instance id would be a handle into the deck |
| `searchSelect` | `passSearch` | none | Legal only when nothing qualifies (or the search is Optional) |
| `searchRegionProvince` | `chooseSearchProvince` | `Province`, `SourceIds` (the Gold payment) | Only provinces that are not destroyed and hold no Region |
| `searchDiscard` | `chooseSearchDiscard` | `TargetId = a card in your hand` | Any hand card EXCEPT the Terrain just found (issue #368); when none is left the stage is not opened at all (public event `searchDiscardSkipped`) |

Which copy of a card is taken when several remain: the one with the lowest instance id, so the choice never depends on deck order.

## What each player sees

| Information | Searcher | Opponent |
| --- | --- | --- |
| The candidate list (`MatchView.PendingSearch`) | yes | **no** (null) |
| Deck contents / the eligible entries | yes, only after choosing a deck (the deck choice lists counts only) | no |
| Deck order | **never** (entries are sorted by type, then name, then id) | never |
| The RNG state (`MatchState.RngState`) | never | never |
| "A search happened" (`searchStarted`, `deckSearched`, `searchPassed`, `deckShuffled` events) | yes | yes |
| The shown card (`MatchView.ShownCards`) | yes | yes (deliberately) |
| The card's instance id in events or the shown record | no | no |
| The public prompt of the pending choice | names the source card and the deck kind only | same |

A pass is public ("found no eligible card in their Fate deck"): that is the rule working as designed, since the opponent is meant to be able to verify the searcher followed it. An Optional search that is declined says "chose not to take a card" instead.

## The shown-cards record

`MatchView.ShownCards` (`ShownCardView[]`), identical in both players' views, append-only, part of the match state (so it survives a reconnect and a server restart):

`Sequence` (1, 2, 3 ... per match, the record's own key), `ShownByPlayerId`, `Definition` (the full `CardDefinition`, for the large card on hover), `SourceCardId` and `SourceName` (Ikoma Shizuka, Imperial Gift ...), `Deck` ("Dynasty"/"Fate"), `Context` ("Ikoma Shizuka, Fate deck search"), `TurnNumber`, `EventSequence` (the sequence number of the public `cardShown` event, to line the record up with the log).

**Decision: the shown card's instance id is not exposed.** The record is keyed by its own sequence number. An instance id would be a handle: the card may end up in a hidden hand, and ids of other hidden cards are guessable in sequence. The record needs only the definition to be reviewable, and the public event carries no card id either. This is asserted by test, in both the record and the opponent's whole view.

## The persisted, deterministic shuffle

- `MatchState.RngState` (a `ulong`) is the whole state of the existing SplitMix64 generator. Setup continues **the very sequence the seed started**: `Create` and `CreatePublished` store `random.State` after the four setup shuffles, and the code that shuffles at setup is unchanged. Every mid-game shuffle reads the value, shuffles through the same `StableRandom`, and writes the advanced value back (`ShuffleDeck`).
- Why replays and recorded setups are unchanged: setup consumes the generator exactly as before (a pinned test hashes each player's Dynasty order, Fate order, opening hand and province cards for three seeds on both the training and the published test scenario, recorded from `origin/main` before this change, and asserts `RngState == seed + 156 * 0x9E3779B97F4A7C15`, i.e. nothing but the four setup shuffles consumed it). `ReplayRecord` still holds `(ContentVersion, Scenario, Seed, Commands)`; a replay re-creates the setup and re-executes the commands, and each `Execute` works on a `Copy` that is discarded when the command is rejected, so the generator is only ever advanced by accepted commands.
- Server recovery: the server persists the room as that journal (seed plus accepted commands) and recovers with `MatchEngine.Replay`, so the shuffles are reproduced exactly. It never persists or sends a state snapshot. Proven end to end by `SearchAndShuffleSurviveRestart` (a real server, restarted mid-turn, the cards player 0 draws from the shuffled deck equal those of a match that never restarted).
- The state never reaches a view; a test asserts no view or wire envelope contains `rng` or the value.

## Ikoma Shizuka (personality-wave-27) and Imperial Gift (event-wave-3)

Both verified against the scans (the WebP working copies' sha256 match the receipts). Shizuka: Force 2, Chi 3, Honor Requirement 3, Personal Honor 2, Cost 5 (the top corners are Force and Chi; the bottom row is Honor Requirement, Cost, Personal Honor). The catalogue's "Search you Dynasty or Fate deck" is a typo; the scan prints "your". Imperial Gift's scan prints only "Gain 2 Honor and search your Fate deck for an Item, show it, and put it into your hand." (`Imperial * Unique`).

**Ikoma Shizuka.** Limited, bow Shizuka as the cost (a bowed Shizuka is rejected; the action is once per turn). Dynasty deck: criteria Region only; the Region is shown, then attached to a province of the player's choice that is not destroyed and has no Region, through the same `AttachRegionToProvince` the Events Phase reveal uses (so it is public and its static effects, e.g. Fallow Lands' +2 strength, apply at once); when no province can take it the Region is shown and goes to the Dynasty discard pile; the Dynasty deck is then shuffled. Fate deck: criteria Terrain only (a Strategy with the Terrain keyword); the Terrain is shown, put into the hand, the player discards a card **other than the Terrain just found** (issue #368: if no other card is in the hand nothing is discarded, with no error), the Fate deck is shuffled. Nothing qualifying: Pass, shuffle, the action resolves. After the search resolves, a "your action bowed a Personality" Reaction (Tamori Ruya) gets its window, as after any action.

**Region cost ("paying all costs").** A Region attached by a search pays its own printed Gold cost through the payment path merged in #307: `QuoteRegionGold` builds one Fixed `GoldQuote`; the `chooseSearchProvince` option carries it in `ActionOption.Gold` with the engine's suggested sources pre-filled in `Command.SourceIds`; `Pay()` validates and charges exactly the chosen sources and emits the one public `payment` event. A costed Region always asks for the province even when only one is free (the payment needs the player's sources); a free Region with exactly one free province attaches without a decision. An unpaid Region is never charged.

**Imperial Gift.** Wired: the trigger its Oracle text describes ("After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in one of them") already exists. That clause is boilerplate the catalogue prepends to every Event (event-wave-2's note says so, and the scan does not print it); an Event resolves when the Events Phase reveals it (`ResolveEvent`), which is that moment. `ResolveEvent` now gains 2 Honor, opens the Fate-deck Item search (the Event stays face up in its province while it resolves), and once the search has shuffled the Event is discarded, the province refills, and the reveal loop resumes.

Other Events that share the trigger, **not wired** (each is a different search, this only reports what it needs):

| Event | What it needs |
| --- | --- |
| Discovering the Temple | a Fate deck **or discard pile** search for a card titled Blood of the Preserver (Titles criteria exist; a discard-pile source does not) |
| Gaining Advantage | an **optional** search (the flag exists) for a non-Unique Strategy with a Political ability (an ability-classification criterion is not built), then put in hand and discard a card (the effect exists) |
| In Search of Guidance | a Dynasty search for a Sensei Personality and an effect "refill this province with him, face-up" (not built) |
| Signs and Portents | not a search at all: "look at the top four cards of any Fate deck. You may discard one of them. Put the rest back in any order" is the other family (top N, below) |

## The Events Phase reveal cursor (a fix that came with the work)

`ContinueEventsReveal` used to restart at province 0 after a deferred Event (Harsh Choices), so the face-down card the Event's own province had just refilled with was **revealed in the same Events Phase**, and resolved if it was an Event. `MatchState.EventsRevealNext` now resumes from the next province. This changes Harsh Choices' declined path (its refilled card stays face down, as ordinary Events have always behaved) and is pinned by a test.

## The 44 catalogue cards: does Pass-only-when-none hold?

Read all 44 texts. It matches every card except two printed optional searches: **Gaining Advantage** ("you may search your Fate deck ...") and **Wisdom Gained** ("each player may search ..."), which need `SearchSpec.Optional`. Other shapes the family needs that this does not do yet, so the next wirer knows:

- **Discard pile sources** (16 cards) and **multi-source** searches ("your discard pile, then Fate deck", "hand, then discard pile, then Fate deck", Asako Chukage's Provinces): a spec with several sources searched in order, and a candidate list that names where each card is.
- **Destinations other than hand/attach**: put into play (Peace, Sacrifice of Pawns, Asako Usohara, I Give You My Name), "put it into play or your hand" (Asako Chukage, Celestial Sword of the Dragon, Togashi's Guidance), refill a province face-up (Kitsu Miwa, Jade Pearl Inn, Otomo Suikihime, Shinjo's Guidance, In Search of Guidance), "take an additional action to play it" (Death of the Winds, Gozu), on top of the deck after shuffling (Meeting the Keepers).
- **Comparisons against another card** (Refuge of the Three Sisters: Focus lower than the Personality's Chi; I Give You My Name; Delving into History: shares a keyword with the performer; Versatile Farm: another title): `SearchComparison` takes constants only.
- **Two cards / an opponent's choice** (Dark Audience: two attachments with different titles, another player removes one).
- **No criteria at all** (Heart of Fudo, Introspection, Walking the Way: any card; Pass is then legal only for an empty deck).
- Explicit "Shuffle your Fate deck" (The Seekers' Temple) is what this flow already does for every search.

## The "look at the top N cards" family (25 cards): what carries over

Not built. It is a different primitive (no search, no shuffle unless printed, and the order of the top N is legitimately visible to the looker). What this work gives it: the `PendingSearch` + `Stage` + `PendingChoice` shape for a private multi-step decision; the criteria evaluator (`SearchEligibility`) for "take a card matching X"; the public shown-cards record for "reveal it"; `ShuffleDeck` and the persisted generator for any "shuffle"; the hidden-information test pattern (the searcher's own view is asserted free of instance ids too). What it would still need: a view of the top N **in order** with positions (a new private view next to `SearchView`, and its own leak tests), and an ordering stage ("put the rest back in any order").

## Fields for the two UIs (Alfredo)

The UIs read these and compute nothing. All are on the `MatchView` the client already receives (no `MatchSession` mapping is needed; `SearchViewsCrossTheWire` fails if a `MatchView` property is ever missing from the wire).

**#314 deck viewer and legal-card selection.** `MatchView.PendingSearch` (`SearchView`, non-null only for the searcher while a search is open):

- `Stage`: `ChooseDeck`, `Select`, `ChooseProvince`, `ChooseDiscard`. Also `MatchView.PendingChoice.Type` is one of `searchDeck`, `searchSelect`, `searchRegionProvince`, `searchDiscard`.
- `SourceCardId`, `SourceName`, `Label`, `Prompt`: which card started it and what is being asked.
- Stage `ChooseDeck`: `DeckChoices[]` = `{Deck, Label, CardsRemaining, Criteria}`. No contents yet.
- Stage `Select`: `Deck`, `Criteria` (the noun), `CardsRemaining`, `Entries[]` = `{DefinitionId, Definition, Count, Eligible, Reason}` (grouped by card, sorted by type then name; `Definition` for the hover preview; `Reason` is the short disabled-tooltip text, null when eligible), `CanPass` (true only when nothing qualifies, or `Optional`), `Optional`.
- Stages `ChooseProvince` / `ChooseDiscard`: `Selected` (the shown card's definition); the choices are ordinary actions.
- Actions (`QueryActions`): `chooseSearchDeck` (`Command.Choice`), `selectSearchCard` (`Command.Choice` = `Entries[i].DefinitionId`; **eligible entries only**), `passSearch` (only when `CanPass`), `chooseSearchProvince` (`Command.Province`, `Command.SourceIds`, `ActionOption.Gold` = the quote for the existing payment panel when the Region costs Gold), `chooseSearchDiscard` (`Command.TargetId`). The client sends the command it is offered; "confirm" is client-side.

**#315 shown-cards frame.** `MatchView.ShownCards` (`ShownCardView[]`, both players, append-only): `Sequence`, `ShownByPlayerId`, `Definition`, `SourceCardId`, `SourceName`, `Deck`, `Context`, `TurnNumber`, `EventSequence`. A new entry appearing is the "something was shown" signal; the public `cardShown` event has the same `EventSequence`. There is no instance id and no lookup that resolves another hidden card.

Until #314 lands the shipped client can open a search (Shizuka's action is offered on her card) but cannot answer it: an unknown pending choice renders as "REACTION WINDOW" with the engine's prompt and no buttons for these action types. Shizuka is in no pinned deck; a custom deck (#262) containing her is the way to reach it.

## Rules questions: answered by Mendon (issue #368)

Every question below was answered; where the answer changed the engine, the change and its test are named.

1. **An unaffordable Region** -- **answered: correct as built.** A found Region that cannot be attached or paid for is discarded (shown first, nothing charged). Every published Region costs 0, so no card is affected today.
2. **A Region found when no province can take it is still "found"** -- **answered: correct as built** (same ruling: a Region that cannot be attached is discarded).
3. **The deck is shuffled after every search**, including one that found a Region and attached it -- **answered: correct as built.** A search always shuffles; a look does not (see the look decision).
4. **"Discard a card" after a Terrain** -- **answered, CHANGED.** The found Terrain may NOT be discarded to its own "discard a card": a Terrain played is placed in play and is no longer in the hand. The engine excludes the found Terrain (that one instance -- a second copy already in the hand may be discarded) from the offered discards (`QueryActions` / `SearchView`) and refuses it if sent. When no other card is in the hand the discard cannot be satisfied: **nothing is discarded, there is no error and no stage to answer**; the search emits the public `searchDiscardSkipped` event, shuffles, and the action resolves. Tests: `TerrainCannotBeDiscardedToItsOwnEffect` (offered and accepted discards through `QueryActions`, the refusal leaves the state untouched, a second copy, exactly one other card, the empty case) and the updated `FateTerrainToHandAndDiscard`; mutation-checked (the Terrain offered again, the command no longer refusing it, the empty case still opening the stage).
5. **Both decks are always choosable, even an empty one** -- **answered: correct as built** (the search then ends in Pass and a shuffle).
6. **Imperial Gift resolves when revealed**, one province at a time like every other Event -- **answered: correct as built.**
7. **Which identical copy is taken** (the lowest instance id) -- not put to Mendon: no rules effect, identical cards in a deck are interchangeable. Stands.
8. **Order Seppuku** -- **answered, CHANGED, and generalized beyond a search.** Seppuku is an Open action, available only as an action on a player's own turn to act during the Action phase (unless a card allows it in another phase; no wired card does). It may not interrupt other players' actions, the current player's action, or any pending decision or reaction window. It was refused only while a search or look was open; it is now governed by ONE predicate for every case, `MatchEngine.SeppukuTimingReason` in `src/L5R.Core/SeppukuTiming.cs`: the Action phase, the player holds priority, and nothing pending. The full rule and its consequences are in the look decision (question 7), which is where the tests are.

## Things found on the way (not changed)

- `MatchQueries.cs` never mentions thirteen pending-choice types (`bowReaction`, `moveReaction`, `nikutaiReaction`, `unimpeachableNameReaction`, `duelReaction`, `duelWonReaction`, `beforeBattleOrder`, `encircledOrder`, `encircledSelect`, `brashDraw`, `overconfidentDraw`, `harshChoices`, `timesOfStrifeDiscard`), so `QueryActions` offers the player deciding in those windows nothing but Concede: the training AI or any legal-actions-driven client would stall there (the tests submit those commands by hand). The search choices are fully covered, and the AI suite proves it. Not changed here.
- `ClientConnection.ReceiveAsync` (which the server tests use to read) caps a message at 32 KiB. That limit is on what a client may **send**; server state messages are not capped and the client reads up to 4 MiB. A state carrying a deck viewer can exceed 32 KiB (a view is already about 27 KB in the fixture; each distinct card in the deck viewer adds about 1 KB because every entry carries its `CardDefinition`, 8.5 KB for the fixture's 9 entries, so a real 40-card deck of 20 to 30 distinct cards adds 20 to 30 KB), so the search server test reads with the client's own limit. If payload size ever matters, `SearchEntryView.Definition` can be dropped for a `DefinitionId` the client resolves from its own catalogue.

## Verification

`dotnet run --project tests/L5R.Core.Tests` (22 deck-search tests, each mutation-checked: pass always legal, selection unchecked, no show, no shuffle, the show carrying an instance id, the deck viewer visible to the opponent, the generator not persisted, a non-deterministic shuffle, the reveal cursor removed, a Region validated but never charged); `dotnet run --project tests/L5R.AI.Tests` (the training AI answers every stage and plays whole AI-vs-AI matches in the fixture, with replay equality); `dotnet run --project tests/L5R.Server.Tests` (a real server restarted mid-turn); `dotnet run --project tests/L5R.Client.Transport.Tests` (every `MatchView` property and every search action over the server envelope); `bash tools/check.sh --skip-client`.

Files: `src/L5R.Core/DeckSearch.cs`, `MatchEngine.cs`, `MatchQueries.cs`, `CardAbilities.cs`, `GoldQuotes.cs`, `Models.cs`, `CardCatalog.cs`, `PublishedContent.cs`, `PublishedMatch.cs`, `src/L5R.AI/TrainingOpponent.cs`, `content/published/personality-wave-27.json`, `content/published/event-wave-3.json`, `tools/content/package_personality_wave_27.py`, `tools/content/package_event_wave_3.py`, `tests/L5R.Core.Tests/DeckSearchTests.cs`.
