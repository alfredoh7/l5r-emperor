# Second City unwired-card audit: five waves wired, the rest documented

**Decision:** Audited every remaining unwired Second City card (127 at the start of this issue, out
of 150 Second City cards total) against the engine's existing primitives (no new architectural
systems — only routine additions already established elsewhere: new Target/Performer catalog-switch
cases, new Effect-keyed terms, new per-ability-id `Require` lines). Wired every card found buildable
this way across seven content waves; for everything else, recorded the specific engine gap in
`tools/content/build_card_inventory.py`'s `BLOCKERS` dict so `docs/CARD_INVENTORY.xlsx` stays
accurate. Net result: 28 cards wired (315 total cards published, up from 287 before this issue), the
remaining 99 unwired Second City cards each carrying a documented blocker or scope-deferral note.

**Waves shipped**, all via the standard content-wave pipeline:
- `strategy-wave-6.json` (5): Dispensing Justice, Bow Before My Will, Surety of Purpose, Unexpected
  Support, Wrathful Ancestor.
- `personality-wave-18.json` (10): Daigotsu Shikenuro, Keppo, Kuronada, Matsu Kasei, Moto Ming-Gwok,
  Shiba Kudome, Shikaro, Shosuro Ritoru, Takasho, Yogo Adi.
- `holding-wave-1.json` (4): Archery Range, Slanderer, Temple of Hotei, Tranquil Garden — the first
  Holding wave to carry curated Effect/Ability data at all.
- `item-wave-3.json` (4): Flesh Cutter Arrows, Fubatsu Plate, Nightingale Blade, Satoshi's Dual
  Warfans.
- `region-wave-2.json` (1): Overgrown Grove.
- `spell-wave-2.json` (3): Intervening Spirit, The Earth Flows, Winds of Dismissal.
- `strategy-wave-7.json` (1): Strategic Sacrifice — the one false negative the BLOCKERS
  documentation pass itself caught after the fact (see below).

**New mechanical primitives added** (all routine catalog/Require extensions, not new systems):
- Target categories: `enemy-fewer-or-equal-followers-than-performer` (enemy-scoped counterpart to
  the existing unrestricted `fewer-or-equal-followers-than-performer`), `ring-in-play-or-own-discard`
  (Tranquil Garden — first Target spanning both an in-play card and one in a discard pile),
  `force-lte-province-strength` (Strategic Sacrifice).
- `reinforcesFromElsewhere` extended with `takasho-move`/`intervening-spirit-move` (reinforcing a
  battlefield without the acting card's own presence, the shape michi-move/rikyou-move/Ring of Earth
  already established).
- The explicit non-Personality-source `AtBattle` Require list extended with `nightingale-blade-bow`,
  `fubatsu-plate-straighten`/`-move`, `earth-flows-move` — an attached Item/Spell's own wielder must
  be present, the shape Scouring Flood/The Earth's Hunger already established.
- The Battle-timing "target must be at the current battlefield" Require gained its first **ability-id**
  exemptions (`intervening-spirit-move`, `earth-flows-move`) alongside its existing two Target-category
  exemptions — both reuse the ordinary `own-personality`/`personality` Target categories (which
  elsewhere DO expect `AtBattle`), so the exemption had to be keyed by ability id, not Target name.
- `ApplyDuelOutcome` gained its first self-performer duel whose win consequence is a Bow rather than a
  Destroy/MoveHome (Shiba Kudome).
- `Equip()`'s cost formula gained a second discount term, `Effect=="lion-clan-discount"` (Satoshi's
  Dual Warfans — keyed off the *equipping player's* Clan, not the wielder's own trait, a different axis
  from the existing `weapon-discount-non-unique` term).
