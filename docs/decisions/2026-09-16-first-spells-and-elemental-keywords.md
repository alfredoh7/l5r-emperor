# The first three Spells: a genuine PersonalityForce gap, a new cost-choice Performer shape, and Elemental Keywords as a not-yet-needed grouping

**Decision:** Suitengu's Path (Seeds of Decay, `oracle-l5r-10133`, Water), Scouring Flood (Emperor
Edition, `oracle-l5r-6502`, Water), and The Earth's Hunger (The Shadow's Embrace, `oracle-l5r-9889`,
Earth) are the first three real `CardType.Spell` cards wired into the engine — every prior Spell in
the catalog was a synthetic `training-*` fixture. Added via the standard content-wave pipeline
(`tools/content/package_spell_wave_1.py`) — catalog-only, not part of the frozen Lion/Crane decks.

**Suitengu's Path** ("This Shugenja has Naval.") needed no new engine code at all — it is a pure
`AttachGrantsKeyword` carrier, the exact mechanism `CardDefinition.AttachGrantsKeyword` already
provides (Scourge of the Sea's own shape, issue #179), just on a Spell for the first time rather than
a Follower/Item. Verified end-to-end through the real Equip (Battle) path (the only way a Spell ever
enters play), not just a direct-attach fixture.

**Scouring Flood** ("This Personality has +2F.") exposed a real, previously-invisible gap:
`PersonalityForce` summed an attached Item's own printed `Force` field into its wielder's total
(`Attachments(card.Id).Where(c=>Definition(c).Type==CardType.Item).Sum(...)`), but never a Spell's.
Nothing published needed a Spell's own `Force` field before now, so the gap was silent. Investigating
the card's own scan, though, showed the "+2F" is **not** a printed Force stat box at all (unlike an
Item, a Spell never prints one) — it is purely a text effect, and the local catalogue record's own
`force` field for this card is empty. So the fix is not "widen the Item filter to include Spell" (that
would just always add 0, since no Spell's `Force` field is ever populated by the content pipeline) —
it is a new, Effect-keyed term exactly mirroring `ItemForceToWielder`'s own shape, but for an attached
Spell's `Effect` string (`Effect=="scouring-flood-force" ? 2 : 0`), applying unconditionally (no
published Spell needs a conditional bonus yet, matching the user's own "whether or not the card is
bowed... every phase" description). Every other reader of a wielder's Force (battle resolution, the
client view, `BattleForce`) already goes through `PersonalityForce`, so no other call site needed
touching.

Scouring Flood's own printed Battle ability ("Bow this card and target a defending Personality: Move
him home") reuses two entirely existing primitives with zero new code: the `"defending"` Target
category (already used by Incapacitated/Favor Battle) and `MoveHome`. Attaching it via Equip (Battle)
automatically qualifies it for that action's own existing "bonus Additional Action for the newly-
attached Spell's own Battle ability" grant (`CardAbilities.cs`, `equip-battle`'s own execution case) —
confirmed unchanged, no edit needed there.

