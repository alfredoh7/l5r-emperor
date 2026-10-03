# Attack targeting: a target above the Attack's strength is legal (issue #488)

Ruling from Mendon (2026-09-24): the core rule on Attacks is correct, and a Ranged or Melee Attack may also be produced with no target selected.

Core rules, Ranged Attack / Melee Attack: "Ranged X Attack" is shorthand for "Target a Follower or a Personality without Followers in the current enemy army. If its Force is <= X, destroy it." Targeting is not required up front, so an action containing a Ranged Attack can be announced with no valid target in sight.

## What was wrong

`RequireLegalAttackTarget` (`src/L5R.Core/AttackTargeting.cs`) also required the target's compared value (Force, or the stat an effect compares, such as Gold Cost) to be at or below the Attack's strength. That turned the rule's "if its Force is <= X, destroy it" into a targeting restriction: a stronger card could not be targeted at all, so the Attack (and any effect that applies to a target that survives, such as Koshin Keep's) could not be aimed at it. Found by the Emperor Edition Stronghold pass while wiring Koshin Keep.

## What changed

- The strength comparison is removed from target legality. Every other restriction is unchanged: the target must be a card in the current enemy army, a Follower or a Personality without Followers (unless an exception applies), not Melee/Ranged immune, and so on.
- Resolution (`ResolveAttackNow`) already compared the target's Force (or the compared stat) to the strength before destroying, so a stronger target is now legal to target and simply survives. Costs are still paid and the Attack still counts as having targeted a card (Carpenter Castle's +2 Honor).
- An Attack with no target continues to work; it does nothing.
- ~40 existing tests pinned the old rule (they asserted a stronger target was rejected or not offered). Each was updated to the rule: the target is accepted or offered and survives; everything else each test verified is unchanged. A new regression, `AttackMayTargetAboveStrength`, covers a Ranged 3 Attack at a Force-5 Personality, the no-target Attack and Carpenter Castle's cost/Honor.
- One knock-on: with the strength check gone, the old `NoAttachmentsFilter` test's Item and Follower-token cases turned out not to be attachment blocks at all (only an attached Follower card blocks an Attack, per the core rules); those cases now target a card within strength and are destroyed, and their comments were corrected. Whether Item or token attachments should ever block an Attack is a separate decision.
- Redirect windows (Ezuno, Blade of Champions, Hida Kisada) now open for a target whatever its Force, as it is a legal target.

## Verification

Full core suite 0 failures (1497 passes); AI 74/74, Server 8/8, Client.Transport 12/12.
