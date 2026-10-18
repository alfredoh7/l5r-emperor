# What Forge and the Godot / C# ecosystem can teach us (issue #903)

Read-only research on 13 October 2026, in the shape of the earlier Yuzu skills-and-agents comparison: look at what other projects use, compare it with this repository, and sort it into
adopt, adapt the idea, and skip. Three researchers each read the real repositories through `gh` and the web (workflows, build files, docs); I then checked the claims that matter most
against our own tree. Findings carry their evidence (a path or URL in the source reports); anything not checked against a repository is marked **unverified**. One researcher hit the
GitHub rate limit, so the presentation-and-assets survey (section 3) is thinner than the other two and found no open-source Godot card game worth studying.

Licences matter here. **Forge is GPL-3.0** and `db0/godot-card-game-framework` is AGPL-3.0: take ideas only, never code. The Chickensoft libraries, gdUnit4, Kenney, `chun92/card-framework`,
`nathanhoad/godot_sound_manager`, coverlet and BenchmarkDotNet are MIT, Apache-2.0 or CC0 and can be reused. `game-icons.net` is CC BY 3.0 (credit required).

## What we already have (do not chase)

Checked in this repo: Dependabot for GitHub Actions and NuGet (`.github/dependabot.yml`), a PR template, CODEOWNERS, issue templates, a zero-warning `TreatWarningsAsErrors` policy, a
changed-file format check, the comment-swallow lint, sharded Mac and Windows CI with a Godot import cache, a nightly production check, seeded soak and profile runners, a listing/validation
consistency audit, per-task handoffs, repo skills and four review subagents, and a test runner that already fails when a filter matches nothing. Forge has none of the stricter gates (its
only static check is Checkstyle for unused imports; one Ubuntu test job; no format check, no sharding, no soak, no replay harness), and Chickensoft's template gates less strictly than we do.
Pure-C# rules in a Godot-free assembly is the architecture Chickensoft's LogicBlocks and GodotNodeInterfaces try to approximate; ours is already there.

## Recommended, in order

| # | Item | From | Theme | Effort | Call |
|---|---|---|---|---|---|
| 1 | Safety valves on auto-pass: a visible indicator, one key that cancels every automatic behaviour, and a time budget so a slow or unclear "any action?" scan asks the player instead of passing | Forge `docs/Advanced-Yield-Options.md` | reliability, flow | S | Adopt the idea |
| 2 | Skip the desktop CI jobs for docs-only changes (only `preview-checks.yml` has a path filter today; a docs PR currently occupies the Mac runner for about 20 minutes) | Forge `ios-compat-gate.yml` path filters | updates | S | Adopt |
| 3 | Drive UI checks through Godot's input pipeline (`Input.ParseInputEvent` / `Viewport.PushInput`) instead of the real mouse, so window focus stops mattering; keep a few real-input smoke checks. Our native hover checks are the flaky ones | GodotTestDriver (MIT), gdUnit4 scene runner | reliability | M | Adapt the idea (a small in-house helper first) |
| 4 | Pacing settings: separate "skip phase delay" and "skip outcome beat" toggles and a click-to-skip key (our outcome beat is about 3 s per resolved battle) | Forge speed options, card-framework | flow | S-M | Adapt the idea |
| 5 | "Pass until X" yield markers on the phase track, with a data-driven list of interrupt reasons (attack against you, opponent action or reaction, targeting, a reveal) | Forge yield markers and interrupt table | flow | M | Adapt; **rules lead decides what interrupts** |
| 6 | Suggestion prompt "Nothing to do, pass until your next turn?" with decline scope (never, always, once per turn) instead of silent passing | Forge yield suggestions | flow | M | Adapt |
| 7 | Server-path desync soak: play bot-vs-bot through the server and client projection and compare each seat's projected-state hash with the authority; catches hidden-information and projection divergence the in-process soak cannot | Forge `docs/Development/Network-Testing.md` | reliability | M | Adapt |
| 8 | Fixed-timestep capture (Godot Movie Maker `--fixed-fps`) for deterministic screenshots; spike one scenario | Godot docs | reliability | S | Spike (works headless or in CI is **unverified**) |
| 9 | Coverage trend for `L5R.Core` only (coverlet plus ReportGenerator), as a report, not a gate | Chickensoft `coverage.sh` | reliability | S-M | Adopt (works with our custom runner is **unverified**) |
| 10 | CI failure webhook to the maintainers' chat, beside the `nightly-broken` issue | Forge `snapshots-pc.yml` | updates | S | Adopt the idea |
| 11 | Preview release notes generated from `docs/handoffs/tasks/` summaries since the last preview tag | Forge changelog step | updates | S | Adapt (better than theirs) |
| 12 | Hang and crash dumps in CI test runs (`blame` data collection) | gdUnit4Net `.runsettings-ci` | reliability | S | Adopt |
| 13 | Meziantou.Analyzer, introduced rule by rule as suggestions first (we run `TreatWarningsAsErrors`) | Meziantou (MIT) | reliability | S | Adapt (rules that fire on our code are **unverified**) |
| 14 | Opt-in local crash bundle (last log lines, seed, action log, no hidden state), then a Sentry pilot on preview builds only | Forge `BugReporter.java`, `sentry-godot` (MIT) | reliability | M | Adapt; privacy review first |
| 15 | Rolling "latest" prerelease plus a launch-time update check with a channel setting (none, latest, release); tag by SHA | Forge `AutoUpdater.java`, `snapshot-*.yml` | updates | M | Adapt later; our testers use the source launcher today |
| 16 | Scoped mutation testing on one rules namespace (payment, legality, Honor/Gold), manual or nightly | Stryker.NET | reliability | M | Adapt (needs an xUnit/MTP wrapper around our runner: **unverified**) |
| 17 | Sound and icon polish: Kenney CC0 interface audio, a pooled sound manager (MIT), AccessKit screen-reader labels for the HUD and action buttons (Godot 4.5+, experimental; the SubViewport table is not a Control tree) | Kenney, `godot_sound_manager`, Godot 4.5 AccessKit | presentation | S-M | Adopt cautiously; check tone against the aesthetic, 4.7 mono and `gl_compatibility` support is **unverified** |
| 18 | Add a policy line against agents adding unnecessary wiring tests | Forge `CONTRIBUTING.md` | updates | S | Adopt |

