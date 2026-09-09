# Accepted: A artwork with C contextual help

Alfredo approved the Continuous Brass Rail (A) with Compact Rail + Context (C) help behavior for issues #10 and #65. Display Straighten, Events, Action, Attack, Dynasty and End in that order. Reuse the approved aged-lacquer/brass bitmap, with live Musashi Brush labels, soft active glow and accessible hover/focus help. Keep all six phases visible in smaller windows.

The phase bar displays authoritative state; it must not advance phases or insert delays into automatic Straighten/Events transitions. Attack comprises existing attack/battle sub-states. Preserve the current separate priority indicator and Pass action. Help text must describe the actual phase rather than the old ended-match fallback for Straighten/Events.

Sources: assets/ui/six-phase-track/approved-A.png and approved-C.png. Implemented in PR99; see the task handoff and native screenshots for validation. Shared semantic help will request mdornbrook review. No engine timing or rules changes are planned.
