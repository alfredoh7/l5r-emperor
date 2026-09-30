# Open battlefields — approved presentation

Alfredo approved A's standalone court with B's on-demand help and C's location navigation when several battlefields coexist on 2026-09-30. This is the second portion of [issue #499](https://github.com/alfredoh7/l5r-emperor/issues/499).

Use the existing approved navy mat, physical card stacks, brass medallion and red brush court pixels. Close both ends of a court with no province, remove its neck, and label it "Open battlefield". Keep rules detail behind a help control. Navigation frames a location only; legal battle selection remains an engine command. Keep an orphaned court in place if its province disappears. Derive province association from the public battlefield view, including null for primary battlefields whose province has gone; an additional battlefield may still have a province.

Ruling R-002 and `EmperorRulingR1.cs` remain authoritative: no province strength, no adjacent battlefields, no province destruction. No engine or wire changes are planned. Mendon/Nathan review mechanics presentation; concept approval does not authorize merging.

Implementation and verification are in progress. Existing main is the base; open PR #564 also touches board layout/battle progress and must be checked again before delivery. Preserve its separate work.

Approved references: `assets/ui/open-battlefield/A-standalone-court.png`, `B-quiet-rim-and-inspection.png`, and `C-multiple-battlefields.png`. The concept sidebar's newer vertical stage rail belongs to #564; this task will preserve the base's existing battle controls and document any remaining comparison difference.
