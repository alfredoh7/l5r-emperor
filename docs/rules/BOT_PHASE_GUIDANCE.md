# Bot guidance: Battle/Open cards (issue #998)

Mendon's playtest note: the practice bot spends cards in the Open phase that do little or nothing, when the Battle use is better (Soshi House Guard is the example). This is the survey behind the fix and the general guidance it implements.

## What the engine has

- Timing is a plain string on `AbilityDefinition`: `Battle` (~1010 definitions), `Reaction` (~421), `Limited` (~386), `Open` (~372), `BattleOrOpen` (~123), plus `FocusEffect` and `BeforeResolution`.
- **There is no combined "Battle/Limited" timing.** `Limited` is legal only in the Action phase (the Open phase) for the active player. `BattleOrOpen` is legal in the Action phase or the Battle phase. "Battle/Open" appears only as printed card text. Usage limits (`UsageKey`, `UsesPerTurn`, default once per turn) are separate from timing, so spending a once-per-turn `BattleOrOpen` use in the Open phase forfeits the battle use that turn.
- Phase for a use is `Phase.Action` (Open) or `Phase.Battle`. The bot already read abilities only from their printed label, target category and the public view (`AbilityIntent`).

## The `BattleOrOpen` abilities, by what they do

| Group | Examples | Open phase vs Battle |
|---|---|---|
| Straighten (the largest, ~40) | `negishi-straighten`, `immovable-object-straighten`, `ring-of-air-straighten`, `cycle-of-vengeance-straighten` | Useful in either phase **only if the target is bowed** (often best before attack declaration). A no-op on a straight card. |
| Bow / bow-and-honor | `soshi-conditional-bow`, `phantom-visage-bow`, `osawa-bow` | A plain bow works in both phases. Soshi bows only "if it is a Combat Segment". |
| Force boost / penalty | `kunji-boost` (+2F), `heiwa-bonus`/`heiwa-penalty`, `suwabe-force`, `of-one-instant-boost` | Matters only in a battle, where the odds and the target are known. |
| Honor | `asako-house-guard-force-*` (Force effect **and** gain 1 Honor), `asahina-straighten` (straighten and gain 1 Honor) | The Honor payoff is real in either phase. |
| Destroy | `gold-and-steel-destroy`, `brawl-destroy`, `broken-alliance-destroy` | Permanent either way. |
| Recruit, equip, draw, move, province strength, extra-action grants | `essence-of-undeath-recruit`, `foothills-equip-weapon`, `genji-province-raise` | Phase-independent. |

Soshi House Guard (`follower-wave-3`, ability `soshi-conditional-bow`): "Target a Personality: if it is a Combat Segment, bow him; if he is dishonorable, his controller loses 1 Honor." In the Open phase the bow is skipped, so against an honorable Personality it **does nothing**; against a dishonorable one it only costs them 1 Honor. In a battle it bows an opposed Personality, which is far stronger. It also targets *any* Personality, so an own dishonorable target would cost the bot Honor.

## General guidance (implemented in `TrainingOpponent.PhaseChoice.cs`, flag `UsePhaseChoice`)

1. **Never use something that does nothing, in any phase.** The bot decides this only for effects it can read exactly: a bow on a bowed card, a plain straighten on a straight card, an Honor loss that needs a dishonorable target the target is not, and a "Combat Segment only" bow used in the Open phase. Any other wording, or an unknown target, is left to the ordinary rules (it never guesses a card is pointless).
2. **In the Open phase, hold an ability whose effect matters only in combat** (a bow the card limits to a Combat Segment, or a Force change) **and that has no payoff of its own outside combat** (gaining Honor, drawing). The card is used in the Combat Segment instead, where the bot knows the odds and what the target is doing.
3. **Phase-independent effects keep the ordinary scoring in both phases:** straightening a bowed unit, Honor, drawing, recruiting, equipping, plain bows and destroys.

## Proposals not implemented

- **Timing by value, not by text.** A Force boost in the Open phase could be right when it lets the bot declare an attack it could not otherwise; today the attack planner already counts boosts in hand, so holding is safe.
- **Once-per-turn budgeting.** A held use is lost if no battle happens that turn. The bot could release a held use at the end of the Open phase when it will not attack and expects no attack.
- **Strategies.** Strategy cards with a `BattleOrOpen` printed ability go through the same rule, but Strategies that only declare their timing in code (for example Force-three cards) are not read for phase.
- **Conditional effects beyond Soshi's wording** ("if he is Unaligned", "unless ...") need a small effect-condition model; the current reading handles bow, straighten and "if dishonorable" Honor loss only.
- **Measurement.** Neither the unopposed gate (#997) nor this rule has been measured in the bot arena; both are behind flags (`UseUnopposedGate`, `UsePhaseChoice`) so the arena can compare them.
