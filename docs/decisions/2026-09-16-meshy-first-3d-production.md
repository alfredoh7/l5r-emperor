# Meshy during initial 3D asset production

Accepted by Alfredo on 2026-09-16 in #245. UI/art lane; applies to new 3D production across future tasks.

Every new 3D object, including ornamental UI frames, must use Meshy in initial production. An approved image or measured Blender blockout may establish shape and camera fit first. Then produce the geometry/materials with Meshy, clean up and optimize in Blender, integrate with Godot, and compare native output to the approved source. Do not ship a hand-built approximation with Meshy deferred until after user feedback.

Existing accepted assets need not be regenerated. Flat original card scans, UI text and 2D bitmap art retain their existing workflows. A 3D asset may be rendered to a transparent runtime image where the current UI benefits from that; keep the authoring model and record that distinction. Keys stay in macOS Keychain, raw signed URLs stay out of Git, and large source files belong in private Releases.

First application: Meshy-built ivory F Noble Crest for Home/Favor. The user also reiterated that test/final previews should retain the approved Caudex/WeatheredQuill and worn black lacquer menu system. Related PR #246; production instructions: docs/ART_IMPLEMENTATION.md and docs/MESHY_WORKFLOW.md.
