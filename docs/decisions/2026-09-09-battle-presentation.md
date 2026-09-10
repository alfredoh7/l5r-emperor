# Battle courts, assignment intentions and province falls

Accepted visual direction from Alfredo: Pass 1 B (shared battle court), Pass 2 B (ink stepping stones), Pass 3 A+B (fractured and extinguished threshold). Implementation is shared UI/rules presentation and requires Mendon's review through PR #76 / issue #75.

- Each threatened province owns a separate, connected court. Defenders are nearest their province from either seat; attackers are opposite. Holdings stay on the left. Home units remain outside courts on the table-facing side of the province rows. The user explicitly rejected the first implementation that put Home behind the province rows.
- Drafts display translucent permitted card faces and ink paths. Only an engine-accepted assignment moves real cards. Attachments keep their root membership.
- Courts can grow/contract around their contents. Only the current battle reserves occupied lane/battle space so resolution does not shift remaining province positions. Normal Action/Dynasty play returns to the original compact 218-unit province pitch and removes the empty battle band. Returning bowed units have temporary Home space reserved on both sides.
- A new active battle is gently framed with its province; wheel, drag, arrow keys and Fit table override the camera. Reduced motion uses immediate transitions.
- Resolution uses actual public events and views: short outcome beat, real transfers to their reported Home/discard/dead archive destinations, then a dim fractured threshold. There is no locally predicted casualty or authoritative Force calculation.
- Fallen provinces keep a low numbered threshold and a hover explanation, with no card-sized bay and no assignment control. History groups each battle result into expandable actual event details.

The per-Personality inlaid Force/Chi values and engine-supplied unit-force standard remain unchanged. A bowed Personality's unit contribution is still supplied by the engine, never recalculated by this presentation.

See `docs/ART_IMPLEMENTATION.md` for the standing user-approved source-to-game visual workflow.

Future cinematic work can add named character announcements and sound. It must preserve the distinction between destruction, bowing and returning home, reduced-motion support, and player control of the camera.
