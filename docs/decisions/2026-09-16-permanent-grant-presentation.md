# Permanent grants: quiet seal and icon-led detail panel

Status: accepted design direction from Alfredo, 2026-09-16. Issue #213 / PR #256. UI lead: alfredoh7. The read-only shared view addition requests mdornbrook review.

Use A's quiet gold on-card seal with B's icon-led black lacquer detail panel. Reuse approved ornament pixels, keep uppercase WeatheredQuill headings and Caudex elsewhere, and retain full printed faces and the established token rail. A single grant has no number; multiple permanent grants have a live aggregate count. The numeral is separate from token quantities.

Show grant name, actual duration, full ability text and public source on demand. Clicking selects the owning card; source inspection is read-only. Do not put permanent ability prose over the card face. Retain the existing near-edge rail for bowed cards and ownership-aware attachment clearance.

The concept's illustrative Spirit of the Berserker combination does not authorize adding it to the playable catalogue. Present only authoritative live metadata; explicitly staged review fixtures may demonstrate future effect text. Never infer permanent stat grants from a card's current statistics.

Affected areas: CardChanges, CardChangePanel, CardTokenRail, CardTile, Main's projected hover/inspector and the optional public CardView effect projection. See `docs/PERMANENT_GRANTS.md` and source/native evidence for the implementation boundaries.
