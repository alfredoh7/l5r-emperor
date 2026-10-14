namespace L5R.Core;

/// <summary>Every card the tutorial's table is built from, by catalogue id, in one place so a lesson's script and its table cannot drift apart. Decks are listed top first.</summary>
internal static class TutorialCards
{
    // The pinned profile's own pairing: you are Crane (Shinden Asahina: 4 Gold, 6 starting Family Honor, province strength 6) against Lion (Halls of Memory: 3 Gold, 7 Honor, strength 7).
    // Lion has the higher Honor, so Lion goes first and you, second, start with the bowed Bamboo Harvesters, exactly as in a real match.
    public const string YourStronghold = "oracle-l5r-6855";
    public const string FoeStronghold = "oracle-l5r-2949";
    public const string BorderKeep = "oracle-l5r-1106";
    public const string BambooHarvesters = "oracle-l5r-679";
    public const string FoeDefender = "oracle-l5r-3636"; // Ikoma Shika, 2 Force: against province strength 7 your 9 Force before the battle action just fails to break the province (9 - 2 is not more than 7)

    public const string WillingSpirits = "oracle-l5r-9337"; // the Event
    public const string DaidojiTsunehiko = "oracle-l5r-9812", AkodoKisho = "oracle-l5r-229", DojiAtsumichi = "oracle-l5r-2060"; // cards you cannot buy yet that the Border Keep cycles away: 8 Gold, an Honor Requirement of 10, 8 Gold
    public const string FallowLands = "oracle-l5r-9865", MegumisGuidance = "oracle-l5r-10028"; // the Region and the Celestial revealed on turn two
    public const string SmallFarm = "oracle-l5r-7303", AkodoUehara = "oracle-l5r-305", LargeFarm = "oracle-l5r-4664", IkomaShika = "oracle-l5r-3636";

    public const string TellTheTale = "oracle-l5r-7862", OneKoku = "oracle-l5r-5709", VeteranSkirmishers = "oracle-l5r-9147", LightArmor = "oracle-l5r-10118",
        EmbersFinalFire = "oracle-l5r-2309", RingOfFire = "oracle-l5r-6335", CrushingStrength = "oracle-l5r-1589";

    /// <summary>Provinces 1-4 at setup, then the refill for the Event's province (the Region), then the three cards the Border Keep brings face up (a Holding, the Personality, a spare), then the Celestial and a spare for the purchases' refills.</summary>
    public static readonly string[] YourDynasty = [WillingSpirits, DaidojiTsunehiko, AkodoKisho, DojiAtsumichi, FallowLands, SmallFarm, AkodoUehara, LargeFarm, MegumisGuidance, IkomaShika];
    public static readonly string[] DynastyFiller = [SmallFarm, LargeFarm];
    /// <summary>The six cards of the "forgotten" opening draw (limited action, reaction, follower, item, spell, ring), then the card drawn at the end of turn one.</summary>
    public static readonly string[] YourFate = [TellTheTale, OneKoku, VeteranSkirmishers, LightArmor, EmbersFinalFire, RingOfFire, CrushingStrength];
    public static readonly string[] FateFiller = ["oracle-l5r-9147", "oracle-l5r-10118"];
    public static readonly string[] FoeDynasty = ["oracle-l5r-7303", "oracle-l5r-4664", "oracle-l5r-4840", "oracle-l5r-7303", "oracle-l5r-4664", "oracle-l5r-4840"];
}
