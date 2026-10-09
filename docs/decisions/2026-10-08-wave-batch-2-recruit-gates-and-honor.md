# Wave batch 2: recruit gates, Proclaim and Honor (issue #733)

Status: **implemented on `claude/733-wave-batch-2`; the readings below await Mendon.** Lane: rules (mdornbrook, standing cross-lane authority). Batch 2 of [the wirable-card wave plan](2026-10-07-wirable-card-wave-plan.md) (umbrella #742). Engine: `src/L5R.Core/Wave733.cs`. Packager: `tools/content/package_wave_733.py`. Tests: `tests/L5R.Core.Tests/Wave733Tests.cs` (suite `wave-733`, 12 tests).

## What is wired

12 of the batch's 16 cards, in four profiles: personality-wave-151 (8), follower-wave-63 (1), strategy-wave-135 (2), stronghold-wave-47 (1). Each card was read against its own scan; the scan wins where the catalogue text differs (Invocation prints "Honor gains are reduced by 1 unless the player controls a Spirit").

| Card | Built as |
|---|---|
| Asp Skirmisher, Suzume Shindo, Ikoma Natsu - exp, Tsi Hayamizu, Ichiro Otani | the P3 Effects `will-not-join-human-controller`, `-spider-player`, `-unicorn-player`, `-scorpion-player` and `will-not-enter-honor-nonnegative` |
| Bayushi Hamada | the P3 Effect `crab-players-may-control` (he is Loyal to the Scorpion); +1F for each of your Personalities who is Crab Clan or a Courtier (`EngineHooks.PersonalityForce`) |
| Tsi Hayamizu | +1F for each Mine you control (the same hook) |
| Yoritomo Yashinko - exp | the P3 Effect `crane-players-may-proclaim`; ":bow: Produce 3 Gold" as a bowing payment token (P1) |
| Ikoma Natsu - exp | `w733-natsu-cavalry` (Open: GrantKeyword Cavalry for the turn) |
| Suzume Shindo | `DuelRuleRegistry` (P18): `FirstToFocusPlayerId` when he is the challenger |
| Ichiro Otani | `w733-otani-duel` (a Battle duel of Force, P18 `ExcludeItemBonuses`); the loser is bowed |
| Kimura, Forgiver of Sins | an entry cost term (Ring of Air: ignore Gold Cost); `w733-kimura-proclaim` (Air Limited, discard a card as a cost) |
| Court Scribe | `w733-scribe-honor` (Political Open, bow) |
| Invocation | `w733-invocation`: a P14 each-gain modifier of -1 with the `unless-controls:Spirit` filter, through the opponent's turn |
| Delicate Gamble | `w733-gamble`: bow your target unbowed Courtier, discard the top card of the other player's Fate deck, then a branch of four options written at resolution |
| The False Path | the Stronghold: two P10 keyword rules, an entry term, a P3 recruit gate, a P14 Honor-change hook, `EnlightenmentBarred`, and `w733-falsepath-search` |

## Not wired (4), with blocker notes in `tools/content/pe_wave_733_blockers.json`

None is published, so no printed effect is dropped.

- **Zansho - exp** needs a ruling. "Bow Zansho's target unbowed Follower" does not say whose Follower: any unbowed Follower at the battlefield (the other player's too), only the controller's, or one attached to Zansho. They are three different cards.
- **Merchant Patronage** is a Political Interrupt before any action on another player's Holding, and its player chooses who takes the Honor change. The before-action windows only predict movers and a few curated ids (P8), and nothing asks the acting player at that point.
- **The Heavens' Blessing**: "No player can gain Honor in Action Phases (this turn)" is a P14 modifier, but "Until the game ends, a player must pay 2 Gold to draw a card from effects of his own Strategies during an Action Phase" has no home (`Draw` has no source, and a declinable payment in the middle of an effect has no decision). Publishing only one sentence would drop printed text.
- **Marriage of the Emerald Champion**: the pieces exist (`KeepEventInPlay`, a P11 dishonor bar, a P14 `targeted-by-other` filter) but the Event's target is chosen at reveal and the Event then leaves its province for play. `ResolveEventChoiceCommand`'s default arm answers only P5's Events that stay in their province, so a new kind needs one hook there and a record binding the Event to its target.

## Shared lines edited

- `src/L5R.Core/Dueling.cs`, `ApplyDuelOutcome`: `Wave733Abilities.OtaniDuel` joins the label group of "Bow the duel's loser" (First Blood, Watchful Eye).
- `src/L5R.Core/RuleFlags.cs`, `EnlightenmentBarred`: one more term, `Wave733EnlightenmentBarred` (a Stronghold is in play from setup, so no flag can be set for it).
- New persisted state: `MatchState.Wave733HonorBoosts` (`JsonPropertyOrder` 22910), omitted until a Kimura or Court Scribe boost is stored.

## Readings for Mendon to confirm or correct

1. **Hamada and Hayamizu** count live: Personalities of the controller in play (bowed or not) that are Crab Clan or a Courtier, and Holdings in play with the Mine keyword. A card that is both counts once. In a province they show only their printed Force.
2. **"A Human Personality" (Asp Skirmisher)** is P3's reading: a Personality without the Nonhuman keyword. All the "will not join" refusals refuse a Recruit and a Reserve recruit and are judged at the Stronghold Clan, as P3 built them.
3. **Natsu, "Give a target Personality Cavalry"** is a Turn-long grant on any Personality in play, either player's; Open, no bow.
4. **Shindo** has the right in any duel in which he is the challenger, whatever issued it; it only decides who acts first, and the alternation after that is unchanged.
5. **Otani** challenges as himself (no separate performer). "Items do not give bonuses or positive Modifiers to Force" is P18's `ExcludeItemBonuses` for both duelists' Items; positive Modifiers other than Items' are not excluded. Every loser is bowed (a tie has none); a Stalwart loser is not.
6. **Kimura's boost** applies to the controller's next Proclaim this turn and is spent by it. "Increase ... to equal the card's Focus Value" never lowers a gain: a Focus Value at or below the usual gain changes nothing (and the boost is still spent). The discard is a cost and its Focus Value is read with its modifiers.
7. **Court Scribe**: "from your cards" is a change whose source card is owned by the Scribe's controller, to either player's Honor; a rulebook change with no card, and a Proclaim, are not from his cards. The boost is the next such change this turn, of either sign, one larger; a change of 0 is not counted. It applies before the Honor-change window so a Reaction played there sees it. This differs from P14's own `own-cards` filter (the source's owner is the affected player), which would miss a card of his that makes the other player lose Honor, so it is stored in its own list.
8. **Invocation** reduces each gain by 1 (floor 0) for every player who does not control a card with the Spirit keyword, Proclaim gains included, through the opponent's next turn (it lapses when the turn after that begins). Losses are untouched.
9. **Delicate Gamble**: "another player's" Fate deck is the opponent's. The discard comes first, so the acting player chooses knowing the card: either player, gain or lose, for the Focus Value or 3, whichever is lower. A discarded card with Focus 0, or an empty deck, asks nothing. "He lost Honor" is that his Honor went down, so a loss reduced to 0 by another effect gives no waiver. The waiver is the same game-long flag as P3's "ignore Honor Requirements".
10. **The False Path**:
    - "Your non-Unique Monk and Fudo Personalities in and out of play have Monk and Fudo" is two P10 rules (a Monk gains Fudo, a Fudo Personality gains Monk), for the Stronghold's controller only, in a province or in play.
    - "Fudo Personalities join you for 2 less Gold" and "you ignore Honor Requirements of your Fudo cards" apply to a Recruit and a Reserve recruit of a Personality with Fudo, printed or given by the Stronghold. A Fudo Item or Follower has no Honor Requirement to ignore.
    - "You do not lose Honor from Fudo cards you own" is a loss whose source card is a Fudo card owned by the affected player; another player's Fudo card still costs Honor.
    - "You may not win by Enlightenment" holds for its controller only.
    - The search takes the controller's Fate discard pile first, then the Fate deck, as a Mendon R4 ordered search does; Rings are Fate cards.

## Client contract

No new command, option field or view field. The Gamble choice uses the existing `effectChoice` decision with four listed options; the searches use the existing search flow; Yashinko's token uses the `"bow-reaction"` kind string P1 named.

## Art pack

The 12 new scans need catalogue-art coverage. The local candidate extends the published pack `catalogue-art-issue732-20261007-r1` (4,478 images): tag `catalogue-art-issue733-20261008-r1`, 4,490 images, 600,844,776 bytes, SHA-256 `53c271eb4bd0d64c11badada099a7d809206dc59ad0d1718af091525e7433de2`. Built with `tools/content/build_catalogue_art.py --base-archive`; it is published before this branch merges (the PR records the release link and the clean-download check).
