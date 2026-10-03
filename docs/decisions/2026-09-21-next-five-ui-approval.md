# Next five UI designs — approved implementation directions

Alfredo approved these concepts in the task goal on 2026-09-21. These are design approvals; each working implementation still requires a prepared, individually launchable demo and Alfredo’s hands-on review. Mendon must approve before merge. No merge into main is authorized.

| Issue | Approved direction | Required correction |
|---|---|---|
| #377 Opponent payment | A sidebar + C immediate outcome feedback | Plain gold coins with no numeral or emblem |
| #323 Top-card viewer | A | Remove bottom card thumbnails and redundant bottom-left wording |
| #345 Multiple targets | A board selection + C expandable groups | Engine supplies candidates, limits and slot order |
| #311 Battle progression | C edge strip + A expandable sequence | Infantry has a distinct icon from Declare |
| #400 Created copies | A small twin-card seal | Preserve full faces and existing unit/attack tabs |

The fifteen concept images, prompts and provenance are retained in Alfredo’s local `artifacts/ui-next-design-review-2026-09-21` gallery. Runtime source scans remain authoritative: reconstructed text, stats and card faces in the concepts are not rules or art changes.

Each issue gets an isolated stacked review branch and native checks. The combined preview base integrates main `99875bc5` with the prior UI stack `6b6908d7`. Conflict resolution preserves main’s after-resolution timing and battle casualty bookkeeping alongside the existing preview reaction discovery and Earth Flows guard. The inherited stabilization rules candidates remain unapproved; this design acceptance does not approve them. Relevant deterministic checks must pass before delivery.

Gold, Honor, targeting legality, pending decisions and hidden state remain engine/server owned. Use the current engine views rather than client calculations. Waiting players receive only their own public view. No frontend framework or new 3D assets are required.
