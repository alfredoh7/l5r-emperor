# Training opponent

`TrainingOpponent.Choose(MatchView, ActionOption[])` produces an `OpponentDecision` containing a command and a short explanation, or a null command when it must wait. The opponent only accepts the redacted view and legal actions for its seat. It never receives a `MatchEngine`, authoritative state, seed, replay, hidden hand, or deck order.

The opponent builds production early, buys Personalities, uses Proclaim, attaches support, uses its two supported training Battle actions, attacks when visible Force threatens a province, and defends visible threats. It uses deterministic tie breaks. This is a modest practice opponent, not a competitive L5R player or a search-based AI.

Always execute its returned command through the authoritative engine. The UI should make practice mode explicit and invoke it only for the configured AI seat. Apply one decision at a time after the previous result is displayed, respecting reaction priority and pending choices. Never make two AI decisions against the same state version.

The dependency-free tests run several seeded matches through completion, reject any illegal command, cap total decisions, replay completed results, and exercise assignment construction from the offered legal unit/province metadata.
