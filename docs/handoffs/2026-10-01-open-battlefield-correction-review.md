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
