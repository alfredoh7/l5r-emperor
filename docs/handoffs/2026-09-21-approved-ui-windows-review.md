# Approved UI demos 1–4 — Windows review delivery

Alfredo approved previews 1–4, with direct deck-button navigation in #4, and explicitly asked to send Mendon the demos. Their merge is contingent on Mendon approving and relevant checks passing. Mendon should record UI/QoL feedback for Alfredo to address. Previews 5–8 still await Alfredo and are excluded from this Windows build.

## Exact candidate

Source PR #396, commit `db649158edd527b72714fab053d7f190af425e09`. The package contains named launchers for Decision Chooser, Shown Cards, Action Restriction and Deck Search; Start Review opens demo 1. Extract All, then double-click a launcher. No source checkout, Godot or SDK is required. Settings/decks are isolated from normal game data. Open one demo at a time.

[Private release](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr396-db649158ed-r1-windows) · [Batch feedback / source PR](https://github.com/alfredoh7/l5r-emperor/pull/396). The release remains marked Validation pending until the exact uploaded package passes the Windows review package job. `previews/published/approved-ui-1-4.json` pins its size and SHA-256. `publish.py deliver` checks that exact receipt at the passing validation commit before sending one targeted notification.

## Validation and limits

Source: zero-warning/error client build, Godot import, native Mac deck-search checks at 1920×1080 normal and 1280×800 reduced; full check.sh --skip-client passed including 34/34 AI, 8/8 server and 10/10 transport. The updated transport test covers bow/destroy metadata and submitted commands. Previous individual native evidence for demos 1–3 remains in their source handoffs. The Windows export validates runtime, managed libraries, resources and exclusion of development files. Packaging/publication Python tests: 8/8 passed. The README, prepared modes and approval/feedback instructions were inspected inside the actual archive.

Windows package validation must pass on the delivery commit before notification. Its headless startup and real .cmd routing checks are not Windows graphical testing or Mendon's rules acceptance. Neither is an external multiplayer match claimed. The source retains separately unapproved inherited stabilization rules candidates, which must be resolved with Mendon before integration; UI approval does not approve those mechanics.

This resumes delivery-tooling issue #297 / PR #298 and the existing private-release workflow. No production/main branch merge, repository visibility change or new access grant is part of delivery. GitHub mention/review requests provide the notification; email delivery/read status cannot be verified.
