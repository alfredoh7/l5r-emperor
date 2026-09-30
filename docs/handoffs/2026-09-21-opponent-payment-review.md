# Opponent payment review — #377

Implementation branch: `codex/377-opponent-payment`. Draft PR: https://github.com/alfredoh7/l5r-emperor/pull/427. Base: `codex/ui-next-preview-base` (`90d65013`), incorporating current rules through `99875bc5` and the previous approved UI preview stack. Nothing is merged into main.

## Approved design

A’s sidebar plus C’s immediate outcome feedback, with a plain gold coin and no numeral. The existing lacquer frame, full real card scans, Caudex body and WeatheredQuill headings are reused. One new raster coin was generated with the built-in ImageGen tool; exact production source and prompt are in `src/L5R.Client/Art/Payments/provenance.json`.

The native view retains the source card, first payment, amount, outcome, Gold sources and confirmation in the sidebar. Selected sources appear first in a horizontally scrollable row; the payment coverage and excess remain next to Confirm. This adapts the concept to arbitrary source counts rather than assuming exactly two holdings. Source/actual comparisons are in Alfredo’s local concept gallery at `http://127.0.0.1:8799/native-payment/`.

## Player behavior

The engine’s `PendingOpponentPayment` opens automatically for the payer. The default zero payment is reviewed, never silently submitted by `AddAction`. Amount changes use the engine’s bounds, quotes, suggested sources and exact `Outcomes` text; the UI does not calculate Honor. A single Confirm sends the chosen amount and sources. The board’s own ready Gold sources also toggle the payment once an amount is chosen. Underfunded payment is disabled. Choosing zero submits no sources.

The actor gets a read-only source/first-payment/waiting view. Their payload and controls contain no private quote, outcome preview or payer selection. Version, seat and connection changes reset a draft. Automatic no-Gold resolution shares the existing persistent public explanation. The engine owns resolution, reaction continuation and logs.

## Prepared launcher

`Review Opponent Payment.command` opens a real Virtuous Charity decision immediately in an isolated practice profile. On this Mac, `python3 tools/create-review-launcher.py 'Review Opponent Payment.command' 'L5R Opponent Payment - Review'` creates `artifacts/L5R Opponent Payment - Review.app`. `Play Latest.command` and `artifacts/L5R Emperor - Latest.app` open the ordinary combined checkout.

Use **Opponent paid 3**, **Opponent paid 0**, **No Gold available**, and **Other seat** to restart cases or inspect either player. The fixture uses published full-scan cards and real engine commands; it does not inject a fake pending choice. Review controls exist only in this prepared demo.

Review checklist:

1. Raise your amount to 3, 4 and the maximum; read the immediate engine outcomes.
2. Choose payment sources through the cards or on the table; remove them to see Confirm disabled, then restore them.
3. Inspect full cards by hover or click. Use Tab/Enter on amount, sources and confirmation.
4. Switch seats before confirming; the opponent cannot see your private draft. Return to the payer; the amount resets to zero.
5. Confirm a payment, then restart. Try paying zero and the automatic no-Gold case.

## Validation state

- `bash tools/check.sh --skip-client`: all requested integrated deterministic checks passed, including new main’s after-resolution and Gold primitives. Full local log: `/tmp/l5r-377-baseline-check.log`.
- Client compilation: zero warnings and zero errors. Godot resource import passed.
- `bash tools/check-opponent-payment-ui.sh`: native 1920×1080 normal and 1280×800 reduced runs passed; the prepared 1920×1080 review-navigation run also passed. These cover real keyboard/mouse amount and source controls, source selection on the table, zero, tie, payer/opponent Honor feedback and cap, insufficient sources, engine resolution, hover inspection, waiting privacy, seat/version reset, busy guard and automatic explanations.
- The native input harness now waits for actual OS focus before table clicks. Earlier unfocused macOS launches correctly rejected table input; activating the game title bar allowed the complete prepared run to pass. No input guard was bypassed.
- The prepared-navigation assertion was corrected to expect the waiting seat after its first toggle, then the payer on return. Both directions passed.
- Representative native evidence is in `docs/screenshots/opponent-payment/`. Source/native comparison uses the actual concept and game screenshots at comparable sidebar widths. The plain coin is generated production artwork; other surfaces reuse existing approved pixels. This is not an exact full-screen pixel-match claim.
- Actual `.app` launcher opened the prepared real-engine case. Native manual clicks verified amount 4, immediate Honor feedback, the public-only waiting seat and automatic no-Gold explanation. The existing `gold-variable-confirm` native regression passed (`UI_SMOKE_PASS`, capture `Ok`).
- The launcher exposed ten current-main printings missing from the inherited artwork package. The complete 2,956-image package is published as private prerelease `catalogue-art-pr427-20260921-r1`; GitHub confirms the 402,873,324-byte asset SHA-256 `34c1d2a003ab333b13ad03888b21718efd0f9b5edfd5dd97de209a8c69fc2895`. Local manifest coverage verification passed. Artwork binaries remain outside Git.
- Exact implementation-head CI is checked after publishing; earlier draft-only CI is not evidence for this implementation.


Alfredo’s concept acceptance is not hands-on approval. Mendon review, Windows graphical playtesting and explicit merge authorization remain outstanding. The inherited stabilization rules candidates remain unapproved. The other four approved designs (#323, #345, #311, #400) remain to be implemented under the active goal.
