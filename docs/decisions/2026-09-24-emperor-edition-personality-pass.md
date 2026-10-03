# Emperor Edition Personality pass: every unwired Personality re-checked against the engine (issue #477)

Requested by Mendon: evaluate all 78 unwired Personalities of the Emperor Edition set against `docs/rules/EMPEROR_EDITION_CORE_RULES.md` and the engine as it now stands, wire everything that fits within existing or new primitives that do not conflict with the core rules, and record a specific blocker for the rest.

**71 of the 78 are now wired** (waves 68-73, six parallel batches merged into one branch); **7 stay unwired**, each with a specific blocker in `tools/content/build_card_inventory.py` (`BLOCKERS`) and therefore in `docs/CARD_INVENTORY.xlsx` (2172 cards, 938 wired, 867 before this pass). All 78 scans exist locally, so no card was blocked by a missing scan of its own; the seven blocked cards are blocked by proxy scans or by engine structure (below).

| Wave | Batch | Wired | Cards |
|---|---|---|---|
| personality-wave-68 | 1 (Shadowlands, Spider) | 11 / 13 | Chuda Niiro, Daigotsu Aya, Gyoken, Matsuda, Misaki, Negishi, Goju Oyoto, Ekichu no Oni, Ninube Onchi, Nosloc no Oni, Ugulu no Oni |
| personality-wave-69 | 2 (Crab, Lion) | 12 / 13 | Hida Horu, Kisada (Emperor), Komatsu, Takeuchi, Watari, Kaiu Esumi, Hideaki, Kuni Shinoda, Toritaka Shishido, Ikoma Shinju, Matsu Hana, Matsu Yuuto |
| personality-wave-70 | 3 (Unicorn, Mantis) | 12 / 15 | Moto Naleesh, Shigeru, Tetsuo, Shinjo Itao, Utaku Ryoko, Iuchi Shunshi, Tsuruchi Samuru, Tomaru, Unmei, Yoritomo Hama, Hiromi, Naoto |
| personality-wave-71 | 4 (Crane, Scorpion) | 15 / 16 | Asahina Keigo, Kitiaru, Michiru, Kakita Genshi, Seishi, Tadanobu, Doji Makoto, Bayushi Hurunayi, Mitsuo, Nitoshi, Rentatsu, Waru, Shosuro Kameyoi, Tanzaki, Soshi Yorimi |
| personality-wave-72 | 5 (Phoenix, Dragon) | 11 / 11 | Agasha Ryo, Asako Nobunori, Tsunefusa, Yorisada, Isawa Kimi - exp 2, Norimichi, Shiba Kataken, Sansesuke, Tsukimi - exp 4, Kitsu Tamasine, Tamori Kusugi |
| personality-wave-73 | 6 (others) | 10 / 10 | Kitsuki Yataku, Nishimura, Genmyo, Qalyar, Seppun Washi, Yasuki Jekku, Yasuki Tanimura, Yung, Daigotsu Ishibashi, Daigotsu Kanpeki - exp |

## Not wired (7)

- **Goju Kobashi** and **Suiteiru no Oni** (batch 1): each creates a Proxy Follower whose scan is not available locally (Kobashi's Spawn, oracle-l5r-10008; Suiteiru's Podling, 10021). Kobashi's "consumes it" is also not defined in the core rules; Suiteiru needs a created count taken from the destroyed Personality's Chi, a variable number of attach hosts and Honor loss equal to the number actually created.
- **Akodo Makotai** (batch 2): creates the plain Infantry Follower Proxy (oracle-l5r-9738), which is neither published nor scanned locally.
- **Shinjo Ki-Chang - exp**, **Kitsune Denhei**, **Kitsune Gina** (batch 3): each creates a Proxy whose scan is missing (Dutiful Cavalry 10003, Denhei's Snake 10002, Gina's Fox 10005). Denhei also needs a "base abilities may not be used" lock with an undefined "bitten" duration; Gina needs a battlefield-scoped -1F trait on the created card.
- **Shosuro Rokuta** (batch 4): creates Rokuta's Apprentice (Proxy 10013), scan missing; the "trait" half of his trigger has no destroying trait in the engine to hook.

