using static RegressionAssertions;
using L5R.Core;

// Bayushi Nitoshi - inexperienced (issue #617): look at the top card of the Fate deck; an attachment, or a Strategy playable this phase, may be taken ("Yes") and the game then holds until it is
// played or returned to the top of the deck ("No" leaves it there at once).
internal static class NitoshiInexperiencedTests
{
    private const string Nitoshi = "oracle-l5r-10248";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published as printed: Honor and Treachery's Nitoshi carries the Battle and Limited halves of one shared-use look", PublishedShape),
            ("Limited, an attachment on top: Yes takes it and holds the game; nothing but its attach or the return button is accepted; the attach pays, and ends the action", AttachmentYesHoldsThenAttaches),
            ("Return to top of deck: the card goes back to the top, the hand is as before, the hold is gone and priority passes", ReturnPutsTheCardBack),
            ("No leaves the card on top at once; a card that is neither an attachment nor a Strategy playable now cannot be taken (Yes is disabled with its reason, and refused)", NoAndIneligibleCards),
            ("Battle, a Strategy on top: Yes holds; its own play is offered beside the return; playing it spends it, lifts the hold and passes priority; the other player's actions stay waiting", BattleStrategyYesPlays),
            ("A real Strategy (Banish All Doubt, Limited, in the Action phase) is played through its own ability: the play's own look opens inside the hold, the hold waits for it, then ends with the action", RealStrategyPlaysThroughItsAbility),
            ("Hidden information: the taken card's name is never in the opponent's view, actions or events until it is played", HiddenFromTheOpponent),
        ];
        runner.Run("nitoshi-inexperienced", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }

    private static MatchState Start(Phase phase, string topCard)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = phase; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        foreach (var c in s.Cards.Values.Where(c => c.Zone is Zone.DynastyDeck or Zone.FateDeck).ToArray()) s.Cards.Remove(c.Id);
        foreach (var p in s.Players) { p.DynastyDeck.Clear(); p.FateDeck.Clear(); }
        if (phase == Phase.Battle) s.CurrentBattleProvince = 0;
        Put(s, "training-lion-samurai", 0, Zone.FateDeck); // the card under the top one
        var top = Put(s, topCard, 0, Zone.FateDeck); s.Players[0].FateDeck.Remove(top.Id); s.Players[0].FateDeck.Insert(0, top.Id);
        return s;
    }

    private static CardInstance Put(MatchState s, string definition, int owner, Zone zone, int? province = null)
    {
        var n = s.Cards.Count; while (s.Cards.ContainsKey($"test-{n}")) n++;
        var c = new CardInstance { Id = $"test-{n}", DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone is not (Zone.Hand or Zone.DynastyDeck or Zone.FateDeck), ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        var p = s.Players[owner];
        if (zone == Zone.Hand) p.Hand.Add(c.Id);
        if (zone == Zone.FateDeck) p.FateDeck.Add(c.Id);
        return c;
    }

    private static (MatchEngine Engine, CardInstance Nitoshi) Table(Phase phase, string topCard)
    {
        var s = Start(phase, topCard);
        var nitoshi = phase == Phase.Battle ? Put(s, Nitoshi, 0, Zone.Battlefield, province: 0) : Put(s, Nitoshi, 0, Zone.Home);
        Put(s, "training-lion-samurai", 1, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, province: phase == Phase.Battle ? 0 : null);
        return (MatchEngine.FromTestState(s), nitoshi);
    }

    private static string Ability(MatchState s) => s.Phase == Phase.Battle ? "nitoshi-inexp-look-battle" : "nitoshi-inexp-look-limited";
    private static GameCommand Use(MatchEngine e, CardInstance nitoshi) => new("useAbility", 0, e.Version, nitoshi.Id, AbilityId: Ability(e.State));
    private static GameCommand Arrange(MatchEngine e, string disposition) => new("arrangeTopCards", 0, e.Version, CardIds: [$"0:{disposition}"]);
    private static string TopDefinition(MatchEngine e, int player = 0) => e.State.Cards[e.State.Players[player].FateDeck[0]].DefinitionId;

    private static void PublishedShape()
    {
        var card = CardCatalog.Get(Nitoshi);
        Check(card.Type == CardType.Personality && card.Force == 6 && card.Chi == 4 && card.GoldCost == 10 && card.PersonalHonor == 1, "stats");
        Check(card.Abilities!.SequenceEqual(["nitoshi-inexp-look-battle", "nitoshi-inexp-look-limited"]), "abilities");
        Check(AbilityCatalog.All["nitoshi-inexp-look-battle"].Timing == "Battle" && AbilityCatalog.All["nitoshi-inexp-look-limited"].Timing == "Limited", "timings");
        Check(AbilityCatalog.All["nitoshi-inexp-look-battle"].UsageKey == AbilityCatalog.All["nitoshi-inexp-look-limited"].UsageKey, "one shared use");
        Check(LookCatalog.All.ContainsKey(NitoshiInexperienced.Look.Id) && NitoshiInexperienced.Look.Count == 1, "a one-card look");
    }

    private static void AttachmentYesHoldsThenAttaches()
    {
        var (e, nitoshi) = Table(Phase.Action, "training-armor");
        var armor = e.State.Players[0].FateDeck[0];
        Accept(e, Use(e, nitoshi));
        var view = e.GetView(0).PendingLook ?? throw new Exception("The player sees the card");
        Check(view.Cards![0].Dispositions.Single(d => d.Disposition == "playit").Legal, "an attachment may be taken");
        Check(view.Cards[0].Dispositions.Single(d => d.Disposition == "top").Label.StartsWith("No"), "the question is Yes / No");
        Accept(e, Arrange(e, "playit"));
        Check(e.State.PendingChoice?.Type == "nitoshiHold" && e.State.Players[0].Hand.Contains(armor), "the card is in the hand and the game holds");
        Check(e.State.PriorityPlayerId == 0, "the holder keeps priority");
        var offered = e.QueryActions(0).Where(a => a.Type is not "concede").ToArray();
        Check(offered.Any(a => a.Type == "nitoshiAttach" && a.Enabled) && offered.Any(a => a.Type == "nitoshiReturn"), "attach and return are offered");
        Check(offered.All(a => a.Type is "nitoshiAttach" or "nitoshiReturn"), "nothing else is offered during the hold");
        Reject(e, new("pass", 0, e.Version));
        Reject(e, new("endDynasty", 0, e.Version));
        Check(e.State.PendingChoice?.Type == "nitoshiHold", "a rejected command leaves the hold");
        var attach = offered.Single(a => a.Type == "nitoshiAttach").Command!;
        Accept(e, attach);
        Check(e.State.Cards[armor].Zone == Zone.Attached && e.State.Cards[armor].AttachedTo == nitoshi.Id, "attached to the Personality at Nitoshi's location");
        Check(e.State.PendingChoice is null && e.State.Nitoshi.CardId is null && e.State.PriorityPlayerId == 1, "the hold is over and priority passes");
    }

    private static void ReturnPutsTheCardBack()
    {
        var (e, nitoshi) = Table(Phase.Action, "training-armor");
        var armor = e.State.Players[0].FateDeck[0]; var handBefore = e.State.Players[0].Hand.Count; var deckBefore = e.State.Players[0].FateDeck.Count;
        Accept(e, Use(e, nitoshi)); Accept(e, Arrange(e, "playit"));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "nitoshiReturn").Command!);
        Check(e.State.Players[0].FateDeck[0] == armor && e.State.Players[0].FateDeck.Count == deckBefore && e.State.Players[0].Hand.Count == handBefore, "back on top, hand as before");
        Check(e.State.Cards[armor].Zone == Zone.FateDeck && !e.State.Cards[armor].FaceUp, "face down in the deck");
        Check(e.State.PendingChoice is null && e.State.Nitoshi.CardId is null && e.State.PriorityPlayerId == 1, "hold gone, priority passes");
    }

    private static void NoAndIneligibleCards()
    {
        var (e, nitoshi) = Table(Phase.Action, "training-armor");
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Use(e, nitoshi)); Accept(e, Arrange(e, "top"));
        Check(TopDefinition(e) == "training-armor" && e.State.Players[0].Hand.Count == handBefore && e.State.PendingChoice is null && e.State.PriorityPlayerId == 1, "No leaves it on top and the action ends");

        // A Strategy whose play is a Battle action cannot be taken in the Action phase.
        var (e2, n2) = Table(Phase.Action, "training-rally");
        var hand2 = e2.State.Players[0].Hand.Count;
        Accept(e2, Use(e2, n2));
        var disposition = e2.GetView(0).PendingLook!.Cards![0].Dispositions.Single(d => d.Disposition == "playit");
        Check(!disposition.Legal && !string.IsNullOrEmpty(disposition.Reason), "Yes is disabled with a reason");
        Reject(e2, Arrange(e2, "playit"));
        Check(e2.State.Players[0].Hand.Count == hand2 && TopDefinition(e2) == "training-rally", "a refused Yes changes nothing");
        Accept(e2, Arrange(e2, "top"));
    }

    private static void BattleStrategyYesPlays()
    {
        var (e, nitoshi) = Table(Phase.Battle, "training-rally");
        var rally = e.State.Players[0].FateDeck[0];
        Accept(e, Use(e, nitoshi));
        Check(e.GetView(0).PendingLook!.Cards![0].Dispositions.Single(d => d.Disposition == "playit").Legal, "a Battle Strategy may be taken in a battle");
        Accept(e, Arrange(e, "playit"));
        Check(e.State.PendingChoice?.Type == "nitoshiHold" && e.State.Players[0].Hand.Contains(rally), "held, in the hand");
        var offered = e.QueryActions(0).Where(a => a.Type is not "concede").ToArray();
        Check(offered.Any(a => a.Type == "playStrategy" && a.CardId == rally && a.Enabled) && offered.Any(a => a.Type == "nitoshiReturn"), "its own play and the return are offered");
        Check(offered.All(a => a.Type is "playStrategy" or "nitoshiReturn"), "nothing else is offered");
        Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the other player waits");
        Reject(e, new("pass", 0, e.Version));
        Accept(e, offered.First(a => a.Type == "playStrategy").Command!);
        Check(e.State.Cards[rally].Zone == Zone.FateDiscard, "spent as an ordinary play");
        Check(e.State.PendingChoice is null && e.State.Nitoshi.CardId is null, "the hold is lifted");
        Check(e.State.PriorityPlayerId == 1 && !e.State.AdditionalActions.Any(g => g.ScopeCardIds is not null && g.ScopeCardIds.Contains(rally)), "priority passes; no grant is left");
    }

    private static void RealStrategyPlaysThroughItsAbility()
    {
        var s = Start(Phase.Action, "oracle-l5r-690");
        foreach (var filler in new[] { "training-armor", "training-kama", "training-spell", "training-reserve-item" }) Put(s, filler, 0, Zone.FateDeck);
        var nitoshi = Put(s, Nitoshi, 0, Zone.Home); var tactician = Put(s, "training-tactician", 0, Zone.Home);
        Put(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var strategy = e.State.Players[0].FateDeck[0];
        Accept(e, Use(e, nitoshi)); Accept(e, Arrange(e, "playit"));
        Check(e.State.PendingChoice?.Type == "nitoshiHold" && e.State.Players[0].Hand.Contains(strategy), "held");
        var offered = e.QueryActions(0).Where(a => a.Type is not "concede").ToArray();
        Check(offered.All(a => a.CardId == strategy || a.Type == "nitoshiReturn"), "only the card's own plays and the return");
        var play = offered.First(a => a.Type == "useAbility" && a.Enabled && a.Command!.PerformerId == tactician.Id);
        Accept(e, play.Command!);
        Check(e.State.PendingLook is not null && e.State.Nitoshi.CardId == strategy, "the Strategy's own look is open and the hold is remembered");
        Accept(e, new("arrangeTopCards", 0, e.Version, CardIds: ["0:hand", "1:bottom", "2:bottom", "3:bottom"]));
        Check(e.State.PendingChoice is null && e.State.Nitoshi.CardId is null && e.State.Cards[strategy].Zone == Zone.FateDiscard, "the Strategy is spent and the hold has ended");
        Check(e.State.PriorityPlayerId == 1, "priority passes once");
    }

    private static void HiddenFromTheOpponent()
    {
        var (e, nitoshi) = Table(Phase.Battle, "training-rally");
        var rally = e.State.Players[0].FateDeck[0];
        Accept(e, Use(e, nitoshi));
        Check(e.GetView(1).PendingLook is null, "the opponent has no look");
        Accept(e, Arrange(e, "playit"));
        var json = System.Text.Json.JsonSerializer.Serialize(e.GetView(1).Events) + System.Text.Json.JsonSerializer.Serialize(e.GetView(1).PendingChoice);
        Check(!json.Contains("Training rally") && !json.Contains(rally), "the taken card is not named to the opponent");
        Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the opponent only waits");
    }
}
