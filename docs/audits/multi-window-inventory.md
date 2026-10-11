# Multi-window card inventory (issue #830)

Every wired card with two or more action windows: several printed actions, Battle/Open, Battle/Limited (modelled as a Battle + a Limited ability sharing one use), Open + Battle, Reaction + Battle, and so on.

## Method

1. **Generic probe** (`MultiWindowAuditTests`, runs in CI): a kitchen-sink table is built per card and every Battle / Open / Limited / Battle-Open ability is probed in four windows (Battle as attacker, Battle as defender, Action phase as active player, Action phase as the other player). An accepted offer is also executed. Invariant asserted: **an action is never enabled outside the windows its timing allows.** Window expectations: Battle = both battle windows; Open = both Action windows; Limited = Action phase, active player; Battle/Open = all four.
2. **Hand-built scenarios** (`MultiWindowAudit1-6Tests`): abilities whose own preconditions the generic table cannot meet (Imperial Favor, Rings, Kata, tokens, cards under a Holding, discard-pile sources, ...) each have a scenario proving the effect in every allowed window and refusal in every other.
3. **Reaction windows** (`MultiWindowReaction1-4Tests`): each Reaction of a multi-window card is opened through normal play, offered to the right player only, accepted with its effect checked, and shown not offered outside its window.

## Defects found and fixed

- *All Within the World*: a Ring target was refused in every Battle window (the at-battlefield target rule hit a Ring at home).
- *Strength of the Divine* (both abilities) and *Visage of the Orochi*: performer category had no listing case, so the action was only ever listed disabled.
- *Strong Defenses* (Battle boost): same missing listing case; could never be offered to a client or the AI.
- *Shosuro Hawado - exp*: the after-Battle-action Reactions were listed enabled while he was home, then refused on execution; now listed disabled with the reason.

## Blockers

None. No ability was left unverified.

## Battle / Open / Limited abilities