Mapped to your three asks:

1. **More reliable, stable, efficient:** items 1, 3, 7, 8, 9, 12, 13, 14, 16. The standout is 3 (it removes the flakiness behind the four hover failures we have been working around) and 7 (it
   tests the one thing our soak cannot, what each seat is actually sent).
2. **Simpler, faster updates:** items 2, 10, 11, 15, 18. Item 2 is the cheapest real saving.
3. **Smoother presentation and flow:** items 1, 4, 5, 6, 17. Forge's yield system is the strongest model found: markers on the phase strip, interrupt conditions with defaults, one key to cancel,
   and suggestion prompts. It extends the empty-battle auto-pass we just built.

## Considered and not recommended

- **Chickensoft AutoInject, GodotNodeInterfaces, LogicBlocks, the GodotGame template:** tied to their node-script conventions and would mean rewriting the very large partial `Main`; the take-away
  is the idea of extracting presenters (a view interface in, intents out) one piece at a time.
- **abarichello/godot-ci:** Linux and Docker, no macOS signing path, so it does not match our runners. If signed macOS previews are wanted, Godot's own exporter can notarise on our Mac runner (option names in 4.7 are **unverified**).
- **release-please** (needs Conventional Commits), **Velopack** (no external players yet), **Tracy** (Godot 4.7 C# support unverified), **Renovate automerge** (flaky native checks make green CI weak evidence; keep Dependabot and merge by hand).
- **Skins and themes, a command palette, radial menus, client-side undo:** conflict with the approved aesthetic, the GPL (palette), or the authoritative engine (undo would have to be an engine command).
- **Card frameworks** (`chun92/card-framework`, `db0/godot-card-game-framework`, a fake-3D demo): our table, stacks and inspector already cover them, and two of the three are GPL or AGPL. One idea from card-framework is already ours: it batches card layout to once per frame, which is what the pile rewrite in #885 did for draw calls.

## What to decide

Items 5 and 6 are rules-adjacent: which events interrupt a pass is a rulings question, so they need the rules lead before any build. Everything else is engineering that can proceed on request,
starting with items 2, 3 and 1.

## Not covered

No open-source Godot card game's own UX flow was reviewed, and no maintained addon was found for toasts, tooltips, rich-text effects, card shaders, colour-blind palettes or hold-to-confirm.
Forge's card-script validation is ordinary tests plus a Workshop editor; we found nothing equivalent to our listing audit to copy.
