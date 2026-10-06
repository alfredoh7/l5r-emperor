# P23: the ExperiencedCoM and Inexperienced forms of Experienced (issue #672)

Status: **implemented, readings awaiting Mendon**. Lane: rules. Requested by Mendon on 2026-10-06 under issue #672. This is primitive P23 of [the remaining-primitives plan](2026-10-05-remaining-unwired-primitives.md), built from [its prompt](2026-10-05-remaining-unwired-primitives-prompts.md).

## What changed

Coils of Madness prints two keyword forms the engine did not understand.

- **ExperiencedCoM.** Bayushi Kachiko and Isawa Tsuke print "Experienced" with a superscript "CoM". The catalogue spells it as the single word `ExperiencedCoM`. The engine read that as no Experienced keyword at all. It is now Experienced at level 1, so an "is this Personality Experienced?" check is true for these cards, and an effect that removes Experienced removes it.
- **Inexperienced.** The catalogue names these cards "X - inexperienced". The title comparison did not strip that suffix, so in catalogue-name contexts (the deck builder, the collection editor, the deck troubleshooter) "Hida O-Ushi - inexperienced" was not recognised as a version of "Hida O-Ushi". It is now. An Inexperienced card stays at level 0.

All of it is in `src/L5R.Core/Experienced.cs` (`Level`, `BaseTitle`, `DeckName`, and a new `IsInexperienced`). No other shared file was edited.

Deck names are unchanged for every card in the pool. In particular Bayushi Kachiko and Isawa Tsuke keep the deck names "Bayushi Kachiko" and "Isawa Tsuke" although they are now level 1; renaming them to "Bayushi Kachiko Experienced" would have made any saved deck that lists them fail to load.

## Cards wired (wave `personality-wave-128`, Coils of Madness scans)

| Card | Id | Abilities |
|---|---|---|
| Hida O-Ushi - inexperienced | oracle-l5r-10437 | `o-ushi-inexp-bow` |
| Tsuruchi - inexperienced | oracle-l5r-10469 | `tsuruchi-inexp-arrows`, `tsuruchi-inexp-arrow-attack` |

Both scans agree with the catalogue text. The rules are in `src/L5R.Core/ExperiencedVariants.cs`; the tests are `ExperiencedVariantsTests`.

**How Tsuruchi's Battle action plays.** The Attack's target is named when the action is announced, like every Attack. When the action resolves, the engine asks Tsuruchi's player how many Arrow tokens to destroy, offering one answer for each number from zero to all of them. The tokens are destroyed and the Ranged Attack is made at twice that number. With no Arrow token nothing is asked. This differs from Togashi Mitsu, whose command must carry the ids of the Fire tokens to destroy: no client can build that command from the offered actions, so Mitsu's Attack is in practice always strength 0 in the client. Tsuruchi uses the existing Either/Or decision, which the client already presents.

## Card left unwired

**Bayushi Nitoshi - exp (oracle-l5r-10480).** "After Nitoshi enters play, all players lose 3 Honor. Ninja Battle: Melee 6 Attack." The Melee 6 Attack exists. The entry trait does not: the engine's entry Honor loss (`EntryHonorLoss`) takes Honor from the card's controller only, and there is no feature hook for "after this card enters play" that a new file could register. Wiring him needs either an all-players form of the entry loss or an entry hook, both of which are edits to shared files outside this primitive. He is not published, so no printed ability is missing from a playable card.

## Readings for Mendon to confirm or correct

1. **ExperiencedCoM is Experienced level 1.** It behaves exactly like a plain "Experienced" keyword for the level rule.
2. **An ExperiencedCoM card may not overlay a plain Experienced card of the same title**, and the reverse is also refused, because both are level 1 and the rule needs a higher level. It may overlay a level 0 version, and an Experienced 2 version may overlay it. No card in the pool depends on this today: Kachiko and Tsuke have no other version in the catalogue.
3. **Inexperienced is level 0, the same level as the plain version.** So an Experienced version may overlay an Inexperienced one, and the Inexperienced one overlays nothing. It also means **the plain version may not overlay the Inexperienced version** (0 onto 0). The core rules document says nothing about Inexperienced. The catalogue records these cards as experience -1, which would instead let the plain version overlay the Inexperienced one. If that is the rule you want, it is a small change to `Experienced.Level` and its callers; say so and it will be made. Affected today: Bayushi Nitoshi (plain and Inexperienced are both published) and Border Keep (a Holding, so overlay does not apply).
4. **Deck construction treats the Inexperienced and plain versions as different cards.** A deck may hold one Unique "Bayushi Nitoshi" and one Unique "Bayushi Nitoshi - inexperienced", as it may hold one of each Experienced level. This was already the behaviour; it is recorded here because reading 3 puts both at level 0, and the core rule's exception is worded for "different experience levels". In play only one of them can be controlled at a time (Unique, same title).
5. **Hida O-Ushi's target** is any Personality in play whose Force is lower than hers when the action is announced: either player's, at home or elsewhere, bowed or not. "Lower Force" is her current Force against the target's current Force.
6. **An Arrow token carries no bonus.** Togashi Mitsu's Fire tokens print "+1F"; Tsuruchi's print nothing.
7. **"Until he has exactly five" gives nothing to a Tsuruchi who already has five or more Arrow tokens**; it never removes any. Other kinds of token on him are not counted.
8. **The number of Arrow tokens destroyed is chosen when the action resolves, not when it is announced.** Destroying them is an effect of the action (there is no colon making it a cost), so a Reaction to the action is played before its player commits to a number, and a negated action destroys none.
9. **The Attack's target is chosen at announcement, before the number.** The engine already rules that a target's Force never decides whether an Attack may target it, only whether the Attack destroys it, so the player may announce a target and then choose a number too small to destroy it.
10. **A Ranged Attack of strength 0 destroys nothing, even a card with 0 Force.** This is the engine's existing rule for every Attack (`ResolveAttackNow`), not something this change introduced; it is listed because "destroy zero tokens" makes a strength 0 Attack an ordinary play for Tsuruchi.
11. **Each use counts toward the three per turn whatever number is chosen**, including zero and including a use made with no token.

## Not done

- Bayushi Kachiko, Isawa Tsuke, Doji Hoturi, Hantei XVI, Kokujin and Iuchi Karasu (the other cards P23 unlocks) are not wired; the prompt named only O-Ushi and Tsuruchi as demonstration cards.
- The collection editor (`src/L5R.Client/MyCollectionEditor.cs`) labels a version by level: "Base" for level 0. Now that "X - inexperienced" groups with "X", a title with both shows two buttons labelled "Base". `Experienced.IsInexperienced` is there for the client lane to tell them apart; the client file was not edited here.
- Keywords such as "Experienced 3.5", "Experienced The Student" and "Soul of X Experienced" are still level 0, as before. They are outside P23.
- The Godot `.jpg.import` sidecars for the two new scans, the art pack and the inventory workbook are left to the integration step.