| Card | Type | Ability | Timing | Verified by |
|---|---|---|---|---|
| A Close Call | Strategy | `a-close-call-boost` | Battle | generic probe |
| A Forefather's Vengeance | Strategy | `forefather-destroy` | Battle | generic probe |
| A Forefather's Vengeance | Strategy | `forefather-honor` | Limited | generic probe |
| A Glimpse of Fate | Strategy | `pe-glimpse-of-fate-draw` | BattleOrOpen | generic probe |
| A Paragon's Strength | Strategy | `paragon-strength-attack` | Battle | generic probe |
| A Paragon's Strength | Strategy | `paragon-strength-move` | Battle | generic probe |
| A Pure Stroke | Strategy | `pure-stroke-raise` | BattleOrOpen | generic probe |
| A Quiet Transaction | Strategy | `quiet-transaction-lock` | Open | hand-built scenario |
| A Quiet Transaction | Strategy | `quiet-transaction-tokens` | Open | hand-built scenario |
| A Time for Mortal Men | Strategy | `mortal-men-boost` | BattleOrOpen | generic probe |
| A Yojimbo's Duty | Strategy | `yojimbos-duty-recruit` | Open | generic probe |
| A Yojimbo's Duty | Strategy | `yojimbos-duty-straighten` | BattleOrOpen | generic probe |
| Advance Warning | Strategy | `advance-warning-lower` | Battle | generic probe |
| Advance Warning | Strategy | `advance-warning-raise` | Battle | generic probe |
| Agasha Kurou | Personality | `kurou-attach-spell` | BattleOrOpen | generic probe |
| Akodo Dairuko | Personality | `dairuko-melee` | Battle | generic probe |
| Akodo Furu | Personality | `furu-bow` | Battle | generic probe |
| Akodo Furu | Personality | `furu-straighten` | Battle | generic probe |
| Akodo Kano | Personality | `kano-destroy-token` | Battle | hand-built scenario |
| Akodo Kano | Personality | `kano-token` | Limited | generic probe |
| Akodo Tezuka | Personality | `tezuka-grant` | BattleOrOpen | hand-built scenario |
| Akodo Tsudoken | Personality | `tsudoken-exp-bow` | Battle | generic probe |
| Akodo Tsudoken | Personality | `tsudoken-exp-straighten` | Battle | generic probe |
| Akodo Tsudoken | Personality | `tsudoken-exp2-straighten` | BattleOrOpen | generic probe |
| All Within the World | Spell | `promo-d-all-within-ring` | BattleOrOpen | hand-built scenario |
| All Within the World | Spell | `promo-d-all-within-self` | BattleOrOpen | hand-built scenario |
| Ambush Tactics | Strategy | `wl-ambush-attack` | Battle | generic probe |
| Ambush Tactics | Strategy | `wl-ambush-attack-move` | Battle | generic probe |
| Asahina House Guard | Follower | `asahina-straighten` | BattleOrOpen | generic probe |
| Asahina Munefusa | Personality | `munefusa-move` | Battle | generic probe |
| Asahina Munefusa | Personality | `munefusa-straighten` | Open | hand-built scenario |
| Asahina Nanae | Personality | `wlpt-nanae-jade` | BattleOrOpen | generic probe |
| Asako Chukage | Personality | `chukage-draw` | Limited | hand-built scenario |
| Asako Chukage | Personality | `chukage-move` | Battle | generic probe |
| Asako Hayate | Personality | `hayate-cavalry` | Open | generic probe |
| Asako Hayate | Personality | `hayate-naval` | Open | generic probe |
| Asako Hayate | Personality | `hayate-tactician` | Open | generic probe |
| Asako Heiwa | Personality | `heiwa-bonus` | BattleOrOpen | generic probe |
| Asako Heiwa | Personality | `heiwa-penalty` | BattleOrOpen | generic probe |
| Asako House Guard | Follower | `asako-house-guard-force-chi` | BattleOrOpen | generic probe |
| Asako House Guard | Follower | `asako-house-guard-force-honor` | BattleOrOpen | generic probe |
| Asako Izuna | Personality | `noboru-exp2-favor` | Limited | generic probe |
| Asako Izuna | Personality | `wlpt-izuna-bow-battle` | Battle | generic probe |
| Asako Izuna | Personality | `wlpt-izuna-bow-limited` | Limited | generic probe |
| Asako Jirou | Personality | `jirou-kolat` | Open | generic probe |
| Asako Jirou | Personality | `jirou-ninja` | Open | generic probe |
| Asako Jirou | Personality | `jirou-shadowlands` | Open | generic probe |
| Asako Jukko | Personality | `jukko-switch` | BattleOrOpen | generic probe |
| Asako Karachu | Personality | `wl1-karachu-cavalry` | Open | generic probe |
| Asako Karachu | Personality | `wl1-karachu-conqueror` | Open | generic probe |
| Asako Karachu | Personality | `wl1-karachu-naval` | Open | generic probe |
| Asako Tsunefusa | Personality | `tsunefusa-bow` | Battle | generic probe |
| Asako Tsunefusa | Personality | `tsunefusa-kolat` | Open | generic probe |
| Asako Tsunefusa | Personality | `tsunefusa-ninja` | Open | generic probe |
| Asako Tsunefusa | Personality | `tsunefusa-shadowlands` | Open | generic probe |
| Asako Usohara | Personality | `w739-usohara-ring` | Limited | hand-built scenario |
| Asako Usohara | Personality | `w739-usohara-search` | Limited | generic probe |
| Asako Yorisada | Personality | `yorisada-bow` | Battle | generic probe |
| Asako Yorisada | Personality | `yorisada-straighten` | Battle | generic probe |
| Ascending the Ranks | Strategy | `ascending-ranks-battle` | Battle | generic probe |
| Ascending the Ranks | Strategy | `ascending-ranks-open` | Open | generic probe |
| Asset Denial | Strategy | `asset-denial-battlefield` | Battle | hand-built scenario |
| Asset Denial | Strategy | `asset-denial-destroy` | Open | hand-built scenario |
| Asset Denial | Strategy | `asset-denial-home` | Battle | generic probe |
| Astonishing Accusation | Strategy | `accusation-honor-loss` | Limited | generic probe |
| Basic Lesson | Strategy | `basic-lesson-holding` | Limited | generic probe |
| Basic Lesson | Strategy | `basic-lesson-samurai` | Open | generic probe |
| Bayushi Dijuro | Personality | `wlpa-dijuro-lower` | Battle | generic probe |
| Bayushi Dijuro | Personality | `wlpa-dijuro-raise` | Battle | generic probe |
| Bayushi Ebara | Personality | `ebara-equip` | BattleOrOpen | generic probe |
| Bayushi Irezu | Personality | `irezu-move` | Battle | hand-built scenario |
| Bayushi Nitoshi | Personality | `nitoshi-destroy` | Battle | generic probe |
| Bayushi Nitoshi | Personality | `nitoshi-frame` | Limited | generic probe |
| Bayushi Nitoshi | Personality | `nitoshi-inexp-look-battle` | Battle | generic probe |
| Bayushi Nitoshi | Personality | `nitoshi-inexp-look-limited` | Limited | generic probe |
| Bayushi Nori | Personality | `nori-bow-attachments` | Battle | generic probe |
| Bayushi Nori | Personality | `nori-bow-personality` | Battle | generic probe |
| Bayushi Suwabe | Personality | `suwabe-force` | BattleOrOpen | generic probe |
| Bayushi Tenzan | Personality | `tenzan-penalty-battle` | Battle | generic probe |
| Bayushi Tenzan | Personality | `tenzan-penalty-limited` | Limited | generic probe |
| Bayushi Waru | Personality | `waru-bow-battle` | Battle | generic probe |
| Bayushi Waru | Personality | `waru-bow-limited` | Limited | generic probe |
| Beseech Sakkaku | Spell | `beseech-sakkaku-destroy` | Limited | generic probe |
| Beseech Sakkaku | Spell | `beseech-sakkaku-honor` | Limited | generic probe |
| Besieged | Strategy | `besieged-lower` | Open | generic probe |
| Besieged | Strategy | `besieged-raise` | Open | generic probe |
| Blood of Shahai | Celestial | `wl-shahai-blood` | Battle | generic probe |
| Blood of Shahai | Celestial | `wl-shahai-chi` | BattleOrOpen | hand-built scenario |
| Blood of the Preserver | Item | `w739-blood-reanimate` | BattleOrOpen | generic probe |
| Boiling Point | Strategy | `wl-sg-boiling-bow-straighten` | BattleOrOpen | generic probe |
| Bonds of Coin | Strategy | `bonds-of-coin-destroy` | Open | hand-built scenario |
| Bonds of Coin | Strategy | `bonds-of-coin-draw` | Limited | hand-built scenario |
| Border Keep | Holding | `border-first` | Limited | generic probe |
| Border Keep | Holding | `border-game` | Limited | generic probe |
| Brawl | Strategy | `brawl-bow` | Battle | generic probe |
| Brawl | Strategy | `brawl-destroy` | BattleOrOpen | hand-built scenario |
| Breaking the Rhythm | Strategy | `breaking-rhythm-bonuses` | Battle | generic probe |
| Breaking the Rhythm | Strategy | `breaking-rhythm-penalties` | Battle | generic probe |
| Broken Alliance | Strategy | `broken-alliance-destroy` | BattleOrOpen | generic probe |
| Broken Alliance | Strategy | `broken-alliance-move` | Battle | generic probe |
| Brotherhood Schism | Strategy | `brotherhood-schism-rings` | Open | generic probe |
| Brotherhood Schism | Strategy | `brotherhood-schism-shuffle` | Limited | hand-built scenario |
| Brothers in Battle | Strategy | `p2-brothers-in-battle` | BattleOrOpen | generic probe |
| Cast Aside the Weak | Strategy | `cast-aside-bow` | Battle | generic probe |
| Cast Aside the Weak | Strategy | `cast-aside-straighten` | Open | generic probe |
| Ceaseless Vigil | Strategy | `ceaseless-vigil-watch` | BattleOrOpen | generic probe |
| Celestial Sword of the Crab | Item | `celestial-sword-crab-straighten` | BattleOrOpen | generic probe |
| Celestial Sword of the Crane | Item | `celestial-sword-crane-favor` | Open | hand-built scenario |
| Celestial Sword of the Crane | Item | `celestial-sword-crane-weaken` | Open | generic probe |
| Celestial Sword of the Lion | Item | `celestial-sword-lion-create` | Battle | generic probe |
| Celestial Sword of the Mantis | Item | `celestial-sword-mantis-attack` | Battle | generic probe |
| Celestial Sword of the Mantis | Item | `celestial-sword-mantis-force` | Battle | generic probe |
| Celestial Sword of the Phoenix | Item | `celestial-sword-phoenix-return` | BattleOrOpen | hand-built scenario |
| Celestial Sword of the Scorpion | Item | `celestial-sword-scorpion-bow` | Battle | generic probe |
| Celestial Sword of the Unicorn | Item | `celestial-sword-unicorn-bow` | Battle | generic probe |
| Champion of Thunder | Follower | `champion-of-thunder-attack` | Battle | generic probe |
| Chuda Hikano | Personality | `w735-hikano-maho` | Limited | hand-built scenario |
| Chuda Hikano | Personality | `w735-hikano-straighten` | Battle | hand-built scenario |
| Chuda Hitaka | Personality | `hitaka-destroy-jade` | Limited | generic probe |
| Chuda Hitaka | Personality | `hitaka-recruit-oni` | Limited | generic probe |
| Control the Board | Strategy | `control-the-board-terrain` | Battle | hand-built scenario |
| Control the Board | Strategy | `control-the-board-weaken` | Battle | generic probe |
| Creating Order | Strategy | `creating-order-negate` | BattleOrOpen | generic probe |
| Creating Order | Strategy | `creating-order-straighten` | BattleOrOpen | generic probe |
| Crushing Strength | Strategy | `crushing-destroy` | Battle | generic probe |
| Crushing Strength | Strategy | `crushing-force` | Battle | generic probe |
| Cursed Relic | Item | `cursed-relic-attack` | Battle | generic probe |
| Cursed Relic | Item | `cursed-relic-remove` | Battle | generic probe |
| Daidoji Akeha | Personality | `akeha-recon-search` | Limited | generic probe |
| Daidoji Gensai | Personality | `w735-gensai-attack` | Battle | generic probe |
| Daidoji Gensai | Personality | `w735-gensai-move` | Battle | generic probe |
| Daigotsu Arakan | Personality | `wl-pc-arakan-battle` | Battle | hand-built scenario |
| Daigotsu Arakan | Personality | `wl-pc-arakan-limited` | Limited | hand-built scenario |
| Daigotsu Aya | Personality | `aya-cavalry` | Open | generic probe |
| Daigotsu Aya | Personality | `aya-duelist` | Open | generic probe |
| Daigotsu Aya | Personality | `aya-tactician` | Open | generic probe |
| Daigotsu Gyoken | Personality | `gyoken-attach` | BattleOrOpen | generic probe |
| Daigotsu Kanpeki | Personality | `kanpeki-destroy` | Battle | generic probe |
| Daigotsu Kazuko | Personality | `kazuko-fear-melee` | Battle | generic probe |
| Daigotsu Kazuko | Personality | `kazuko-fear-melee-destroy` | Battle | hand-built scenario |
| Daigotsu Negishi | Personality | `negishi-straighten` | BattleOrOpen | generic probe |
| Daigotsu Yamato | Personality | `yamato-transfer` | BattleOrOpen | hand-built scenario |
| Daigotsu Yutaka | Personality | `p5-yutaka-return` | Battle | hand-built scenario |
| Daigotsu Yutaka | Personality | `p5-yutaka-return-dark` | Battle | hand-built scenario |
| Dangerous Indulgence | Strategy | `dangerous-indulgence-bow` | Battle | generic probe |
| Dangerous Indulgence | Strategy | `dangerous-indulgence-straighten` | Battle | generic probe |
| Deadly Discipline | Strategy | `deadly-discipline-bow` | Battle | generic probe |
| Deadly Discipline | Strategy | `deadly-discipline-tokens` | Limited | generic probe |
| Deception Revealed | Strategy | `deception-revealed-weaken` | Battle | generic probe |
| Destructive Priorities | Strategy | `destructive-priorities-kata` | Limited | generic probe |
| Discovering a Conspiracy | Strategy | `discovering-conspiracy-expose` | BattleOrOpen | generic probe |
| Disgraceful Conduct | Strategy | `disgraceful-conduct-bow` | Battle | generic probe |
| Disgraceful Conduct | Strategy | `disgraceful-conduct-bow-unit` | Battle | generic probe |
| Divination Bowl | Item | `wl-bowl-draw` | Limited | generic probe |
| Divination Bowl | Item | `wl-bowl-look` | Limited | generic probe |
| Doji Atsumichi | Personality | `atsumichi-bow` | Battle | generic probe |
| Doji Atsumichi | Personality | `atsumichi-straighten` | Battle | generic probe |
| Doji Hakuseki | Personality | `hakuseki-disable` | Open | generic probe |
| Doji Hakuseki | Personality | `hakuseki-duel` | Battle | generic probe |
| Doji Makoto | Personality | `makoto-duel` | Battle | generic probe |
| Doji Makoto | Personality | `makoto-favor` | Open | generic probe |
| Doji Razan | Personality | `wl1-razan-destroy` | Limited | hand-built scenario |
| Drain of Effort | Strategy | `w738-drain-draw` | Open | generic probe |
| Drain of Effort | Strategy | `w738-drain-watch` | Open | generic probe |
| Drawing on the Mountain | Spell | `drawing-mountain-negate` | BattleOrOpen | generic probe |
| Duel of Serpents | Strategy | `duel-serpents-bow` | Battle | generic probe |
| Duel of Serpents | Strategy | `duel-serpents-honor` | Open | hand-built scenario |
| Duel to First Blood | Strategy | `first-blood-duel-battle` | Battle | generic probe |
| Duel to First Blood | Strategy | `first-blood-duel-limited` | Limited | generic probe |
| Easy Persuasion | Strategy | `easy-persuasion-move` | Battle | generic probe |
| Effortless Counterattack | Strategy | `effortless-counterattack-move` | Battle | generic probe |
| Ekichu no Oni | Personality | `kuronada-destroy` | Battle | generic probe |
| Ekichu no Oni | Personality | `mihato-move` | Battle | generic probe |
| Ekichu no Oni | Personality | `tsuji-bow` | Battle | generic probe |
| Elite Sentry | Follower | `elite-sentry-destroy` | Battle | generic probe |
| Elite Sentry | Follower | `elite-sentry-straighten` | BattleOrOpen | generic probe |
| Entrenched Position | Strategy | `entrenched-bow` | Battle | generic probe |
| Essence of Undeath | Spell | `essence-of-undeath-recruit` | BattleOrOpen | generic probe |
| Expensive Achievement | Strategy | `expensive-achievement-lower` | Battle | generic probe |
| Expensive Achievement | Strategy | `expensive-achievement-raise` | Battle | generic probe |
| Fallen | Strategy | `pe-fallen-bow` | BattleOrOpen | generic probe |
| False Ring of Air | Ring | `false-ring-of-air-force` | Battle | generic probe |
| False Ring of Air | Ring | `false-ring-of-air-recruit` | Battle | hand-built scenario |
| False Ring of Fire | Ring | `wl-false-ring-of-fire-recruit` | Open | hand-built scenario |
| False Ring of Water | Ring | `false-ring-of-water-attack` | Battle | generic probe |
| False Ring of Water | Ring | `false-ring-of-water-recruit` | Battle | hand-built scenario |
| False Ring of the Void | Ring | `wl-false-ring-of-void-recruit` | Battle | hand-built scenario |
| Field Messenger | Follower | `field-messenger-transfer` | BattleOrOpen | generic probe |
| Find Your Center | Strategy | `wlsac-find-center-enter` | Limited | generic probe |
| Find Your Center | Strategy | `wlsac-find-center-straighten` | Battle | hand-built scenario |
| Flanking Unit | Follower | `flanking-unit-move-adjacent` | Battle | hand-built scenario |
| Flanking Unit | Follower | `flanking-unit-move-home` | Battle | generic probe |
| Folklore | Strategy | `promo-b-folklore-force` | BattleOrOpen | hand-built scenario |
| Folklore | Strategy | `promo-b-folklore-move` | Battle | hand-built scenario |
| Foothills Keep | Stronghold | `foothills-equip-weapon` | BattleOrOpen | generic probe |
| Foothold of the Mad | Stronghold | `foothold-holdings` | Limited | generic probe |
| Foothold of the Mad | Stronghold | `foothold-madness` | Limited | generic probe |
| Forging Destiny | Strategy | `forging-destiny-move` | Battle | generic probe |
| Friends in the Jungle | Strategy | `wl-sg-friends-jungle` | BattleOrOpen | generic probe |
| Fubatsu Plate | Item | `fubatsu-plate-move` | Battle | generic probe |
| Fubatsu Plate | Item | `fubatsu-plate-straighten` | Battle | generic probe |
| Fudo | Personality | `promo-fudo-charge` | Limited | generic probe |
| Fudo | Personality | `promo-fudo-destroy` | Battle | generic probe |
| Fudo's Assurances | Celestial | `wl-fudo-assurances-destroy` | Open | hand-built scenario |
| Fudo's Assurances | Celestial | `wl-fudo-assurances-straighten` | BattleOrOpen | hand-built scenario |
| Gaining Experience | Strategy | `promo-b-gaining-experience-cavalry` | Open | generic probe |
| Gaining Experience | Strategy | `promo-b-gaining-experience-tactician` | Open | generic probe |
| Games of Will | Strategy | `games-of-will-bow` | Battle | generic probe |
| Games of Will | Strategy | `games-of-will-negate` | Battle | generic probe |
| Genji's Students | Follower | `genji-attack` | Battle | generic probe |
| Genji's Students | Follower | `genji-province-lower` | BattleOrOpen | generic probe |
| Genji's Students | Follower | `genji-province-raise` | BattleOrOpen | generic probe |
| Gold and Steel | Strategy | `gold-and-steel-bow` | Battle | generic probe |
| Gold and Steel | Strategy | `gold-and-steel-bow-move` | Battle | generic probe |
| Gold and Steel | Strategy | `gold-and-steel-destroy` | BattleOrOpen | hand-built scenario |
| Govern the Land | Strategy | `pe-govern-the-land-attach` | BattleOrOpen | generic probe |
| Govern the Land | Strategy | `pe-govern-the-land-holding` | Open | generic probe |
| Gozu | Personality | `sp-gozu-destroy` | Battle | hand-built scenario |
| Gozu | Personality | `sp-gozu-destroy-discard` | Battle | hand-built scenario |
| Green Lake Dojo | Holding | `wl-green-lake-dojo-cavalry` | Open | hand-built scenario |
| Green Lake Dojo | Holding | `wl-green-lake-dojo-cavalry-own-turn` | Open | hand-built scenario |
| Hachiman's Prowess | Celestial | `wl-hachiman-grant` | Limited | generic probe |
| Hachiman's Prowess | Celestial | `wl-hachiman-straighten` | Battle | generic probe |
| Headbutt | Strategy | `p5-headbutt-destroy` | Battle | generic probe |
| Headbutt | Strategy | `p5-headbutt-destroy-fresh` | Battle | hand-built scenario |
| Hida Fubatsu | Personality | `fubatsu-bow` | Battle | generic probe |
| Hida Fubatsu | Personality | `fubatsu-create` | Limited | generic probe |
| Hida Kisada (Emperor) | Personality | `kisada-destroy` | Battle | generic probe |
| Hida Komatsu | Personality | `komatsu-bow` | Battle | generic probe |
| Hida Komatsu | Personality | `komatsu-destroy-attachment` | Battle | generic probe |
| Hida Yamadera | Personality | `p5-yamadera-bow` | Battle | generic probe |
| Hidden Defenses | Strategy | `hidden-defenses-bow` | Battle | hand-built scenario |
| Hidden Weapons | Holding | `w739-hidden-battle` | Battle | hand-built scenario |
| Hidden Weapons | Holding | `w739-hidden-limited` | Limited | hand-built scenario |
| Hidden Weapons | Holding | `w739-hidden-store` | Limited | generic probe |
| Hiruma Nitani | Personality | `nitani-exp-attack` | Battle | generic probe |
| Hiruma Nitani | Personality | `nitani-exp-destroy` | Battle | hand-built scenario |
| Hiruma Tensin | Personality | `hiruma-sniper-attack` | Battle | hand-built scenario |
| Hiruma Tensin | Personality | `tensin-follower-attack` | Battle | hand-built scenario |
| Hoshoku-sha | Personality | `wl1-hoshoku-destroy` | BattleOrOpen | generic probe |
| Humble Farm | Holding | `humble-farm-create` | BattleOrOpen | generic probe |
| Hundred-Fold Cut | Strategy | `hundred-fold-cut-draw` | Battle | generic probe |
| Ikoma Hakige | Personality | `hakige-force-bonus` | Battle | generic probe |
| Ikoma Hakige | Personality | `hakige-move-home` | Battle | generic probe |
| Ikoma Shika | Personality | `shika-ranged` | Battle | generic probe |
| Ikoma Shika | Personality | `shika-ranged-discard` | Battle | generic probe |
| Ikoma Tsukasa | Personality | `tsukasa-bow` | Battle | generic probe |
| Ikoma Tsukasa | Personality | `tsukasa-straighten` | Battle | generic probe |
| Immovable Object | Strategy | `immovable-object-bow` | Battle | generic probe |
| Immovable Object | Strategy | `immovable-object-straighten` | BattleOrOpen | generic probe |
| Imperial Protection | Strategy | `imperial-protection-provinces` | Open | generic probe |
| Imperial Protection | Strategy | `imperial-protection-watch` | Open | generic probe |
| In the Heart of Battle | Strategy | `heart-of-battle-surge` | BattleOrOpen | generic probe |
| Incense Mill | Holding | `incense-mill-cleanse` | BattleOrOpen | generic probe |
| Inciting Mistrust | Strategy | `p5-inciting-mistrust-discard` | Battle | generic probe |
| Indecision | Strategy | `indecision-move` | Battle | generic probe |
| Inspired Devotion | Strategy | `inspired-devotion-bonus` | BattleOrOpen | generic probe |
| Inspired Devotion | Strategy | `inspired-devotion-chi` | BattleOrOpen | generic probe |
| Insulting Gesture | Strategy | `insulting-gesture-shame` | Battle | hand-built scenario |
| Insulting Gesture | Strategy | `insulting-gesture-take` | Open | generic probe |
| Interrogation | Strategy | `pe-interrogation-draw` | Open | generic probe |
| Isawa Hibana | Personality | `hibana-boost` | BattleOrOpen | hand-built scenario |
| Isawa Kaname | Personality | `kaname-ranged-attack` | Battle | generic probe |
| Isawa Koiso | Personality | `koiso-battle-destroy` | Battle | generic probe |
| Isawa Koiso | Personality | `koiso-limited-destroy` | Limited | hand-built scenario |
| Isawa Mitsuko | Personality | `mitsuko-bow-destroy` | Battle | generic probe |
| Isawa Mitsuko | Personality | `mitsuko-straighten` | Open | generic probe |
| Isawa Momoko | Personality | `momoko-spell-waiver` | BattleOrOpen | generic probe |
| Isawa Norimichi | Personality | `norimichi-army` | Battle | generic probe |
| Isawa Norimichi | Personality | `w734-norimichi-lower` | Battle | generic probe |
| Isawa Norimichi | Personality | `w734-norimichi-raise` | Battle | generic probe |
| Ivory Magistrate Outpost | Strategy | `ivory-outpost-battle` | Battle | generic probe |
| Ivory Magistrate Outpost | Strategy | `ivory-outpost-open` | Open | generic probe |
| Iweko Seiken | Personality | `w739-seiken-favor` | Open | generic probe |
| Jurojin's Blessing | Celestial | `jurojin-look` | Limited | generic probe |
| Jurojin's Blessing | Celestial | `jurojin-return` | Open | generic probe |
| Kaiu Esumi | Personality | `esumi-attack-lower` | Battle | generic probe |
| Kaiu Esumi | Personality | `esumi-attack-raise` | Battle | generic probe |
| Kaiu Fumiko | Personality | `fumiko-bow-lower` | Battle | generic probe |
| Kaiu Fumiko | Personality | `fumiko-bow-raise` | Battle | generic probe |
| Kaiu Iemasa | Personality | `iemasa-bow` | Battle | generic probe |
| Kaiu Iemasa | Personality | `iemasa-rebuild` | Limited | generic probe |
| Kaiu Nakano | Personality | `p2-nakano-force` | BattleOrOpen | generic probe |
| Kaiu Tojikana | Personality | `tojikana-bow` | Battle | generic probe |
| Kaiu Tojikana | Personality | `tojikana-honor` | Limited | generic probe |
| Kakita Kae | Personality | `kae-move-home` | Battle | generic probe |
| Kakita Tadanobu | Personality | `tadanobu-duel-battle` | Battle | generic probe |
| Kakita Tadanobu | Personality | `tadanobu-duel-limited` | Limited | generic probe |
| Kamalakar | Personality | `kamalakar-move-battlefield` | Battle | hand-built scenario |
| Kamalakar | Personality | `kamalakar-move-home` | Battle | generic probe |
| Kenro-ji-jin's Bounty | Celestial | `pe-kenro-ji-jins-bounty-farms` | Limited | generic probe |
| Khol Regulars | Follower | `khol-regulars-attack` | Battle | generic probe |
| Killing Intent | Strategy | `p2-killing-intent-straighten` | Battle | generic probe |
| Killing Intent | Strategy | `p2-killing-intent-straighten-home` | Battle | generic probe |
| Kitsu Akai | Personality | `kitsu-akai-battle` | Battle | generic probe |
| Kitsu Akai | Personality | `kitsu-akai-create` | Limited | generic probe |
| Kitsu Tamasine | Personality | `tamasine-boost-many` | Battle | hand-built scenario |
| Kitsu Tamasine | Personality | `tamasine-boost-one` | Battle | generic probe |
| Kitsuki Jakuei | Personality | `jakuei-bow` | Battle | generic probe |
| Kitsuki Jakuei | Personality | `jakuei-bow-favor` | Battle | hand-built scenario |
| Kitsuki Yoyugi | Personality | `yoyugi-lock` | Open | hand-built scenario |
| Kitsuki Yoyugi | Personality | `yoyugi-lock-free` | Open | generic probe |
| Kitsune Yuko | Personality | `wl1-yuko-honor` | Limited | generic probe |
| Koan's Whisper | Spell | `w741-whisper-destroy` | Limited | hand-built scenario |
| Koan's Whisper | Spell | `w741-whisper-reduce` | Limited | generic probe |
| Kunji | Personality | `kunji-boost` | BattleOrOpen | generic probe |
| Kunji | Personality | `kunji-exp-return-free` | Open | hand-built scenario |
| Kunji | Personality | `kunji-exp-return-paid` | Open | hand-built scenario |
| Kuon's Legacy | Strategy | `kuons-legacy-rage` | BattleOrOpen | hand-built scenario |
| Kurohito's Perfection | Celestial | `kurohito-perfection-cavalry` | Limited | generic probe |
| Kurohito's Perfection | Celestial | `kurohito-perfection-duelist` | Limited | generic probe |
| Kurohito's Perfection | Celestial | `kurohito-perfection-tactician` | Limited | generic probe |
| Kyuden Otomo | Stronghold | `otomo-courtier-token` | Open | generic probe |
| Legendary Rivalry | Strategy | `pe-legendary-rivalry-bow` | BattleOrOpen | hand-built scenario |
| Library of Rebirth | Stronghold | `library-rebirth-straighten` | Battle | generic probe |
| Long-Range Scouts | Follower | `w735-scouts-transfer` | BattleOrOpen | generic probe |
| Lost in Transit | Strategy | `lost-in-transit-bow` | BattleOrOpen | generic probe |
| Maddened Horde | Follower | `wl-horde-enemy` | Battle | generic probe |
| Maddened Horde | Follower | `wl-horde-own` | Battle | generic probe |
| Mahatsu | Personality | `promo-mahatsu-air` | Open | generic probe |
| Mahatsu | Personality | `promo-mahatsu-earth` | Open | generic probe |
| Mahatsu | Personality | `promo-mahatsu-fire` | Open | generic probe |
| Mahatsu | Personality | `promo-mahatsu-water` | Open | generic probe |
| Manipulate Energies | Strategy | `manipulate-energies-give-cavalry` | Open | hand-built scenario |
| Manipulate Energies | Strategy | `manipulate-energies-give-naval` | Open | hand-built scenario |
| Manipulate Energies | Strategy | `manipulate-energies-give-tactician` | Open | hand-built scenario |
| Manipulate Energies | Strategy | `manipulate-energies-remove-cavalry` | Open | generic probe |
| Manipulate Energies | Strategy | `manipulate-energies-remove-naval` | Open | generic probe |
| Manipulate Energies | Strategy | `manipulate-energies-remove-tactician` | Open | generic probe |
| Mara's Touch | Spell | `maras-touch-attack` | Battle | generic probe |
| Mara's Touch | Spell | `maras-touch-cavalry` | Open | generic probe |
| Martyr's Call | Strategy | `wl-martyrs-call` | BattleOrOpen | generic probe |
| Master of Beasts | Spell | `master-of-beasts-straighten` | BattleOrOpen | hand-built scenario |
| Meeting the Keepers | Strategy | `keepers-draw` | Open | hand-built scenario |
| Megumi's Guidance | Celestial | `megumis-guidance-shield` | BattleOrOpen | generic probe |
| Merchant Outpost | Holding | `wl-merchant-outpost-enemy` | Limited | generic probe |
| Merchant Outpost | Holding | `wl-merchant-outpost-own` | Limited | hand-built scenario |
| Methodical Questioning | Strategy | `methodical-questioning-bow-look` | Limited | generic probe |
| Methodical Questioning | Strategy | `methodical-questioning-look` | Limited | generic probe |
| Might of the Sun | Spell | `pe-might-of-the-sun-bow` | Battle | hand-built scenario |
| Might of the Sun | Spell | `pe-might-of-the-sun-free` | Battle | generic probe |
| Mirumoto Houken | Personality | `houken-exp-straighten` | BattleOrOpen | generic probe |
| Mirumoto Houken | Personality | `houken-straighten` | BattleOrOpen | generic probe |
| Mirumoto Kalen | Personality | `w740-kalen-attack` | Battle | generic probe |
| Mirumoto Kalen | Personality | `w740-kalen-attack2` | Battle | generic probe |
| Mirumoto Mitoshi | Personality | `mitoshi-bow` | Battle | generic probe |
| Mirumoto Mitoshi | Personality | `mitoshi-straighten` | Battle | generic probe |
| Mirumoto Shikei | Personality | `shikei-draw` | Limited | generic probe |
| Mirumoto Shikei | Personality | `shikei-duel` | Battle | generic probe |
| Monastery of New Thought | Stronghold | `monastery-fire-attack` | Battle | hand-built scenario |
| Monastery of New Thought | Stronghold | `monastery-fire-token` | Limited | hand-built scenario |
| Moto Aoi | Personality | `aoi-boost` | BattleOrOpen | generic probe |
| Moto Hailung | Personality | `hailung-attach` | Battle | hand-built scenario |
| Moto Hailung | Personality | `hailung-attach-discount` | Battle | hand-built scenario |
| Moto Isul | Personality | `isul-bow` | Battle | generic probe |
| Moto Isul | Personality | `isul-straighten` | Battle | generic probe |
| Moto Ming-Gwok | Personality | `wl-pc-mingwok-ranged` | Battle | generic probe |
| Moto Ming-Gwok | Personality | `wl-pc-mingwok-ring` | Open | hand-built scenario |
| Moto Rani | Personality | `rani-exp-straighten` | Battle | generic probe |
| Moving and Unmoving | Strategy | `moving-unmoving-approach` | Battle | generic probe |
| Moving and Unmoving | Strategy | `moving-unmoving-retreat` | Battle | generic probe |
| Mukudori's Wisdom | Strategy | `wl-sg-mukudori-bow` | BattleOrOpen | hand-built scenario |
| Mukudori's Wisdom | Strategy | `wl-sg-mukudori-straighten` | BattleOrOpen | hand-built scenario |
| Murderous Intent | Strategy | `murderous-intent-boost` | BattleOrOpen | generic probe |
| My Lord's Favor | Strategy | `lords-favor-straighten` | BattleOrOpen | hand-built scenario |
| My Lord's Favor | Strategy | `lords-favor-take` | Open | generic probe |
| Naka Mahatsu | Personality | `mahatsu-search` | Limited | generic probe |
| Never Safe | Strategy | `never-safe-penalty` | Battle | generic probe |
| Nikutai | Follower | `nikutai-attack` | Battle | generic probe |
| Ninube Hajime | Personality | `hajime-move-battlefield` | Battle | hand-built scenario |
| Ninube Hajime | Personality | `hajime-move-home` | Battle | generic probe |
| No Time for Games | Strategy | `no-time-for-games-rehonor` | Open | hand-built scenario |
| Of One Instant | Strategy | `of-one-instant-boost` | BattleOrOpen | generic probe |
| On the Hunt | Strategy | `on-the-hunt-equip` | Battle | generic probe |
| On the Hunt | Strategy | `on-the-hunt-straighten` | Open | generic probe |
| Oracle of the Void | Holding | `w736-oracle-draw` | Limited | generic probe |
| Oracle of the Void | Holding | `w736-oracle-refill` | Limited | generic probe |
| Overpower | Strategy | `wl-overpower-limited` | Limited | generic probe |
| Overpower | Strategy | `wl-overpower-open` | Open | generic probe |
| Oyo's Second Lesson | Strategy | `oyos-second-lesson-reborn` | BattleOrOpen | generic probe |
| P'an Ku | Personality | `w827-panku-madness` | Open | generic probe |
| P'an Ku | Personality | `w827-panku-take` | Limited | hand-built scenario |
| P'an Ku's Madness | Celestial | `w736-madness-bar` | Open | hand-built scenario |
| P'an Ku's Madness | Celestial | `w736-madness-token` | Limited | generic probe |
| Paid Off | Strategy | `paid-bow` | Open | generic probe |
| Paid Off | Strategy | `paid-straighten` | Open | generic probe |
| Path to Inner Peace | Spell | `w736-path-peace` | BattleOrOpen | generic probe |
| Pearl of Embers | Item | `pearl-of-embers-straighten` | BattleOrOpen | generic probe |
| Pearls & Spirits | Strategy | `pearls-spirits-chi-attack` | Battle | generic probe |
| Pearls & Spirits | Strategy | `pearls-spirits-force-attack` | Battle | generic probe |
| Pearls & Spirits | Strategy | `pearls-spirits-sacrifice-attack` | Battle | generic probe |
| Perfect Sacrifice | Strategy | `perfect-sacrifice-battle` | Battle | generic probe |
| Perfect Sacrifice | Strategy | `perfect-sacrifice-limited` | Limited | generic probe |
| Perplexing Guests | Strategy | `p5-perplexing-guests-create` | Battle | generic probe |
| Petty Squabbles | Strategy | `p5-petty-return` | BattleOrOpen | generic probe |
| Petty Squabbles | Strategy | `p5-petty-reveal` | Limited | generic probe |
| Phantom Visage | Strategy | `phantom-visage-bow` | BattleOrOpen | generic probe |
| Precision | Strategy | `precision-attack` | Battle | generic probe |
| Profits | Strategy | `pay-profits-enter` | Limited | generic probe |
| Provoked Violence | Strategy | `wlsac-provoked-violence` | Open | generic probe |
| Provoked Violence | Strategy | `wlsac-provoked-violence-yojimbo` | Open | hand-built scenario |
| Re-fortification | Strategy | `pe-re-fortification-holding` | Limited | hand-built scenario |
| Re-fortification | Strategy | `pe-re-fortification-move` | Battle | hand-built scenario |
| Readied Steel | Strategy | `readied-steel-attach` | BattleOrOpen | generic probe |
| Readied Steel | Strategy | `readied-steel-straighten` | BattleOrOpen | generic probe |
| Recruitment Station | Holding | `wl-recruitment-station-equip` | BattleOrOpen | hand-built scenario |
| Renyu's Wrath | Item | `wl-renyu-destroy` | Battle | hand-built scenario |
| Renyu's Wrath | Item | `wl-renyu-ranged` | Battle | generic probe |
| Request Authorization | Strategy | `request-authorization-move` | Battle | generic probe |
| Riku | Personality | `wlpt-riku-duel` | Battle | generic probe |
| Ring of Air | Ring | `ring-of-air-recruit` | Battle | hand-built scenario |
| Ring of Air | Ring | `ring-of-air-straighten` | BattleOrOpen | generic probe |
| Ring of Earth | Ring | `ring-of-earth-lower` | Battle | generic probe |
| Ring of Earth | Ring | `ring-of-earth-raise` | Battle | generic probe |
| Ring of Earth | Ring | `ring-of-earth-recruit` | Battle | hand-built scenario |
| Ring of Fire | Ring | `ring-of-fire-destroy` | Battle | generic probe |
| Ring of Fire | Ring | `ring-of-fire-recruit` | Battle | hand-built scenario |
| Ring of Water | Ring | `ring-of-water-move-home` | Battle | generic probe |
| Ring of Water | Ring | `ring-of-water-recruit` | Battle | hand-built scenario |
| Ring of the Void | Ring | `ring-of-void-draw` | Limited | generic probe |
| Ring of the Void | Ring | `ring-of-void-recruit` | Limited | generic probe |
| Rugashi Bazaar | Holding | `rugashi-revenge` | BattleOrOpen | hand-built scenario |
| Rumors Travel | Strategy | `rumors-travel-move` | Battle | generic probe |
| Rumors Travel | Strategy | `rumors-travel-reduce` | Limited | generic probe |
| Sadahako's Artistry | Celestial | `w821-sadahako-gift` | Limited | generic probe |
| Sadahako's Artistry | Celestial | `w821-sadahako-honor` | Limited | generic probe |
| Sandayu | Personality | `sandayu-straighten` | BattleOrOpen | generic probe |
| Scavenging Party | Strategy | `scavenging-party-provinces` | Open | generic probe |
| Scavenging Party | Strategy | `scavenging-party-refill` | Limited | generic probe |
| Scorching Lash | Spell | `w735-lash-minus` | BattleOrOpen | generic probe |
| Scorching Lash | Spell | `w735-lash-plus` | BattleOrOpen | generic probe |
| Sea & Sky | Spell | `pe-sea-and-sky-remove` | BattleOrOpen | hand-built scenario |
| Secret Reserve | Strategy | `secret-reserve-straighten` | BattleOrOpen | generic probe |
| Seek the Stain | Spell | `p5-seek-stain-equip-battle` | Battle | hand-built scenario |
| Seek the Stain | Spell | `p5-seek-stain-equip-limited` | Limited | hand-built scenario |
| Seek the Stain | Spell | `p5-seek-stain-kolat` | Open | generic probe |
| Seek the Stain | Spell | `p5-seek-stain-ninja` | Open | generic probe |
| Seek the Stain | Spell | `p5-seek-stain-shadowlands` | Open | generic probe |
| Seeking the Question | Strategy | `seeking-question-enter` | Limited | generic probe |
| Seeking the Question | Strategy | `seeking-question-look` | Limited | hand-built scenario |
| Selfless Defense | Strategy | `selfless-defense-destroy` | Battle | generic probe |
| Selfless Defense | Strategy | `selfless-defense-move` | Battle | generic probe |
| Serenity in Air | Strategy | `serenity-air-enter` | Limited | generic probe |
| Serenity in Air | Strategy | `serenity-air-straighten` | Battle | hand-built scenario |
| Serenity in Air | Strategy | `serenity-air-straighten-open` | Open | hand-built scenario |
| Set the Fields Aflame | Spell | `set-fields-holding` | Limited | generic probe |
| Set the Fields Aflame | Spell | `set-fields-region` | Open | hand-built scenario |
| Set the Fields Aflame | Spell | `set-fields-terrain` | Battle | hand-built scenario |
| Shadow Dragon | Personality | `shadow-dragon-exp2-return` | Open | hand-built scenario |
| Shadow Dragon | Personality | `shadow-dragon-exp2-take` | Battle | generic probe |
| Shadow Tactics | Strategy | `shadow-tactics-home` | Battle | generic probe |
| Shadow Tactics | Strategy | `shadow-tactics-stay` | Battle | generic probe |
| Shiba Iaimiko | Personality | `wl-pc-iaimiko-cavalry` | Open | generic probe |
| Shiba Iaimiko | Personality | `wl-pc-iaimiko-conqueror` | Open | generic probe |
| Shiba Iaimiko | Personality | `wl-pc-iaimiko-force` | Open | generic probe |
| Shiba Jouta | Personality | `p5-jouta-attack` | Battle | generic probe |
| Shiba Jouta | Personality | `p5-jouta-attack-dead` | Battle | hand-built scenario |
| Shiba Tetsaka | Personality | `wlpt-tetsaka-cavalry` | Open | generic probe |
| Shiba Tetsaka | Personality | `wlpt-tetsaka-naval` | Open | generic probe |
| Shiba Tsukimi | Personality | `tsukimi-attack` | Battle | generic probe |
| Shiba's Guidance | Celestial | `shibas-guidance-return` | BattleOrOpen | hand-built scenario |
| Shinjo Horibe | Personality | `horibe-bow` | Battle | generic probe |
| Shinjo Horibe | Personality | `horibe-straighten` | Battle | generic probe |
| Shinjo Katsuo | Personality | `katsuo-straighten` | BattleOrOpen | generic probe |
| Shinjo Kinto | Personality | `kinto-exp-move-battle` | Battle | generic probe |
| Shinjo Kinto | Personality | `kinto-exp-move-home` | Battle | generic probe |
| Shinjo Min-Hee | Personality | `minhee-destroy` | Battle | generic probe |
| Shinjo Min-Hee | Personality | `minhee-return` | Limited | generic probe |
| Shinobi Vassal | Follower | `wl-vassal-destroy` | Battle | generic probe |
| Shinobi Vassal | Follower | `wl-vassal-equip` | Battle | hand-built scenario |
| Shiro Chugo | Stronghold | `chugo-melee` | Battle | generic probe |
| Shosuro Aroru | Personality | `aroru-exp2-attack` | Battle | generic probe |
| Shosuro Chikata | Personality | `chikata-bow` | Battle | generic probe |
| Shosuro Chikata | Personality | `chikata-straighten` | Battle | generic probe |
| Shosuro Chizuru | Personality | `w734-chizuru-force` | Open | generic probe |
| Shosuro Chizuru | Personality | `w734-chizuru-honor` | Open | generic probe |
| Shosuro Hawado | Personality | `hawado-move-battlefield` | Battle | generic probe |
| Shosuro Hawado | Personality | `hawado-move-home` | Battle | generic probe |
| Shosuro Kameyoi | Personality | `wl-pc-kameyoi-dishonor` | Limited | generic probe |
| Shosuro Kameyoi | Personality | `wl-pc-kameyoi-honor` | Open | generic probe |
| Shosuro Makiko | Personality | `makiko-favor` | Open | generic probe |
| Shrine to Hachiman | Holding | `shrine-hachiman-straighten` | BattleOrOpen | generic probe |
| Siege Volley | Strategy | `siege-volley-attack` | Battle | generic probe |
| Siege Volley | Strategy | `siege-volley-lower` | Battle | generic probe |
| Siege Volley | Strategy | `siege-volley-raise` | Battle | generic probe |
| Sinkhole | Strategy | `pe-sinkhole-region` | Battle | hand-built scenario |
| Sinkhole | Strategy | `pe-sinkhole-terrain` | Battle | hand-built scenario |
| Skipping the Puddle | Strategy | `skipping-the-puddle-enter` | Limited | generic probe |
| Skipping the Puddle | Strategy | `skipping-the-puddle-move` | Battle | hand-built scenario |
| Social Grace | Strategy | `social-grace-penalty` | Open | generic probe |
| Solid Defense | Strategy | `solid-defense-move` | Battle | generic probe |
| Soshi House Guard | Follower | `soshi-conditional-bow` | BattleOrOpen | generic probe |
| Soshi Yoshihara | Personality | `yoshihara-dishonor` | Open | generic probe |
| Soshi Yoshihara | Personality | `yoshihara-lock` | Open | generic probe |
| Sound the Retreat | Strategy | `sound-the-retreat-enter` | Limited | generic probe |
| Sound the Retreat | Strategy | `sound-the-retreat-move` | Battle | hand-built scenario |
| Spiritual Coalescence | Strategy | `w741-coalescence` | BattleOrOpen | generic probe |
| Stay Put | Strategy | `stay-put-bow` | Battle | generic probe |
| Stay Put | Strategy | `stay-put-bow-attach` | Battle | hand-built scenario |
| Stay Put | Strategy | `stay-put-bow-attach-discount` | Battle | hand-built scenario |
| Steal an Advantage | Strategy | `steal-advantage-battle` | Battle | generic probe |
| Steal the Candle's Flame | Spell | `tf-steal-candle-attack` | Battle | generic probe |
| Step Through the Void | Spell | `p5-step-through-void` | BattleOrOpen | generic probe |
| Stolen Merchandise | Holding | `p5-stolen-ability` | Open | hand-built scenario |
| Stolen Merchandise | Holding | `p5-stolen-gold` | Open | generic probe |
| Strength of the Divine | Strategy | `strength-of-the-divine-force` | Battle | hand-built scenario |
| Strength of the Divine | Strategy | `strength-of-the-divine-honor` | Battle | hand-built scenario |
| Strength of the Mountain | Strategy | `strength-of-the-mountain-bow` | Battle | generic probe |
| Strength of the Mountain | Strategy | `strength-of-the-mountain-earth` | Battle | generic probe |
| Strength of the Tsunami | Spell | `tsunami-move-home` | Battle | generic probe |
| Strength of the Tsunami | Spell | `tsunami-straighten` | Battle | generic probe |
| Striking Through the Void | Strategy | `striking-void-reduce` | BattleOrOpen | generic probe |
| String of Victories | Strategy | `string-of-victories` | BattleOrOpen | generic probe |
| Strong Defenses | Strategy | `strong-defenses-boost` | Battle | hand-built scenario |
| Stuck in Rokugan | Strategy | `stuck-in-rokugan-discard` | Limited | hand-built scenario |
| Stuck in Rokugan | Strategy | `stuck-in-rokugan-straighten` | Open | generic probe |
| Suitengu's Blessing | Celestial | `pe-suitengus-blessing-move` | Battle | generic probe |
| Suitengu's Blessing | Celestial | `pe-suitengus-blessing-naval` | Limited | generic probe |
| Suitengu's Torch | Stronghold | `torch-move` | Battle | generic probe |
| Takasho | Personality | `takasho-attack` | Battle | generic probe |
| Takasho | Personality | `takasho-move` | Battle | generic probe |
| Tales of Dishonor | Strategy | `tales-of-dishonor-straighten` | Battle | generic probe |
| Taming the Beast | Spell | `taming-beast-straighten` | BattleOrOpen | generic probe |
| Tamori Kusugi | Personality | `kusugi-lower` | Battle | generic probe |
| Tamori Kusugi | Personality | `kusugi-raise` | Battle | generic probe |
| Tamori Tamoko | Personality | `tamoko-open` | Open | hand-built scenario |
| Tamori Yayu | Personality | `yayu-bow-attack` | Battle | hand-built scenario |
| Tamori Yayu | Personality | `yayu-destroy-attack` | Battle | hand-built scenario |
| Tanemono | Personality | `tanemono-switch` | BattleOrOpen | generic probe |
| Temple Grounds | Strategy | `w738-temple-battle` | Battle | generic probe |
| Temple to the Elements | Holding | `temple-elements-air` | Open | generic probe |
| Temple to the Elements | Holding | `temple-elements-earth` | Open | generic probe |
| Temple to the Elements | Holding | `temple-elements-fire` | Open | generic probe |
| Temple to the Elements | Holding | `temple-elements-water` | Open | generic probe |
| Temples of Gisei Toshi | Holding | `temples-gisei-follower` | Limited | generic probe |
| Temples of Gisei Toshi | Holding | `temples-gisei-item` | Limited | generic probe |
| Temples of Gisei Toshi | Holding | `temples-gisei-spell` | Limited | generic probe |
| Testimony | Strategy | `testimony-dishonor` | Limited | hand-built scenario |
| Testimony | Strategy | `testimony-rehonor` | Limited | hand-built scenario |
| Tetsuo | Personality | `tetsuo-destroy-attachment` | Battle | generic probe |
| Tetsuo | Personality | `tetsuo-destroy-weaker-weaponed` | Battle | hand-built scenario |
| The Ancestral Sword of Hantei | Item | `ancestral-sword-hantei-bow` | Battle | generic probe |
| The Blade of the Balash | Item | `pf-balash-attack` | Battle | generic probe |
| The Blade of the Balash | Item | `pf-balash-attack-hand` | Battle | hand-built scenario |
| The Broken Man | Personality | `wlpa-brokenman-bow` | Battle | generic probe |
| The Compassion of the Unicorn | Strategy | `compassion-unicorn-straighten` | BattleOrOpen | hand-built scenario |
| The Cycle of Vengeance | Strategy | `cycle-of-vengeance-straighten` | BattleOrOpen | generic probe |
| The Dark Naga | Personality | `dark-naga-take` | Battle | generic probe |
| The Dark Naga | Personality | `wl-pc-darknaga-equip` | Open | hand-built scenario |
| The Dark Naga | Personality | `wl-pc-darknaga-ranged` | Battle | generic probe |
| The Deciding Moment | Strategy | `pe-deciding-moment-draw` | Battle | generic probe |
| The Deciding Moment | Strategy | `pe-deciding-moment-order` | Limited | hand-built scenario |
| The Earth's Strength | Spell | `earths-strength-straighten` | Battle | generic probe |
| The Fires of War | Strategy | `fires-of-war-second-use` | BattleOrOpen | generic probe |
| The Game Has Changed | Strategy | `game-has-changed` | BattleOrOpen | generic probe |
| The Governor's Estate | Stronghold | `w821-governor-force-down` | Battle | generic probe |
| The Governor's Estate | Stronghold | `w821-governor-force-up` | Battle | generic probe |
| The Governor's Estate | Stronghold | `w821-governor-honor` | Limited | generic probe |
| The Height of Courage | Strategy | `height-courage-boost` | Battle | generic probe |
| The Heir's Wrath | Strategy | `heirs-wrath-bow` | Limited | hand-built scenario |
| The Heir's Wrath | Strategy | `heirs-wrath-favor` | Limited | hand-built scenario |
| The Honesty of the Phoenix | Strategy | `honesty-phoenix-name` | BattleOrOpen | hand-built scenario |
| The Honor of the Lion | Strategy | `honor-lion-boost` | BattleOrOpen | hand-built scenario |
| The Host's Advantage | Strategy | `hosts-advantage-bow` | Battle | generic probe |
| The Host's Advantage | Strategy | `hosts-advantage-destroy` | Limited | hand-built scenario |
| The Isha's Yumi | Item | `pf-yumi-attack` | Battle | generic probe |
| The Isha's Yumi | Item | `pf-yumi-attack-hand` | Battle | hand-built scenario |
| The Kami's Blessing | Spell | `kami-blessing-samurai` | Battle | generic probe |
| The Kami's Blessing | Spell | `kami-blessing-self` | BattleOrOpen | generic probe |
| The Legion's Might | Strategy | `wlsac-legions-might-1` | Battle | generic probe |
| The Legion's Might | Strategy | `wlsac-legions-might-3` | Battle | hand-built scenario |
| The Moon's Imperative | Celestial | `moons-imperative-discard` | Open | generic probe |
| The Moon's Imperative | Celestial | `moons-imperative-holding` | Open | generic probe |
| The New Foundries | Stronghold | `p5-foundries-attack` | Battle | hand-built scenario |
| The New Foundries | Stronghold | `p5-foundries-draw` | Battle | hand-built scenario |
| The New Foundries | Stronghold | `p5-foundries-put` | Limited | generic probe |
| The New Foundries | Stronghold | `p5-foundries-straighten` | Battle | hand-built scenario |
| The Otoro Estate | Stronghold | `otoro-attack` | Battle | generic probe |
| The Otoro Estate | Stronghold | `otoro-attack-move` | Battle | generic probe |
| The Otoro Estate | Stronghold | `otoro-look` | Open | generic probe |
| The Passing of Tradition | Strategy | `wl-passing-copy` | BattleOrOpen | generic probe |
| The Passing of Tradition | Strategy | `wl-passing-limited` | Limited | generic probe |
| The Power of One | Strategy | `power-one-bow` | Battle | generic probe |
| The Power of One | Strategy | `power-one-straighten` | BattleOrOpen | generic probe |
| The Power of a Word | Strategy | `power-word-move` | Battle | hand-built scenario |
| The Red Hunger | Personality | `red-hunger-attachments` | Battle | generic probe |
| The Red Hunger | Personality | `red-hunger-personality` | Battle | generic probe |
| The Second City | Region | `second-city-reveal` | Open | generic probe |
| The Second City | Region | `second-city-reveal-raise` | Open | generic probe |
| The Shakash | Personality | `shakash-bow` | Battle | generic probe |
| The Shakash | Personality | `shakash-move-home` | Battle | generic probe |
| The Sincerity of the Dragon | Strategy | `sincerity-dragon-name` | BattleOrOpen | hand-built scenario |
| The Sorrow | Personality | `w735-sorrow-battle` | Battle | generic probe |
| The Sorrow | Personality | `w735-sorrow-limited` | Limited | generic probe |
| The Sun's Aspiration | Celestial | `suns-aspiration-holding` | Open | generic probe |
| The Sun's Aspiration | Celestial | `suns-aspiration-shuffle` | Open | generic probe |
| The Tachikaze | Holding | `w739-tachikaze-battle` | Battle | hand-built scenario |
| The Tachikaze | Holding | `w739-tachikaze-limited` | Limited | hand-built scenario |
| The Tao of Fudo | Spell | `wl-tao-of-fudo-hand` | BattleOrOpen | hand-built scenario |
| The Tao of Fudo | Spell | `wl-tao-of-fudo-straighten` | BattleOrOpen | hand-built scenario |
| The Temple of Death | Stronghold | `temple-of-death-dead` | Limited | generic probe |
| The Temple of Death | Stronghold | `temple-of-death-straighten` | BattleOrOpen | generic probe |
| The Temple of Death | Stronghold | `temple-of-death-straighten-bow` | BattleOrOpen | hand-built scenario |
| The Turquoise Court | Holding | `pe-turquoise-court-gain` | Limited | generic probe |
| The Turquoise Court | Holding | `pe-turquoise-court-lose` | Limited | generic probe |
| The Vengeful | Follower | `vengeful-attack` | Battle | generic probe |
| The Vengeful | Follower | `vengeful-straighten` | Battle | generic probe |
| The Weakness of Man | Strategy | `weakness-of-man-toggle` | BattleOrOpen | generic probe |
| The World Disappears | Strategy | `world-disappears-bow` | Open | hand-built scenario |
| The World Disappears | Strategy | `world-disappears-void` | Open | hand-built scenario |
| The Yari of the Shabura | Item | `pe-yari-ranged` | Battle | generic probe |
| Theological Indecision | Strategy | `theological-indecision-chi` | BattleOrOpen | generic probe |
| Theological Indecision | Strategy | `theological-indecision-discard` | Limited | hand-built scenario |
| Through the Breach | Strategy | `pe-through-the-breach-create` | Limited | generic probe |
| Through the Breach | Strategy | `pe-through-the-breach-remove` | Limited | hand-built scenario |
| Thunder Bow | Item | `w736-bow-transfer` | BattleOrOpen | generic probe |
| Thunder's Wrath | Spell | `thunders-wrath-attack` | Battle | generic probe |
| Thunder's Wrath | Spell | `thunders-wrath-bow` | Battle | generic probe |
| Tireless Efforts | Strategy | `tireless-efforts-negate` | BattleOrOpen | generic probe |
| Tireless Efforts | Strategy | `tireless-efforts-straighten` | Battle | generic probe |
| Togashi Hiroto | Personality | `hiroto-bow` | Battle | generic probe |
| Togashi Hizumi | Personality | `hizumi-province-lower` | Battle | generic probe |
| Togashi Hizumi | Personality | `hizumi-province-raise` | Battle | generic probe |
| Togashi Mitsu | Personality | `mitsu-fire-attack` | Battle | generic probe |
| Togashi Mitsu | Personality | `mitsu-fire-tokens` | BattleOrOpen | generic probe |
| Togashi Osawa | Personality | `osawa-bow` | BattleOrOpen | generic probe |
| Togashi Osawa | Personality | `osawa-straighten` | BattleOrOpen | generic probe |
| Togashi Sakata | Personality | `sakata-attack` | Battle | generic probe |
| Togashi Sakata | Personality | `sakata-fire-token` | Limited | generic probe |
| Togashi Sakata | Personality | `sakata-sake-token` | Limited | generic probe |
| Togashi Torazu | Personality | `torazu-fire-attack` | Battle | hand-built scenario |
| Togashi's Guidance | Celestial | `togashis-guidance-ring-play` | BattleOrOpen | hand-built scenario |
| Togashi's Guidance | Celestial | `togashis-guidance-ring-search` | BattleOrOpen | hand-built scenario |
| Tokiko | Personality | `p5-tokiko-battlefield` | Battle | generic probe |
| Tokiko | Personality | `p5-tokiko-home` | BattleOrOpen | generic probe |
| Training Bushido Virtue | Strategy | `training-bow-any` | Open | generic probe |
| Training Bushido Virtue | Strategy | `training-bow-at-battle` | Battle | generic probe |
| Training Bushido Virtue | Strategy | `training-limited-draw` | Limited | generic probe |
| Training Dark Virtue | Strategy | `training-bow-any` | Open | generic probe |
| Training Dark Virtue | Strategy | `training-bow-at-battle` | Battle | generic probe |
| Training Dark Virtue | Strategy | `training-straighten-at-battle` | Battle | generic probe |
| Training Discipline item | Item | `training-discipline-ability` | BattleOrOpen | generic probe |
| Training False Ring of Air | Ring | `false-ring-of-air-force` | Battle | generic probe |
| Training False Ring of Air | Ring | `false-ring-of-air-recruit` | Battle | hand-built scenario |
| Training False Ring of Water | Ring | `false-ring-of-water-attack` | Battle | generic probe |
| Training False Ring of Water | Ring | `false-ring-of-water-recruit` | Battle | hand-built scenario |
| Training Humble Farm | Holding | `humble-farm-create` | BattleOrOpen | generic probe |
| Training Mitsu | Personality | `mitsu-fire-attack` | Battle | generic probe |
| Training Mitsu | Personality | `mitsu-fire-tokens` | BattleOrOpen | generic probe |
| Training Recon actor | Personality | `training-plain-open` | Open | generic probe |
| Training Recon actor | Personality | `training-recon-attack` | Battle | generic probe |
| Training Recon actor | Personality | `training-recon-duel` | Battle | generic probe |
| Training Recon actor | Personality | `training-recon-open` | Open | generic probe |
| Training Ring of Air | Ring | `ring-of-air-recruit` | Battle | hand-built scenario |
| Training Ring of Air | Ring | `ring-of-air-straighten` | BattleOrOpen | generic probe |
| Training Ring of Earth stub | Ring | `ring-of-earth-lower` | Battle | generic probe |
| Training Ring of Earth stub | Ring | `ring-of-earth-raise` | Battle | generic probe |
| Training Ring of Earth stub | Ring | `ring-of-earth-recruit` | Battle | hand-built scenario |
| Training Ring of Fire stub | Ring | `ring-of-fire-destroy` | Battle | generic probe |
| Training Ring of Fire stub | Ring | `ring-of-fire-recruit` | Battle | hand-built scenario |
| Training Ring of Water | Ring | `ring-of-water-move-home` | Battle | generic probe |
| Training Ring of Water | Ring | `ring-of-water-recruit` | Battle | hand-built scenario |
| Training Ring of the Void | Ring | `ring-of-void-draw` | Limited | generic probe |
| Training Ring of the Void | Ring | `ring-of-void-recruit` | Limited | generic probe |
| Training Sakata | Personality | `sakata-attack` | Battle | generic probe |
| Training Sakata | Personality | `sakata-fire-token` | Limited | generic probe |
| Training Sakata | Personality | `sakata-sake-token` | Limited | generic probe |
| Training Tamoko | Personality | `tamoko-open` | Open | hand-built scenario |
| Training card bower | Personality | `training-bow-enemy-card-at-battle` | Battle | generic probe |
| Training card bower | Personality | `training-bow-enemy-card-open` | Open | generic probe |
| Training cost fixture | Personality | `training-at-any-cost` | Open | generic probe |
| Training cost fixture | Personality | `training-charity-honor` | Open | generic probe |
| Training cost fixture | Personality | `training-charity-honor-capped` | Open | generic probe |
| Training cost fixture | Personality | `training-disgrace-bow` | Open | generic probe |
| Training cost fixture | Personality | `training-stain-straighten` | Open | generic probe |
| Training cost fixture | Personality | `training-urgency-draw` | Limited | generic probe |
| Training delayed-effect caster | Personality | `training-delayed-boost` | Battle | generic probe |
| Training delayed-effect caster | Personality | `training-delayed-bow` | Battle | generic probe |
| Training discard armory | Holding | `training-attach-weapon-performer` | BattleOrOpen | generic probe |
| Training discard attacher | Personality | `training-attach-follower-free` | Limited | generic probe |
| Training discard attacher | Personality | `training-attach-weapon-discount` | Limited | generic probe |
| Training dual attacker | Personality | `training-honor-cost-attack` | Battle | generic probe |
| Training dual attacker | Personality | `training-weapon-attack` | Battle | generic probe |
| Training followed-target attacker | Personality | `training-followed-dishonorable-melee-attack` | Battle | generic probe |
| Training followed-target attacker | Personality | `training-followed-recon-attack` | Battle | generic probe |
| Training holding maker | Personality | `training-create-bowed-holding` | Open | generic probe |
| Training holding maker | Personality | `training-create-charter` | Open | generic probe |
| Training holding maker | Personality | `training-create-charter-tax` | Open | generic probe |
| Training holding maker | Personality | `training-create-terrain` | Battle | generic probe |
| Training holding maker | Personality | `training-create-terrain-open` | Open | hand-built scenario |
| Training keyword satisfier | Personality | `training-keyword-conditional-force-boost` | Battle | generic probe |
| Training keyword satisfier | Personality | `training-keyword-conditional-straighten` | Open | generic probe |
| Training look source | Personality | `training-look-any` | Limited | generic probe |
| Training look source | Personality | `training-look-dynasty` | Limited | generic probe |
| Training look source | Personality | `training-look-fixed` | Limited | generic probe |
| Training look source | Personality | `training-look-show` | Limited | generic probe |
| Training look source | Personality | `training-look-take-item` | Limited | generic probe |
| Training move source | Personality | `training-move-anywhere` | Battle | hand-built scenario |
| Training move source | Personality | `training-move-both-home` | Battle | generic probe |
| Training move source | Personality | `training-move-current` | Battle | hand-built scenario |
| Training move source | Personality | `training-move-performer-and-target` | Battle | hand-built scenario |
| Training move source | Personality | `training-move-source-adjacent` | Battle | hand-built scenario |
| Training penalty source | Personality | `training-bonus-stats` | Battle | generic probe |
| Training penalty source | Personality | `training-cost-to-zero` | Battle | generic probe |
| Training penalty source | Personality | `training-dishonor-target` | Battle | generic probe |
| Training penalty source | Personality | `training-gold-cost-token` | Battle | generic probe |
| Training penalty source | Personality | `training-penalty-chi` | Battle | generic probe |
| Training penalty source | Personality | `training-penalty-force` | Battle | generic probe |
| Training penalty source | Personality | `training-penalty-force-card` | Battle | generic probe |
| Training penalty source | Personality | `training-penalty-force-own` | Battle | generic probe |
| Training penalty source | Personality | `training-penalty-gold` | Battle | generic probe |
| Training penalty source | Personality | `training-penalty-honor` | Battle | generic probe |
| Training scout | Personality | `training-gain-reconnaissance` | Open | generic probe |
| Training scout | Personality | `training-gain-reconnaissance-here` | Battle | generic probe |
| Training scout | Personality | `training-reconnaissance-strike` | Battle | generic probe |
| Training scout maker | Personality | `training-create-scouts` | Battle | generic probe |
| Training scout maker | Personality | `training-create-scouts-each` | Battle | generic probe |
| Training search source | Personality | `training-search-cheap` | Limited | generic probe |
| Training search source | Personality | `training-search-focus` | Limited | generic probe |
| Training search source | Personality | `training-search-optional` | Limited | generic probe |
| Training skin donor | Personality | `training-bow-any` | Open | generic probe |
| Training skin donor | Personality | `training-create-token` | Open | generic probe |
| Training skin receiver | Personality | `training-copy-ability` | Open | generic probe |
| Training skin receiver | Personality | `training-copy-ability-turn` | Open | generic probe |
| Training skin receiver | Personality | `training-copy-keyword` | Open | generic probe |
| Training slots source | Personality | `training-slots-discard` | Open | generic probe |
| Training slots source | Personality | `training-slots-mixed` | Open | generic probe |
| Training slots source | Personality | `training-slots-two-personalities` | Battle | generic probe |
| Training slots source | Personality | `training-slots-two-political` | Battle | generic probe |
| Training slots source | Personality | `training-slots-up-to-three` | Open | generic probe |
| Training stat-cost Strategy | Strategy | `training-stat-cost-performer` | Open | generic probe |
| Training stat-cost Strategy | Strategy | `training-stat-cost-source` | Open | generic probe |
| Training token master | Personality | `training-create-token` | Open | generic probe |
| Training token master | Personality | `training-destroy-token` | Open | hand-built scenario |
| Training token master | Personality | `training-make-dead` | Open | generic probe |
| Training token master | Personality | `training-move-token` | Open | hand-built scenario |
| Training two-action Discipline | Strategy | `return-to-action-move` | Battle | hand-built scenario |
| Training two-action Discipline | Strategy | `training-discipline-ability` | BattleOrOpen | generic probe |
| Training value maker | Personality | `training-create-computed` | Open | generic probe |
| Training value maker | Personality | `training-create-pair` | Open | generic probe |
| Training value maker | Personality | `training-create-strong-scout` | Open | generic probe |
| Training value maker | Personality | `training-create-titled` | Open | generic probe |
| Trembling Earth | Spell | `pe-trembling-earth-bow` | Battle | generic probe |
| Trembling Earth | Spell | `pe-trembling-earth-equip` | Battle | generic probe |
| Trusting Instinct | Strategy | `trusting-instinct-move` | Battle | generic probe |
| Tsubute | Item | `p5-tsubute-attack` | Battle | generic probe |
| Tsubute | Item | `p5-tsubute-attack-hand` | Battle | hand-built scenario |
| Tsuruchi | Personality | `tsuruchi-inexp-arrow-attack` | Battle | generic probe |
| Tsuruchi | Personality | `tsuruchi-inexp-arrows` | Open | generic probe |
| Turning Away | Strategy | `p2-turning-away-lower` | Battle | hand-built scenario |
| Turning Away | Strategy | `p2-turning-away-raise` | Battle | hand-built scenario |
| Two Stings | Strategy | `two-stings-attack` | Battle | generic probe |
| Two Stings | Strategy | `two-stings-attack-from-discard` | Battle | hand-built scenario |
| Unchecked Fury | Strategy | `wlsac-unchecked-fury-enter` | Limited | generic probe |
| Unchecked Fury | Strategy | `wlsac-unchecked-fury-equip` | Battle | hand-built scenario |
| Unfortunate Hesitation | Strategy | `unfortunate-hesitation` | BattleOrOpen | generic probe |
| Unholy Strike | Strategy | `unholy-strike-attack` | Battle | generic probe |
| Unimpeachable Name | Strategy | `unimpeachable-name-bow` | Battle | generic probe |
| Unsafe Passage | Strategy | `unsafe-passage-grant` | Battle | generic probe |
| Unsettling Doubts | Strategy | `p5-doubts-refill` | Limited | generic probe |
| Unsettling Doubts | Strategy | `p5-doubts-remove` | Open | hand-built scenario |
| Urgency | Strategy | `urgency-draw` | Limited | generic probe |
| Urgency | Strategy | `urgency-limit` | Open | generic probe |
| Versatile Blade | Item | `tf-versatile-move` | Battle | hand-built scenario |
| Versatile Blade | Item | `tf-versatile-straighten` | Battle | generic probe |
| Visitation | Strategy | `visitation-bow` | Open | generic probe |
| Visitation | Strategy | `visitation-honor` | Limited | generic probe |
| Void Tattoo | Strategy | `void-tattoo-return` | Battle | hand-built scenario |
| Void Tattoo | Strategy | `void-tattoo-return-void` | Battle | hand-built scenario |
| Wall of Honor | Strategy | `wall-of-honor-lower` | Battle | generic probe |
| Wall of Honor | Strategy | `wall-of-honor-raise` | Battle | generic probe |
| Ward of Air | Spell | `ward-of-air-move-home` | Battle | generic probe |
| Well Prepared | Strategy | `well-prepared-bow` | Battle | hand-built scenario |
| Well Prepared | Strategy | `well-prepared-straighten` | Battle | hand-built scenario |
| Wind of the Moon | Spell | `wind-moon-gold` | Open | generic probe |
| Wind of the Moon | Spell | `wind-moon-honor` | Open | generic probe |
| Words of Consecration | Spell | `words-of-consecration-watch` | Open | generic probe |
| Wyrmbone Katana | Item | `pf-wyrmbone-destroy` | Battle | generic probe |
| Wyrmbone Katana | Item | `pf-wyrmbone-move` | Battle | hand-built scenario |
| Yasuki Baiko | Personality | `baiko-favor` | Open | generic probe |
| Yasuki Baiko | Personality | `baiko-shame` | Limited | hand-built scenario |
| Yasuki Daito | Personality | `wlpt-daito-bow` | Limited | generic probe |
| Yasuki Daito | Personality | `wlpt-daito-gold` | Limited | generic probe |
| Yasuki Ikke | Personality | `ikke-move-home` | Battle | generic probe |
| Yasuki Jinn-Kuen | Personality | `jinn-kuen-straighten` | Open | generic probe |
| Yasuki Tsujiken | Personality | `tsujiken-exp-bow` | Open | generic probe |
| Yasuki Tsujiken | Personality | `tsujiken-exp-dishonor` | Limited | generic probe |
| Yasuki Tsujiken | Personality | `tsujiken-exp-draw` | Limited | generic probe |
| Yasuki Tsujiken | Personality | `tsujiken-exp-straighten` | Open | generic probe |
| Yasuki Tsujiken | Personality | `tsujiken-straighten-holding` | Open | generic probe |
| Yoritomo Hama | Personality | `hama-bow` | Battle | generic probe |
| Yoritomo Hama | Personality | `hama-straighten` | Battle | generic probe |
| Yoritomo Singh | Personality | `singh-exp2-destroy` | Battle | generic probe |
| Yoritomo Tansen | Personality | `wlpt-tansen-province` | Battle | generic probe |
| Yoritomo Tansen | Personality | `wlpt-tansen-ranged` | Battle | generic probe |
| Yotsu Ueda's Chosen | Follower | `pcc-yotsu-bow` | Battle | generic probe |
| Yotsu Ueda's Chosen | Follower | `pcc-yotsu-straighten` | Battle | generic probe |

