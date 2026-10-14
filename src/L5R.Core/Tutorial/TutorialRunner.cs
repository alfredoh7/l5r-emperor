namespace L5R.Core;

/// <summary>What the coach panel shows for the current step (issue #935). Highlights are card ids to point at; the panel never decides anything.</summary>
public sealed record TutorialCoach(string StepId, string Title, string Body, string? Button, string[] HighlightCardIds, int Step, int Steps, bool Finished, string? Instruction = null);

/// <summary>One beat of a lesson. A narration step has a <see cref="Button"/> and waits for it; an action step has an <see cref="Allow"/> and waits for the player to make that move.
/// Everything else the engine would offer is shown as unavailable, so a click the lesson does not want never reaches the engine.</summary>
internal sealed class TutorialStep
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    /// <summary>Narration: the label of the button that moves on (also run <see cref="OnContinue"/>). Null for an action step.</summary>
    public string? Button { get; init; }
    public Action<MatchEngine>? OnContinue { get; init; }
    /// <summary>Action step: which engine options stay available (type, ability, card), judged from the option's command.</summary>
    public Func<TutorialLookup, GameCommand, bool>? Allow { get; init; }
    /// <summary>Action step: the parameters the lesson requires on the command itself (the cards chosen for a province refresh, the unit equipped), checked when it is submitted.</summary>
    public Func<TutorialLookup, GameCommand, bool>? Accept { get; init; }
    /// <summary>Narrows which of the engine's variants of one action is offered (a purchase's Clan-discount and full-cost forms), so the others never appear.</summary>
    public Func<GameCommand, bool>? Variant { get; init; }
    /// <summary>Action step: exactly what to do for the lesson to move on, shown apart from the explanation. There is no button until it is done.</summary>
    public string? Instruction { get; init; }
    public string RefusalHint { get; init; } = "Follow the highlighted step first.";
    public Func<TutorialLookup, IEnumerable<string>>? Highlight { get; init; }
    /// <summary>Action step: the only cards the move may target (the three face-up provinces for the Border Keep), so the table offers just those.</summary>
    public Func<TutorialLookup, IEnumerable<string>>? Targets { get; init; }
    /// <summary>Action step: the only provinces an attack or defence may name.</summary>
    public int[]? OnlyProvinces { get; init; }
    public bool IsAction => Allow is not null;
}

/// <summary>Resolves "the Border Keep" and "Akodo Uehara" in a running lesson to card ids, for step predicates and highlights.</summary>
internal sealed class TutorialLookup(MatchEngine engine)
{
    public string DefinitionOf(string? cardId) => cardId is null ? "" : engine.TutorialDefinitionOf(cardId);
    public bool Is(string? cardId, string definitionId) => cardId is not null && DefinitionOf(cardId) == definitionId;
    public string[] Own(string definitionId, params Zone[] zones) => engine.TutorialCardIds(TutorialRunner.Seat, definitionId, zones);
    public string[] Foe(string definitionId, params Zone[] zones) => engine.TutorialCardIds(1 - TutorialRunner.Seat, definitionId, zones);
    public string Stronghold(int owner) => engine.TutorialStrongholdId(owner);
    public string? Province(int owner, int index) => engine.TutorialProvinceCardId(owner, index);
}

/// <summary>Plays a scripted lesson over the real engine (issue #935). The engine stays the only authority on legality: the runner narrows what is offered, plays the practice opponent's side of the script,
/// and moves the coach panel along. Offline only, and the lesson's table is never used for a room.</summary>
public sealed class TutorialRunner
{
    public const int Seat = 0;
    private readonly MatchEngine _engine;
    private readonly TutorialStep[] _steps;
    private readonly TutorialLookup _lookup;
    private int _index;
    private int? _attackedProvince;

    private TutorialRunner(MatchEngine engine, TutorialStep[] steps) { _engine = engine; _steps = steps; _lookup = new(engine); Settle(); }

    /// <summary>Lesson 1: cards and card types.</summary>
    public static TutorialRunner StartLessonOne() { var engine = MatchEngine.CreateTutorial(); return new(engine, TutorialLessonOne.Steps); }

