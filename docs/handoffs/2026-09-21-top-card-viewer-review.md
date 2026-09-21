# Top-card viewer review — #323

PR: https://github.com/alfredoh7/l5r-emperor/pull/428. Branch `codex/323-top-card-viewer`, stacked on opponent-payment preview #427 (`09774bbd`). Main through `9876402c` is integrated, including Mendon’s newly merged province-strength/Terrain update #424. That merge was conflict-free; its rules changes are inherited from main. Nothing is merged into main. #427 has passing exact-head collaboration, Mac and Windows checks and awaits human review.

## Approved design and behavior

Alfredo selected concept A, removing duplicate bottom thumbnails and redundant bottom-left wording. The native tray has one row of full scans, destinations and numbered return positions under the cards, running engine-supplied counts above, and an explicit confirmation. Reorder buttons support keyboard and mouse. Selected outgoing cards have a gold frame; hover enlarges a full face and clicking opens inspection. Reset arrangement is local only.

The tray uses the existing approved lacquer/brass frame pixels and game fonts, not reconstructed card art. Source/native screenshots are compared in the local gallery at `http://127.0.0.1:8799/native-top-cards/`. Actual engine situations differ from the concept: Banish All Doubt returns its other cards to the bottom, so the demo says bottom. This is not a claim that the full screenshot is pixel-identical to ImageGen.

The engine remains authoritative. Only `PendingLook` supplies viewed cards, ownership, legal destinations, disabled reasons, limits and reorder permission. No deck contents are queried in production UI. Each card retains its engine position token and Confirm sends one `arrangeTopCards` command. Min/max counts are presentation guards based on engine fields; the server validates the actual command. Optional deck selection uses both deck kind and owner and advances with one click. No shuffle or cancellation of the engine choice is added.

The opponent deck is explicitly labeled. Cards and draft order stay private to the viewer. Seat, match, version, busy and disconnect changes clear/rebuild the local view; open private inspections also close. A waiting player receives only a public waiting explanation. Public show-and-take uses the existing engine history. Fixed-order, short-deck and no-eligible cases follow engine fields; an empty deck resolves without trapping the player.

## Prepared launcher

Use `Review Top Cards.command`. On this Mac, `python3 tools/create-review-launcher.py 'Review Top Cards.command' 'L5R Top Cards - Review'` creates `artifacts/L5R Top Cards - Review.app`. The launcher builds the current checkout and opens an isolated practice profile directly in the viewer. `artifacts/L5R Emperor - Latest.app` opens the ordinary combined checkout.

Prepared cases:
- **Reorder:** Public Records, with real engine reorder resolution.
- **Take + top:** explicitly a training primitive exercise requiring an Item, with real published scans; no invented published-card rules.
- **Take + bottom:** Banish All Doubt with an unbowed Tactician and four cards.
- **Any Fate deck:** the published Signs and Portents Event, including the opponent’s private deck view.
- **Show + take:** Bayushi Kahoku, including type/keyword restrictions and public reveal.
- **Fixed order:** a training primitive exercise; only discard selection is allowed.
- **Other seat:** check that private cards and an unfinished arrangement disappear.

The review navigation only exists in this prepared demo. It constructs initial test state and then uses real engine commands to open/resolve choices; it does not inject fake pending views.

## Validation

- Client compilation passed with zero warnings/errors; Godot import and local artwork coverage verification passed.
- `bash tools/check-top-cards-ui.sh '1920x1080 review-normal' '1280x800 reduced'` passed on the final implementation. The earlier standard 1920×1080 run also passed before the last label/layout polish.
- Native checks exercise mouse and keyboard reordering/menus, full-face hover/inspection, required/min/max and forbidden choices/reasons, exact engine return order at top and bottom, take/discard/show outcomes, either deck owner, waiting privacy, inspection cleanup, seat/draft reset, fixed/short/empty/no-eligible cases, busy pause/resume and prepared navigation. The harness compares the resulting real engine deck order with the displayed arrangement.
- After integrating main `9876402c`, `bash tools/check.sh --skip-client` passed all requested deterministic suites and `bash tools/check-top-cards-ui.sh '1280x800 integrated-reduced'` passed the complete native viewer exercise again. The prepared app launcher was reopened against the integrated build.
- The complete 2,959-image artwork dependency is published privately as `catalogue-art-pr428-20260921-r1`. GitHub confirms 403,226,088 bytes and SHA-256 `262fc197eda909f48409552d48c37e675a9e923526ea06de41088f76d6e1ab01`, matching the installed/committed receipt. This includes all three new main printings; prior previews’ packages are preserved.
- Final native logs have `TOP_CARDS_PASS` and no resource-leak warnings. A first layout probe caught footer wrapping expanding the tray; explicit button widths and compact labels fixed it. An unparented optional header container was also removed before the final runs.
- Actual `.app` launch passed. Manual native clicks reordered cards and navigated through Signs and Portents into the opponent’s deck, with ownership visible and a separate arrangement confirmation.
- Screenshots in `docs/screenshots/top-cards/` show normal and reduced native output. These are not Windows graphical evidence or human rules acceptance. CI is checked against the published implementation head.

Alfredo hands-on review and Mendon review remain pending. No main merge is authorized; inherited stabilization rules candidates remain unapproved. The active goal still requires #345, #311 and #400, followed by combined review delivery.
