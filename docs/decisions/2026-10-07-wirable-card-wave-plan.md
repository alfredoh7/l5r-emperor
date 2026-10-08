# Wirable-card wave plan (issue #742)

Status: **proposed** (a plan, not a ruling). Lane: rules. Requested by Mendon on 2026-10-07: package every currently wirable unwired card into an efficient set of waves. **No card is wired by this document.**

## Inputs and counts

- `docs/CARD_INVENTORY.xlsx` regenerated from `main` at `15fc74d0`: 201 of 2,217 cards unwired.
- Per-card primitive codes from [the #668 appendix](2026-10-05-remaining-unwired-primitives-cards.md). All 25 primitives P1 to P25 have merged since, so a card is wirable when it is not rejected, multiplayer-only, ruling-blocked or scan-blocked.
- Excluded by Mendon: Peace Treaty, Wildfire, Green Nightshade Blend ([rejected](2026-10-05-rejected-cards.md)); Begone, Fool!, Prideful Allies, Ningen's "Treatise on the Nezumi" (multiplayer-only). 195 remain.
- **182 wirable now. 13 wait on a ruling:** Foothold of the Mad, Journey's End Keep - exp, Chuda Kaito, Ikoma Satoru - exp, The Egg of P'an Ku - exp, Pierce the Tapestry, Unbearable Weight, Shrine to the Heavens, Stone of Remembrance, Minor Blackmail, Benten's Blessing, Jizo's Mercy, Sadahako's Artistry.
- No card is scan-blocked. Kimura, Forgiver of Sins (`oracle-l5r-10451`) and The Crystal Tears (`oracle-l5r-10718`) have receipt-verified 750x1050 scans (working-copy sha256 matches; the `SCAN2` label in the appendix was stale). The created-card proxies for Kikumura, Tsumaro, Yuribara, Constructing the Self and The Tower of Vigilance are published ([#677](2026-10-06-677-created-card-proxies.md)).

## How cards were grouped

Each card goes to the batch of its **rarest** needed primitive, so it lands with the mechanism most specific to it; generic infrastructure (P9, P24, P4) does not decide a batch. 124 cards need one primitive, 47 need two, 11 need three or more. A wave profile holds one card type, so each batch is **one PR with one packager writing one profile per type** (64 wave files across 10 PRs), as `package_created_proxies_677.py` did across six types. Every needed primitive is already merged, so no batch waits on another.

| Batch | Issue | Cards | Waves |
|---|---|---|---|
| 1 Payment and cost terms | #732 | 17 | 6 |
| 2 Recruit gates, Proclaim, Honor | #733 | 16 | 5 |
| 3 Stat terms and Experienced | #734 | 22 | 6 |
| 4 Keyword rules, Home and Tireless | #735 | 18 | 5 |
| 5 Card restrictions and immunities | #736 | 21 | 8 |
| 6 After-event windows and period watches | #737 | 17 | 8 |
| 7 Turn boundary, rule flags, province events | #738 | 18 | 6 |
| 8 Zones, search, decision UI | #739 | 17 | 8 |
| 9 Duels and rulebook modifiers | #740 | 15 | 7 |
| 10 Created cards, Fortification, small effects | #741 | 21 | 5 |

Suggested order, lowest risk first: 3, 4, then 1, 2, 5, 8, 10, then 9, then 6 and 7 last (they depend on timing windows and watches and hold the most Events).

## Batches

### Batch 1 Payment and cost terms (#732)

Why together: P1 and P2 both extend the Gold quote and cost pipeline (`QuotePurchase`, `GoldQuotes`, `AbilityCosts`, `EmperorMechE`).

- **Personality (5):** Asukai, the Tireless; Daigotsu Kikumura; Doji Kurohime; Morito Inoue, Scourge of the Plains; Sengmai, the Unbreakable
- **Follower (1):** Black Riders
- **Item (2):** Chiyurei's Axe; Tsujiken's Coin
- **Spell (1):** Opening the Veil
- **Strategy (4):** Duty Over All Things; Trade Embargo; Truce; Uncovering Treasures
- **Holding (4):** Colonial Dojo; Ever-Winter Copse; Suikihime's Chambers; The Amethyst Court

### Batch 2 Recruit gates, Proclaim, Honor (#733)

Why together: P3 recruitment gates and Proclaim eligibility sit next to P14 Honor-change modifiers (Kimura, Marriage of the Emerald Champion, Merchant Patronage).

- **Stronghold (1):** The False Path
- **Personality (9):** Asp Skirmisher; Bayushi Hamada; Ichiro Otani, the Stone Breaker; Ikoma Natsu - exp; Kimura, Forgiver of Sins; Suzume Shindo, the Final Blade; Tsi Hayamizu, the Hand of Steel; Yoritomo Yashinko - exp; Zansho - exp
- **Follower (1):** Court Scribe
- **Strategy (4):** Delicate Gamble; Invocation; Merchant Patronage; The Heavens' Blessing
- **Event (1):** Marriage of the Emerald Champion

### Batch 3 Stat terms and Experienced (#734)

Why together: P9 stat terms and P23 Experienced/Inexperienced parsing are both stat evaluation; P23 also fixes the silent "Experienced" parse bug.

- **Personality (13):** Bayushi Kachiko; Bayushi Nitoshi - exp; Doji Hoturi - inexperienced; Hantei XVI - inexperienced; Isawa Norimichi - exp; Isawa Tsuke; Iuchi Karasu - inexperienced; Kokujin - inexperienced; Mumoku no Oni; Shosuro Chizuru; Shosuro Tanihara; Usagi Seki, the Untouchable; Utaku Ji-Yun - exp
- **Item (3):** Armor of the Mad; Kshatriya Artifacts; Stonemason's Hammer
- **Spell (1):** The Sky's Barrier
- **Strategy (3):** Ready the Defenses; Unseemly Alliance; Warded Premises
- **Holding (1):** Well-Defended Farm
- **Event (1):** Riding to the Rescue

### Batch 4 Keyword rules, Home and Tireless (#735)

Why together: P10 keyword rules and P16 Tireless/Home flags are data on the card or ability, read by `Keyword()` and `ValidateAbility`.

- **Personality (8):** Chuda Hikano; Chun; Daidoji Gensai; Doji Tatsuzo; Isawa Tsumaro; The Shakash - exp; The Sorrow; Tsiang, the Benevolent
- **Follower (3):** Elephant Cavalry; Long-Range Scouts; Uji's Saboteurs
- **Spell (5):** Anchored in Earth; Blood of Isawa's Tribe; Chikushudo's Infusion; Drawing on All Things; Scorching Lash
- **Strategy (1):** Suikihime's Sanction
- **Celestial (1):** The Inevitable Grasp of Conquest

### Batch 5 Card restrictions and immunities (#736)

Why together: P11 card restrictions and P12 immunities are the two "what a card may or may not do" checks.

- **Personality (8):** Asako Kaitoko - exp; Hida Kaiji - exp; Isawa Ryuzo; Ninube Onchi - exp; Otomo Suikihime, Queen of Fools - exp; P'an Ku - exp; Susumu Haikaro; Yoritomo Naoto - exp
- **Follower (1):** Kamalakar's Harem
- **Item (4):** Koan's Jingasa; Map of the Northern Colonies; Tankoji; Thunder Bow
- **Spell (2):** Path to Inner Peace; The Earth's Armor
- **Strategy (3):** A Cleansing Breath; Bonds of Hospitality; Harbor of Kalani's Landing
- **Holding (1):** Oracle of the Void - exp
- **Event (1):** Rampant Paranoia
- **Celestial (1):** P'an Ku's Madness

### Batch 6 After-event windows and period watches (#737)

Why together: P6 after-event windows, P7 period watches and P8 action-effect watch share the after-event record and window machinery. Highest risk.

- **Personality (8):** Akumasa; Asahina Shigemitsu - exp; Hida Tadama; Hiruma Ikeuchi; Iweko Shibatsu; Moto Taigo, the Skull Taker - exp; Susumu Kuroko; Yogo Katsuta
- **Item (1):** Koan's Robes
- **Spell (1):** Reverse Fate
- **Strategy (2):** Imperial Deployment; The Crystal Tears
- **Holding (1):** Magistrate's Stipend
- **Event (2):** An End to Hostilities; Unseasonable Weather
- **Celestial (1):** Natsu-Togumara's Guidance
- **Region (1):** Verdant Jungle

### Batch 7 Turn boundary, rule flags, province events (#738)

Why together: P4 turn boundary, P5 province-reveal Events and P13 rule flags are turn and phase hooks; 9 of 18 are Events Phase cards. Highest risk.

- **Personality (2):** Doji Tatsuki - exp; Shosuro Takazaki
- **Item (2):** Dagger of Crystal Wind; Lantern Oil
- **Strategy (3):** Drain of Effort; Suck the Marrow; Temple Grounds
- **Holding (1):** Abundant Farmlands
- **Event (9):** A Dangerous Game; An Empire of Madness; Journey's End Siege; Open Recruitment; Rejected Mediation; Revelations; The Jade Championship; The Madness Spreads; The Onyx Championship
- **Celestial (1):** Chagatai's Wrath

### Batch 8 Zones, search, decision UI (#739)

Why together: P20 search/look, P21 zone extensions and P25 decision infrastructure share the deck viewer, zone queries and multi-pick prompts.

- **Personality (6):** Asako Usohara; Ikoma Jeiku; Iuchi Wattu; Iweko Seiken; Kitsu Miwa; Yoritomo Mikaru
- **Follower (1):** Disciples of Master Coin
- **Item (2):** Blood of the Preserver - exp; Paneki's Mask
- **Spell (2):** Poison the Cup; Unbound Essence
- **Strategy (1):** Now We Are Enemies
- **Holding (3):** Hidden Weapons; Jade Pearl Inn; The Tachikaze
- **Event (1):** Dark Audience
- **Region (1):** Kokure's Lands

### Batch 9 Duels and rulebook modifiers (#740)

Why together: P17 additional-action scope, P18 duel rules and P19 rulebook modifiers all alter duel or rulebook-action rules.

- **Personality (6):** Akodo Kano - exp; Doji Hakuseki - exp3; Ikoma Ayumu - exp; Mirumoto Kalen - exp; Seppun Ryota; Yoritomo Harumi
- **Follower (1):** Frontier Farmer
- **Item (1):** Fortune's Charm
- **Spell (1):** Focus on the Flame
- **Strategy (3):** A Magistrate Falls; Demonstrating Technique; Practicing Kata
- **Event (2):** Harbingers of War; The Empire's Foe Exposed
- **Celestial (1):** Hantei's Guidance

### Batch 10 Created cards, Fortification, small effects (#741)

Why together: P22 created cards and grants, P15 Fortification and P24 small effects are mostly feature-file-only changes.

- **Personality (5):** Kitsune Merihiko - exp; Kitsune Yuribara; Ninube Tsukau; Shinjo Kinto - exp2; Tamori Tsuchiya
- **Item (1):** Watersteel Sword
- **Spell (3):** Befriend the Tides; Constructing the Self; Koan's Whisper
- **Strategy (11):** A Doomseeker's Tomb; A Moving Battle; Harmony; Kitsuki Judgment; Overwhelming Chaos; Shifting Waves; Sorrowful Prayer; Spiritual Coalescence; Strength in the Earth; Unexpected Discovery; Unsettling Gathering
- **Holding (1):** The Tower of Vigilance

## Caveats

- The codes are the #668 assessment's, not the result of building each card. Expect a few cards per batch to need a small extension or a ruling; move them out with a blocker note instead of guessing (Kitsune Yuribara's forced-target rule is the likeliest).
- Each card still needs its own reading, scan check and tests; follow `.claude/skills/wire-card-wave`.
- The inventory workbook's blocker categories are stale for these cards and were not used.

Umbrella: #742.
