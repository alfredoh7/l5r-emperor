namespace L5R.Core;

public sealed partial class MatchEngine
{
    private bool? RunHoukenExperienced(string id, PlayerState player, CardInstance? source)
    {
        if (id != "houken-exp-straighten") return null;
        StraightenUnit(source!);
        if (_state.CurrentBattleProvince is int battlefield && Army(1 - player.Id, battlefield).Any())
            MoveToBattlefield(source!, battlefield);
        else MoveHome(source!, false);
        return false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature metadata before use.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterHoukenExperienced() => EngineHooks.RunEffects.Register("HoukenExperienced.RunEffects", 10000,
        static (engine, context) => engine.RunHoukenExperienced(context.id, context.player, context.source));
}
