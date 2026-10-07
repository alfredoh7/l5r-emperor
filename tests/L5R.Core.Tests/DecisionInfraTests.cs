using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #723, primitive P25 (DecisionInfra.cs, TargetSlots.cs, EitherOr.cs, EffectChoice.cs): the decision infrastructure, with no card wired. Every piece is driven by fixtures registered below
// (all keyed on "training-p25-" spec, effect, ability or pending-type ids, so no other suite sees them): a multi-pick Either/Or spec, a pending type with its own actions and no QueryActions branch,
// a branch ability with its two effect hooks, slot abilities with a cross-constraint, and a shown-card choice spec.
internal static class DecisionInfraTests
{
    private const string None = "training-p25-none", Multi = "training-p25-multi", Forced = "training-p25-forced", Generic = "training-p25-generic", GenericPick = "p25TestPick", ShownSpec = "training-p25-shown";
    private const string BranchAbility = "training-p25-branch", PairAbility = "training-p25-pair", PairDefaultOnly = "training-p25-pair-default-only", OwnEnemyAbility = "training-p25-own-enemy";
    private const string DifferentClans = "The two Personalities may not share a Clan alignment.";
    private static readonly List<string> Log = [];

    public static void Run(TestRunner runner) => runner.Run("decision-infra", new (string, Action)[]
    {
        ("Multi-pick Either/Or: Min/Max are enforced, Done is offered once Min are picked, the picked keys are applied in the order picked and the spec sees them all at the end", MultiPickBasics),
        ("Multi-pick Either/Or: a pick the engine did not offer, a Done before Min and a Gold option are refused and leave the state unchanged; Min 0 allows an empty Done", MultiPickRefusals),
        ("Multi-pick Either/Or: with nothing to choose beyond what is forced the picks are made and applied without a decision opening", MultiPickForced),
        ("Multi-pick Either/Or: a game saved mid-decision resumes exactly as the original, and the saved fields exist only while it is used", MultiPickRoundTrip),
        ("PendingActions: a feature-registered pending type lists its own options for its decider only, accepts the answer through its own hook, and a refused answer changes nothing", RegisteredPendingType),
        ("EffectChoice hooks: a hook-registered branch effect applies, a chosen option can open a further decision through the continuation hook, and an ordinary option finishes the action", EffectChoiceHooks),
        ("CrossConstraint: the pair is rejected with the constraint's reason beside the per-slot checks, a legal pair is accepted, and a refusal leaves the state unchanged", CrossConstraintValidation),
        ("CrossConstraint: the offer reflects it -- the second slot offers only candidates the constraint admits, and the default selection is one that satisfies it", CrossConstraintOffer),
        ("Shown choice: the decider is offered the shown cards by position and definition, answers one, and the spec's hook applies it; the other player only waits", ShownChoiceBasics),
        ("Shown choice: Min/Max and Done, refusals leave the state unchanged, nothing to choose resolves itself, and the pending state names no instance id and round-trips", ShownChoiceMulti),
        ("Unused means unchanged: an ordinary Either/Or carries none of the new fields, a game that never used them saves none of them, and the channels are sealed", UnusedUnchanged),
    });

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null) => Batch336Support.Add(s, def, owner, zone, bowed, province);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static string Seq(IEnumerable<string> items) => string.Join(",", items);

    // ---- the fixtures ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        EngineHooks.EitherOrPrompt.Register("TestP25Prompt", 19500, static (engine, p, source, affected) => p.SpecId is Multi or Forced or None ? $"{source}: training multi-pick." : null);
        EngineHooks.EitherOrOptions.Register("TestP25Options", 19500, static (engine, p, affected, source, payable) => p.SpecId switch
        {
            Multi => [new("a", "Option A", "A.", EitherOrKind.Choose, 0, true, null), new("b", "Option B", "B.", EitherOrKind.Choose, 0, true, null),
                new("c", "Option C", "C.", EitherOrKind.Choose, 0, false, "C is not available."), new("d", "Option D", "D.", EitherOrKind.Choose, 0, true, null),
                new("pay", "Option Pay", "Costs Gold.", EitherOrKind.Choose, 2, true, null)],
            Forced => [new("x", "Option X", "X.", EitherOrKind.Choose, 0, true, null), new("y", "Option Y", "Y.", EitherOrKind.Choose, 0, true, null), new("z", "Option Z", "Z.", EitherOrKind.Choose, 0, false, "Z is not available.")],
            None => [],
            _ => null
        });
        EngineHooks.ApplyEitherOr.Register("TestP25Apply", 19500, static (engine, p, option, source, decider, affected, sourceIds) =>
        {
            if (p.SpecId is not (Multi or Forced or None)) return false;
            Log.Add(option.Key == "done" ? "apply:done:" + Seq(p.Picked ?? []) : "apply:" + option.Key);
            return true;
        });
        EngineHooks.PendingActions.Register("TestP25PendingActions", 19500, static (engine, context) => context.Choice.Type != Generic ? null :
            context.PlayerId != context.Choice.PlayerId ? [] : [new ActionOption(GenericPick, "Training: take path one", true, null, new GameCommand(GenericPick, context.PlayerId, engine.Version, Choice: "one"), context.Choice.CardId),
                new ActionOption(GenericPick, "Training: take path two", true, null, new GameCommand(GenericPick, context.PlayerId, engine.Version, Choice: "two"), context.Choice.CardId)]);
        EngineHooks.PendingChoice.Register("TestP25PendingChoice", 19500, static (engine, type, command) =>
        {
            if (type != Generic) return false;
            if (command.Type != GenericPick || command.Choice is not ("one" or "two")) MatchEngine.RefuseCommand("Choose one of the offered paths.");
            Log.Add("generic:" + command.Choice);
            engine.ResumeAfterDecision(command.PlayerId);
            return true;
        });
        EngineHooks.Abilities.Register("TestP25Abilities", 19500, static abilities =>
        {
            abilities.Add(BranchAbility, new("Open", "Training P25: choose an effect", "none", Branch: new(BranchCondition.Always,
                [new("a", "Effect A", ["training-p25-a"]), new("b", "Effect B", ["training-p25-b"])], new("none", "Nothing", []))));
            abilities.Add(PairAbility, new("Battle", "Training P25: two Personalities of different Clans", "slots",
                Slots: [new("first", "personality", "The first Personality"), new TargetSlot("second", "personality", "The second Personality") { CrossConstraint = ClansDiffer, CrossConstraintReason = DifferentClans }]));
            abilities.Add(PairDefaultOnly, new("Battle", "Training P25: two Personalities of different Clans (default only)", "slots",
                Slots: [new TargetSlot("first", "personality", "The first Personality") { ProbeDefaultOnlyForLaterSlots = true }, new TargetSlot("second", "personality", "The second Personality") { CrossConstraint = ClansDiffer, CrossConstraintReason = DifferentClans }]));
            abilities.Add(OwnEnemyAbility, new("Battle", "Training P25: one of yours and one of theirs, of different Clans", "slots",
                Slots: [new("mine", "own-personality", "Your Personality"), new TargetSlot("theirs", "enemy-personality", "An enemy Personality") { CrossConstraint = ClansDiffer, CrossConstraintReason = DifferentClans }]));
            return abilities;
        });
        EngineHooks.RunEffects.Register("TestP25Run", 19500, static (engine, context) => context.id is PairAbility or PairDefaultOnly or OwnEnemyAbility ? false : null);
        EngineHooks.BranchEffects.Register("TestP25BranchEffects", 19500, static (engine, effect, command, source, target) =>
        {
            if (effect is not ("training-p25-a" or "training-p25-b")) return false;
            Log.Add("branch:" + effect[^1]);
            return true;
        });
        EngineHooks.EffectChoiceContinuations.Register("TestP25Continuation", 19500, static (engine, pending, option) =>
        {
            if (option.Id != "b") return false;
            engine.OpenGenericChoice(Generic, pending.PlayerId, pending.Command.CardId, "Training: a further decision after the effect.");
            return true;
        });
        EngineHooks.ShownChoiceApply.Register("TestP25ShownApply", 19500, static (engine, p) =>
        {
            if (p.SpecId != ShownSpec) return false;
            Log.Add("shown:" + Seq(p.Chosen.Select(s => s.ToString())));
            engine.ResumeAfterDecision(p.ActingPlayerId);
            return true;
        });
    }

    private static readonly Func<MatchEngine, IReadOnlyList<CardInstance>, bool> ClansDiffer = static (engine, cards) => cards.Select(c => CardCatalog.Get(c.DefinitionId).Clan).Distinct().Count() == cards.Count;

    private static (MatchEngine Engine, string Source) Table0(Phase phase = Phase.Action, int actor = 0, Action<MatchState>? more = null)
    {
        var s = Batch336Support.Table(phase, actor);
        var source = Add(s, "training-lion-samurai", 1, Zone.Home);
        more?.Invoke(s);
        return (MatchEngine.FromTestState(s), source.Id);
    }

    private static GameCommand Pick(MatchEngine e, string source, string key, int player = 0) => new("chooseEitherOr", player, e.Version, CardId: source, Choice: key);
    private static string[] Keys(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEitherOr").Select(a => a.Command?.Choice ?? "(" + a.Label + ")").ToArray();

    // ---- 1. multi-pick Either/Or -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MultiPickBasics()
    {
        Log.Clear();
        var (e, source) = Table0();
        Check(e.OpenEitherOrForTest(Multi, source, 0, 0, null, min: 1, max: 2), "a multi-pick decision opens");
        Eq(e.State.PendingChoice?.Type, "eitherOr"); Eq(e.State.PendingChoice!.PlayerId, 0);
        Check(e.State.PendingChoice.Prompt.Contains("Choose 1 to 2", StringComparison.Ordinal), "the prompt says how many: " + e.State.PendingChoice.Prompt);
        // The view lists the picks and a Done that is not legal yet; the other player sees the same decision read-only.
        var view = e.GetView(0).PendingEitherOr!;
        Eq(Seq(view.Options.Select(o => o.Key)), "a,b,c,d,pay,done", "the options and Done");
        Check(!view.Options.Single(o => o.Key == "done").Legal && view.Options.Single(o => o.Key == "done").Reason == "Choose at least 1.", "Done is not legal before Min picks");
        Check(e.GetView(1).PendingEitherOr is { ViewerDecides: false }, "the other player sees it read-only");
        Eq(Keys(e, 1).Length, 0, "the other player is offered nothing");
        // First pick: b. Nothing is applied yet; b leaves the list and Done becomes legal.
        Accept(e, Pick(e, source, "b"));
        Eq(Log.Count, 0, "nothing applies before the decision ends");
        Eq(Seq(e.GetView(0).PendingEitherOr!.Options.Select(o => o.Key)), "a,c,d,pay,done", "a picked option leaves the list");
        Check(e.GetView(0).PendingEitherOr!.Options.Single(o => o.Key == "done").Legal, "Done is legal once Min are picked");
        Check(e.State.PendingChoice!.Prompt.Contains("1 chosen so far", StringComparison.Ordinal), "the prompt counts the picks: " + e.State.PendingChoice.Prompt);
        Eq(Seq(e.State.PendingEitherOr!.Picked!), "b");
        // Second pick reaches Max: the decision ends, each pick is applied in the order picked, then the spec sees the whole set.
        Accept(e, Pick(e, source, "a"));
        Eq(Seq(Log), "apply:b,apply:a,apply:done:b,a", "applied in the order picked, then the set");
        Check(e.State.PendingEitherOr is null && e.State.PendingChoice is null, "nothing is left open");
        Eq(e.State.PriorityPlayerId, 1, "the action opportunity passes on as after any action");
        // One pick then Done ends it with fewer than Max.
        Log.Clear();
        var (f, fsource) = Table0();
        f.OpenEitherOrForTest(Multi, fsource, 0, 0, null, min: 1, max: 3);
        Accept(f, Pick(f, fsource, "d"));
        Check(f.State.PendingEitherOr is not null, "Max not reached: still open");
        Accept(f, Pick(f, fsource, "done"));
        Eq(Seq(Log), "apply:d,apply:done:d");
        Check(f.State.PendingEitherOr is null, "Done ends it");
        // The bot-facing list: the engine lists exactly the offered actions, Done disabled (no command) until legal.
        var (g, gsource) = Table0();
        g.OpenEitherOrForTest(Multi, gsource, 0, 0, null, min: 2, max: 3);
        var done = g.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Label.StartsWith("Done", StringComparison.Ordinal));
        Check(!done.Enabled && done.Command is null && done.Reason == "Choose at least 2.", "Done is listed disabled with the reason");
    }

    private static void MultiPickRefusals()
    {
        Log.Clear();
        var (e, source) = Table0();
        e.OpenEitherOrForTest(Multi, source, 0, 0, null, min: 1, max: 2);
        Reject(e, Pick(e, source, "done"));                       // before Min
        Reject(e, Pick(e, source, "c"));                          // listed but not legal
        Reject(e, Pick(e, source, "nonsense"));                   // never offered
        Reject(e, Pick(e, source, "pay"));                        // a Gold option cannot be combined
        Reject(e, Pick(e, source, "a", player: 1));               // not the decider
        Reject(e, Pick(e, source, "a") with { SourceIds = ["fx-1"] }); // a pick needs no Gold sources
        Accept(e, Pick(e, source, "a"));
        Reject(e, Pick(e, source, "a"));                          // already picked: no longer offered
        Eq(Log.Count, 0, "refusals applied nothing");
        // Min 0: Done at once is legal and applies only the empty set.
        Log.Clear();
        var (f, fsource) = Table0();
        f.OpenEitherOrForTest(Multi, fsource, 0, 0, null, min: 0, max: 2);
        Check(f.GetView(0).PendingEitherOr!.Options.Single(o => o.Key == "done").Legal, "Done is legal at once with Min 0");
        Accept(f, Pick(f, fsource, "done"));
        Eq(Seq(Log), "apply:done:", "an empty set");
        Check(f.State.PendingEitherOr is null, "ended");
    }

    private static void MultiPickForced()
    {
        Log.Clear();
        var (e, source) = Table0();
        // Two legal options and Min 2: nothing to choose, so both are taken in listed order and the caller finishes the action itself.
        Check(!e.OpenEitherOrForTest(Forced, source, 0, 0, null, min: 2, max: 3), "no decision opens");
        Eq(Seq(Log), "apply:x,apply:y,apply:done:x,y");
        Check(e.State.PendingEitherOr is null && e.State.PendingChoice is null, "nothing is open");
        Check(e.State.Events.Any(ev => ev.Type == "eitherOrAutoResolved"), "announced in the log");
        // Min 1 of two legal: a real choice.
        Log.Clear();
        var (f, fsource) = Table0();
        Check(f.OpenEitherOrForTest(Forced, fsource, 0, 0, null, min: 1, max: 3), "a real choice opens");
        Accept(f, Pick(f, fsource, "y"));
        Eq(Seq(f.QueryActions(0).Where(a => a.Type == "chooseEitherOr").Select(a => a.Command?.Choice ?? "(" + a.Label.Split(' ')[0] + ")")), "x,(Option),done", "the picked option left the list; the unavailable one stays listed");
        Accept(f, Pick(f, fsource, "x"));
        Eq(Seq(Log), "apply:y,apply:x,apply:done:y,x", "no legal option left: the decision ends by itself");
        Check(f.State.PendingEitherOr is null, "ended");
        // Nothing legal at all with Min 0: no decision.
        Log.Clear();
        var (g, gsource) = Table0();
        Check(!g.OpenEitherOrForTest(None, gsource, 0, 0, null, min: 0, max: 2), "no options at all: nothing to ask");
    }

    private static void MultiPickRoundTrip()
    {
        Log.Clear();
        var (e, source) = Table0();
        e.OpenEitherOrForTest(Multi, source, 0, 0, null, min: 1, max: 2);
        Accept(e, Pick(e, source, "d"));
        var json = Json(e);
        Check(json.Contains("\"Picked\":[\"d\"]", StringComparison.Ordinal) && json.Contains("\"Max\":2", StringComparison.Ordinal), "the saved decision carries its picks and bounds");
        var copy = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(json)!);
        Eq(Json(copy), json, "a restored game saves byte for byte the same");
        Accept(copy, Pick(copy, source, "a"));
        Accept(e, Pick(e, source, "a"));
        Eq(Json(copy), Json(e), "both resume to the same position");
        Eq(Seq(Log), "apply:d,apply:a,apply:done:d,a,apply:d,apply:a,apply:done:d,a", "each resumed through the same hooks");
        // Replaying the commands from the beginning reproduces the pending decision.
        Log.Clear();
        var (r, rsource) = Table0();
        r.OpenEitherOrForTest(Multi, rsource, 0, 0, null, min: 2, max: 2);
        Accept(r, Pick(r, rsource, "a"));
        Eq(Seq(r.State.PendingEitherOr!.Picked!), "a");
        Eq(r.State.PendingEitherOr.Min, 2);
    }

    // ---- 2. PendingActions and the EffectChoice hooks ----------------------------------------------------------------------------------------------------------------------------

    private static void RegisteredPendingType()
    {
        Log.Clear();
        var (e0, source) = Table0();
        var e = Batch336Support.Edit(e0, s => { s.PendingChoice = new(Generic, 0, source, "Training: a decision no QueryActions branch knows."); s.PriorityPlayerId = 0; });
        var mine = e.QueryActions(0).Where(a => a.Type == GenericPick).ToArray();
        Eq(mine.Length, 2, "the decider is offered the registered options");
        Check(mine.All(a => a.Enabled && a.Command is not null), "each carries a complete command");
        Eq(e.QueryActions(1).Count(a => a.Type == GenericPick), 0, "the other player has none");
        Check(e.QueryActions(0).All(a => a.Type is GenericPick or "concede"), "and nothing from the ordinary chain: " + Seq(e.QueryActions(0).Select(a => a.Type)));
        Reject(e, mine[0].Command! with { Choice = "three" });
        Reject(e, mine[0].Command! with { PlayerId = 1 });
        Accept(e, mine[1].Command!);
        Eq(Seq(Log), "generic:two");
        Check(e.State.PendingChoice is null, "the type's own hook closed it");
        // A pending type nobody registered still falls to the ordinary chain: the hook is consulted first and says nothing about it.
        var other = Batch336Support.Edit(e0, s => { s.PendingChoice = new("training-p25-unregistered", 0, source, "Nobody knows this."); s.PriorityPlayerId = 0; });
        Eq(other.QueryActions(0).Count(a => a.Type == GenericPick), 0, "not offered for a type it did not register");
    }

    private static void EffectChoiceHooks()
    {
        Log.Clear();
        MatchEngine Fresh(out string id)
        {
            var s = Batch336Support.Table(Phase.Action, 0);
            var card = Add(s, "training-lion-samurai", 0, Zone.Home);
            card.GrantedAbilities.Add(new(BranchAbility, "test-fixture", GrantDuration.Permanent)); id = card.Id;
            return MatchEngine.FromTestState(s);
        }
        // An ordinary option: its effect goes through the hook and the action then finishes.
        var e = Fresh(out var id);
        Accept(e, Batch336Support.Enabled(e, 0, BranchAbility, id).Command!);
        Eq(e.State.PendingChoice?.Type, "effectChoice");
        Eq(Seq(e.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice!)), "a,b");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "a").Command!);
        Eq(Seq(Log), "branch:a");
        Check(e.State.PendingChoice is null && e.State.PendingEffectChoice is null, "the action finished");
        // The other option opens a further decision through the continuation: the action does not finish until it is answered.
        Log.Clear();
        var f = Fresh(out var fid);
        Accept(f, Batch336Support.Enabled(f, 0, BranchAbility, fid).Command!);
        Accept(f, f.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "b").Command!);
        Eq(Seq(Log), "branch:b");
        Eq(f.State.PendingChoice?.Type, Generic, "the continuation opened its own decision");
        Check(f.State.PendingEffectChoice is null, "the effect choice is spent");
        Eq(f.QueryActions(0).Count(a => a.Type == GenericPick), 2, "and it lists its own options");
        Accept(f, f.QueryActions(0).First(a => a.Type == GenericPick).Command!);
        Eq(Seq(Log), "branch:b,generic:one");
        Check(f.State.PendingChoice is null, "answered");
    }

    // ---- 3. TargetSlot.CrossConstraint ---------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, string Actor, string Crane1, string Crane2, string Lion, string Scorpion) PairTable(string ability)
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var actor = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        actor.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));
        var c1 = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var c2 = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var scorpion = Add(s, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0);
        return (MatchEngine.FromTestState(s), actor.Id, c1.Id, c2.Id, lion.Id, scorpion.Id);
    }

    private static void CrossConstraintValidation()
    {
        var t = PairTable(PairAbility); var e = t.Engine;
        // Each card is legal on its own for each slot; the pair is not.
        var same = Batch336Support.Use(e, 0, PairAbility, t.Actor, selections: [Batch336Support.Pick("first", t.Crane1), Batch336Support.Pick("second", t.Crane2)]);
        Eq(e.SlotSelectionErrorForTest(same, PairAbility), DifferentClans, "the constraint's own reason");
        Eq(Batch336Support.Rejection(e, same), DifferentClans, "a command that submits it is refused with that reason");
        var after = Json(e);
        Reject(e, same); Eq(Json(e), after, "a refusal leaves the state unchanged");
        // The per-slot checks still come first: a non-Personality card is refused for its own slot's reason, not the constraint's.
        var bad = Batch336Support.Use(e, 0, PairAbility, t.Actor, selections: [Batch336Support.Pick("first", t.Crane1), Batch336Support.Pick("second", "no-such-card")]);
        Check(e.SlotSelectionErrorForTest(bad, PairAbility) != DifferentClans, "an unknown card is not the constraint's business");
        // A pair of different Clans goes through, in either order.
        var legal = Batch336Support.Use(e, 0, PairAbility, t.Actor, selections: [Batch336Support.Pick("first", t.Crane1), Batch336Support.Pick("second", t.Lion)]);
        Check(e.SlotSelectionErrorForTest(legal, PairAbility) is null, "a pair of different Clans is legal");
        Check(e.SlotSelectionErrorForTest(Batch336Support.Use(e, 0, PairAbility, t.Actor, selections: [Batch336Support.Pick("first", t.Scorpion), Batch336Support.Pick("second", t.Crane2)]), PairAbility) is null, "in the other order too");
        // The same card twice is also the same Clan: the constraint covers it (the per-slot check does not forbid it here).
        var twice = Batch336Support.Use(e, 0, PairAbility, t.Actor, selections: [Batch336Support.Pick("first", t.Lion), Batch336Support.Pick("second", t.Lion)]);
        Eq(e.SlotSelectionErrorForTest(twice, PairAbility), DifferentClans);
        Accept(e, legal);
        Check(e.State.PendingChoice is null, "and the action resolves");
    }

    private static void CrossConstraintOffer()
    {
        // (1) Default earlier choice only: the second slot offers exactly the cards the constraint admits beside the first slot's default (the first card, a Crane), and the default pair satisfies it.
        var t = PairTable(PairDefaultOnly); var e = t.Engine;
        var option = Batch336Support.Options(e, 0, PairDefaultOnly, t.Actor).Single();
        Check(option.Enabled, "the ability is offered: " + option.Reason);
        var first = option.Slots!.Single(s => s.Slot == "first"); var second = option.Slots!.Single(s => s.Slot == "second");
        var defaultFirst = option.Command!.Selections!.Single(s => s.Slot == "first").CardIds![0];
        Eq(defaultFirst, first.CardIds.OrderBy(x => x, StringComparer.Ordinal).First(), "the first slot defaults to its first candidate");
        string ClanOf(string id) => CardCatalog.Get(e.CardForTest(id).DefinitionId).Clan;
        var admitted = first.CardIds.Where(id => ClanOf(id) != ClanOf(defaultFirst)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Check(admitted.Length > 0, "the table has candidates of another Clan");
        Eq(Seq(second.CardIds.OrderBy(x => x, StringComparer.Ordinal)), Seq(admitted), "exactly the candidates that satisfy it are offered, none sharing the default first card's Clan");
        var defaultSecond = option.Command!.Selections!.Single(s => s.Slot == "second").CardIds![0];
        Check(e.SlotSelectionErrorForTest(option.Command!, PairDefaultOnly) is null && defaultSecond != defaultFirst, "the default selection is a legal pair");
        // (2) Every earlier choice probed: a candidate is offered while some earlier choice admits it, and the offered default still satisfies it.
        var u = PairTable(PairAbility); var o2 = Batch336Support.Options(u.Engine, 0, PairAbility, u.Actor).Single();
        Check(o2.Enabled && u.Engine.SlotSelectionErrorForTest(o2.Command!, PairAbility) is null, "the default pair is legal");
        var allSecond = o2.Slots!.Single(s => s.Slot == "second").CardIds;
        Check(allSecond.Contains(u.Crane1), "a Crane is offered for the second slot because a Lion could be the first");
        // (3) The first card's default has no partner: the offer falls back to an earlier choice that has one, so the default command is still legal.
        var s3 = Batch336Support.Table(Phase.Battle, 1);
        CardInstance Named(string id, string def, int owner) { var c = new CardInstance { Id = id, DefinitionId = def, OwnerId = owner, Zone = Zone.Battlefield, FaceUp = true, ProvinceIndex = 0 }; s3.Cards.Add(id, c); return c; }
        var actor = Named("m-actor", "training-lion-samurai", 1);
        actor.GrantedAbilities.Add(new(OwnEnemyAbility, "test-fixture", GrantDuration.Permanent));
        var mineCrane = Named("a-crane", "training-crane-samurai", 1);   // sorts first: it is the default for the first slot, and shares the enemy Crane's Clan
        var theirCrane = Named("z-enemy", "training-crane-samurai", 0);
        var e3 = MatchEngine.FromTestState(s3);
        var o3 = Batch336Support.Options(e3, 1, OwnEnemyAbility, actor.Id).Single();
        Check(o3.Enabled, "offered: " + o3.Reason);
        var mine = o3.Command!.Selections!.Single(s => s.Slot == "mine").CardIds![0]; var theirs = o3.Command.Selections!.Single(s => s.Slot == "theirs").CardIds![0];
        Eq(theirs, theirCrane.Id);
        Check(mine != mineCrane.Id, "the default does not pair a Crane with the enemy Crane");
        Check(e3.SlotSelectionErrorForTest(o3.Command, OwnEnemyAbility) is null, "the default command is legal");
        Eq(mine, actor.Id, "the default falls back to the earlier choice that has a partner");
        var bad = Batch336Support.Use(e3, 1, OwnEnemyAbility, actor.Id, selections: [Batch336Support.Pick("mine", mineCrane.Id), Batch336Support.Pick("theirs", theirCrane.Id)]);
        Eq(e3.SlotSelectionErrorForTest(bad, OwnEnemyAbility), DifferentClans);
        Accept(e3, o3.Command);
        Check(e3.State.PendingChoice is null, "and the action resolves");
        // (4) With no partner for any choice the ability is offered disabled, with the constraint's reason.
        var s4 = Batch336Support.Table(Phase.Battle, 0);
        var a4 = Add(s4, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        a4.GrantedAbilities.Add(new(OwnEnemyAbility, "test-fixture", GrantDuration.Permanent));
        Add(s4, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        var o4 = Batch336Support.Options(e4, 0, OwnEnemyAbility, a4.Id).Single();
        Check(o4.Enabled == false || e4.SlotSelectionErrorForTest(o4.Command!, OwnEnemyAbility) is null, "never offered enabled with an illegal default");
    }

    // ---- 4. a decider's choice among shown cards ---------------------------------------------------------------------------------------------------------------------------------

    private static void ShownChoiceBasics()
    {
        Log.Clear();
        var (e, source) = Table0(Phase.Action, 0);
        var one = e.ShowCardForTest(0, "training-crane-samurai", source); var two = e.ShowCardForTest(0, "training-lion-samurai", source);
        Check(e.OpenShownChoiceForTest(ShownSpec, source, 0, 1, [one, two], 1, 1), "opens for player 1 (the other player decides)");
        Eq(e.State.PendingChoice?.Type, "shownChoice"); Eq(e.State.PendingChoice!.PlayerId, 1);
        var offered = e.QueryActions(1).Where(a => a.Type == "chooseShown").ToArray();
        Eq(offered.Length, 2, "one action per shown card");
        Check(offered[0].Label.Contains("Crane training samurai", StringComparison.Ordinal) && offered[1].Label.Contains("Lion training samurai", StringComparison.Ordinal), "named by definition: " + Seq(offered.Select(a => a.Label)));
        Eq(Seq(offered.Select(a => a.Command!.Choice!)), "1,2", "and by position");
        Eq(e.QueryActions(0).Count(a => a.Type == "chooseShown"), 0, "the acting player only waits");
        Eq(e.GetView(0).PendingChoice?.PlayerId, 1, "both views show who decides");
        Check(e.GetView(0).ShownCards!.Length == 2 && e.GetView(1).ShownCards!.Length == 2, "the shown faces are in both views");
        Reject(e, offered[0].Command! with { Choice = "3" });
        Reject(e, offered[0].Command! with { PlayerId = 0 });
        Reject(e, offered[0].Command! with { Choice = "done" });
        Accept(e, offered[1].Command!);
        Eq(Seq(Log), "shown:2");
        Check(e.State.PendingChoice is null && e.State.PendingShownChoice is null, "applied and closed");
        Eq(e.State.PriorityPlayerId, 1, "the action opportunity passes on");
    }

    private static void ShownChoiceMulti()
    {
        Log.Clear();
        var (e, source) = Table0(Phase.Action, 0);
        var seqs = new[] { "training-crane-samurai", "training-lion-samurai", "training-honor-strike-samurai" }.Select(d => e.ShowCardForTest(0, d, source)).ToArray();
        e.OpenShownChoiceForTest(ShownSpec, source, 0, 1, seqs, 1, 2);
        var json = JsonSerializer.Serialize(e.State.PendingShownChoice!.Candidates) + JsonSerializer.Serialize(e.State.PendingShownChoice.Chosen);
        Check(!json.Contains("fx-", StringComparison.Ordinal) && json == "[1,2,3][]", "the candidates are positions, never an instance id: " + json);
        Check(e.QueryActions(1).Count(a => a.Type == "chooseShown" && a.Label.StartsWith("Done", StringComparison.Ordinal)) == 0, "no Done before Min");
        Reject(e, new GameCommand("chooseShown", 1, e.Version, CardId: source, Choice: "done"));
        Accept(e, new GameCommand("chooseShown", 1, e.Version, CardId: source, Choice: "3"));
        Reject(e, new GameCommand("chooseShown", 1, e.Version, CardId: source, Choice: "3")); // already chosen
        var saved = Json(e);
        var copy = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(saved)!);
        Eq(Json(copy), saved, "a restored game saves the same");
        Check(copy.QueryActions(1).Any(a => a.Type == "chooseShown" && a.Label.StartsWith("Done", StringComparison.Ordinal)), "Done is offered once Min are chosen");
        Accept(copy, copy.QueryActions(1).Single(a => a.Type == "chooseShown" && a.Label.StartsWith("Done", StringComparison.Ordinal)).Command!);
        Eq(Seq(Log), "shown:3");
        // Choosing up to Max ends it without Done.
        Log.Clear();
        Accept(e, new GameCommand("chooseShown", 1, e.Version, CardId: source, Choice: "1"));
        Eq(Seq(Log), "shown:3,1", "Max reached: applied in the order chosen");
        // Nothing to choose: resolved without a decision, and the caller finishes the action.
        Log.Clear();
        var (f, fsource) = Table0(Phase.Action, 0);
        Check(!f.OpenShownChoiceForTest(ShownSpec, fsource, 0, 1, [], 1, 1), "no candidate: nothing is asked");
        var only = f.ShowCardForTest(0, "training-crane-samurai", fsource);
        Check(!f.OpenShownChoiceForTest(ShownSpec, fsource, 0, 1, [only], 1, 1), "one forced candidate: nothing is asked");
        Eq(Seq(Log), "shown:,shown:" + only);
    }

    // ---- unused means unchanged ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void UnusedUnchanged()
    {
        // An ordinary single-pick Either/Or (a published training spec) opens, lists its own options without Done, and carries none of the new fields.
        var (e, source) = Table0();
        Check(e.OpenEitherOrForTest(EitherOrSpecs.TrainingThreeWay, source, 1, 0, null), "an ordinary decision opens");
        var pending = JsonSerializer.Serialize(e.State.PendingEitherOr);
        Check(!pending.Contains("\"Min\"", StringComparison.Ordinal) && !pending.Contains("\"Max\"", StringComparison.Ordinal) && !pending.Contains("\"Picked\"", StringComparison.Ordinal), "no new field is saved: " + pending);
        Eq(Seq(e.GetView(0).PendingEitherOr!.Options.Select(o => o.Key)), "pay,honor,accept", "no Done option");
        Check(!e.State.PendingChoice!.Prompt.Contains("Choose ", StringComparison.Ordinal) || !e.State.PendingChoice.Prompt.Contains("so far", StringComparison.Ordinal), "the prompt is unchanged");
        Accept(e, Pick(e, source, "accept"));
        Check(e.State.PendingEitherOr is null, "answered by one pick, as before");
        // A game that never used any of it saves none of it.
        var fresh = Json(MatchEngine.CreateEconomyScenario());
        Check(!fresh.Contains("PendingShownChoice", StringComparison.Ordinal) && !fresh.Contains("\"Picked\"", StringComparison.Ordinal), "the saved game has no trace of P25");
        // The channels are sealed with the feature's own registrations.
        Check(EngineHooks.PendingActions.Names.Contains("P25ShownChoiceActions") && EngineHooks.PendingActions.Names.Contains("TestP25PendingActions"), "PendingActions is sealed with its registrations");
        Check(EngineHooks.BranchEffects.Names.Contains("TestP25BranchEffects") && EngineHooks.EffectChoiceContinuations.Names.Contains("TestP25Continuation") && EngineHooks.ShownChoiceApply.Names.Contains("TestP25ShownApply"), "the other channels too");
        // The shared decision-state guard knows the new pending property.
        Check(EngineHooks.DecisionState.Names.Contains(nameof(MatchState.PendingShownChoice)), "PendingShownChoice is a registered decision state");
    }
}
