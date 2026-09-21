# Published-card guard without a hard-coded total

Status: accepted; implemented. Lane: rules (mdornbrook; test-project and tooling edits under his standing authority). Issue: #360.

## Decision

`PublishedTests.Catalogue` no longer pins `Eq(published.Length, N)` under a growing comment block. Adding a card wave now edits no shared line in the
test project. The protection the pin gave is kept by two derived checks and one per-wave file.

## Why

Every wave PR appended a "+ K (issue #...)" line and a new N to the same lines, so every merge conflicted every other open wave PR there. Worse, the
number was a silent trap: the old comment itself records a merge where both sides computed 331 for different reasons (git would have taken either with
no conflict marker) and another whose two sides said 393 and 396. Of the five engine PRs open on 2026-09-19, #357 (418), #358 (420) and #348 (424) each
carried a total computed on the older 417 base rather than main's 422, so all three were already wrong against main before anyone resolved anything.

## How the guard works

1. `CatalogueMatchesDisk` derives the expected set from `content/published/*.json` (every file except `*-sources.json`, deserialised as `PublishedSnapshot`
   exactly as the loader does) and asserts the non-ScenarioOnly ids in `CardCatalog` equal it: none missing, none extra. It also fails on an id listed
   twice in a file or published by two files, a file whose `Profile` differs from its name, a ScenarioOnly card in a published file, and a wave file that
   L5R.Core does not embed (or an embedded profile with no file). Embedding is checked before `CardCatalog` is touched, so a dropped csproj line is reported
   by file name instead of as a type-initialiser failure. `lion-crane-test-v1.json` is a frozen deck fixture that deliberately repeats 40 pinned ids; it is
   excluded from the duplicate check and its ids must exist in another file. The repository root is found the way the other tests find it (`Support337.RepoRoot`,
   walking up from the test binary to `AGENTS.md`), never from the working directory.
2. `WaveManifests` compares each wave file with `tests/L5R.Core.Tests/PublishedWaves/<wave>.txt` (its card ids, one per line, sorted). Every wave file
   needs a manifest and every manifest needs a wave file; the id sets must be equal. A new wave adds one new file, which git merges without conflict.
   `python3 tools/content/pin_wave_ids.py` writes the missing manifests (`--update` rewrites existing ones for a deliberate change to an existing wave).
   All 93 current files were pinned in this change.

## What it catches, and what it cannot

Catches: a wave whose cards are not loaded into `CardCatalog` (chain link dropped in a merge), an extra published card in the catalogue that no file backs, a
duplicated id, a wave file not embedded, and (through the manifest) a wave file deleted together with its loader, chain link and csproj line, or shrunk or
grown without its manifest changing. Mutation-checked, see the PR.

Cannot catch: deleting a wave file and its manifest together, which is a deliberate two-file removal by design (a reviewer sees both deletions in the
diff); and a manifest generated from an already-wrong file (the manifest pins what the file says, not whether the card is correct). No checked-in list can stop someone who edits every place on purpose.
A shared sorted id list was rejected because two PRs adding neighbouring ids conflict; a frozen baseline was rejected because waves added after the freeze
would be unprotected; a base-branch diff in `collab.py audit` was rejected because it needs history in CI and does not run in the unit test suite.

## Other conflict magnets in the test project

Removed: the `XTests.Run(runner);` registration line per suite in `tests/L5R.Core.Tests/Program.cs`. Suites are now discovered by reflection: any
`internal static class *Tests` with `public static void Run(TestRunner)` runs (ordered by name; the executed test count is identical to before); a `*Tests`
class without that method throws, so a suite cannot be silently skipped.

