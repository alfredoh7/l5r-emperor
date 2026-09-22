# The delayed "move home" effect, the cards the delayed effects complete, Steady Resolve, Akodo Dairuko and Kyuden Hida - exp

Status: accepted for implementation; the rules lead's answers to the open questions of `2026-09-19-battle-rulings-round-2.md` ("build delayed move-home, yes to packaging Steady Resolve and Akodo Dairuko, yes to Stronghold Kyuden Hida - exp").
Lane: shared (rules engine plus published content; no client or view contract change). Issue: #364. Builds on the delayed-effect primitive of #354 (`RegisterDelayedBattleEffect`, `BeforeResolution.cs`).

## 1. The delayed "move home" effect (`RegisterDelayedBattleEffect(..., "moveHome", target, thenMoveToBattleId: card)`)

The kinds of a delayed effect were bow, straighten, destroy and modifier. A fifth, **moveHome**, moves the target home when its turn in the one ordered before-resolution set comes, and may carry one **dependent second move**: "and if he moved, move this Shugenja to the current battlefield" (The Wave Persists).

- **"If he moved."** He moved only when he was at a battlefield when his turn came (a target already home, or gone from play, moved nothing) and the move was not negated (`MoveHome` keeps its negation check). When he did not move, the dependent card does not either. A target that left play is skipped like every delayed effect; a dependent card that left play is simply not moved.
- **Order.** The target goes home first and only then does the dependent card move to the current battlefield; both precede the Force comparison, like every member of the set. The dependent card arrives as a whole unit (`MoveToBattlefield`: attachments travel).
- **No after-action window.** A move the set applies is not "after the resolution of an action", so the move does not queue Legion of the Bat's window (`PendingMoveReactionCardId` is cleared).
- The record is plain state (`DelayedBattleEffect.ThenMoveId`), so a snapshot mid-battle round-trips; it lapses with its battle, applied or not.

## 2. The cards the delayed effects complete

Every text, Gold Cost box and Focus below was read from the card's own scan (the local WebP working copy, sha256-checked against the committed receipt).

