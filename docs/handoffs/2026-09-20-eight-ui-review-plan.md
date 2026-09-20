# Eight UI previews — Alfredo's review batch

## Delivery and acceptance

Alfredo approved these concept combinations. Implementation completion means a tested, launchable review candidate. **No merge into main is authorized.** Each task needs its own clickable Mac launcher opening a prepared real-engine scenario, a short test checklist, native screenshots, and honest limitations. Alfredo may approve or request changes independently. Rules acceptance and native Windows testing are separate from automated checks.

| Order | Issue | Approved direction | Current evidence |
|---|---|---|---|
| 1 | #339 | A sidebar + B clearer source preview; reusable choice and alternative-cost flow | Ready for individual review; [PR #392](https://github.com/alfredoh7/l5r-emperor/pull/392), native Mac checks and dedicated launcher; no merge |
| 2 | #315 | B shown-card history sidebar | Ready for individual review; [PR #393](https://github.com/alfredoh7/l5r-emperor/pull/393), native Mac and live reconnect checks, dedicated launcher; no merge |
| 3 | #347 | A restriction seal | Pending |
| 4 | #314 | B full-card gallery inside A search panel | Pending |
| 5 | #343 | A sidebar + B explicit consequences | Pending |
| 6 | #342 | B copy tray + A compact result summary | Pending |
| 7 | #346 | A; reuse current battle assignment arrows and province placement | Pending |
| 8 | #355 | A explicit target choices + B full-card previews | Pending |

Approved concepts and prompts are in the local `artifacts/ui-eight-design-review-2026-09-20` gallery in the parent checkout. No new concept approval is required for these combinations. Preserve full card scans, lacquer/Caudex/WeatheredQuill presentation, reduced motion, and sparse table labels. Do not use Impeccable.

## Isolated preview baseline

The task branch starts at main `20708c37` and integrates the original Regions/Celestials UI lineage `7360ff1c` (including prior UI consolidation and stabilization). Original ancestry avoids replaying the squashed stabilization stack against newer mechanics. Before delivery, main was refreshed to `0d694f02` (merged #390/#391); the preview base is now `1ab2d75a`. This does not merge any work into main or alter existing review branches.

Compatibility work:

- Current `GoldQuote`/`TargetGold` replaces the earlier `ActionOption.GoldCost`; support-card payment UI and regressions use the engine's quote and suggested payments. Remove the obsolete duplicate EquipCost helper.
- Preserve current specific before-action and targeted-reaction discovery; the older generic discovery is retained only for bow reactions.
- Include both Infantry and Cavalry maneuver phases in battle presentation. Present attack declaration separately from assignment.
- Times of Strife now uses the shared `forcedDiscard` engine command. Its existing Event presentation handles that exact public prompt; other forced discards are for #339's generic chooser.
- Move the inherited stabilization/Event regressions into the current test discovery convention; preserve their behavior coverage.
- The earlier stabilization corrections (Weakness target-base Force, keyword-only Shadowlands, mandatory Fate costs and Earth Flows) are carried as already documented review candidates in `2026-09-17-non-duel-stabilization.md`. They remain **unapproved shared rules changes**, not newly accepted interpretations. Current main's wider reaction, Gold, multi-target and timing primitives remain authoritative.

No feature, native validation or preview readiness is claimed until its evidence is recorded below or in the task handoff. #339 ownership is recorded on GitHub; the project-board token is unavailable.
