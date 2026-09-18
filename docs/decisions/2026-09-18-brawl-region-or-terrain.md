# Brawl: the "Regions not built" blocker was stale

**Status:** accepted (rules lane; one client line, shared). **Issue:** #304.

**Decision.** Brawl (oracle-l5r-1127, `strategy-wave-16`) is wired. `BLOCKERS` listed it as "Needs Regions
(not built)", but Regions (`region-wave-1/2/3`, `Zone.Region`, `ProvinceState.RegionId`) and Terrains
(`DiscardTerrain`, Ring of Water's destroyed-a-Terrain flag) both already existed. Nothing new about
Regions had to be built.

**The card** (scan verified, Second City printing, sha256 matches its pinned receipt; Cost 0, Focus 1):

> Battle: Target an enemy card without attachments: Bow it.
> Battle/Open: Target a Region *(at any province)* or a Terrain: Destroy it.

Two alternative effects of one Strategy, wired as a paired-ability set sharing one `UsageKey`
(`brawl-bow`, `brawl-destroy`), the same shape as Strength of the Divine and Horibe.

**Interpretations, for the rules lead to confirm or overturn.**
- The italic "(at any province)" attaches to "Region". A Region sits in `Zone.Region`, never
  `AtBattle`-eligible, so the new `region-or-terrain` Target is exempt from the shared "a Battle-timing
  target must be at the current battlefield" Require. A Terrain needs no separate rule: the engine
  discards a Terrain when its battle resolves, so a Terrain in play is always at the current battlefield.
- Ownership is unrestricted for both targets: either player's Region or Terrain.
- A destroyed Region goes to its owner's Dynasty discard and its province pointer clears, exactly the
  removal `ResolveProvinceDestruction` already applies when the province itself is destroyed
  (new `DestroyRegion` helper). Its static effect (e.g. Fallow Lands' +2 strength) leaves with it.
- Destroying a Terrain reuses `DiscardTerrain` and sets the acting player's `TerrainDestroyedThisBattle`,
  which is Ring of Water's own "destroyed a Terrain" recruitment condition.
- The bow half reuses `enemy-card-without-attachments` verbatim and is registered in `BowsTarget`, so
  Brothers of the Great Lake's bow-negation reaction sees it like every other bow ability.

**Client.** `FindCard` did not search `ProvinceView.Region`, so the target picker (`FindCard(id)!`) would
have thrown for a Region target. Added the one lookup. On-table gold outlines for Region targets depend on
open PR #292 (Regions and Celestials presentation); the picker dropdown works without it. This was checked
by CI compilation only, not exercised in a native client.

**Affected files.** `src/L5R.Core/CardAbilities.cs`, `PublishedContent.cs`, `CardCatalog.cs`,
`L5R.Core.csproj`; `src/L5R.Client/Main.cs`; `tools/content/package_strategy_wave_16.py`;
`content/published/strategy-wave-16*.json`; `tests/L5R.Core.Tests` (`StrategyWave16Primitives`).