These seven are the next scan-fetch candidates; each is mechanically wirable once its Proxy scan is available (recorded in its note).

## New primitives and engine changes

Each batch's code is in its own `EmperorPersonalityBatchN.cs` with small marked hooks in shared files.

- **Batch 1:** an entering-play window for out-of-play targets (Aya's Dark Virtue retrieval); a follower Gold discount term in `GoldQuotes.EquipGoldCost` (Misaki); a wildcard ability lock key `"*"` (Onchi's "remove abilities" -- every action on the target is refused until the turn ends); Ugulu's negation of a Human's movement to a battlefield (hooked into `MoveToBattlefield`).
- **Batch 2:** attach-from-hand recorded on the action that made it (Takeuchi); a zero floor on a province's strength; Horu's Force withheld from resolution while his army has two or more units; Shishido's +1 to Ranged Attacks performed by his army; Shinoda's paired move; Hideaki's temporary province strength.
- **Batch 3:** an Attack compared against Gold Cost instead of Force (Samuru); a live keyword mirror (Unmei); "not negated or delayed" for Hiromi's Battle actions; a battle-long ability lock (Naoto); entering-play Reactions that target a card in hand (Itao).
- **Batch 4:** an alternate Gold source whose amount is the Focus Value of a named hand card (`GameCommand.PaymentDiscards`, **a shared server/client wire field** -- server test and client mapping updated); Genshi's Either/Or chained into an ability lock with its own expiry; Rentatsu's held-bowed state and turn-start Honor choice; moves an action makes recorded for after-action Reactions (Mitsuo); a twice-per-turn waiver of bow costs (Yorimi).
- **Batch 5:** Ryo's face-up refill; Nobunori's base-Force comparison; Kimi's element-sharing bonus and the deciding-player discard; the after-action window for cards put into another hand; Sansesuke/Norimichi's redirect and negation; Tsukimi's twice-per-battle moves; Tamasine's created Ancestor and conditional multi-target boost; Kusugi's +/-4 province strength.
- **Batch 6:** a no-refuse entry on a held action's negation list (Yataku); an optional up-front hand-discard slot (Nishimura); Tanimura's alternate Gold source (`PaymentActionsOnly`, `PaymentBows`) and Either/Or "pay 4 Gold or lose 3 Honor"; a Ring-enters-play search (Yung); Kanpeki's Strategy-performed after-action trigger and Force-comparison target; Ishibashi's shown Dark Virtue as the strength source.

Merge notes: the batches were merged into one branch and their shared-file conflicts resolved by hand (chains of `or "id"` target lists, the catalogue dictionary, the `AbilityDefinition` record, `AttackComparedValue`/`AttackComparedForce`, Gold-source and Either/Or switches); the full core suite is 0 failures.

## Readings and assumptions (Mendon to confirm or correct)

**Batch 1**
1. Onchi: "abilities" means the target's actions only; static traits stay. Attached Items' and Followers' abilities are theirs.
2. Aya: "you may put a Dark Virtue" is an optional entering-play window; the Honor loss stays mandatory and runs first. "Give Aya Cavalry, Duelist, or Tactician" lasts until the end of the turn, one choice per turn.
3. Ugulu covers movement effects only (not army assignment), for either player's Humans, including his own controller's.
4. Misaki's discount applies to any Follower attach, not only from hand; a Follower whose Honor Requirement she does not meet is still not a legal attach.
5. Oyoto and Onchi are packaged with Clan `Ninja` (the packager takes the first listed clan of "Ninja, Spider").

**Batch 2**
6. Esumi: the +2/-2 is chosen when the action is announced (two catalog entries sharing one use), applying whenever the Attack targeted a card.
7. Horu: only his own Force is withheld, not his Followers'. Hana: Items are skipped; a dishonorable Hana is rehonored instead of gaining Honor. Hideaki: the +3 drops at the top of resolution, before "before resolution" effects.
8. Takeuchi's hand-attach record is set at Equip, the attach-from-discard ability, Reserve attach and Singh Remnants; only Equip is tested end to end. Kisada's redirect fires only for another player's Battle action. Shinju's enemy card is the primary target.

