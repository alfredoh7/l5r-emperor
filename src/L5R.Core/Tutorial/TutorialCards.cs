namespace L5R.Core;

/// <summary>Every card the tutorial's table is built from, by catalogue id, in one place so a lesson's script and its table cannot drift apart. Decks are listed top first.</summary>
internal static class TutorialCards
{
    // The classic matchup: you are Lion (Halls of Memory: 3 Gold, 7 starting Family Honor, province strength 7) against Crane (Shinden Asahina: 4 Gold, 6 Honor, strength 6).
    // Lion has the higher Honor, so you go first and the opponent starts with the bowed Bamboo Harvesters. Three Gold from your Stronghold alone buys your Lion Personality at his Clan discount ("boxable").
    public const string YourStronghold = "oracle-l5r-2949";
    public const string FoeStronghold = "oracle-l5r-6855";
    public const string BorderKeep = "oracle-l5r-1106";
    public const string BambooHarvesters = "oracle-l5r-679";
    public const string FoeDefender = "oracle-l5r-1645"; // Daidoji Kenshi, 5 Gold, 3 Force: the opponent buys him on its turn. Against province strength 6, your 9 Force before the battle action just fails to break the province (9 - 3 is not more than 6)

    public const string WillingSpirits = "oracle-l5r-9337"; // the Event
    public const string MatsuHana = "oracle-l5r-4916", IkomaHakige = "oracle-l5r-3607", KitsuAkai = "oracle-l5r-4371"; // cards you cannot buy yet that the Border Keep cycles away: 9 Gold, an Honor Requirement of 12 (8 Gold), 10 Gold
    public const string FallowLands = "oracle-l5r-9865", MegumisGuidance = "oracle-l5r-10028"; // the Region and the Celestial revealed on turn two
    public const string SmallFarm = "oracle-l5r-7303", AkodoUehara = "oracle-l5r-305", LargeFarm = "oracle-l5r-4664", IkomaShika = "oracle-l5r-3636";

    public const string TellTheTale = "oracle-l5r-7862", Truce = "oracle-l5r-10401", VeteranSkirmishers = "oracle-l5r-9147", LightArmor = "oracle-l5r-10118",
        EmbersFinalFire = "oracle-l5r-2309", RingOfFire = "oracle-l5r-6335", CrushingStrength = "oracle-l5r-1589";

    /// <summary>Provinces 1-4 at setup, then the refill for the Event's province (the Region), then the three cards the Border Keep brings face up (a Holding, the Personality, a spare), then the Celestial and a spare for the purchases' refills.</summary>
    public static readonly string[] YourDynasty = [WillingSpirits, MatsuHana, IkomaHakige, KitsuAkai, FallowLands, SmallFarm, AkodoUehara, LargeFarm, MegumisGuidance, IkomaShika];
    public static readonly string[] DynastyFiller = [SmallFarm, LargeFarm];
    /// <summary>The six cards of the "forgotten" opening draw (limited action, reaction, follower, item, spell, ring), then the card drawn at the end of turn one.</summary>
    public static readonly string[] YourFate = [TellTheTale, Truce, VeteranSkirmishers, LightArmor, EmbersFinalFire, RingOfFire, CrushingStrength];
    public static readonly string[] FateFiller = ["oracle-l5r-9147", "oracle-l5r-10118"];
    /// <summary>The opponent's provinces: a Holding, the one Personality it buys with its first turn's Gold (Kenshi, 5 Gold at full price), then Holdings.</summary>
    public static readonly string[] FoeDynasty = ["oracle-l5r-7303", FoeDefender, "oracle-l5r-4664", "oracle-l5r-4840"];
    public static readonly string[] FoeFiller = ["oracle-l5r-7303", "oracle-l5r-4664", "oracle-l5r-4840"];
}
