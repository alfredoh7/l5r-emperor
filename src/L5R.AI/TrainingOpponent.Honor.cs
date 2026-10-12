using System.Text.RegularExpressions;
using L5R.Core;

namespace L5R.AI;

// Issue #870 (Mendon's playtest note): a Unicorn (high-Honor) bot played A Paragon's Strength as a Ranged Attack that could not destroy its target and lost 4 Honor for nothing.
//
//   * Honor the player using an ability loses ("Lose 4 Honor") is weighed by the deck's Honor tier: a high-Honor deck hardly ever pays it, an in-between deck pays it only for a clear gain, a
//     low-Honor deck barely minds. Honor gained is worth more to a high-Honor deck. Both apply on top of the ability's ordinary score, so the destroy / bow / penalize / help order is unchanged
//     when no Honor is involved.
//   * An Attack that cannot destroy its target is worth only the rest of its action: the Attack and the Honor it costs are taken off the ability, and what is left (a move home, Honor gained,
//     cards drawn) is scored on its own; when nothing is left the bot passes.
//   * Exception: an Attack that destroys its target and so swings the battle from losing to winning is played whatever the Honor it costs.
public static partial class TrainingOpponent
{
    /// <summary>Score points taken off an ability for each Honor its user loses, by the deck's Honor tier.</summary>
    internal static double HonorLossWeight(HonorTier tier) => tier switch { HonorTier.High => 25, HonorTier.Intermediate => 12, _ => 4 };

    /// <summary>Extra score for an ability that gains Honor, on top of the generic bonus, for a high-Honor deck (added in GenericAbilityScore).</summary>
    internal const double HighHonorGainBonus = 35;

    private static readonly Regex AttackClause = new(@"(?:Ranged|Melee)\s+(?:Attack\s*)?\d*\s*(?:Attack)?[^.]*\.?", RegexOptions.IgnoreCase);
    private static readonly Regex DestroyedFollowUp = new(@"\bIf\b[^.]*\bdestroyed\b[^.]*\.?", RegexOptions.IgnoreCase);
    private static readonly Regex OwnHonorLoss = new(@"\bLose\s+(?:\d+|X)\s+Honor\b[^.]*\.?", RegexOptions.IgnoreCase);
    private static readonly Regex BonusStrength = new(@"\+\s?(\d+)\s+strength", RegexOptions.IgnoreCase);
    private static readonly Regex StrengthEqualsForce = new(@"strength equal to (?:his|her|its|the performer's) Force", RegexOptions.IgnoreCase);

    /// <summary>The score of a use of an ability once its Honor cost and gain and a fruitless Attack have been weighed; the score unchanged when the rules do not apply.</summary>
    private static double HonorAwareScore(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (!x.Memory.UseHonorAware || action.Type != "useAbility" || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return score;
        var intent = AbilityIntent.Of(ability);
        if (intent.IsReaction) return score; // a Reaction has its own rules (negation, weighted choice)
        var penalty = SelfLossPenalty(x, intent);
        if (intent.IsAttack)
        {
            var source = x.Card(command.CardId);
            var target = x.Card(command.TargetId);
            var strength = KnownAttackStrength(x, command, ability, source);
            var kills = strength is not int reach || target is null || target.Force <= reach;
            if (!kills)
            {
                // No target can be destroyed: only the rest of the action counts, scored as an ability of its own.
                var rest = AbilityIntent.Of(new AbilityDefinition(ability.Timing, WithoutAttack(ability.Label ?? ""), "none"));
                if (rest.Rank == EffectRank.Neutral && !rest.HonorGain && !rest.Draws) return BarredScore;
                var restScore = GenericAbilityScore(x, rest) - SelfLossPenalty(x, rest) - penalty;
                return restScore <= PassScore ? BarredScore : restScore;
            }
            // A kill that swings the battle from losing to winning is worth the Honor it costs.
            if (Swings(x, target)) penalty = 0;
        }
        return score - penalty;
    }

    private static double SelfLossPenalty(Ctx x, AbilityIntent intent) => intent.SelfHonorLoss ? intent.SelfHonorLossAmount * HonorLossWeight(x.Profile.Tier) : 0;

    /// <summary>The label with its Attack, the sentences that follow from the Attack destroying something, and the Honor its user loses taken out (the Honor is weighed separately).</summary>
    internal static string WithoutAttack(string label)
    {
        var colon = label.LastIndexOf(':');
        var costs = colon >= 0 ? label[..(colon + 1)] : "";
        var effect = colon >= 0 ? label[(colon + 1)..] : label;
        effect = DestroyedFollowUp.Replace(AttackClause.Replace(effect, ""), "");
        return costs + OwnHonorLoss.Replace(effect, "").Trim();
    }

    /// <summary>What the Attack reaches: its printed strength, "strength equal to his Force" read off the performer, plus the bonuses its text promises and the augments the bot could still use; null when it cannot be told.</summary>
    private static int? KnownAttackStrength(Ctx x, GameCommand command, AbilityDefinition ability, CardView? source)
    {
        var label = ability.Label ?? "";
        var basis = BattlePolicy.AttackStrength(command.AbilityId);
        if (basis is null && StrengthEqualsForce.IsMatch(label) && (x.Card(command.PerformerId) ?? source) is { } performer) basis = Math.Max(0, performer.Force);
        if (basis is not int known) return null;
        foreach (Match bonus in BonusStrength.Matches(label)) known += int.Parse(bonus.Groups[1].Value);
        var augmenters = x.Self.Home.Concat(x.Self.Army).Append(x.Self.Stronghold).Concat(x.Self.Hand).Concat(x.Cards.Values.Where(c => c.OwnerId == x.Self.Id && c.AttachedTo is not null));
        foreach (var card in augmenters.DistinctBy(c => c.Id).Where(c => !c.Bowed && !c.ActionLocked))
            foreach (var (id, printed, _) in BattlePolicy.Printed(card))
                if (BattlePolicy.AttackAugment(printed) is > 0 and var augment && x.Memory.UsesThisTurn(x.View.TurnNumber, id, card.Id) == 0) known += augment;
        return known;
    }

    /// <summary>Whether destroying this enemy unit takes the bot from not winning the battle to winning it.</summary>
    private static bool Swings(Ctx x, CardView? target)
    {
        if (x.Odds is not { } odds || target is null) return false;
        var removed = Math.Min(odds.Enemy, UnitForceOf(x, target));
        return !odds.Winning && (odds with { Enemy = odds.Enemy - removed }).Winning;
    }
}
