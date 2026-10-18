namespace L5R.Core;

// Issue #859: the two printed actions the printed-action sweep found missing on cards whose other text was already wired.
//
//   Tamori Kazushige (oracle-l5r-7777)  "Open: If it is another player's turn, target one of his Personalities: Before this phase ends, straighten him. Before this turn ends, if the Personality did
//                                        not assign to attack this turn, gain 2 Honor."   Bayushi Misaki's watch (MisakiWatch) with a different turn-end reward: the acting player gains 2 Honor.
//   Togashi Korimi (oracle-l5r-8545)    "Interrupt, Bow this card: After you Recruit Korimi, draw a card."   Read as an entering-play Reaction (Korimi enters play by being recruited) that bows him as its cost.
public static class Wave859Abilities
{
    public const string KazushigeStraighten = "w859-kazushige-straighten", KorimiDraw = "w859-korimi-draw", KazushigeKind = "kazushige";
    public const int KazushigeHonor = 2;

    public static Dictionary<string, AbilityDefinition> AddWave859(this Dictionary<string, AbilityDefinition> all)
    {
        all[KazushigeStraighten] = new("Open", "If it is another player's turn, target one of his Personalities: Before this phase ends, straighten him. Before this turn ends, if the Personality did not assign to attack this turn, gain 2 Honor", "enemy-personality");
        all[KorimiDraw] = new("Reaction", "Bow this card: After you Recruit Korimi, draw a card", "none", Trigger: "own-enters-play", Costs: ActionCost.BowSource);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private void Wave859Validate(ValidationHookContext context)
    {
        if (context.id != Wave859Abilities.KazushigeStraighten) return;
        // The same condition as Bayushi Misaki's: another player's turn, and one of the active player's Personalities.
        Require(_state.ActivePlayerId != context.player.Id, "Tamori Kazushige acts only on another player's turn.");
        if (context.command.TargetId is { } id && _state.Cards.TryGetValue(id, out var target)) Require(target.OwnerId == _state.ActivePlayerId, "Target one of the active player's Personalities.");
    }

    private bool? RunWave859(RunHookContext c)
    {
        switch (c.id)
        {
            case Wave859Abilities.KazushigeStraighten:
                _state.MisakiWatches.Add(new(c.target!.Id, c.source!.Id, c.player.Id, _state.TurnNumber, Kind: Wave859Abilities.KazushigeKind));
                Emit("kazushigeWatch", $"{Definition(c.source).Name}: {Definition(c.target).Name} will be straightened before this phase ends; {c.player.Name} gains 2 Honor before the turn ends unless he is assigned to attack.", c.player.Id, c.target.Id);
                return false;
            case Wave859Abilities.KorimiDraw:
                Draw(c.player);
                return false;
            default:
                return null;
        }
    }

    // The turn-end half of Kazushige's watch (ResolveShadowsEmbraceBatch1TurnEnd calls it for a watch of his kind whose target was not assigned to attack).
    private void Wave859KazushigeTurnEnd(MisakiWatch watch, CardInstance? source) =>
        GainHonor(watch.ActingPlayerId, Wave859Abilities.KazushigeHonor, source is null ? "Tamori Kazushige" : Definition(source).Name, source);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave859Hooks()
    {
        EngineHooks.Abilities.Register("Wave859Abilities", 13859, static abilities => abilities.AddWave859());
        EngineHooks.ValidateEarly.Register("Wave859Validate", 13859, static (engine, context) => engine.Wave859Validate(context));
        EngineHooks.RunEffects.Register("Wave859RunEffects", 13859, static (engine, context) => engine.RunWave859(context));
    }
}
