# Windows playtest — 15 October 2026

Task: [#981](https://github.com/alfredoh7/l5r-emperor/issues/981). Game source: `c98ea77a9e00eca1445d1304dad41933e64aa407`, confirmed identical to GitHub main before packaging. No game source, content, rules or UI changes were made for this delivery.

Release: [windows-main-20261015-c98ea77a-r1](https://github.com/alfredoh7/l5r-emperor/releases/tag/windows-main-20261015-c98ea77a-r1). The adjacent JSON receipt records the byte counts and SHA-256 of both download assets and their complete inner archive. Packages remain development prereleases; publication does not establish human acceptance.

## Playing

Download **both** `L5R-Emperor-Windows-c98ea77a-Part1.zip` and `L5R-Emperor-Windows-c98ea77a-Part2.zip`. Extract both into the same destination, open `L5R Emperor Windows c98ea77a`, and double-click `Play L5R Emperor.cmd`. First launch verifies every part and the complete archive, extracts the self-contained game, and launches it. Subsequent launches reuse the prepared game. Allow at least 12 GB free for extraction/preparation; keep the folder together.

The package includes Godot, the .NET runtime, the pinned 4,667-image WebP artwork pack and the preloaded deck catalogue. No development tools or runtime downloads are needed. Play → Practice offers the saved decks and Lion/Crane presets; Play → Tutorial opens the tutorial. Private multiplayer needs a separately configured server. This is an unsigned private development build.

## Validation

- `bash tools/export.sh windows`: passed on native Windows, using shared Godot 4.7.2 .NET and SDK 10.0.400. Client build: zero warnings, zero errors. Strict exporter validated PE format, managed libraries, bundled .NET host, resource/art inclusion, source omission and workspace-path omission.
- `python tools/package-share-build.py`: passed. Complete inner archive: 2,615,042,528 bytes; delivered in two ZIPs because it exceeds GitHub's 2 GiB per-asset limit.
- Actual exported executable: headless startup passed with `DOTNET_ROOT*` and `DOTNET_HOST_PATH` removed from the child environment. Native `menu`, `menu-play` and `menu-flow` captures passed on NVIDIA GeForce RTX 3060, OpenGL compatibility renderer. Main-menu/Play images were visually inspected. The menu-flow assertions verify custom Lion-versus-Crane Practice start, seat/name preservation, pause/resume, cancelled replacement and settings persistence.
- Each delivery ZIP: SHA-256 and CRC passed. The real PowerShell helper successfully assembled the complete archive; extracted executable, resource pack, hostfxr and client assembly hashes match the strict export. The assembled executable passed headless startup.
- Both the inner packaged CMD and outer multipart bootstrap passed actual Windows argument-routing checks in directories containing spaces, `&` and `!`, from an unrelated working directory. The real game files remained intact; routing used a copied argument-capturing executable.
- The helper derives from `tools/previews/prepare_review.ps1`, adapted for the full-game share archive. Its process-local module path uses Windows PowerShell's own modules, and assembly uses a short system temporary directory to avoid extraction path limits. No persistent PowerShell/security setting is changed.

An attempted direct `PracticeSetupProbe.tscn` invocation was rejected by the official release template's disabled scene-path overrides. The normal exported `menu-flow` entry point supplied the successful Practice validation instead. No game modification was needed.

The automated menu-flow run reports one ObjectDB instance leaked at exit. It exits successfully with no Godot error or unhandled exception. No complete hand-played match, external multiplayer test or expert rules acceptance was performed.

GitHub's [Mac foundation check](https://github.com/alfredoh7/l5r-emperor/actions/runs/38691886441) passed on the exact source commit. Windows hosted jobs never started: their annotations report failed account payments or a spending-limit block. The nightly and other hosted checks are also red; they are not claimed as passed. No billing, visibility or merge-policy changes were made. Local Windows package validation is separate from those CI results.

Local evidence is under ignored `artifacts/checks/windows-release-981/`; the complete standalone export is under `artifacts/windows/`. The downloaded-package equivalent is prepared under `artifacts/play space & bang!/L5R Emperor Windows c98ea77a/`.
