using L5R.Core;
using static RegressionAssertions;

internal static class EventDecisionTests
{
    public static void Run(TestRunner runner) => runner.Run("EventDecisionTests", [
        ("HarshQueries", HarshQueries),
        ("LastProvince", LastProvince),
        ("StrifeQueries", StrifeQueries),
        ("QueuedAndEmpty", QueuedAndEmpty),
    ]);

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static CardInstance Add(MatchState s, string definition, int player, Zone zone, int? province = null)
    {
        var id = "event-probe-" + s.Cards.Count;
        var card = new CardInstance { Id = id, DefinitionId = definition, OwnerId = player, Zone = zone, FaceUp = true, ProvinceIndex = province };
        s.Cards.Add(id, card); if (zone == Zone.Hand) s.Players[player].Hand.Add(id); return card;
    }
    static MatchEngine Harsh(bool last = false)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var eventCard = Add(s, "oracle-l5r-2978", 1, Zone.Province, 1); eventCard.FaceUp = false;
        s.Players[1].Provinces[1].CardId = eventCard.Id;
        if (last) foreach (var p in s.Players[1].Provinces.Where(p => p.Index != 1)) p.Destroyed = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("endDynasty", 0, e.Version));
        Check(e.State.PendingChoice?.Type == "harshChoices", "Event did not open naturally at reveal"); return e;
    }
    static MatchEngine Strife(bool empty = false, bool queue = false)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 1; s.ActivePlayerId = 0;
        Add(s, "oracle-l5r-8494", 0, Zone.EventInPlay);
        var ring = Add(s, "training-ring-of-earth-stub", 1, Zone.Home);
        if (empty) foreach (var id in s.Players[1].Hand.ToArray()) { s.Cards[id].Zone = Zone.FateDiscard; s.Players[1].Hand.Remove(id); }
        if (queue) { s.PendingForcedDiscards.Add(new(1, "Times of Strife")); s.PendingOverconfidentDraws.Add(1); }
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("useAbility", 1, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-lower", TargetPlayerId: 0, Province: 0));
        return e;
    }
    public static void HarshQueries()
    {
        var e = Harsh(); var before = e.State; var options = e.QueryActions(1).Where(a => a.Type != "concede").ToArray();
        Check(options.Length == 2 && options.All(a => a.Type == "harshChoices"), "Missing or unrelated Event choices");
        OfferedCommandsValidate(e, () => MatchEngine.FromTestState(before));
        Check(e.QueryActions(0).All(a => a.Type is "wait" or "concede"), "Opponent offered Event decisions");
        // Issue #581: the consequence is the engine's, public to both seats, so the client counts nothing.
        var provinces = before.Players[1].Provinces.Count(p => !p.Destroyed);
        foreach (var seat in new[] { 0, 1 })
            Check(e.GetView(seat).PendingHarshChoices is { EndsMatch: false, CardsDrawn: 3 } consequence && consequence.ProvincesBefore == provinces && consequence.ProvincesAfter == provinces - 1, $"Seat {seat} lacks the engine's Harsh Choices consequence");
        Reject(e, options[0].Command! with { PlayerId = 0 }); Reject(e, new("pass", 1, e.Version));
        foreach (var choice in options)
        {
            var fork = MatchEngine.FromTestState(before); Accept(fork, choice.Command!);
            Check(fork.State.Phase == Phase.Action && fork.State.PendingChoice is null, "Events failed to resume");
            Check(fork.State.Players[1].Provinces[1].Destroyed == (choice.Command!.Choice == "destroy"), "Wrong province outcome");
            Check(fork.State.Players[1].Hand.Count == before.Players[1].Hand.Count + (choice.Command.Choice == "destroy" ? 3 : 0), "Wrong draw count");
            Reject(fork, choice.Command);
        }
    }
    public static void LastProvince()
    {
        var e = Harsh(true); var hand = e.State.Players[1].Hand.Count;
        var action = e.QueryActions(1).Single(a => a.Command?.Choice == "destroy");
        Check(action.Label.Contains("Defeat"), "Final-province consequence absent");
        Check(e.GetView(0).PendingHarshChoices is { ProvincesBefore: 1, ProvincesAfter: 0, EndsMatch: true, CardsDrawn: 0 }, "The view does not say the last province ends the match");
        Accept(e, action.Command!);
        Check(e.GetView(0).PendingHarshChoices is null, "The consequence outlived the decision");
        Check(e.State.WinnerId == 0 && e.State.Phase == Phase.Finished && e.State.PendingChoice is null, "Victory overwritten by Event continuation");
        Check(e.State.Players[1].Hand.Count == hand, "Drew after match finished"); Check(e.QueryActions(1).Length == 0, "Finished match still offers commands");
    }
    public static void StrifeQueries()
    {
        var e = Strife(); var state = e.State; var actions = e.QueryActions(1).Where(a => a.Type != "concede").ToArray();
        Check(actions.Length == state.Players[1].Hand.Count && actions.All(a => a.Type == "forcedDiscard"), "Discard choices do not match hand");
        OfferedCommandsValidate(e, () => MatchEngine.FromTestState(state));
        HiddenIdsAbsent(e.GetView(0), state.Players[1].Hand);
        Check(e.QueryActions(0).All(a => a.Type is "wait" or "concede"), "Hand choices leaked to opponent");
        Reject(e, new("forcedDiscard", 1, e.Version, TargetId: state.Players[0].Hand[0]));
        Reject(e, new("pass", 1, e.Version));
        var action = actions[0]; Accept(e, action.Command!);
        Check(e.State.PendingChoice is null && e.State.PriorityPlayerId == 0, "Did not restore action opportunity");
        Check(e.GetView(0).Players[1].Discard.Any(c => c.Id == action.Command!.TargetId && c.FaceUp), "Completed discard not public");
        Reject(e, action.Command!);
    }
    public static void QueuedAndEmpty()
    {
        Check(Strife(true).State.PendingChoice is null, "Empty hand prompted for a discard");
        var e = Strife(queue: true);
        for (int i = 0; i < 2; i++) Accept(e, e.QueryActions(1).First(a => a.Type == "forcedDiscard").Command!);
        Check(e.State.PendingChoice?.Type == "overconfidentDraw", "Deferred draw lost behind discards");
        var actions = e.QueryActions(1).Where(a => a.Type == "overconfidentDraw").ToArray(); Check(actions.Length == 2, "Optional draw lacks both choices");
        var state = e.State; OfferedCommandsValidate(e, () => MatchEngine.FromTestState(state));
        Accept(e, actions.Single(a => a.Command!.Choice == "decline").Command!);
        Check(e.State.PendingChoice is null && e.State.PriorityPlayerId == 0, "Deferred queue failed to resume");
    }
}
