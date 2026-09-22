using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #431: the two Before the Dawn cards that needed a small generalization of a primitive the engine already had -- Yasuki Dokansuto (conditional Gold production for a Holding, personality-wave-47)
// and Necessary Evil (an assignment-only movement lock and a Gold cost counted from the Personalities you control, strategy-wave-44).
internal static class BeforeTheDawnGoldAndLockTests
{
    private const string Dokansuto = "oracle-l5r-9437", NecessaryEvil = "oracle-l5r-5524";

    public static void Run(TestRunner runner) => runner.Run("btd-gold-and-lock", [
        ("Published shapes: Yasuki Dokansuto and Necessary Evil are the printed cards with their abilities, traits and art", PublishedShapes),
        ("Yasuki Dokansuto: bows to produce 2 Gold, only when paying for a Holding", DokansutoProduces),
        ("Necessary Evil: pay the number of your Personalities, at least 4; the targeted Personality may not be assigned to battlefields this turn, and nothing else about his movement changes", NecessaryEvilLocks),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        var dok = CardCatalog.Get(Dokansuto); var evil = CardCatalog.Get(NecessaryEvil);
        Check(dok.Name == "Yasuki Dokansuto" && dok.Type == CardType.Personality && dok.Effect == "dokansuto-gold" && (dok.Abilities ?? []).Length == 0, "Dokansuto: a conditional Gold producer");
        Check(evil.Name == "Necessary Evil" && evil.Type == CardType.Strategy && evil.Effect == "none" && (evil.Abilities ?? []).SequenceEqual(["necessary-evil-lock"]), "Necessary Evil");
        foreach (var id in new[] { Dokansuto, NecessaryEvil })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void DokansutoProduces()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var dokansuto = Add(s, Dokansuto, 0, Zone.Home);
        var holding = Add(s, "training-marketplace", 0, Zone.Province); var personality = Add(s, "training-crane-samurai", 0, Zone.Province); var strategy = Add(s, "training-focus-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(dokansuto.Id, 0, new PaymentSubject(holding, PaymentKind.Recruit)), 2, "2 Gold for a Holding bought");
        Eq(e.GoldProduced(dokansuto.Id, 0, new PaymentSubject(personality, PaymentKind.Recruit)), 0, "nothing for a Personality");
        Eq(e.GoldProduced(dokansuto.Id, 0, new PaymentSubject(holding, PaymentKind.Equip)), 0, "nor for an attachment");
        Eq(e.GoldProduced(dokansuto.Id, 0, new PaymentSubject(strategy, PaymentKind.Play)), 0, "nor for a card played");
        Eq(e.GoldProduced(dokansuto.Id, 0, new PaymentSubject(holding, PaymentKind.Action)), 0, "nor for an action");
        Eq(e.GoldProduced(dokansuto.Id, 0), 0, "nor for an unnamed payment");
        // Chosen as a source for a Holding, he bows.
        var producers = e.PaymentOptions(0, 2, new PaymentSubject(holding, PaymentKind.Recruit));
        Check(producers.Any(o => o.SourceIds.Contains(dokansuto.Id)), "Dokansuto is among the ways to pay for a Holding");
        Check(!e.PaymentOptions(0, 2, new PaymentSubject(personality, PaymentKind.Recruit)).Any(o => o.SourceIds.Contains(dokansuto.Id)), "and not for a Personality");
        var bowed = Edit(e, st => st.Cards[dokansuto.Id].Bowed = true);
        Check(!bowed.PaymentOptions(0, 2, new PaymentSubject(holding, PaymentKind.Recruit)).Any(o => o.SourceIds.Contains(dokansuto.Id)), "bowed, he produces nothing");
    }

    private static void NecessaryEvilLocks()
    {
        // Two Personalities on the acting side: the cost is 4 (the floor); the lock is on the target only, and only for assignment.
        foreach (var (mine, expected) in new[] { (2, 4), (4, 4), (6, 6) })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
            s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
            for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home);
            var card = Add(s, NecessaryEvil, 0, Zone.Hand); var foe = Add(s, "training-lion-samurai", 1, Zone.Home);
            for (var i = 0; i < mine; i++) Add(s, "training-crane-samurai", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var option = Options(e, 0, "necessary-evil-lock", card.Id).First(o => o.Enabled);
            Eq(option.Gold!.Cost, expected, $"{mine} Personalities: {expected} Gold");
            if (mine != 2) continue;
            Check(option.TargetIds!.Contains(foe.Id), "any Personality");
            Accept(e, Offer(e, 0, "necessary-evil-lock", card.Id, foe.Id));
            Eq(e.State.Cards[foe.Id].MovementLocks.Count, 1, "one lock on the target");
            Eq(e.State.Cards[foe.Id].MovementLocks[0].Kinds, MovementKinds.Assign, "assignment only: he may still move");
            Check(e.State.Events.Any(x => x.Type == "movementLocked" && x.Message.Contains("assignment to battlefields")), "the log says what is barred");
        }
        // With no Personality of your own it cannot be used.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0; t.Players[0].TurnsTaken = 3; t.Players[1].TurnsTaken = 3;
        t.Cards[t.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        for (var i = 0; i < 8; i++) Add(t, "training-marketplace", 0, Zone.Home);
        var card2 = Add(t, NecessaryEvil, 0, Zone.Hand); Add(t, "training-lion-samurai", 1, Zone.Home);
        var te = MatchEngine.FromTestState(t);
        Check(!Options(te, 0, "necessary-evil-lock", card2.Id).Any(o => o.Enabled), "you must control a Personality: " + string.Join(";", Options(te, 0, "necessary-evil-lock", card2.Id).Select(o => o.Reason)));
    }
}
