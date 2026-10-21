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

    public const string IkomaSugo = "oracle-l5r-3641", MatsuHachiro = "oracle-l5r-4915", CopperMine = "oracle-l5r-1497", SmallFarm = "oracle-l5r-7303", LargeFarm = "oracle-l5r-4664";
    public const string DaidojiTsunehiko = "oracle-l5r-9812", DaidojiKenshi = "oracle-l5r-1645";
    public const string LightArmor = "oracle-l5r-10118", VeteranSkirmishers = "oracle-l5r-9147", BlockSupplyLines = "oracle-l5r-1045", Incapacitated = "oracle-l5r-3730",
        PreparedForDeath = "oracle-l5r-6051", DefensiveStance = "oracle-l5r-10368", PaidOff = "oracle-l5r-5861", NeverAfraid = "oracle-l5r-9916";

    /// <summary>A forty-card deck: the lesson's cards on top, then the rest of the side's deck in its own order. A card the deck does not hold displaces the last card.</summary>
    internal static string[] Deck(string[] top, TutorialSide side, bool dynasty)
    {
        var pinned = side.Deck ?? PublishedContent.Snapshot.Decks.Single(d => d.Clan == side.Clan);
        var rest = (dynasty ? pinned.Dynasty : pinned.Fate).ToList();
        foreach (var id in top) rest.Remove(id);
        return [.. top.Concat(rest).Take(40)];
    }
}
