---
name: card-pass-report
description: Turn an L5R card pass into a reconciled report of wired and blocked cards, printing readings and rulings, scan/art status, validation evidence, and concrete client-lane contract changes.
---

# Report a card pass

Use the actual branch diff, packagers, manifests, test results and generated inventory, not remembered totals. Follow AGENTS.md for sharing and handoff. Read only the relevant printing/ruling decisions and docs/rules/RULINGS.md entries. The supplied [report outline](references/report-outline.md) is a starting structure; scale detail to the pass.

Reconcile the unique cards reviewed with wired/blocked/out-of-scope counts. Identify printed versions by card ID and source/scan evidence. Keep "confirmed by Mendon" distinct from proposed readings, engine inference and unanswered questions. Record confirmed rulings in the numbered register only when the user's actual reply supports them; link evidence and affected cards. Preserve incompatible historical rulings with a supersession link instead of silently rewriting them.

For each blocked card give the concrete missing mechanic, ambiguous reading or matching scan, and the smallest next action. Do not treat blanking printed text, a near-miss proxy, successful compilation or an enabled action as complete implementation. Describe shared mechanics once and map the affected cards to them. Keep original period text separate from later Oracle wording.

Client-lane items must be concrete: new/changed commands and choices, public/private view fields, payment/target selection, replay/reconnect behavior, asset/presentation needs and whether a UI handles them already. Name the authoritative Core/server side and each seat's visibility. A successful Client.Transport test proves serialization/integration, not native UI acceptance. Do not label a pass "no client changes" just because no client file was edited.

Record exact test counts/failures, same-seed evidence when measured, before/after inventory, art-pack tag/hash/coverage, outstanding Godot/native tests and human acceptance separately. Avoid claiming benchmarks from mismatched runs or a merged dependency from a stacked branch. Update the pass decision/report and branch handoff rather than appending conversational history to PROJECT_STATUS.md. Prepare a coherent PR description referencing the issue; no external notifications without the user's/workflow's authorization.
