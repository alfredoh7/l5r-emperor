# Emperor table design

## Status

The user approved a stylized 3D war table, readable native controls, and a suggest-then-confirm economy flow. Three Imagegen concepts are saved in assets/concepts. A (lacquer and green silk) is the provisional prototype skin; B (paper and stone) and C (indigo and brass) remain switchable. Final art-direction selection is still open. This is the early visual prototype in the approved plan, not final presentation acceptance.

## Physical scene

Two friends sit at their desktop computers for a deliberate evening card match, reading small pieces of information frequently; a quiet green playing surface, ivory card faces, and a darker wood perimeter keep the cards readable while evoking a physical campaign table.

## Composition

A nearly overhead 3D table occupies the viewport. Opponent provinces and home sit across the far edge; our provinces, producers, and hand are near. The active phase is above the board. A parchment inspector on the right explains the selected card and offers its legal actions. The action log is a toggleable sheet at left. Strategic choices appear in the inspector, including payment alternatives. Transitions do not delay authoritative updates.

## Color roles

Restrained palette. Ink oklch(22% 0.02 150), muted jade oklch(35% 0.04 150), aged paper oklch(90% 0.03 85), antique gold oklch(72% 0.09 80), Crane blue oklch(72% 0.06 230), Lion ochre oklch(72% 0.11 80). Exact renderer sRGB values are centralized in Palette.cs. Highlight shapes and text supplement all colors.

## Typography and components

Readable sans for all rules, buttons, and numbers. A restrained serif appears only in the game title and card names. Minimum primary button height 40 logical pixels. Four-pixel spacing unit; meaningful groups use 12/24/32px gaps. Hairline borders, slight paper corners, subtle elevation. No opaque dashboard card grid, invented resource pool, or rasterized HUD text.

## Mock fidelity inventory

- Dark beveled wood table: Blender geometry and real-time material.
- Quiet fabric/washi playing field: engine material, switchable A/B/C palette.
- Small outer-edge candle and brass props: Blender meshes and engine lights.
- Clear cards: native controls and exact engine-projected data; generated original illustrations only on explicitly synthetic training cards.
- Phase timeline: semantic labels/buttons tied to actual phase and priority.
- Gold/Honor controls: preview of selected payment and authoritative Honor, never editable HUD-owned totals.
- Decorative card text and symbol inconsistencies in concepts: replaced with accurate live rules text and consistent controls.

## Acceptance

Native screenshots and interaction checks at 1280x720, 1440x900, 1920x1080 and ultrawide. All cards inspectable by click/keyboard, source selection visible, no hidden-card data, no clipped primary actions, reduced motion supported. Full final-art quality and hardware performance require later acceptance testing.

## Gameplay clarity pass · September 2026

The user's approved priority is card orientation, unit grouping, battlefield placement, then direct interaction. Bowed cards rotate 90 degrees, with short transitions and a reduced-motion setting. Holdings stay together in each player's home row. Personalities own independently selectable attachment stacks; the stack's numeric control spreads or folds the cards. Unit Force remains upright outside the card faces.

Four numbered battlefield lanes align with the provinces, separating both armies and labeling the current battle, unit counts and live Force. Home cards are never displayed as battlefield cards. Assignment arrows are dashed blue and remain local drafts until confirmation; ability-target arrows are gold. Clicking a legal card or province selects it, with the existing inspector controls retained as alternatives. Game state always comes from the engine snapshot.

This pass improves playability using the existing table and card artwork. Further human gameplay feedback and final art approval remain separate from automated/native smoke checks.

## Playable lacquer polish (0.3.0)

Published cards display complete original scans at their native aspect ratio. Game state lives in surrounding outlines, orientation and unit footers. Built-in Imagegen supplied a revised playable concept and silk/walnut material textures; Blender MCP rebuilt the textured table, exported to Godot. Native controls use a matching lacquer/brass treatment and a persistent next-move guide. The card hover popup is bounded independently of wrapped text, preserves pending decisions, and reads only the projected CardView. `RulesHelp` contains reusable topic keys for a future scripted tutorial. See [UI_POLISH.md](docs/UI_POLISH.md) for the target, iterations, deliberate runtime differences and help behavior.
