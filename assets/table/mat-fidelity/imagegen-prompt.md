# Fabric extraction / reconstruction experiment

Built-in imagegen edit, 2026-09-14. Reference: `assets/table/phase-8/navy-mat-v4.png`.
Requested 3840×2160; returned **1672×941**. This is NOT a 4K master or an approved replacement. Ornament placement, brightness and corner details drifted. Retained for comparison only. The production asset review reuses the original pixels instead.

```text
Use case: precise-object-edit.
Asset type: production base-color fabric texture for the existing L5R Emperor playable mat, landscape 16:9, requested 3840x2160 output if supported.
Input image 1 is the EDIT TARGET, the approved current 1672x941 mat artwork. Preserve this exact design, not a new concept.
Primary request: faithfully reconstruct its existing navy fabric and gold botanical ornament with crisp, restrained close-up detail at a higher resolution. Separate the fabric from the wooden frame by removing ONLY the very thin outermost raised wooden/lacquer rim; extend the adjacent cloth seamlessly to all four image edges. Keep the thin gold printed perimeter and the existing gold Japanese botanical edge ornament at the same proportional positions.
Invariants: same deep midnight navy tone, same extremely subtle woven / interlaced fabric center, identical quiet uncluttered central field, same fine antique-gold foliage massing down left and right, same sparse top and bottom gold lines. Do not brighten or redesign. Preserve the sparse tiny corner flourishes, their proportions and spacing. Center fabric must not become coarse, busy, strongly embossed, shiny, noisy or patchy.
Rendering: exact flat orthographic top-down UV texture, even neutral illumination suitable for realtime material; no perspective, no shadows cast by objects, no dimensional border/frame. Refine the fine textile fibers and gold line edges without oversharpening or adding new motifs. The whole image is fabric and the printed gold ornament.
No table, wood, lamps, cards, UI, text, logos, watermarks, circles, boards, additional objects, or environment. This is a single texture map, not a presentation sheet.
```
