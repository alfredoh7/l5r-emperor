using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #416 (stage 2 of Mendon's rulings of 2026-09-21): the Gold cost read from a printed number of a card (GoldCostRule.StatValue), A Quiet Transaction built on it, Holdings whose
// production depends on what they pay for (Red Crane Dojo, Bushi Dojo), and the temporary removal of keywords and Clan alignments "until the action ends" (Red Crane Dojo's Reaction).
internal static class GoldStatCostTests
{
    private const string QuietTransaction = "oracle-l5r-70", RedCrane = "oracle-l5r-6219", BushiDojo = "oracle-l5r-1190", DispensingJustice = "oracle-l5r-2029";

    public static void Run(TestRunner runner) => runner.Run("gold-stat-cost", [
        ("Published shape: A Quiet Transaction, Red Crane Dojo and Bushi Dojo are the printed cards with their abilities, conditional-production Effects and art", PublishedShapes),
        ("The stat rule reads Force, Chi, Gold Cost, Personal Honor, Focus Value and Province Strength, printed (base) or current, and a modifier and floor apply on top", StatValues),
        ("A Gold cost from a stat is quoted per target and per performer, priced from the current or the base value, paid through the ordinary Gold pathway, and refused when the sources cannot cover it", StatCostQuotes),
        ("A Quiet Transaction: pay his BASE Personal Honor plus one for a dishonorable Personality, who then may not be assigned or move to battlefields this turn; the second Open action gives him two -2 Gold Cost tokens", QuietTransactionFlow),
        ("Conditional production: Red Crane Dojo produces 4 instead of 3 when paying for a Ninja card (recruited, equipped or played, never an action), Bushi Dojo 6 when paying for a Follower; both only for that payment", ConditionalProduction),
        ("A Ninja recruit is payable by Red Crane Dojo alone and a non-Ninja of the same cost is not; a Follower is payable by Bushi Dojo alone and an Item is not -- for the quote, validation and the payment", PurchasesAndEquips),
        ("Red Crane Dojo's Reaction: after an action targets its controller's Personality, Ninja and Shadowlands are removed from him until the action ends -- even while the Holding is bowed -- and given back after", RedCraneReaction),
        ("A removal that was never given back (the action did not finish) is given back at the end of the turn, and only what was removed is returned", RemovalsEndWithTheTurn),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardDefinition Def(CardInstance card) => CardCatalog.Get(card.DefinitionId);

    // Bows every Gold producer of a player except the named ones, so a test states exactly what can pay.
    private static void OnlyProducers(MatchState s, int player, params CardInstance[] keep)
    {
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == player && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0 && !keep.Contains(c))) c.Bowed = true;
    }

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        var qt = CardCatalog.Get(QuietTransaction); var rc = CardCatalog.Get(RedCrane); var bd = CardCatalog.Get(BushiDojo);
        Check(qt.Name == "A Quiet Transaction" && qt.Type == CardType.Strategy && qt.Focus == 1 && qt.GoldCost == 0 && qt.Abilities!.SequenceEqual(new[] { "quiet-transaction-lock", "quiet-transaction-tokens" }), "A Quiet Transaction");
        Check(rc.Name == "Red Crane Dojo" && rc.Type == CardType.Holding && rc.GoldCost == 4 && rc.GoldProduction == 3 && rc.Keywords!.Contains("Ninja") && rc.Keywords.Contains("Dojo")
            && rc.Effect == "red-crane-dojo-gold" && rc.Abilities!.SequenceEqual(new[] { "red-crane-dojo-ninja" }), "Red Crane Dojo");
        Check(bd.Name == "Bushi Dojo" && bd.Type == CardType.Holding && bd.GoldCost == 3 && bd.GoldProduction == 0 && bd.Effect == "bushi-dojo-gold" && bd.Abilities!.Length == 0, "Bushi Dojo");
        foreach (var id in new[] { QuietTransaction, RedCrane, BushiDojo })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        var rule = AbilityCatalog.All["quiet-transaction-lock"].Cost!;
        Check(rule.Basis == GoldCostBasis.Stat && rule.StatOf == CostStatSubject.Target && rule.Stat == CostStat.PersonalHonor && rule.StatBase && rule.Modifier == 1 && rule.Kind == GoldCostKind.Derived, "the rule on A Quiet Transaction");
    }

    private static void StatValues()
    {
        var s = Table(Phase.Action, 0);
        var person = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var def = Def(person);
        // Untouched: base and current agree.
        foreach (var stat in new[] { CostStat.Force, CostStat.Chi, CostStat.GoldCost, CostStat.PersonalHonor, CostStat.Focus })
            Eq(e.CostStatValue(e.State.Cards[person.Id], stat, true), e.CostStatValue(e.State.Cards[person.Id], stat, false), $"{stat}: base equals current before anything changes it");
        Eq(e.CostStatValue(e.State.Cards[person.Id], CostStat.Chi, true), def.Chi); Eq(e.CostStatValue(e.State.Cards[person.Id], CostStat.PersonalHonor, true), def.PersonalHonor);
        Eq(e.CostStatValue(e.State.Cards[person.Id], CostStat.GoldCost, true), def.GoldCost); Eq(e.CostStatValue(e.State.Cards[person.Id], CostStat.Force, true), def.Force);
        // Modified: the base ignores modifiers and tokens, the current counts them.
        person.Modifiers.Add(new("test", "test", "Force", 3)); person.Modifiers.Add(new("test", "test", "Chi", 2)); person.Modifiers.Add(new("test", "test", "GoldCost", -1));
        person.Modifiers.Add(new("test", "test", "Focus", 2));
        var e2 = MatchEngine.FromTestState(s);
        var p2 = e2.State.Cards[person.Id];
        Eq(e2.CostStatValue(p2, CostStat.Force, false), def.Force + 3, "current Force"); Eq(e2.CostStatValue(p2, CostStat.Force, true), def.Force, "base Force");
        Eq(e2.CostStatValue(p2, CostStat.Chi, false), def.Chi + 2, "current Chi"); Eq(e2.CostStatValue(p2, CostStat.Chi, true), def.Chi, "base Chi");
        Eq(e2.CostStatValue(p2, CostStat.GoldCost, false), def.GoldCost - 1, "current Gold Cost"); Eq(e2.CostStatValue(p2, CostStat.GoldCost, true), def.GoldCost, "base Gold Cost");
        Eq(e2.CostStatValue(p2, CostStat.Focus, false), def.Focus + 2, "current Focus"); Eq(e2.CostStatValue(p2, CostStat.Focus, true), def.Focus, "base Focus");
        // A dishonorable Personality's CURRENT Personal Honor is 0; the printed one is unchanged.
        person.Dishonorable = true;
        var e3 = MatchEngine.FromTestState(s);
        Eq(e3.CostStatValue(e3.State.Cards[person.Id], CostStat.PersonalHonor, false), 0, "current Personal Honor of a dishonorable Personality");
        Eq(e3.CostStatValue(e3.State.Cards[person.Id], CostStat.PersonalHonor, true), def.PersonalHonor, "base Personal Honor is the printed one");
        // Province Strength is the Stronghold's; a card without the stat reads 0.
        var stronghold = s.Cards[s.Players[0].StrongholdId];
        Check(e3.CostStatValue(e3.State.Cards[stronghold.Id], CostStat.ProvinceStrength, true) == Def(stronghold).ProvinceStrength, "Province Strength of a Stronghold");
        Eq(e3.CostStatValue(e3.State.Cards[person.Id], CostStat.ProvinceStrength, true), 0, "a Personality has none");
        // The rule's own arithmetic: modifier, floor and the never-below-zero clamp.
        var down = GoldCostRule.StatValue(CostStatSubject.Source, CostStat.Focus, true, modifier: -1, floor: 1);
        Check(down.Modifier == -1 && down.Floor == 1, "modifier and floor are on the rule");
    }

    private static void StatCostQuotes()
    {
        // A performer's CURRENT Chi plus 2: the quote follows the performer chosen and the modifiers he carries.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var buffed = Add(s, "training-lion-samurai", 0, Zone.Home); buffed.Modifiers.Add(new("test", "test", "Chi", 3));
        var card = Add(s, "training-stat-cost-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var plainOption = Enabled(e, 0, "training-stat-cost-performer", card.Id, plain.Id);
        var buffedOption = Enabled(e, 0, "training-stat-cost-performer", card.Id, buffed.Id);
        Eq(plainOption.Gold!.Cost, Def(plain).Chi + 2, "the performer's current Chi plus 2");
        Eq(buffedOption.Gold!.Cost, Def(buffed).Chi + 3 + 2, "his modifiers count: the cost is of the CURRENT Chi");
        Check(plainOption.Gold.Kind == GoldCostKind.Derived && plainOption.Gold.Basis.Contains("current Chi"), "a derived quote that says where the number is from: " + plainOption.Gold.Basis);
        Check(plainOption.Gold.Payments.Length > 0, "and is payable through the ordinary pathway");
        // Paid: the offered sources are charged and the action is taken.
        var goldBefore = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed);
        Accept(e, plainOption.Command! with { SourceIds = plainOption.Gold.Payments[0].SourceIds });
        Check(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed) > goldBefore, "the sources were bowed");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains($"paid {Def(plain).Chi + 2} Gold")), "the payment names the amount");

        // The source's own printed Focus minus 1, never below 1.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var strategy = Add(t, "training-stat-cost-strategy", 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        var focus = Enabled(f, 0, "training-stat-cost-source", strategy.Id);
        Eq(focus.Gold!.Cost, Math.Max(1, Def(strategy).Focus - 1), "the source's printed Focus Value minus 1 (at least 1)");
        Check(focus.Gold.Basis.Contains("base Focus"), "read from the printed value: " + focus.Gold.Basis);
        // The floor: a Focus Value of 1 minus 1 would be 0, but the rule says at least 1.
        var low = Add(t, "training-stat-cost-low-strategy", 0, Zone.Hand);
        var lowEngine = MatchEngine.FromTestState(t);
        Eq(Enabled(lowEngine, 0, "training-stat-cost-source", low.Id).Gold!.Cost, 1, "the floor holds the cost at 1");

        // Cannot be covered: refused, and nothing is paid.
        var u = Table(Phase.Action, 0); u.PriorityPlayerId = 0;
        foreach (var c in u.Cards.Values.Where(c => c.OwnerId == 0)) if (CardCatalog.Get(c.DefinitionId).GoldProduction > 0) c.Bowed = true;
        var rich = Add(u, "training-lion-samurai", 0, Zone.Home); rich.Modifiers.Add(new("test", "test", "Chi", 50));
        var c3 = Add(u, "training-stat-cost-strategy", 0, Zone.Hand);
        var g = MatchEngine.FromTestState(u);
        var opt = Options(g, 0, "training-stat-cost-performer", c3.Id, rich.Id).FirstOrDefault();
        Check(opt is null || !opt.Enabled, "not offered when nothing can pay a cost that high");
    }

    private static void QuietTransactionFlow()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var target = Add(s, "training-crane-veteran", 1, Zone.Home); target.Dishonorable = true;
        var target2 = Add(s, "training-lion-samurai", 1, Zone.Home); target2.Dishonorable = true;
        var honorable = Add(s, "training-crane-samurai", 1, Zone.Home);
        var card = Add(s, QuietTransaction, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "quiet-transaction-lock", card.Id);
        Check(option.TargetIds!.Contains(target.Id) && option.TargetIds.Contains(target2.Id) && !option.TargetIds.Contains(honorable.Id), "only a dishonorable Personality is a target");
        // BASE Personal Honor plus one: a dishonorable Personality's current Personal Honor is 0, so the printed number is what counts.
        var honorOf = Def(target).PersonalHonor; var honorOf2 = Def(target2).PersonalHonor;
        Eq(option.TargetGold![target.Id].Cost, honorOf + 1, "his base (printed) Personal Honor plus one");
        Eq(option.TargetGold[target2.Id].Cost, honorOf2 + 1, "each target has its own quote");
        Check(option.TargetGold[target.Id].Basis.Contains("base Personal Honor"), "the basis says it: " + option.TargetGold[target.Id].Basis);
        Accept(e, option.Command! with { TargetId = target.Id, SourceIds = option.TargetGold[target.Id].Payments[0].SourceIds });
        Check(e.State.Cards[target.Id].MovementLocks is [{ Kinds: var kinds }] && kinds == (MovementKinds.ToBattlefield | MovementKinds.Assign), "he may not be assigned or move to battlefields");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains($"paid {honorOf + 1} Gold")), "the Gold was paid");

        // The lock: assignment is refused, a move to the battlefield is negated, other cards are unaffected.
        var t = Table(Phase.InfantryDeclaration, 1); t.PriorityPlayerId = 1;
        var barred = Add(t, "training-crane-veteran", 1, Zone.Home); var free = Add(t, "training-lion-samurai", 1, Zone.Home);
        barred.MovementLocks.Add(new("A Quiet Transaction", t.TurnNumber, MovementKinds.ToBattlefield | MovementKinds.Assign));
        var f = MatchEngine.FromTestState(t);
        Reject(f, new GameCommand("attack", 1, f.Version, Assignments: new() { [barred.Id] = 0 }));
        Accept(f, new GameCommand("attack", 1, f.Version, Assignments: new() { [free.Id] = 0 }));
        var m = Table(Phase.Battle, 1); m.PriorityPlayerId = 1;
        Add(m, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var away = Add(m, "training-crane-veteran", 1, Zone.Home); var mover = Add(m, "training-move-battle-strategy", 1, Zone.Hand);
        away.MovementLocks.Add(new("A Quiet Transaction", m.TurnNumber, MovementKinds.ToBattlefield | MovementKinds.Assign));
        var g = MatchEngine.FromTestState(m);
        Accept(g, Offer(g, 1, "training-move-to-battle", mover.Id, away.Id));
        Eq(g.State.Cards[away.Id].Zone, Zone.Home, "the lock negates a move to the battlefield");

        // Not a dishonorable Personality: no target; the second half likewise.
        var u = Table(Phase.Action, 0); u.PriorityPlayerId = 0;
        var only = Add(u, "training-crane-samurai", 1, Zone.Home); Add(u, QuietTransaction, 0, Zone.Hand);
        var h = MatchEngine.FromTestState(u);
        Check(!Options(h, 0, "quiet-transaction-lock").Any(o => o.Enabled && (o.TargetIds?.Contains(only.Id) ?? false)), "an honorable Personality is not a target of the first half");
        Check(!Options(h, 0, "quiet-transaction-tokens").Any(o => o.Enabled && (o.TargetIds?.Contains(only.Id) ?? false)), "nor of the second");

        // The second half: two -2 Gold Cost tokens, free.
        var v = Table(Phase.Action, 0); v.PriorityPlayerId = 0;
        var victim = Add(v, "training-battle-maiden", 1, Zone.Home); victim.Dishonorable = true;
        var c2 = Add(v, QuietTransaction, 0, Zone.Hand);
        var w = MatchEngine.FromTestState(v);
        var cost0 = Def(victim).GoldCost;
        var tokens = Enabled(w, 0, "quiet-transaction-tokens", c2.Id);
        Check(tokens.Gold is null || tokens.Gold.Cost == 0, "it costs no Gold");
        Accept(w, tokens.Command! with { TargetId = victim.Id });
        Eq(w.State.Cards[victim.Id].Tokens.Count(x => x.Stat == "GoldCost" && x.Amount == -2), 2, "two -2 Gold Cost tokens");
        Eq(w.GetView(0).Players.SelectMany(p => p.Home).Single(c => c.Id == victim.Id).Definition!.GoldCost, cost0, "his printed cost is not changed");
        Eq(w.State.Cards[victim.Id].Tokens.Where(x => x.Stat == "GoldCost").Sum(x => x.Amount), -4, "-4 in all");
    }

    private static void ConditionalProduction()
    {
        var s = Table(Phase.Action, 0);
        var red = Add(s, RedCrane, 0, Zone.Home); var bushi = Add(s, BushiDojo, 0, Zone.Home);
        var ninja = Add(s, "training-ninja-source", 0, Zone.Home); var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Hand); var item = Add(s, "training-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        CardInstance N(string id) => e.State.Cards[id];
        int Red(PaymentSubject? p) => e.GoldProduced(red.Id, 0, p);
        int Bushi(PaymentSubject? p) => e.GoldProduced(bushi.Id, 0, p);
        Eq(Red(null), 3, "Red Crane Dojo's printed production when nothing says what it is paying for");
        Eq(Red(new PaymentSubject(N(plain.Id), PaymentKind.Recruit)), 3, "3 for a card without the keyword");
        Eq(Red(new PaymentSubject(N(ninja.Id), PaymentKind.Recruit)), 4, "4 when recruiting a Ninja card");
        Eq(Red(new PaymentSubject(N(ninja.Id), PaymentKind.Equip)), 4, "4 when equipping one");
        Eq(Red(new PaymentSubject(N(ninja.Id), PaymentKind.Play)), 4, "4 when playing one");
        Eq(Red(new PaymentSubject(N(ninja.Id), PaymentKind.Action)), 3, "3 for an action of a Ninja card in play: it is not paying FOR the card");
        Eq(Bushi(null), 0, "Bushi Dojo produces nothing by itself");
        Eq(Bushi(new PaymentSubject(N(follower.Id), PaymentKind.Equip)), 6, "6 when equipping a Follower");
        Eq(Bushi(new PaymentSubject(N(item.Id), PaymentKind.Equip)), 0, "nothing for an Item");
        Eq(Bushi(new PaymentSubject(N(follower.Id), PaymentKind.Play)), 0, "nor for a Follower that is merely played");
        var creates = new AbilityDefinition("Open", "creates a Follower", "none", Creates: new CreatedCardSpec(CardType.Follower));
        Eq(Bushi(new PaymentSubject(N(plain.Id), PaymentKind.Action, creates)), 6, "6 for an action that creates a Follower");
        var createsPersonality = new AbilityDefinition("Open", "creates a Personality", "none", Creates: new CreatedCardSpec(CardType.Personality));
        Eq(Bushi(new PaymentSubject(N(plain.Id), PaymentKind.Action, createsPersonality)), 0, "but not one that creates something else");
        // The HUD figure is the printed production.
        Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == red.Id).GoldProduced, 3, "the view shows 3");
    }

    private static void PurchasesAndEquips()
    {
        // A cost-4 Personality bought from a province: Red Crane Dojo (3, or 4 for a Ninja) is the only producer.
        MatchEngine Dynasty(bool ninja, out string cardId, out CardInstance red)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
            red = Add(s, RedCrane, 0, Zone.Home);
            OnlyProducers(s, 0, red);
            var province = s.Players[0].Provinces[0];
            var bought = s.Cards[province.CardId!];
            bought.DefinitionId = "training-battle-maiden"; bought.FaceUp = true;
            if (ninja) GiveKeyword(bought, "Ninja");
            cardId = bought.Id;
            return MatchEngine.FromTestState(s);
        }
        var n = Dynasty(true, out var ninjaId, out var redN);
        var quoteN = n.QuotePurchase(0, ninjaId);
        Eq(quoteN.Cost, 4);
        Check(quoteN.Payments.Length > 0 && quoteN.Payments.Any(p => p.SourceIds.SequenceEqual(new[] { redN.Id })), "Red Crane Dojo alone is offered for a Ninja recruit of cost 4");
        Accept(n, new GameCommand("purchase", 0, n.Version, ninjaId, SourceIds: [redN.Id]));
        Eq(n.State.Cards[ninjaId].Zone, Zone.Home, "the Ninja Personality was recruited with Red Crane Dojo's 4 Gold");
        Check(n.State.Cards[redN.Id].Bowed, "and the Holding bowed");
        var p = Dynasty(false, out var plainId, out var redP);
        var quoteP = p.QuotePurchase(0, plainId);
        Check(quoteP.Payments.Length == 0, "a non-Ninja of the same cost cannot be paid by Red Crane Dojo's 3");
        Reject(p, new GameCommand("purchase", 0, p.Version, plainId, SourceIds: [redP.Id]));
        // Excess is lost: a cost-2 Ninja paid with the Dojo's 4.
        var lost = Dynasty(true, out var cheapId, out var redL);
        var cheap = Edit(lost, st => st.Cards[cheapId].DefinitionId = "training-ninja-source");
        var quoteC = cheap.QuotePurchase(0, cheapId);
        Eq(quoteC.Cost, 3);
        Accept(cheap, new GameCommand("purchase", 0, cheap.Version, cheapId, SourceIds: [redL.Id]));
        Check(cheap.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("producing 4") && x.Message.Contains("1 excess was lost")), "the whole production paid for that one card, and the excess is lost");

        // An ability's payment: a Strategy played from a hand is a card being paid for. Its action costs the performer's current Chi plus 2 = 4: Red Crane Dojo alone pays it when
        // the Strategy is a Ninja card (4) and not otherwise (3); the same Strategy's action from a card in play would be an action, not a card.
        MatchEngine Played(bool ninja, out CardInstance strategy, out CardInstance performer, out CardInstance dojo)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            dojo = Add(s, RedCrane, 0, Zone.Home);
            OnlyProducers(s, 0, dojo);
            performer = Add(s, "training-lion-samurai", 0, Zone.Home);
            strategy = Add(s, "training-stat-cost-strategy", 0, Zone.Hand);
            performer.Modifiers.Add(new("test", "test", "Chi", 2 - Def(performer).Chi)); // a CURRENT Chi of 2, so the action costs 4
            if (ninja) GiveKeyword(strategy, "Ninja");
            return MatchEngine.FromTestState(s);
        }
        var pn = Played(true, out var ninjaStrategy, out var performerN, out var dojoN);
        var ninjaOption = Enabled(pn, 0, "training-stat-cost-performer", ninjaStrategy.Id, performerN.Id);
        Eq(ninjaOption.Gold!.Cost, 4);
        Check(ninjaOption.Gold.Payments.Any(x => x.SourceIds.SequenceEqual(new[] { dojoN.Id })), "a Ninja Strategy's action is paid by Red Crane Dojo's 4 alone");
        Accept(pn, ninjaOption.Command! with { SourceIds = [dojoN.Id] });
        Check(pn.State.Cards[dojoN.Id].Bowed, "and it bowed");
        var pp = Played(false, out var plainStrategy, out var performerP, out var dojoP);
        Check(!Options(pp, 0, "training-stat-cost-performer", plainStrategy.Id, performerP.Id).Any(o => o.Enabled && o.Gold is { Payments.Length: > 0 }), "a non-Ninja Strategy's 4 is beyond Red Crane Dojo's 3");
        Reject(pp, Use(pp, 0, "training-stat-cost-performer", plainStrategy.Id, null, performerP.Id) with { SourceIds = [dojoP.Id] });

        // Bushi Dojo: equipping a Follower (cost 2) is payable by it alone; an Item (cost 2) is not.
        MatchEngine Equip(string attachment, out CardInstance card, out CardInstance host, out CardInstance dojo)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            dojo = Add(s, BushiDojo, 0, Zone.Home);
            OnlyProducers(s, 0, dojo);
            host = Add(s, "training-crane-samurai", 0, Zone.Home);
            card = Add(s, attachment, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var fe = Equip("training-follower", out var followerCard, out var followerHost, out var bushi);
        var equipOption = fe.QueryActions(0).FirstOrDefault(a => a.Type == "equip" && a.CardId == followerCard.Id && a.Enabled);
        Check(equipOption is not null, "equipping a Follower is offered with Bushi Dojo as the only producer: " + string.Join("; ", fe.QueryActions(0).Where(a => a.CardId == followerCard.Id).Select(a => $"{a.Type} [{a.Enabled}] {a.Reason}")));
        Accept(fe, new GameCommand("equip", 0, fe.Version, followerCard.Id, TargetId: followerHost.Id, SourceIds: [bushi.Id]));
        Eq(fe.State.Cards[followerCard.Id].Zone, Zone.Attached, "the Follower was equipped, paid by Bushi Dojo's 6");
        var ie = Equip("training-item", out var itemCard, out var itemHost, out var bushi2);
        Reject(ie, new GameCommand("equip", 0, ie.Version, itemCard.Id, TargetId: itemHost.Id, SourceIds: [bushi2.Id]));
        Check(!ie.QueryActions(0).Any(a => a.Type == "equip" && a.CardId == itemCard.Id && a.Enabled), "an Item cannot be paid for by Bushi Dojo");
    }

    private static void RedCraneReaction()
    {
        MatchEngine Battle(string targetDef, bool dojoBowed, bool ninja = false, bool shadowlands = false)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var dojo = Add(s, RedCrane, 0, Zone.Home, bowed: dojoBowed);
            var target = Add(s, targetDef, 0, Zone.Battlefield, province: 0);
            if (ninja) GiveKeyword(target, "Ninja");
            var magistrate = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(magistrate, "Magistrate");
            Add(s, DispensingJustice, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        GameCommand Justice(MatchEngine e, string targetId)
        {
            var card = e.State.Cards.Values.Single(c => c.DefinitionId == DispensingJustice);
            var magistrate = e.State.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
            return Use(e, 1, "dispensing-justice-destroy", card.Id, targetId, magistrate.Id);
        }
        // Control: without the Reaction, Dispensing Justice destroys a Ninja.
        var s0 = Battle("training-crane-samurai", dojoBowed: true, ninja: true);
        var t0 = s0.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Accept(s0, Justice(s0, t0.Id));
        // The window: a bowed Red Crane Dojo may still react (Tireless).
        Eq(s0.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for a Ninja Personality of the Dojo's controller, even with the Holding bowed");
        Check(ReactionOffered(s0, 0, "red-crane-dojo-ninja"), "Red Crane Dojo's Reaction is offered while bowed");
        var doj = s0.State.Cards.Values.Single(c => c.DefinitionId == RedCrane);
        Accept(s0, ReactionOffer(s0, 0, "red-crane-dojo-ninja", doj.Id));
        Check(doj.Bowed || s0.State.Cards[doj.Id].Bowed, "the Holding is bowed");
        ClosePassing(s0);
        Check(s0.State.Cards[t0.Id].Zone != Zone.Dead && s0.State.Cards[t0.Id].Zone != Zone.DynastyDiscard, "the Ninja was not destroyed: the effect conditional on Ninja did not apply");
        Eq(s0.State.Cards[t0.Id].Zone, Zone.Home, "the rest of the action happened: he was moved home");
        Check(s0.HasKeyword(t0.Id, "Ninja"), "and he has Ninja again once the action has ended");
        Check(s0.State.TemporaryRemovals.Count == 0 && s0.State.Cards[t0.Id].RemovedKeywords.Count == 0, "nothing is left removed");

        // Without the Reaction: destroyed.
        var s1 = Battle("training-crane-samurai", dojoBowed: false, ninja: true);
        var t1 = s1.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Accept(s1, Justice(s1, t1.Id));
        ClosePassing(s1);
        Check(s1.State.Cards[t1.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "declining the Reaction, Dispensing Justice destroys a Ninja Personality: " + s1.State.Cards[t1.Id].Zone);

        // Shadowlands: a Clan alignment, removed and returned the same way.
        var s2 = Battle("training-shadowlands-personality", dojoBowed: false);
        var t2 = s2.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Accept(s2, Justice(s2, t2.Id));
        Accept(s2, ReactionOffer(s2, 0, "red-crane-dojo-ninja"));
        ClosePassing(s2);
        Eq(s2.State.Cards[t2.Id].Zone, Zone.Home, "a Shadowlands Personality is not destroyed either, and goes home");
        Check(s2.State.Cards[t2.Id].RemovedClanAffiliations.Count == 0, "his Clan alignment is his again");

        // Not offered: nothing to remove, or somebody else's Personality.
        var s3 = Battle("training-crane-samurai", dojoBowed: false);
        var t3 = s3.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Accept(s3, Justice(s3, t3.Id));
        Check(s3.State.PendingChoice is not { Type: "beforeActionReaction" } || !ReactionOffered(s3, 0, "red-crane-dojo-ninja"), "a Personality who is neither Ninja nor Shadowlands: no window for the Dojo");
        var s4 = Battle("training-crane-samurai", dojoBowed: false, ninja: true);
        var stranger = Edit(s4, st => { foreach (var c in st.Cards.Values.Where(c => c.DefinitionId == RedCrane)) c.OwnerId = 1; });
        var t4 = stranger.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Accept(stranger, Justice(stranger, t4.Id));
        Check(!ReactionOffered(stranger, 1, "red-crane-dojo-ninja") && !ReactionOffered(stranger, 0, "red-crane-dojo-ninja"), "the Dojo protects only its own controller's Personality");
    }

    private static bool ReactionOffered(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);
    private static GameCommand ReactionOffer(MatchEngine e, int player, string ability, string? cardId = null)
    {
        var offers = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability && (cardId is null || a.CardId == cardId)).ToArray();
        Check(offers.Length == 1, $"{ability}: expected one enabled offer for player {player}, got {offers.Length}; " + string.Join("; ", e.QueryActions(player).Select(a => $"{a.Type}/{a.Label} [{a.Enabled}] {a.Reason}")));
        return offers[0].Command!;
    }
    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void RemovalsEndWithTheTurn()
    {
        // A removal whose action never finished: still standing when the turn ends, given back with it, and only what was taken.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var card = Add(s, "training-ninja-source", 1, Zone.Home);
        card.RemovedKeywords.Add("Ninja"); card.RemovedKeywords.Add("Some Other Removal");
        s.TemporaryRemovals.Add(new TemporaryRemoval { CardId = card.Id, Keywords = ["Ninja"], Until = TemporaryRemovals.Action });
        var e = MatchEngine.FromTestState(s);
        Check(!e.HasKeyword(card.Id, "Ninja"), "removed for now");
        // Through the end of this turn.
        var turn = e.State.TurnNumber;
        for (var i = 0; i < 200 && e.State.TurnNumber == turn; i++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var pick = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).FirstOrDefault(a => a.Type is "discardHand" or "pass" or "endDynasty" or "declineReaction" or "declineStraighten" or "declineAttack")
                ?? throw new Exception("nothing to pass with in " + e.State.Phase);
            Accept(e, pick.Command!);
        }
        Check(e.State.TurnNumber != turn, "the turn ended");
        Check(e.HasKeyword(card.Id, "Ninja"), "Ninja is given back when the turn ends");
        Check(e.State.Cards[card.Id].RemovedKeywords.SequenceEqual(new[] { "Some Other Removal" }), "and only what the record took: an unrelated removal stays");
        Check(e.State.TemporaryRemovals.Count == 0, "the record is gone");
    }
}
