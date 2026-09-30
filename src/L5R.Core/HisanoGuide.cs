namespace L5R.Core;

/// <summary>Forgotten Legacy's Kitsune Hisano and the Personality printed on her created scan.</summary>
public static class HisanoGuideAbilities
{
    public const string Create = "hisano-create-guide";
    public const string Move = "hisano-guide-move";

    public static Dictionary<string, AbilityDefinition> AddHisanoGuide(this Dictionary<string, AbilityDefinition> all)
    {
        all[Create] = new("Reaction", "Once per game, after Hisano enters play: Create Hisano's Guide, a 2F/2C/3PH Mantis Clan · Nonhuman · Fox · Spirit · Unique Personality", "none",
            Trigger: "own-enters-play", OncePerGame: true,
            Creates: new(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3,
                Keywords: ["Mantis Clan", "Nonhuman", "Fox", "Spirit", "Unique"],
                AbilityText: "Battle: Target an enemy attacking Personality: Move him home.", ProxyId: "oracle-l5r-10020"));
        all[Move] = new("Battle", "Target an enemy attacking Personality: Move him home", "enemy-attacking");
        return all;
    }
}

public sealed partial class MatchEngine
{
    private bool? RunHisanoGuide(string id, CardInstance? target, PlayerState player)
    {
        if (id != HisanoGuideAbilities.Move) return null;
        MoveHome(target!, false);
        return false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before catalogue construction.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterHisanoGuideHooks()
    {
        EngineHooks.Abilities.Register("AddHisanoGuide", 10000, static abilities => abilities.AddHisanoGuide());
        EngineHooks.RunEffects.Register("RunHisanoGuide", 10000, static (engine, context) => engine.RunHisanoGuide(context.id, context.target, context.player));
    }
}
