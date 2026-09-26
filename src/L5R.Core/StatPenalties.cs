namespace L5R.Core;

// Stat penalties (issue #336; Mendon's ruling, quoted in docs/decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md): a stat penalty is any
// modifier applied to a statistic that is negative -- -4F is a Force penalty, -1C a Chi penalty, -2 Gold Cost a Gold cost penalty (as is
// reducing a Gold cost to zero), -1 Personal Honor a Personal Honor penalty. Dishonoring a Personality is NOT a Personal Honor penalty (it
// sets a status; CardPersonalHonor reads 0 while dishonorable, no modifier is ever added). Every modifier is applied through ApplyModifier
// (or, for a persistent stat token, CreateToken), and both call NoteStatPenalty, the one place a penalty is classified and announced to the
// cards that watch for it.
public sealed partial class MatchEngine
{
    private static readonly string[] PenaltyStats = ["Force", "Chi", "GoldCost", "PersonalHonor"];
    private static bool IsStatPenalty(string stat, int amount) => amount < 0 && PenaltyStats.Contains(stat, StringComparer.Ordinal);

    // The Gold cost of a card as it stands now: its printed cost plus every GoldCost modifier, never below zero. Read by the comparisons against another
    // card's cost ("with a 0 Gold Cost", "equal or lower Gold Cost") and, since the Forgotten Legacy pass (issue #467, Wind of the Moon), by the recruit
    // price itself (QuotePurchase).
    private int CardGoldCost(CardInstance card) => Math.Max(P2GoldCostMinimum(card) /* issue #503: Growing Hostility */, Definition(card).GoldCost + StatModifier(card, "GoldCost")
        + KanahashiGoldCostPenalty(card) + P5GoldCost(card)); // The Shadow's Embrace pass (issue #476), batch 2: Yoritomo Kanahashi's "-2 Gold Cost" on other players' cards in play.

    // "Reduce his Gold cost to zero" is a Gold cost penalty of exactly what the card costs now; a card that already costs nothing is not reduced.
    private void ReduceGoldCostToZero(CardInstance card, string sourceId, string effect)
    {
        var cost = CardGoldCost(card);
        if (cost > 0) ApplyModifier(card, sourceId, effect, "GoldCost", -cost);
    }

    private const string PenaltyScopeEnemyOpposing = "enemy-personality-opposing-at-own-battlefield", PenaltyScopeAnyCard = "any-card";

    /// <summary>A trait that reacts to a stat penalty being given: for each penalty of one of Stats that Scope covers, the card gets Reward
    /// on RewardStat (an ordinary modifier, until the end of the turn). Registered by the watching card's Effect.</summary>
    private readonly record struct StatPenaltyTrait(string[] Stats, string Scope, string RewardStat, int Reward);

    private static readonly Dictionary<string, StatPenaltyTrait> StatPenaltyTraits = new(StringComparer.Ordinal)
    {
        // Utaku Liu-Xeung, Experienced (oracle-l5r-9085): "After the resolution of an action that gave a Force penalty to an enemy card at
        // Liu-Xeung's battlefield: Give Liu-Xeung +1F." Mendon's ruling: a trait, so automatic and every time; a Force penalty to a
        // Personality opposing her at a battlefield where she is present.
        ["force-after-enemy-force-penalty-at-battlefield"] = new(["Force"], PenaltyScopeEnemyOpposing, "Force", 1),
        // Synthetic fixture (never in a deck): reacts to a penalty of every classified stat to any card, so a test can see each one.
        ["training-penalty-watcher"] = new(PenaltyStats, PenaltyScopeAnyCard, "Force", 1),
    };

    private bool PenaltyTraitApplies(CardInstance watcher, CardInstance target, StatPenaltyTrait trait) => trait.Scope switch
    {
        PenaltyScopeEnemyOpposing => Definition(target).Type == CardType.Personality && target.OwnerId != watcher.OwnerId
            && watcher.Zone == Zone.Battlefield && target.Zone == Zone.Battlefield && target.ProvinceIndex == watcher.ProvinceIndex,
        PenaltyScopeAnyCard => true,
        _ => false,
    };

    // Called by ApplyModifier and CreateToken after a modifier or stat token has been put on `target`. A positive amount, or a stat outside
    // the four, is not a penalty and does nothing. The traits are mandatory and work while bowed (core rules, Traits and effects), in
    // the same order every time, and their own reward is a bonus, so a trait can never be triggered by itself.
    private void NoteStatPenalty(CardInstance target, string sourceId, string effect, string stat, int amount)
    {
        if (!IsStatPenalty(stat, amount)) return;
        foreach (var watcher in _state.Cards.Values.Where(c => InPlay(c) && StatPenaltyTraits.ContainsKey(Definition(c).Effect)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            var trait = StatPenaltyTraits[Definition(watcher).Effect];
            if (!trait.Stats.Contains(stat, StringComparer.Ordinal) || !PenaltyTraitApplies(watcher, target, trait)) continue;
            Emit("actionTraitFired", $"{Definition(watcher).Name}: a {stat} penalty was given to {Definition(target).Name}, +{trait.Reward} {trait.RewardStat} (this turn).", watcher.OwnerId, watcher.Id);
            ApplyModifier(watcher, watcher.Id, Definition(watcher).Effect, trait.RewardStat, trait.Reward);
        }
    }
}
