# Kokujin, P'an Ku, Zansho, Chiyurei's Axe and the Koan cards (issue #827)

Status: **accepted** (Mendon's rulings of 2026-10-11, recorded as R-049 to R-053). Lane: rules, with a decision screen the client already renders. Engine: `src/L5R.Core/Wave827.cs`; waves `personality-wave-827` and `item-wave-827` (`tools/content/package_wave_827.py`).

| Card | Wired as |
|---|---|
| Kokujin - inexperienced (oracle-l5r-10486) | `w827-kokujin-duel` (a Battle duel) and an entry Honor loss; the sacrifice question is `w827KokujinChoice`, opened from `ResolveDuel` |
| P'an Ku - exp (oracle-l5r-10497) | `w827-panku-madness` (Open, bow) and `w827-panku-take` (Limited, bow); a `ControlReturn` with `RemainBowed` and `NoSeppuku` |
| Zansho - exp (oracle-l5r-10505) | `w827-zansho-ranged`: the bowed Follower is the cost (`CostSubject.Chosen`); Effect `will-not-join-spider-player` |
| Chiyurei's Axe (oracle-l5r-10340) | `AttachRequiresName`, an Equip cost term (-2 for Hida Chiyurei), `AttackComparedValue` |
| Koan's Jingasa (oracle-l5r-10524) | `AttachGrantsKeyword` Shugenja, the Spell limit (`Wave827KoanExcess`), the odd-Focus bow waiver (P24) |
| Koan's Robes (oracle-l5r-10525) | a `PersonalityForce` term, the even-Focus bow waiver (P24), a `WatchScope.Unit` destruction watch (P7) |

Not wired: Ningen's 'Treatise on the Nezumi' (allies are out of scope). Hida Chiyurei (vanilla-wave-1) and plain Zansho (personality-wave-58) were already published.

## What Mendon ruled (2026-10-11)

See R-049 to R-053 in `docs/rules/RULINGS.md` for the wording. In short: Kokujin's question after a won duel (a list of every Personality the controller controls, clickable on the table or in a list that highlights both ways; not negatable or reactable); P'an Ku's optional straightening and the control link with a seppuku bar; Zansho bows his own Follower; the Axe compares the lower of current and printed Force with no choice; the Koan keyword count sets the Spell limit and the Robes' bonus, and the Robes protect the whole unit.

## What was built

- **The sacrifice question** is a pending decision (`w827KokujinChoice`) whose actions the engine builds: one per Personality (`DecisionCandidateIds` carries the card), plus "do not destroy". The client's generic decision tray already shows candidate cards with faces, lets a click on the table card select its entry, and highlights table card and entry both ways (`TableLink`); no client change was needed. `ResolveDuel` asks once per duel and runs again with the answer, as Awed Witness does.
- **P'an Ku's link** extends the P24 `ControlReturn` (SourceStraightens) with `RemainBowed` and `NoSeppuku`. The Straighten Phase adds him to `EligibleForOptionalStraighten`; an effect's straightening is intercepted in `Straighten` (queued) and offered after the command (`w827PanKuStraighten`). `SeppukuBarredByControl` hides and refuses Order Seppuku for the taken Personality.
- **Koan**: `Wave827AfterCommand` (run after every command) opens `w827KoanDiscard` while a Personality carrying a Jingasa has more Spells than Koan cards attached; `DiscardAttachmentFromPlay` discards the chosen Spell (it is discarded, not destroyed).
- **The unit watch**: `WatchScope.Unit` covers the Personality the carrier is attached to and every card attached to him.
- **The bot** answers the three decisions (`TrainingOpponent`): it gives up a Personality only for a loser worth at least one point more (Force plus Personal Honor), keeps P'an Ku bowed, and discards the first listed Spell.

## Readings for Mendon to confirm

1. **The bow waivers keep the printed parity** (confirmed by Mendon, 2026-10-11: his "satisfies the cost" wording was too broad). The Jingasa waives the bow of odd-Focus Spells, the Robes of even-Focus Spells (0 is even), through the P24 waiver.
2. **Koan cards count every Koan-keyword card in their unit, themselves included** (Mendon, 2026-10-11, after the first build): the Robes' bonus and the Jingasa's Spell limit both use that count (the Personality's own keyword too, if it has one).
3. **Koan enforcement timing.** The Spell limit is checked after every command, never mid-action; the controller chooses which Spell to discard, one at a time, until within the limit.
4. **P'an Ku's choice exists only while he holds a Personality.** With nothing taken he straightens as any card does. The "unless the straightening may not be negated" exception is the engine's existing unnegatable-action test. An effect's straightening is held until its command finishes (it is not applied mid-action); the Straighten Phase uses the `declineStraighten` screen.
5. **P'an Ku's target is another player's Personality** with more Madness tokens than printed Personal Honor; the tokens are removed only if control was taken. If control is refused (Loyal, a duplicate Unique) nothing else happens.
6. **Kokujin.** A tie has no winner and nobody is asked: both Personalities are destroyed. If the loser cannot be controlled by the winner (Loyal), nobody is asked and the loser is destroyed. If the chosen Personality's destruction is negated, control is not taken and the loser is spared, as nothing "to take control" was paid; if control is then refused (a duplicate Unique card) the loser is also spared.
7. **Zansho's Follower** is chosen with the action (one offered action per unbowed Follower attached to him); its Force is read as it is bowed. The Arakan bonus is judged by the target's name.
8. **The Axe's comparison** applies to any Melee Attack whose source card is Chiyurei, or an Item, Spell or Follower attached to him. A Personality with Followers is compared by its current Force as every Attack is.
9. **Spider Clan** is judged on the player's Clan when Kokujin enters play.

## Not covered by a test

- The client's rendering of the three new decisions on a native display (generic decision tray only; no new screen).
- Kokujin's question when the loser has a Reaction that moves it away mid-duel (the duel's own windows close before the question).
- P'an Ku's effect-straighten offer when another pending decision is open at the end of the command (it waits until none is).
