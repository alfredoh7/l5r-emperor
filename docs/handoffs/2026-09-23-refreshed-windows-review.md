# Refreshed Windows UI review delivery — 23 September 2026

Issue [#297](https://github.com/alfredoh7/l5r-emperor/issues/297); delivery [PR #454](https://github.com/alfredoh7/l5r-emperor/pull/454). Alfredo requested the latest Top Cards, ghost-copy, multiple-target, battle-progress and clipping revisions in Mendon's easy-to-launch Windows package.

## Delivered

- [Windows download and complete instructions](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr434-9f8ef9f5a5-r1-windows).
- [Mendon notification, read back and verified](https://github.com/alfredoh7/l5r-emperor/pull/434#issuecomment-5790404279).
- Exact game source: `9f8ef9f5a5af0db65406a9114be14baa515356f3`, the updated combined [PR #434](https://github.com/alfredoh7/l5r-emperor/pull/434). This includes Mendon's integration fixes; it is newer than the original `1d581154` Top Cards implementation.
- ZIP: `L5R-Review-pr434-9f8ef9f5a5-r1-Windows.zip`, 1,787,505,284 bytes. SHA-256: `12f071d6966e7309a28e81dc63d3059aa76a32bc7997c3e4bd16759b5abb1655`.
- Specification: `previews/ui-revisions-september-23.json`; exact published receipt: `previews/published/ui-revisions-september-23.json`.

Extract All into a new folder, then open **Start Review.cmd**. It opens draggable Top Cards directly. Named launchers open Top Cards, Ghost Copies, Multiple Targets, Battle Progress and Table Visibility. `Other previews/Opponent Payment.cmd` preserves the approved payment demo; `Open Full Game.cmd` opens the same build's menus. All eight launchers include isolated settings/log paths. The complete runtime and artwork are bundled; no source checkout, SDK, editor or download at launch is needed. `Send Feedback.url` targets the combined PR #434 because the earlier feature PRs are now closed as superseded. Earlier releases remain immutable.

The checklist records Alfredo's Multiple Targets/Battle Progress approvals, selected Top Cards/ghost-copy designs, and the remaining hands-on acceptance boundaries. Mendon receives a Claude prompt, exact checksum and a request for UI/QoL or rules feedback for Alfredo. GitHub posting is verified; email delivery/read status is not observable.

## Validation

- Packaging/publication regressions: **9/9 pass**.
- Clean exact source SHA and every capture mode checked against the source's committed launchers. Archived specification, eight actual scripts, feedback URL and isolated configuration inspected.
- Windows export passed managed-library, .NET-host, resource and source-exclusion validation. Initial clean client compilation had zero errors and one inherited unused-field warning (`BattleTracker._flowArt`); subsequent incremental native builds had zero errors/warnings. No game source was changed for this delivery.
- Uploaded size and GitHub SHA-256 matched the local receipt. The exact downloaded package passed [Windows package run 35826153462](https://github.com/alfredoh7/l5r-emperor/actions/runs/35826153462) at delivery commit **`8208d97f31a8c074272b33ab3df6126b91222fb9`**. Logs confirm the actual exported Windows executable started with its bundled runtime/resources and isolated data, followed by all eight launcher copies executing through Windows command handling. This includes the new Table Visibility and nested Opponent Payment launcher. The previously published bundles passed in the same run.
- Source PR #434's Mac/Windows CI was green at the pinned source. Delivery Mac/Windows foundation, collaboration and routing checks also passed at `8208d97f`. Final documentation-only changes preserve the validated specification, receipt and ZIP byte-for-byte.
- Fresh native Mac **Top Cards**: 1920×1080 normal and 1280×800 reduced motion pass, including real drag/insertion, keyboard equivalent, rejected/full/outside drops, undo, exact engine destinations/order and private-state cancellation. Fresh **Ghost Copies**: both configurations pass, including marker inspection, both seats, stacks, battle and Removed gallery. Their native captures were inspected.
- Fresh native **table coverage**: 1920×1080, 1440×900 and 2560×1080 pass all four former clipping boundaries, real input and fixed-HUD protection. The edge capture was inspected.
- **Native limitation:** the Multiple Targets run stopped at its explicit `Native game window did not receive focus` guard before its board input assertions; it is not a pass or an observed game logic failure. The desktop control tool selected an older Godot instance and then timed out. The chained Battle Progress run did not execute. Their earlier native evidence remains in the feature handoffs, but it is not fresh validation of `9f8ef9f5`. These two native reruns and actual Windows graphical/expert play remain open.

## Local source and remaining boundaries

The untouched game export checkout is `artifacts/windows-refresh-source` in the parent workspace. Native logs/captures are under its `artifacts/checks/`, `artifacts/table-coverage/` and `artifacts/review-refresh-validation/`. A static-verified fast Mac helper, `artifacts/L5R Updated UI - Review.app`, opens this exact source's prebuilt Top Cards mode. The generated Latest app was refreshed; Finder activation of these wrappers was not claimed during the desktop-tool outage.

The delivery task is complete. Human gameplay/rules acceptance, the two native reruns above and any feedback changes remain separate. Known modified-hand-limit, cancelled local-handoff privacy and duel #448 issues are disclosed as outside this delivery. The earlier minimal clipping fix and other work had already been merged independently before this task; **this task merged nothing** and did not authorize merging PR #434.

Git refs, open PRs and source head were refreshed; ownership was recorded on issue #297. No board token is configured, so GitHub supplied the collaboration record. The previous delivery checkout's unrelated untracked file was preserved. Delivery branch `codex/297-windows-refresh` starts at current main `78573475`; no edits were made to the older review worktrees.
