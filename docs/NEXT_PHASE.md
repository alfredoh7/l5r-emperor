# Next development phase: dependable Lion–Crane matches

**Sequence update (2026-09-09):** [NEXT_GAME_PLAN.md](NEXT_GAME_PLAN.md) is the current delivery order: catalogue → deck builder/storage → custom Practice → dueling → Spells → card waves. The acceptance procedures below remain applicable; the older tutorial-first ordering is superseded.

The user is ready to pause broad visual redesign after the bitmap phase-bar pass. Keep the current table as the presentation baseline; further changes should address problems found during play.

## 1. Complete-match acceptance and rules corrections

The current two-deck profile has implementations for all 40 distinct definitions, automated bookkeeping, a legal-action AI and deterministic replay. These are implementation and automated-test results, not completed human acceptance.

Play complete games as both Lion and Crane, including a human-versus-human match. Record any point where a player cannot understand or complete a legal action, where automatic payments/timing disagree with the pinned rules profile, or where a crowded battlefield obscures necessary interactions. Capture the command journal and turn/card context for each defect; reproduce rules defects as engine scenarios and interaction defects through native controls. Review disputed historical wording against the selected Emperor printings, separately from deck balance.

Completion means both players can finish games without manual bookkeeping workarounds; the exercised reactions, purchases, attachments, battle decisions and victory resolution agree with the pinned profile; recorded matches replay to the same final state; and blocking defects are resolved. UI redesign beyond required playability corrections stays deferred.

## 2. Private multiplayer and platform acceptance

The existing server has locally tested authority, private seat views, persistent journals, duplicate/stale-command handling and reconnection. The open work is a real external private game and native Windows acceptance.

Validate the distributed clients on a Mac and a Windows PC, then play across separate machines through a private HTTPS/WSS server. Exercise loss of connection during a choice/payment, reconnect, resumed priority, opponent departure, and server restart. Verify hidden information stays private and both clients converge to the same authoritative match. Record actual hardware, OS and renderer behavior. A packaged Windows build is not evidence that these checks passed.

Select the hosting arrangement with the user when this phase begins; no public service or paid hosting is created by this roadmap.

## 3. Scripted beginner tutorial

After the two-deck game is reliable, implement short guided scenarios using the same rules engine and existing contextual help. Teach the board and bowing, Gold production/payment, buying a Personality, attachments, action priority, attack/defense, and victory. Give each lesson a clear objective, contextual explanation, a recoverable mistake path, and a restart/skip option. Finish with a guided match that gradually removes prompts.

Tutorial scripts should observe real match events and legal actions, rather than implement a parallel rules system. They explain rules and controls; they do not expose private information or prescribe optimal competitive decisions.

## 4. Additional decks, then broader Emperor coverage

Expand by a small verified deck/card group at a time. For each group, pin printing/text/art provenance, implement newly required mechanics, add interaction cases, and complete human matches before marking it playable. Duels, Spells, Events, Cavalry and Rings are not inferred to work merely because their cards exist in the catalogue. Build the deck builder around verified playable content, then extend it as coverage grows.

The immediate recommendation is steps 1–2. The tutorial is the next substantial feature; broader content follows. This document records the proposed sequence, not approval to start deploying or expanding the game in the current art task.
