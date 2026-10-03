# Top Cards and the remaining review approvals

Status: design selected by Alfredo on 22 September 2026; new Top Cards native result awaits his hands-on review. Lane: UI presentation of the existing shared contract. Issue #323 / PR #434. No main merge authorized.

## Accepted direction

Alfredo: “I recommend A’s layout with B’s clear deck-order labels.” Implement one screen with large destination trays, full original card faces, a physical remaining-deck anchor, and one final confirmation. Dragging primarily moves cards between engine-offered destinations and reorders each return group. The empty landing slot and neighboring cards preview the result before release. Undo, Escape and click/keyboard placement provide reversible alternatives.

Labels stay with positions, not card identities. Top order starts with “1 · Drawn first”; bottom order starts with “1 · Immediately below the deck” and ends with “Very bottom.” When only one card goes below the deck it has both descriptions. Show-and-take and discard destinations disclose the public outcome. Opponent-deck ownership stays explicit.

The existing engine contract, legality, counts, per-card eligibility, fixed-order behavior and hidden information remain authoritative. Moving cards changes only a private client draft. Confirm sends one arrangement, still validated by the engine. A full destination rejects another card with a reason; it never silently replaces the first card. Seat, connection/busy and view changes remove private drag previews and reset the draft.

Source files and provenance: [approved concepts](../designs/2026-09-22-top-cards-drag/README.md). The game reuses its existing lacquer/brass art, heading font and original scans. Approval covers the layout and deck-order communication, not generated card text or a claim of pixel-identical decoration.

## Other approvals

Alfredo explicitly says Multiple Targets (#345) and Battle Progress (#311) are good to go. This covers the revised demos in PR #434, including removal of target chevrons and the phase bar aligned with the narrow icon strip. Mendon was notified and asked to leave UI/QoL or rules changes for Alfredo:

- [Combined review PR](https://github.com/alfredoh7/l5r-emperor/pull/434#issuecomment-5780063819)
- [Multiple Targets](https://github.com/alfredoh7/l5r-emperor/pull/429#issuecomment-5780064192)
- [Battle Progress](https://github.com/alfredoh7/l5r-emperor/pull/430#issuecomment-5780064575)

These are Alfredo’s approvals, not Mendon’s acceptance or authorization to merge main. Previous Opponent Payment acceptance and the selected ghost-copy design remain recorded in their existing decisions/handoffs.

## 23 September correction

Alfredo rejected the earlier native appearance as not matching the selected concept. The earlier reuse-only interpretation was insufficient: restore A’s brush title, larger full faces and physical deck anchor, a visible Hand slot, and the landing highlight treatment. Keep B’s explicit positional labels. His screenshot displays B; an optional layout clarification was requested, with the earlier explicit A+B selection retained in the absence of a changed direction. This is not renewed approval of the native implementation. The correction uses a derived raster atlas with provenance in `assets/ui/top-cards/provenance.json`. The Windows package pinned at 9f8ef9f5 does not contain it.
