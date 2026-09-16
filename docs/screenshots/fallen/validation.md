# Native validation — Fallen counter and inspection

2026-09-16, Apple M1 Max, Godot 4.7.2 .NET / OpenGL compatibility renderer.

- `bash tools/check.sh --skip-client`: **439 checks passed** (43 collaboration, 130 content/tooling, 172 core, 52 published, 9 deck format, 10 deck workshop, 14 AI, 5 server, 4 client transport). Existing checks cover authoritative DeadCount, ordinary dishonorable death, Seppuku honorable death, rehonoring at battle resolution and created-card removal.
- Client C# build: **0 warnings, 0 errors**. Godot import passed.
- `bash tools/check-fallen.sh`: **14 native cases passed**: seven component cases, four full-game cases and three existing token-rail cases. Source comparison is visual evidence, not a functional assertion. Component input cases exercise real row/tab clicks, Enter activation, owner switching and stale-selection clearing. Crowded mode scrolls 24 rows and clicks the last one. Orientation mode checks all four bow/dishonor compositions, hit tests, six tokens plus permanent grant, upright reading and hidden/dead exclusion.
- Full-game cases open the proper owner’s modal by clicking the actual counter, verify public-pile separation and table input blocking, retain the original authoritative session, and dismiss with Escape in table captures. Both seats and 1280 × 720 are included. Forced GC checks idle texture retention. Fixtures are explicit read-only public snapshots, not real recorded deaths.
- Existing token cases cover normal, bowed attachments and perspective, including no rail/card overlap, crowded density and owner clicks. The legacy probe’s pointer injection was made viewport-specific with an explicit window focus/mouse-entry notification to remove native-window focus ambiguity.
- Visual passes corrected an expired atlas resource on the opponent counter, the source tooltip pointer baked across A’s bottom edge, long-name wrapping, clipped identity text, a low-resolution enlarged intact fan, and rectangular backgrounds on the small on-table dishonor markers. Final empty-state and tab-border refinements received targeted native rechecks.

## Launcher / catalogue dependency

The first `Play Latest.command` verification caught seven main-branch printings missing from the older WebP manifest. The publisher builder regenerated the 2,749-image WebP85 pack from verified originals. Its SHA-256 **a1ecf9de0845f7619c6c6a69ba96943eb61952eac3046b36e4059dbab35d9e20** exactly matches the already published private `catalogue-art-webp-v4-20260916` release (375,462,528 bytes). The receipt and manifest now reference that existing release; no release was replaced and no original printing changed. Local installation and the real launcher’s offline preflight, build and native capture passed (`launcher.png`).

## Review limits

The full original card faces and established Caudex/WeatheredQuill fonts replace imagegen’s illustrative typography and cropped scans. Approved ornamental pixels are reused, and the intact large fan is a built-in imagegen derivative documented in `assets/ui/fallen/`. Screens are faithfully based on A/B, not claimed to be exact full-screen pixel equivalents. The surrounding room is the existing approved integration and was not changed in this task.

This branch requests Mendon’s shared review for the state presentation; automated checks are not expert rules acceptance. Native Windows play and a two-machine session were not performed. PR #261 is stacked on #256 and #248; those review dependencies remain separate. The Duel Demo #220 remains excluded.
