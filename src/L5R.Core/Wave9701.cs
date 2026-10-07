namespace L5R.Core;

// Issue #689: P11 cards whose complete printed behavior is supported by the restriction APIs.
public static class Wave9701Abilities
{
    public const string JinE = "w9701-jin-e", Reprieve = "w9701-reprieve", Gap = "w9701-gap", Blockade = "w9701-blockade";
    public static Dictionary<string, AbilityDefinition> Add(Dictionary<string, AbilityDefinition> all)
    {
        all[JinE] = new("Open", "Give a target Personality and Jin-e each a -1F/-1C Poison token", "w9701-personality", Costs: ActionCost.BowSource, ActionKeywords: ["Ninja"]);
        all[Reprieve] = new("Open", "Other players' actions may not target your honorable Personality this phase", "w9701-honorable");
        all[Gap] = new("Open", "Two Personalities with the same Family name may not be assigned to the same battlefield", "slots", Slots: [new("first", "w9701-personality", "A Personality"), new("second", "w9701-personality", "Another Personality of the same Family")]);
        all[Blockade] = new("Open", "Cards will not enter play from the target province this turn", "any-province");
        return all;
    }
}

public sealed partial class MatchEngine
{
    private bool? RunWave9701(string id, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case Wave9701Abilities.JinE:
                CreateTwoStatToken(target!, "Poison", source!.Id, "Force", -1, "Chi", -1);
                CreateTwoStatToken(source, "Poison", source.Id, "Force", -1, "Chi", -1);
                return false;
            case Wave9701Abilities.Reprieve:
                AddTargetBarRule(target!, new(Players: "others"), until: RestrictionUntil.Phase);
                return false;
            case Wave9701Abilities.Gap:
                AddPairRestriction(SlotCards(command, "first").Single(), SlotCards(command, "second").Single());
                return false;
            case Wave9701Abilities.Blockade:
                AddProvinceEntryLock(command.TargetPlayerId!.Value, command.Province!.Value);
                return false;
            default: return null;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register card callbacks before catalogue readers.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave9701()
    {
        CardRestrictionCatalog.Register("oracle-l5r-10864", new StaticRestriction(RestrictionKind.NoAttach, "type:Follower|Item"));
        CardRestrictionCatalog.Register("oracle-l5r-10868", new StaticRestriction(RestrictionKind.NoAttach, "keyword:Armor"));
        CardRestrictionCatalog.Register("oracle-l5r-10284", new(RestrictionKind.ActWhileBowed), new(RestrictionKind.AssignWhileBowed));
        CardRestrictionCatalog.Register("oracle-l5r-10666", new(RestrictionKind.NoAttach, "any"), new(RestrictionKind.NoChallenge));
        foreach (var id in new[] { "oracle-l5r-10837", "oracle-l5r-10666" }) CardRestrictionCatalog.RegisterTargetBars(id, new StaticTargetBar(new(Players: "controller", ActionKeyword: "Political")));
        EngineHooks.Abilities.Register("Wave9701", 12700, Wave9701Abilities.Add);
        EngineHooks.TargetAllows.Register("Wave9701Targets", 12700, (engine, c) =>
            Definition(c.target).Type == CardType.Personality && (c.ability.Target == "w9701-personality" || c.ability.Target == "w9701-honorable" && c.target.OwnerId == c.player.Id && !c.target.Dishonorable));
        EngineHooks.ValidateLate.Register("Wave9701Validate", 12700, (engine, c) =>
        {
            if (c.id == Wave9701Abilities.Gap) Require(engine.SameFamilyName(engine.SlotCards(c.command, "first").Single(), engine.SlotCards(c.command, "second").Single()), "Choose two different Personalities of the same Family.");
            if (c.id == Wave9701Abilities.Blockade) Require(engine.Player(engine._state.ActivePlayerId).TurnsTaken > 1, "Sudden Blockade cannot be used during the active player's first turn.");
        });
        EngineHooks.RunEffects.Register("Wave9701Run", 12700, (engine, c) => engine.RunWave9701(c.id, c.command, c.player, c.source, c.target));
    }
}
