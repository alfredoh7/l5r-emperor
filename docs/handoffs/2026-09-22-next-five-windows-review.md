# Next five approved UI designs — Windows review delivery

Continues issue #297 / PR #298 and the private Windows preview workflow. Alfredo approved the five ImageGen directions: opponent-payment A+C (plain coins), top-card A (one full-scan row), multiple-target A+C, battle C+A (distinct Infantry helmet), and created-copy A. He requested the established isolated, launchable-demo workflow. This delivery does not merge anything or substitute design approval for hands-on acceptance.

`previews/approved-ui-next-five.json` pins the eventual exact game source from PR #432, incorporating #427–430. Each prepared review mode is checked against its checked-in command launcher. One self-contained Windows ZIP will contain Start Review, five named demos, Open Full Game, isolated settings/runtime/artwork, review instructions and a feedback shortcut. The earlier review batches remain available separately.

The delivery branch integrated origin/main `97d8e407` without conflicts. Its packaging regressions passed 9/9, and `bash tools/check.sh --skip-client` passed all requested checks, including 7/7 transport checks. These checks cover this tooling/main checkout; the isolated game source has its own native evidence and CI.

Publication remains gated on the exact uploaded ZIP passing Windows runtime and command-launcher checks. The publisher verifies receipt SHA-256 and byte size, then sends one deduplicated GitHub mention to Mendon with the download, checklist, approval boundary, feedback request and Claude prompt. Native Windows graphical play, expert rules acceptance and external multiplayer are separate human acceptance. Email delivery/read status is not claimed.

Package source, receipt and successful delivery evidence will be recorded below after validation. No notification of a ready package is sent before the package gate passes.

## Exact packaged candidate

- Source: `0aa6992d19115153f02455391875b94b6aa25d72` (PR #432, with #427–430).
- Asset: `L5R-Review-pr432-0aa6992d19-r1-Windows.zip`.
- Bytes: 1,450,407,450; SHA-256 `dd155ea5a95f8919ddb28b210e3d456213c9ee301a3b6422f53adbb07211d04d`.
- Private release tag: `preview-pr432-0aa6992d19-r1-windows`.
- Windows export validation passed: managed libraries, .NET host, resources and source exclusion. Actual ZIP specification, all seven launchers and review/approval boundaries were inspected.
- Each of the five Mac launchers was opened in its feature worktree; final source has native normal/reduced evidence in its handoffs.
- Private upload digest/byte size verified. Exact downloaded Windows runtime/launcher CI remains pending at this record.