| Card | Ability (published) | Wired? | What it needed |
|---|---|---|---|
| **The Wave Persists** (9890, Spell, spell-wave-9) | `wave-persists-move`, Effect `wave-persists-force` | **yes** | the moveHome delayed effect; a Reaction in the Engage window (trigger `after-engaging`) from an attached Spell whose Shugenja is not at the current battlefield; target category `own-personality-at-battle` ("your Personality there"); the static +2F (`CardForce`: only while the wielder is Water) |
| **Holding Cells** (10733, Strategy) | `holding-cells-bow` | **yes** | "Kolat Battle, 4 Gold": the 4 Gold is the ACTION's own cost (`Cost: Fixed(4)`; the card has no Gold Cost box) and "Kolat" its action keyword (`ActionKeywords`), not a keyword of the card; delayed bow; "Lose 1 Honor" is the acting player's own, at once |
| **Power of the Night** (9920, Strategy) | `power-of-the-night-chi` | **yes** | performer: an unbowed Ninja at the battlefield, bowed as the action resolves unless he is Shadowlands (clan or keyword); delayed -3 Chi on the target, a Chi of 0 destroying him through the existing modifier rule |
| **Quick Recovery** (6132, Strategy) | `quick-recovery-straighten` | **yes** | target category `bowed-card-in-performer-unit` (the performer or one of his attachments); delayed straighten |
| **Reckless Rush** (6211, Strategy) | `reckless-rush-bow` | **yes** | the performer bows as the cost; the target is an "enemy card without attachments" (the engine's existing category: a Follower or an unattached Personality), bowed, and destroyed if it is an attachment or its base Gold Cost is 0 (the two are independent of whether the bow was negated); delayed straighten of the performer |
| **Harsh Taskmaster** (10151, Strategy) | `harsh-taskmaster-attach` | **yes** | `AttachPile.OwnHandOrDiscard` (a Follower in YOUR hand or your own discard pile, attached to the performing Commander ignoring costs: no Cost rule, Equip legality still applies); delayed destroy; an additional Battle action for the acting player |
| **The Tale of the Disgraced** (10553, Strategy) | -- | **no** | it names TWO targets in one action (your unbowed Personality and an enemy card) and the engine offers one: an action option lists the legal targets of a single choice, so the second could only be typed in by a hand-built command. The delayed conditional straighten (Fallen or Magistrate) and the keywords exist. Needs the general multi-target selection (PR #348: action-lock, multi-target, move destinations); then it is one ability and the package, and its scan is already in the local cache |

Readings recorded: Reckless Rush's "enemy card without attachments" is the same category Masumi, Kojinrue and Xijkt use, so an Item or Spell attached to a Personality is not offered as a target (the Personality is, when unattached; the attachment is destroyed with it); Harsh Taskmaster's attached Follower may open its own "after entering play" window, in which case the additional action's grant waits behind that window (a limit the Equip (Battle) follow-up already has).

## 3. Steady Resolve and Akodo Dairuko, packaged

Their engine abilities (`steady-resolve-boost`, `dairuko-rally`) were built and proven through training cards in #354; this change publishes the real cards and adds the one missing ability.

- **Steady Resolve** (9930, strategy-wave-27): Cost 0, Focus 2; the printed Reaction as-is.
- **Akodo Dairuko** (200, personality-wave-38, the Emperor Edition printing): Force 6, Chi 5, Cost 10, Honor Requirement 10, Personal Honor 5, keywords Lion Clan, Clan Champion, Samurai, Paragon, Tactician, Loyal, Unique. The scan reads "Reaction: Even if Dairuko is bowed, before the resolution of a battle at her battlefield: Dairuko rallies her troops. That battle's resolution will not bow cards in your army. Battle: Melee 8 Attack." (the catalogue's current text calls the first line the Tireless keyword, which this printing does not carry; the published text is the scan's). `dairuko-melee` is a Melee 8 Attack (destroys a Personality of Force 8 or less; 9 is out of reach).

## 4. Kyuden Hida - exp (4638, stronghold-wave-8)

**Identity.** The catalogue has exactly two Kyuden Hida cards: the Stronghold `oracle-l5r-4638` ("Kyuden Hida - exp", the Experienced Stronghold this change wires) and the Event `oracle-l5r-6200` (Rebuilding Kyuden Hida). The relayed note that 4638 was "the base Stronghold" does not match the data: 4638 IS the Experienced one (keyword Experienced, the Recon Reaction below), and no base Stronghold of that name exists in the catalogue. The Emperor Edition printing (#394) is shipped; the Before the Dawn reprint has the same words.

**Text.** "Recon Reaction: After engaging: You have Reconnaissance at the current battlefield. Create and attach a 0F Scout Follower to each of one to three of your Personalities in your current army. Remove those Followers from the game after the battle ends." Province Strength 8, Gold Production 4, Starting Honor 3.

- `kyuden-hida-exp-scouts` is a Recon Reaction (ActionKeywords `Recon`, so Ebizo and the other Recon watchers see it) in the Engage window (trigger `after-engaging`), used by whoever controls the Stronghold, for their own army; it grants Reconnaissance at the current battlefield (existing primitive).
- The choice is one to three different Personalities of the acting player's army at the current battlefield (a new Target category, `own-army-personalities`). Each army member is a candidate `TargetId` on its own -- what the current client sends when a card is clicked, so the card is playable for one Scout today -- and two or three are named together on `CardIds` (shaped like Border Keep's province choice; naming both a target and a list is refused). A client multi-select for the second and third is UI work on the existing command contract, not a rules change.
- **The created Follower.** No printing exists for a "0F Scout Follower", so it is the scenario-only definition `created-scout-follower` (Follower, Force 0, keyword Scout): never in a deck, never counted as published, and it has no art (the client shows a stat card for it). Each is a new card owned by its host's controller, attached to him (`CreateAttachedFollower`), and tracked in `MatchState.CreatedUntilBattleEndsIds`; `FinishBattleResolution` removes every tracked card from the game, wherever it stands (attached, or already in the discard pile with a destroyed host). It has no "after entering play" text, so no entry window opens.

## 5. Scans and packaging

- **Nothing was downloaded.** The nine scans were already in the local reference cache (`assets/reference/oracle/<set>/webp`, gitignored); each was copied into this worktree only after its sha256 matched the committed receipt (`working_copy.sha256`), and each was read by eye against the printed text, cost box and Focus. The wave sources files record the printing, the image path and the sha256 exactly as earlier waves did. The runtime art is `src/L5R.Client/Content/Published/oracle-l5r-<id>.jpg`, as always. (The approval was to download only Steady Resolve and Akodo Dairuko from the same source earlier waves used; the receipt-verified local copies made even that unnecessary.)
- `package_vanilla_wave.build_definition_fields` gained an optional `prefer_slug` so a wave can name the printing to ship when a card has several (Dairuko and Kyuden Hida: Emperor Edition; the default is unchanged).
- Wave numbers are the next free per type on origin/main (03fa444a) plus 4, so they cannot collide with the open PRs: strategy-wave-27 (main's highest is 22), spell-wave-9 (4), personality-wave-38 (33), stronghold-wave-8 (3).
- The pinned published-card count is 431 (422 on origin/main, 9 new: six Strategies, one Spell, one Personality, one Stronghold), recounted from the distinct non-ScenarioOnly ids across `content/published/*.json`; PR #361 removes the hard-coded number, and whichever lands second takes its version.
- `BLOCKERS` in `tools/content/build_card_inventory.py` lost the eight wired cards and now states The Tale of the Disgraced's gap; `docs/CARD_INVENTORY.xlsx` is regenerated (431 wired).

## Tests

`delayed-move-home-wave` (11), `kyuden-hida-exp` (6), plus the updated pins (`battle-timing` and `recon-action` Recon-carrier inventories, the round-2 Kyuden Hida separation test, the published count). Mutation checks, each made the intended tests fail and was reverted: the dependent move happening when the target did not move; the +2F ignoring Water; The Wave Persists usable with the Shugenja at the battlefield, or aimed at any Personality; Holding Cells costing no Honor, or bowing at once; Power of the Night bowing a Shadowlands performer, or giving -2C; Quick Recovery reaching any bowed card; Reckless Rush not straightening, or destroying only attachments; Harsh Taskmaster granting no action, never destroying the Follower, or taking any player's hand or discard pile; Dairuko's Melee at 7 or 9; Kyuden Hida granting no Reconnaissance, never removing its Scouts (or only while attached), allowing four Personalities, or ignoring a click's single TargetId. One further mutant, removing the ownership test from the multi-select, survives by construction: the army lookup (`Army(player, province)`) already returns only the acting player's own units, so the test was redundant.
