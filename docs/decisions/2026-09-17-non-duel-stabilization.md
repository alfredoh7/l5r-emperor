# Non-duel continuations and card corrections

Issue [#274](https://github.com/alfredoh7/l5r-emperor/issues/274), PR [#275](https://github.com/alfredoh7/l5r-emperor/pull/275). Alfredo authorized the stabilization pass; shared rules review by mdornbrook is requested before merge. These are implemented corrections awaiting that review, not a claim of human rules acceptance.

- Brash retains explicit Draw/Decline choices. Bow and before-action reactions enumerate through the same validator that executes them, including exact watcher, ownership, usage limit and targets. Passing remains an explicit engine command; Practice consumes the same offered actions.
- `ActionOption.GoldCost` is an optional additive public quote. Ordinary Equip groups only targets sharing the same Core-computed price, using the same `EquipCost` as execution. This handles Satoshi's Lion discount and Katagi's target-specific discount without moving rules into the UI. Older action payloads deserialize with a null quote.
- Holding wave 1 retains its selected printings and artwork. Archery Range, Slanderer, Temple of Hotei and Tranquil Garden produce the 2/5/2/2 Gold stated in their committed card text. The packaging script now preserves these fields on regeneration.
- The Weakness of Man compares the target's current Force to that target's printed Force. This explicitly corrects the performer-base comparison recorded in `2026-09-17-battle-open-presence-primitive.md`; the Battle/Open presence rules are unchanged. The old presence tests now use a genuinely modified target, and new positive/negative cases isolate the comparison.
- Shadowlands is tested through the keyword mechanism for Dispensing Justice, Bow Before My Will and Winds of Dismissal. No clan alignments, card text or selected printings are changed.
- Overgrown Grove and Improved Defenses require their full Fate-card discard cost before an action is offered or accepted. Rejected underpayments are atomic.
- The Earth Flows' winning-army bonus requires actual enemy-army destruction and the existing ordinary Honor-gain path; winning an empty battlefield does not award it.

The approved lacquer/WeatheredQuill/Caudex controls are reused. Regions/Celestials table presentation and separate duel integration remain outside this task.
