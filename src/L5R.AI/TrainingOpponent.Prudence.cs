using L5R.Core;

namespace L5R.AI;

// Issue #902 (Mendon's bot notes, 2026-10-12). Four pieces of general guidance, none of which names a card:
//
//   * An effect that hits BOTH sides ("Each player targets a unit he controls with the lowest total Gold Cost ... Destroy all targets") is worth playing only when the opponent loses clearly
//     more than the bot: the cheapest unit the bot has in play is worth at least 3 less than the cheapest the opponent has, or the bot has no Personality in play and the opponent has one.
//     Otherwise the bot passes (before: it destroyed nothing and lost 4 Honor on its first turn). A Personality-creating ability scores higher while the bot holds such an effect that the
//     new unit would make worth playing: it becomes the bot's cheapest unit.
//   * A bow is aimed at a card that is not already bowed (Personality, Follower, attachment, Holding, Ring alike); with none, the action is kept, not spent.
//   * A claim on the first action of a battle ("The Attacker has the first opportunity") is made only by the Attacker, at a battle where both sides have units.
//   * Early on, while the bot is still building its economy, the Action phase (which comes before the Dynasty phase of the same turn) does not spend Gold on cards from the hand: the Gold
//     buys Holdings and Personalities a moment later.
public static partial class TrainingOpponent
{
    /// <summary>A unit's worth for the comparison: its Personality's Gold Cost plus everything attached to him.</summary>
    internal static int UnitValue(PlayerView player, CardView personality) =>
        (personality.Definition?.GoldCost ?? 0) + player.Home.Concat(player.Army).Where(c => c.AttachedTo == personality.Id).Sum(c => c.Definition?.GoldCost ?? 0);

    public static int[] UnitValues(PlayerView player) =>
        player.Home.Concat(player.Army).Where(c => c.Definition?.Type == CardType.Personality).Select(c => UnitValue(player, c)).ToArray();

    /// <summary>Whether an effect on both sides' units favors the bot, given what each side has in play (an extra unit of the bot's own may be counted).</summary>
    public static bool MutualFavorsBot(int[] own, int[] opponent, int? extraOwn = null)
    {
        if (extraOwn is int extra) own = [.. own, extra];
        if (opponent.Length == 0) return false;
        return own.Length == 0 || own.Min() + 3 <= opponent.Min();
    }

    private static bool HoldsMutualEffect(Ctx x) =>
        x.Self.Hand.Any(h => h.Definition?.Abilities?.Any(a => AbilityIntent.Of(a).Mutual) == true);

    private static bool EarlyEconomy(Ctx x) =>
        x.View.Phase == Phase.Action && x.Self.TurnsTaken <= 2 && x.Self.Home.Count(c => c.Definition?.Type == CardType.Holding) < 3
        && x.Self.Provinces.Any(p => p.Card is { Definition: not null }); // something to buy in the Dynasty phase that follows

    /// <summary>The score once the prudence rules have been applied; unchanged when none applies.</summary>
    private static double PrudenceScore(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (!x.Memory.UsePrudence) return score;
        var isReaction = action.Type == "useAbility" && command.AbilityId is not null && AbilityIntent.Of(command.AbilityId).IsReaction;
        // Early economy: the Gold is for the Dynasty phase that follows this Action phase.
        if (EarlyEconomy(x) && action.Type is "useAbility" or "playStrategy" or "equip" && x.View.PendingChoice is null && action.Gold is { Cost: > 0 } && !isReaction)
            return Math.Min(score, BarredScore);
        if (action.Type != "useAbility" || command.AbilityId is null) return score;
        var intent = AbilityIntent.Of(command.AbilityId);
        var own = UnitValues(x.Self); var theirs = UnitValues(x.Opponent);
        if (intent.Mutual && !MutualFavorsBot(own, theirs)) return Math.Min(score, BarredScore);
        if (intent.CreatesPersonality && !isReaction && HoldsMutualEffect(x) && !MutualFavorsBot(own, theirs) && MutualFavorsBot(own, theirs, 0))
            return Math.Max(score, CreateBeforeMutualScore);
        if (intent.ClaimsFirstOpportunity && !FirstClaimUseful(x)) return Math.Min(score, BarredScore);
        return score;
    }

    /// <summary>A Personality created so that an effect on both sides becomes worth playing goes first: above ordinary abilities, below the Favor.</summary>
    internal const double CreateBeforeMutualScore = 200;

    /// <summary>Only the Attacker gains from the first action of a battle, and only at a battlefield where both armies stand.</summary>
    private static bool FirstClaimUseful(Ctx x)
    {
        if (x.View.ActivePlayerId != x.Self.Id) return false;
        var province = x.View.BattleStep?.BattlefieldProvince ?? x.View.CurrentBattleProvince;
        if (province is not int p) return false;
        return x.Self.Army.Any(c => c.ProvinceIndex == p && c.Definition?.Type is CardType.Personality or CardType.Follower)
            && x.Opponent.Army.Any(c => c.ProvinceIndex == p && c.Definition?.Type is CardType.Personality or CardType.Follower);
    }

    /// <summary>Whether bowing this card would do nothing because it is already bowed (any kind of card).</summary>
    internal static bool BowWasted(AbilityIntent intent, CardView? target) =>
        intent.Rank == EffectRank.Bow && intent.Side != TargetSide.Own && target is { Bowed: true };
}
