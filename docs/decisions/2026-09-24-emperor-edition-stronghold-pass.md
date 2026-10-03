# Emperor Edition Stronghold pass: every unwired Stronghold re-checked against the engine (issue #486)

Requested by Mendon: evaluate all 24 unwired Strongholds of the Emperor Edition set against `docs/rules/EMPEROR_EDITION_CORE_RULES.md` and the engine as it now stands, wire everything that fits within existing or new primitives that do not conflict with the core rules, and record a specific blocker for the rest.

**23 of the 24 are wired** (waves 17-19, three parallel batches merged into one branch, plus the Plain of Gold proxy that The Golden Plains creates), and **Halls of the Forgotten stays unwired on Mendon's instruction**: a number of cards delay effects, actions or even a phase, and they will be built together as one delay mechanism rather than card by card. Its blocker note is in `tools/content/build_card_inventory.py` (`BLOCKERS`) and therefore in `docs/CARD_INVENTORY.xlsx` (2174 cards, 1072 wired, up 24). All 24 scans exist locally; the Plain of Gold scan was supplied by Mendon on 2026-09-24 (a corrected 429x600 PNG of the real card, converted to WebP, not resized; recorded as a `working_copy` block on its receipt, with the master hash and source URL untouched).

| Wave | Batch | Wired | Cards |
|---|---|---|---|
| stronghold-wave-17 | 1 | 7 / 8 | Eternal Victory Dojo, Hidden Falls Dojo, Keep of the Dead, Koshin Keep, Kyuden Otomo - exp, Library of Rebirth, Midday Shadow Court |
| stronghold-wave-18 | 2 | 8 / 8 + proxy | Palace of the Breaking Dawn, Pillars of Virtue, Shiro Chugo, Steel Soul Dojo, Suitengu's Torch, Temple of Purity, The Aerie, The Golden Plains, and the Plain of Gold (created-only Terrain) |
| stronghold-wave-19 | 3 | 8 / 8 | The Khan's Estate, The Otoro Estate, The Shadow's Lair, The Spider's Web, The Temple of Death - exp, Watchful Eye Dojo, The Waystation of the Path, Yasuki Palaces - exp |

## Rulings applied during the pass

- **The Waystation of the Path**: "The Henshin is reborn" is flavor text with no rules effect (Mendon). The mechanics wired are the printed ones: after another player's action targets your performing Henshin, target another of your Personalities; transfer his attachments and tokens; the action's effects that would destroy the Henshin instead overlay him onto that Personality.
- **Halls of the Forgotten**: left unwired on purpose (see above); the general "delay effects / actions / a phase" mechanism is to be designed jointly.
- **Attack targeting** (Mendon): the core rule is correct -- a Ranged or Melee Attack targets a Follower or a Personality without Followers in the enemy army and destroys it only if its Force is at or below the strength, and an Attack may also be produced with no target selected. The engine currently only lets an Attack target a card whose Force is at or below the strength. That discrepancy (found by batch 1 while wiring Koshin Keep, whose effects apply to a target that survives) is fixed as its own change, not in this pass.

## New primitives and engine changes

- **Batch 1:** a deck-construction rule carried by a Stronghold (`DeckWorkshop.Review` accepts four copies each of the two most-copied non-Unique Battle Strategies -- a soft warning like the copy limit); a Gold source usable even while bowed and without bowing, for a payment in a given phase (`PaymentWhen`, `PaymentEvenIfBowed`; Library of Rebirth); a delayed draw at the next End Phase (`PendingEndPhaseDraws`); a restricted extra action (`AdditionalActionGrant.RangedTargetId`, Koshin Keep); Favor tokens on Kyuden Otomo - exp (every action that discards the Imperial Favor as a cost also offers a Favor token instead; every way of taking the Favor goes through `TakeImperialFavor`); a province-set target (`GameCommand.Provinces`, **a shared server/client wire field** -- wire record, session mapping and server test updated); an additional Infantry Maneuvers Segment (Hidden Falls Dojo); a new watch kind for Midday Shadow Court.
- **Batch 2:** the announce window also opens for Thunder Battle actions; a per-action "unnegatable and undelayable" key feeding the same checks Hiromi's trait uses; a per-action Honor gain/loss reduction (Shiro Chugo); a Ronin entry discount and Honor Requirement waiver (Palace of the Breaking Dawn); the Aerie's duel outcome; a `own-terrain-in-hand` target and a Terrain play that spends a scoped additional-action grant (Plain of Gold); creation of the Plain of Gold even when a Terrain stands there is skipped, leaving only the Reconnaissance (see readings).
- **Batch 3:** an attack-assignment movement lock (`MovementKinds.AssignAttack`, The Khan's Estate); a private province look (`MatchState.ProvinceSights` / `MatchView.ProvinceSights` -- the looker sees province index and definition only, never a card id; **a new field in the shared match view** that the client does not display yet); the Weapon-attach-on-entry Reaction trigger `own-personality-enters-play` (The Spider's Web); a `ProductionCondition.PayingForPersonality` (Yasuki Palaces); a replacement-of-destroy check in `NegatesEffect("destroy")` and a Henshin overlay onto a differently titled Personality (The Waystation, without weakening the same-title Experienced checks).

