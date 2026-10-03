# Approved Top Cards Windows refresh — 23 September 2026

Issue [#297](https://github.com/alfredoh7/l5r-emperor/issues/297), delivery [PR #454](https://github.com/alfredoh7/l5r-emperor/pull/454), source [PR #434](https://github.com/alfredoh7/l5r-emperor/pull/434).

Alfredo approved the corrected Top Cards UI at `58456f7264ea04e7e8e116f57316e360ef5b1313` and requested delivery before the separate outdated-task cleanup. [Recorded approval](https://github.com/alfredoh7/l5r-emperor/pull/434#issuecomment-5795932121).

## Exact review build

Game source is `659e015f0e3562bd4b83f5fa445efed1d3fd2892`, the current combined candidate. It includes the approved correction, clipping integration and Mendon's subsequent main/#456 integration. The client and Top Cards art are unchanged between Alfredo's approved commit and this source. Later Seeds of Decay/Honor and Treachery expansions on main are outside this pinned review build. The delivery-tooling branch has current main integrated independently; do not confuse tooling and game source.

[New private Windows package](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr434-659e015f0e-r2-windows): revision 2. The earlier immutable `9f8ef9f5` package predates the corrected appearance and is superseded for this review. Specification: `previews/approved-ui-september-23.json`. The receipt records exact bytes and SHA-256 in `previews/published/approved-ui-september-23.json`.

Extract All into a new folder and open **Start Review.cmd**. It opens the corrected draggable Top Cards. Eight launchers include the five requested demos, Opponent Payment and Full Game. Runtime/artwork and isolated settings are bundled. Feedback returns to PR #434. No checkout or development tools are required on Mendon's PC.

## Validation

- All nine packaging/publication regressions pass.
- Source PR #434 has successful Mac/Windows foundation, collaboration and Windows package checks at `659e015f`; routing is skipped, not failed.
- Fresh Mac **Multiple Targets** passes at 1920×1080 normal and 1280×800 reduced motion: real board/keyboard selection, count bounds, deselect/reselect order, exact engine effects, independent/dependent choices, optional zero, hand privacy, seat/busy reset and per-target reactions.
- Fresh Mac **Battle Progress** passes at both sizes/modes: native strip/keyboard, formal maneuvers, retained assignments, decline, Naval priority, override, forced/ordinary passes, ordered/optional/Encircled effects, reaction ownership, empty battles and layout.
- These four passes replace the previous focus-blocked/incomplete native reruns. The proper native window was activated through its title bar; no focus guard or assertion was bypassed. Selected normal/reduced captures were inspected; [captures and complete passing logs](../screenshots/approved-windows-review/) are committed with this delivery.
- Native client compilation has zero errors and the inherited unused `BattleTracker._flowArt` CS0169 warning; subsequent incremental build has no warnings.
- Generated Godot import UID changes in the isolated export checkout were preserved in an ignored patch and restored before packaging; no game source was edited.

- Windows export passed strict managed-library, runtime, resource and source-omission validation. ZIP: 1,788,872,534 bytes; SHA-256 `d2be7ad61f2491921f45580a4755c42c6f2e845e9a6454bfa237a4f86b8e78ad`. Archive contents match the specification; all eight routes and the corrected Top Cards atlas were verified.
- The prepared Mac app entry script was executed and the resulting native Top Cards screen inspected through Computer Use. It shows the corrected horizontal trays, brush title, larger card faces/deck stack, explicit bottom-order labels and glowing Hand slot. Finder automation did not establish an app-bundle launch; the verified entry script and native rendering are the evidence.

## Publication checkpoint and limits

This file records preparation of the exact package. The release stays **Validation pending** until the exact uploaded/downloaded archive passes **Windows review package** CI. The publisher compares the receipt at the exact successful delivery commit, verifies the uploaded SHA-256, and posts one version-marked notification to Mendon. The final CI runs and verified notification link are recorded on delivery PR #454; do not infer completion from this checkpoint alone.

Windows headless/runtime/launcher validation is separate from Mendon's graphical and rules acceptance. Older modified-hand-limit discarding, cancelled local seat-handoff privacy and duel #448 follow-ups remain outside this refresh. No main merge or outdated-task cleanup is authorized by this task.

Local prepared launchers use `artifacts/windows-refresh-source`, the exact pinned checkout. The fast **L5R Updated UI - Review.app** opens Top Cards; the generated **L5R Emperor - Latest.app** opens the full game via the portable `Play Latest.command`.