    public MatchEngine Engine => _engine;
    public bool Finished => _index >= _steps.Length;
    public string? Refusal { get; private set; }

    private TutorialStep? Current => Finished ? null : _steps[_index];

    public TutorialCoach Coach
    {
        get
        {
            if (Current is not { } step) return new("done", "Lesson complete", "", null, [], _steps.Length, _steps.Length, true);
            return new(step.Id, step.Title, step.Body, step.Button, step.Highlight?.Invoke(_lookup).ToArray() ?? [], _index + 1, _steps.Length, false, step.Instruction);
        }
    }

    /// <summary>The engine's own options for you, with everything the current step does not ask for switched off.</summary>
    public ActionOption[] Actions()
    {
        var offered = _engine.QueryActions(Seat);
        if (Current is not { IsAction: true } step || _engine.PriorityPlayerId != Seat)
            return offered.Select(a => a with { Enabled = false, Reason = "Read the tutorial note, then press its button." }).ToArray();
        var targets = step.Targets?.Invoke(_lookup).ToHashSet();
        return offered.Select(a => a.Enabled && a.Command is { } command && step.Allow!(_lookup, command) && (step.Variant is null || step.Variant(command))
            ? a with
            {
                TargetIds = targets is not null && a.TargetIds is not null ? a.TargetIds.Where(targets.Contains).ToArray() : a.TargetIds,
                Provinces = step.OnlyProvinces is { } only && a.Provinces is not null ? a.Provinces.Where(only.Contains).ToArray() : a.Provinces
            }
            : a with { Enabled = false, Reason = a.Enabled ? step.RefusalHint : a.Reason }).ToArray();
    }

    /// <summary>A move from the table. Anything the step does not ask for is refused with the step's hint, before the engine sees it.</summary>
    public CommandResult Execute(GameCommand command)
    {
        Refusal = null;
        if (Current is not { IsAction: true } step || !step.Allow!(_lookup, command) || step.Accept is { } accept && !accept(_lookup, command))
        {
            Refusal = Current?.RefusalHint ?? "The lesson is over.";
            return new(false, Refusal, _engine.GetView(Seat).Version, []);
        }
        var result = _engine.Execute(command);
        if (!result.Accepted) { Refusal = result.Error; return result; }
        if (command.Type == "attack") _attackedProvince = command.Assignments is { Count: > 0 } assignments ? assignments.Values.First() : command.Province;
        _index++; Settle();
        return result;
    }

    /// <summary>The button of a narration step.</summary>
    public bool Continue()
    {
        if (Current is not { IsAction: false } step) return false;
        step.OnContinue?.Invoke(_engine);
        _index++; Settle();
        return true;
    }

    /// <summary>Plays the opponent's side of the script, and your own passes while it is the opponent's turn, until a decision is yours.</summary>
    private void Settle()
    {
        for (var guard = 0; guard < 80 && _engine.WinnerId is null; guard++)
        {
            var seat = _engine.PriorityPlayerId;
            var options = _engine.QueryActions(seat).Where(a => a.Enabled && a.Command is not null).ToArray();
            ActionOption? pick;
            if (seat != Seat) pick = FoeChoice(options);
            else if (_engine.GetView(Seat).ActivePlayerId != Seat) pick = options.FirstOrDefault(a => a.Type == "declineReaction") ?? options.FirstOrDefault(a => a.Type == "pass");
            else return;
            if (pick is null || !_engine.Execute(pick.Command!).Accepted) return;
        }
    }

    private ActionOption? FoeChoice(ActionOption[] options) =>
        options.FirstOrDefault(a => a.Type == "declineReaction")
        ?? options.FirstOrDefault(a => a.Type == "assignDefense" && _attackedProvince is int attacked && (a.Command!.Assignments?.Values.Contains(attacked) ?? a.Command.Province == attacked))
        ?? options.Where(a => a.Type == "purchase" && Engine.TutorialDefinitionOf(a.CardId!) == TutorialCards.FoeDefender).OrderBy(a => a.Quote?.Cost ?? 0).FirstOrDefault() // its one purchase: the defender
        ?? options.FirstOrDefault(a => a.Type == "pass")
        ?? options.FirstOrDefault(a => a.Type == "endDynasty");
}
