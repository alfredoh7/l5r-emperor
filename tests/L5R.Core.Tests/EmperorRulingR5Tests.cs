using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards follow-up (issue #492), Mendon's rulings, group R5: clans (Ninja is a keyword, never a Clan), Shinjo Ki-Chang's trigger (tested in EmperorProxyYTests), He's Mine! and
// Irezu's substitution in an accepted duel, whole-unit bowing and straightening, Sasumata and Terrain, and the player's choice in an Attack's "may compare" clauses.
internal static class EmperorRulingR5Tests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }

    private const string HesMine = "oracle-l5r-3008";

    public static void Run(TestRunner runner) => runner.Run("emperor-ruling-R5", [
        ("Clans: no published card has the Clan \"Ninja\" (there is no Ninja clan); every card that had it keeps Ninja as a keyword and has the Clan of its \"<Clan> Clan\" keyword (Goju, Ninube: Spider; Shosuro, Bayushi: Scorpion)", NinjaIsNotAClan),
        ("Clans: a Spider-clan Ninja such as Goju Oyoto has the Spider clan alignment and the Ninja keyword, and is not aligned with any Ninja clan", NinjaClanAlignment),
        ("He's Mine!: a refusable challenge that has not been accepted opens no substitution window (a crafted command is rejected); once accepted the Samurai may take the place; a refused challenge is no duel", HesMineRefusable),
        ("He's Mine!: the reactor may also replace the challenger of an accepted duel of his own", HesMineChallengerAndIrezu),
    ]);

    // ---- Clans ----------------------------------------------------------------------------------------------------------------------------------------
    private static void NinjaIsNotAClan()
    {
        var ninjaClan = CardCatalog.Cards.Values.Where(c => c.Clan == "Ninja").Select(c => c.Id).ToArray();
        Check(ninjaClan.Length == 0, "cards whose Clan is Ninja: " + string.Join(", ", ninjaClan));
        Check(CardCatalog.Cards.Values.All(c => (c.Clans ?? []).All(clan => clan != "Ninja")), "no card has a Ninja clan among its Clans");
        var expected = new (string Id, string Clan)[]
        {
            ("oracle-l5r-2855", "Spider"), ("oracle-l5r-5590", "Spider"), ("oracle-l5r-5591", "Spider"), ("oracle-l5r-2858", "Spider"), ("oracle-l5r-9711", "Spider"), ("oracle-l5r-2849", "Spider"), ("oracle-l5r-9850", "Spider"),
            ("oracle-l5r-7121", "Scorpion"), ("oracle-l5r-7179", "Scorpion"), ("oracle-l5r-10250", "Scorpion"), ("oracle-l5r-10226", "Scorpion"), ("oracle-l5r-790", "Scorpion"), ("oracle-l5r-840", "Scorpion"),
            ("oracle-l5r-850", "Scorpion"), ("oracle-l5r-9846", "Scorpion"), ("oracle-l5r-10653", "Scorpion"),
            ("oracle-l5r-10008", "Unaligned") // the Ninja Follower Proxy has no Clan of its own
        };
        foreach (var (id, clan) in expected)
        {
            var card = CardCatalog.Get(id);
            Eq(card.Clan, clan, $"{card.Name}'s Clan");
            if (id != "oracle-l5r-10008") Check((card.Keywords ?? []).Contains("Ninja") && (card.Keywords ?? []).Contains($"{clan} Clan"), $"{card.Name} keeps Ninja as a keyword beside {clan} Clan");
        }
        // Every published card keyword "<Clan> Clan" on a Personality agrees with its Clan field when that field is not Unaligned (the audit behind the fix).
        foreach (var card in CardCatalog.Cards.Values.Where(c => c.Type == CardType.Personality && c.Clan != "Unaligned" && (c.Keywords ?? []).Any(k => k.EndsWith(" Clan", StringComparison.Ordinal) && k != "Clan Champion")))
            Check((card.Keywords ?? []).Contains($"{card.Clan} Clan") || (card.Clans ?? []).Any(clan => (card.Keywords ?? []).Contains($"{clan} Clan")), $"{card.Name}: Clan {card.Clan} matches a Clan keyword");
    }

    private static void NinjaClanAlignment()
    {
        var s = Table(Phase.Action, 0);
        var oyoto = Add(s, "oracle-l5r-2855", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(oyoto.Id, "Ninja") && e.HasKeyword(oyoto.Id, "Spider Clan"), "Goju Oyoto is a Ninja and a Spider Clan Personality");
        Eq(CardCatalog.Get("oracle-l5r-2855").Clan, "Spider", "his Clan field");
        Check(e.HasClanAlignmentForTest(oyoto.Id, "Spider") && !e.HasClanAlignmentForTest(oyoto.Id, "Ninja"), "aligned with the Spider Clan, never with a Ninja clan");
        var hawado = Add(s, "oracle-l5r-10226", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        Check(e2.HasClanAlignmentForTest(hawado.Id, "Scorpion") && !e2.HasClanAlignmentForTest(hawado.Id, "Ninja"), "Shosuro Hawado is Scorpion");
    }

    // ---- He's Mine! / Irezu ---------------------------------------------------------------------------------------------------------------------------
    private static void FinishDuel(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingDuel is not null; guard++)
        {
            var mover = e.State.PriorityPlayerId;
            var focus = e.QueryActions(mover).FirstOrDefault(a => a.Enabled && a.Command is not null && (a.Type == "focus" && a.Command.Choice == "deck" || a.Type == "strike" || a.Type == "declineReaction"));
            Check(focus is not null, $"nothing to do for player {mover} ({e.State.PendingChoice?.Type} choice for {e.State.PendingChoice?.PlayerId}, priority {e.State.PriorityPlayerId}, stage {e.State.PendingDuel?.Stage}): " + string.Join("; ", e.QueryActions(mover).Select(a => $"{a.Type}[{a.Enabled}]")));
            Accept(e, focus!.Command!);
        }
        Check(e.State.PendingDuel is null, "the duel did not finish");
    }

    private static (MatchEngine E, CardInstance Card, CardInstance Victim, CardInstance Samurai, CardInstance Duelist, CardInstance Challenge) RefusableSetup()
    {
        var s = Table(Phase.Battle, 1);
        var victim = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var samurai = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Samurai");
        var duelist = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var card = Add(s, HesMine, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "training-duel-challenge", challenge.Id, victim.Id, performerId: duelist.Id));
        return (e, card, victim, samurai, duelist, challenge);
    }

    private static void HesMineRefusable()
    {
        // Awaiting the answer: the challenged player may only accept or refuse; He's Mine! is not on offer and a crafted command is refused (refusal cannot be moved to a new target).
        var (e, card, victim, samurai, _, _) = RefusableSetup();
        Eq(e.State.PendingDuel?.Stage, "AwaitingAccept", "the refusable challenge awaits its answer");
        Check(e.State.PendingChoice is null, "no reaction window is open yet");
        Check(!e.QueryActions(0).Any(a => a.Type == "useAbility"), "no ability is offered while the challenge awaits its answer");
        var rejection = Rejection(e, new GameCommand("useAbility", 0, e.Version, card.Id, TargetId: victim.Id, AbilityId: "hes-mine-substitute", PerformerId: samurai.Id));
        Check(rejection.Length > 0, "a crafted He's Mine! is refused before the challenge is accepted: " + rejection);
        Check(!e.State.Cards[victim.Id].Bowed && e.State.Cards[card.Id].Zone == Zone.Hand, "nothing was paid");
        Eq(e.State.PendingDuel?.ChallengedId, victim.Id, "the challenged Personality is unchanged, so only he may refuse");

        // Accepted: the before-the-duel window opens and the Samurai may take his place.
        Accept(e, e.QueryActions(0).First(a => a.Type == "acceptDuel").Command!);
        Eq(e.State.PendingChoice?.Type, "duelReaction", "accepting opens the window before the duel begins");
        var reaction = Enabled(e, 0, "hes-mine-substitute", card.Id, samurai.Id);
        Accept(e, reaction.Command!);
        Check(e.State.PendingDuel is { } duel && duel.ChallengedId == samurai.Id && e.State.Cards[victim.Id].Bowed, "the Samurai is in the duel and the Personality bowed");
        Check(!e.State.PendingDuel!.Refusable || e.State.PendingDuel.Stage == "Reaction", "the duel is already accepted: nobody may refuse it any more");
        FinishDuel(e);
        Check(e.State.Cards[samurai.Id].Bowed, "the substitute bows after the duel ends");

        // Refused: no duel, no window, and He's Mine! stays in the hand.
        var (r, rcard, _, _, _, _) = RefusableSetup();
        Accept(r, r.QueryActions(0).First(a => a.Type == "declineDuel").Command!);
        Check(r.State.PendingDuel is null && r.State.PendingChoice is null, "a refused challenge is no duel");
        Eq(r.State.Cards[rcard.Id].Zone, Zone.Hand, "He's Mine! was not used");
        Check(!CanUse(r, 0, "hes-mine-substitute", rcard.Id), "it cannot be used after the refusal either");
    }

    private static void HesMineChallengerAndIrezu()
    {
        // The reactor's own duel: player 0 challenges player 1 with an unrefusable duel; player 0 (the challenger) may replace his own participant when the window reaches him.
        var s = Table(Phase.Battle, 0);
        var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var samurai = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Samurai");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, "training-duel-force", 0, Zone.Hand);
        var card = Add(s, HesMine, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-duel-force", challenge.Id, enemy.Id, performerId: mine.Id));
        Eq(e.State.PendingChoice?.Type, "duelReaction", "the mandatory duel opens the window");
        Eq(e.State.PendingChoice?.PlayerId, 1, "the challenged player has the first opportunity");
        Accept(e, new("declineReaction", 1, e.Version));
        Check(CanUse(e, 0, "hes-mine-substitute", card.Id, samurai.Id), "then the challenger may replace his own participant");
        Accept(e, Enabled(e, 0, "hes-mine-substitute", card.Id, samurai.Id).Command!);
        Check(e.State.PendingDuel is { } duel && duel.ChallengerId == samurai.Id && duel.ChallengedId == enemy.Id, "the Samurai now challenges");
        Check(e.State.Cards[mine.Id].Bowed, "the replaced Personality is bowed as the cost");

        // Never the other player's participant: the Lion player cannot use it on the Crane's duelist (he has none), and a Samurai of the wrong side is not a performer.
        var s2 = Table(Phase.Battle, 1);
        var v = Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Kw(Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Samurai");
        var d = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var ch = Add(s2, "training-duel-force", 1, Zone.Hand);
        var mine2 = Add(s2, HesMine, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 1, "training-duel-force", ch.Id, v.Id, performerId: d.Id));
        Accept(e2, new("declineReaction", 0, e2.Version));
        Check(CanUse(e2, 1, "hes-mine-substitute", mine2.Id), "the challenger's controller may use his own copy on his own participant");
        Check(!e2.QueryActions(0).Any(a => a.Label.StartsWith("Before your target Personality enters a duel", StringComparison.Ordinal)), "the other player is offered no He's Mine! of the first player's");
    }
}
