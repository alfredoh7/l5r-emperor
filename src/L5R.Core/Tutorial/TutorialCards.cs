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

    // The Events, Regions and Celestials lesson (issue #1074): Unicorn Maidens (you) against Mantis Manipulation.
    // Plains of the Maiden: 5 Gold, 5 starting Honor, province strength 7. Koshin Keep: 4 Gold, 2 starting Honor, province strength 7. Unicorn has more Honor, so you go first.
    public const string UnicornStronghold = "oracle-l5r-5982", MantisStronghold = "oracle-l5r-4541";
    public const string DarkExperiments = "oracle-l5r-10029", NaturalAviary = "oracle-l5r-9866", EbisusHonesty = "oracle-l5r-2276", ProsperousVillage = "oracle-l5r-6086";
    public const string ShindenShorai = "oracle-l5r-6857", YoritomosGuidance = "oracle-l5r-9662", KobunePort = "oracle-l5r-4496", ShelteredPort = "oracle-l5r-9799",
        PlainsOfOtosanUchi = "oracle-l5r-5979", SuitengusBlessing = "oracle-l5r-9948", BoastfulProclamation = "oracle-l5r-1086";
    public const string SneakAttack = "oracle-l5r-7311", SoundOfThunder = "oracle-l5r-8347", WallOfHonor = "oracle-l5r-9198", PureIntent = "oracle-l5r-9922", Sniping = "oracle-l5r-7312";

    // The Recruiting Personalities lesson (issue #1075): Crab Control (you) against Classic Crane.
    // Yasuki Palaces (exp): 5 Gold, 1 less when paying for a Personality; strength 7. Shinden Asahina is Crane's Stronghold (FoeStronghold).
    public const string YasukiPalaces = "oracle-l5r-9459", IronMine = "oracle-l5r-3808", Marketplace = "oracle-l5r-4840", Pillowbook = "oracle-l5r-5958";
    public const string YasukiBaiko = "oracle-l5r-10044", YasukiTanimura = "oracle-l5r-9464", YasukiTsujiken = "oracle-l5r-9469", YasukiTsujikenExp = "oracle-l5r-10045",
        YoritomoSachina = "oracle-l5r-9623", UtakuMai = "oracle-l5r-9086";

    /// <summary>A forty-card deck: the lesson's cards on top, then the rest of the side's deck in its own order. A card the deck does not hold displaces the last card.</summary>
    internal static string[] Deck(string[] top, TutorialSide side, bool dynasty)
    {
        var pinned = side.Deck ?? PublishedContent.Snapshot.Decks.Single(d => d.Clan == side.Clan);
        var rest = (dynasty ? pinned.Dynasty : pinned.Fate).ToList();
        foreach (var id in top) rest.Remove(id);
        return [.. top.Concat(rest).Take(40)];
    }
}
