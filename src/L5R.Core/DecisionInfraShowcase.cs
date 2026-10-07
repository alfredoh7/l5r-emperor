namespace L5R.Core;

// P25 (issue #723): isolated review positions for the three decision shapes the infrastructure adds, for the native decision probe (tools/check-decision-chooser.sh, modes p25-unknown, p25-multi and
// p25-shown). Like the other showcases (DecisionShowcase.cs) they exist only through MatchEngine.CreateDecisionShowcase: no deck, room or card can open them, because the pending type and the specs
// below are named by nothing else.
public sealed partial class MatchEngine
{
    internal const string ShowcaseUnknownType = "p25Showcase", ShowcasePickCommand = "p25ShowcasePick", ShowcaseMultiSpec = "p25-showcase-multi", ShowcaseShownSpec = "p25-showcase-shown";
    private static readonly string[] ShowcaseKeys = ["alpha", "beta", "gamma"];

    private static MatchEngine CreateDecisionInfraShowcase(string kind)
    {
        var state = CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null; state.CurrentBattleProvince = null;
        foreach (var id in state.Players[0].Hand.ToArray()) state.Cards.Remove(id);
        state.Players[0].Hand.Clear();
        var source = new CardInstance { Id = "p25-review-source", DefinitionId = "oracle-l5r-3636", OwnerId = 0, Zone = Zone.Home, FaceUp = true };
        state.Cards.Add(source.Id, source);
        var engine = FromTestState(state);
        switch (kind)
        {
            case "p25-unknown":
                engine._state.PendingChoice = new(ShowcaseUnknownType, 0, source.Id, "Review: a pending decision the client has no screen for. Choose one.");
                break;
            case "p25-multi":
                if (!engine.OpenEitherOr(ShowcaseMultiSpec, engine.Card(source.Id), 0, 0, null, "action", min: 1, max: 2)) throw new InvalidOperationException("The multi-pick showcase did not open.");
                break;
            case "p25-shown":
                var first = engine.ShowCardOf(1, "oracle-l5r-1450", source); var second = engine.ShowCardOf(1, "oracle-l5r-5523", source);
                if (!engine.OpenShownChoice(ShowcaseShownSpec, engine.Card(source.Id), 1, 0, [first, second], 1, 1, "Review: choose one of the cards your opponent showed.")) throw new InvalidOperationException("The shown-choice showcase did not open.");
                break;
            default: throw new ArgumentException("Unknown decision showcase.", nameof(kind));
        }
        return engine;
    }

    private int ShowCardOf(int playerId, string definitionId, CardInstance source)
    {
        _state.ShownCards.Add(new ShownCard(_state.ShownCards.Count + 1, playerId, definitionId, source.Id, source.DefinitionId, SearchDeck.Fate, "review", _state.TurnNumber, _state.Events.Count));
        return _state.ShownCards.Count;
    }

    // Registration only: callbacks are not invoked during module initialization.
    private static void RegisterDecisionInfraShowcase()
    {
        EngineHooks.PendingChoice.Register("P25ShowcasePick", 16701, static (engine, type, command) => engine.HandleShowcasePick(type, command));
        EngineHooks.PendingActions.Register("P25ShowcaseActions", 16701, static (engine, context) => context.Choice.Type == ShowcaseUnknownType ? engine.ShowcaseActions(context) : null);
        EngineHooks.EitherOrPrompt.Register("P25ShowcasePrompt", 16701, static (engine, p, source, affected) => p.SpecId == ShowcaseMultiSpec ? $"{source}: choose one or two of the three omens." : null);
        EngineHooks.EitherOrOptions.Register("P25ShowcaseOptions", 16701, static (engine, p, affected, source, payable) => p.SpecId != ShowcaseMultiSpec ? null :
            ShowcaseKeys.Select(key => new EitherOrOption(key, char.ToUpperInvariant(key[0]) + key[1..], $"The {key} omen is read.", EitherOrKind.Choose, 0, true, null)).ToArray());
        EngineHooks.ApplyEitherOr.Register("P25ShowcaseApply", 16701, static (engine, p, option, source, decider, affected, sourceIds) =>
        {
            if (p.SpecId != ShowcaseMultiSpec) return false;
            if (option.Key != EitherOrDoneKey) engine.Emit("eitherOrChosen", $"{decider.Name} read the {option.Key} omen.", decider.Id, source.Id);
            return true;
        });
        EngineHooks.ShownChoiceApply.Register("P25ShowcaseShownApply", 16701, static (engine, p) =>
        {
            if (p.SpecId != ShowcaseShownSpec) return false;
            foreach (var sequence in p.Chosen) engine.Emit("shownApplied", $"The chosen card was {engine.ShownCandidateName(sequence)}.", p.DeciderPlayerId, p.SourceCardId);
            engine.NextAction(p.ActingPlayerId);
            return true;
        });
    }

    private IEnumerable<ActionOption> ShowcaseActions(PendingActionsContext context) =>
        _state.PendingChoice?.PlayerId != context.PlayerId ? [] : ShowcaseKeys.Select(key => new ActionOption(ShowcasePickCommand, "Take the " + key + " path", true, null,
            new GameCommand(ShowcasePickCommand, context.PlayerId, _state.Version, Choice: key), _state.PendingChoice!.CardId));

    private bool HandleShowcasePick(string type, GameCommand command)
    {
        if (type != ShowcaseUnknownType) return false;
        Require(command.Type == ShowcasePickCommand && command.Choice is not null && ShowcaseKeys.Contains(command.Choice), "Choose one of the offered paths.");
        Emit("p25ShowcasePicked", $"{Player(command.PlayerId).Name} took the {command.Choice} path.", command.PlayerId);
        _state.PendingChoice = null;
        NextAction(command.PlayerId);
        return true;
    }
}
