# Created-copy ghost marker

Status: accepted visual direction by Alfredo on 22 September 2026; native implementation available for hands-on review. No main merge authorized.

## Decision

Use **B’s overlapping ghost outlines with A’s shorter, quieter wisps** from the ImageGen studies in `docs/designs/2026-09-22-copy-marker/`. Keep the front outline crisp at table scale, offset the rear outline, and keep smoke close to the symbol. Replace the solid round seal while preserving its lower-left placement and click-to-inspect behavior. The card face stays fully opaque and unobscured. Wisps are static in this implementation.

## Authority and scope

Alfredo chose this combination explicitly in the UI review. The marker still depends only on public, face-up `CreatedCopy` state. It does not infer a copy source, mark ordinary cards with printed Created text, or change removal rules. Mendon’s rules review remains separate from visual acceptance.

## Implementation and evidence

- Issue: https://github.com/alfredoh7/l5r-emperor/issues/400
- Review: https://github.com/alfredoh7/l5r-emperor/pull/434
- UI lead: alfredoh7; rules lead: mdornbrook.
- `src/L5R.Client/CreatedCopyStatus.cs` renders the faithful generated sprite from `Art/CreatedCopies/ghost-copy-marker.png`; the original PNG, exact prompt and provenance are retained. AtlasTexture removes only transparent margin at render time.
- Native checks passed at 1920×1080 normal and 1280×800 reduced motion. Evidence is in `docs/screenshots/ghost-copy-marker/`; the handoff records behavior and review limits.

This supersedes the round twin-card seal’s visual treatment in this review branch. It does not supersede prior CreatedCopy semantics or human merge gates.
