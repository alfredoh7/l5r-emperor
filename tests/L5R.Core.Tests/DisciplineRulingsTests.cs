using static RegressionAssertions;
using L5R.Core;

// Mendon's rulings on the Discipline/Absent PR (#331/#332), implemented in issue #353: a Discipline play from a discard pile pays the Discipline
// cost FIRST and then the action's own costs (one combined quote, offered only when both parts are payable), from hand only the action's own cost;
// Expensive Achievement, Knowledge and Power and Two Steady Breaths (strategy-wave-23); Lost in Transit's per-mode presence (none as an Open action in
// the Action phase, required only as a Battle action); False Ring of Air's bow is a cost. See docs/decisions/2026-09-19-discipline-rulings.md.
internal static class DisciplineRulingsTests
{
    private const string ExpensiveAchievement = "oracle-l5r-10938", KnowledgeAndPower = "oracle-l5r-10942", TwoSteadyBreaths = "oracle-l5r-10961",
        MercantileConflict = "oracle-l5r-10947", LostInTransit = "oracle-l5r-10945", FalseRingOfAir = "oracle-l5r-10531";
    private const string Kp = "knowledge-and-power", Tsb = "two-steady-breaths-destined", Lit = "lost-in-transit-bow", Frair = "false-ring-of-air-force";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("strategy-wave-23 published shape: the three Discipline cards as printed (Focus, the Discipline cost as GoldCost), each ability on the Discipline flag; the two 'Limited, 1 Gold' actions carry an explicit own cost; Mercantile Conflict is published in strategy-wave-110", PublishedShape),
            ("Issue #1030: Knowledge and Power resolving after Perplexing Guests discarded its target does nothing instead of throwing: costs paid, no token created, no exception", TargetDiscardedInTheWindow),
            ("Additive Discipline cost (Knowledge and Power): from the discard pile ONE combined quote of 2 (Discipline 1 first, then the action's own 1), two payments in that order, the card removed from the game; the sources' order is irrelevant", AdditiveQuoteAndPayment),
            ("Additive Discipline cost: Gold does not carry over -- one source covering the total is refused; the play is offered only when BOTH parts are payable (else disabled), and a shortfall of either part is rejected", BothPartsMustBePayable),
            ("From HAND only the action's own cost applies (Knowledge and Power 1; Two Steady Breaths 1, not 3); the Discipline cost is never charged there and the card goes to the Fate discard pile", HandPlayPaysOnlyTheOwnCost),
            ("Two Steady Breaths: 2 + 1 combined from the discard pile; the target is your own face-up Personality in a province (others rejected); Destined is granted for the turn and his recruit draws a card; Limited (Action phase, the active player)", TwoSteadyBreathsEffect),
            ("Knowledge and Power effect: destroys one or two tokens (named on CardIds, default his first) and gives as many +1F tokens; any Personality with a token is a legal target, none without; invalid selections are rejected", KnowledgeAndPowerEffect),
            ("Knowledge and Power: Champion of Thunder's 'negate the action's effects' reaches it -- the tokens are neither destroyed nor created", KnowledgeAndPowerNegated),
            ("Expensive Achievement: Battle only, presence required; +2 or -2 to the CURRENT battlefield's province (the Defender's, whoever acts), the two halves share one use; from the discard pile it costs its 2 Gold Discipline cost and is removed", ExpensiveAchievementEffect),
            ("Lost in Transit has two modes, each with its own presence rule: as an Open action in the Action phase no presence and no battlefield target; as a Battle action presence and a target at the battlefield -- in hand and from the discard pile", LostInTransitTwoModes),
            ("False Ring of Air: an Absent Battle action whose cost is bowing the Ring -- bowed before the effect, a bowed Ring cannot pay it, and it has no hand play (nothing to bow)", FalseRingBowIsACost),
            ("Atomic costs (#356) with Discipline: a NEGATED Discipline play still pays the Discipline cost first and then the action's own cost and is removed from the game (no effect); from hand only the own cost; False Ring of Air's declared bow is paid when negated and, when blocked, aborts the whole action", NegatedPlaysPayAtomically),
        ];
        runner.Run("discipline-rulings", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null, bool? faceUp = null)
    {
        var c = new CardInstance { Id = $"dr-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = faceUp ?? zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    private static MatchState ActionTable()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        return s;
    }

    private static MatchState BattleTable(int active = 0, int priority = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.ActivePlayerId = active; s.PriorityPlayerId = priority;
        return s;
    }

    private static string[] Producers(MatchState s, int owner = 0) =>
        s.Cards.Values.Where(c => c.OwnerId == owner && !c.Bowed && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    // Leaves exactly `keep` unbowed Gold sources (by index into the producer list, largest producers first).
    private static void BowAllButProducers(MatchState s, int keep)
    {
        var producers = Producers(s).OrderByDescending(id => CardCatalog.Get(s.Cards[id].DefinitionId).GoldProduction).ThenBy(id => id, StringComparer.Ordinal).ToArray();
        foreach (var id in producers.Skip(keep)) s.Cards[id].Bowed = true;
    }

    private static GameCommand Use(MatchEngine e, CardInstance card, string ability, string? target = null, string[]? sources = null, string[]? cardIds = null, int player = 0) =>
        new("useAbility", player, e.Version, CardId: card.Id, TargetId: target, AbilityId: ability, SourceIds: sources, CardIds: cardIds);

    // The option for ONE of the card's actions (a card can carry several: Expensive Achievement's two halves, a Ring's recruit and its action).
    private static ActionOption? Offer(MatchEngine e, CardInstance card, string ability, int player = 0) =>
        e.QueryActions(player).SingleOrDefault(a => a.Type == "useAbility" && a.CardId == card.Id && a.Label.StartsWith(AbilityCatalog.All[ability].Label));

    private static string[] Payments(MatchEngine e) => e.State.Events.Where(x => x.Type == "payment").Select(x => x.Message).ToArray();
    private static void AddToken(CardInstance c, string id, string type = "Fire") => c.Tokens.Add(new CardToken(id, type, "test", "Force", 1));

    // ---- published shape --------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        Eq(PublishedContent.StrategyWave23.Cards.Length, 3);
        foreach (var (id, name, focus, discipline) in new[] { (ExpensiveAchievement, "Expensive Achievement", 3, 2), (KnowledgeAndPower, "Knowledge and Power", 2, 1), (TwoSteadyBreaths, "Two Steady Breaths", 4, 2) })
        {
            var c = CardCatalog.Get(id);
            Check(!c.ScenarioOnly && c.Type == CardType.Strategy, id);
            Eq(c.Name, name); Eq(c.Focus, focus, name + " Focus"); Eq(c.GoldCost, discipline, name + ": the published GoldCost is the DISCIPLINE cost");
            Check(c.Text.StartsWith("Discipline :g"), c.Text);
            Check(c.Abilities!.All(a => AbilityCatalog.All[a].Discipline), name + ": every action is on the Discipline flag");
        }
        Eq(string.Join(",", CardCatalog.Get(ExpensiveAchievement).Abilities!), "expensive-achievement-raise,expensive-achievement-lower");
        Eq(AbilityCatalog.All[Kp].Timing, "Limited"); Eq(AbilityCatalog.All[Tsb].Timing, "Limited");
        Eq(AbilityCatalog.All[Kp].Cost?.Amount, (int?)1); Eq(AbilityCatalog.All[Tsb].Cost?.Amount, (int?)1);
        Check(AbilityCatalog.All["expensive-achievement-raise"].Cost is null, "Expensive Achievement's action has no cost of its own");
        // Mercantile Conflict waited for an Honor-loss-increase primitive; it is published in strategy-wave-110 (issue #622, TfStrategiesB.cs) with the Discipline amount as its Gold Cost.
        Check(CardCatalog.Cards.ContainsKey(MercantileConflict) && !CardCatalog.Get(MercantileConflict).ScenarioOnly && CardCatalog.Get(MercantileConflict).GoldCost == 1 && AbilityCatalog.All["mercantile-conflict-bow"].Discipline, "Mercantile Conflict is published with its Discipline cost (strategy-wave-110)");
    }

    // ---- the additive cost -------------------------------------------------------------------------------------
    private static void TargetDiscardedInTheWindow()
    {
        foreach (var named in new[] { false, true })
        {
            var s = ActionTable();
            var kp = Add(s, KnowledgeAndPower, 0, Zone.Hand);
            var victim = Add(s, "training-lion-samurai", 1, Zone.Home);
            AddToken(victim, "tok-a");
            Add(s, "oracle-l5r-5928", 1, Zone.Hand); // Perplexing Guests: after another player's action targets his Personality, discard him
            var e = MatchEngine.FromTestState(s);
            var goldBefore = Producers(s).Count(id => !e.State.Cards[id].Bowed);
            Accept(e, named ? Use(e, kp, Kp, victim.Id, offerSources(e, kp), ["tok-a"]) : Offer(e, kp, Kp)!.Command!);
            Accept(e, e.QueryActions(1).First(a => a.Enabled && a.Command?.AbilityId == "p5-perplexing-guests-discard").Command!);
            Eq(e.State.Cards[victim.Id].Zone, Zone.DynastyDiscard, "the target was discarded in the window");
            for (var guard = 0; e.State.PendingChoice?.Type == "beforeActionReaction" && guard < 6; guard++)
                Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Enabled && a.Type == "declineReaction").Command!); // used to throw ArgumentOutOfRange / InvalidOperation here
            Check(e.State.PendingChoice is null, "the window closed and the action finished");
            Eq(e.State.Cards[victim.Id].Tokens.Count, 0, "no token was created on the discarded card");
            Check(e.State.Cards[kp.Id].Zone != Zone.Hand, "the Strategy was played: its costs stay paid");
            Check(Producers(s).Count(id => !e.State.Cards[id].Bowed) < goldBefore, "the Gold was paid");
        }
        string[] offerSources(MatchEngine e, CardInstance card) => Offer(e, card, Kp)!.Command!.SourceIds!;
    }

    private static void AdditiveQuoteAndPayment()
    {
        MatchEngine Table(out CardInstance kp, out CardInstance target)
        {
            var s = ActionTable();
            kp = Add(s, KnowledgeAndPower, 0, Zone.FateDiscard);
            target = Add(s, "training-lion-samurai", 1, Zone.Home);
            AddToken(target, "tok-a");
            return MatchEngine.FromTestState(s);
        }
        var e = Table(out var card, out var victim);
        var option = Offer(e, card, Kp)!;
        Check(option.Enabled, option.Reason ?? "");
        Eq(option.Gold?.Kind, GoldCostKind.Fixed); Eq(option.Gold!.Cost, 2, "Discipline 1 + the action's own 1");
        Check(option.Gold.Basis.Contains("Discipline cost 1 (paid first)") && option.Gold.Basis.Contains("= 2 in all"), option.Gold.Basis);
        Check(option.Gold.Payments.Length > 0 && option.Gold.Payments.All(p => p.SourceIds.Length >= 2 && p.Cost == 2), "every suggested payment is two parts, from two different sources");
        Check(option.Label.Contains("Discipline (removed from the game)"), option.Label);
        Check(option.Command!.SourceIds!.SequenceEqual(option.Gold.Payments[0].SourceIds), "the command carries the first suggestion");
        Accept(e, option.Command);
        var paid = Payments(e);
        Eq(paid.Length, 2, "two payments, not one");
        Check(paid[0].Contains("paid 1 Gold") && paid[0].Contains("Discipline cost, paid first"), "the Discipline cost is paid first: " + paid[0]);
        Check(paid[1].Contains("paid 1 Gold") && paid[1].Contains("the action's own cost"), "then the action's own: " + paid[1]);
        Check(option.Command.SourceIds!.All(id => e.State.Cards[id].Bowed), "exactly the offered sources bowed");
        Eq(e.State.Cards[card.Id].Zone, Zone.RemovedFromGame, "removed from the game after the action resolved");
        Eq(e.State.Cards[victim.Id].Tokens.Count, 1); Eq(e.State.Cards[victim.Id].Tokens[0].Type, "Force", "the destroyed token was replaced by a +1F token");
        // The order the sources are listed in is irrelevant: the split is a property of the set.
        var e2 = Table(out var card2, out var victim2);
        var reversed = Offer(e2, card2, Kp)!.Command!;
        Accept(e2, reversed with { SourceIds = reversed.SourceIds!.Reverse().ToArray() });
        Eq(e2.State.Cards[card2.Id].Zone, Zone.RemovedFromGame);
    }

    private static void BothPartsMustBePayable()
    {
        // ONE unbowed source producing 3: it covers the total (2) but not both payments -- Gold does not carry over.
        MatchEngine One(Zone zone, out CardInstance kp, out CardInstance target)
        {
            var s = ActionTable();
            kp = Add(s, KnowledgeAndPower, 0, zone);
            target = Add(s, "training-lion-samurai", 1, Zone.Home);
            AddToken(target, "tok-a");
            BowAllButProducers(s, 1);
            return MatchEngine.FromTestState(s);
        }
        var e = One(Zone.FateDiscard, out var card, out var victim);
        var only = Producers(e.State).Single();
        Check(e.GoldProduced(only, 0) >= 2, "precondition: the one source alone would cover the total of 2");
        var option = Offer(e, card, Kp)!;
        Check(!option.Enabled && option.Command is null, "the discard play is not offered when both parts cannot be paid");
        Reject(e, Use(e, card, Kp, victim.Id, [only]));
        var refusal = e.Execute(Use(e, card, Kp, victim.Id, [only]));
        Check(!refusal.Accepted && refusal.Error!.Contains("Discipline cost (1 Gold) paid first"), refusal.Error ?? "");
        Reject(e, Use(e, card, Kp, victim.Id));
        // The same one source pays the hand play (only the action's own 1).
        var hand = One(Zone.Hand, out var handCard, out var handVictim);
        var handOffer = Offer(hand, handCard, Kp)!;
        Check(handOffer.Enabled, handOffer.Reason ?? ""); Eq(handOffer.Gold!.Cost, 1);
        Accept(hand, handOffer.Command!);

        // Two sources but the SECOND part short: a Discipline cost of 2 (Two Steady Breaths) leaves nothing for the action's own 1.
        var s2 = ActionTable();
        var tsb = Add(s2, TwoSteadyBreaths, 0, Zone.FateDiscard);
        var province = Add(s2, "training-crane-samurai", 0, Zone.Province, province: 0);
        s2.Players[0].Provinces[0].CardId = province.Id;
        BowAllButProducers(s2, 1);
        var e2 = MatchEngine.FromTestState(s2);
        var single = Producers(e2.State).Single();
        Check(!Offer(e2, tsb, Tsb)!.Enabled, "3 needed as 2 then 1: one source is not enough");
        Reject(e2, Use(e2, tsb, Tsb, province.Id, [single]));
    }

    private static void HandPlayPaysOnlyTheOwnCost()
    {
        var s = ActionTable();
        var kp = Add(s, KnowledgeAndPower, 0, Zone.Hand);
        var victim = Add(s, "training-lion-samurai", 1, Zone.Home);
        AddToken(victim, "tok-a");
        var tsb = Add(s, TwoSteadyBreaths, 0, Zone.Hand);
        var province = Add(s, "training-crane-samurai", 0, Zone.Province, province: 0);
        s.Players[0].Provinces[0].CardId = province.Id;
        var e = MatchEngine.FromTestState(s);
        foreach (var (card, ability, target, expected) in new[] { (kp, Kp, victim.Id, 1), (tsb, Tsb, province.Id, 1) })
        {
            var option = Offer(e, card, ability)!;
            Check(option.Enabled, option.Reason ?? "");
            Eq(option.Gold!.Cost, expected, "only the action's own cost from hand");
            Check(!option.Gold.Basis.Contains("Discipline") && !option.Label.Contains("Discipline (removed"), option.Gold.Basis);
            Reject(e, Use(e, card, ability, target));   // the own cost is due
        }
        Accept(e, Offer(e, kp, Kp)!.Command!);
        var paid = Payments(e);
        Eq(paid.Length, 1); Check(paid[0].Contains("paid 1 Gold") && !paid[0].Contains("Discipline"), paid[0]);
        Eq(e.State.Cards[kp.Id].Zone, Zone.FateDiscard, "an ordinary Strategy play: discarded, not removed from the game");
    }

    // ---- Two Steady Breaths ------------------------------------------------------------------------------------
    private static void TwoSteadyBreathsEffect()
    {
        var s = ActionTable();
        var tsb = Add(s, TwoSteadyBreaths, 0, Zone.FateDiscard);
        var recruit = Add(s, "training-crane-samurai", 0, Zone.Province, province: 1);
        s.Players[0].Provinces[1].CardId = recruit.Id;
        var faceDown = Add(s, "training-crane-veteran", 0, Zone.Province, province: 2, faceUp: false);
        s.Players[0].Provinces[2].CardId = faceDown.Id;
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Province, province: 1);
        s.Players[1].Provinces[1].CardId = enemy.Id; enemy.FaceUp = true;
        var home = Add(s, "training-crane-courtier", 0, Zone.Home);
        var control = MatchEngine.FromTestState(s); // the same table, before the play
        var e = MatchEngine.FromTestState(s);
        var option = Offer(e, tsb, Tsb)!;
        Check(option.Enabled, option.Reason ?? "");
        Eq(option.Gold!.Cost, 3, "Discipline 2, then the action's own 1"); Check(option.Gold.Payments.All(p => p.SourceIds.Length >= 2));
        Check(option.TargetIds!.Contains(recruit.Id), "your face-up Personality in a province is offered");
        // Every province Personality of the province row is checked at its own province card: the ones that are not legal are rejected.
        foreach (var bad in new[] { faceDown, enemy, home })
            Reject(e, option.Command! with { TargetId = bad.Id });
        Accept(e, option.Command! with { TargetId = recruit.Id });
        var granted = e.State.Cards[recruit.Id].GrantedKeywords.Single(g => g.Keyword == "Destined");
        Eq(granted.Duration, "Turn", "given for the turn: it spans this turn's Dynasty phase");
        Eq(e.State.Cards[tsb.Id].Zone, Zone.RemovedFromGame);
        Eq(Payments(e).Length, 2);
        // His recruit this turn draws a card (Destined's own entry effect); the same recruit without the grant does not.
        int HandGainOnPurchase(MatchState state)
        {
            state.Phase = Phase.Dynasty; state.PriorityPlayerId = 0; state.ActivePlayerId = 0;
            foreach (var c in state.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = false; // a fresh Gold supply
            var buyer = MatchEngine.FromTestState(state);
            var handBefore = buyer.State.Players[0].Hand.Count;
            var quote = buyer.QuotePurchase(0, recruit.Id);
            Check(quote.Error is null, quote.Error ?? "");
            Accept(buyer, new GameCommand("purchase", 0, buyer.Version, CardId: recruit.Id, SourceIds: quote.Payments[0].SourceIds));
            while (buyer.State.PendingChoice?.Type == "proclaim") Accept(buyer, new GameCommand("declineReaction", buyer.State.PriorityPlayerId, buyer.Version));
            return buyer.State.Players[0].Hand.Count - handBefore;
        }
        Eq(HandGainOnPurchase(e.State), 1, "Destined: a card is drawn after he enters play");
        Eq(HandGainOnPurchase(control.State), 0, "and nothing without the grant");

        // Limited: the active player's Action phase only.
        var battle = BattleTable();
        var b = Add(battle, TwoSteadyBreaths, 0, Zone.Hand);
        var bp = Add(battle, "training-crane-samurai", 0, Zone.Province, province: 1); battle.Players[0].Provinces[1].CardId = bp.Id;
        var be = MatchEngine.FromTestState(battle);
        Reject(be, Use(be, b, Tsb, bp.Id, TwoGold(be)));
        var notActive = ActionTable(); notActive.ActivePlayerId = 1; notActive.PriorityPlayerId = 0;
        var n = Add(notActive, TwoSteadyBreaths, 0, Zone.Hand);
        var np = Add(notActive, "training-crane-samurai", 0, Zone.Province, province: 1); notActive.Players[0].Provinces[1].CardId = np.Id;
        var ne = MatchEngine.FromTestState(notActive);
        Reject(ne, Use(ne, n, Tsb, np.Id, TwoGold(ne)));
    }

    private static string[] TwoGold(MatchEngine e) => e.PaymentOptions(0, 1).FirstOrDefault()?.SourceIds ?? [];

    // ---- Knowledge and Power ----------------------------------------------------------------------------------
    private static void KnowledgeAndPowerEffect()
    {
        // Default: one token (his first), replaced by one +1F token; either side's Personality is a legal target.
        foreach (var owner in new[] { 0, 1 })
        {
            var s = ActionTable();
            var kp = Add(s, KnowledgeAndPower, 0, Zone.Hand);
            var target = Add(s, "training-lion-samurai", owner, Zone.Home);
            AddToken(target, "tok-a"); AddToken(target, "tok-b", "Sake");
            var e = MatchEngine.FromTestState(s);
            var option = Offer(e, kp, Kp)!;
            Check(option.Enabled && option.TargetIds!.Contains(target.Id), option.Reason ?? "");
            Accept(e, option.Command! with { TargetId = target.Id });
            var tokens = e.State.Cards[target.Id].Tokens;
            Eq(tokens.Count, 2); Check(!tokens.Any(t => t.Id == "tok-a"), "his first token was destroyed"); Check(tokens.Any(t => t.Id == "tok-b"));
            Eq(tokens.Count(t => t.Type == "Force" && t.Stat == "Force" && t.Amount == 1), 1, "and he gained one +1F token");
        }
        // Two named tokens: both destroyed, two +1F tokens given (the net Force change is zero for +1F tokens, the count is what is asked).
        {
            var s = ActionTable();
            var kp = Add(s, KnowledgeAndPower, 0, Zone.Hand);
            var target = Add(s, "training-lion-samurai", 1, Zone.Home);
            AddToken(target, "tok-a"); AddToken(target, "tok-b"); AddToken(target, "tok-c");
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, kp, Kp, target.Id, TwoGold(e), ["tok-b", "tok-c"]));
            var tokens = e.State.Cards[target.Id].Tokens;
            Eq(tokens.Count, 3); Check(tokens.Any(t => t.Id == "tok-a") && !tokens.Any(t => t.Id is "tok-b" or "tok-c"), "exactly the named tokens were destroyed");
            Eq(tokens.Count(t => t.Type == "Force"), 2, "as many +1F tokens as were destroyed");
        }
        // Rejected: three tokens, a token that is not his, the same token twice, a target with no tokens, a card that is not a Personality.
        {
            var s = ActionTable();
            var kp = Add(s, KnowledgeAndPower, 0, Zone.Hand);
            var target = Add(s, "training-lion-samurai", 1, Zone.Home);
            AddToken(target, "tok-a"); AddToken(target, "tok-b"); AddToken(target, "tok-c");
            var other = Add(s, "training-crane-samurai", 0, Zone.Home); AddToken(other, "tok-x");
            var bare = Add(s, "training-lion-veteran", 1, Zone.Home);
            var holding = Add(s, "training-marketplace", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var pay = TwoGold(e);
            Reject(e, Use(e, kp, Kp, target.Id, pay, ["tok-a", "tok-b", "tok-c"]));
            Reject(e, Use(e, kp, Kp, target.Id, pay, ["tok-x"]));
            Reject(e, Use(e, kp, Kp, target.Id, pay, ["tok-a", "tok-a"]));
            Reject(e, Use(e, kp, Kp, bare.Id, pay));
            Reject(e, Use(e, kp, Kp, holding.Id, pay));
            var offered = Offer(e, kp, Kp)!.TargetIds!;
            Check(offered.Contains(target.Id) && offered.Contains(other.Id) && !offered.Contains(bare.Id) && !offered.Contains(holding.Id), "only Personalities with a token are offered");
        }
    }

    private static void KnowledgeAndPowerNegated()
    {
        var s = ActionTable();
        var kp = Add(s, KnowledgeAndPower, 0, Zone.Hand);
        var host = Add(s, "training-crane-samurai", 1, Zone.Home);
        AddToken(host, "tok-a");
        var champion = Add(s, "oracle-l5r-1292", 1, Zone.Attached, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, kp, Kp, host.Id, TwoGold(e)));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "an action targeting a card in Champion of Thunder's unit opens its window");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: champion.Id, TargetId: host.Id, AbilityId: "champion-of-thunder-negate"));
        Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        var tokens = e.State.Cards[host.Id].Tokens;
        Check(tokens.Count == 1 && tokens[0].Id == "tok-a", "negated: nothing destroyed, nothing created");
        Check(e.State.Cards[kp.Id].Zone == Zone.FateDiscard && Payments(e).Length == 1, "the card is still spent and its cost paid");
    }

    // ---- Expensive Achievement ---------------------------------------------------------------------------------
    private static void ExpensiveAchievementEffect()
    {
        // The Attacker (player 0) plays it: "this Province" is the Defender's, the current battlefield's.
        {
            var s = BattleTable();
            var ea = Add(s, ExpensiveAchievement, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var before = (s.Players[0].Provinces[0].Strength, s.Players[1].Provinces[0].Strength);
            var e = MatchEngine.FromTestState(s);
            var option = Offer(e, ea, "expensive-achievement-raise")!;
            Check(option.Enabled && option.Gold is null, option.Reason ?? "free from hand");
            Accept(e, Use(e, ea, "expensive-achievement-raise"));
            Eq(e.ProvinceStrengthForTest(1, 0), before.Item2 + 2, "the Defender's current-battlefield province +2");
            Eq(Batch336Support.StrengthNextTurn(e, 1, 0), before.Item2, "Expensive Achievement: until end of turn (issue #492 ruling R3)");
            Eq(e.ProvinceStrengthForTest(0, 0), before.Item1, "not the Attacker's own province of the same index");
            Eq(e.State.Cards[ea.Id].Zone, Zone.FateDiscard);
            Reject(e, Use(e, ea, "expensive-achievement-lower")); // one use: the two halves share it (and it left the hand)
        }
        {
            var s = BattleTable();
            var ea = Add(s, ExpensiveAchievement, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var before = e.ProvinceStrengthForTest(1, 0);
            Accept(e, Use(e, ea, "expensive-achievement-lower"));
            Eq(e.ProvinceStrengthForTest(1, 0), before - 2);
        }
        // The Defender plays it: their OWN province is the one at the current battlefield.
        {
            var s = BattleTable(active: 1, priority: 0);
            var ea = Add(s, ExpensiveAchievement, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var before = (e.ProvinceStrengthForTest(0, 0), e.ProvinceStrengthForTest(1, 0));
            Accept(e, Use(e, ea, "expensive-achievement-raise"));
            Eq(e.ProvinceStrengthForTest(0, 0), before.Item1 + 2); Eq(e.ProvinceStrengthForTest(1, 0), before.Item2);
        }
        // A plain Battle action: no presence, or the Action phase, is rejected (hand and discard pile alike).
        foreach (var zone in new[] { Zone.Hand, Zone.FateDiscard })
        {
            var noPresence = BattleTable();
            var a = Add(noPresence, ExpensiveAchievement, 0, zone);
            var ne = MatchEngine.FromTestState(noPresence);
            Reject(ne, Use(ne, a, "expensive-achievement-raise", sources: zone == Zone.Hand ? null : ne.PaymentOptions(0, 2).First().SourceIds));
            var action = ActionTable();
            var b = Add(action, ExpensiveAchievement, 0, zone);
            var ae = MatchEngine.FromTestState(action);
            Reject(ae, Use(ae, b, "expensive-achievement-raise", sources: zone == Zone.Hand ? null : ae.PaymentOptions(0, 2).First().SourceIds));
        }
        // From the discard pile: the 2 Gold Discipline cost (the action has no cost of its own), then removed from the game.
        {
            var s = BattleTable();
            var ea = Add(s, ExpensiveAchievement, 0, Zone.FateDiscard);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var option = Offer(e, ea, "expensive-achievement-raise")!;
            Check(option.Enabled, option.Reason ?? ""); Eq(option.Gold!.Cost, 2);
            Check(option.Gold.Basis.Contains("Discipline cost 2"), option.Gold.Basis);
            Reject(e, Use(e, ea, "expensive-achievement-raise"));
            var before = e.ProvinceStrengthForTest(1, 0);
            Accept(e, option.Command!);
            Eq(Payments(e).Length, 1); Eq(e.ProvinceStrengthForTest(1, 0), before + 2);
            Eq(e.State.Cards[ea.Id].Zone, Zone.RemovedFromGame);
        }
    }

    // ---- Lost in Transit: two modes ------------------------------------------------------------------------------
    private static void LostInTransitTwoModes()
    {
        // (zone, phase, acting player's own unit at the battlefield, host and attachment at the battlefield) -> accepted?
        bool Play(Zone zone, Phase phase, bool presence, bool targetAtBattlefield)
        {
            var s = phase == Phase.Battle ? BattleTable() : ActionTable();
            var card = Add(s, LostInTransit, 0, zone);
            var host = targetAtBattlefield ? Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0) : Add(s, "training-lion-samurai", 1, Zone.Home);
            var attachment = Add(s, "training-item", 1, Zone.Attached, province: targetAtBattlefield ? 0 : null, parent: host.Id);
            if (presence && phase == Phase.Battle) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var sources = zone == Zone.FateDiscard ? e.PaymentOptions(0, 1).First().SourceIds : null;
            var result = e.Execute(Use(e, card, Lit, attachment.Id, sources));
            if (result.Accepted)
            {
                Check(e.State.Cards[attachment.Id].Bowed, "Bow a target attachment");
                Eq(e.State.Cards[card.Id].Zone, zone == Zone.FateDiscard ? Zone.RemovedFromGame : Zone.FateDiscard);
            }
            return result.Accepted;
        }
        foreach (var zone in new[] { Zone.Hand, Zone.FateDiscard })
        {
            // Open (Action phase): no presence anywhere, and the attachment need not be at any battlefield.
            Check(Play(zone, Phase.Action, presence: false, targetAtBattlefield: false), $"{zone}: playable as an Open action with no presence");
            // Battle: presence is required...
            Check(!Play(zone, Phase.Battle, presence: false, targetAtBattlefield: true), $"{zone}: a Battle action needs the acting player's presence");
            // ...and the target attachment must be at the battlefield...
            Check(!Play(zone, Phase.Battle, presence: true, targetAtBattlefield: false), $"{zone}: a Battle action's target must be at the battlefield");
            // ...and with both it works.
            Check(Play(zone, Phase.Battle, presence: true, targetAtBattlefield: true), $"{zone}: a Battle action with presence and a target there");
        }
        // The offer follows the phase the same way (the Open option is not gated by a battle that is not happening).
        var s2 = ActionTable();
        var lit = Add(s2, LostInTransit, 0, Zone.Hand);
        var host2 = Add(s2, "training-lion-samurai", 1, Zone.Home); Add(s2, "training-item", 1, Zone.Attached, parent: host2.Id);
        Check(Offer(MatchEngine.FromTestState(s2), lit, Lit)!.Enabled, "offered enabled in the Action phase");
        var s3 = BattleTable();
        var lit3 = Add(s3, LostInTransit, 0, Zone.Hand);
        var host3 = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s3, "training-item", 1, Zone.Attached, province: 0, parent: host3.Id);
        Check(!Offer(MatchEngine.FromTestState(s3), lit3, Lit)!.Enabled, "offered disabled in a battle where the acting player has no presence");
    }

    // ---- False Ring of Air --------------------------------------------------------------------------------------
    private static void FalseRingBowIsACost()
    {
        MatchEngine Table(Zone ringZone, bool ringBowed, out CardInstance ring, out CardInstance enemy)
        {
            var s = BattleTable();
            ring = Add(s, FalseRingOfAir, 0, ringZone, bowed: ringBowed);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        // In play and unbowed, with NO unit of the acting player at the battlefield (Absent): bows the Ring, gives -3F.
        var e = Table(Zone.Home, false, out var ring, out var target);
        var offer = Offer(e, ring, Frair)!;
        Check(offer.Enabled, offer.Reason ?? "");
        Accept(e, Use(e, ring, Frair, target.Id));
        Check(e.State.Cards[ring.Id].Bowed, "bowing the Ring is the cost");
        Eq(e.GetView(1).Players[1].Army.Single(c => c.Id == target.Id).Force, 1, "4 - 3");
        // A bowed Ring cannot pay the cost.
        var bowed = Table(Zone.Home, true, out var bowedRing, out var t2);
        Reject(bowed, Use(bowed, bowedRing, Frair, t2.Id));
        Check(Offer(bowed, bowedRing, Frair) is null or { Enabled: false });
        // A False Ring prints no "discard from hand" alternative: from hand there is nothing to bow, so it has no hand play (its recruit action is separate).
        var hand = Table(Zone.Hand, false, out var handRing, out var t3);
        Reject(hand, Use(hand, handRing, Frair, t3.Id));
        Check(Offer(hand, handRing, Frair) is null or { Enabled: false }, "not offered as a usable action from hand");
        Eq(hand.State.Cards[handRing.Id].Zone, Zone.Hand);
    }

    // ---- atomic cost payment (#356) ---------------------------------------------------------------------------------
    private static void NegatedPlaysPayAtomically()
    {
        // A whole-action negation (the test seam): every announced action is negated, its costs are still paid, only its effects are skipped.
        foreach (var zone in new[] { Zone.FateDiscard, Zone.Hand })
        {
            var s = ActionTable();
            var kp = Add(s, KnowledgeAndPower, 0, zone);
            var victim = Add(s, "training-lion-samurai", 1, Zone.Home); AddToken(victim, "tok-a");
            var e = MatchEngine.FromTestState(s);
            var asked = 0; e.CostBlockHook = (_, _) => { asked++; return false; };
            e.NegateEveryAction = true;
            var option = Offer(e, kp, Kp)!;
            Check(option.Enabled, option.Reason ?? "");
            Accept(e, option.Command!);
            var paid = Payments(e);
            if (zone == Zone.FateDiscard)
            {
                Eq(paid.Length, 2, "a negated Discipline play pays BOTH parts");
                Check(paid[0].Contains("Discipline cost, paid first") && paid[1].Contains("the action's own cost"), "Discipline first: " + string.Join(" | ", paid));
                Eq(e.State.Cards[kp.Id].Zone, Zone.RemovedFromGame, "and the card is removed from the game as before");
            }
            else
            {
                Eq(paid.Length, 1, "from hand only the action's own cost");
                Check(!paid[0].Contains("Discipline"), paid[0]);
                Eq(e.State.Cards[kp.Id].Zone, Zone.FateDiscard);
            }
            Check(e.State.Cards[victim.Id].Tokens.Count == 1 && e.State.Cards[victim.Id].Tokens[0].Id == "tok-a", "no effect: the token is untouched");
            Eq(asked, 0, "the action declares no bow or destroy cost, so there is nothing a negation could block");
        }
        // False Ring of Air's bow is a DECLARED cost: paid when the action is negated (its effect is not)...
        MatchEngine Ring(out CardInstance ring, out CardInstance enemy)
        {
            var s = BattleTable();
            ring = Add(s, FalseRingOfAir, 0, Zone.Home);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var negated = Ring(out var ringA, out var enemyA);
        negated.NegateEveryAction = true;
        Accept(negated, Use(negated, ringA, Frair, enemyA.Id));
        Check(negated.State.Cards[ringA.Id].Bowed, "a negated action still pays its cost: the Ring is bowed");
        Eq(negated.GetView(1).Players[1].Army.Single(c => c.Id == enemyA.Id).Force, 4, "but the -3F never happened");
        // ...and when paying it is blocked, the ENTIRE action is negated and nothing is paid.
        var blocked = Ring(out var ringB, out var enemyB);
        blocked.CostBlockHook = (cost, subject) => cost.Verb == CostVerb.Bow && subject?.Id == ringB.Id;
        Accept(blocked, Use(blocked, ringB, Frair, enemyB.Id));
        Check(!blocked.State.Cards[ringB.Id].Bowed, "a blocked bow: nothing is paid, the Ring stays unbowed");
        Eq(blocked.GetView(1).Players[1].Army.Single(c => c.Id == enemyB.Id).Force, 4, "and nothing resolves");
        Check(blocked.State.Cards[ringB.Id].AbilityUses.ContainsKey(Frair), "the action was still taken: its use is spent");
        // The ordinary (not negated) flow pays it once, before the effect.
        var plain = Ring(out var ringC, out var enemyC);
        Accept(plain, Use(plain, ringC, Frair, enemyC.Id));
        Check(plain.State.Cards[ringC.Id].Bowed); Eq(plain.GetView(1).Players[1].Army.Single(c => c.Id == enemyC.Id).Force, 1);
    }
}
