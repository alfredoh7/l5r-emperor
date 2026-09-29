# Two Dynasty cards in one province — approved presentation

Alfredo approved A’s shared horizontal province layout with C’s temporary ordering cues on 29 September 2026. Tracks the province portion of #499. The Region is distinct from two equal Dynasty slots; all sit within one province border and share one province number. The first/second choice appears only while the engine requests it.

The concept approval covers appearance and interaction. It does not approve a merge or certify Plains of Otosan Uchi’s complete rules implementation. Mendon’s #499 request leaves the card’s reveal/refill/purchase/destruction rules wiring to the engine lane after the view contract is agreed. This change supplies the view, native client and an explicitly labelled presentation review fixture; it must not silently make the unsupported card playable.

Full card scans, current province framing, sparse table labels, engine-supplied commands and private face-down information are preserved. The generated card names, text and stats are illustrative; actual scans are used by the native client.

## Contract and verification

Proposed contract in PR #544, for Mendon's review:

- `ProvinceView.DynastySlots == null` means legacy `Card` is slot 0. A supplied array replaces that legacy field, including a null card in an empty slot. Stable slot indices 0/1 identify A/B through refills; the Region remains a separate public card. A province that holds no Dynasty cards gets no inferred slot.
- `MatchView.PendingProvinceOrder` identifies a particular decision, its deciding player, owner, province and public-safe prompt. Only the deciding seat receives `Options`; the other seat receives an empty array and waiting UI. A two-slot permutation is identified by option ID, ordered slot indices, a label, enabled/reason, and the exact engine command. The client does not invent a command kind or execute reveal/refill logic.
- A fresh decision/version clears the draft selection. Confirm checks decision ID, current version, deciding seat, option availability and command seat/version. It forwards the offered command unchanged. UI confirmation is not authority: Core/server must revalidate it.
- Both slots and the Region are included in lookup, highlighting, transfer presentation and public battle receipts. One province frame/number is retained. Wide frames stretch plain rails while keeping ornamental pieces proportional.

Producer work remains explicitly outstanding: multi-slot authoritative state, save/replay compatibility, reveal/resolve/refill ordering, purchases, destruction and Honor effects, slot-aware private sights, action IDs, AI and reconnect. All hidden card fields must be redacted before producing a view. Use distinct opaque public slot IDs for face-down cards, not private instance/definition IDs. Order labels/reasons/commands must not disclose identities. Test this through both seats and the real server before enabling the effect. This PR's fixture/serialization checks do not establish production secrecy for that future producer.

`--province-review` (or interactive `--capture-mode=province-review`) opens explicitly labelled fixtures. They project actual scans into test views, intercept their order confirmations, and block game commands. No fixture registers Plains as playable. Returning to the menu removes the fixture and its controls.

The province-less battlefield and other pending choices in #499 remain separate scope. Exact test outcomes and native evidence are recorded in the task handoff and review document.
