namespace L5R.Core;

// ======================================================================================================================================================================================
// STRATEGY WAVE 120 (issue #699, cluster "benediction"): Benediction (oracle-l5r-10930, Aftermath), "Open: Destroy your target Holding. Give a Province +3 strength."
//
// Mendon's ruling (issue #699): the Province may be ANY province (either player's, face-up or face-down), chosen with the province selection the client already has (the hover highlight, the
// list of a face-up province's cards, "face down" noted). Destroying the Holding is a separate effect and happens FIRST; if its destruction is negated (or the Holding is already gone) the
// Province strength bonus is NOT negated. The +3 is an ordinary duration effect: until the end of the turn (R-001; the scan prints no other duration), the province strength Ring of Earth gives.
//
// The two targets are two slots of the multi-target shape (TargetSlots.cs): a card slot (your Holding) and the new province slot kind (TargetSlotKinds.Province), whose selection is a
// province owner and index. Both are chosen up front, with the command; nothing here is hidden information (a face-down province's card is never named).
// ======================================================================================================================================================================================

public static class WlBenedictionAbilities
{
    public const string Benediction = "wlben-benediction";
    public const string HoldingSlot = "holding", ProvinceSlot = "province";
    public const int StrengthBonus = 3;

    public static Dictionary<string, AbilityDefinition> AddWlBenediction(this Dictionary<string, AbilityDefinition> all)
    {
        // "Open: Destroy your target Holding. Give a Province +3 strength."
        all[Benediction] = new("Open", "Destroy your target Holding. Give a Province +3 strength", "slots",
            Slots: [new(HoldingSlot, "own-holding", "Your Holding to destroy"), new(ProvinceSlot, TargetSlotKinds.Province, "A Province to give +3 strength")]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private bool? RunWlBenediction(RunHookContext context)
    {
        if (context.id != WlBenedictionAbilities.Benediction) return null;
        var (command, player) = (context.command, context.player);
        var holding = SlotCards(command, WlBenedictionAbilities.HoldingSlot).First();
        // The Holding's destruction is its own effect and happens first; a negated or failed destruction (DestroyUnits skips it) leaves the strength effect untouched.
        if (InPlay(holding)) DestroyUnits([holding], false, player.Id);
        var province = command.Selections!.First(s => s.Slot == WlBenedictionAbilities.ProvinceSlot);
        GiveProvinceStrengthThisTurn(province.ProvinceOwner!.Value, province.Province!.Value, WlBenedictionAbilities.StrengthBonus, "Benediction"); // until end of turn (R-001)
        return false;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlBenedictionHooks()
    {
        EngineHooks.Abilities.Register("AddWlBenediction", 15200, static abilities => abilities.AddWlBenediction());
        EngineHooks.RunEffects.Register("RunWlBenediction", 15201, static (engine, context) => engine.RunWlBenediction(context));
    }
}