Merge notes: the three batches were merged into one branch and their shared-file conflicts resolved by hand (Honor Requirement waivers combined from Keep of the Dead and Palace of the Breaking Dawn, the catalogue extension chain, the after-attack hooks, the Recon and use-limit pinned lists, and the attack-performer audit list, where The Otoro Estate's two attacks name their performing Ninja).

## Readings and assumptions (Mendon to confirm or correct)

**Batch 1**
1. Keep of the Dead: "or he assigned there" is read like Charge Into Danger: move him if enemy units are there, or he is already at the battlefield; a Commander already there does not move, so nothing straightens.
2. Hidden Falls Dojo: the scan prints no "this turn", so the effect is read as this turn only; targetable provinces are the Defender's; Reconnaissance is now cleared at turn end; Cavalry Scouts cannot be assigned (the extra segment is an Infantry one); both players may hold it.
3. Koshin Keep: the Force reduction lasts until the turn ends; the additional Ranged Attack must come from a different card (each card's ability is once per turn). Its effects only fire on a target that survives, which the Attack-targeting correction above makes reachable.
4. Kyuden Otomo - exp: the after-you-take-the-Favor trait is an optional Reaction in the after-action window, usable while bowed and unlimited per turn; the Favor-token substitution is a separate offered option, the Imperial Favor stays the default.
5. Library of Rebirth: "paying for a Spell" means equipping or attaching one, not a Spell's own action; an unbowed Library uses its Reaction first and stays unbowed.
6. Midday Shadow Court: "an action he performed ends" fires as the action's resolution is finished; Lobby counts as performed by the Personality it bows.
7. Eternal Victory Dojo: the Reaction is once per turn by default; the draw comes as the End Phase begins, before the ordinary End Phase draw.

**Batch 2**
8. Shiro Chugo's Reaction window opens only when the action's printed label mentions an Honor gain or loss (or the action is in the existing Honor-loss list) and you have an unbowed Courtier; it covers an action's synchronous effects, its Attack and its duel outcome; Honor paid as a cost is not distinguished from an effect.
9. Temple of Purity's "will not be negated" covers the -4F and, because that penalty is the action's own effect, the whole action against a whole-action negation (Hiromi's reading). The Aerie and Temple of Purity read "another player's Personality" as an enemy Personality (as Duel of Haiku does).
10. Suitengu's Torch has the Thunder keyword, so its own Battle action counts as a Thunder Battle action and triggers its own Reaction window; that Reaction is itself an action an untargeted negator can negate.
11. Palace of the Breaking Dawn follows the printed scan ("1 less Gold, ignoring Honor Requirements"; a Battle action with no Tireless keyword), not the catalogue text; no published Ronin Follower has an Honor Requirement above 0, so that waiver path has no test.
12. The Golden Plains: "Put it into play there" is part of the Stronghold's Reaction (the Plain is put into play, not played); if a Terrain already stands at that battlefield only the Reconnaissance half happens (the one-Terrain-per-battlefield rule leaves no room). The Plain of Gold's own action just grants an additional Battle action to play the Terrain; the grant is scoped, like the Ring recruits, and playing any other action forfeits it.

**Batch 3**
13. The Otoro Estate: "your Ninja" means the performing Ninja, and he goes home whether or not the Attack hit; the Ninja Open look is announced publicly (a look happened) but reveals nothing to the opponent.
14. Watchful Eye Dojo: "dishonor him" is the refuser (the target), and the Honor goes to the acting player. The Shadow's Lair: a unit's Force is the Personality plus his Followers; "once per battle" uses a per-battle stamp like Castle of Order.
15. The Spider's Web: the Kensai discount is forced while available, so the full-price ability is refused for a Kensai until the discount is spent; a window opens only if a Weapon is in hand; it is a trait, so not once-per-turn and usable while the stronghold is bowed.
16. Yasuki Palaces: "paying for a Personality" means recruiting only. The Khan's Estate: the once-per-Tactician-per-turn Tactical cap is not enforced, matching the existing Tactical cards.
17. The Waystation of the Path: "your performing Henshin" is read as the Henshin the other player's action targets, treated as the Reaction's performer (offered whenever another player's action targets it, destroying or not); the stronghold's ordinary bowed restriction applies; an attachment that may not attach to the other Personality stays with the Henshin; if the other Personality has left play when the destruction would happen, nothing is substituted; the transfer is immediate, so a non-destroying action leaves the Henshin bare where he stands (this last case is not covered by a test).

## Verification

- Full core suite: 0 failures (1534 passes) after merging all three batches; each batch's own suite (`emperor-stronghold-batch1..3`) passes.
- Pinned lists updated where the new cards legitimately extend them, each with an "issue #486" comment: Recon-tagged actions, "absent" moves, use-limit exemptions, Seppuku pending states, Gold-through-an-ability, the attack-performer audit list.
- `docs/CARD_INVENTORY.xlsx` regenerated: 2174 cards, 1072 wired.
- Catalogue art pack pinned to `catalogue-art-pr486-20260924-r1` (3519 images).
