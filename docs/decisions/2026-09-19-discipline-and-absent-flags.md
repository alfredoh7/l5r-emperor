# Discipline as a play mode, Absent as a flag, and Return to Action

**Status:** implemented in PR #332 for issue #331 (rules lane; cross-lane authority of the rules lead). The interpretations that were
open rules questions are **all closed** by Mendon's rulings, recorded in
[2026-09-19-discipline-rulings.md](2026-09-19-discipline-rulings.md) (issue #353). **Issue:** #331.

## The request

Mendon, verbatim: "Let's wire up a Discipline Battle action so that you have a reference card to test. Return to Action from
Aftermath has the Discipline trait (a player may pay 2 gold to play this from their discard pile). In addition, Return to
Action has an Absent Battle action (player does not require presence to play an Absent Battle action). If the player is the
defender, they may target a personality at home and move their unit to the battlefield. If the movement action resolves, the
personality is straightened."

The card (`oracle-l5r-10953`, Aftermath #138, Strategy, Focus 3, no Gold Cost box; scan verified, the WebP working copy's
sha256 matches its receipt):

> Discipline :g2: (You may pay 2 Gold to play this Discipline from your discard pile, then remove it from the game after its
> action resolves.)
> Absent Battle: If you are the Defender, target your Personality at home and move him to this battlefield. After he moves,
> straighten him.

Published as `strategy-wave-18` (free on `origin/main` and in every open PR; PR #328 claims strategy-wave-17).

## A. The Discipline model

Before: Discipline was an ability `Timing == "Discipline"` usable only from a discard pile. Consequences: a Discipline card in
hand could not be played at all, the printed verb was enforced by a per-id hack (`if (id == "banzai") ...`), and Lost in
Transit skipped the presence rule its printed "Battle/Open" verb carries.

Now: Discipline is a **flag on the ability** (`AbilityDefinition.Discipline`, a trailing defaulted field). The ability's
`Timing` stays its printed verb (`Battle`, `BattleOrOpen`, ...), so its phase, presence and target rules are exactly what the
card prints (the supplement: Discipline layers a play mode on top of "using one of its actions"). The flag adds one source
zone and one price, decided by where the card is when the action is played (`MatchEngine.DisciplineFromDiscard`, the single
place):

| Mode | Source | Price | After the action resolves |
|---|---|---|---|
| Ordinary play | a Strategy in **hand** | free (a Discipline Strategy prints no Gold Cost box; the icon IS the Discipline cost) | discarded like any Strategy |
| Discipline play | a card in a **discard pile** (`FateDiscard`/`DynastyDiscard`) | the Discipline cost = the card's published `GoldCost`, through the one Gold-quote path (`GoldQuotes.ImplicitGoldRule`; quote basis "... Discipline cost N") | **removed from the game** |

- A card is in one zone at a time, so it is offered as exactly one of the two, never both; a card in hand is never offered as
  a discard play and vice versa (tested).
- The published `GoldCost` of a Discipline card holds the Discipline cost (Return to Action 2, Banzai! 2, Lost in Transit 1).
  The hand play must not read it, and does not (tested: no payment event, sources rejected).
- A non-Strategy card carrying the flag (the `training-discipline-item` fixture) gains only the discard-pile mode: a hand play
  exists only for a Strategy, whose printed action is its ordinary play.
- **Once per turn still applies to the card** (Aftermath supplement: "a Discipline card generally cannot be played from the
  discard pile on the same turn it was played from hand"): the card's own once-per-turn stamp survives the move to the discard
  pile, so a same-turn replay is rejected and a later turn's is allowed (tested).
- The removal is decided up front (`disciplineFromDiscard`, before anything moves the card) and also handled on the two early-
  return paths (a deferred Attack, a challenge-issuing duel) so no future Discipline Attack or duel is left in the pile.
- Not modeled: a Discipline card whose action **also** has its own Gold cost, where the supplement adds the two together
  (Knowledge and Power, Two Steady Breaths). No wired card does; `ImplicitGoldRule` documents the extension point.

## B. The Absent flag

`AbilityDefinition.Absent` (Coils of Madness rule 9: "the action may be taken even without controlling any unit at the current
battlefield"). Per the request it waives **both** presence Requires in `ValidateAbility`'s battle block: the acting player's
unit at the battlefield and, for a Personality source, the source being at the battlefield. It is otherwise still a Battle
action: Battle phase only, and its Target/Performer keep whatever location rule they print (the shared "target must be at the
battlefield" Require is not waived, except by the target category's own exemption).

There is no separate mirror in `AbilityActions`: option generation calls `ValidateAbility` per candidate, so an offered command
is by construction one the flag allows; the only parallel check there (`actingAsBattle` for performers) is about Battle/Open.

**Migrated onto the flag** (wired abilities whose printed text says Absent, or says exactly what rule 9 says):

| Ability | Printed | Effect of migrating |
|---|---|---|
| `ring-of-earth-raise` / `-lower` | "Even if you control no units at the current battlefield" (= rule 9's gloss) | none: they were id-list entries; a Ring is never a Personality, so the flag's source half never applied |
| `ring-of-water-move-home` | "Water Absent Repeatable Battle" | none observable: its target is your own Personality **at** the battlefield, which is presence |
| `false-ring-of-air-force` | "Absent Battle, bow" | **behavior change**: the printed Absent was previously dropped (documented simplification #194), so it needed presence; now a player with no unit there may target an enemy Personality at the battlefield. Tested with and without presence |
| `return-to-action-move` | "Absent Battle" | new |

**Left on the old id list (`reinforcesFromElsewhere`), and why:** `michi-move`, `rikyou-move`, `takasho-move`,
`intervening-spirit-move`, `houken-exp-straighten`, `sanctuary-of-the-waves-move`, `mai-move-force-penalty`,
`your-clan-needs-you`, `munefusa-move`. None prints "Absent". Their exemptions are engine-side rulings that a self-moving
Personality "reinforces" a battle, and several print "Even if this unit / Munefusa is not at the current battlefield" -- which
is the *Home* concept (about the CARD's location, its own rule 9 keyword), not Absent (about the PLAYER's presence). Folding
them into `Absent` would relabel a ruling as a printed keyword and blur two different rules; each keeps its existing tests.
`recruitReserve` is a command, not an ability ("Reserve recruitment is an absent battle action"), and is unchanged.

**Charge Into Danger is not on the flag, and the request's example does not hold.** Its *scan* (Torn Asunder #110) prints
"Battle: Choose your performing Personality: If he assigned to, or would be opposed at, the current battlefield, move him there.
..." -- plain Battle, which is what we publish (a period printing; we do not silently change those). The catalogue's
`current_oracle_text` for it is a later re-templating ("Absent Battle: Target your performing Personality at any location who
either is at, assigned to, or would be opposed at ..."). It requires presence today and keeps doing so. If Mendon wants the
Oracle text instead, it is a one-line flag plus a performer-location change; that is a printing decision, not part of this work.

## C. Return to Action

`return-to-action-move`: Timing `Battle`, `Discipline: true`, `Absent: true`, Target `own-personality-at-home` (new).

- **Defender only**: the acting player is not `ActivePlayerId`; checked in `ValidateAbility` (before the effects colon, so
  every offer and every submitted command sees it). The attacker's option is disabled with a reason.
- **Target**: the acting player's own Personality in `Zone.Home`, bowed or not (the straighten is part of the card, so a bowed
  one is a legal and the best target). A Personality at a battlefield, an opposing one, a dead one, a Holding or an attachment is
  rejected. The shared "Battle target must be at the battlefield" Require is exempted for this category (home is not the
  battlefield).
- **The move** reuses `MoveToBattlefield`: the whole unit goes to the current battlefield (attachments travel), so he fights
  in the defending army for that battle, and Legion of the Bat's move reaction still sees him. New `TryMoveToBattlefield`
  wraps it with the same `NegatesEffect("move")` check `MoveHome` has, because this is the first move-to-battlefield that applies
  to a **target** (every other one moves its own source/performer, which no negation window ever names). The ability is
  registered as kind `move` in `MoveOrStraightenKind`, so Tamori Tamoko's window is offered (either player's).
- **The straighten** happens only if the movement resolved ("If the movement action resolves, the personality is
  straightened"): a negated move (Tamoko, Champion-style "all") leaves him at home, bowed if he was, no straighten. Only **he** is
  straightened, not his unit ("straighten him"). The card is still spent when the movement is negated (hand: discarded;
  discard pile: cost paid, removed from the game).
- Both modes work end to end through `QueryActions`: the hand copy is offered free (no quote, no sources), the discard copy
  with a fixed 2 Gold quote and the engine's suggested sources; both offered commands validate on a replay (tested).

## Behavior changes to already-wired cards (read these)

1. **Banzai! and Lost in Transit are now playable from HAND** (free, then the Fate discard pile). Previously a Discipline card
   in hand could not be played at all (it was offered disabled).
2. **Lost in Transit, played during a battle, now needs the acting player's presence at the battlefield** (and its target
   attachment at that battlefield), like every Battle/Open card since #267. The old Discipline timing skipped it. Played as an
   Open action in the Action phase it is unchanged (no presence). Its existing regression test gained one unit for player 0
   for this reason -- the only existing test that changed for behavior.
3. **False Ring of Air's Absent is now real** (see the table above).
4. Banzai! is unchanged for the discard-pile play (the per-id narrowing hack is deleted; its printed `Battle` timing gives the
   same Battle-phase-plus-presence rule). As genuine `Battle` timing it is now subject to the general Battle-action machinery
   (Times of Strife's discard, first-action limits, an Additional Action grant), which is correct for a Battle action.

## Rules questions (were OPEN; CLOSED by Mendon, see 2026-09-19-discipline-rulings.md)

1. **CLOSED (confirmed).** A Discipline card in hand is a free ordinary Strategy play; its other printed costs still apply.
2. **CLOSED.** Lost in Transit needs presence only as a Battle action (none in the Action phase) -- as built. False Ring of Air is a
   true Absent Battle action and bowing the Ring is a cost -- confirmed, with a fix for the hand play.
3. **CLOSED.** The "Discipline cost plus the action's own cost" case that was "not modeled" is now built (issue #353).

The original text of the three questions follows, for the record.

1. **A Discipline card in hand is a free ordinary Strategy play.** The catalogue text of all seven Discipline cards prints no
   Gold Cost; the supplement says Discipline is "an additional way" to play from the discard pile. I read that as: the base card
   is a normal Strategy played from hand for its printed cost (none). If the intended reading is that a Discipline card can only
   ever be played from a discard pile, the hand mode is one line in `ImplicitGoldRule`/`ValidateAbility` to disable.
2. **Lost in Transit's presence in a battle** (change 2 above) and **False Ring of Air's Absent** (change 3): both follow the
   printed verb strictly; say if either should stay as before.

## What the two flags unblock

**Discipline cards still unwired** (none touched):

- Expensive Achievement (10938, "Battle: Give this Province +2 or -2 Strength"): straightforward now -- Discipline flag, a
  dual-UsageKey pair like Ring of Earth's, an effect on the current battlefield's province.
- Knowledge and Power (10942, "Limited, :g1:") and Two Steady Breaths (10961, "Limited, :g1:"): each action has its **own** 1 Gold
  cost, so they need the additive "Discipline cost plus the action's own cost" extension noted above, plus their own effects
  (token destroy/create; a keyword grant to a face-up Personality out of play).
- Mercantile Conflict (10947, Political Open): needs an "increase the next two Honor losses" modifier primitive.

**Absent**: the catalogue has 32 cards containing the word "Absent"; 8 are wired (Return to Action makes 9; Charge Into Danger is one of the
8 only because the catalogue's re-templated text says Absent -- its scan does not, see above), 24 were unwired before this work. Of
those 24 (Return to Action being one):

- **Reminder text only, no Absent action of their own (6):** Concealed Reserves, Daigotsu Chiboshi, Isawa Ikariya, Kakita Ujirou,
  Hida Iguchi, Utaku Sakiko (the Reserve recruit / "Absent Engage" reminders; Reserve's own recruit is `recruitReserve`).
- **Blocked on an unbuilt timing (1):** Bayushi Wateru ("Absent Interrupt").
- **Straightforward with the flag and existing primitives (8):** Tactical Setback (enemy -2F), Indomitable Home (province +2),
  Siege Volley's Absent half (province bonus/penalty by Castles controlled; its Ranged 4 Attack half exists), The Way of the World
  (own Personality at home to the battlefield if he would be opposed, +2F if a Monk -- `own-personality-at-home` and
  `TryMoveToBattlefield` are exactly what it needs), Blighted Region (Defender-only, move a performer, conditional straighten and
  Honor), A Brave New World (a performer at any location plus a hand discard), Natsumi (a self-mover like Michi, plus "Tireless" and
  her attach restriction) and Yasuki Palaces - exp (its action; its production passive is separate).
- **Need one further small primitive (8):** Broken Alliance (variable Gold plus a cost-comparison target), Defensive Grill (a choice
  of destination battlefield, like Hawado's), Back to the Front ("assigned to this battlefield this turn" tracking), Flooded Pass (a
  Terrain played by an ability), Hantei's Guidance (a static "Absent for you" grant on the rulebook Favor action -- now a small
  override of the flag), Breaking the Rhythm (a Focus Effect and choosing which modifiers to negate), Recovering What Was Lost
  (Engage-timed), Searching for Answers (Look/search, with #328).

## Affected files

`src/L5R.Core/CardAbilities.cs` (the two flags, `DisciplineFromDiscard`, `TryMoveToBattlefield`, the deleted Banzai! hack, the
new target category and ability), `GoldQuotes.cs`, `PublishedContent.cs`, `CardCatalog.cs`, `L5R.Core.csproj`;
`tools/content/package_strategy_wave_18.py`, `content/published/strategy-wave-18*.json`,
`src/L5R.Client/Content/Published/oracle-l5r-10953.jpg`; `tests/L5R.Core.Tests/DisciplineAndAbsentTests.cs` (new, 14 tests),
`Program.cs`, `PublishedTests.cs` (pinned count 400); `docs/CARD_INVENTORY.xlsx`.
