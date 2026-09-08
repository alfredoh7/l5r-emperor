# Unit Standard and battle-progress presentation

UI direction accepted by Alfredo on 2026-09-08: approved concept C uses a navy cloth standard with three gold spears beside the whole unit, separate from the orange Personality Force and gray Chi discs. Use the existing Musashi header font and light-gray numerals. Preserve actual card faces, bow states, attachment inspection, and sparse board information.

Implementation in PR #36, stacked on #35. Related issues: #18 (stats), #20 (battle sequence), #15 (Engage). UI lead: alfredoh7. Shared projection/phase interpretation awaits mdornbrook review.

`CardView.UnitForce` is an optional, server-produced effective contribution from the existing `BattleForce` calculation. It includes the Personality and qualifying Followers and already accounts for bow/Elite. Item Force is already included in the Personality value. It is not an army sum or a client-computed total. Hidden, non-Personality and out-of-play cards have null. Older server payloads omit the standard rather than showing a fabricated zero. Existing arithmetic and battle resolution are unchanged.

The textured tracker maps current public phases to Assignment and Battle actions. A newly received `battleResolved` event lights Resolution for 4.5 seconds without pausing play; hover reveals the engine event. New battle/assignment, match reset, or expiry clears it. It does not add unimplemented maneuver/Engage windows. The current priority player controls the clan color and response text; Resolution is labeled Resolved, never as a player wait. Reduced motion stops the halo pulse.

The full tracker is shown when no card/choice occupies the inspector. Inspecting/choosing uses a compact row of the same medallions. Future sequencing work should replace the phase mapping with explicit authoritative segments when available, with rules-lead review.

Validation and captures: `tools/check-unit-standard.sh`, `docs/screenshots/unit-standard/`, task handoff. Native Mac checks are not Windows graphical testing or expert rules acceptance.
