namespace L5R.Core;

/// <summary>Every card the tutorial's table is built from, by catalogue id, in one place so a lesson's script and its table cannot drift apart. Decks are listed top first.</summary>
internal static class TutorialCards
{
    // The pinned profile's own pairing, from the Lion side: you are Lion (Halls of Memory: 3 Gold, 7 starting Family Honor, province strength 7) against Crane (Shinden Asahina: 4 Gold, 6 Honor, strength 6).
    // Lion has the higher Honor, so you go first; Crane, second, starts with the bowed Bamboo Harvesters, exactly as in a real match.
    public const string YourStronghold = "oracle-l5r-2949";
    public const string FoeStronghold = "oracle-l5r-6855";
    public const string BorderKeep = "oracle-l5r-1106";
    public const string BambooHarvesters = "oracle-l5r-679";

    public const string WillingSpirits = "oracle-l5r-9337"; // the Event
    public const string MatsuHana = "oracle-l5r-4916", IkomaHakige = "oracle-l5r-3607", KitsuAkai = "oracle-l5r-4371"; // the three Personalities the first Events phase reveals: 9 Gold (7 with the Clan Discount), Honor Requirement 12 and 8 Gold, 10 Gold
    public const string FallowLands = "oracle-l5r-9865", MegumisGuidance = "oracle-l5r-10028"; // the Region and the Celestial revealed on turn two
    public const string SmallFarm = "oracle-l5r-7303", AkodoUehara = "oracle-l5r-305", CopperMine = "oracle-l5r-1497", LargeFarm = "oracle-l5r-4664";
    public const string DojiAtsumichi = "oracle-l5r-2060"; // the defender: 8 Gold, 6 with the Crane Clan Discount, which Shinden Asahina (4) and the Border Keep (2) make exactly

    public const string TellTheTale = "oracle-l5r-7862", Truce = "oracle-l5r-10401", VeteranSkirmishers = "oracle-l5r-9147", LightArmor = "oracle-l5r-10118",
        EmbersFinalFire = "oracle-l5r-2309", RingOfFire = "oracle-l5r-6335", DefensiveStance = "oracle-l5r-10368", NeverAfraid = "oracle-l5r-9916";

    /// <summary>Provinces 1-4 at setup, then the refill for the Event's province (the Region), then the three cards the Border Keep brings face up (a Holding, the Personality, the Clan Holding),
    /// then the Celestial (the refill after the first purchase) and two spares for the other purchases' refills.</summary>
    public static readonly string[] YourDynasty = [WillingSpirits, MatsuHana, IkomaHakige, KitsuAkai, FallowLands, SmallFarm, AkodoUehara, CopperMine, MegumisGuidance, LargeFarm, SmallFarm];
    public static readonly string[] DynastyFiller = [LargeFarm, SmallFarm];
    /// <summary>The six cards of the "forgotten" opening draw (limited action, reaction, follower, item, spell, ring), then the card drawn at the end of turn one.</summary>
    public static readonly string[] YourFate = [TellTheTale, Truce, VeteranSkirmishers, LightArmor, EmbersFinalFire, RingOfFire, DefensiveStance];
    public static readonly string[] FateFiller = [VeteranSkirmishers, LightArmor];
    /// <summary>Crane's Dynasty deck opens on Doji Atsumichi (province 1); its Fate deck opens on the one card its End-phase draw will give it.</summary>
    public static readonly string[] FoeDynasty = [DojiAtsumichi, SmallFarm, LargeFarm];
    public static readonly string[] FoeFate = [NeverAfraid];
}
