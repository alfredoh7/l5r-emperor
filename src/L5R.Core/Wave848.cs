namespace L5R.Core;

// Issue #848: The Courtesy of the Crane's Open action (oracle-l5r-7996, strategy-wave-5; its Focus Effect was already wired).
//
//   "Open: Choose your performing unbowed Crane Clan Personality: Choose another player. He may draw a card. Draw two cards."
//
// The game has two players, so "another player" is the opponent: he decides whether to draw (the Either/Or decision), then the Strategy's player draws two cards. Both draws go through Draw, so
// The Heavens' Blessing (Wave835.cs) makes each of the player's own two cards a 2 Gold payment while the other player's optional card stays free: it is not an effect of his own Strategy.
public static class Wave848Abilities
{
    public const string CourtesyOpen = "courtesy-of-the-crane-open", CourtesyDraw = "w848-courtesy-draw";

    public static Dictionary<string, AbilityDefinition> AddWave848(this Dictionary<string, AbilityDefinition> all)
    {
        all[CourtesyOpen] = new("Open", "Open: Choose your performing unbowed Crane Clan Personality: Choose another player. He may draw a card. Draw two cards", "none",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Crane Clan"], ActionKeywords: ["Political"]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private bool? RunWave848(RunHookContext c)
    {
        if (c.id != Wave848Abilities.CourtesyOpen) return null;
        // The Strategy leaves the hand as it is played and the decision opens as an action's own tail would have (the same shape as Courteous Gift's).
        SpendHandPlayedCard(c.player, c.source);
        if (!c.consumedGrant) _state.AdditionalActions.RemoveAll(c.preExistingGrants.Contains);
        return OpenEitherOr(Wave848Abilities.CourtesyDraw, c.source!, c.player.Id, 1 - c.player.Id, Card(c.command.PerformerId!), "action", c.bowedBeforeAction,
            stayWithActor: _state.AdditionalActions.Any(g => g.PlayerId == c.player.Id && Forfeitable(g)), performerId: c.command.PerformerId);
    }

    private string? Wave848Prompt(PendingEitherOr p, string source) =>
        p.SpecId == Wave848Abilities.CourtesyDraw ? $"{source}: you may draw a card. Its player then draws two cards." : null;

    private EitherOrOption[]? Wave848Options(PendingEitherOr p) =>
        p.SpecId != Wave848Abilities.CourtesyDraw ? null
            : [new("draw", "Draw a card", "You draw a card.", EitherOrKind.Choose, 0, true, null), new("decline", "Do not draw", "You draw nothing.", EitherOrKind.Choose, 0, true, null)];

    private bool Wave848Apply(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider)
    {
        if (p.SpecId != Wave848Abilities.CourtesyDraw) return false;
        var drew = option.Key == "draw";
        if (drew) Draw(decider);
        Emit("eitherOrChosen", drew ? $"{decider.Name} draws a card ({Definition(source).Name})." : $"{decider.Name} does not draw ({Definition(source).Name}).", decider.Id, source.Id);
        var actor = Player(p.ActingPlayerId);
        Draw(actor); Draw(actor); // "Draw two cards": the Strategy's player
        return true;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave848Hooks()
    {
        EngineHooks.Abilities.Register("Wave848Abilities", 13848, static abilities => abilities.AddWave848());
        EngineHooks.RunEffects.Register("Wave848RunEffects", 13848, static (engine, context) => engine.RunWave848(context));
        EngineHooks.EitherOrPrompt.Register("Wave848EitherOrPrompt", 14848, static (engine, p, source, affected) => engine.Wave848Prompt(p, source));
        EngineHooks.EitherOrOptions.Register("Wave848EitherOrOptions", 14848, static (engine, p, affected, source, payable) => engine.Wave848Options(p));
        EngineHooks.ApplyEitherOr.Register("Wave848ApplyEitherOr", 14848, static (engine, p, option, source, decider, affected, sourceIds) => engine.Wave848Apply(p, option, source, decider));
    }
}