**Batch 3**
9. Tetsuo follows the printed scan ("If you control any Cavalry units at the current battlefield: Move Tetsuo there. If he moved, Ranged 4 Attack"), not the differently worded Oracle text.
10. Naleesh's "overruns it" is flavour; the bow applies to the whole unit. Hama's "lower Gold Cost" compares against Hama's own Gold Cost. Hiromi's unnegatable actions also cover whole-action negation. Shigeru's "a Limited action targets your Personality" includes your own; "now 0" means 0 or less. Naoto's lock covers every ability of each card in the unit, Reactions included, until the battle ends. Shunshi's and Samuru's "Soul of ... Experienced" keyword is one printed keyword, left unsplit.

**Batch 4**
11. Kitiaru copies for the turn; "Political" means an ability tagged Political. Michiru covers another player's action or Attack destroying the Personality, not battle casualties; one shot.
12. Genshi: the lock lasts until his controller's next turn begins; a dishonorable Genshi is rehonored instead of gaining the Honor; a target with one ability is locked without a choice. Makoto's "challenges" is treated as unrefusable, like "duels". Nitoshi: "a different one of your Personalities" means different from the target, so Nitoshi himself is a legal victim; "a card without attachments in a unit" is a Follower or a bare Personality of either player.
13. Rentatsu: "negate his straightening" is offered only in his controller's Straighten Phase; the Honor-loss choice comes when the turn begins, before straightening; the effect ends once he straightens. Waru is two abilities (Battle and Limited) sharing one use. Kameyoi: the Honor loss is judged before he is dishonored, using printed base Personal Honor.
14. Yorimi: the "may" is applied automatically to the first two eligible bow costs each turn (declared costs only). Keigo: the discarded card cannot be the card being paid for. Tadanobu: the duel-won window opens even when his controller has no Favor.

**Batch 5**
15. Ryo turns the province's existing face-down refill face-up (recruits from Purchase or Reserve). Nobunori's "may compare against base Force" is applied automatically as the lower of current and base Force. Tsunefusa's Open lasts until end of turn. Yorisada's bow/straighten are two entries sharing one use.
16. Kimi: "Kimi's location" means the same battlefield or both at home; element keywords are Air, Earth, Fire, Water, Void, from her or her attached Spells. Sansesuke's "an action" is not limited to another player's; legality is judged after he is bowed. Tsukimi's Reaction is a Reaction, not marked "Absent"; each half needs a real change of location. Tamasine's "since your last turn began" clears when that player's turn starts and is set only from an Action Phase.

**Batch 6**
17. Yataku is offered only against an action that targets a Samurai and would create a refusable challenge (currently Doji Shigeyuki's duel). Nishimura's optional discard is chosen up front with the target. Genmyo copies until end of turn and only from another Personality of yours.
18. Tanimura's Gold counts Equip, Strategy plays and ability actions as "an action" but not a recruit; it does not bow him and is once per turn. His Political Reaction applies to any Battle action by another player, including Ranged and Melee Attacks; "the player" is the acting player. Yung treats "a Ring enters play" as one of your Rings' `*-recruit` actions resolving. Kanpeki counts a Battle/Open Strategy action taken in the Battle phase as a Battle action (Byung's existing wiring is stricter). Ishibashi's shown card stays in hand.

## Verification

- Full core suite: 0 failures (1383 passes) after merging all six batches; each batch's own suite (`emperor-personality-batch1..6`) passes.
- Pinned lists updated where the new cards legitimately extend them, each with an "issue #477" comment: Recon-tagged actions, Naval carriers, "absent" moves, player-target inventory, once-per-turn limit exemptions, Gold-through-an-ability list, and the attack-performer audit (Daigotsu Misaki names her performing Follower).
- `docs/CARD_INVENTORY.xlsx` regenerated: 2172 cards, 938 wired (+71).
- Catalogue art pack pinned to `catalogue-art-pr477-20260924-r1` (3385 images, incremental over `catalogue-art-pr469-20260924-r2`).
