# Open battlefields — approved presentation

Alfredo approved A's standalone court with B's on-demand help and C's location navigation when several battlefields coexist on 2026-09-30. This is the second portion of [issue #499](https://github.com/alfredoh7/l5r-emperor/issues/499).

Use the existing approved navy mat, physical card stacks, brass medallion and red brush court pixels. Close both ends of a court with no province, remove its neck, and label it "Open battlefield". Keep rules detail behind a help control. Navigation frames a location only; legal battle selection remains an engine command. Keep an orphaned court in place if its province disappears. Derive province association from the public battlefield view, including null for primary battlefields whose province has gone; an additional battlefield may still have a province.

Ruling R-002 and `EmperorRulingR1.cs` remain authoritative: no province strength, no adjacent battlefields, no province destruction. No engine or wire changes are planned. Mendon/Nathan review mechanics presentation; concept approval does not authorize merging.

Implemented on PR #565 using main `1723502b` as the base. Open PR #564 also touches board layout/battle progress; it remained open during native verification. Preserve its separate work and reconcile that overlap before merging either combined result.

Approved references: `assets/ui/open-battlefield/A-standalone-court.png`, `B-quiet-rim-and-inspection.png`, and `C-multiple-battlefields.png`. The concept sidebar's newer vertical stage rail belongs to #564; this task will preserve the base's existing battle controls and document any remaining comparison difference.

## Implemented behavior

- Province association is read from the permitted `BattlefieldView.Province`, including an explicit null for a destroyed primary province. Older snapshots fall back to surviving defender provinces. No wire, state, legality or resolution changes.
- Closed courts reuse `battle-court-reference.png`, `crimson-reference.png` and the existing daisho pixels. Only plain border runs stretch. Screen-facing captions follow the projected rim, keeping names readable and clear of card faces. The help control opens a dismissible lacquer panel.
- Several open locations receive stable A/B/etc. names for that battle. Additional fronts still associated with a province say `Province N · extra front`. They remain beyond the piles, with no misleading neck pointing at another bay.
- `View battlefield` controls move the camera only, with the existing smooth glide or immediate reduced-motion movement. Actual resolution-order choices still use the engine's `selectBattle` actions. Viewing an inactive court explicitly identifies where actions remain.
- Extra lane storage grows with the public view instead of silently omitting battlefields after four. Empty unresolved courts remain available. Removing a province does not move its surviving courts.

## Review and evidence

`bash tools/check-open-battlefield-ui.sh` passed compilation (zero warnings/errors), import and native Godot checks: both seats, real scans and card inspection, native help/Escape, camera-only navigation, associated additional front, orphaned positions and stable names, six courts, an empty unresolved selection, bowed units/attachments, no caption/card overlap, 1440×900 and ultrawide, reduced motion and camera glide. The existing `emperor-ruling-R1` suite passed 15/15.

The first parallel Godot import crashed; a single-threaded retry completed. Final imports and native checks passed. [Source/native comparison](../../assets/ui/open-battlefield/comparison.html) retains the actual concept and native captures. This is faithful reuse of the approved court artwork, not full-screen pixel identity: the live engine retains its current layout, real cards and horizontal stage strip while #564 remains separate.

The prepared review uses isolated presentation fixtures and blocks game commands. Use its scenario selector, Other seat, View courts, Remove province, card inspection and help. Full gameplay uses the same production renderer with normal engine actions. Human Windows playability and mechanics acceptance remain with Mendon/Nathan; no merge is authorized by this implementation.