Left, and why: the `tests` array in `Program.cs` and in `PublishedTests.cs` (each new case is an entry appended at the end; removing that needs attributes on
every test method); per-wave count assertions in wave-specific tests (each wave's own lines, no shared edit); the per-wave `Length != N` checks in the
`PublishedContent.cs` loaders (a wave's own new lines). No other test or tool hard-codes the global total or a per-wave count that every wave must edit: the
AI, server and transport suites and `tools/` were searched, and the remaining mentions of 417 and 422 are historical prose in earlier decision notes.

## The other shared lines every wave still edits (assessed, not changed)

Each wave PR still appends to: `L5R.Core.csproj` (one `EmbeddedResource` line), `PublishedContent.cs` (a property and a loader whose duplicate check lists every
earlier wave, so each new loader is longer than the last), the single 20 KB `.Concat(...)` line in `CardCatalog.cs`, the `BLOCKERS` dict in
`tools/content/build_card_inventory.py`, and the binary `docs/CARD_INVENTORY.xlsx`. All are appends at the same position, so two open wave PRs always conflict.

Recommendation, in order of value:

1. Auto-discover waves (one PR, after the current queue has merged, because it conflicts with every open wave PR). In the csproj use
   `<EmbeddedResource Include="../../content/published/*.json" Exclude="../../content/published/*-sources.json" LogicalName="L5R.Published.%(Filename).json" />`
   (log names by file stem; the ~96 `Load("L5R.Published.X.json", ...)` calls become `Load(stem)`, a mechanical edit). `PublishedContent` exposes
   `Waves`, built once from `GetManifestResourceNames()` sorted ordinally (never file-system glob order, which differs by OS), and `CardCatalog.Build` concatenates
   `Waves` instead of the hand chain. A single duplicate pass over the assembled list replaces the growing per-loader chains and names both waves. Keep the named
   properties only for waves that existing tests reference; new waves are read with `Wave("personality-wave-34")`. The per-wave count/type/text checks in each loader
   move into the derived test (type from the file-name prefix, count from the manifest).
2. Risks. Load order: not load-bearing. The engine only calls `CardCatalog.Get(id)`; `CardCatalog.Cards` is enumerated in the client (an id lookup) and in a few tests
   that sort or compare sets, so transcript replay and deck-workshop tests do not depend on it, and a sorted order is deterministic. The static-initialiser hazard the
   chain comments describe (a loader reading a later-declared property gets null) disappears when waves are built in one lazy method rather than by textual property
   order; `TestSnapshot` must stay outside the wave list because the catalogue deliberately does not merge the frozen test snapshot; `Snapshot` and `VanillaWave1` are ordinary entries.
3. `BLOCKERS`: add an overlay read from `content/blockers/*.json` (one new file per wave, later files win, a null value clears a note) so a wave stops editing the
   shared dict; move the legacy dict there later.
4. `CARD_INVENTORY.xlsx`: stop regenerating it in wave PRs (a binary file cannot merge). Regenerate once after merges with `python3 tools/content/build_card_inventory.py`,
   or publish it as a CI artifact. Do not add a freshness check, which would recreate the conflict. `build_card_inventory.py` already globs the wave files.

Until item 1 lands, a sync agent resolves the csproj, `PublishedContent.cs` and `CardCatalog.cs` conflicts by keeping both sides' additions.

## What an open wave PR does after this merges

Resolve the one remaining conflict by deleting your edit, not by choosing a number:

1. `tests/L5R.Core.Tests/PublishedTests.cs`, `Catalogue()`: take origin/main's text for the whole function head, that is everything between
   `private static void Catalogue()` and the `foreach(var c in published)` line. Delete your appended "+ K (issue ...)" comment lines and your `Eq(published.Length, N)`.
   No number replaces them.
2. `tests/L5R.Core.Tests/Program.cs`: take origin/main's suite-discovery block and delete your `XTests.Run(runner);` line. Your suite class must be
   `internal static class <Name>Tests` with `public static void Run(TestRunner runner)`. Entries you added to the `tests` array stay.
3. Run `python3 tools/content/pin_wave_ids.py` and commit the new `tests/L5R.Core.Tests/PublishedWaves/<wave>.txt` for each wave you add (use `--update` if you
   changed cards in an existing wave file).
4. Run `bash tools/check.sh --skip-client`. A missing csproj entry, loader or chain link, or an id in two waves, now fails by name.

## Affected files

`tests/L5R.Core.Tests/PublishedTests.cs`, `tests/L5R.Core.Tests/Program.cs`, `tests/L5R.Core.Tests/PublishedWaves/*.txt`, `tools/content/pin_wave_ids.py`,
`tools/content/test_pin_wave_ids.py`, `tools/content/README.md`, `.gitattributes`.
