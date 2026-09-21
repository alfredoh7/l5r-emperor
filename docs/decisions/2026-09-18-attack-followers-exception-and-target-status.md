# Attack-vs-Followers exception (P1) and the target-status predicate (P2), with personality-wave-24

Status: accepted; one rules question open (below). Lane: rules (engine primitives plus a six-card Personality wave). Issue: #313, requested
directly by Mendon (the rules lead) for Bayushi Kahoku - exp (`oracle-l5r-807`).

Mendon's rulings, which this note implements and does not re-litigate:

- Kahoku's trait "breaks the rules" of Ranged/Melee Attacks. The rule is that a Personality may not be targeted by a Ranged or Melee Attack if
  he has attached Followers, UNLESS the card producing the Attack (or another card modifying it, e.g. a Reaction) explicitly says it MAY
  target Personalities with attached Followers. Several cards do this, so it is generalized to both Ranged and Melee.
- The dishonorable/honorable condition Kahoku depends on is also common on strategy cards and becomes a generalized primitive that checks a target
  for honorable or dishonorable status per the action's specification. Dispensing Justice has the same mechanism combined with keywords
  (dishonorable, Kolat, Ninja or Shadowlands: any one is sufficient, several still count once).

## P1 -- the Attack-vs-Followers exception (`AttackTargeting.cs`)

