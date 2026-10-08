using L5R.Core;
using static RegressionAssertions;

// Issue #586: Border Keep's first-turn refresh (and Doubts of Refill) must offer, accept and resolve the face-down cards of the player's provinces.
// A card an Event, Region or Celestial left behind is refilled face down on the first turn (Events Phase); every view names it "hidden-p{owner}-province{n}",
// so the engine offers it by that place and resolves it back to the real card server-side, never putting the real id on the wire.
internal static class BorderKeepFaceDownTests
{
    private const string BorderKeep = "oracle-l5r-10240", RebuildingKyudenHida = "oracle-l5r-6200"; // an Event that resolves at once and refills its province

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);

    public static void Run(TestRunner runner) => runner.Run("border-keep-face-down", [
        ("After an Event resolves in the first Events Phase, Border Keep is offered in the Action phase and can refresh the face-down card the province was refilled with", EventThenRefresh),
        ("Face-down province cards are offered by place, never by real id; face-up cards keep their id; a mixed selection refreshes both", OfferedByPlace),
        ("A hidden id of the opponent's province, a missing province or a slot suffix is refused and changes nothing", RefusedIds),
    ]);

    // The first turn: every province card starts face down, province 0 holds an Event; the Events Phase reveals them, resolves the Event and refills province 0 face down.
    private static (MatchEngine Engine, string KeepId, string Refilled) AfterFirstEventsPhase()
    {
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var keep = Batch336Support.Add(s, BorderKeep, 0, Zone.Home);
        var eventCard = Batch336Support.Add(s, RebuildingKyudenHida, 0, Zone.Province, province: 0);
        s.Players[0].Provinces[0].CardId = eventCard.Id;
        foreach (var p in s.Players[0].Provinces) if (p.CardId is string id) s.Cards[id].FaceUp = false;
        var e = MatchEngine.FromTestState(s);
        e.EventsPhaseForTest(0);
        var live = e.State;
        Check(live.Phase == Phase.Action, "the Events Phase ended in the Action phase: " + live.Phase);
        var refilled = live.Players[0].Provinces[0].CardId;
        Check(refilled is not null && refilled != eventCard.Id && !live.Cards[refilled].FaceUp, "the Event's province was refilled face down");
        live.Players[0].TurnsTaken = 1;
        return (MatchEngine.FromTestState(live), keep.Id, refilled!);
    }

    private static void EventThenRefresh()
    {
        var (e, keep, refilled) = AfterFirstEventsPhase();
        var option = Batch336Support.Options(e, 0, "border-first", keep).Single();
        Check(option.Enabled, "Border Keep is offered in the Action phase after an Event resolved: " + option.Reason);
        var hidden = "hidden-p0-province0";
        Check(option.TargetIds!.Contains(hidden) && !option.TargetIds!.Contains(refilled), "the face-down refill is offered by its place, not its real id");
        Accept(e, option.Command! with { CardIds = [hidden] });
        var after = e.State.Players[0];
        Check(after.Provinces[0].CardId is string next && next != refilled && e.State.Cards[next].FaceUp, "the province was refilled face up");
        Check(e.State.Cards[refilled].Zone == Zone.DynastyDeck && after.DynastyDeck.Contains(refilled), "the face-down card went to the bottom of the deck");
    }

    private static void OfferedByPlace()
    {
        var (e, keep, _) = AfterFirstEventsPhase();
        var player = e.State.Players[0];
        var faceUp = player.Provinces.Where(p => p.CardId is not null && e.State.Cards[p.CardId].FaceUp).Select(p => p.CardId!).ToArray();
        var offered = Batch336Support.Options(e, 0, "border-first", keep).Single().TargetIds!;
        Check(faceUp.Length > 0 && faceUp.All(offered.Contains), "face-up cards keep their ids");
        Check(offered.Count(id => id.StartsWith("hidden-p0-province", StringComparison.Ordinal)) == player.Provinces.Count(p => p.CardId is not null && !e.State.Cards[p.CardId].FaceUp), "each face-down card appears once, by place");
        Accept(e, Batch336Support.Options(e, 0, "border-first", keep).Single().Command! with { CardIds = ["hidden-p0-province0", faceUp[0]] });
        Check(e.State.Players[0].Provinces[0].CardId is string a && e.State.Cards[a].FaceUp, "both refreshed face up");
    }

    private static void RefusedIds()
    {
        var (e, keep, _) = AfterFirstEventsPhase();
        var command = Batch336Support.Options(e, 0, "border-first", keep).Single().Command!;
        var before = System.Text.Json.JsonSerializer.Serialize(e.State);
        foreach (var bad in new[] { "hidden-p1-province0", "hidden-p0-province9", "hidden-p0-province0-slot1" })
            Batch336Support.Rejection(e, command with { CardIds = [bad] });
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == before, "a refused command changed nothing");
    }
}
