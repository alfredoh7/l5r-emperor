# Wire-list cluster "pers-aftermath" (#674, personality-wave-132)

Status: accepted as engineering readings; the rules points under "Readings built in" are Mendon's to confirm or overrule.
Lane: rules (mdornbrook, standing cross-lane authority). Issue: https://github.com/alfredoh7/l5r-emperor/issues/674. Branch `claude/674-pers-aftermath`.

## What is wired

Seventeen Aftermath Personalities, each from its own Aftermath scan (`assets/reference/oracle/aftermath`, printing 1 image 1) and its printed text, packaged by `tools/content/package_personality_wave_132.py` and built on the existing mechanisms (`src/L5R.Core/WlPersAftermath.cs`, hook priority 14500, no feature state, so no `JsonPropertyOrder`). **No card is blocked.**

| Card | Printed ability | Built as |
|---|---|---|
| Akodo Uehara - exp (10850) | Limited: put a target Terrain from your discard pile into your hand; discard a card | `own-card-in-discard` narrowed to a Terrain in the Fate discard pile; the discard is the queued forced discard |
| Bayushi Dijuro (10867) | Siege Battle, Bow: Ranged 3 Attack; if it destroyed a card, give this Province +2 or -2 | two abilities sharing one use (raise / lower), one funnel call after the Attack |
| Bayushi Wateru (10870) | Ninja Interrupt: after you Recruit Wateru during battle, take an additional Battle action | an entering-play Reaction (Battle phase only) granting an additional action |
| Daidoji Tametaka - exp2 (10839) | Mantis ignore his Honor Requirement; Battle: three -1F tokens on an enemy Personality | `HonorRequirementWaiverClan` (Akodo Chiyo's) and three Force tokens of -1 |
| Daigotsu Chiboshi (10874) | Battle, Gold: equip a Follower to Chiboshi; additional Battle action | Hailung's attach-from-hand with Gyoken's grant |
| Daigotsu Endo (10875) | Limited, Gold: if Endo is dead, Recruit him | Kunji - exp's dead-pile recruit paying all costs |
| Ide Mutsuken (10885) | Open, Bow: the active player must choose and bow one of his unbowed Personalities | the decider choice, decided by the active player |
| Isidoros (10880) | After he enters play lose 2 Honor; Battle: straighten your target Follower | `EntryHonorLoss` 2 and `own-follower` (Katsuo's) |
| Kaiu Esumi - exp (10834) | Battle: destroy a target enemy attachment | Hida Komatsu's `komatsu-destroy-attachment`, as Chikara |
| Kitsu Tamao (10852) | Interrupt: after you Recruit Tamao, create a 2F/2C/3PH Lion Clan Ancestor | Kagako's entering-play creation (the published Lion Ancestor scan, oracle-l5r-9785) |
| Shiba Heirai (10865) | Battle: if opposed, discard a card for a Force bonus equal to its Focus Value | Akodo Regulars' discard-for-bonus gated on `Opposed` |
| Shinjo Sujikaro (10889) | Battle, Bow: Melee 3 Attack | Daigotsu Ryuko's shape |
| Susumu Takada (10879) | Invest 3: discard a card to gain Honor equal to its Focus Value | Clay Kshatriya's Invest window, a trait |
| The Broken Man (10866) | Invest 5: two +1F tokens; Earth Battle: bow a target enemy card with lower Force | Invest as above; a new in-file target category |
| Tsuruchi Shusaku (10858) | Repeatable Battle, Gold 2: +1F | `UsesPerTurn: Unlimited` with a fixed Gold cost |
| Usui (10884) | After he enters play lose 1 Honor; Earth Battle: this Province +2 strength | `EntryHonorLoss` 1; the current battlefield's province, until the turn ends |
| Yasuki Jiro (10836) | Political Limited, Gold 3: dishonor a target Personality | Slanderer's dishonor |

Scan checks: all seventeen scans exist and match the card (name, stats, Honor Requirement, Gold Cost, Focus, keywords). The scan wins over the catalogue in two places: Bayushi Wateru prints "Ninja Interrupt:" (the catalogue says "Ninja Absent Interrupt:") and Shiba Heirai prints "Phoenix Clan" (the catalogue lists a bare "Phoenix").

## Shared files touched (one line each)

- `CardAbilities.cs`: the single funnel every Attack resolves through (`ResolveAttackNow`) calls `AfterWlPersAftermathAttack` (Dijuro's "if this destroyed a card").
- `EmperorRulingR4.cs`: `TraitReactions.Is` also asks `WlPersAftermathAbilities.IsTrait` (Takada's and The Broken Man's Invest are traits, no "Reaction:" designator; the pinned `TraitReactions.All` list is unchanged, as for the Ivory Sword).
- `ShadowsEmbraceBatch4.cs`: Yasuki Jiro joins the curated `DishonorsTarget` list, so a Reaction that negates a dishonor (Social Grace) sees his action.

## Readings built in (not confirmed rulings)

- **"Interrupt: After you Recruit X" (Tamao, Wateru)** is the engine's optional entering-play Reaction window (`own-enters-play`), the shape Kagako's and Clay Kshatriya's printed Reactions use, not the Coils "before an action's effects resolve" Interrupt: the printed trigger is "After you Recruit". It is offered in the window any entry into play opens (a recruit from a province, a dead-pile recruit, an overlay), not only for a province recruit. A card that the engine puts into play by a search or an Equip window opens the same window; that is existing behaviour for every entering-play ability.
- **Wateru: "during battle"** is the Battle phase with a current battle. She is a Reserve card, so the usual recruit is the Reserve recruit in a battle; bought in the Dynasty phase she has no battle, so her Interrupt is refused. The grant is the ordinary additional Battle action (`GrantAdditionalAction(player, null)`), usable by any card of her controller.
- **Uehara: "Discard a card."** is a second effect after the Terrain is returned, not a cost, so it uses the queued forced discard (asked of the acting player as soon as the action resolves; an empty hand owes nothing). The returned Terrain is in the hand and may be the card discarded. This differs from Ikoma Shizuka's ruling (#368: the Terrain just found cannot be the discarded card): Uehara is a targeted return, not a search, and the engine's forced discard has no "other than" filter. Say if the same exclusion should apply.
- **Uehara: "a Terrain from your discard pile"** is a Strategy with the Terrain keyword in the acting player's Fate discard pile (a Terrain is a Strategy, as in Shizuka's search).
- **Dijuro: "+2 or -2"** is one printed choice made when the action is taken (two abilities sharing one use, the Togashi Hizumi and Siege Volley shape), not when the Attack resolves. "Destroyed a card" is Bayushi Higaonna's reading: the target was in play when the Attack resolved and is not now. "This Province" is the province at the current battlefield (the Defender's), until the turn ends (R-001). A Siege Battle label is a plain action keyword (Dijuro carries Siege himself).
- **Tametaka's tokens** are three tokens of -1F on the target (so a later removal of a token removes 1F), not a single -3F modifier; they last as tokens do, not only until the turn ends.
- **Chiboshi: "Equip a Follower to Chiboshi"** is from the acting player's hand, paying all costs (the Gold star), as Hailung's "Target a Follower in your hand: Attach it to Hailung". The additional Battle action is granted as the attach resolves.
- **Endo's Recruit** pays all costs and respects the Honor Requirement (he has none), enters at home, and draws (Destined); he is recruited from the dead pile as Kunji - exp is, so he is not offered while alive.
- **Mutsuken: "The active player must choose"** is the player whose turn it is, even when he is not the Personality's controller (the decider default for an unregistered spec); with a single unbowed Personality the choice is made for him, with none nothing happens. The Personalities that may be chosen are his own unbowed Personalities in play (Mutsuken, bowed as the cost, is not one when his controller is the active player). The bow is negatable by the same Stalwart rule as any bow.
- **Heirai: "If Heirai is opposed"** is the core-rules Opposed (an enemy unit at his battlefield) at the time the action is taken; the discarded card is the cost and its Focus Value is the bonus, until the turn ends.
- **Invest (Takada, The Broken Man)** is the card's own one-time entering-play payment with the printed fixed cost (cost reductions do not apply, core rules); it is a trait (not barred by "No players may take Reactions") per the R4 ruling on Invest.
- **The Broken Man: "an enemy card with lower Force"** is an enemy Personality or Follower in play whose Force is strictly lower than The Broken Man's current Force (his tokens count); Earth is a plain label (the card has Earth).
- **Shusaku: "Repeatable"** only lifts the once-a-turn limit; a Battle action still alternates priority with the opponent. Each use is paid.
- **Jiro: "Political Limited"** is a Limited action tagged Political, used in the Action phase; the dishonor is Slanderer's, and Social Grace sees it.

## Not covered

- Art: the seventeen scans are copied into `Content/Published` but are not in the catalogue art pack; a publisher step (`build_catalogue_art.py` and a release) is pending, as for every new wave.
- No decision is new: the choices reuse the generic decider, forced-discard, entry-reaction and effect UIs.
