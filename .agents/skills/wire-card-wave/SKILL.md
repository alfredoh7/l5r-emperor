---
name: wire-card-wave
description: Package and wire a reviewed L5R Emperor card wave with printing provenance, feature-owned rules, ID manifests, regression tests, artwork coverage, and generated inventory.
---

# Wire a card wave

Work from the repository root. Follow AGENTS.md for branch, overlap checks, handoff and PR workflow; this recipe supplies the content steps. Read docs/rules/RULINGS.md for relevant rulings and docs/PITFALLS.md when missing scans, workbook locks or client imports affect the task. Preserve the pinned Lion/Crane profile.

## Establish the inputs

Identify the exact card IDs, reviewed period printing, full scan, stats, keywords and printed text. Search content/published/*.json first: do not publish an existing ID a second time. Choose an unused `<type>-wave-<positive integer>` profile with no leading zeroes. Source membership in a catalogue is not proof of the requested printing or complete executable support.

Use a receipt-verified original scan or its verified working_copy. Fresh clones may lack the gitignored scans: `python tools/content/sync_webp_working_copies.py --apply` restores pinned working copies; its default is a dry run. Inspect that tool and the receipt before applying it. Do not substitute near-miss created-card scans; docs/requests/created-card-scans-needed.md records outstanding requests. For an unresolved reading, record the question and continue independent supported cards; do not guess a rule or replace printed effects with Effect=none.

## Package the reviewed wave

Create `tools/content/package_<type>_wave_<N>.py`; do not change an existing wave's PROFILE. The maintained examples are tools/content/package_vanilla_wave.py (no printed text) and tools/content/package_strategy_wave_1.py (curated ability/text overrides).

The shared helper is imported from the same tools/content directory:

```python
from package_vanilla_wave import build_definition_fields
fields, provenance = build_definition_fields(root, card_id, allow_text=True, prefer_slug=reviewed_set_slug)
oracle_id = fields.pop("_oracle_id")
fields["Abilities"] = reviewed_ability_ids
fields["Effect"] = reviewed_effect_id
```

For genuinely text-free cards use allow_text=False and empty Abilities/Effect=none. The helper verifies receipt hashes and copies the scan into src/L5R.Client/Content/Published. Verify the returned provenance printing_set against the requested set: prefer_slug may fall back when that set has no matching record. If a mismatch occurs, fix the input selection before packaging. Inspect the full helper contract; use a reviewed text override only when supported by the actual printing, never current scraped wording alone.

The packager must write UTF-8 `content/published/<profile>.json` with Profile, Rules, Cards and Decks (normally []), and `<profile>-sources.json` with the corresponding provenance rows. Keep it rerunnable with explicit card selection. Assert unique IDs and the requested count. Run the packager instead of hand-editing generated JSON; tests should call its package function against fixture inputs as well as check the published shape.

## Wire the behavior

Reuse existing generic mechanics when they exactly implement the reviewed card. Otherwise add a feature-owned partial MatchEngine file in src/L5R.Core and a metadata-only module initializer that registers typed static callbacks on EngineHooks. Use unique stable IDs and explicit priorities >=10000. Add Abilities catalogue entries there. First-answer hooks return null when unhandled (an empty candidate array is handled); predicates/pending handlers return false when unhandled; additive bonuses return zero. RunEffects returns null when unhandled, false when handled/completed, and true when handled/pending. Choose the validation checkpoint appropriate to the action; preserve costs, optional choices, hidden information and negation.

Feature state belongs in a partial MatchState in the feature file, with a unique explicit JsonPropertyOrder above 1007. Classify new Pending properties using DecisionState or NonDecisionState with nameof(MatchState.Property); decision registrations also supply an open-state predicate. Explain any nondecision classification. See docs/decisions/2026-09-28-engine-hook-registry.md for channel contracts and compatibility remainders.

Do not add registration lines to L5R.Core.csproj, PublishedContent.cs, CardCatalog.cs or the shared CardAbilities/Models chains for an ordinary new wave/mechanism. Wave resources are embedded/discovered automatically. Do not append to PublishedWaveOrder.txt or rewrite the frozen legacy-hook fixture. A new hook kind or an entangled legacy case is separate shared-interface work, not a reason to disguise a shared edit.

## Pin, test and report

1. Run `python tools/content/pin_wave_ids.py --wave <profile>`. Commit the new tests/L5R.Core.Tests/PublishedWaves/<profile>.txt. Do not use --update unless intentionally changing an existing manifest.
2. Add an automatically discovered Core test class with public static Run(TestRunner). Use tests/L5R.Core.Tests/EngineHookRegistryTests.cs for the runner shape and existing feature tests for fixtures. Test printed behavior, illegal/rejected cases without state mutation, costs, choices and relevant timing/negation; confirm the ID manifest and resolved definitions. Commands can be run with `dotnet run --project tests/L5R.Core.Tests -- --filter <suite>`.
3. Record baseline and final inventory counts; run `python tools/content/build_card_inventory.py` after updating any actual blockers in its source data. Commit the generated docs/CARD_INVENTORY.xlsx, never hand-edit it. Close Excel if it locks the workbook. When a validation exercise is local-only, redirect that module's OUTPUT to an ignored path under the exercise root instead of modifying the tracked workbook.
4. Newly copied or changed scans require matching catalogue-art coverage before a usable client launch. Follow the art-pack-release skill when rebuilding/releasing is in scope; otherwise clearly report the pending publisher step. A scratch fixture with synthetic scans may validate packaging/Core only and must not claim client artwork readiness.
5. Run `python -m unittest discover -s tools/content -p 'test_*.py'`, targeted Core tests, and `bash tools/check.sh --skip-client` for production rules changes. Read tools/env.sh for the pinned SDK setup (on Windows use the bundled .tools/dotnet on PATH and L5R_PYTHON=python). Record exact results; leave Godot work to CI only when the task permits it, and say so.
6. Finish AGENTS.md's handoff/audit/commit/push/PR loop. Report cards wired/blocked, confirmed readings versus questions, new client-lane contracts, scan/art status, checks, and before/after inventory. New suite classes/manifests need no shared test-list edits. Never claim a synthetic acceptance wave is real playable content.
