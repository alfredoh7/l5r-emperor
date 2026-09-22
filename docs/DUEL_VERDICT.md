# Duel verdict implementation — 2026-09-17

Issue #205 / PR #220. The exact approved hybrid source is [the centered verdict](../assets/ui/duel-results/approved-centered-verdict.png): soft summary shadow, warm winner glow, centered consequences with no side icons. Runtime artwork remains the approved Crimson Standoff mat/landscape, original published cards and existing red stroke. Semantic values use live Caudex and Musashi Brush typography.

## Presentation

The engine-revealed cards turn over on the mat, then move between their actual projected corners into retained evidence positions. Final totals and their recorded stat/focus breakdown fade in. After a 2.25-second comparison beat, the winner glow and labels enter while the defeated card settles onto the same mat plane. Consequences begin at 3.35 seconds and receive 3.2 seconds per source group. The final state remains until Continue. The losing side receives the existing Japanese red brush stroke at 42% opacity, bounded above the total and contribution line. Both sides receive it for a double loss, and neither for refusal. Sparse commitments use larger faces; three/four cards use two compact rows with clearance from the gold border.

Card interpolation is quintic, with zero endpoint velocity/acceleration. Phase prompts use 250 ms smooth fades. Stakes and subsequent reaction accounts fade in/out; the established cinematic introduction retains its deliberate camera cuts. Menu/inspection locks pause moving cards and the cinematic clock together. Skip and reduced motion settle the same result immediately. Resizing clears obsolete screen-space transfers and lays out the current state without replaying the ending.

Revealed cards and defeated attachments are public historical evidence, not a second playable pool. Hover/click uses the main-game inspector and identifies the historical context. The actual discard, dead and removed-card piles remain authoritative and accessible. The now-irrelevant hand retracts on resolution, including when an aftermath reaction is pending; legal reaction controls remain active after the presentation.

The engine appends a defaulted `ResolutionReason` to its retained result, recording the existing higher-total / Duelist / both-lose / refusal branch. There is no change to winner calculation, legality or execution order. Refusal never shows invented 0–0 arithmetic. The held summary selects the first two source groups with changes, retaining no-change groups when there are no other consequences. Every group is shown in execution order during motion, and the complete source-linked account remains in Duel details.

## Validation

- `bash tools/check.sh --skip-client`: **491 checks pass** (43 collaboration, 132 content, 6 Meshy tools, 172 Core, 52 published, 9 deck-format, 10 workshop, 56 AI, 5 server, 6 transport). Includes reason branches, legacy snapshot deserialization and replay checks.
- Client compilation: **zero warnings/errors**. Full `tools/check-duel-demo.sh`: normal and compact reduced 20-step flows, 13 special scenarios, and both 43-step guidance paths pass. The guidance probe now waits for menu readiness; an earlier fixed-delay entry race and a desktop-interference hover failure were followed by a passing isolated full run.
- Final brush-stroke follow-up: native canonical result, mirrored seat and **7–7 double loss** inspected. Both-loss strokes stay above both breakdowns. Four-focus retained evidence stays within the mat border; refusal shows no fictitious arithmetic.
- Two native pause checks freeze card movement, cinematic time and engine version together. Skipping the moving verdict settles the public evidence and submits no gameplay command. Shared inspection of retained cards passes.
- A separate **40.833-second, 1920×1080/30fps silent video** completes four real commands, canonical **8–7**, one opening/ending, destruction, Courtesy and restored priority. Sequential reveal, fall and summary frames were inspected. The local video is `artifacts/duel-verdict/final.mp4`; it is not committed as source.
- A separate real-time Apple M1 Max run measures **4,487 completed frames** after startup: median **8.390 ms**, P95 **13.815 ms**, P99 **14.431 ms**, max **37.358 ms**. There was **one frame above 33.34 ms during focus selection**, and none during the ending phases. Screenshot/movie encoding is excluded. This does not establish universal performance or certify AAA art quality.
- Development launcher regenerated; strict artwork preflight, build and headless startup pass. The native suite exercises Play-menu entry. Unrelated project settings and catalogue imports are preserved and excluded.

[Evidence and reports](screenshots/duel-demo/verdict/validation.json) · [Native result](screenshots/duel-demo/verdict/result.png) · [Opposite seat](screenshots/duel-demo/verdict/opposite-seat.png) · [Double loss](screenshots/duel-demo/verdict/both-lose.png) · [Performance](screenshots/duel-demo/verdict/performance.json)

The approved source and native image were compared at the same width. Original published scans and engine-provided text intentionally replace illustrative concept card text; the result is not claimed to be a pixel-identical render.

## Remaining review

Mendon's mechanical/shared-interface review remains required, including the previously recorded effect-order question and effective province-strength reporting under Times of Strife. Normal-match activation is still separate. The table-to-Crimson entry preview in PR #266 is a separate task; this result pass changes no main-board transition files. The duel stays silent.
