using System.Text.RegularExpressions;
using L5R.Core;

namespace L5R.AI;

/// <summary>Which route(s) to victory a deck's own printed cards support (bot loop S0, issue #963). Read from the bot's own decklist only: printed card text and counts, never a deck name or the opponent.</summary>
public enum WinRoute { Military, Honor, Dishonor }

/// <summary>
/// Densities are per 40 cards of the Dynasty and Fate decks. <see cref="HonorGain"/> is Honor the deck gains itself (printed "gain N Honor"), <see cref="OpponentHonorLoss"/> is Honor a player is made to lose
/// (printed "controller / player loses N Honor", with "gains or loses" split in half), <see cref="HonorCost"/> is Honor the deck's own cards make it lose. Military is always the fallback: a deck without an Honor or
/// Dishonor engine can only win on the battlefield, and a deck with one still has to survive until it works.
/// </summary>
public sealed record DeckArchetype(WinRoute Primary, WinRoute? Secondary, double HonorGain, double OpponentHonorLoss, double HonorCost)
{
    /// <summary>An engine this dense wins games on its own.</summary>
    public const double PrimaryDensity = 15, SecondaryDensity = 6;

    private static readonly Regex Gain = new(@"\bgain (\d+|one|two|three|four|X) honor", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GainEqual = new(@"gain honor equal", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Lose = new(@"(controller|opponent|enemy player|target player|that player|player)\W{0,3}\s*(who )?(loses|gains or loses) (\d+|one|two|three|X) honor", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Either = new(@"gains or loses (\d+|one|two|three|X) honor", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OwnCost = new(@"(?:^|[.:]\s)Lose (\d+|one|two|X) Honor", RegexOptions.Compiled | RegexOptions.Multiline);

    private static int Amount(string s) => s.ToLowerInvariant() switch { "one" => 1, "two" => 2, "three" => 3, "four" => 4, "x" => 2, var d when int.TryParse(d, out var v) => v, _ => 2 };

    /// <summary>Classifies a deck from <paramref name="cards"/> (each printed card with its copy count; setup cards count only for their text).</summary>
    public static DeckArchetype Classify(IEnumerable<(CardDefinition Card, int Count)> cards)
    {
        double gain = 0, lose = 0, cost = 0; var deckCards = 0;
        foreach (var (card, count) in cards)
        {
            var setup = card.Type == CardType.Stronghold || card.Name == "Bamboo Harvesters" || card.Name.StartsWith("Border Keep", StringComparison.Ordinal);
            if (!setup) deckCards += count;
            var text = card.Text ?? "";
            foreach (Match m in Gain.Matches(text)) gain += count * Amount(m.Groups[1].Value);
            gain += count * 2 * GainEqual.Matches(text).Count;
            foreach (Match m in Lose.Matches(text)) lose += count * Amount(m.Groups[4].Value);
            foreach (Match m in Either.Matches(text)) { gain += count * Amount(m.Groups[1].Value) * .5; lose += count * Amount(m.Groups[1].Value) * .5; }
            foreach (Match m in OwnCost.Matches(text)) cost += count * Amount(m.Groups[1].Value);
        }
        if (deckCards == 0) return new(WinRoute.Military, null, 0, 0, 0);
        double Density(double x) => 40 * x / deckCards;
        var (g, l, c) = (Density(gain), Density(lose), Density(cost));
        if (l >= PrimaryDensity && l > g) return new(WinRoute.Dishonor, WinRoute.Military, g, l, c);
        if (g >= PrimaryDensity) return new(WinRoute.Honor, WinRoute.Military, g, l, c);
        return new(WinRoute.Military, g >= SecondaryDensity ? WinRoute.Honor : null, g, l, c);
    }
}
