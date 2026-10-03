# Occupied resolved primary battlefield correction — #499

When the primary Two Fronts battle resolves with surviving defenders and the extra front later destroys its province, the public engine view still includes that primary battlefield. The client previously omitted its court because it was neither current nor unresolved. This hid the defenders and left only the visible Open battlefield B.

The table now includes engine-listed and occupied primary courts, restricted to the defender’s existing primary indices. Older views without battlefield metadata use public army positions. Empty destroyed resolved primaries still disappear when their units leave. No authoritative rules, commands, resolution or wire contracts changed.

## Review

Run `python3 tools/create-open-battlefield-launcher.py --correction` and open the resulting app. It starts **Resolved defenders after province destruction**. Inspect the defending survivor and its attachment in Open battlefield A, switch seats, and use the sidebar’s view controls. B remains the active battle. The prepared scene disables engine commands; the separate Core regression executes the actual Two Fronts command sequence.

## Validation

- Added native regression fails on the original renderer: occupied resolved primary court disappeared after province destruction.
- Final foreground native suite passes: both seats, survivor/attachment inspection, older-view fallback, empty-court retirement, camera-only navigation, stable positions/names, 1440×900 and ultrawide, reduced and smooth motion, plus previous open-battlefield scenarios. A background repeat failed mouse inspection; rerunning the same checks in a foreground app passed.
- Client compilation: zero warnings/errors. Changed C# whitespace verification passed.
- `bash tools/check.sh --skip-client`: all requested tooling, Core, AI, server and transport checks passed. R1: 18/18; AI: 76/76; server: 8/8; transport: 17/17.
- Review fixtures and automated checks do not replace Mendon/Nathan’s mechanics/presentation acceptance or Windows graphical playtesting.

No merge is authorized. Windows review delivery and expert review remain pending at the initial implementation commit; the task handoff and PR track delivery.

## Windows validation candidate

[Private correction preview](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr575-d00a9c6562-r1-windows) is staged for PR #575 at game source `d00a9c656297cddadb83d4897494c643abbb963c`. The versioned ZIP is `L5R-Review-pr575-d00a9c6562-r1-Windows.zip`, SHA-256 `2b3d80b3cfbf7f8c0eb196e26e532a7a46928677f96ef3594b7cf64e74fe3177`. GitHub upload size/hash verification passed. The interactive correction capture mode was also launched graphically at 1440×900 and inspected; it opens the intended scenario directly. Windows package CI must pass before the publisher sends the targeted review notification. Neither staging nor delivery merges the correction.

The context helper’s refresh timed out reading older local Git history; direct GitHub reads and remote-main verification confirmed the unchanged `8eb1a7c` base and overlapping work. A separate temporary Git transport cache published only this task branch, avoiding cloud-evicted older objects; other branches and main were not changed. Local review tools are preserved in a task-specific non-cloud cache so the one-click launcher does not depend on temporary SDK files.
