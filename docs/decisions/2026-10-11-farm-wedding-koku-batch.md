# Well-Tended Farm, Cross-Clan Wedding, Caught Unawares, One Koku, The Duty of the Crab, Stone of Remembrance, Shrine to the Heavens, Minor Blackmail, The Onyx Championship, Benten's Blessing and Jizo's Mercy (issue #829)

Status: **accepted** (Mendon's rulings of 2026-10-11, recorded as R-054 to R-062). Lane: rules, with a client multi-select panel, a card-tear animation and a deck-builder count. Engine: `src/L5R.Core/Wave829.cs`; waves `holding-wave-829`, `event-wave-829`, `strategy-wave-829` and `celestial-wave-829` (`tools/content/package_wave_829.py`).

| Card | Wired as |
|---|---|
| Well-Tended Farm (oracle-l5r-9292) | printed production 1 Gold; `w829-farm-honor` (Limited, bow: gain 1 Honor) |
| One Koku (oracle-l5r-5709) | a payment token for a Strategy in hand (`PaymentTokens`), discarded when chosen; `w829-koku-gold` |
| Shrine to the Heavens (oracle-l5r-7229) | printed production 2 Gold; three P6 Celestial-entry decisions (`CelestialEntryRegistry`), one per Shrine in play, asked only for a Celestial revealed in a Province |
| Cross-Clan Wedding (oracle-l5r-1576) | a P5 province-reveal Event; the second effect is the other player's decision (`w829WeddingGive`) |
| The Onyx Championship (oracle-l5r-8242) | a P5 province-reveal Event that permanently grants a Shugenja Shadowlands and `w829-onyx-maho` |
| Minor Blackmail (oracle-l5r-5061) | a P5 province-reveal Event that sets an untimed watch (`BlackmailWatch`) |
| Caught Unawares (oracle-l5r-1239) | `w829-caught-open` (a 1 to 2 target slot, a "no Personalities of yours" condition, a this-turn straighten lock, a conditional draw); `w829-caught-battle` (a new target filter) |
| The Duty of the Crab (oracle-l5r-8036) | `w829-duty-focus` (a delay Focus Effect) and `w829-duty-battle` (recruits from a province or the discard pile at the current battlefield) |
| Stone of Remembrance (oracle-l5r-7501) | `w829-stone-negate` (a targeted-by-action Reaction); zero cards toward the deck minimum; the `cardTorn` event and a client animation |
| Benten's Blessing (oracle-l5r-959) | a multi-select decision on entry (`w829BentenChoice`, Sleep's three actions), a live +1 Personal Honor term, and `w829-benten-redirect` |
| Jizo's Mercy (oracle-l5r-10563) | `w829-jizo-home` (Battle: move a target attacking Personality home) and `w829-jizo-mercy` (a per-battlefield mark read by the battle resolution) |

## What Mendon ruled (2026-10-11)

See R-054 to R-062 in `docs/rules/RULINGS.md` for the wording. In short: the Wedding is two separate resolvable effects; Caught Unawares needs five small primitives; Stone of Remembrance counts as zero cards, reacts to any action that would destroy a card of its controller, and is torn in half on screen; each Shrine protects one Celestial per Celestial revealed, checked only at the reveal; Minor Blackmail is an untimed watch on performing or being assigned (never on being targeted); The Onyx Championship's grant is a plain recruit from the discard pile (dead Oni included); Benten's Blessing is a multi-pick over Personalities in play, discarded, dead or face-up in a Province, and the +1 PH counts in a Proclaim; Jizo's Mercy replaces the Province's destruction with 4 Honor and leaves every other effect of the resolution.

## What was built

