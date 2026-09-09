# Approved token rail and short unit ribbon

Issue #81; implementation PR #92. The user's latest choice is concept **C**, superseding the previous concept-only recommendation against that placement. The later clarification specifically selects the short upper-right tabs in C's top-left and top-right examples, not a slimmer version of the old hanging flag.

## Source artwork

- `approved-C.png`: exact approved concept. Runtime `unit-ribbon-reference.png` is the same image. The short ribbon silhouette samples x447–488, y119–264, including the navy cloth, curved divider and gold outline. Live UnitForce replaces the printed 3; polished spear pixels replace the pale concept spears.
- `approved-token-artwork.png`: exact approved token library. Runtime `card-token-source.png` is the same image. The 4×4 atlas uses centers (208 + 280×column, 157 + 221×row), radius 94. Last cell is the generic modifier; its baked value is masked before drawing the actual modifier.
- Runtime `card-token-hardware.png`: imagegen-derived empty lacquer rail and polished spears; prompt in `production-prompts.json`. Its transparency preview was baked into the source, so only opaque rail regions and spear silhouettes are sampled. No checkerboard is rendered.

The rail samples x147–358, y60–1474 of the production sheet. Its lacquer surface is fitted to the card's edge. The ribbon keeps the source's short silhouette; it is not the old brass bracket. Original published card scans remain untouched. Source comparison intentionally retains actual printing differences rather than replacing the card with the generated illustration.

## Display behavior

Tokens are public `CardView.Tokens`, grouped by type, affected statistic and amount. Sources remain in the hover details. Counts are quantities, not current Force/Chi; Blood tokens with different effects are not combined. One through four distinct groups retain default sizing; five and six compact. Twelve identical Fire tokens occupy one ×12 slot.

Only face-up, identified cards in play show rails. Personalities, Holdings, Strongholds and attachments share the renderer. Bowed cards use a horizontal near-edge rail. Separately bowed attachments in a folded stack use distinct bands below that stack; upright attachments have separate left bands. Hidden portions of attachments stay normally stacked. Hover and click identify the owning card; expanded inspection retains separate cards. The inspector includes textual token details.

The short right ribbon reads the engine's UnitForce, including zero. This change adds no rules, makes no token effects authoritative in the client and does not make catalogue cards playable.

Run `bash tools/check-token-rails.sh` on a native graphical Mac. Evidence and limits are in `docs/screenshots/token-rails/README.md`.

2026-09-09 material refinement: the short ribbon keeps the C outline, gold curved divider and polished spear hardware, but samples a full 2D woven-cloth swatch from the earlier `unit-standard-mounted.png` asset in deeper navy. Mipmaps prevent the weave from aliasing at small table scales; no new generated bitmap or gameplay value changes.
