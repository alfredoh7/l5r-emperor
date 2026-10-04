namespace L5R.Core;

/// <summary>"Bow this card: Produce 2 Gold, or 3 Gold if you are a [Clan] Clan player" (Gold Mine, Marketplace, Hachi's Revenge, Copper Mine, Geisha House, Iron Mine, Kobune Port, Shinomen Marsh,
/// Silver Mine, Stables, ...): a Holding whose Effect is "&lt;clan&gt;-gold" produces one more Gold for a player of that Clan. This is the one place that maps such an Effect to its Clan: the engine
/// (MatchEngine.GoldProduced), Stolen Merchandise's copy of a Gold-producing trait (EmperorP5Hidden.IsGoldEffect) and the training opponent's production estimate (L5R.AI.TrainingOpponent.Production) all read
/// it, so a new Clan Holding needs only its Effect string here.</summary>
public static class ClanGoldEffects
{
    /// <summary>The Clan a "&lt;clan&gt;-gold" Effect names, or null when the Effect is not a Clan-conditional production.</summary>
    public static string? ClanOf(string? effect) => effect switch
    {
        "crane-gold" => "Crane",
        "dragon-gold" => "Dragon",
        "lion-gold" => "Lion",
        "scorpion-gold" => "Scorpion",
        "crab-gold" => "Crab",
        "mantis-gold" => "Mantis",
        "phoenix-gold" => "Phoenix",
        "spider-gold" => "Spider",
        "unicorn-gold" => "Unicorn",
        _ => null
    };

    public static bool IsClanGold(string? effect) => ClanOf(effect) is not null;

    /// <summary>The extra Gold a Holding with this Effect produces for a player of <paramref name="playerClan"/>: 1 for the named Clan, otherwise 0.</summary>
    public static int Bonus(string? effect, string? playerClan) => ClanOf(effect) is { } clan && string.Equals(clan, playerClan, StringComparison.Ordinal) ? 1 : 0;
}
