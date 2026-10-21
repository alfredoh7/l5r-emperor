namespace L5R.Core;

/// <summary>Every card the tutorial's table is built from, by catalogue id, in one place so the script and its table cannot drift apart.
/// The decks are the pinned Lion and Crane decks (<c>content/published/lion-crane-v1.json</c>) with the cards the lesson meets put on top, in the order they will be met.</summary>
internal static class TutorialCards
{
    // Lion (you): Halls of Memory, 3 Gold, 7 starting Honor, province strength 7. Crane: Shinden Asahina, 4 Gold, 6 Honor, strength 6. Lion has more Honor, so you go first.
    public const string YourStronghold = "oracle-l5r-2949";
    public const string FoeStronghold = "oracle-l5r-6855";
    public const string BorderKeep = "oracle-l5r-1106";
    public const string BambooHarvesters = "oracle-l5r-679";

    public const string IkomaSugo = "oracle-l5r-3641", MatsuHachiro = "oracle-l5r-4915", CopperMine = "oracle-l5r-1497", GoldMine = "oracle-l5r-2867", SmallFarm = "oracle-l5r-7303", LargeFarm = "oracle-l5r-4664";
    public const string DaidojiTsunehiko = "oracle-l5r-9812", DaidojiKenshi = "oracle-l5r-1645";
    public const string LightArmor = "oracle-l5r-10118", VeteranSkirmishers = "oracle-l5r-9147", BlockSupplyLines = "oracle-l5r-1045", Incapacitated = "oracle-l5r-3730",
        PreparedForDeath = "oracle-l5r-6051", DefensiveStance = "oracle-l5r-10368", PaidOff = "oracle-l5r-5861", NeverAfraid = "oracle-l5r-9916";

    /// <summary>Lion's Dynasty deck, top first: provinces 1-4 at setup, then the card the first purchase's province is refilled with (turn two's Events phase reveals it).</summary>
    public static readonly string[] YourDynasty = [IkomaSugo, CopperMine, MatsuHachiro, GoldMine, SmallFarm];
    /// <summary>Lion's Fate deck, top first: the six-card opening hand, then the card drawn at the end of turn one.</summary>
    public static readonly string[] YourFate = [LightArmor, VeteranSkirmishers, BlockSupplyLines, Incapacitated, PreparedForDeath, DefensiveStance, PaidOff];
    /// <summary>Crane's Dynasty deck, top first: its four provinces. Its Fate deck opens on the one card its End-phase draw will give it.</summary>
    public static readonly string[] FoeDynasty = [DaidojiTsunehiko, DaidojiKenshi, SmallFarm, LargeFarm];
    public static readonly string[] FoeFate = [NeverAfraid];

    /// <summary>A forty-card deck: the lesson's cards on top, then the rest of the pinned deck in its own order. A card the pinned deck does not hold (Copper Mine) displaces the last card.</summary>
    internal static string[] Deck(string[] top, string clan, bool dynasty)
    {
        var pinned = PublishedContent.Snapshot.Decks.Single(d => d.Clan == clan);
        var rest = (dynasty ? pinned.Dynasty : pinned.Fate).ToList();
        foreach (var id in top) rest.Remove(id);
        return [.. top.Concat(rest).Take(40)];
    }
}
