namespace L5R.Core;

// Colonial Farm (oracle-l5r-10231, holding-wave-52, issue #667): "Enters play paying 1 less Gold if you are a Lion Clan player." is the Effect "lion-clan-discount" (ClanEntryDiscount); "Bow this card:
// Produce 5 Gold." is the printed Gold production; "Limited: Bow this card and target your Personality: Create a 1F Ashigaru Follower and attach it to him." creates Colonists (follower-wave-47,
// Mendon's supplied scan) through the data-driven create-and-attach (Natural Aviary's shape). Nothing here needs code beyond the catalogue entry.
public static class ColonialFarmAbilities
{
    public const string Create = "pe-colonial-farm-create";

    public static Dictionary<string, AbilityDefinition> AddColonialFarm(this Dictionary<string, AbilityDefinition> all)
    {
        all[Create] = new("Limited", "Bow this card and target your Personality: Create a 1F Ashigaru Follower and attach it to him", "own-personality",
            Costs: ActionCost.BowSource, Creates: new(CardType.Follower, Force: 1, Keywords: ["Ashigaru"], ProxyId: "user-scan-colonists"));
        return all;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterColonialFarmHooks() =>
        EngineHooks.Abilities.Register("AddColonialFarm", 12310, static abilities => abilities.AddColonialFarm());
}
