# Multiple-target chooser review — #345

PR: https://github.com/alfredoh7/l5r-emperor/pull/429. Branch `codex/345-multi-targets`, stacked on #428 at `dd204714`. Main through `c8e13901` is integrated conflict-free, including Mendon’s #425. No merge into main is authorized. Inherited UI/stabilization candidates remain subject to their own reviews.

Alfredo approved A’s board selection plus C’s expandable groups. That is design acceptance; Alfredo’s hands-on acceptance and Mendon’s rules review remain pending.

## Result

- Reads the engine’s `ActionOption.Slots`, count bounds and candidates. Required selections start empty; no offered default silently selects the first target.
- Each up-front group expands independently. Choose cards on the projected table or in the full-scan list, or choose an engine-listed player. Selected cards appear first in chosen order, with matching numbered curved board links. Collapsed attachments retain their physical stack and expose their normal top strip; close markers separate instead of covering each other.
- Running counts, explicit confirmation and cancellation. Optional zero is supported. Keyboard selection, order controls, hover and large inspection use the same local draft. The fixed footer stays in the window.
- At-resolution slots remain informational until the engine opens the existing explicit `effectChoice` chooser. Yoshitada’s honorable target asks nothing; dishonorable and Fear Me! follow-ups occur only after the engine’s first effect.
- Each before-action reaction names/shows its actual current target, highlights that card, and frames the battlefield when the target or viewing seat changes. It does not infer or skip a reaction window.
- Drafts and private inspection reset on changed seat, match/version, busy state and disconnection. Commands preserve slot order and player card order. Engine validation remains authoritative, including any cross-slot combination constraints in newer cards; this client does not infer extra candidates or rules.
- Existing alternative-cost review can preserve a multi-target draft. Existing single-target cost and Attack behavior remain intact.

## Review launcher

Run `Review Multiple Targets.command`, or create the local app with:

`python3 tools/create-review-launcher.py 'Review Multiple Targets.command' 'L5R Multiple Targets - Review'`

On this Mac: `artifacts/L5R Multiple Targets - Review.app`. The actual app was opened and verified in Godot, including manually selecting an attachment and switching to Yoshitada. The normal `artifacts/L5R Emperor - Latest.app` was also refreshed. The prepared launcher builds this checkout, never fetches or merges a branch, and uses its own practice profile.

Prepared cases: Bonds of Coin; Yoshitada; honorable target; Fear Me!; Groups*, Optional*, Hand* and Reactions*. Asterisks explicitly identify training fixtures for generic behavior. Fixtures create initial state and use real engine actions; they do not inject invented pending views or card legality.

## Validation

- Client compilation: zero warnings/errors.
- `bash tools/check-multi-targets-ui.sh '1920x1080 review-integrated' '1280x800 integrated-reduced'`: passed after integrating main `c8e13901`. Earlier standard/reduced runs also passed; native inspection found and fixed stacked marker overlap and offscreen reaction focus. The final disabled-zero caption is a wording-only follow-up compiled after these runs.
- Native coverage: projected attachment picking, keyboard selection/reordering, upper/lower bounds, exact destroyed set, matching ordered links, private inspection/seat reset, published conditional Honor choices and Fear Me!, independent player/card groups, optional zero, private own-hand discard, busy reset and sequential per-target reactions. Prepared navigation was exercised. Actual 1920×1080 and 1280×800 captures were inspected beside A/C.
- `bash tools/check-decision-chooser.sh '1280x800 fukui-pay-bow' '1280x800 generic'`: passed (existing alternate cost and five generic choice types).
- `bash tools/check-attack-targets-ui.sh '1280x800 reduced'`: passed, including actual projected table clicks, brush/tab clearance, costs, Tensin and reactions. The first attempt had no macOS window focus. The probe now waits for real OS activation instead of sending a click into an intentionally blocked table; the rerun had focus and passed. No production focus guard was bypassed.
- `bash tools/check.sh --skip-client`: all requested checks passed on integrated main, including 12/12 client transport cases. Exact results remain in ignored `artifacts/checks/` and the session’s full check log.
- Private complete artwork pack: `catalogue-art-pr429-20260921-r1`, 2,964 images, 403,922,870 bytes, SHA-256 `3097336d303ee736839137788877786ed1a37bd967cfa633bde53512b4d3d5c9`. Installed coverage and uploaded GitHub asset size/digest verified. Previous preview packages preserved.

## Visual evidence and limits

`docs/screenshots/multi-targets/` contains native selection, conditional, groups, reaction and reduced-motion evidence. The local approved gallery links `native-multi-targets/` with A/C beside the actual implementation; all images load. It preserves original scans and existing lacquer pixels. Dynamic card positions, perspective, native controls and text differ from generated illustrations; no full-screen pixel-equivalence claim is made.

This task authors client presentation, fixtures and artwork receipts. Core/data changes in its branch are inherited preview work and the explicit current-main merge, not new rules authored for #345. The cumulative stack audits as shared and requires Mendon review. Exact final-head CI is checked after publishing; CI/export is not native Windows gameplay or human acceptance. The goal still includes #311, #400 and combined review delivery.
