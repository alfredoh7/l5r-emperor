# Approved demos 5–8 — refreshed Windows review delivery

Alfredo approved Either/Or (#343 / PR #401), Copy (#342 / PR #403), Movement (#346 / PR #404), and Attack targets (#355 / PR #410). He requested Windows demos and a notification for Mendon. UI/QoL feedback should be recorded for Alfredo; merging requires Mendon approval and required checks. Nothing is merged by this delivery.

## Exact refreshed candidate

Game source `6b6908d7b4f020b894a4071c3db925d98840166f` includes the final requested spear-clearance sequence: the complete spear/Unit Force ribbon lowers for 160 ms, then the attack tab enters for 180 ms. Dismissal reverses that sequence. The corrected Ranged/Melee review buttons are included. Main through `485320af` and its complete verified 2,946-image art pack are included.

`previews/approved-ui-5-8.json` pins this revision. The matching uploaded receipt is `previews/published/approved-ui-5-8.json`. One self-contained ZIP contains Start Review, Demo 5 — Either/Or, Demo 6 — Copy, Demo 7 — Movement, Demo 8 — Attack Targets and Open Full Game command launchers, runtime, artwork, isolated settings, feedback shortcut and review instructions. No source checkout, SDK or Godot editor is needed. Quit between demos.

The previous `552b54a4` release remains immutable. It is superseded for this review by the new release following its successful exact Windows package validation; it was not replaced in place.

## Evidence

- Source build/import and full deterministic checks passed. Native Attack 1920×1080 normal and 1280×800 normal/reduced review checks passed, including intermediate clearance/reversal frames, source lifecycle, both seats, zoom, performing Followers, native table input and prepared-navigation clicks. The actual Mac app opened and the Melee and cancel controls worked through desktop input.
- Demos 5, 6 and 7 each passed native 1280×800 reduced-review-check again on this same source before packaging. All four Mac app launchers resolve to this checkout’s named prepared commands.
- Packaging/publication Python regressions pass 9/9. The exporter validates the actual Windows runtime, resources and source exclusion; staging verifies the uploaded byte count and SHA-256.
- Source Mac CI initially failed before compilation because bootstrap’s download timed out. Attempt 2 of run 35598262930 passed on the same game commit; both source Mac and Windows checks are now successful. This was not a native/UI test failure.
- The Windows package job downloads the actual uploaded ZIP, validates its checksum, starts its exported .NET executable headlessly and checks isolated data. It runs byte-identical copies of every launcher through Windows command handling in a path with spaces and punctuation, against an argument recorder. This validates routing, not graphical play.

## Delivery gate and human review

`publish.py deliver` requires a successful Windows review package job on a commit containing this exact receipt. It then verifies the uploaded asset again and posts one deduplicated @mdornbrook notification containing the download, per-demo checklist, Alfredo’s approval, merge gate, feedback request and Claude launch prompt. The gate passed and delivery completed; exact evidence follows.

Mendon’s graphical/rules/shared-contract review, native Windows graphical play and external multiplayer remain human acceptance. UI approval does not approve inherited stabilization/rules candidates. GitHub mention delivery is verifiable; email receipt/read status is not claimed.

## Completed delivery

- [Updated Windows release](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr410-6b6908d7b4-r2-windows). ZIP `L5R-Review-pr410-6b6908d7b4-r2-Windows.zip`, 1,425,343,714 bytes, SHA-256 `cb878ec60d227126bab09e9f6876f804dc8f6a92196e349ad9d74f0c101479c0`. The uploaded digest and size match the local archive and committed receipt.
- [Windows package validation run 35599418450](https://github.com/alfredoh7/l5r-emperor/actions/runs/35599418450), successful on receipt/tooling commit `9244505b5109db5c321f01094c98172e234af8f1`. Its log confirms the exported runtime and all six launchers for this new batch, plus the retained 1–4 and Regions/Celestials packages. Subsequent documentation-only commits do not change that receipt, specification or uploaded artifact.
- [Verified @mdornbrook notification](https://github.com/alfredoh7/l5r-emperor/pull/410#issuecomment-5760497921) includes Alfredo’s approval, latest spear refinement, per-demo review and feedback request, merge gate, download, hash and Claude prompt. The release is marked Review, not Validation pending. PRs #401/#403/#404/#410 point to this refreshed package and remain open for Mendon review.
- [Game source CI 35598262930](https://github.com/alfredoh7/l5r-emperor/actions/runs/35598262930), successful Mac/Windows checks on exact packaged source `6b6908d7b4f020b894a4071c3db925d98840166f` after the bootstrap retry. Local deterministic/native results are described above.
- All four current Mac `.app` launchers have been opened through the desktop to their named prepared scenarios: Either/Or (Sakuma), Copy (Fukuzo), Movement (Sea Troll Predator), Attack (Horse Archers/Stalking Tiger). Owned test processes were closed afterward. Root checkout/main were not changed, and no preview PR was merged.

Requested implementation, packaging and notification are complete. The remaining actions are Mendon’s review/feedback and any later merge after required approvals/checks. This record does not claim a human Windows graphical playtest or external multiplayer acceptance.