- **Primitives for Caught Unawares:** a "you control no Personalities" check (`Wave829Validate`), a slot of one to two targets, `LockStraightening` (the existing per-turn lock: a card that may not be straightened this turn, by effect, until the turn ends; the "next straightening" one-shots are not used), the draw when it is not your turn, and a target category "an enemy card with no unbowed attachments" (`TargetAllows`).
- **P5 Events** use `ProvinceRevealEvents`. They stay face up in their province and fire at the start of each of their owner's turns while they are there, so each firing asks again (this is the P5 design). The Wedding's second decision is opened inside the answer and the Events Phase resumes when it is answered (`RunAfterProvincesRevealed` waits while it is open).
- **Shrine to the Heavens** adds up to three P6 entry choices (the hook names `Wave829Shrine1..3`; Holdings are limited to three copies), each sparing a Celestial from the discard (`SparesFromDiscard`). The Celestial entering play is marked as "revealed in a Province" only when it comes through `ResolveCelestialCore`, so a Celestial put into play with no Province (the Sacred Rosary) asks nothing.
- **Benten's Blessing** keeps its selection in the engine (`Wave829State.BentenSelected`) like Sleep does: one command per click on a card or a row (`budgetSelectAdd` toggles it), `budgetSelectClear`, `budgetSelectConfirm`. The client's multi-select panel (`Main.BudgetSelect.cs`) renders both; its rows now highlight the card on the table, and a click on a table card toggles it. The decision opens after the command that revealed the Celestial. The +1 PH is a live term in `CardPersonalHonor` (`StaticPersonalHonorBonus`) and in the Proclaim's Honor gain.
- **Stone of Remembrance** emits `cardTorn` when its Reaction is used; `CardTear` (client) draws the previous frame's card split along a jagged seam, the halves falling apart, before the card is gone. With reduced motion there is no animation. `DeckWorkshop.CountsTowardMinimumOf` makes it count as zero toward the 40-card minimum (the deck builder and custom practice).
- **Jizo's Mercy** marks the battlefield for the turn; `ContinueResolveBattle` asks `Wave829JizoReplacesDestruction` where the Province would be destroyed.
- **The bot** gives away the weakest Personality for the Wedding, names the strongest enemy for Minor Blackmail, ticks only its own Personalities for Benten's Blessing, and never removes one it ticked.

## Readings for Mendon to confirm

1. **Shrine to the Heavens, repeated choices.** Each Shrine's question lists the Celestials in play; a Celestial already named by an earlier Shrine is still listed, and naming it again wastes that Shrine. Three Shrines are supported (the deck limit).
2. **Shrine, "revealed in a Province".** Only a Celestial that enters through the Events Phase reveal (or another reveal that uses `ResolveCelestialCore`) asks. A Celestial put into play by another card does not.
3. **Cross-Clan Wedding.** "Another player" is the opponent (two-player game). The Event fires every one of its owner's turns while it is face up; every firing asks again. If the first give is refused (Loyal, or a duplicate Unique) there is no second effect; a refused second give does nothing more.
4. **Caught Unawares.** "You control no Personalities" is judged when the action is played (home or at a battlefield). The targets are any Personalities in play, either player's. The straightening lock applies only to a target that was actually bowed (if the bow is negated nothing is locked).
5. **The Duty of the Crab.** The Crab Personality is the action's target (province or discard pile, never the dead pile or a face-down province card); this offers both sources in one action. He is not treated as the performer, so a card that watches performers does not see him. "Units at the current battlefield" means a Personality of yours there.
6. **Stone of Remembrance.** It reacts when another player's action targets a card of its controller's that the action could destroy (an Attack, or a catalogued destroy action, including destroying an attachment). It cannot protect itself.
7. **Minor Blackmail.** Each firing of the Event adds a watch; a watch fires once and stays until it does, whatever happens to the Event. "Performed" is read from the resolved action's performer, or the Personality an attached Item, Spell or Follower acts through. Assignment fires on either side.
8. **The Onyx Championship.** The recruit comes from the owner's discard pile, dead Oni included; the "then deck, then Provinces" search is dropped as Mendon ruled. The destroyed Personality is paid as a cost; the 3 Honor are lost after the Oni enters. Each firing may give another Shugenja the grant.
9. **Benten's Blessing.** The candidates are both players' face-up Personalities in play, in the discard piles, in the dead pile, or face-up in a Province (not a hand, a deck or a face-down Province card). Two Benten's Blessings in play both count. The redirect names another Personality of its controller's, legal for the action, as the "performing" Personality.
10. **Jizo's Mercy.** The 4 Honor are gained only when the Province would otherwise have been destroyed; the mark lasts until that battle resolves or the turn ends.

## Not covered by a test

- The client's rendering of the multi-select panel for Benten's Blessing and of the card-tear animation on a native display (the Core tests show the actions and the event; the client compiles).
- The Wedding's second decision across a saved game.
