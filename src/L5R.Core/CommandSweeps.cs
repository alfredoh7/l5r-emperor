namespace L5R.Core;

// Post-command sweeps as registered, ordered steps (issue #853). MatchEngine.Execute used to call a hand-ordered list of SweepX / WaveNNNAfterCommand methods, so the order was implicit
// and a new sweep was one more line in a long method. Each phase below is an OrderedHooks list: a sweep registers once under a name and a priority, and CommandSweepTests pins the order.
//   BeforeApply    -- on the working copy of the state, before the command is validated and applied (a rejected command is discarded together with it, so it never mutates the live state).
//   AfterApply     -- after the command's effects, before the aftermath records and the version bump.
//   AfterAccepted  -- once an accepted command is complete (it receives the turn the command started in).
internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Action<MatchEngine>> SweepsBeforeApply = new();
    internal static readonly OrderedHooks<Action<MatchEngine>> SweepsAfterApply = new();
    internal static readonly OrderedHooks<Action<MatchEngine, int>> SweepsAfterAccepted = new();
}

public sealed partial class MatchEngine
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register the command sweeps before any engine reader.")]
    internal static void RegisterCommandSweeps()
    {
        EngineHooks.SweepsBeforeApply.Register("SpellCopies", 100, static engine => engine.SweepSpellCopies()); // issue #838: a copy owed to a card that left play before this command

        EngineHooks.SweepsAfterApply.Register("ReturnCardsToOwners", 100, static engine => engine.ReturnCardsOutOfPlayToTheirOwners());
        EngineHooks.SweepsAfterApply.Register("SpellCopies", 200, static engine => engine.SweepSpellCopies()); // issue #838
        EngineHooks.SweepsAfterApply.Register("Wave827", 300, static engine => engine.Wave827AfterCommand()); // Koan's Jingasa's Spell limit, P'an Ku's straightening choice
        EngineHooks.SweepsAfterApply.Register("Wave829", 400, static engine => engine.Wave829AfterCommand()); // Benten's Blessing's choice of Personalities
        EngineHooks.SweepsAfterApply.Register("Wave835", 500, static engine => engine.Wave835AfterCommand()); // the Heavens' Blessing's owed draws, Unbearable Weight's return to a hand
        EngineHooks.SweepsAfterApply.Register("CreatedCardsInPiles", 600, static engine => engine.RemoveCreatedCardsLeftInPiles());

        EngineHooks.SweepsAfterAccepted.Register("Fortifications", 100, static (engine, _) => engine.SweepFortifications()); // P15 (FortificationHoldings.cs)
        EngineHooks.SweepsAfterAccepted.Register("ZoneHistory", 200, static (engine, turnAtStart) => engine.SweepZoneHistory(turnAtStart)); // P21 (ZoneExtensions.cs)
        EngineHooks.SweepsAfterAccepted.Register("Facts", 300, static (engine, _) => engine.SweepFactsAfterCommand()); // P6 (AfterFacts.cs): facts only a comparison sees, and the drain when nothing is open
    }

    internal static IReadOnlyList<string> SweepOrderForTest(string phase) => phase switch
    {
        "before" => EngineHooks.SweepsBeforeApply.Names,
        "after" => EngineHooks.SweepsAfterApply.Names,
        _ => EngineHooks.SweepsAfterAccepted.Names,
    };

    private void RunSweeps(OrderedHooks<Action<MatchEngine>> phase) { foreach (var sweep in phase.Items) sweep(this); }
}
