using L5R.Core;

// Issue #898: reactions to the same moment stack. Two Chrysanthemum Blossoms on one Personality both react to his assignment; playing the first must leave the second on offer.
internal static class ReactionStackingTests
{
    private const string Blossom = "oracle-l5r-1340";
    public static void Run(TestRunner runner) => runner.Run("reaction-stacking", [
        ("Two Chrysanthemum Blossoms on one Personality each give +1F and 1 Honor after his assignment", TwoBlossoms),
        ("Two Entrenched Positions in the Defender's hand are each asked about the Attacker's early Battle action: passing on the first still lets him play the second", TwoEntrenchedPositions),
        ("Several Stormless Furies in hand each attach to the performing Thunder Shugenja after his Battle action, and one already attached to another Shugenja still bows the performer",StormlessFuries),
        ("Two copies of Tamori Ruya (if the card is not Unique) are both asked about the acting player's bow", TwoRuyas),
        ("Togashi Hizumi's and Tamori Ruya's windows for the same bow are both opened, one after the other", HizumiAndRuya),
    ]);

    private const string Hizumi = "oracle-l5r-8521", Ruya = "oracle-l5r-7789";

    private static void TwoEntrenchedPositions()
    {
        var entrenched = CardCatalog.Cards.Values.First(c => (c.Abilities ?? []).Contains("entrenched-negate")).Id;
        var s = Batch336Support.Table(Phase.Battle, 1); s.BattleNumber = 1; s.FirstActionPlayerId = 1; s.AttackerLeadBattle = 1;
        var attackerCard = Batch336Support.Add(s, entrenched, 1, Zone.Hand);
        var first = Batch336Support.Add(s, entrenched, 0, Zone.Hand); var second = Batch336Support.Add(s, entrenched, 0, Zone.Hand);
        Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var victim = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        RegressionAssertions.Accept(e, Batch336Support.Offer(e, 1, "entrenched-bow", attackerCard.Id, victim.Id));
        var asked = new List<string>();
        for (var guard = 0; guard < 10 && e.State.PendingChoice is { Type: "announceReaction" } open; guard++)
        {
            if (!asked.Contains(open.CardId!)) asked.Add(open.CardId!);
            if (asked.Count == 2 && e.State.PriorityPlayerId == 0) { RegressionAssertions.Accept(e, e.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "entrenched-negate").Command!); break; }
            RegressionAssertions.Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); // the window alternates: each side passes in turn
        }
        Batch336Support.Check(asked.Count == 2 && asked.Contains(first.Id) && asked.Contains(second.Id), "both copies were asked about: " + string.Join(", ", asked));
        for (var guard = 0; e.State.PendingChoice is not null && guard < 6; guard++) RegressionAssertions.Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Batch336Support.Check(!e.State.Cards[victim.Id].Bowed, "the second copy negated the Attacker's action");
    }

    private static void StormlessFuries()
    {
        const string Fury = "oracle-l5r-7512";
        var s = Batch336Support.Table(Phase.Battle, 0);
        var performer = Batch336Support.Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
        var other = Batch336Support.Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
        Batch336Support.Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var worn = Batch336Support.Add(s, Fury, 0, Zone.Attached, province: 0, parent: other.Id);
        var a = Batch336Support.Add(s, Fury, 0, Zone.Hand); var b = Batch336Support.Add(s, Fury, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        RegressionAssertions.Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: performer.Id, AbilityId: "training-weapon-attack"));
        var attached = new List<string>();
        for (var guard = 0; guard < 12 && e.State.PendingChoice is { Type: "battleActionReaction" } open; guard++)
        {
            var mine = e.State.PriorityPlayerId == 0 ? e.QueryActions(0).FirstOrDefault(o => o.Enabled && o.Type == "useAbility" && o.CardId == open.CardId) : null;
            if (mine is not null) { RegressionAssertions.Accept(e, mine.Command!); if (mine.Command!.AbilityId == "stormless-fury-attach") attached.Add(open.CardId!); continue; }
            RegressionAssertions.Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        }
        Batch336Support.Check(attached.Contains(a.Id) && attached.Contains(b.Id), "both Furies in hand attached: " + string.Join(", ", attached));
        Batch336Support.Check(e.State.Cards[a.Id].AttachedTo == performer.Id && e.State.Cards[b.Id].AttachedTo == performer.Id, "to the performing Shugenja");
        Batch336Support.Check(e.State.Cards[performer.Id].Bowed, "the Fury already worn by the other Shugenja bowed the performer");
    }

    private static void TwoRuyas()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var creator = Batch336Support.Add(s, "training-grant-bower", 0, Zone.Battlefield, province: 0);
        var a = Batch336Support.Add(s, Ruya, 0, Zone.Battlefield, province: 0); var b = Batch336Support.Add(s, Ruya, 0, Zone.Battlefield, province: 0);
        var victim = Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        RegressionAssertions.Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: creator.Id, AbilityId: "training-grant-bow", TargetId: victim.Id));
        var windows = new List<string>();
        for (var guard = 0; guard < 8 && e.State.PendingChoice is { Type: "bowReaction" } open; guard++)
        {
            if (!windows.Contains(open.CardId!)) windows.Add(open.CardId!);
            RegressionAssertions.Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        }
        Batch336Support.Check(windows.Contains(a.Id) && windows.Contains(b.Id), "both Ruyas were asked: " + string.Join(", ", windows));
    }

    private static void HizumiAndRuya()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var creator = Batch336Support.Add(s, "training-grant-bower", 0, Zone.Battlefield, province: 0);
        var ruya = Batch336Support.Add(s, Ruya, 0, Zone.Battlefield, province: 0);
        var hizumi = Batch336Support.Add(s, Hizumi, 1, Zone.Battlefield, province: 0);
        var victim = Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        RegressionAssertions.Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: creator.Id, AbilityId: "training-grant-bow", TargetId: victim.Id));
        var windows = new List<string>();
        for (var guard = 0; guard < 8 && e.State.PendingChoice is { Type: "bowReaction" } open; guard++)
        {
            windows.Add(open.CardId!);
            RegressionAssertions.Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        }
        Batch336Support.Check(windows.Contains(hizumi.Id) && windows.Contains(ruya.Id), "both watchers were asked: " + string.Join(", ", windows));
    }



    private static void TwoBlossoms()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.InfantryDeclaration; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.UnresolvedBattlefields = [0, 1, 2, 3];
        var host = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        var a = Batch336Support.Add(s, Blossom, 0, Zone.Attached, parent: host.Id);
        var b = Batch336Support.Add(s, Blossom, 0, Zone.Attached, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        var force = e.CardForceForTest(host.Id); var honor = Batch336Support.Honor(e, 0);
        RegressionAssertions.Accept(e, new GameCommand("attack", 0, e.Version, Assignments: new Dictionary<string, int> { [host.Id] = 0 }));
        Batch336Support.Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the assignment opens the window");
        Batch336Support.Check(Batch336Support.CanUse(e, 0, "chrysanthemum-blossom-boost", a.Id) && Batch336Support.CanUse(e, 0, "chrysanthemum-blossom-boost", b.Id), "both Blossoms are offered");
        RegressionAssertions.Accept(e, Batch336Support.Enabled(e, 0, "chrysanthemum-blossom-boost", a.Id).Command!);
        Batch336Support.Check(e.State.PendingChoice?.Type == "afterActionResolvedReaction", "the window stays open for the second Blossom: " + e.State.PendingChoice?.Type);
        Batch336Support.Check(Batch336Support.CanUse(e, 0, "chrysanthemum-blossom-boost", b.Id), "the second Blossom is still offered after the first was played");
        RegressionAssertions.Accept(e, Batch336Support.Enabled(e, 0, "chrysanthemum-blossom-boost", b.Id).Command!);
        Batch336Support.Eq(e.CardForceForTest(host.Id), force + 2, "two +1F tokens");
        Batch336Support.Eq(Batch336Support.Honor(e, 0), honor + 2, "two Honor");
    }
}
