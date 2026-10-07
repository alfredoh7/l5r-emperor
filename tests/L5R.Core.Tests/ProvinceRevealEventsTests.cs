using L5R.Core;
using static RegressionAssertions;

// Primitive P5 (issue #711, ProvinceRevealEvents.cs): Events that stay face up in their province. The flagged effect ids are registered by each test and removed afterwards, so no other suite sees them;
// the cards that use the primitive are wired in a later wave.
internal static class ProvinceRevealEventsTests
{
    private static readonly List<string> Log = [];
    private static string Mode = "";

    public static void Run(TestRunner runner) => runner.Run("province-reveal-events", [
        ("A flagged face-up Event resolves once in its owner's Events Phase and stays in its province; an ordinary Event is discarded as before", ResolvesAndStays),
        ("A flagged Event revealed this phase fires in the same phase, and a face-down one does not fire before it is revealed", RevealedThenFires),
        ("Once per turn: it fires again on its owner's next Events Phase while it is still there, never on the other player's", OncePerTurn),
        ("A decision pauses the Events Phase, survives a save, and the answer is applied and the phase resumes", DecisionPauses),
        ("An Event that left its province (the province was destroyed) no longer fires, and the destroyed province discards it as an ordinary card", Destroyed),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);

    private static IEnumerable<CardDefinition> Events() => CardCatalog.Cards.Values.Where(c => c.Type == CardType.Event && !c.ScenarioOnly && !string.IsNullOrEmpty(c.Effect) && c.Effect != "none");

    private static (CardInstance Card, MatchEngine Engine) Table(string effect, int province, bool faceUp, Phase phase = Phase.Straighten, int actor = 0)
    {
        var s = Batch336Support.Table(phase, actor);
        var card = Batch336Support.Add(s, Events().First(c => c.Effect == effect).Id, 0, Zone.Province);
        s.Players[0].Provinces[province].CardId = card.Id; card.ProvinceIndex = province; card.FaceUp = faceUp;
        return (card, MatchEngine.FromTestState(s));
    }

    // An Event whose ordinary reveal ends in the discard pile with no decision (found, not hard-coded, so a catalogue change does not break the suite).
    private static CardDefinition Plain()
    {
        foreach (var candidate in Events())
        {
            var s = Batch336Support.Table(Phase.Straighten, 0);
            var card = Batch336Support.Add(s, candidate.Id, 0, Zone.Province);
            s.Players[0].Provinces[0].CardId = card.Id; card.ProvinceIndex = 0; card.FaceUp = false;
            try
            {
                var e = MatchEngine.FromTestState(s);
                e.EventsPhaseForTest(0);
                if (e.State.PendingChoice is null && e.State.Cards[card.Id].Zone == Zone.DynastyDiscard) return candidate;
            }
            catch (Exception) { }
        }
        throw new Exception("No plain Event in the catalogue");
    }

    private static string Flag(string effect, string mode = "")
    {
        Mode = mode; Log.Clear();
        ProvinceRevealEvents.Register(effect, static (engine, revealed) =>
        {
            Log.Add("fired:" + revealed.Card.Id);
            return Mode == "ask" ? new RevealEventAsk("Test: discard your hand?", []) : null;
        }, static (engine, revealed, choice, target) => Log.Add("answer:" + choice));
        return effect;
    }

    private static T With<T>(string effect, string mode, Func<T> body)
    {
        Flag(effect, mode);
        try { return body(); } finally { ProvinceRevealEvents.UnregisterForTest(effect); Mode = ""; }
    }

    private static string FlaggedEffect() => Events().First(c => c.Id != Plain().Id).Effect;

    private static void ResolvesAndStays()
    {
        var effect = FlaggedEffect();
        With(effect, "", () =>
        {
            var (card, e) = Table(effect, 1, faceUp: true);
            e.EventsPhaseForTest(0);
            Check(Log.Count(l => l == "fired:" + card.Id) == 1, "it fired once: " + string.Join(",", Log));
            Check(e.State.Cards[card.Id] is { Zone: Zone.Province, FaceUp: true, ProvinceIndex: 1 } && e.State.Players[0].Provinces[1].CardId == card.Id, "it is still face up in its province");
            Check(e.State.Phase == Phase.Action, "the Events Phase went on: " + e.State.Phase);
            return 0;
        });
        var plain = Plain();
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var ordinary = Batch336Support.Add(s, plain.Id, 0, Zone.Province);
        s.Players[0].Provinces[0].CardId = ordinary.Id; ordinary.ProvinceIndex = 0; ordinary.FaceUp = false;
        var f = MatchEngine.FromTestState(s);
        f.EventsPhaseForTest(0);
        Check(f.State.Cards[ordinary.Id].Zone == Zone.DynastyDiscard, "an ordinary Event is discarded after it resolves");
        Check(f.State.ProvinceRevealResolved is null, "and leaves no stamp behind");
    }

    private static void RevealedThenFires()
    {
        var effect = FlaggedEffect();
        With(effect, "", () =>
        {
            var (card, e) = Table(effect, 2, faceUp: false);
            Check(!e.ResolveRevealEventsForTest(0) && Log.Count == 0, "a face-down flagged Event fires nothing before it is revealed");
            e.EventsPhaseForTest(0);
            Check(Log.Count(l => l == "fired:" + card.Id) == 1, "revealed in the loop, it fired in the same phase: " + string.Join(",", Log));
            Check(e.State.Cards[card.Id] is { Zone: Zone.Province, FaceUp: true }, "and it stays face up in its province");
            return 0;
        });
    }

    private static void OncePerTurn()
    {
        var effect = FlaggedEffect();
        With(effect, "", () =>
        {
            var (card, e) = Table(effect, 0, faceUp: true);
            e.EventsPhaseForTest(0);
            Check(!e.ResolveRevealEventsForTest(0) && Log.Count == 1, "asked again the same turn it does not fire again");
            var later = e.State; later.TurnNumber += 2; later.ActivePlayerId = 0; later.Phase = Phase.Straighten;
            var g = MatchEngine.FromTestState(later);
            g.EventsPhaseForTest(0);
            Check(Log.Count == 2, "next turn it fires again: " + Log.Count);
            var other = g.State; other.TurnNumber += 1; other.ActivePlayerId = 1; other.Phase = Phase.Straighten;
            var h = MatchEngine.FromTestState(other);
            h.EventsPhaseForTest(1);
            Check(Log.Count == 2, "the other player's Events Phase never fires it");
            return card;
        });
    }

    private static void DecisionPauses()
    {
        var effect = FlaggedEffect();
        With(effect, "ask", () =>
        {
            var (card, built) = Table(effect, 0, faceUp: true);
            var later = built.State.Players[0].Provinces[3].CardId!;
            var e = Batch336Support.Edit(built, st => st.Cards[later].FaceUp = false);
            e.EventsPhaseForTest(0);
            Check(e.State.Phase == Phase.Events && e.State.PendingChoice is { Type: "eventChoice" } && e.State.PendingEventChoice is { Kind: "p5-reveal-event", Origin: "reveal" }, "the Events Phase paused on the decision: " + e.State.Phase + "/" + e.State.PendingEventChoice?.Kind);
            var options = e.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
            Check(options.Length == 2 && options.Any(a => a.Command!.Choice == "yes") && options.Any(a => a.Command!.Choice == "no"), "the owner may answer yes or no");
            Check(e.QueryActions(1).All(a => a.Type != "eventChoice"), "the other player is not asked");
            var json = System.Text.Json.JsonSerializer.Serialize(e.State);
            var restored = System.Text.Json.JsonSerializer.Deserialize<MatchState>(json)!;
            Check(restored.PendingEventChoice is { Kind: "p5-reveal-event" } && restored.ProvinceRevealResolved?.ContainsKey(card.Id) == true, "the decision and the stamp survive a save");
            Accept(e, options.First(a => a.Command!.Choice == "yes").Command!);
            Check(Log.Contains("answer:yes"), "the answer was applied");
            Check(e.State.PendingChoice is null && e.State.Phase == Phase.Action, "the phase resumed and went on: " + e.State.Phase);
            Check(Log.Count(l => l == "fired:" + card.Id) == 1, "and the Event did not fire a second time on the resume");
            Check(e.State.Cards[card.Id] is { Zone: Zone.Province, FaceUp: true }, "it is still in its province");
            Check(e.State.Cards[later].FaceUp, "the provinces after it were revealed after the answer");
            return 0;
        });
    }

    private static void Destroyed()
    {
        var effect = FlaggedEffect();
        With(effect, "", () =>
        {
            var (card, e) = Table(effect, 1, faceUp: true);
            e.DestroyProvinceForTest(0, 1);
            Check(e.State.Cards[card.Id].Zone != Zone.Province, "the destroyed province discarded the Event: " + e.State.Cards[card.Id].Zone);
            e.EventsPhaseForTest(0);
            Check(Log.Count == 0, "and it no longer fires");
            return 0;
        });
    }
}
