using L5R.Core;
using System.Text.Json;
using static RegressionAssertions;

internal static class DuelChoiceEvidenceTests
{
    public static void Run(TestRunner runner) => runner.Run("duel-choice-evidence", [
        ("Every supported choice records its public outcome, source and replay",AllKinds),
        ("Optional decline is evidence; required choice rejects decline without a beat",Decline),
        ("Consecutive choices preserve execution order without duplicates",Sequence),
        ("Draw evidence exposes no private card identity in either seat",DrawPrivacy)
    ]);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Send(MatchEngine e, GameCommand c) { var r = e.Execute(c); Check(r.Accepted, r.Error ?? "Command rejected"); }
    private static MatchEngine Prepare(string scenario)
    {
        var e = MatchEngine.CreateDuelDemo(scenario);
        Send(e, e.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "steel-on-steel-duel").Command!);
        for (var n = 0; n < 20 && e.State.PendingChoice?.Type != "focusEffectChoice"; n++)
        {
            var seat = e.State.PriorityPlayerId; var view = e.GetView(seat); var actions = e.QueryActions(seat).Where(a => a.Enabled && a.Command is not null).ToArray(); GameCommand? command = null;
            if (view.PendingDuel?.Stage == "Focusing")
            {
                if (seat == 0) command = actions.FirstOrDefault(a => a.Type == "focus" && view.Players[0].Hand.Any(c => c.Id == a.Command!.CardId && (c.Definition!.Abilities ?? []).Any(id => AbilityCatalog.All.TryGetValue(id, out var def) && def.Timing == "FocusEffect")))?.Command;
                else if (view.Players[1].FocusCount < (scenario == "choice-multiple" ? 2 : 1)) command = actions.FirstOrDefault(a => a.Type == "focus" && a.Command!.Choice == "deck")?.Command;
                command ??= actions.First(a => a.Type == "strike").Command;
            }
            else command = actions.FirstOrDefault(a => a.Type is "orderDuelFocusEffects" or "acceptDuel" or "declineReaction")?.Command;
            Check(command is not null, "No preparation command"); Send(e, command!);
        }
        Check(e.State.PendingChoice?.Type == "focusEffectChoice", "Did not pause for choice");
        Check(e.GetView(0).LastDuel!.Result is null, "No final verdict before decision"); return e;
    }
    private static void AllKinds()
    {
        foreach (var scenario in new[] { "choice-discard", "choice-attach", "choice-straighten", "choice-destroy", "choice-effect" })
        {
            var e = Prepare(scenario); var source = e.State.PendingChoice!.CardId; var before = e.GetView(0).LastDuel!.Beats!.Length;
            var option = e.QueryActions(0).First(a => a.Type == "chooseFocusEffect" && a.Command!.Choice != "decline");
            Check(!e.QueryActions(1).Any(a => a.Type == "chooseFocusEffect"), "No choices for other seat");
            Send(e, option.Command!); var record = e.GetView(0).LastDuel!;
            var beat = record.Beats!.Single(b => b.Source?.Id == source && b.Kind == "FocusEffect");
            Check(beat.Index == before && beat.Lines.Length > 0 && !beat.Lines.Contains("No change from this effect."), scenario + " has real outcome");
            Check(record.Result is not null, "Verdict resumes");
            var version = e.Version; Check(!e.Execute(option.Command!).Accepted && e.Version == version, "Stale duplicate rejected");
            Check(JsonSerializer.Serialize(e.ExportReplay()) == JsonSerializer.Serialize(MatchEngine.Replay(e.ExportReplay()).ExportReplay()), "Replay command identity");
            Check(JsonSerializer.Serialize(record.Beats) == JsonSerializer.Serialize(MatchEngine.Replay(e.ExportReplay()).GetView(0).LastDuel!.Beats), "Replay retains exact evidence");
        }
    }
    private static void Decline()
    {
        var e = Prepare("choice-discard"); var source = e.State.PendingChoice!.CardId;
        Send(e, e.QueryActions(0).Single(a => a.Command?.Type == "chooseFocusEffect" && a.Command.Choice == "decline").Command!);
        Check(e.GetView(0).LastDuel!.Beats!.Single(b => b.Source?.Id == source).Lines.Any(l => l.Contains("took nothing")), "Decline has explanation");
        e = Prepare("choice-straighten"); var before = JsonSerializer.Serialize(e.GetView(0).LastDuel);
        Check(!e.QueryActions(0).Any(a => a.Command?.Type == "chooseFocusEffect" && a.Command.Choice == "decline"), "Required choice omits decline");
        Check(!e.Execute(new("chooseFocusEffect", 0, e.Version, Choice: "decline")).Accepted, "Required decline rejected");
        Check(JsonSerializer.Serialize(e.GetView(0).LastDuel) == before, "Rejected decline adds no evidence");
    }
    private static void Sequence()
    {
        var e = Prepare("choice-multiple"); var sources = new List<string>();
        while (e.State.PendingChoice?.Type == "focusEffectChoice")
        {
            sources.Add(e.State.PendingChoice.CardId!); Send(e, e.QueryActions(0).First(a => a.Type == "chooseFocusEffect" && a.Command!.Choice != "decline").Command!);
        }
        Check(sources.Count == 2, "Two actual pauses"); var beats = e.GetView(0).LastDuel!.Beats!;
        Check(beats.Where(b => b.Kind == "FocusEffect").Select(b => b.Source!.Id).SequenceEqual(sources), "Each choice occurs exactly once in its actual order");
        Check(beats.Select(b => b.Index).SequenceEqual(Enumerable.Range(0, beats.Length)), "Stable contiguous indices");
    }
    private static void DrawPrivacy()
    {
        var e = Prepare("choice-effect"); var hidden = e.State.Players[0].FateDeck.ToArray();
        Send(e, e.QueryActions(0).Single(a => a.Type == "chooseFocusEffect" && a.Command!.TargetId == "draw-card").Command!);
        foreach (var seat in new[] { 0, 1 })
        {
            var json = JsonSerializer.Serialize(e.GetView(seat).LastDuel!.Beats);
            foreach (var id in hidden) Check(!json.Contains(id), "Evidence leaks drawn/deck card ID");
            Check(json.Contains("draw a card"), "Public count/action retained");
        }
    }
}