- `MatchEngine.CheckEarthFlowsBonus`, a new post-battle-resolution helper called from `ResolveBattle`'s
  two outright-win branches only (never the tie branch — The Earth Flows' own text requires being "in
  the WINNING army," which a tie never has), reading a new `Effect=="earth-flows-honor"` term off the
  winning army's own attached Spells.

**Why Quagmire (`oracle-l5r-6123`) stayed blocked despite looking close:** its own text ("after a bow
resolves at this province or an adjacent province") would reuse `OpenBowReactionWindow`, the shared
watcher-detection function Togashi Hizumi/Tamori Ruya already depend on — but that function hardcodes
`AtBattle(c)` for the watching card (structurally always false for a `Zone.Region` source) and
same-province-only matching (no adjacency). Making Quagmire work would mean modifying that **shared**
function, risking a regression to Hizumi/Ruya's own already-published behavior — judged out of scope
for routine per-card wiring, unlike every other addition in this issue.

**Why the tie branch never calls `CheckEarthFlowsBonus`:** The Earth Flows' own printed condition is
"if this Shugenja is Earth and in the winning army" — a tied battle has no winning army by definition,
so the check is omitted there entirely rather than special-cased to no-op.

**Two stale `BLOCKERS` entries corrected**, both originally tagged "Not blocked; deferred for scope"
by an earlier triage pass, reclassified to blocked after closer reading of their own printed text:
- Engulfing Flames (`oracle-l5r-2342`): needs one Battle ability to be *both* an ordinary
  Target-and-destroy ability *and* the special `"attack"` Target shape (which hands off to a separate
  AttackStrength/AttackKind/ResolveOrDeferAttack switch) — the schema has no way to express "optionally
  destroy, and independently, conditionally also attack" on one ability id.
- Silent Terror (`oracle-l5r-7261`): needs a "discard the Dynasty card sitting in the current
  battlefield's province" primitive — distinct from Kharmic-discard (which only ever discards a card
  the acting player already controls) and from the ordinary Dynasty-phase self-discard.

**Strategic Sacrifice (`oracle-l5r-7524`), a genuine false negative:** flagged by the background audit
agent covering the Strategy/rest batch as fully buildable, not blocked — a plain Siege Performer
(`own-unbowed-personality-at-battle-with-keyword`, already common) plus one routine new Target category
comparing Force to the current battlefield's own province strength. "Bow or straighten the card" is
implemented as an unconditional toggle (straighten if already bowed, bow otherwise), the ordinary L5R
templating reading for that exact phrase rather than a player-facing choice. Wired as `strategy-wave-7`
once caught, rather than left mis-documented as blocked.

**Blocker-category summary** (most common recurring gaps across ~100 documented cards, this issue plus
the two earlier Strategy/Personality triage waves in the same audit):
1. **No non-AtBattle-gated keyword Performer category.** Every existing `RequiredKeywords`-narrowed
   Performer choice assumes Battle timing's blanket AtBattle presence check; an Open/Limited/BattleOrOpen
   card wanting "choose your [Keyword] Personality" as a cost has nothing to reuse. Single largest
   recurring blocker overall.
2. **Missing Reaction triggers.** No generic "after a Battle action resolves," "before this battle's
   resolution," "after this card is targeted," or "after a Ninja/Political action resolves" trigger
   exists — `before-attack-resolves` and `own-enters-play` are the only two built. Second-largest
   category, concentrated in Strategy (Terrain-adjacent triggers) and Personality alike.
3. **Runtime custom-ability grant.** `GrantedKeywords` lets an effect grant a KEYWORD to a card
   instance; nothing grants a whole new ABILITY (with its own Target/Timing) this way — blocks all six
   "Spirit of the X" Strategy cards plus two Spells that create an Item with its own printed text.
4. **Card creation/revival/discard-pile beyond the one built shape.** `MatchEngine.CreatePersonality`
   only creates Personalities; nothing creates an Item/Follower/Holding. Nothing returns a card from
   `Zone.Dead`/`Zone.FateDiscard`/`Zone.DynastyDiscard` back into play.
5. **Negation/immunity primitives beyond Protection/Stalwart.** A handful of cards want a novel
   "negate unless paid" or "targeting immunity" shape distinct from both existing negation points.
6. **Deck search/look-then-choose/shuffle-into-deck.** No primitive inspects deck contents beyond the
   blind top-card draw/discard already built.
7. **Engage timing.** Not started at all — a handful of cards are gated on it entirely.
8. **Self-replacing Terrain.** "Destroy another Terrain at this battlefield, then play this one" has no
   precedent; every existing Terrain rule is "at most one, until destroyed."

**Lane:** Rules (mdornbrook).

**Affected files:** `src/L5R.Core/CardAbilities.cs` (all new AbilityCatalog entries, Target/Performer
cases, Requires, execution cases), `MatchEngine.cs` (`CheckEarthFlowsBonus`, `Equip()`'s cost formula),
`Dueling.cs` (`ApplyDuelOutcome`'s `shiba-kudome-duel` case), `PublishedContent.cs`/`CardCatalog.cs`/
`L5R.Core.csproj` (seven new wave loaders), seven new `tools/content/package_*.py` scripts,
`tools/content/build_card_inventory.py` (`BLOCKERS` dict), `docs/CARD_INVENTORY.xlsx` (regenerated),
`tests/L5R.Core.Tests/Program.cs`/`PublishedTests.cs` (new regression coverage, pinned count 315).

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/264

**Status:** Accepted and implemented for all seven waves (36 cards). Blocked/deferred documentation
complete for the remaining 100 unwired Second City cards.