The default rule is unchanged and lives in one place, `RequireLegalAttackTarget`: an Attack target is a Follower, or a Personality with no attached
FOLLOWER. Only a Follower attachment blocks direct targeting; an Item or Spell never does (A Close Call's premise). What is new is
`AttackFollowerException(Kind, Condition, TargetIs)`, one explicit permission:

- `Kind` -- `"Ranged"`, `"Melee"` or `"Both"`. Melee Attacks follow identical rules but are not Ranged Attacks, so a card may reach either or both.
- `Condition` -- a game-state gate; `Reconnaissance` (the acting player holds it at the CURRENT battlefield; another province's, or the opponent's,
  does not count). An unknown condition throws rather than silently reading as "never".
- `TargetIs` -- a P2 target-status gate on the targeted Personality (`TargetStatus.IsDishonorable` for Kahoku).

It reaches an Attack through five sources, all evaluated by the single `AttackMayTargetFollowedPersonality`:

| Shape | Printed form | Mechanism |
| --- | --- | --- |
| (a) | on the attacking ability, unconditional | `AbilityDefinition.FollowerException: new("Ranged")` (Tsuruchi Kosoko - exp) |
| (b) | on the ability, conditional on game state | `FollowerException: new("Ranged", Condition: Reconnaissance)` (Strategic Strike, Toritaka Chokichi text; unwired) |
| (c) | conditional on the target's status | `TargetIs: TargetStatus.IsDishonorable` |
| (d) | source-scoped static trait | `StaticAttackFollowerExceptions`, keyed by the card's `Effect`; compares the action's PERFORMER, so it applies to whatever ability that card performs (including a wielded Item/Spell's) and never to an Attack performed by another card (Kahoku - exp) |
| (e) | granted by another card before the Attack | `GrantAttackFollowerException`, a standing grant in `MatchState.AttackTargetGrants` (Yoritomo Ebumi) |

Shapes (b)/(c) printed on an ability are proven end to end through the `training-followed-attacker` fixture, since no published card carries them yet.

### What a grant (e) can and cannot do

An Attack's target is chosen and validated ONCE, at declaration (the `useAbility` command carries it); `PendingAttack.TargetId` is fixed from then on.

- It CAN leave a standing permission that makes the player's NEXT Attack action of the covered kind (any ability of that kind, from any card he
  controls) able to name a followed Personality, subject to its own Condition/TargetIs. The grant is spent by that action whether or not the
  exception was needed ("your next Ranged Attack action"; a Melee action leaves a Ranged grant alone), belongs to its own player, and expires when
  the battle resolves.
- It CANNOT be created after the declaration to rescue that declaration. A Reaction inside the announce window (opened after validation) or the
  before-attack-resolves window can change the Attack's strength and its target's Force or status, but can neither legalize a target the
  declaration rejected nor re-target a pending Attack. Cards such as "That Ranged Attack may target a card in the Defender's home" (No Hiding
  Place) need a target re-selection step in those windows, which does not exist; they are a different exception axis (target location) and are
  not part of this primitive.
- Legality is not re-checked at resolution: a target legal when declared stays the Attack's target even if the exception's own condition (say
  Kahoku's "dishonorable") stops holding before it resolves; only the Force comparison is re-read.

### What happens to a followed Personality

- The Personality dies as a unit: destroying a Personality takes his attached Followers with him (core rules, "If the Personality leaves play, the
  whole unit leaves play the same way"). `DestroyUnits` already discards each attachment to the Fate discard pile as a destroyed card
  (Expendable draws included); nothing new was needed. This is settled by the rules text, not a decision.
- OPEN RULES QUESTION (the one decision point, `AttackComparedForce`): which Force is compared to the Attack's strength when the target is a
  Personality WITH Followers -- the Personality's OWN Force (printed plus modifiers plus Item/Spell bonuses, never his Followers'), or the whole
  UNIT's Force total (Personality plus each attached Follower, the quantity battle resolution uses)? The core rules define a Ranged X Attack as "If
  its Force is <= X, destroy it" about the targeted card and define "Unit Force total" only for battle resolution, so the literal reading -- the
  targeted Personality's own Force -- is what is wired, provisionally. Every caller comparing an Attack's target to its strength
  (declaration-time legality and resolution-time destruction) goes through that one method, so a ruling for the unit total is a one-line change plus
  flipping one test assertion (`AttackFollowersExceptionShapes`, the "Personality Force 5 <= strength 6" case). For every target that needs no
  exception the two readings are identical.

### Kahoku - exp's action

"Target an enemy Personality: Give him -4F. Ranged 3 Attack." is two SEPARATE selections: the -4F target is the ability's `TargetId`; the Attack
names no target of its own, so per the core rules it is its own targeting step ("not required up front"), carried on `CardIds` (the established
optional-second-selection idiom of Scorn the Weak and Strength in Terror). The Attack may, but need not, hit the Personality just weakened, and its
declaration-time strength check sees the same action's -4F on that Personality (otherwise the card's shrink-then-shoot use would be illegal); the
resolution-time comparison re-reads live Force. The Attack half is optional. There is no client UI yet for the second selection (the same limit
Scorn the Weak has); the command is offered with the first legal -4F target and no Attack target.

Mendon's confirmation of this reading (2026-09-19), verbatim: "Bayushi Kahoku: Targets a personality, applies a -4F to the targeted personality.
Then, Bayushi Kahoku makes a Ranged 3 Attack. The Ranged Attack is a self contained action, so it may target a different card than the target that
was given a -4F. Bayushi Kahoku's force is not modified by this action. If the target of the ranged attack is a dishonored personality, the ranged
attack may target that personality regardless of whether or not they have followers attached." Each clause is pinned by `BayushiKahokuExpTrait`:
the Attack may hit a different card (or the same one, or nothing), the -4F lands only on the `TargetId` Personality and never on Kahoku (his own Force is
asserted unchanged, and the test fails if the -4F is applied to him), and a dishonorable followed Personality is a legal Attack target only for Attacks
Kahoku performs, while an honorable followed one is not.

## P2 -- the target-status predicate (`TargetStatus.cs`)

`TargetStatus(Honorable, Dishonorable, Keywords, Clans, ClanOrKeyword)` is a declarative ANY-OF test on a target card. Satisfying several terms is
still one match (the answer is a boolean, never a count); satisfying any one is sufficient; an empty specification matches nothing.

- Honorable / Dishonorable: the engine's one existing notion -- `CardInstance.Dishonorable` on a Personality. Only a Personality is ever
  honorable or dishonorable, so a Follower never satisfies either term. The `honorable-personality` / `dishonorable-personality` Target categories
  now ask the predicate, so there is no second representation.
- Keywords: printed or granted (the live `Keyword()` check). Clans: Clan alignment only. ClanOrKeyword: a name held either as a Clan alignment or
  as a keyword -- "Shadowlands", which Asako Rikate, Takasho, Keppo and Daigotsu Kendo print only as a keyword beside their real Clan.
- Presets: `IsHonorable`, `IsDishonorable`, `IsShadowlands`, `KolatNinjaOrShadowlands`, `DishonorableKolatNinjaOrShadowlands`.

It works in two roles, both parameterized by the action's own `AbilityDefinition`:

- an EFFECT condition, `IfTarget`, read with `MatchEngine.EffectConditionMet` ("destroy him if ...", "+2 strength if the target is dishonorable");
- a target-LEGALITY condition, `TargetMustBe`, layered on whichever Target category the ability already uses exactly like `RequireNoAttachments`
  ("target an enemy dishonorable Personality"), and `AttackFollowerException.TargetIs`.

### Migrated onto P2 (each parity-tested; every old outcome is preserved except the intended fix)

- `dispensing-justice-destroy` and `winds-of-dismissal-choice`: `DishonorableKolatNinjaOrShadowlands`. Both previously checked Clan alignment only
  and missed the Shadowlands-KEYWORD-only cards; they now match them (Asako Rikate is destroyed by Dispensing Justice; Winds of Dismissal costs his
  controller Honor). The "no attachments" clause of Dispensing Justice is untouched.
- `mitsuko-bow-destroy` (Isawa Mitsuko, Experienced): `KolatNinjaOrShadowlands` -- exactly its old hand-rolled test (its own `IsShadowlands` helper
  is gone; the term replaces it).
- The non-experienced Kahoku's "+2 strength if the target is dishonorable": `IfTarget: IsDishonorable`, read by `AttackStrength`.

### Left hand-rolled (no risk taken; listed rather than forced)

`.Dishonorable` reads that could use `IfTarget` unchanged: `yogo-adi-choice`, `law-of-darkness-weaken`, `nori-bow-personality` /
`nori-bow-attachments`, `soshi house guard`'s Honor loss, `nakai-exp-weaken`. Board-state (not target) tests that are a different shape:
`ControlsDishonorablePersonality`, `personality-controller-controls-dishonorable`, `force-if-enemy-dishonorable` (Kahoku's own +2F),
`force-vs-dishonorable-or-gaijin-opposed` (Kitsuki Miroken), `attachment-or-dishonorable-personality`, `optional-dishonorable-personality`. And two
Performer-side Shadowlands checks that still test Clan alignment only and so still miss keyword-only Shadowlands cards: the
`own-shadowlands-personality-at-battle` Performer (Consuming Weakness) and `bow-before-my-will`'s "if your Paragon is Shadowlands".

## The family, with wired / unwired status

Attached-Followers exception (P1), from `content/oracle/catalogue-index.json`:

- Bayushi Kahoku - exp (807): WIRED, shapes (d)+(c).
- Tsuruchi Kosoko - exp (9831): WIRED, shape (a).
- Yoritomo Ebumi (9832): WIRED, shape (e); the additional Battle action uses the existing grant behind a Reconnaissance check.
- Strategic Strike (7525): unwired. Its exception (b) is ready, but a Strategy played from hand whose Attack opens a reaction window is never
  discarded from hand (`ResolveOrDeferAttack` returns before `ExecuteAbilityEffects`' tail), a latent bug Strength of the Divine and False Ring of
  Water already have; it needs that fix and a strategy wave.
- Toritaka Chokichi (8644): unwired; also needs a Follower-eligible Performer category ("bow your performing Follower").
- Ikiryo (3589): unwired; the exception (a) is ready, but it needs "last attached to" tracking (strength is that Personality's Chi).
- Elephant Caravan (9868) is NOT a member: its BLOCKERS note claimed this override, but the card prints no Attack. The note is corrected.
- Not this axis (a Defender's-home target exception): No Hiding Place, Lookout, Strength in the Earth.

Target-status (P2) wave-24 cards: Kakita Kazan (4185) uses `TargetMustBe`; Hiruma Nitani (3311) and Kuni Shinoda - exp (10276) use `IfTarget`.
Bayushi Shibata - exp (10080) was built and then deliberately NOT published: its "+3 strength" is a province-strength stat change the core rules
end with the turn, while every province-strength change in the engine (Improved Defenses, Ring of Earth, Yogo Adi, Dueling) is permanent, so it
waits on a timed province-strength modifier (now a BLOCKERS note).

## Affected files

`src/L5R.Core/AttackTargeting.cs`, `TargetStatus.cs` (new), `CardAbilities.cs`, `MatchEngine.cs`, `Models.cs` (`AttackTargetGrants`),
`PublishedContent.cs`, `CardCatalog.cs`, `L5R.Core.csproj`; `content/published/personality-wave-24*.json`;
`tools/content/package_personality_wave_24.py`, `build_card_inventory.py`; `tests/L5R.Core.Tests/Program.cs`, `PublishedTests.cs`.
