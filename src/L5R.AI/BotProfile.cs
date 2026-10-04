using L5R.Core;

namespace L5R.AI;

public enum HonorTier { Low, Intermediate, High }

/// <summary>
/// Issue #642: how the bot reads its own deck's relationship with Honor (Mendon's spec). A Stronghold that starts at 5 or more Honor, or a deck whose
/// Personalities average more than 2.5 Personal Honor, is a HIGH honor deck; a Stronghold under 3, or an average under 1.5, is LOW; the rest are
/// INTERMEDIATE. When the two signals disagree (a 5-Honor Stronghold with a 1.2 average) the side with more signals wins and a tie is INTERMEDIATE.
///
/// The average is the bot's OWN information: the decklist if the caller supplies it (<see cref="BotContext.DeckAveragePersonalHonor"/>), otherwise the
/// average over every own Personality it has been shown so far (its hand, table, provinces, discard and dead pile), and until it has seen three,
/// only the Stronghold speaks. Nothing about the opponent's hidden cards is used.
/// </summary>
public sealed record BotProfile(HonorTier Tier, int StartingHonor, double? AveragePersonalHonor)
{
    public const double HighAverage = 2.5, LowAverage = 1.5;
    public const int HighStart = 5, LowStartBelow = 3;
    /// <summary>Honor difference, own minus opponent, from which an Intermediate deck treats the race as "close or ahead" for Proclaim.</summary>
    public const int CloseBehind = -4, FarBehind = -5;

    public static BotProfile Assess(PlayerView self, double? deckAverage = null)
    {
        var start = self.Stronghold.Definition?.StartingHonor ?? 0;
        var average = deckAverage ?? SeenAverage(self);
        var high = (start >= HighStart ? 1 : 0) + (average is > HighAverage ? 1 : 0);
        var low = (start > 0 && start < LowStartBelow ? 1 : 0) + (average is < LowAverage ? 1 : 0);
        var tier = high > low ? HonorTier.High : low > high ? HonorTier.Low : HonorTier.Intermediate;
        return new(tier, start, average);
    }

    private static double? SeenAverage(PlayerView self)
    {
        var people = self.Home.Concat(self.Army).Concat(self.Hand).Concat(self.Discard).Concat(self.Provinces.Select(p => p.Card).OfType<CardView>())
            .Where(c => c.Definition?.Type == CardType.Personality).GroupBy(c => c.Id).Select(g => g.First().Definition!.PersonalHonor).ToArray();
        return people.Length >= 3 ? people.Average() : null;
    }

    /// <summary>Whether Proclaim is a priority right now: always for HIGH, never for LOW (unless Gold is spare), and for INTERMEDIATE when the bot is within 4 of
    /// the opponent's Honor or ahead, not when it trails by more than 5 (in between it is neutral).</summary>
    public ProclaimStance StanceFor(int ownHonor, int opponentHonor, bool extraGold)
    {
        if (Tier == HonorTier.High) return ProclaimStance.Prioritize;
        if (Tier == HonorTier.Low) return extraGold ? ProclaimStance.Prioritize : ProclaimStance.Deprioritize;
        var lead = ownHonor - opponentHonor;
        return lead >= CloseBehind ? ProclaimStance.Prioritize : lead < FarBehind ? ProclaimStance.Deprioritize : ProclaimStance.Neutral;
    }

    /// <summary>"Gain Honor" or "make the opponent lose Honor": HIGH gains, LOW takes it from the opponent, INTERMEDIATE gains when ahead or below its own
    /// starting Honor and takes it away otherwise (Mendon's spec).</summary>
    public bool PrefersOwnGain(int ownHonor, int opponentHonor) =>
        Tier switch
        {
            HonorTier.High => true,
            HonorTier.Low => false,
            _ => ownHonor > opponentHonor || StartingHonor > 0 && ownHonor < StartingHonor
        };
}

public enum ProclaimStance { Deprioritize, Neutral, Prioritize }
