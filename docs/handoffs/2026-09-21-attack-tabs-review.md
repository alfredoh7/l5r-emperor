# Approved F attack tabs and demos 5–8

Alfredo approved all four UI previews and selected F's temporary bow/katana source tab with a subtle slide. [Approval and merge gate](../decisions/2026-09-21-approved-ui-5-8.md). Mendon review and required CI remain necessary; this task does not merge anything into main.

The tab takes 180 ms to slide out/in, stays on the current source through targeting, confirmation and pending reactions, and retracts on cancellation/resolution. Reduced motion changes immediately. Retargeting keeps the same tab; switching the explicitly performing Follower changes the mount. Public reaction cues can be shown to either seat; private unfinished selections clear on a seat change. Menu, winner and disconnected state clear the cue. Offscreen sources are clipped with the table. The marker ignores input, preserves Force counters and adds no lower plaque. Attack names remain in the existing action panel.

`AttackCueView` carries only canonical engine kind and source/explicit performer. The optional ActionOption/AvailableAction field and MatchView pending cue preserve older-payload defaults. No attack rules, payment/strength calculations, target legality, commands, historical printings or hidden-state decisions move to the client. Pending cues reject hidden/off-board sources. Shared-contract review is requested from Mendon.

## Open each Mac demo

- [5 — Either/Or](../../artifacts/L5R%20Either%20Or%20-%20Review.app)
- [6 — Copy](../../artifacts/L5R%20Copy%20-%20Review.app)
- [7 — Movement](../../artifacts/L5R%20Movement%20-%20Review.app)
- [8 — Attack targets and type tabs](../../artifacts/L5R%20Attack%20Targets%20-%20Review.app)

Portable `Review … .command` entries build this checkout and prepare separate review profiles. The Windows batch is published through PR #298 and announced on PR #410 only after the exact uploaded package passes Windows checks. It contains a named `.cmd` for each demo, Start Review, the runtime/art and feedback instructions. No source checkout or SDK is required for Mendon.

## Evidence on this revision

- Full `bash tools/check.sh --skip-client` passed. Targeted action/reaction suite 31/31 (new display-contract lifecycle cases), AI 34/34, server 8/8, client transport 12/12. Ranged/Melee kinds, attached sources, explicit performing Followers, before-cost and attack reactions for both viewers, clearing after resolution, hidden-source exclusion and older payloads are covered. Commands retain their explicit null/card/province targets.
- Client compilation and Godot import passed with zero build warnings/errors.
- Attack native checks: 1920×1080 normal, 1280×800 reduced, and final 1280×800 reduced-review-check passed. Input coverage includes keyboard, sidebar and actual table clicks, confirmation/cancel/retarget, No target, card hit, reaction, Follower legality, no-card cases, Melee, province, bow costs and Tensin. New checks cover normal enter/exit midpoint, reduced-motion immediacy, no restart on retarget, counter clearance, close zoom, both seats during a pending reaction and selected-Follower changes.
- The initial unfocused run failed table retargeting; raising the native window fixed focus without weakening input assertions. A seat-view check initially inspected a court outside the retained camera viewport; the check now fits the table before verifying visibility. A frame-count-only wait inspected a still-running source-change slide; the check now waits the animation's wall-clock duration. Final full native runs passed.
- Native Either/Or, Copy and Movement `1280x800 reduced-review-check` all passed again on this combined build. Their individual functionality/checklists remain in the [eight-demo index](2026-09-21-eight-ui-review-index.md).
- All four refreshed Mac `.app` launchers were opened through the desktop and visibly reached their named prepared scenarios. The actual Attack launcher also switched to Melee with a desktop click, showing Stalking Tiger and the katana. Test game processes were closed afterward. Launcher paths were checked against this checkout.
- [Source/native comparison](../../assets/ui/attack-targets/comparison.html) was inspected in the browser at comparable card width and through actual playable normal/small-window captures. Exact approved F and the built-in imagegen production atlas/prompt are retained. Full card scans and the red brush remain intact; no whole-screen pixel-equivalence claim.
- Main through `828f1bb7` is integrated. Its two additional scans are included in the refreshed 2,917-image private catalogue dependency `catalogue-art-pr410-20260921-r2`; SHA-256 `c3b498825eacdddce9550cd183e2d849c8ca4dbaace85e499e6d102d224bfdfd`. The complete archive was built and installed with integrity verification. Normal launch keeps the coverage gate.

Windows graphical play, external multiplayer and Mendon's rules/shared-contract acceptance remain open. UI approval does not approve the inherited stabilization/rules candidates or every stacked dependency. Read final CI at the exact PR head, and the delivery receipt/notification for the exact Windows package. Root checkout/main are unchanged.
