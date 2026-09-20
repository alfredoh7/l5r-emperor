# Decision chooser (#339) — ready for individual review

This is **1 of 8** approved UI previews. **No merge into main is authorized.** PR [#392](https://github.com/alfredoh7/l5r-emperor/pull/392) remains a draft against the isolated `codex/ui-eight-preview-base`; the other seven tasks remain pending. Test success and human acceptance do not authorize a merge.

## Open the prepared demo

On Alfredo's Mac: [L5R Decision Chooser — Review.app](../../artifacts/L5R%20Decision%20Chooser%20-%20Review.app).

The local app is generated, not checked into Git. On another Mac checkout, run:

```sh
python3 tools/create-review-launcher.py 'Review Decision Chooser.command' 'L5R Decision Chooser - Review'
```

Or open the checked-in `Review Decision Chooser.command`. This builds this review checkout, verifies the artwork dependency, then opens the prepared Yumaru choice. It never fetches another branch or merges main. The artwork is installed locally for offline launches; the private dependency is [catalogue-art-ui-eight-20260921-r2](https://github.com/alfredoh7/l5r-emperor/releases/tag/catalogue-art-ui-eight-20260921-r2).

## Short review checklist

1. **Yumaru:** the sidebar should immediately show all three movement choices, with none selected. Select an option; the board must wait until **Confirm choice**. Use **Restart demo** to try the other outcomes. Keyboard focus and Enter also select an option before confirmation.
2. **Source inspection:** **Inspect source** opens the complete card; closing it must leave the decision pending. The source is highlighted on the table.
3. **Next demo → Fukui:** review the performing Ancestor and target, then choose **Review cost choices**. Either bow or destroy the Ancestor; select and confirm. Restart to try the other cost. **Back to target** clears the draft cost.
4. **Next demo → bowed Ancestor:** bowing should be disabled with its explanation; destruction remains available. The created Ancestor is removed from the game when destroyed, as required by the current engine.
5. **Next demo** returns to Yumaru. Review controls are confined to this demo mode.

## Implementation and approved visual direction

- A's automatic sidebar with B's full source-card preview and full-size inspection, using the existing approved lacquer assets, Caudex and WeatheredQuill. Reference concepts are the parent checkout's `artifacts/ui-eight-design-review-2026-09-20/images/01-decisions-a.png` and `01-decisions-b.png`.
- The native captures below were inspected against those references. The compact source row, verified period scans and live game layout replace illustrative generated card art; no claim of exact full-screen pixel equivalence is made.
- Engine-listed answers for effectChoice, preservingHonor, rinShame, conqueror, declineStraighten and forcedDiscard appear independently of card selection. Times of Strife retains its dedicated presentation.
- Explicit selection before confirmation, disabled reasons, keyboard controls, source inspection and opponent waiting. Pending answers submit the engine's complete command unchanged.
- Alternative costs group by source, ability and performer. Target selection precedes “How do you pay?”. Gold, when quoted, continues through the existing payment panel.
- Additive `AlternativeCostView` metadata retains grouping and labels for disabled options whose Command is null. The UI does not invent commands or determine legality. Mendon's review is requested for this shared contract and the prepared fixtures.
- Yumaru and Fukui use real published abilities. Other generic fixtures exercise engine choices; the Conqueror grant, choose-straighten test definition and prepared forced-discard queue are scenario setup, not proof of a complete published-card trigger chain.

## Verified evidence

- `bash tools/check.sh --skip-client`: all requested deterministic checks passed (`artifacts/decision-current-main-check.log`), including the expanded showcase regressions, the newly merged created-card/player-target suites, 34/34 AI, 8/8 server and 8/8 transport. This final run includes main `0d694f02` and the compact footer refinement.
- Expanded focused showcase checks: 3/3 passed (`artifacts/decision-fixtures.log`), covering all three Yumaru consequences, stale-command rejection, opponent privacy, disabled-cost metadata serialization and all five additional generic decision types. Earlier focused suites: 16/16 alternative-cost and 10/10 effect-choice passed.
- Final client build: zero warnings/errors; Godot headless import passed.
- Native Mac: all three Yumaru outcomes, Fukui destruction and bow payments, bowed-option reason, opponent waiting, all five other generic types, explicit confirmation and reduced motion passed. Resolutions exercised: 1920×1080 and 1280×800.
- Final compact demo check: all option buttons fully inside the visible scroll area; restart, next-demo cycle and clearing the draft via Back all passed (`artifacts/checks/decisions/review-check.log`). After refreshing main, Yumaru, both Fukui cost paths and the complete review-navigation cycle passed again (`artifacts/decision-current-main-native.log`). Godot regenerated the two newly shipped scan import UIDs during that import; those native values are retained.
- Actual `.app` launch verified through the Mac UI: it builds and opens the prepared Yumaru decision. Full source inspection and demo navigation were also checked in the native window.
- Updated artwork dependency: 2,897 verified scans, private prerelease asset uploaded; offline installation verified. Bulk artwork stays out of Git.

![Yumaru review at 1280×800](../screenshots/decision-chooser/yumaru-review.png)

![Fukui with a bowed Ancestor at 1280×800](../screenshots/decision-chooser/fukui-bowed-review.png)

## Boundaries and remaining review

The preview baseline integrates current main `0d694f02` (including PRs #390 and #391) with the approved UI lineage, on `codex/ui-eight-preview-base` (`1ab2d75a`). Its inherited stabilization rules candidates remain unapproved; see the eight-task review plan. Main and the root checkout were not modified.

The PR uses a preview base. The routing check is skipped; GitHub Mac/Windows verification jobs are running and the context/handoff check passed on the prior pushed revision. Local results above are not a CI-pass claim; check the PR for the latest revision status. A multiplayer reconnect session, native Windows graphical play and expert rules acceptance have not been performed for this preview. Alfredo's individual UI acceptance and Mendon's shared-contract review remain outstanding. **No merge permission has been granted.**
