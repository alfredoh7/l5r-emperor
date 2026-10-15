using L5R.Core;

namespace L5R.AI;

// Issue #963, bot loop round 1 (Mendon's spec): Order Seppuku is for a Dishonored Personality, and only when the family Honor is low enough (about -5 or lower) that the Honor lost to
// dishonor could cost the game. It is weighed carefully, because winning a contested battle is the ordinary way to rehonor a Personality (the engine rehonors every Dishonorable
// Personality in a winning or tying army), and a Seppuku throws the Personality away. Nothing here names a card or a deck.
public static partial class TrainingOpponent
{
    /// <summary>Family Honor at or below which Seppuku is considered at all.</summary>
    public const int SeppukuHonorLimit = -5;
    /// <summary>The Honor, after the Personality dies dishonored, at or below which the death alone brings the loss at -20 within reach.</summary>
    public const int SeppukuDangerFloor = -12;

    /// <summary>
    /// Score of ordering Seppuku for the Personality in <paramref name="command"/>. Below a pass (10) means "do not". Dying dishonored costs its controller the Personality's Personal Honor
    /// (rules: the dishonorable death penalty), which Seppuku avoids by rehonoring first, so Seppuku buys exactly that much Honor and costs the Personality's Force.
    /// </summary>
    private static double SeppukuScore(Ctx x, GameCommand command)
    {
        var card = x.Self.Home.Concat(x.Self.Army).FirstOrDefault(c => c.Id == command.CardId);
        if (card?.Definition is null || !card.Dishonorable) return -100;
        if (x.Self.Honor > SeppukuHonorLimit) return -100; // family Honor is not in danger: keep the Personality
        var penalty = card.Definition.PersonalHonor;
        var afterDeath = x.Self.Honor - penalty;
        if (afterDeath > SeppukuDangerFloor) return -100; // even dying dishonored would leave the family comfortably above the loss
        // It can still win its way back to honor: a Personality that is part of the attack the bot would make this turn is rehonored by winning that battle.
        var units = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        var provinces = x.Opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        if (!card.Bowed && PlanAttack(x, units, provinces) is { } plan && plan.ContainsKey(card.Id)) return -100;
        // Weigh the Honor it saves against the Force it takes off the board.
        return PassScore + 8 * penalty + 3 * (SeppukuDangerFloor - afterDeath) - 6 * card.Force;
    }
}