**The Earth's Hunger** ("Bow this Shugenja, or bow this card if he is Earth... Destroy it.") needed a
genuinely new Performer category, `own-wielder-or-source-with-wielder-keyword` — a cost choice in the
same family as Xijkt's/Tamori Shosei's own `own-source-or-unbowed-holding-with-keyword`
(issue #207 follow-up), but shaped the other way round: Xijkt's own condition is checked against the
ALTERNATE candidate itself (an unbowed Holding's own keyword); The Earth's Hunger's condition is
checked against the WIELDER (the card the Spell is attached to) even though the alternate candidate
being offered is the Spell itself, which carries no keyword condition of its own. The wielder is
always an eligible cost-payer; the Spell becomes a second eligible choice only when the wielder
carries `RequiredKeywords`. No "must be unbowed" check on either candidate in the Performer switch
itself, mirroring Xijkt's own precedent exactly (the shared bowed-source Require already rejects the
whole action if the ability's own source — the Spell — is already bowed; nothing stops the wielder
from being chosen again later the same turn if it becomes unbowed again by some other means, the same
latent shape Xijkt's own precedent already accepted).

Both Scouring Flood's and The Earth's Hunger's own Battle abilities needed an explicit presence
Require the shared Battle-Timing gate does not provide for free: the existing blanket check
(`ValidateAbility`, `ability.Timing == "Battle"` block) only requires the source card's own
`AtBattle()` status when `Definition(source).Type == CardType.Personality` — a Spell-type source
falls through that check entirely. Added `Require(AtBattle(source!), ...)` explicitly for both new
ability ids; `AtBattle()` already resolves an attached card's own parent correctly (the same helper
Khol Regulars' own Reaction already uses), so this is exactly "the Spell's own wielder is at the
current battlefield," no new helper needed.

**Elemental Keywords, recorded but not built:** the user clarified Water/Fire/Void/Earth/Air are
"Elemental Keywords" as a group (explicitly **not** Thunder), "sometimes counted as a group or
qualified as members of the group." No existing card or code anywhere checks "does this card carry
ANY ONE of a fixed keyword set" (the only existing primitive, `HasKeywords`, is a strict ALL-of check)
— confirmed nothing needs it yet: all three cards wired here just carry their own single specific
element directly in `Keywords` (`["Water"]`/`["Water"]`/`["Earth"]`), the same plain per-card
convention every other keyworded card already uses, with no "is this an Elemental Keyword" check
required by any of them. Recorded here, not built, the same discipline as prior "not yet needed"
design-boundary notes (e.g. the Unique/Singular control-transfer decision) — build the actual
any-of-five grouping primitive when a real card's printed text needs it, not speculatively ahead of
one.

Two data-quality notes, both following established precedent: The Earth's Hunger's local catalogue
record duplicated its own text (`source_record.text` + `printing.text` both present, one bolded, one
plain) — fixed with a `TEXT_OVERRIDE` in `package_spell_wave_1.py`, verified against the card's own
scan, the same mechanism Fallow Lands' own packaging script already needed. Scouring Flood and
Suitengu's Path both needed no such fix.

**Reason and evidence:** User spec (2026-09-16): "Elemental Keywords behave like other keywords but
are sometimes counted as a group or qualified as members of the group. Thunder is not an elemental
keyword. ... [Suitengu's Path] has a keyword (Water, Water, Fire, Void, Earth, Air are considered
Element Keyword or Elemental keywords) and a trait. The trait is 'This Shugenja has Naval.'... the
personality to which Suitengu's Path is attached gains the keyword: Naval and contributes to the
count of Naval cards in an army [no existing or requested counting effect reads this yet — see
below]. ... Scouring Flood... also has an elemental keyword and a trait (This personality has +2F).
As it is a trait, this +2F is applied whether or not the card is bowed or straightened and is
applicable during every phase. It also has a printed ability: Battle: bow this card and target a
defending personality: move him home... This battle ability could be take as an additional action if
Scouring Flood was attached using the Equip (Battle) action [confirmed already automatic]. ... The
Earth's Hunger... has a condition within the battle action. Bow this Shugenja, or bow this card if he
is Earth... The cost of the spell changes based on the keywords of the shugenja to which The Earth's
Hunger is attached. If the Shugenja has the Earth keyword..., then they may bow the spell instead of
the shugenja to perform the spell's action. Otherwise, they must bow the shugenja."

No "count of Naval cards in an army" bonus exists anywhere in the codebase (confirmed by search) —
the same "not yet needed" treatment as the Elemental Keyword grouping above; nothing wired here reads
that count, so nothing was built for it.

**Lane:** Rules (mdornbrook). No client/UI change in this PR.

**Affected files:** `src/L5R.Core/MatchEngine.cs` (`PersonalityForce`'s new Spell term),
`CardAbilities.cs` (`own-wielder-or-source-with-wielder-keyword` Performer category, the
`AtBattle(source!)` presence Require for both new ability ids, the two catalog entries/execution
cases), `PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (`SpellWave1`), new
`tools/content/package_spell_wave_1.py`.

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/254

**Status:** Accepted and implemented for all three cards. The Elemental Keyword grouping primitive
and the Naval-count effect are both accepted-as-future-scope but not implemented — no card exists yet
that needs either.
