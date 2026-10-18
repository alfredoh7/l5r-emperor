# Great Sacrifice compares total Force; a Spell's static effect follows its printed subject (issue #936)

Status: implemented on Mendon's rulings of 2026-10-13, following [the validation-report reading](2026-10-13-validation-report-reading.md) and [the Fortitude / Spell-host fixes](2026-10-13-validation-focus-fortitude-spell-host.md). Lane: rules (shared code).

## A. Great Sacrifice compares the unit's total Force (R-084)

The target of Great Sacrifice is an enemy unit whose **total Force** (`UnitTotalForce`: the Personality and every Follower, bowed or not) is equal to or lower than the performer's Force plus Personal Honor, plus one if he is defending. It used the unit's battle contribution (`BattleForce`, which leaves out bowed cards). This supersedes item 11 of the 2026-09-21 second-city pass for the unit total (that doc now points here) and resolves the hole reported in the Fortitude note. The category `enemy-unit-within-performer-sacrifice` is the one place listing, validation and execution read.

## B. Spell static effects follow the printed subject (R-085)

After #928 a Spell on a Personality who is not currently a Shugenja provides no actions. Mendon's ruling for the always-on text: where the Spell's printed static text names the Shugenja host as the subject ("This Shugenja has ...", "While this Shugenja is Water, he ...", his destruction or bowing), the effect is silent while the host is not a Shugenja; where it says "This Personality", it is not. The Spell stays attached; the effect returns when the host is a Shugenja again (including by a granted keyword). A static whose subject is not the host is left alone; those are listed for Mendon below and nothing was guessed.

Engine: `ShugenjaSubjectStaticSpells` (CardCopy.cs) lists the Shugenja-subject Spells by id; `SpellStaticSilenced`, `StaticSpells(host)` and `GrantedKeywordLive` are the shared gate (the same live `Keyword(host, "Shugenja")` test as `AbilityIdsOf`). It is applied once at the shared mechanisms (keyword rules, standing immunities, element replacements, `AttachGrantsKeyword` grants, the Elite-effect grant) and at the Force/Chi readers that previously read `Attachments(...)` directly. Tempest of Flame ("This Personality has +2F") used to require a Shugenja; that gate was removed.

### Audit of every published Spell with a static effect

