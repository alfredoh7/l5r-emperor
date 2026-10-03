# Follower tokens shield their Personality from Attacks (issue #490)

Ruling from Mendon (2026-09-24): only Followers prevent their attached Personality from being targeted by Ranged and Melee Attacks, unless an ability printed on the Attack grants such an effect. Follower cards and Follower tokens both achieve this. Spells and Items are attachments too, but they only prevent effects that target "cards without attachments" from targeting the Personality; they do not block Attacks.

## What was wrong

`IsFollowedPersonality` (`AttackTargeting.cs`) only counted attached Follower cards, so a Personality carrying a Follower token was still a legal Attack target. (The "without attachments" side was already right: `HasAttachments` counts any attached card and Follower, Weapon and Armor tokens.)

## What changed

- `IsFollowedPersonality` now also counts a Follower token. Every existing exception (an ability that may target a followed Personality, a static trait of the performer, a standing grant) applies to it exactly as to a Follower card.
- Item and Spell attachments, and Weapon and Armor tokens, still do not block an Attack; they still count for "without attachments" effects.
- `NoAttachmentsFilter` now pins the ruling: a Follower token rejects the Attack; a Spell, Weapon token and Armor token do not; the existing Item and Fire-token cases are unchanged.

Verification: full core suite 0 failures.
