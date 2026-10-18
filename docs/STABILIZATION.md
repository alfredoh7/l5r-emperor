# Focused non-duel stabilization

Issue: https://github.com/l5r-emperor-dev/l5r-emperor/issues/274
PR: https://github.com/l5r-emperor-dev/l5r-emperor/pull/275

## Scope and results

The authorized pass covers three previously unreachable continuation types (Brash draw, bow reactions and before-action reactions) and the six PR #270 findings: Holding production, The Weakness of Man's target, Shadowlands keyword conditions, Fate-deck discard costs, authoritative Equip discounts, and The Earth Flows' Honor trigger. The existing approved controls and artwork are reused. The [contract/correction record](decisions/2026-09-17-non-duel-stabilization.md) describes exact semantics and the additive public quote.

All nine regression groups failed on the original implementation and passed with fixes. Cases cover repeated draws, stale and wrong-seat commands, legal alternate reaction targets, using/passing deferred reactions, detached Practice decisions, actual Gold payments, differing equip-target costs, older/round-tripped payloads, atomic cost rejection, and opposed/unopposed Honor outcomes.

Approved non-duel UI PR #270 is the explicit stack dependency. Current main #272 (two Expendable Followers) was integrated at 84a4964. The test-list conflict retained both the new follower regression and this pass's target-base correction. The new #277 reaction hooks are not merged into this task; its adjacent Core/query hunks were inspected and remain independently owned by Mendon. The separate duel candidate #268 is preserved.

## Validation

After #272 integration, `bash tools/check.sh --skip-client` passed **477 checks**: 43 collaboration, 130 content, 190 Core, 52 published, 9 deck-format, 10 deck-workshop, 13 custom-Practice, 20 AI, 5 server and 5 client-transport. The prior checkpoint passed 476 before the additional follower test. The client compiled with zero warnings/errors; Godot import and `bash 'Play Latest.command' --headless --quit-after 3` passed.

`bash tools/check-stabilization.sh` passed all **8 native Mac scenarios**, covering Brash draw/decline/opponent privacy, targeted bow reaction, before-action reaction and pass, and 2-Gold/0-Gold equipment confirmations at 1672x941 and 1280x800. `bash tools/check-event-decisions.sh` passed all **8 existing Event scenarios**, including compact windows, last-province defeat, opponent views and full-motion mode. The final native logs contained no engine errors. Representative captures were visually inspected; [validation receipt](screenshots/stabilization/validation.json) and [Brash](screenshots/stabilization/brash-decline.png), [bow reaction](screenshots/stabilization/bow.png), [before-action reaction](screenshots/stabilization/before.png), [free Equip](screenshots/stabilization/equip-free.png) screenshots are retained.

Native diagnostics use real engine triggers and offered commands. Fixture setup is injected explicitly, and initial equipment-card inspection is prepared programmatically. Gameplay gestures use native viewport/GUI mouse input; the embedded pass-device privacy modal uses focused Enter-key input. No button signals are invoked directly. Earlier diagnostic attempts failed to traverse that modal; the corrected final run passed, with normal gameplay untouched. These are automated native checks, not human match acceptance.

The pinned artwork receipt now covers **2,781 images**, including #272's two Followers. The 379,799,472-byte archive has SHA-256 `e653ddb960b94997a2021c4d4a33644522048f0ff3d8e37fa407b0a37a239d85`; offline installation verification passed. Private release `catalogue-art-webp-v8-20260917` uses an exact-hash rebuild of v7 plus the two new scans. The temporary publisher branch/job is delivery tooling, not a game change or stack dependency. The development launcher points at this stabilization checkout.

GitHub CI and mdornbrook's shared review must pass before merging. The approved non-duel stack remains unmerged. The context helper hit its known null-files error while inspecting open PRs; Git fetch plus direct GitHub REST/CLI queries supplied current main, issue and overlap facts. No board token is configured, so issue #274 and PR #275 carry coordination.

## Inherited review triage

This bounded pass does not claim the entire stack is free of review findings. Open older discussions need separate shared review:

- #261's missing Event commands and last-province continuation are already fixed by dependency #270; this pass preserves their regressions.
- #248's Suspicions timing and already-face-up Event handling remain separate engine findings. Suspicions' historical decision explicitly describes every player's Events Phase while its stored text says “your” Action Phase; obtain the rules lead's resolution rather than silently revising that recorded interpretation here.
- #256's Earth's Hunger bow-cost validation and ability-ID source labels remain outside this pass's six reviewed card issues.
- #261's Times of Strife/Reserve trigger discussion remains separate rules work, as recorded in the Event handoff.
- Duel findings in #220/#268 remain with the separate duel integration.

Shared review from mdornbrook, Alfredo's native acceptance, native Windows gameplay and a full human match are distinct from automated checks. Regions/Celestials table presentation follows this task.
