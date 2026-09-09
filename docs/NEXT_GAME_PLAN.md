# Next game development plan

Agreed direction: 2026-09-09. Alfredo is satisfied with the battle presentation for now and asked to organize Mendon’s next mechanical priorities. Broad battle redesign is paused; defects that prevent legal play still get fixed. Tracking parent: [#86](https://github.com/alfredoh7/l5r-emperor/issues/86).

## What the audit found

- **Test/live deck separation is implemented:** #115 remains open, but PR #116 merged. The frozen test deck pins composition; card behavior still comes from one shared CardCatalog. It is not a second rules engine. Preserve the decision in `docs/decisions/2026-09-09-test-live-deck-split.md`.
- **Catalogue data is ahead of its presentation:** #23 is closed; the reference pool contains 2,143 Emperor-legal entries. A `Card Library` menu entry and `LibraryDialog` already exist, including catalogue search. Finish/integrate them rather than building a duplicate viewer. The current engine-supported pool is much smaller than the reference pool.
- **Artwork availability is a delivery concern:** PR #118 merged for #117. Its ~2.2GB offline cache is gitignored and is not automatically delivered by pulling GitHub. PR #119 is still open for two promo URL corrections. Verify packaged artwork coverage separately from catalogue metadata and playable mechanics.
- **Deck building is not merely a missing button:** current TitleMenu has no Deck Builder entry, and no builder screen was found on main. Existing #25/#67/#68 cover the screen/editing/storage; #29/#69/#70 cover making decks playable. The deck format already exists (#24); reuse it.
- **Dueling is not implemented; Spell support is partial:** dedicated tasks #121/#122 now capture these gaps. Existing Shugenja targeting, Spell types and attachment paths must be reused and audited.
- Several issues remain open although their implementation has merged: #101, #104, #107, #109, #111, #113 and #115. Treat them as implementation/acceptance reconciliation, not instructions to rebuild the work. Likewise, open visual PRs are not all on main, even when the local preview contains them.

## Ordered delivery passes

| Pass | Deliverable and issues | Proposed responsibility | Completion gate |
|---|---|---|---|
| 1 — Catalogue | Finish the existing Card Library for the expanded catalogue: [#120](https://github.com/alfredoh7/l5r-emperor/issues/120); artwork #117/#119 | Alfredo/UI; coordinate content fields with Mendon | Find cards by name/Clan/type/keyword/deck; inspect full faces and printings; visibly distinguish supported from unwired; usable missing-art fallback and reproducible artwork delivery |
| 2a — Build a deck | Add Deck Builder menu entry and edit in memory: #25/#67 | Alfredo/UI | Clan/Stronghold/pregame selection; Dynasty/Fate lists; counts and authoritative validation feedback; preserve full catalogue inspection with support badges |
| 2b — Save a deck | Local save/load: #68, existing #24 format | Alfredo/UI | Named decks round-trip; import shared files; malformed-file feedback; overwrite/cancel and unsaved-change protection; no login |
| 3 — Play your deck | Custom Practice: #29/#69 | Mendon/Core construction and validation; Alfredo/setup UI | Select two supported decks, launch, exercise abilities and replay; unsupported effects fail clearly; live preset remains available, frozen test deck stays out of the playable menu |
| 4 — Dueling | Rules/state → representative duel → player interaction: [#121](https://github.com/alfredoh7/l5r-emperor/issues/121) | Mendon/rules; Alfredo/interaction after contract settles | One complete duel through all relevant choices/outcomes, privacy and replay tests, native interaction and rules-lead acceptance |
| 5 — Shugenja/Spells | Existing support audit → missing equip/use/timing → representative spells: [#122](https://github.com/alfredoh7/l5r-emperor/issues/122) | Mendon/rules; Alfredo/interaction | Attach, pay, use, react and resolve correctly; Battle Equip/additional-action limits tested; host/Spell attribution and cleanup correct |
| 6 — Expand wired cards | Small mechanic-based import waves under #86; reuse #95/#97/#111 follow-ups where appropriate | Mendon/rules and card wiring; Alfredo fixes discovered presentation gaps | Every newly supported card has period provenance, all printed effects wired, interaction regressions and a repeatable playable test deck |
| 7 — Private custom games and acceptance | #70; complete games #71, native Windows #72, external match #73 | Shared, human testers explicitly involved | Validated private custom decks, correct hidden views/reconnect/version behavior, complete games on actual Mac and Windows builds |

**Next UI task is #120.** Before substantial new screen artwork, create concepts for approval using the established weathered navy/brass style. Reuse the approved source-to-game comparison workflow. No engine rewrite or broad table redesign is part of that pass.

This is a delivery order, not a requirement for Mendon to wait while UI work happens: #69 can begin from fixture decks before #67/#68 finish; duel/Spell rule audits can run alongside passes 1–3. Existing supported-deck human acceptance (#71–#73) should continue throughout. Private custom-deck work #70 depends on #69 and saved/validated deck selection, not on wiring every card or finishing every future mechanic.

## Scope and rules safeguards

- Browsable, legal, implemented and human-accepted are distinct states. A downloaded card or an Emperor legality flag is not evidence of executable effects.
- #25 permits editing over-limit decks with warnings; #67 must not turn those editing warnings into arbitrary hard blocks. Play-time legality/support comes from Core/server. Unimplemented cards can remain visible for planning while unsupported matches are rejected clearly.
- Do not hard-code current supported-card counts into the UI. Derive support from the shared authoritative content data and show useful reasons when an effect is unavailable.
- Dueling and Spells are major gaps, not the only gaps. #15 Engage reactions, remaining #20 battle-segment work, general Dishonor/Rehonor, Terrain, Region/Celestial behavior and other Reaction triggers remain in `ROADMAP.md`. Prioritize those by the next complete deck’s actual requirements; file bounded work before claiming those cards supported.
- A general log was previously rejected on the left during battles. Do not reintroduce it as part of these passes without a new design decision.
- Tutorials (#74), further cinematics and cosmetic battle expansions follow dependable-match acceptance, not this immediate implementation queue.

## How each pass is handed off

Use the existing issue and an isolated PR; record the source contract, screenshot/interaction evidence and exact remaining limitations. Rules changes require focused regressions and `tools/check.sh --skip-client`; UI changes require client compilation plus native interaction/render checks. Never substitute CI export for Windows playtesting or tests for Mendon’s rules acceptance.

Finish one reviewable slice before expanding its scope. Read current main and overlapping PRs at each start. Proposed responsibilities here do not assign Mendon or remove his standing authority to work anywhere. Keep this sequence under #86 and update ROADMAP status in the implementation PR that actually changes it. Do not close open issues solely because this plan mentions a merged PR.

## Validation of this plan

Compared live GitHub issues/PRs with main at ea88983: TitleMenu/LibraryDialog, Core deck and match entry points, ROADMAP, NEXT_PHASE, test/live-deck decision and recent #115/#117 handoffs. Confirmed no existing dedicated dueling, spell-completion or expanded-viewer issue before creating #120–#122. Planning-only: no game code changed, no new gameplay test or complete-game acceptance claimed.
