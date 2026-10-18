using System.Text.RegularExpressions;
using L5R.Core;

namespace L5R.AI;

// Issue #998 (Mendon's playtest note): cards that can be used in the Open phase or in a Battle (timing "BattleOrOpen") were spent in the Open phase even when that did little or nothing,
// which wastes the card and the player's time. The bot reads only the ability's printed effect text, its target and the public view:
//   1. It never picks a use that does nothing, in any phase: a bow on a unit that is already bowed, a straighten on a unit that is already straight, an Honor loss that needs a
//      dishonorable target the target is not, and a bow that only happens "if it is a Combat Segment" used outside one.
//   2. In the Open phase it holds an ability whose effect matters only in a combat (a bow the card limits to a Combat Segment, a Force change) and that has no payoff of its own outside
//      combat (gaining Honor, drawing), so the use is made in the Combat Segment, where the bot knows the odds and what the target is doing. Effects that matter in either phase (a
//      plain bow or destroy, straightening a bowed unit, Honor, cards) are left to the ordinary rules.
public static partial class TrainingOpponent
{
    private static readonly Regex CombatSegmentOnly = new(@"\bif it is a combat segment\b", RegexOptions.IgnoreCase);
    private static readonly Regex HonorIfDishonorable = new(@"\bif (?:he|she|it|the target) is dishonorable\b[^.;]*?\bloses?\s+(?:\d+|X)\s+honor", RegexOptions.IgnoreCase);
    private static readonly Regex CombatBow = new(@"\bif it is a combat segment\b,?\s*bow\s+(?:him|her|it|them)", RegexOptions.IgnoreCase);
    private static readonly Regex BowTarget = new(@"\bbow\b", RegexOptions.IgnoreCase);
    private static readonly Regex PlainStraighten = new(@"^\s*straighten\s+[^,;.]*\.?\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex OnlyCombat = new(@"\bif it is a combat segment\b|[+-]\s?\d+\s?F\b", RegexOptions.IgnoreCase);

    /// <summary>The Open-phase (Action phase) or Battle-phase use of a Battle/Open ability, judged on what it would do. Null leaves the ordinary score.</summary>
    private static double? PhaseUseAdjust(Ctx x, ActionOption action, GameCommand command, AbilityIntent intent, double score)
    {
        if (!x.Memory.UsePhaseChoice || intent.IsReaction || intent.Negates) return null;
        if (x.View.Phase is not (Phase.Action or Phase.Battle)) return null;
        var abilities = BattleOrOpenAbilities(x, action, command).ToArray();
        if (abilities.Length == 0) return null;
        var inOpen = x.View.Phase == Phase.Action;
        var target = x.Card(command.TargetId ?? command.PerformerId); // "Choose your performing X: Straighten him" names no target; the performer is the card
        // A card with several Battle/Open abilities is barred only when every one of them is pointless here or held for the battle.
        return abilities.All(a => DoesNothingHere(AbilityIntent.EffectText(a.Label ?? ""), target, inOpen) || inOpen && HeldForBattle(AbilityIntent.EffectText(a.Label ?? ""), AbilityIntent.Of(a)))
            ? BarredScore : null;
    }

    private static IEnumerable<AbilityDefinition> BattleOrOpenAbilities(Ctx x, ActionOption action, GameCommand command)
    {
        if (action.Type == "useAbility") return command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var one) && one.Timing == "BattleOrOpen" ? [one] : [];
        if (action.Type == "playStrategy" && x.Card(command.CardId) is { } card) return BattlePolicy.Printed(card).Where(p => p.Ability.Timing == "BattleOrOpen").Select(p => p.Ability);
        return [];
    }

    /// <summary>
    /// True when the use would change nothing. Decided only for the effects the bot can read exactly (a bow, a conditional Honor loss, a plain straighten); any other wording, or an
    /// unknown target, is left to the ordinary rules rather than guessed.
    /// </summary>
    public static bool DoesNothingHere(string effect, CardView? target, bool inOpen)
    {
        if (target is null) return false;
        var bows = BowTarget.IsMatch(effect); var combatOnly = CombatSegmentOnly.IsMatch(effect);
        var honorIfDishonorable = HonorIfDishonorable.IsMatch(effect); var straightens = PlainStraighten.IsMatch(effect);
        if (!bows && !honorIfDishonorable && !straightens) return false;
        // Anything left once the recognised clauses are removed is an effect the bot cannot judge: it does not claim the use is pointless.
        var rest = HonorIfDishonorable.Replace(effect, ""); rest = CombatBow.Replace(rest, "");
        if (!straightens) rest = Regex.Replace(rest, @"\bbow\s+(?:him|her|it|them)\b", "", RegexOptions.IgnoreCase);
        if (!straightens && Regex.Replace(rest, @"[\s,.;]|\band\b|\bthen\b", "", RegexOptions.IgnoreCase).Length > 0) return false;
        var applies = false;
        if (bows) applies |= (!combatOnly || !inOpen) && !target.Bowed;
        if (honorIfDishonorable) applies |= target.Dishonorable;
        if (straightens) applies |= target.Bowed;
        return !applies;
    }

    /// <summary>
    /// True when an Open-phase use is better made in the battle: its effect matters only in a combat (a bow the card limits to a Combat Segment, or a Force change), and it has no payoff of its own
    /// outside one (gaining Honor, drawing a card).
    /// </summary>
    public static bool HeldForBattle(string effect, AbilityIntent intent) =>
        OnlyCombat.IsMatch(effect) && !(intent.HonorGain && intent.Side != TargetSide.Enemy) && !intent.Draws;
}