| Spell | Printed static text | Subject | Class | Engine path |
|---|---|---|---|---|
| Suitengu's Path | This Shugenja has Naval. | This Shugenja | silenced | AttachGrantsKeyword grant |
| Bo of Water | While this Shugenja is Water, he contributes Force even if bowed. | This Shugenja | silenced | EliteEffect |
| Chikushudo's Infusion | This Shugenja has Cavalry while he is Shadowlands. | This Shugenja | silenced | keyword rule |
| Drawing on All Things | This Shugenja has the element keywords of every Ring you control. | This Shugenja | silenced | keyword rule |
| Scorching Lash | This Shugenja has Duelist while he is Fire. | This Shugenja | silenced | keyword rule |
| Death, Defeated | This Shugenja has +2F and Shadowlands. | This Shugenja | silenced | PersonalityForce, AttachGrantsKeyword |
| Immolation | This Shugenja has +3F, or +4F while he is Fire. | This Shugenja | silenced | PersonalityForce |
| Communion with Earth | This Shugenja has Stalwart. | This Shugenja | silenced | AttachGrantsKeyword grant |
| Strength of the Tsunami | This Shugenja has +6F. | This Shugenja | silenced | PersonalityForce |
| A Dragon's Favor | While this Shugenja is Thunder, he has +1C. | This Shugenja | silenced | PersonalityChi |
| Ember's Final Fire | While this Shugenja is Fire, he has +1F. | This Shugenja | silenced | PersonalityForce |
| The Swell of Strength | This Shugenja has +2F. | This Shugenja | silenced | PersonalityForce |
| Pearl of Rage | This Shugenja has +1F for each Pearl card ... | This Shugenja | silenced | ForgottenLegacyBatch5Force |
| The Mountain's Power | While this Shugenja is Earth and defending, negate his destruction ... | This Shugenja (his) | silenced | DestructionShielded hook |
| Wind of the Moon | While this Shugenja is Air, he straightens during each player's Straighten Phase. | This Shugenja | silenced | Straighten hook |
| Thunder's Stamina | This Shugenja has +3F. | This Shugenja | silenced (first sentence only) | PersonalityForce |
| Warded Paths | While this Shugenja is defending, he has +3F. | This Shugenja | silenced (first sentence only) | PersonalityForce |
| Mara's Touch | While this Shugenja is Nonhuman, he has +2F. | This Shugenja | silenced | PersonalityForce |
| Ochiai's Bravery | While this Shugenja is Fire, he has Duelist. | This Shugenja | silenced | Keyword hook |
| Might of the Sun | This Shugenja has +4F. | This Shugenja | silenced | PersonalityForce |
| Channeling the Fallen | This Shugenja has +5F. | This Shugenja | silenced | PersonalityForce |
| The Wave Persists | While this Shugenja is Water, he has +2F. | This Shugenja | silenced | PersonalityForce |
| Poison the Cup | This Shugenja has Shadowlands. | This Shugenja | silenced | keyword rule |
| Set the Fields Aflame | While this Shugenja is Air or Fire, he has +1F. | This Shugenja | silenced | PersonalityForce |
| The Earth's Armor | While this Shugenja is defending, other players' cards will not bow him. | This Shugenja (him) | silenced | standing immunity |
| Blood of Isawa's Tribe | ... replace all instances of that keyword on this Shugenja ... | This Shugenja | silenced | element replacement |
| Scouring Flood | This Personality has +2F. | This Personality | unchanged | PersonalityForce |
| Tempest of Flame | This Personality has +2F. | This Personality | unchanged (a former Shugenja gate removed) | PersonalityForce |
| The Thunderer's Protection | This Personality has +4F. | This Personality | unchanged | PersonalityForce |
| Incite the Phantoms | While this Shugenja is Earth, your Spirits contribute Force even while bowed. | not the host (your Spirits) | ambiguous, unchanged | EliteEffect |
| Channel the Moon | While this Shugenja is Shadowlands, your opposed Nonhumans have +1F. | not the host | ambiguous, unchanged | Force hook |
| Erosion | While this Shugenja is Water, other players' provinces have -1 strength. | not the host | ambiguous, unchanged | province strength |
| Drawing on the Mountain | While this Shugenja is Earth, your provinces have +1 strength. | not the host | ambiguous, unchanged | province strength |
| The Tapestry Perceived | While this Shugenja is Void, your maximum hand size is increased by 1. | not the host | ambiguous, unchanged | hand size |
| Conflagration | While this Shugenja is Fire, Ranged Attacks from actions he performs have +1 strength. | attacks he performs | ambiguous, unchanged | attack strength |
| Thunder's Stamina (2nd sentence), Hitomi's Devotion | ... cards in this unit, if/while this Shugenja is Thunder/Earth ... | the unit | ambiguous, unchanged | battle resolution |
| Warded Paths (2nd sentence) | While this Shugenja is Earth and you are the Defender, you decide the order ... | the player | ambiguous, unchanged | battle order |
| Illusory Defense, Opening the Veil | Your Air Shugenja have +1C; Your Stronghold has +1 Gold Production ... | no host reference | unchanged | Chi / Gold hooks |

The remaining Spells have only actions or entering-play triggers (silent on a non-Shugenja since #928) or attach/cost text about the card, not an effect on the host. `Rulings936Tests` pins the list against the printed text ("This Shugenja has" must be listed, "This Personality has" must not).

`ContentVersion`: a bump is advisable at the next bump (static bonuses of the Shugenja-subject Spells on a non-Shugenja host, Tempest of Flame on a non-Shugenja, and the Great Sacrifice target set change for existing journals); this branch does not bump it.
