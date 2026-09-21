using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #419 (stage 5 of Mendon's rulings of 2026-09-21), second part: a Focus Effect that needs a decision pauses the duel's resolution, and the duel goes on from the next Focus Effect and
// finishes as it always did. Disreputable Deal: put a non-Unique Strategy with a Political ability from your discard pile into your hand.
internal static class FocusChoiceTests
{
    private const string DisreputableDeal = "oracle-l5r-2031", Conspiracy = "oracle-l5r-2011", ScandalousRumors = "oracle-l5r-6488", KnowNoFear = "oracle-l5r-4481", QuietTransaction = "oracle-l5r-70";

    public static void Run(TestRunner runner) => runner.Run("focus-effect-choice", [
        ("Published shape: Disreputable Deal is the printed card with its Focus Effect and its Political Open action, and ships its art", PublishedShape),
        ("Disreputable Deal as a Focus Effect: the duel stops, the player chooses among the non-Unique Strategies with a Political ability in his discard pile (or takes nothing), the pick goes to his hand and the duel then finishes", FocusEffectPauses),
        ("The choice is judged for the player whose card it is, from his own discard pile only, and by the Strategy's abilities: an ability printed Political counts, a Unique one or one without does not", Candidates),
        ("A pause in the middle of several Focus Effects: those before it applied, those after it apply once the choice is made, in the order chosen, and the duel's spent cards and outcome are as without a pause", InTheMiddle),
        ("Nothing qualifying asks nobody: the duel finishes at once with the reason in the log", NothingQualifies),
        ("Disreputable Deal's Political Open action: a performing Courtier bows, the target has -3F, and a dishonorable one's controller loses Honor equal to his printed Personal Honor", OpenAction),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardDefinition Def(CardInstance card) => CardCatalog.Get(card.DefinitionId);

    private static void PublishedShape()
    {
        var card = CardCatalog.Get(DisreputableDeal);
        Check(card.Name == "Disreputable Deal" && card.Type == CardType.Strategy && card.Focus == 2 && card.Keywords!.Contains("Political")
            && card.Abilities!.SequenceEqual(new[] { "disreputable-deal-focus", "disreputable-deal-penalty" }), "Disreputable Deal");
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", DisreputableDeal + ".jpg")), "art is shipped");
        Eq(card.ArtworkKey, $"res://Content/Published/{DisreputableDeal}.jpg");
        Check(AbilityCatalog.All["disreputable-deal-focus"].Timing == "FocusEffect" && AbilityCatalog.All["disreputable-deal-penalty"].ActionKeywords!.Contains("Political"), "a Focus Effect and a Political action");
    }

    // A duel at province 0: player 0 challenges (Steel on Steel), focuses `focused` from his hand; player 1 focuses the top of his deck. The Strategies in player 0's discard pile are the candidates.
    private static (MatchEngine E, CardInstance Focused, CardInstance Mine, CardInstance Theirs, CardInstance Rumors, CardInstance NotPolitical, CardInstance UniqueOne) Duel(string focusedDef = DisreputableDeal, bool challengerWins = true)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, challengerWins ? "training-crane-veteran" : "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var theirs = Add(s, challengerWins ? "training-lion-veteran" : "training-paneki-exp3", 1, Zone.Battlefield, province: 0);
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused = Add(s, focusedDef, 0, Zone.Hand);
        var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
        var rumors = Add(s, ScandalousRumors, 0, Zone.FateDiscard);
        var plain = Add(s, KnowNoFear, 0, Zone.FateDiscard);
        var unique = Add(s, QuietTransaction, 0, Zone.FateDiscard); GiveKeyword(unique, "Unique"); GiveKeyword(unique, "Political");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focused.Id));
        return (e, focused, mine, theirs, rumors, plain, unique);
    }

    private static void FocusEffectPauses()
    {
        var (e, focused, mine, theirs, rumors, notPolitical, unique) = Duel();
        Accept(e, new GameCommand("strike", 1, e.Version));
        Eq(e.State.PendingChoice?.Type, "focusEffectChoice", "the duel stops at the Focus Effect");
        Check(e.State.PendingDuel is not null && e.State.PendingFocusChoice is not null, "the duel is kept while it waits");
        Eq(e.State.PendingChoice?.PlayerId, 0, "its player chooses");
        var picks = e.QueryActions(0).Where(x => x.Type == "chooseFocusEffect").ToArray();
        Check(picks.Any(x => x.CardId == rumors.Id) && !picks.Any(x => x.CardId == notPolitical.Id || x.CardId == unique.Id), "the non-Unique Political Strategy is offered, the others are not: " + string.Join(",", picks.Select(x => x.Label)));
        Check(picks.Any(x => x.Command!.Choice == "decline"), "and taking nothing is");
        Check(!e.QueryActions(1).Any(x => x.Type == "chooseFocusEffect"), "the other player has nothing to choose");
        Reject(e, new GameCommand("chooseFocusEffect", 1, e.Version, TargetId: rumors.Id));
        Reject(e, new GameCommand("chooseFocusEffect", 0, e.Version, TargetId: notPolitical.Id));
        Reject(e, new GameCommand("chooseFocusEffect", 0, e.Version, TargetId: unique.Id));
        Check(e.State.Cards[focused.Id].Zone == Zone.Focused || e.State.Cards[focused.Id].Zone == Zone.FateDiscard, "the focused card has not been spent yet or is being");
        Accept(e, new GameCommand("chooseFocusEffect", 0, e.Version, TargetId: rumors.Id));
        Eq(e.State.Cards[rumors.Id].Zone, Zone.Hand, "the Strategy is in his hand");
        Check(e.State.Players[0].Hand.Contains(rumors.Id), "in the hand list");
        Check(e.State.PendingChoice is null && e.State.PendingDuel is null && e.State.PendingFocusChoice is null, "and the duel finished");
        Eq(e.State.Cards[focused.Id].Zone, Zone.FateDiscard, "the focused card was spent");
        Check(e.State.Cards[theirs.Id].Zone == Zone.Dead || e.State.Cards[mine.Id].Zone == Zone.Dead, "the duel's own outcome applied: the loser was destroyed");

        // Decline: nothing moves, the duel still finishes.
        var (f, _, mine2, theirs2, rumors2, _, _) = Duel();
        Accept(f, new GameCommand("strike", 1, f.Version));
        Accept(f, f.QueryActions(0).Single(x => x.Type == "chooseFocusEffect" && x.Command!.Choice == "decline").Command!);
        Eq(f.State.Cards[rumors2.Id].Zone, Zone.FateDiscard, "declined: it stays in the discard pile");
        Check(f.State.PendingDuel is null && (f.State.Cards[theirs2.Id].Zone == Zone.Dead || f.State.Cards[mine2.Id].Zone == Zone.Dead), "and the duel finished");
    }

    private static void Candidates()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var theirs = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand); var deal = Add(s, DisreputableDeal, 0, Zone.Hand);
        var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
        var yours = Add(s, ScandalousRumors, 0, Zone.FateDiscard);                      // qualifies
        var theirsInPile = Add(s, ScandalousRumors, 1, Zone.FateDiscard);               // the other player's pile: no
        var inHand = Add(s, ScandalousRumors, 0, Zone.Hand);                             // in the hand, not the discard pile: no
        var dead = Add(s, "training-crane-samurai", 0, Zone.Dead);                       // not a Strategy: no
        var withoutPolitical = Add(s, "training-focus-strategy", 0, Zone.FateDiscard);   // no Political ability: no
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: deal.Id));
        Accept(e, new GameCommand("strike", 1, e.Version));
        var ids = e.QueryActions(0).Where(x => x.Type == "chooseFocusEffect" && x.Command!.TargetId is not null).Select(x => x.Command!.TargetId).ToArray();
        Check(ids.SequenceEqual(new[] { yours.Id }), "only his own discarded, non-Unique Strategy with a Political ability: " + string.Join(",", ids));
        Check(inHand.Id != yours.Id && theirsInPile.Id != yours.Id && dead.Id != yours.Id && withoutPolitical.Id != yours.Id);
    }

    private static void InTheMiddle()
    {
        // Two Focus Effects: Disreputable Deal (paused) and Discovering a Conspiracy (+2 Honor if the duel is won). Player 0 orders them; whichever comes second waits for the choice.
        foreach (var dealFirst in new[] { true, false })
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var theirs = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand); var deal = Add(s, DisreputableDeal, 0, Zone.Hand); var conspiracy = Add(s, Conspiracy, 0, Zone.Hand);
            var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
            var rumors = Add(s, ScandalousRumors, 0, Zone.FateDiscard);
            var e = MatchEngine.FromTestState(s);
            var honor = Honor(e, 0);
            Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: deal.Id));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: conspiracy.Id));
            Accept(e, new GameCommand("strike", 1, e.Version));
            Eq(e.State.PendingDuel?.Stage, "OrderingEffects", "the active player orders the two Focus Effects");
            Accept(e, new GameCommand("orderDuelFocusEffects", 0, e.Version, CardIds: dealFirst ? [deal.Id, conspiracy.Id] : [conspiracy.Id, deal.Id]));
            Eq(e.State.PendingChoice?.Type, "focusEffectChoice", "the Deal's choice is asked");
            var won = e.State.Cards[theirs.Id].Zone == Zone.Dead;
            Check(won, "the duel's outcome had applied (the loser destroyed)");
            Eq(Honor(e, 0), dealFirst ? honor : honor + 2, dealFirst ? "the Conspiracy's Focus Effect comes after the pause: not applied yet" : "the Conspiracy came before the Deal and has applied");
            Accept(e, new GameCommand("chooseFocusEffect", 0, e.Version, TargetId: rumors.Id));
            Eq(Honor(e, 0), honor + 2, "once the choice is made the rest applies");
            Check(e.State.PendingDuel is null && e.State.Cards[deal.Id].Zone == Zone.FateDiscard && e.State.Cards[conspiracy.Id].Zone == Zone.FateDiscard, "both focused cards spent, the duel finished");
            Eq(e.State.Cards[rumors.Id].Zone, Zone.Hand, "and the Strategy is in the hand");
        }
    }

    private static void NothingQualifies()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var theirs = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand); var deal = Add(s, DisreputableDeal, 0, Zone.Hand);
        var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: deal.Id));
        Accept(e, new GameCommand("strike", 1, e.Version));
        Check(e.State.PendingChoice is null && e.State.PendingDuel is null, "nobody is asked and the duel is over");
        Check(e.State.Events.Any(x => x.Type == "focusEffectNothing"), "the log says why");
    }

    private static void OpenAction()
    {
        MatchEngine Table_(bool dishonorable, out CardInstance courtier, out CardInstance target, out CardInstance card)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            courtier = Add(s, "training-crane-courtier", 0, Zone.Home); GiveKeyword(courtier, "Courtier");
            target = Add(s, "training-lion-veteran", 1, Zone.Home); target.Dishonorable = dishonorable;
            card = Add(s, DisreputableDeal, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Table_(true, out var courtier, out var target, out var card);
        var honor = Honor(e, 1); var force = e.CardForceForTest(target.Id);
        Accept(e, Offer(e, 0, "disreputable-deal-penalty", card.Id, target.Id, courtier.Id));
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed");
        Eq(e.CardForceForTest(target.Id), Math.Max(0, force - 3), "-3F");
        Eq(Honor(e, 1), honor - Def(target).PersonalHonor, "a dishonorable target's controller lost Honor equal to his printed Personal Honor");
        var f = Table_(false, out var c2, out var t2, out var card2);
        var honor2 = Honor(f, 1);
        Accept(f, Offer(f, 0, "disreputable-deal-penalty", card2.Id, t2.Id, c2.Id));
        Eq(Honor(f, 1), honor2, "an honorable target costs his controller nothing");
        var notCourtier = Add(f.State, "training-crane-samurai", 0, Zone.Home);
        Check(!Options(f, 0, "disreputable-deal-penalty", card2.Id).Any(o => o.Enabled && o.Label.Contains($"({notCourtier.Id})")), "only a Courtier performs it");
    }
}
