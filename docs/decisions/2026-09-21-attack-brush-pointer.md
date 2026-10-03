# Attack pointer: straight red brush trail

Status: **pointer direction accepted by Alfredo; attack-type concepts and full #8 acceptance pending.** UI lane, issue #355 / draft PR #410.

Alfredo requested that the thin gold Attack arrow become a beeline of red brush strokes matching the duel Strike motif. After the displayed broken-brush image, he said “that looks good. let's move forward with that.” The exact approved image is retained at `assets/ui/attack-targets/approved-red-brush-trail.png`.

Use actual imagegen-derived raster paint on a straight path, a proportionally scaled arrowhead, and no required animation. Apply it only to active Attack intentions; No target, cancellation and stale/seat cleanup remove it. Ordinary action/equipment and movement arrows keep their existing meanings. No legality, payment or attack outcome changes are authorized by this presentation decision.

Alfredo also requested bow/katana ranged-versus-melee concepts. His latest direction removes the plaque beneath the court and asks for integration into the existing UI, with other ideas welcome. The concepts remain proposals; do not infer attack type from artwork, weapon attachments or label text. Any later implementation must use authoritative engine information and receive the relevant shared review.

Main merge is not authorized. Previews 1–4 have separate conditional acceptance; previews 5–8 and the new attack-type concepts retain their own review gates.