## Reaction abilities of multi-window cards

| Card | Ability | Trigger | Verified by |
|---|---|---|---|
| A Close Call | `a-close-call-negate` | before-attack-resolves | reaction scenario |
| Akodo Dairuko | `dairuko-rally` | before-battle-resolves | reaction scenario |
| Akodo Kakihara | `kakihara-attack-force-bonus` | before-attack-resolves | reaction scenario |
| Akodo Kakihara | `kakihara-force-bonus` | before-action-resolves | reaction scenario |
| Akodo Tsudoken | `tsudoken-exp2-entry` | own-enters-play | reaction scenario |
| Armor of the Heavens | `armor-heavens-gain` | targeted-by-action | reaction scenario |
| Armor of the Heavens | `armor-heavens-lose` | targeted-by-action | reaction scenario |
| Asako Izuna | `izuna-kolat` | own-enters-play | reaction scenario |
| Asako Izuna | `izuna-ninja` | own-enters-play | reaction scenario |
| Asako Izuna | `izuna-shadowlands` | own-enters-play | reaction scenario |
| Asako Kikugoro | `kikugoro-henshin-entry` | own-personality-enters-play | reaction scenario |
| Asako Kikugoro | `kikugoro-ring-entry` | after-action-resolved | reaction scenario |
| Asako Kikugoro | `kikugoro-self-entry` | own-enters-play | reaction scenario |
| Astonishing Accusation | `accusation-negate` | targeted-by-action | reaction scenario |
| Bayushi Irezu | `irezu-duel-enter` | before-duel-begins | reaction scenario |
| Celestial Sword of the Crab | `celestial-sword-crab-honor` | own-enters-play | reaction scenario |
| Celestial Sword of the Crane | `celestial-sword-crane-honor` | own-enters-play | reaction scenario |
| Celestial Sword of the Lion | `celestial-sword-lion-honor` | own-enters-play | reaction scenario |
| Celestial Sword of the Mantis | `celestial-sword-mantis-honor` | own-enters-play | reaction scenario |
| Celestial Sword of the Phoenix | `celestial-sword-phoenix-honor` | own-enters-play | reaction scenario |
| Celestial Sword of the Scorpion | `celestial-sword-scorpion-lose` | own-enters-play | reaction scenario |
| Celestial Sword of the Unicorn | `celestial-sword-unicorn-honor` | own-enters-play | reaction scenario |
| Centering the Soul | `centering-soul-straighten` | after-action-resolved | reaction scenario |
| Centering the Soul | `centering-soul-straighten-destroy` | after-action-resolved | reaction scenario |
| Champion of Thunder | `champion-of-thunder-negate` | before-action-resolves | reaction scenario |
| Closing the Gap | `pe-closing-the-gap-home` | after-action-resolved | reaction scenario |
| Closing the Gap | `pe-closing-the-gap-to-battle` | after-action-resolved | reaction scenario |
| Crippled Sensei | `crippled-sensei-create-action` | after-action-resolved | reaction scenario |
| Crippled Sensei | `crippled-sensei-create-battle` | after-battle-resolution | reaction scenario |
| Daidoji Akeha | `akeha-recon-draw` | after-action-resolved | reaction scenario |
| Daigotsu Aya | `aya-entry-dark-virtue` | own-enters-play | reaction scenario |
| Daigotsu Kanpeki | `kanpeki-additional-action` | after-action-resolved | reaction scenario |
| Deception Revealed | `deception-revealed-straighten` | after-action-resolved | reaction scenario |
| Destructive Priorities | `destructive-priorities-reaction` | before-honor-change | reaction scenario |
| Doji Razan | `wl1-razan-invest` | own-enters-play | reaction scenario |
| Easy Persuasion | `easy-persuasion-honor` | p3-personality-entered-play | reaction scenario |
| Effortless Counterattack | `effortless-counterattack-negate` | before-additional-action-resolves | reaction scenario |
| Entrenched Position | `entrenched-negate` | attacker-battle-action-announced-early | reaction scenario |
| False Ring of Fire | `wl-false-ring-of-fire-reduce` | before-honor-change | reaction scenario |
| False Ring of the Void | `wl-false-ring-of-void-lock` | after-engaging | reaction scenario |
| Feign Death | `feign-death-action` | after-action-resolved | reaction scenario |
| Feign Death | `feign-death-battle` | after-battle-resolution | reaction scenario |
| Forest Cover | `wl-forest-cover-attach` | after-engaging | reaction scenario |
| Forest Cover | `wl-forest-cover-recon` | after-engaging | reaction scenario |
| Forging Destiny | `forging-destiny-negate` | p3-before-event-resolves | reaction scenario |
| Fudo | `promo-fudo-negate` | targeted-by-action | reaction scenario |
| Goju Kaxt | `proxy677-kaxt-create-battle` | after-action-resolved | reaction scenario |
| Goju Kaxt | `proxy677-kaxt-create-home` | after-action-resolved | reaction scenario |
| Hida Kisada (Emperor) | `kisada-redirect` | targeted-by-action | reaction scenario |
| Hida Yamadera | `p5-yamadera-refill` | own-enters-play | reaction scenario |
| Hidden Defenses | `hidden-defenses-move` | after-engaging | reaction scenario |
| Hundred-Fold Cut | `hundred-fold-cut-bow` | targeted-by-action | reaction scenario |
| Hundred-Fold Cut | `hundred-fold-cut-destroy` | targeted-by-action | reaction scenario |
| Inciting Mistrust | `p5-inciting-mistrust-negate` | targeted-by-action | reaction scenario |
| Indecision | `indecision-discard` | after-action-resolved | reaction scenario |
| Interrogation | `pe-interrogation-negate` | before-action-resolves | reaction scenario |
| Isawa Kaname | `kaname-attack-boost` | before-attack-resolves | reaction scenario |
| Isawa Norimichi | `norimichi-negate` | targeted-by-action | reaction scenario |
| Isawa Sakonoko | `sakonoko-negate` | targeted-by-action | reaction scenario |
| Isawa Sakonoko | `sakonoko-negate-spell` | targeted-by-action | reaction scenario |
| Iweko Seiken | `w739-seiken-recruit` | own-enters-play | reaction scenario |
| Kaiu Fumiko | `fumiko-attack-boost` | before-attack-resolves | reaction scenario |
| Kaiu Village | `kaiu-village-armor` | own-personality-enters-from-province | reaction scenario |
| Kaiu Village | `kaiu-village-weapon` | own-personality-enters-from-province | reaction scenario |
| Kakita Kae | `kae-favor-negate` | targeted-by-action | reaction scenario |
| Kenro-ji-jin's Bounty | `pe-kenro-ji-jins-bounty-gold` | paying-gold-cost | reaction scenario |
| Khol Regulars | `khol-regulars-reaction` | before-attack-resolves | reaction scenario |
| Kitsu Tamasine | `tamasine-create` | own-enters-play | reaction scenario |
| Kitsuki Horume | `horume-gain` | own-enters-play | reaction scenario |
| Kitsuki Horume | `horume-honor` | own-won-duel | reaction scenario |
| Kitsuki Horume | `horume-lose` | own-enters-play | reaction scenario |
| Kitsuki Jakuei | `jakuei-favor` | own-won-duel | reaction scenario |
| Kitsune Yuko | `wl1-yuko-invest` | own-enters-play | reaction scenario |
| Know No Fear | `know-no-fear-negate-fear` | targeted-by-action | reaction scenario |
| Know No Fear | `know-no-fear-negate-move` | targeted-by-action | reaction scenario |
| Kyuden Otomo | `otomo-favor-token` | after-action-resolved | reaction scenario |
| Library of Rebirth | `library-rebirth-gold` | paying-gold-cost | reaction scenario |
| Lying in Ambush | `pf-lying-in-ambush-battlefield` | after-action-resolved | reaction scenario |
| Lying in Ambush | `pf-lying-in-ambush-home` | after-action-resolved | reaction scenario |
| Meeting the Keepers | `keepers-search` | after-action-resolved | reaction scenario |
| Moshi Umiko | `umiko-attack-boost` | before-attack-resolves | reaction scenario |
| Moshi Umiko | `umiko-attack-reduce` | before-attack-resolves | reaction scenario |
| Moto Rani | `rani-exp-spell-discount` | paying-gold-cost | reaction scenario |
| Naka Mahatsu | `mahatsu-negate` | before-action-resolves | reaction scenario |
| Near Miss | `pay-near-miss-gold` | paying-gold-cost | reaction scenario |
| Near Miss | `pay-near-miss-negate` | targeted-by-action | reaction scenario |
| Never Safe | `never-safe-return` | after-battle-resolution | reaction scenario |
| Nikutai | `nikutai-straighten` | sibling-follower-bowed | reaction scenario |
| No Time for Games | `no-time-for-games-negate` | attacker-battle-action-announced-early | reaction scenario |
| Otomo Demiyah | `demiyah-gain` | own-enters-play | reaction scenario |
| Otomo Demiyah | `demiyah-lose` | own-enters-play | reaction scenario |
| Perplexing Guests | `p5-perplexing-guests-discard` | targeted-by-action | reaction scenario |
| Precision | `precision-boost` | before-attack-resolves | reaction scenario |
| Precision | `precision-reduce` | before-attack-resolves | reaction scenario |
| Profits | `pay-profits-gold` | paying-gold-cost | reaction scenario |
| Profits | `pay-profits-reduce` | paying-gold-cost | reaction scenario |
| Qalyar | `qalyar-recon-minus` | after-engaging-at-own-battlefield | reaction scenario |
| Qalyar | `qalyar-recon-plus` | after-engaging-at-own-battlefield | reaction scenario |
| Reconnaissance Scouts | `wl-scouts-engage` | after-engaging-at-own-battlefield | reaction scenario |
| Reconnaissance Scouts | `wl-scouts-engage-penalty` | after-engaging-at-own-battlefield | reaction scenario |
| Request Authorization | `request-authorization-negate` | before-action-resolves | reaction scenario |
| Reverse Fate | `w737-reverse-fate-minus` | targeted-by-action | reaction scenario |
| Reverse Fate | `w737-reverse-fate-plus` | targeted-by-action | reaction scenario |
| Riku | `wlpt-riku-redirect` | targeted-by-action | reaction scenario |
| Shiba Tsukimi | `tsukimi-battlefield` | after-action-resolved | reaction scenario |
| Shiba Tsukimi | `tsukimi-home` | after-action-resolved | reaction scenario |
| Shiro Chugo | `chugo-honor-reduce` | before-honor-change | reaction scenario |
| Shosuro Aroru | `aroru-exp2-redirect` | targeted-by-action | reaction scenario |
| Shosuro Hawado | `hawado-exp-move-battlefield` | after-battle-action-resolves | reaction scenario |
| Shosuro Hawado | `hawado-exp-move-home` | after-battle-action-resolves | reaction scenario |
| Shosuro Makiko | `wlpt-makiko-reduce` | before-honor-change | reaction scenario |
| Shosuro Rokuta | `rokuta-action-create` | after-action-resolved | reaction scenario |
| Shosuro Rokuta | `rokuta-battle-create` | after-battle-resolution | reaction scenario |
| Shosuro Ryoken | `ryoken-dishonor` | own-enters-play | reaction scenario |
| Shosuro Ryoken | `ryoken-honor-loss` | after-battle-resolution | reaction scenario |
| Social Grace | `social-grace-negate` | targeted-by-action | reaction scenario |
| Solid Defense | `solid-defense-negate` | before-honor-change | reaction scenario |
| Solid Defense | `solid-defense-negate-favor` | before-honor-change | reaction scenario |
| Steal an Advantage | `steal-advantage-engage` | after-engaging | reaction scenario |
| Steal the Candle's Flame | `tf-steal-candle-interrupt` | before-attack-resolves | reaction scenario |
| Stockpiled Resources | `stockpiled-attach` | after-engaging | reaction scenario |
| Stockpiled Resources | `stockpiled-attach-commander` | after-engaging | reaction scenario |
| Stormless Fury | `stormless-fury-attach` | after-battle-action-resolves | reaction scenario |
| Stormless Fury | `stormless-fury-bow` | after-battle-action-resolves | reaction scenario |
| Strong Defenses | `strong-defenses-reaction` | before-attack-resolves | reaction scenario |
| Suitengu's Torch | `torch-protect` | own-thunder-battle-action-announced | reaction scenario |
| Tales of Dishonor | `tales-of-dishonor-lose-honor` | after-battle-resolution | reaction scenario |
| Taming the Beast | `taming-beast-negate` | targeted-by-action | reaction scenario |
| Tamori Tamoko | `tamoko-negate` | before-action-resolves | reaction scenario |
| Temple Grounds | `w738-temple-engage` | after-engaging | reaction scenario |
| The Ancestral Sword of Hantei | `ancestral-sword-hantei-honor` | own-enters-play | reaction scenario |
| The Broken Man | `wlpa-brokenman-invest` | own-enters-play | reaction scenario |
| The Dark Naga | `dark-naga-forest-negate` | targeted-by-action | reaction scenario |
| The Earth's Strength | `earths-strength-negate-move` | before-action-resolves | reaction scenario |
| The Empress' Address | `empress-address-action` | after-action-resolved | reaction scenario |
| The Empress' Address | `empress-address-battle` | after-battle-resolution | reaction scenario |
| The Governor's Estate | `w821-governor-create` | w821-dynasty-begins | reaction scenario |
| The Height of Courage | `height-courage-negate` | before-action-resolves | reaction scenario |
| The Loyalty of the Scorpion | `loyalty-scorpion-bow` | targeted-by-action | reaction scenario |
| The Loyalty of the Scorpion | `loyalty-scorpion-destroy` | targeted-by-action | reaction scenario |
| The Loyalty of the Scorpion | `loyalty-scorpion-move` | targeted-by-action | reaction scenario |
| The Power of a Word | `power-word-reaction` | targeted-by-action | reaction scenario |
| The Spider's Web | `spiders-web-attach` | own-personality-enters-play | reaction scenario |
| The Spider's Web | `spiders-web-attach-kensai` | own-personality-enters-play | reaction scenario |
| The Tachikaze | `w739-tachikaze-collect` | after-action-resolved | reaction scenario |
| The Yari of the Shabura | `pe-yari-bar-targeting` | after-engaging | reaction scenario |
| The Yari of the Shabura | `pe-yari-return` | after-battle-resolution | reaction scenario |
| Togashi Hiroto | `hiroto-fire-tokens` | own-enters-play | reaction scenario |
| Togashi Hizumi | `hizumi-negate-straighten` | bowed-personality-at-own-battlefield | reaction scenario |
| Togashi Torazu | `torazu-fire-tokens` | own-enters-play | reaction scenario |
| Training Tamoko | `tamoko-negate` | before-action-resolves | reaction scenario |
| Training attack reactor | `training-attack-boost` | before-attack-resolves | reaction scenario |
| Training attack reactor | `training-attack-negate` | before-attack-resolves | reaction scenario |
| Training choose-a-player personality | `training-choose-player-gain` | own-enters-play | reaction scenario |
| Training choose-a-player personality | `training-choose-player-lose` | own-enters-play | reaction scenario |
| Training skin donor | `tsudoken-exp2-entry` | own-enters-play | reaction scenario |
| Trusting Instinct | `trusting-instinct-reaction` | before-attack-resolves | reaction scenario |
| Unholy Strike | `unholy-strike-boost` | before-attack-resolves | reaction scenario |
| Unimpeachable Name | `unimpeachable-name-reaction` | before-any-action-resolves | reaction scenario |
| Unsafe Passage | `unsafe-passage-negate` | targeted-by-action | reaction scenario |
| Ward of Air | `ward-of-air-negate` | targeted-by-action | reaction scenario |
| Well Protected | `pe-well-protected-bow` | targeted-by-action | reaction scenario |
| Well Protected | `pe-well-protected-destroy` | targeted-by-action | reaction scenario |
| Words Have Strength | `words-strength-action` | after-action-resolved | reaction scenario |
| Words Have Strength | `words-strength-resolution` | after-battle-resolution | reaction scenario |
| Words of Consecration | `words-of-consecration-honor` | own-enters-play | reaction scenario |
| Yasuki Ikke | `ikke-honor-loss` | targeted-by-action | reaction scenario |
| Yasuki Ikke | `ikke-negate` | targeted-by-action | reaction scenario |
| Yasuki Jinn-Kuen | `jinn-kuen-negate` | targeted-by-action | reaction scenario |
| Yasuki Tanimura | `tanimura-political-pay` | targeted-by-action | reaction scenario |
| Yasuki Tanimura | `tanimura-produce-gold` | paying-gold-cost | reaction scenario |
| Yasuki Tsujiken | `tsujiken-watch-holding` | own-enters-play | reaction scenario |
| Yoritomo Sakuma | `sakuma-political-negate` | before-action-resolves | reaction scenario |
| Yoritomo Sakuma | `sakuma-produce-gold` | paying-gold-cost | reaction scenario |
| Yoritomo Singh | `singh-exp2-gold` | paying-gold-cost | reaction scenario |
