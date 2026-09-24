# Attack performer audit: every Melee/Ranged Attack against the Choose Performers rule (issue #472)

Requested by Mendon after the Yamadera ruling (#468): a printed action is performed by the card that prints it, unless it names a separate performer, or it is an Item's or Spell's (then the attached Personality). Strategy Ranged Attacks may have no performer.

All 78 Attack abilities in `AbilityCatalog` (Melee and Ranged) were checked against their printed text; `AttackPerformerAuditTests` now pins the result.

## Findings and fixes

1. **Followers were collapsed into their host.** `ActingPersonality` returned the host Personality for any attached card, so a Follower's printed Attack (15 Followers, plus Hiruma Sniper) and a named "performing Follower" (Tensin, Akio, Two Stings) recorded the host as performer. Core rules: an action on a Personality or Follower is performed by that card. Fixed: only an Item or Spell resolves to its wielder.
2. **Carpenter Castle's performer was the bowed Castle/Siege card**; that card is a cost target, not a named performer. The Stronghold that prints the action now performs it (the bowed card is still the cost).
3. **Hida Yamadera - exp** (#468): the dead Personality performs his own Melee 8 Attack.

## Reviewed, unchanged

- Explicit performers that print "performing" (Akio, Junpei, Metsubushi, Monastery of New Thought, Shiro Shiba, Tensin, Two Stings x2): correct.
- Tomiko and Yayu (x2): "Bow Tomiko / one of Yayu's Spells" -- the Performer field only picks the spell to bow; the acting Personality is still the source (a spell resolves to its wielder).
- Rings (False Ring of Water) perform their own action; Strategies with no Performer (Arrow's Eye, Precision, Sticks and Stones) are allowed to have none.

## For Mendon to decide

**Strength of the Divine** (a Strategy, Melee) has no named performer; the engine treats the targeted opposed Paragon as performer. If a Strategy's Melee Attack should have none either, that changes the Paragon's role. Left as is.

Two Attack abilities (`engulfing-flames-attack`, `spirit-scout-attack`) have no published card yet.

## Verification

Full core suite zero failures (two pinned tests updated: follower and Carpenter Castle performer), AI 74/74, Server 8/8, Transport 12/12.
