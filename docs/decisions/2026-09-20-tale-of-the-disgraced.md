# The Tale of the Disgraced (oracle-l5r-10553, strategy-wave-28)

Status: implemented; the reading marked **Mendon** is his ruling, quoted from the coordinator's message of 2026-09-20 (**confirmed by Mendon**). Every other reading is a decision of this
change, recorded with its alternative. Lane: rules (mdornbrook; cross-lane edits under his standing authority). Issue: #369. Builds on `2026-09-19-hotako-yoshitada-predator-liuxeung.md`
(the multi-target primitive, PR #348) and `2026-09-19-delayed-move-home-and-wave.md` / `2026-09-19-battle-rulings-round-2.md` (the delayed straighten and the before-resolution set).

> **The Tale of the Disgraced** (Coils of Madness 135, Strategy, Focus 2, no cost box) -- Battle: Bow your target unbowed Personality. Bow a target enemy card. If your Personality is
> Fallen or a Magistrate, straighten him before this battle resolves.

Read from the scan (the working copy's sha256 checked against the committed receipt, `assets/reference/oracle/coils-of-madness/receipts/oracle-l5r-10553-printing-1-image-1.json`,
`744066a3...`; it was already in the local reference cache, nothing was downloaded). The published text is the catalogue text, which the scan matches word for word. No keyword line.

## Mendon's ruling

"The first bowing in Tale of the Disgraced is not a cost, it is an effect." So the card has **no cost but its Gold and its hand play**, and its three sentences are three effects that resolve
independently unless negated separately. Nothing is declared in `AbilityDefinition.Costs` (#356): there is nothing to declare, and a negated Tale pays only what every negated Strategy pays (it
still leaves the hand).

## The shape (`tale-of-the-disgraced`, no new primitive)

A Battle ability with two **up-front card slots** on the multi-target primitive (#336): `own` -- "your target unbowed Personality" -- and `enemy` -- "a target enemy card", each Min 1 / Max 1,
independent and judged one by one. `enemy` is the existing `enemy-card` category (any card of the other player at the battle: a Personality, a Follower, an attached Item or Spell, whatever its state,
so an already bowed one is a legal target and bowing it changes nothing). `own` is a new target category, `own-unbowed-personality` (`personality && own && !Bowed`), the first "your target unbowed
Personality" *target* (Strength of the Bear, the nearest card, models the same words as a chosen performer; the coordinator asked for two targets here). The shared Battle presence rule applies to
both slots as to any Battle action: each target must be at the current battlefield. The option carries both slots' legal candidates and a complete default command (`QueryActions`), exactly as
Yoshitada's and Bonds of Coin's do; the viewer is #345 (nothing new for it).

**Effect.** In the printed order: bow `own` (`StalwartNegatesBow`, which is where a per-card negation of that one bow is asked), bow `enemy` (the same), then, if `own` is still in play and is Fallen
or a Magistrate, register the delayed straighten (`RegisterDelayedBattleEffect(player, source, "straighten", own)`, #354/#365). Each line is its own effect: a negation of the first bow
(the bow guard fixture, a Reaction of the Personality's controller) leaves the second bow and the straighten alone; a negation of the second (Champion of Thunder in the enemy's unit, Stalwart) leaves
the first bow and the straighten alone; a negation of the **whole** action (Sakuma, Jinn-Kuen's shape) stops all three. `tale-of-the-disgraced` is in `BowsTarget` (it bows its targets), so the curated
bow-negating Reactions (Brothers of Goemon) see it.

## Fallen and Magistrate

**Both are keywords**, tested with the engine's live `Keyword(card, ...)` -- printed or granted, which is how the engine already reads "Fallen" (Akodo Tsudoken's "Lion Clan or Fallen Personality") and
"Magistrate" (Fukuzo, Yoshitada). The **dishonorable status is not Fallen**: a dishonorable Personality with neither keyword is not straightened (tested), one that is also Fallen is.

**When the condition is read (decision).** When the action's third sentence **resolves** -- after the before-action windows have closed and both bows have been applied or negated -- not when the
delayed effect later fires. So a Personality that is a Magistrate as the Tale resolves is straightened before the battle resolves even if a later effect removes the keyword; one that only *becomes*
Fallen afterwards is not; and his own bow being negated does not matter (the sentence is about his status, not the bow). This is the rule the follow-up slots of the previous change use (a conditional
sentence is judged when it resolves, and independently of the sentence before it) applied to a delayed effect: the condition is part of the action, the delayed effect is only "straighten him".
*Alternative:* register always and read the condition when the effect fires (rejected: the text is the action's own "If ...", and it would make a card that says nothing about later changes react to them).
If he is no longer in play as the sentence resolves (a reaction destroyed him) nothing is registered.

## The delayed straighten (the existing primitive, exercised)

It joins the ONE ordered before-resolution set (`BeforeResolution.cs`): after both players pass, before Army Force is tallied, the active player orders the set (one item: it runs directly), and it is
logged as a `beforeBattleEffect`. Tested: a bowed Fallen or Magistrate Personality **counts** in the battle's Force (`attacking Force 4`), and the same Personality without either keyword is
still bowed and does not (`attacking Force 0`). The effect **belongs to its battle** and lapses with it, applied or not: if the Personality has left play by the time the set reaches it the effect is
skipped without a throw and spent; and nothing is left registered when the battle has resolved, so nothing carries into the next battlefield. The engine has exactly one way a battle ends -- its
resolution, which always gathers the set first -- so "the battle ends early" can only mean the target is gone (the first test) or the effect being spent (the second); both are covered.

## Hidden information

The Strategy is played from the hand. Before it is played it appears in nothing of the opponent's: not his `MatchView`, not his `QueryActions` (tested by the serialized JSON containing neither the card
id nor its printed name); the slots never list the opponent's hand or a face-down province card, and naming one is refused like any illegal target. Once played it is in the discard pile, visible to both.

## Windows per target (the primitive's own rule, exercised)

One before-action window per target that has a possible reactor, in slot order: the first slot's window offers only a protective Reaction of the Personality's controller, the second slot's only Champion of
Thunder; a target nobody can react to opens none, and with no reactor at all the action resolves at once. (`training-bow-guard`, a synthetic Reaction, exists because no printed card can protect the ACTING
player's own Personality from his own action's bow -- Champion of Thunder and Stalwart only react to another player's action -- and the first bow's independent negation needed a real window to be tested.)

## Packaging

`strategy-wave-28` (the highest strategy wave on origin/main is 27; 19, 20 and 24-26 are gaps other work may still claim). `tools/content/package_strategy_wave_28.py` (allow_text: the card has rules
text; it verifies the working copy's sha256 against the receipt and copies the art to `src/L5R.Client/Content/Published/oracle-l5r-10553.jpg`, the same tool every wave uses), the per-wave manifest
`tests/L5R.Core.Tests/PublishedWaves/strategy-wave-28.txt` (`tools/content/pin_wave_ids.py`; the published count pin no longer exists), the embedded resource and loader, the catalogue concat, and the BLOCKERS
entry removed (the inventory workbook is regenerated: 443 wired). The published pool is **443** distinct non-ScenarioOnly ids (442 on origin/main plus this one).

## Tests and mutation checks

`TaleOfTheDisgracedTests` (12 tests, all through `QueryActions` and the commands it offers): published shape and the art's receipt; the offered option and its exact legal candidates; every illegal target
and selection, refused atomically; both bows and the condition (Fallen, Magistrate, neither, dishonorable, both); the straighten before Force is tallied and logged before the resolution; the lapse; negation of
only the first bow; of only the second (Champion of Thunder, Stalwart, the enemy controller's guard); of the whole action; one window per target; hidden information; and every legal pair validating on a fresh engine and surviving JSON.

### Mutation checks

Each mutation was applied to the engine source (or the published JSON) alone, the whole suite rebuilt and run, and the source restored (a throwaway script, not committed).

| Mutation | Result |
|---|---|
| the first slot allows a bowed Personality | `OfferedWithLegalCandidates`, `LegalAndIllegalTargets` |
| the first slot allows an enemy Personality | `OfferedWithLegalCandidates`, `LegalAndIllegalTargets`, `OfferedCommandsValidate` |
| the first slot allows any own card | `OfferedWithLegalCandidates`, `LegalAndIllegalTargets` |
| the second slot allows an own card | `PublishedShape`, `OfferedWithLegalCandidates`, `LegalAndIllegalTargets`, `OfferedCommandsValidate` |
| the second slot is optional | `PublishedShape`, `OfferedWithLegalCandidates`, `LegalAndIllegalTargets` |
| Magistrate does not straighten | `BowsAndTheCondition`, `StraightenBeforeForceIsTallied`, `NegatedSecondBowOnly` |
| Fallen does not straighten | `BowsAndTheCondition`, `StraightenBeforeForceIsTallied`, `StraightenLapsesWithItsBattle`, `NegatedFirstBowOnly`, `NegatedSecondBowOnly` |
| the straighten is unconditional | `BowsAndTheCondition`, `StraightenBeforeForceIsTallied` |
| a dishonorable Personality counts as Fallen | `BowsAndTheCondition` |
| the straighten is for the enemy card | `BowsAndTheCondition`, `StraightenBeforeForceIsTallied` |
| the straighten happens at once, not before resolution | `BowsAndTheCondition`, `StraightenBeforeForceIsTallied`, `StraightenLapsesWithItsBattle`, `NegatedFirstBowOnly`, `NegatedSecondBowOnly`, and 2 more |
| the condition needs his bow to have happened | `NegatedFirstBowOnly` |
| a negated first bow stops the second bow | `NegatedFirstBowOnly` |
| a negated second bow stops the straighten | `NegatedSecondBowOnly` |
| the first bow cannot be negated | `NegatedFirstBowOnly` |
| the second bow cannot be negated | `NegatedSecondBowOnly` |
| the first bow is skipped | `LegalAndIllegalTargets`, `BowsAndTheCondition`, `StraightenBeforeForceIsTallied`, `StraightenLapsesWithItsBattle`, `NegatedSecondBowOnly`, and 2 more |
| the second bow is skipped | `LegalAndIllegalTargets`, `BowsAndTheCondition`, `StraightenBeforeForceIsTallied`, `NegatedFirstBowOnly`, `WindowPerTarget`, and 1 more |
| the bow guard does not protect (fixture reaction) | `NegatedFirstBowOnly` |
| the guard reaction reads the command's first target | `NegatedSecondBowOnly` |
| the art sha in the sources is wrong | `PublishedShape` |
| the published Focus is wrong | `PublishedShape` |
| the published ability is dropped | `PublishedShape`, `OfferedWithLegalCandidates`, `LegalAndIllegalTargets`, `BowsAndTheCondition`, `StraightenBeforeForceIsTallied`, and 6 more |

23 of 23 mutations are killed, the whole suite being run each time. One survived the first run -- "the guard reaction reads the command's first target" -- because in a test where the guard protects the FIRST
slot's card the window's target and the command's are the same card; the second-slot case (the enemy controller's guard in the enemy card's window) was added and kills it. Not mutated: the whole-action
negation itself (it is #338's and #356's, covered by their suites; `WholeActionNegated` pins its effect on this card) and the per-target window logic (the primitive's, covered by the mutations of PR #348).

## Affected files

New: `tools/content/package_strategy_wave_28.py`, `content/published/strategy-wave-28.json` and `-sources.json`, `src/L5R.Client/Content/Published/oracle-l5r-10553.jpg`,
`tests/L5R.Core.Tests/PublishedWaves/strategy-wave-28.txt`, `tests/L5R.Core.Tests/TaleOfTheDisgracedTests.cs`, this note. Changed: `src/L5R.Core/CardAbilities.cs` (the ability, the `own-unbowed-personality`
category, the effect case, `BowsTarget`, the bow guard fixture), `CardCatalog.cs` (the concat and the fixture card), `PublishedContent.cs`, `L5R.Core.csproj`, `tools/content/build_card_inventory.py`,
`docs/CARD_INVENTORY.xlsx` (regenerated).
