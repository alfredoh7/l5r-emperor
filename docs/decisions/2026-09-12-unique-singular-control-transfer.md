# Unique/Singular's control-take restriction depends on a control-transfer primitive that doesn't exist yet

**Decision:** Recorded as a durable rule, not yet implemented. When a control-transfer mechanism
(reassigning `CardInstance.OwnerId`, or an equivalent "controls this card" concept distinct from
`OwnerId`) is eventually built, it must include this check from day one:

- A player may not take control of a second copy of a card that has the Unique or Singular
  keyword (matched by `Definition(card).Name`, the same identity Unique/Singular's in-play check
  already uses — see `ControlsCopyOf` in `CardAbilities.cs`).
- If a player temporarily loses control of a Unique/Singular card, and during that time a second
  copy enters play under their control, the temporary effect returning the original card to them
  must be blocked/delayed until that new copy leaves play or their control.
- When a player takes control of an opponent's Personality, every card in that unit (attachments —
  Follower/Item/Spell — not just the Personality itself) must also be checked, since the capturing
  player may already control a duplicate Unique/Singular attachment.

**Reason and evidence:** User spec (verbatim, 2026-09-12): "Unique and Singular have an additional
restriction that has not been stated for Unique yet (so update it). A player may not take control
of a second copy of a card that has the Singular or Unique keyword. So, if a player temporarily
takes loses control of a card with Singular and, in the intervening time, brings into play another
copy of that card, when the temporary effect would return the singular card to them, it cannot be
returned until the new copy leaves play or their control. In addition, it is now important to check
every card in a unit for uniqueness when a player takes control of personality from an opponent
because they may have duplicate copies of a unique spell, item, or follower attached to them."

No control-transfer mechanism exists anywhere in `L5R.Core` today — `CardInstance.OwnerId` is set
once at creation (`Add`/`Purchase`/`Equip`) and never reassigned by any code path. This has been an
explicit, repeated deferral since Loyal (#142: "negating a control-change effect... nothing yet
changes a card's controller/owner after recruitment") and named again across the Focus-1 (#192) and
Lion Clan (#198) audits as "control-transfer mechanics." Building a real control-transfer primitive
with no concrete card driving it would be speculative scope creep (this session's own established
discipline — see e.g. the Reconnaissance/Recon/Kiho classification-only precedents, each documented
rather than half-built ahead of a real need). This decision exists so the rule is preserved exactly
as specified, rather than re-derived (and possibly re-specified differently) whenever a card
actually forces control-transfer to be built.

**Lane:** Rules (mdornbrook).

**Affected files (when control-transfer is eventually built):** wherever `OwnerId` reassignment is
added (likely `MatchEngine.cs`), plus the unit-wide capture check (iterating a captured
Personality's own `Attachments(...)`).

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/202

**Status:** Accepted (the rule itself; not yet implemented — no control-transfer primitive to attach
it to). Superseded if/when control-transfer is built and this check is wired in for real — that PR
should link back here and then this file should be marked superseded.
