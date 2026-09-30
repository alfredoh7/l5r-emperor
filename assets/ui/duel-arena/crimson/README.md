# Crimson Standoff production assets

Alfredo approved concept iteration 5 and requested native implementation with particular attention to grass/cloud motion. The source is `approved-crimson-standoff.png`; it remains unchanged. The three dedicated assets were derived with built-in imagegen (exact prompts and original output provenance retained). No Meshy generation was required.

- `campaign-mat-albedo.png`: orthographic cloth artwork with the approved heraldry, blood patina and border, with generated cards and buttons removed.
- `landscape-clean-plate.png`: the detailed environment without a playing surface or UI.
- `golden-grass-tuft.png`: transparent foliage detail, used on segmented geometry among the Blender ribbon/seed-head exemplars.

The default native review is `CrimsonDuelReview.tscn`, launched by `Review Duel Arena.command`. The original garden and its sources are retained as historical work; the default scene loads none of its large models.

The mat is real shallow cloth geometry, and the foreground foliage uses three Blender mesh variants plus segmented textured cards in spatial cells. Main wind travels across world space; vertex height anchors roots and varies bending stiffness. Fine secondary motion is distinct from the broad gust. The landscape uses a masked animated matte: slow local cloud displacement and restrained valley haze, with the sun and mountain silhouette protected. This is not a full volumetric cloud simulation. It suits the approved fixed camera; a freely orbiting camera would require additional environment geometry and sky work.

Godot's [MultiMesh documentation](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html) describes GPU instancing and the need to consider culling by groups. The broad/detail wind separation and constrained deformation follow the approach discussed in Crytek's [vegetation animation chapter](https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-16-vegetation-procedural-animation-and-shading-crysis), adapted here for this presentation and renderer.

Original card scans are rendered through the existing native controls and shared projected surface. Its optional camera parameters default to the original table values, leaving the main board's projection unchanged. The reference's generated card microtext is never treated as a production scan.

This is a public composition fixture. Disabled action controls do not issue commands, and the both-sides face-down arrangement matches the art study, not a claim about a viewer's live focused-card visibility. Live integration must use the engine-provided card views, choices, focus/reveal order and outcomes. Performance measurements, motion captures and human AAA-quality acceptance are separate evidence.
