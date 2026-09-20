# Kitsu Akai and the Lion Ancestor: the first "create a card" primitive

**Decision:** Kitsu Akai and the Lion Ancestor (Second City, `oracle-l5r-4371`/`oracle-l5r-9785`,
Lion Clan) are wired in conjunction, via the standard content-wave pipeline
(`tools/content/package_personality_wave_17.py`). Kitsu Akai's Limited action is the engine's first
"create a card" effect — every prior "Create..."-shaped ability (`CreateToken`, exhaustively
searched) only ever adds a stat marker to a `CardInstance` that already exists; nothing before this
spawned a brand-new one at runtime. `new CardInstance` previously occurred at exactly two call
sites in the whole codebase, both inside `MatchEngine.Create`/`PublishedMatch`, at match setup only.

**The primitive (`MatchEngine.CreatePersonality`):** mints a `CardInstance` straight from
`CardCatalog.Get(definitionId)`, with a fresh id from a new `MatchState.CreatedCardSequence`
counter (mirroring `TokenSequence`'s own replay-determinism reasoning — match setup's own local
`nextId` closure only runs once and does not survive into gameplay). Enters play through the same
shared entry sequence `Purchase` already funnels through — `Zone.Home`, `FaceUp`, `QueueOverconfidentDraw`,
`OpenEntryReactionWindow` (which itself handles Destined/EntryHonorLoss/Sensei generically) — but
skips every purchase-specific step (`QuotePurchase`'s Gold cost, Proclaim eligibility, `Refill`),
since nothing was recruited from a Province: "created, not recruited," the user's own phrase.
Returns a bool (whether a window opened), the same convention `OpenEntryReactionWindow`/
`OpenOverconfidentDrawWindow` already use, so the calling `ExecuteAbilityEffects` case can decide
whether to `return` early — the identical shape `low-market-equip`/`equip-battle` already use for
their own "attach, then maybe open a window" sequencing. Only Personality creation is implemented
(all that's needed here); a created Holding/Region/Attachment is accepted future scope, matching the
user's own broader description of where each would enter play, but not built until a real card needs
one.

**`CardDefinition.CreatedOnly`, a new flag:** the Lion Ancestor's own printed text ("May not be
included in decks.") is real — verified against its own scan, not oracle metadata — but is a
player-facing statement, not something any existing engine check reads. `CreatedOnly` is the
machine-readable form of that same fact, read in two places: `MatchEngine.DestroyUnits` (below) and
`DeckWorkshop.Review()`, which now flags a selected `CreatedOnly` card with a warning ("cannot be
included in decks") — the same soft-check discipline every other deck-legality rule in this file
already uses (copy limits, the 40/40 minimums, unimplemented cards: all warnings, never a hard
block on `Adjust`/`Load`). A hard block was considered and rejected: nothing else in `DeckFormat`/
`DeckWorkshop` throws for an illegal deck today, and the client's own `Draft.Adjust` call
(`DeckBuilderScreen.cs`) has no surrounding try/catch — a hard throw there would be a genuine crash
risk for an existing, working screen, for a rule this engine already handles the same soft way
everywhere else. `DeckCardInfo.CreatedOnly` defaults `false` so it's additive, not a breaking change
to the record; threaded through from `CardDefinition.CreatedOnly` at the one call site that feeds
`Review()` (`DeckBuilderScreen.NewDraft`), not the two setup-slot-filtering call sites that never
reach `Review()` at all.

**Destruction (`MatchEngine.DestroyUnits`):** "when destroyed it follows the rules of destruction
... and then is removed from the game" needed no new destruction logic at all — a Personality's
existing rules (the Dishonorable-death penalty in `MarkDead`, battle-casualty Honor gain in
`ResolveBattle`, `ExpendableDraw`, `DestroyedDuringBattleBonus`) already apply unconditionally to any
Personality, created or not. What's new is only where it ends up afterward: `Definition(card).CreatedOnly`
redirects to `Zone.RemovedFromGame` right after `MarkDead` — the exact code shape Lady Matsu's Rage's
own redirect already established, reused as a sibling check rather than a rewrite. Ordering matters:
this check is unconditional, checked *before* Lady Matsu's Rage's own `actingPlayerId`-gated check,
specifically because a created card's own most common death (battle resolution's own Force-comparison
casualty sweep) calls `DestroyUnits` with **no** `actingPlayerId` at all — gating the redirect on
`actingPlayerId is int aid` the way Lady Matsu's Rage's own check does would have silently missed the
one death path that actually matters most for this card.

**Kitsu Akai's Battle ability** ("Bow your performing Ancestor Personality...") needed no new
Performer category either — `own-unbowed-personality-at-battle-with-keyword` (Volcano Tattoo's own
generic "Choose your performing [Keyword(s)] Personality" shape) already covers exactly this with a
single `RequiredKeywords: ["Ancestor"]`. Ancestor and Spirit carry no mechanical rule of their own
(the user's own clarification) — Ancestor's only role anywhere in the engine is this qualification
check (plus the pre-existing, unrelated Halls of Memory combat-destruction bonus, `Keyword(card,
"Ancestor")`, which the Lion Ancestor also happens to qualify for, unmodified). `enemy-card` +
`RequireNoAttachments: true` (Earth's Hunger's own shape) covers "target an enemy card without
attachments."

**Data-quality notes:** the local catalogue's raw record for the Lion Ancestor mis-scopes its own
`type` as `"Proxy"` (an oracle-of-the-void bucket for a token card shared across several printings/
sets, not a real `CardType`) and carries no `clan` key at all (only its own Keywords name Lion Clan).
Both hand-corrected in `package_personality_wave_17.py`'s own `OVERRIDE`, verified against the card's
own scan (`assets/reference/oracle/second-city/images/oracle-l5r-9785-printing-3-image-1.jpg`):
templated and stat-blocked identically to an ordinary Personality, Lion Clan affiliated the same as
Kitsu Akai. Force/Chi/PersonalHonor for both cards needed no correction — `build_definition_fields`'s
existing `num("ph")`→`PersonalHonor` mapping already matches Kitsu Akai's own printed "2F/2C/3PH"
text on the Lion Ancestor exactly (Force 2, Chi 2, PersonalHonor 3), verified against the scan, not
assumed from the field's own name. The actual bundled scans (`find_source`'s own existing
cross-set-fallback logic) ended up being the Promotional–Emperor/Embers of War reprints rather than
the Second City ones specifically, since those sets' receipts were already downloaded locally —
confirmed byte-for-byte identical text/stats/keywords against the Second City printing before
accepting this (only the copyright-year footer and border trim color differ, the same reprint the
real card itself is).

**Reason and evidence:** User spec (2026-09-16): "The Lion Ancestor is considered a card and behaves
like one while in play. However, when destroyed it follows the rules of destruction (e.g., if it
dies dishonorably, its personal honor is subtracted from its controller's family honor) and then is
removed from the game. The Lion Ancestor has the keywords described in Kitsu Akai's Limited action
(Lion Clan, Samurai, Ancestor, and Spirit). The Ancestor and Spirit Keywords do not have mechanical
ability and exist solely for qualification. ... The Lion Ancestor is a card and a personality (so,
when destroyed in battle resolution provides 1 or 2 honor to the opponent) but it cannot be
proclaimed as it is created, not recruited. Other created cards are 'cards' ... A created
personality, holding, attachment, or region enter play in the location of other cards of that type.
... There is no restriction on how many copies of a created card can be in play."

No "created Holding/Region/Attachment" primitive exists yet — the same "not yet needed" treatment as
prior deferred-scope notes (Elemental Keywords, the Naval-count effect): nothing published needs one,
so nothing beyond Personality creation was built.

**Lane:** Rules (mdornbrook). One mechanical client-file edit (`DeckBuilderScreen.NewDraft`, a single
constructor-argument addition threading `CreatedOnly` through) — no UI/layout/interaction change.

**Affected files:** `src/L5R.Core/Models.cs` (`CardDefinition.CreatedOnly`,
`MatchState.CreatedCardSequence`), `MatchEngine.cs` (`CreatePersonality`, `DestroyUnits`'s new
redirect branch), `CardAbilities.cs` (`kitsu-akai-create`/`kitsu-akai-battle` catalog entries and
execution cases), `DeckWorkshop.cs` (`DeckCardInfo.CreatedOnly`, `Review()`'s new warning),
`src/L5R.Client/DeckBuilderScreen.cs` (threading `CreatedOnly` into `NewDraft`'s own `DeckCardInfo`),
`PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (`PersonalityWave17`), new
`tools/content/package_personality_wave_17.py`.

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/257

**Status:** Accepted and implemented for both cards. Created Holding/Region/Attachment entry is
accepted-as-future-scope but not implemented.

**Update 2026-09-20 (issue #387):** creation of an Item, Follower or Spell (attached to a Personality) now exists, and a creator names the created card by data, not by id; see `2026-09-20-created-cards-from-proxy-scans.md`. A created Holding, Region or Strategy is still future scope. The deck-workshop soft warning described above is gone: a created-only card is no longer in the workshop pool.
