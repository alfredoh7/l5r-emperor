using L5R.Core;
using static RegressionAssertions;

// Issue #833, slice 8: single-action cards whose own preconditions the generic kitchen-sink probe could not meet (or whose effect no test checked).
// Same scenario model as MultiWindowAudit1Tests: a (phase, who is active, who acts) triple; the action must be offered AND accepted in each window its
// printed timing allows, refused in the others, and do what the printed text says.
internal static partial class SingleAudit8Tests
{
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static string Rejection(MatchEngine e, GameCommand c) => Batch336Support.Rejection(e, c);
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    private sealed record W(int Me, int Foe, bool Battle, string Scenario)
    {
        public Zone Zone => Battle ? Zone.Battlefield : Zone.Home;
        public int? Province => Battle ? 0 : null;
    }
    private sealed record Fx(int Me, string Ability, string Card, string? Target = null, string? Performer = null, SlotSelection[]? Sel = null, string? Choice = null, int? GoldAmount = null, int? Province = null, int? TargetPlayer = null, string[]? CardIds = null, string[]? Extra = null);

    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance PutKw(MatchState s, string def, int owner, W w, params string[] keywords) => Kw(Put(s, def, owner, w), keywords);
    private static void SetDeck(MatchState s, int player, Zone zone, params string[] defs)
    {
        var deck = zone == Zone.FateDeck ? s.Players[player].FateDeck : s.Players[player].DynastyDeck;
        foreach (var id in deck.ToArray()) s.Cards.Remove(id);
        deck.Clear();
        foreach (var def in defs) { var c = Add(s, def, player, zone); c.FaceUp = false; deck.Add(c.Id); }
    }
    private static GameCommand Do(MatchEngine e, string type, int player, string? choice = null, int? targetPlayer = null, string[]? ids = null, string? target = null) => new(type, player, e.Version, TargetId: target, Choice: choice, TargetPlayerId: targetPlayer, CardIds: ids);
    private static CardInstance Put(MatchState s, string def, int owner, W w, bool bowed = false) => Add(s, def, owner, w.Zone, bowed, w.Province);

    // The command the engine itself offers for this fixture (a disabled option has none), completed with the fixture's target, performer and slots.
    private static GameCommand? Offered(MatchEngine e, Fx f)
    {
        var option = Options(e, f.Me, f.Ability, f.Card, f.Performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == f.Ability && (f.Target is null || o.TargetIds is null || o.TargetIds.Contains(f.Target) || o.Command.TargetId == f.Target));
        if (option is null) return null;
        var command = option.Command! with { TargetId = f.Target ?? option.Command.TargetId, Selections = f.Sel ?? option.Command.Selections, Choice = f.Choice ?? option.Command.Choice, GoldAmount = f.GoldAmount ?? option.Command.GoldAmount, Province = f.Province ?? option.Command.Province, TargetPlayerId = f.TargetPlayer ?? option.Command.TargetPlayerId, CardIds = f.CardIds ?? option.Command.CardIds };
        if (command.SourceIds is null)
        {
            var quote = f.Target is not null && option.TargetGold is not null && option.TargetGold.TryGetValue(f.Target, out var tq) ? tq : option.Gold;
            if (quote is { Payments.Length: > 0 }) command = command with { SourceIds = quote.Payments[0].SourceIds };
        }
        return command;
    }

    // Declines every reaction window the action opened (nobody holds a Reaction in these fixtures), so the action has fully resolved.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 20 && e.State.PendingChoice is not null; guard++)
        {
            var step = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "passReaction" or "pass");
            if (step is null) break;
            Accept(e, step.Command!);
        }
    }

    private static readonly string[] Scenarios = ["battle-attacker", "battle-defender", "action-active", "action-other"];

    // Runs one ability through all four windows. `build` lays out the table for the acting player; `effect` sees the state before and the engine after
    // an accepted use. Allowed windows must offer and accept it; every other window must not offer it enabled and must refuse a direct command.
    private static void Windows(string ability, Func<MatchState, W, Fx> build, Action<MatchState, MatchEngine, Fx, W>? effect = null, string[]? only = null, Func<string, string>? why = null)
    {
        var timing = AbilityCatalog.All[ability].Timing;
        var allowed = only ?? MultiWindowAuditTests.Expected(timing);
        foreach (var scenario in Scenarios)
        {
            var battle = scenario.StartsWith("battle");
            var me = scenario is "battle-defender" or "action-active" ? 0 : 1;
            var s = Table(battle ? Phase.Battle : Phase.Action, battle ? 1 : 0);
            s.PriorityPlayerId = me;
            var w = new W(me, 1 - me, battle, scenario);
            var fx = build(s, w);
            var e = MatchEngine.FromTestState(s);
            var before = e.State;
            var command = Offered(e, fx);
            if (allowed.Contains(scenario))
            {
                Check(command is not null, $"{ability} [{timing}] was not offered enabled in {scenario}: " + string.Join("; ", Options(e, fx.Me, ability, fx.Card, fx.Performer).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
                var result = e.Execute(command!);
                Check(result.Accepted, $"{ability} offered but refused in {scenario}: {result.Error}");
                Settle(e);
                effect?.Invoke(before, e, fx, w);
            }
            else
            {
                Check(command is null, $"{ability} [{timing}] must not be offered in {scenario}");
                var direct = Use(e, fx.Me, ability, fx.Card, fx.Target, fx.Performer, fx.Sel ?? []) with { Choice = fx.Choice, Province = fx.Province, TargetPlayerId = fx.TargetPlayer, CardIds = fx.CardIds };
                var error = Rejection(e, direct);
                var expected = why?.Invoke(scenario) ?? "timing is unavailable now";
                Check(error.Contains(expected), $"{ability} refused in {scenario} for the wrong reason (wanted '{expected}'): {error}");
            }
        }
    }

    private static void Gold(MatchState s, int player, int marketplaces = 6) { for (var i = 0; i < marketplaces; i++) Add(s, "training-marketplace", player, Zone.Home); }

    public static void Run(TestRunner runner) => runner.Run("single-audit-8", [
        ("Akumasa: Open, targeting another player's Personality with a Madness token sets a watch that draws a card after he attacks, in either player's Action phase only", AkumasaWatch),
        ("Back to the Front: a bowed Personality at home returns to the battlefield he was assigned to and his unit straightens, as either side of a battle only", BackToTheFront),
        ("Beautiful Host: Political Open gives the performer a +1F Revenge token after another player's action cost you Honor this turn, in either player's Action phase only", BeautifulHost),
        ("Beautiful Host: without an Honor loss from another player's action this turn the Open action is not offered and is refused", BeautifulHostNeedsAHonorLoss),
        ("Buried Treasures: Limited, a Mantis player with four or fewer cards in hand bows it to draw a card, in the active player's Action phase only", BuriedTreasures),
        ("The Merchant's District: Limited, a Scorpion player with four or fewer cards in hand bows it to draw a card, in the active player's Action phase only", MerchantsDistrict),
        ("Colonial Farm: Limited, bowing it creates a 1F Ashigaru Follower attached to the target Personality, in the active player's Action phase only", ColonialFarm),
        ("Daigotsu Bofana (exp): Battle, a Dark Virtue in your discard pile is played through an additional action for a Battle action and then removed from the game, as either side of a battle only", BofanaDarkVirtue),
        ("Deep Roots: Battle, a 7-base-Force performer has his Force penalties negated, as either side of a battle only", DeepRoots),
        ("Doji Kurohime: Limited, bowing her straightens a bowed Honor-producing Holding that has not produced Gold this turn, in the active player's Action phase only", KurohimeStraighten),
        ("Exquisite Blade: Battle, its Melee 6 Attack destroys a Force 6 enemy and spares a Force 7 one, as either side of a battle only", ExquisiteBlade),
        ("Fortitude: Battle, an unbowed Berserker bows an enemy unit with total Force below his base Force and loses his Force penalties, as either side of a battle only", Fortitude),
        ("Hamuro, the Wise: Limited, draws three cards and is removed from the game, in the active player's Action phase only", HamuroDraws),
        ("Hunger of the Fire: Battle, a Fire Shugenja destroys the Spell and an attachment-free enemy card without bowing, as either side of a battle only", HungerFire),
        ("Hunger of the Fire: Battle, a non-Fire Shugenja destroys the Spell and an attachment-free enemy card and bows, as either side of a battle only", HungerWater),
        ("Imperial Explorer's Dojo: Limited, bowing it returns a Terrain from your discard pile to your hand and destroys the Holding, in the active player's Action phase only", ExplorersDojo),
        ("Jade Embrace: Battle, an Earth Shugenja's Spell bows and sets an enemy Personality's Force to 0 and removes the chosen ability, as either side of a battle only", JadeEmbrace),
        ("Kakita Seishi: Open, bowing him rehonors a dishonorable Personality and no honorable one is a target, in either player's Action phase only", SeishiRehonor),
        ("Mantle of the Onyx Champion: Battle, the Onyx Champion's Maho Ranged 5 Attack destroys a Force 1 enemy, as either side of a battle only", OnyxMantle),
        ("Lady Matsu's Rage: Battle, an unbowed attacking Personality discards her to bow an enemy card that is destroyed when it next straightens, for the Attacker only", LadyMatsusRage),
        ("Moshi Tomiko (exp): Limited, bows your unbowed Ring and draws a card without bowing her, in the active player's Action phase only", TomikoBowDraw),
        ("Ogre Savagery: Battle, an unbowed Nonhuman Shadowlands performer bows an enemy Personality, as either side of a battle only", OgreSavagery),
        ("Poison the Cup: Limited, the Shugenja has Shadowlands and the Spell bows to swap a token for a -1C Plague token and cost 1 Honor, in the active player's Action phase only", PoisonTheCup),
        ("Relentless Conviction: Battle, destroys a dishonorable Personality and no honorable one is a target, as either side of a battle only", RelentlessConviction),
        ("Rise of the Dark Naga: Open, with two Nonhuman Personalities in play a face-up Nonhuman in a province enters play paying costs and the province refills face-up, in either player's Action phase only", DarkNagaRise),
        ("Searching for Answers: Battle, only the Defender looks at the top four Dynasty cards and puts a Personality into play free at the battlefield", SearchingForAnswers),
        ("Shinjo Tselu (exp): Limited, searches the Fate deck for a non-Unique Cavalry Follower, shows it, puts it in hand and discards a card, in the active player's Action phase only", TseluSearch),
        ("Stand as the Mountain: Battle, straightens one or two performing Personalities with 7 or more base Force and not a smaller one, as either side of a battle only", StandAsTheMountain),
        ("Sycophants: Political Limited, bows to straighten its Personality and grant a second use of his once-a-turn Political action, in the active player's Action phase only", Sycophants),
        ("The Courage of the Mantis: Battle, with enemy units there an unbowed Mantis Clan Personality moves to the battlefield and an additional Battle action is granted, as either side of a battle only", CourageOfTheMantis),
        ("The Thunder Resounds: Thunder Battle, copies an ability of a Spell at the battlefield onto this Spell until the turn ends, as either side of a battle only", ThunderResounds),
        ("Thunderous Legion: Battle, destroys an enemy card without attachments and not one that has them, as either side of a battle only", ThunderousLegion),
        ("Training adjacent booster (synthetic): Battle, gives a Personality at an adjacent battlefield +2F and none elsewhere, as either side of a battle only", AdjacentBooster),
        ("Unexpected Discovery: Battle, moves your opposed Personality to an adjacent unresolved battlefield, as either side of a battle only", UnexpectedDiscovery),
        ("Wrathful Dead: Battle, destroys a card without attachments in a unit led by a dishonorable Personality, as either side of a battle only", WrathfulDead),
        ("Your Clan Needs You!: Battle, with enemy units there a province Personality enters at the battlefield for 6 less Gold and returns to a province after the battle, as either side of a battle only", YourClanNeedsYou),
    ]);

    private const string Akumasa = "oracle-l5r-10495", BackToFront = "oracle-l5r-662", Host = "oracle-l5r-943", BuriedTreasuresCard = "oracle-l5r-10211", MerchantsDistrictCard = "oracle-l5r-10216", ColonialFarmCard = "oracle-l5r-10231",
        BofanaExp = "oracle-l5r-10315", DeepRootsCard = "oracle-l5r-9901";

    private static void AkumasaWatch() => Windows("w737-akumasa-watch", (s, w) =>
    {
        var akumasa = Add(s, Akumasa, w.Me, Zone.Home);
        var mad = Put(s, "training-lion-samurai", w.Foe, w); mad.Tokens.Add(new CardToken("mad-" + mad.Id, "Madness", "test"));
        Put(s, "training-crane-samurai", w.Foe, w); // a Personality with no Madness token is no target
        return new(w.Me, "w737-akumasa-watch", akumasa.Id, mad.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.AssignmentWatches.Any(a => a.CardId == f.Target && a.Kind == "w737-akumasa" && a.BeneficiaryPlayerId == f.Me), "a watch on the target is set for Akumasa's controller");
        Eq(e.State.Cards[f.Target!].OwnerId, w.Foe, "the target is another player's Personality");
        if (w.Scenario != "action-other") return;
        // The target's controller is the active player: he attacks and Akumasa's controller draws exactly one card.
        var hand = e.State.Players[f.Me].Hand.Count;
        var attack = Batch336Support.Edit(e, st => st.Phase = Phase.InfantryDeclaration);
        Accept(attack, new GameCommand("attack", w.Foe, attack.Version, Assignments: new Dictionary<string, int> { [f.Target!] = 0 }));
        Eq(attack.State.Players[f.Me].Hand.Count, hand + 1, "Akumasa's controller drew a card after the target attacked");
        Check(!attack.State.AssignmentWatches.Any(a => a.Kind == "w737-akumasa"), "the watch is spent");
    });

    private static void BackToTheFront() => Windows("back-to-the-front-move", (s, w) =>
    {
        var card = Add(s, BackToFront, w.Me, Zone.Hand);
        var runner = Add(s, "training-crane-samurai", w.Me, Zone.Home, bowed: true);
        Put(s, "training-lion-samurai", w.Foe, w);
        s.IvoryMovement = new() { Key = $"{s.TurnNumber}|{s.ActivePlayerId}", Assigned = new() { [runner.Id] = [0] } };
        return new(w.Me, "back-to-the-front-move", card.Id, null, runner.Id, Province: 0, Extra: [runner.Id]);
    }, (before, e, f, w) =>
    {
        var runner = e.State.Cards[f.Extra![0]];
        Eq(runner.Zone, Zone.Battlefield, "he moved to the battlefield he was assigned to"); Eq(runner.ProvinceIndex, (int?)0);
        Check(!runner.Bowed, "he moved, so his unit was straightened");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void BeautifulHost() => Windows("tf-beautiful-host-revenge", (s, w) =>
    {
        var host = Add(s, Host, w.Me, Zone.Home);
        var performer = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        s.HonorLostToOtherActionTurn[w.Me] = s.TurnNumber; // the real path (an enemy action costing Honor) is driven in TfHoldingsTests.BeautifulHostRevenge
        return new(w.Me, "tf-beautiful-host-revenge", host.Id, null, performer.Id, Extra: [performer.Id]);
    }, (before, e, f, w) =>
    {
        var tokens = e.State.Cards[f.Extra![0]].Tokens.Where(t => t.Type == "Revenge").ToArray();
        Eq(tokens.Length, 1, "one Revenge token on the performer"); Eq(tokens[0].Stat, "Force"); Eq(tokens[0].Amount, 1);
        Check(!e.State.Cards[f.Card].Bowed, "no bow is printed for the Open action");
    });

    private static void BeautifulHostNeedsAHonorLoss()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Host, 0, Zone.Home); var performer = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "tf-beautiful-host-revenge", host.Id).Any(o => o.Enabled), "no Honor loss from another player's action this turn: not offered");
        Check(Rejection(e, Use(e, 0, "tf-beautiful-host-revenge", host.Id, null, performer.Id)).Contains("Honor loss this turn"), "and refused for that reason");
    }

    private static void MarketDraw(string card, string ability, string clan)
    {
        Windows(ability, (s, w) =>
        {
            s.Players[w.Me].Clan = clan;
            var market = Add(s, card, w.Me, Zone.Home);
            for (var i = 0; i < 4; i++) Add(s, "training-crane-samurai", w.Me, Zone.Hand); // four is the most the card allows
            return new(w.Me, ability, market.Id);
        }, (before, e, f, w) =>
        {
            Eq(e.State.Players[f.Me].Hand.Count, before.Players[f.Me].Hand.Count + 1, "drew exactly one card");
            Check(e.State.Cards[f.Card].Bowed, "the Holding bowed as the cost");
        });
        var wrong = Table(Phase.Action, 0);
        wrong.Players[0].Clan = clan == "Mantis" ? "Crab" : "Mantis";
        var other = Add(wrong, card, 0, Zone.Home);
        var ew = MatchEngine.FromTestState(wrong);
        Check(!Options(ew, 0, ability, other.Id).Any(o => o.Enabled), "another Clan's player is not offered it");
        Check(Rejection(ew, Use(ew, 0, ability, other.Id)).Contains($"{clan} Clan player"), "and is refused for that reason");
        var full = Table(Phase.Action, 0);
        full.Players[0].Clan = clan;
        var market = Add(full, card, 0, Zone.Home);
        for (var i = 0; i < 5; i++) Add(full, "training-crane-samurai", 0, Zone.Hand);
        var ef = MatchEngine.FromTestState(full);
        Check(!Options(ef, 0, ability, market.Id).Any(o => o.Enabled), "five cards in hand is too many");
        Check(Rejection(ef, Use(ef, 0, ability, market.Id)).Contains("four or fewer"), "and is refused for that reason");
    }
    private static void BuriedTreasures() => MarketDraw(BuriedTreasuresCard, "market-draw-mantis", "Mantis");
    private static void MerchantsDistrict() => MarketDraw(MerchantsDistrictCard, "market-draw-scorpion", "Scorpion");

    private static void ColonialFarm() => Windows("pe-colonial-farm-create", (s, w) =>
    {
        var farm = Add(s, ColonialFarmCard, w.Me, Zone.Home);
        var host = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "pe-colonial-farm-create", farm.Id, host.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "the Farm bowed as the cost");
        var made = e.State.Cards.Values.Single(c => c.DefinitionId == "user-scan-colonists");
        Eq(made.Zone, Zone.Attached); Eq(made.AttachedTo, f.Target);
        var def = CardCatalog.Get(made.DefinitionId);
        Check(def.Type == CardType.Follower && def.Force == 1 && def.Keywords!.Contains("Ashigaru"), "a 1F Ashigaru Follower");
        Eq(made.OwnerId, f.Me, "controlled by the Farm's player");
    });

    private static void BofanaDarkVirtue() => Windows("bofana-exp-dark-virtue", (s, w) =>
    {
        var bofana = Put(s, BofanaExp, w.Me, w);
        var enemy = Put(s, "training-lion-samurai", w.Foe, w);
        var dv = Add(s, "training-dark-virtue", w.Me, Zone.FateDiscard);
        return new(w.Me, "bofana-exp-dark-virtue", bofana.Id, dv.Id, Extra: [enemy.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.AdditionalActions.Count, 1, "one additional action granted");
        Check(e.State.AdditionalActions[0] is { FromDiscard: true } g && g.PlayerId == f.Me && g.ScopeCardIds!.SequenceEqual([f.Target!]), "scoped to the chosen Dark Virtue, played from the discard pile");
        var enemy = f.Extra![0];
        Accept(e, Batch336Support.Offer(e, f.Me, "training-bow-at-battle", f.Target!, enemy));
        Check(e.State.Cards[enemy].Bowed, "its Battle action resolved");
        Eq(e.State.Cards[f.Target!].Zone, Zone.RemovedFromGame, "after it resolves the card is removed from the game");
    });

    private static void DeepRoots() => Windows("deep-roots-negate", (s, w) =>
    {
        var card = Add(s, DeepRootsCard, w.Me, Zone.Hand);
        var giant = Put(s, "training-high-force-personality", w.Me, w);
        giant.Modifiers.Add(new("test-source", "test", "Force", -3));
        Put(s, "training-lion-veteran", w.Foe, w);
        return new(w.Me, "deep-roots-negate", card.Id, null, giant.Id, Extra: [giant.Id]);
    }, (before, e, f, w) =>
    {
        Eq(before.Cards[f.Performer!].Modifiers.Sum(m => m.Stat == "Force" ? m.Amount : 0), -3, "(he began with a -3F penalty)");
        Eq(e.CardForceForTest(f.Performer!), 7, "the Force penalty is negated: his base 7");
        Eq(e.State.Cards[f.Performer!].DeepRootsTurn, e.State.TurnNumber, "he contributes Force while bowed this turn");
    });

    private const string Kurohime = "oracle-l5r-10840", Chambers = "oracle-l5r-10436", Exquisite = "oracle-l5r-2418", FortitudeCard = "oracle-l5r-2671", Hamuro = "oracle-l5r-10474", HungerOfFire = "oracle-l5r-3517",
        Dojo = "oracle-l5r-10613", JadeEmbraceCard = "oracle-l5r-9985", Seishi = "oracle-l5r-4220", Mantle = "oracle-l5r-9969";

    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static string[] Targets(MatchEngine e, int player, string ability, string card, string? performer = null) =>
        Options(e, player, ability, card, performer).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();

    private static void KurohimeStraighten()
    {
        Windows("w732-kurohime-straighten", (s, w) =>
        {
            var kurohime = Add(s, Kurohime, w.Me, Zone.Home);
            var honor = Add(s, Chambers, w.Foe, Zone.Home, bowed: true);
            Put(s, "training-lion-samurai", w.Foe, w);
            return new(w.Me, "w732-kurohime-straighten", kurohime.Id, honor.Id);
        }, (before, e, f, w) =>
        {
            Check(!e.State.Cards[f.Target!].Bowed, "the Honor-producing Holding is straightened");
            Check(e.State.Cards[f.Card].Bowed, "Kurohime bowed as the cost");
        });
        var tab = Table(Phase.Action, 0);
        var kuro = Add(tab, Kurohime, 0, Zone.Home);
        var honored = Add(tab, Chambers, 1, Zone.Home, bowed: true); var spent = Add(tab, Chambers, 1, Zone.Home, bowed: true);
        var plain = Add(tab, "training-marketplace", 1, Zone.Home, bowed: true);
        tab.GoldProducedTurns[spent.Id] = tab.TurnNumber;
        var eng = MatchEngine.FromTestState(tab);
        var targets = Targets(eng, 0, "w732-kurohime-straighten", kuro.Id);
        Check(targets.Contains(honored.Id) && !targets.Contains(spent.Id) && !targets.Contains(plain.Id), "only an Honor-producing Holding that has not produced Gold this turn is a target: " + string.Join(",", targets));
        Check(Rejection(eng, Use(eng, 0, "w732-kurohime-straighten", kuro.Id, plain.Id)).Length > 0 && Rejection(eng, Use(eng, 0, "w732-kurohime-straighten", kuro.Id, spent.Id)).Length > 0, "both wrong targets are refused");
        Check(eng.State.Cards[plain.Id].Bowed && eng.State.Cards[spent.Id].Bowed && !eng.State.Cards[kuro.Id].Bowed, "and nothing changed");
    }

    private static void ExquisiteBlade() => Windows("pe-exquisite-melee", (s, w) =>
    {
        var wielder = Put(s, "training-crane-samurai", w.Me, w);
        var blade = Attach(s, Exquisite, wielder);
        var weak = Put(s, "training-lion-samurai", w.Foe, w); Force(weak, 2); // Force 6
        var strong = Put(s, "training-lion-samurai", w.Foe, w); Force(strong, 3); // Force 7
        return new(w.Me, "pe-exquisite-melee", blade.Id, weak.Id, Extra: [wielder.Id, strong.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "Melee 6 destroyed the Force 6 Personality");
        Eq(e.State.Cards[f.Extra![1]].Zone, w.Battle ? Zone.Battlefield : Zone.Home, "the Force 7 Personality was not touched");
        Check(!e.State.Cards[f.Card].Bowed && !e.State.Cards[f.Extra[0]].Bowed, "the printed action has no cost");
    });

    private static void Fortitude() => Windows("fortitude-bow", (s, w) =>
    {
        var card = Add(s, FortitudeCard, w.Me, Zone.Hand);
        var berserker = PutKw(s, "training-crane-veteran", w.Me, w, "Berserker"); // base Force 5
        berserker.Modifiers.Add(new("test-source", "test", "Force", -2));
        var weak = Put(s, "training-lion-samurai", w.Foe, w); // Force 4
        Put(s, "training-lion-veteran", w.Foe, w); // Force 6: not below 5
        return new(w.Me, "fortitude-bow", card.Id, weak.Id, berserker.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the target unit is bowed");
        Eq(e.CardForceForTest(f.Performer!), 5, "his -2F penalty is negated");
        Eq(e.State.Cards[f.Performer!].ForcePenaltiesNegatedTurn, e.State.TurnNumber, "and new Force penalties are negated this turn");
        Check(!e.State.Cards[f.Performer!].Bowed, "the Berserker himself stays unbowed");
    });

    private static void HamuroDraws() => Windows("w9501-hamuro", (s, w) =>
    {
        var hamuro = Add(s, Hamuro, w.Me, Zone.Home);
        Add(s, "training-follower", w.Me, Zone.Attached, parent: hamuro.Id);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "w9501-hamuro", hamuro.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Players[f.Me].Hand.Count, before.Players[f.Me].Hand.Count + 3, "three cards drawn");
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "Hamuro is removed from the game");
        Check(e.State.Cards.Values.Where(c => c.AttachedTo == f.Card).Count() == 0, "nothing stays attached to the removed Personality");
    });

    private static void HungerWindows(string shugenjaDef, bool fire, int targetForce) => Windows("promo-d-hunger-fire-destroy", (s, w) =>
    {
        var shugenja = Put(s, shugenjaDef, w.Me, w); // Fire: 2F/3C, Water: 2F/2C
        var spell = Attach(s, HungerOfFire, shugenja);
        var target = Put(s, "training-lion-samurai", w.Foe, w); Force(target, targetForce - 4);
        Put(s, "training-lion-veteran", w.Foe, w); // Force 6: out of reach
        return new(w.Me, "promo-d-hunger-fire-destroy", spell.Id, target.Id, shugenja.Id, Extra: [shugenja.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the bare enemy Personality is destroyed");
        Check(e.State.Cards[f.Card].Zone is Zone.FateDiscard, "the Spell was destroyed as a cost");
        Eq(e.State.Cards[f.Extra![0]].Bowed, !fire, fire ? "a Fire Shugenja is not bowed" : "a non-Fire Shugenja is bowed");
    });
    private static void HungerFire() => HungerWindows("training-seeds5-fire-shugenja", true, 3);
    private static void HungerWater() => HungerWindows("training-water-shugenja", false, 2);

    private static void ExplorersDojo()
    {
        Windows("wl-imperial-explorers-dojo-terrain", (s, w) =>
        {
            var dojo = Add(s, Dojo, w.Me, Zone.Home);
            var mine = Add(s, "training-terrain", w.Me, Zone.FateDiscard);
            Put(s, "training-lion-samurai", w.Foe, w);
            return new(w.Me, "wl-imperial-explorers-dojo-terrain", dojo.Id, mine.Id);
        }, (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Hand, "the Terrain is in your hand"); Check(e.State.Players[f.Me].Hand.Contains(f.Target!), "listed in the hand");
            Check(e.State.Cards[f.Card].Zone is Zone.DynastyDiscard or Zone.Dead or Zone.FateDiscard, "the Holding is destroyed: " + e.State.Cards[f.Card].Zone);
        });
        var tab = Table(Phase.Action, 0);
        var d = Add(tab, Dojo, 0, Zone.Home);
        var own = Add(tab, "training-terrain", 0, Zone.FateDiscard); var foes = Add(tab, "training-terrain", 1, Zone.FateDiscard); var notTerrain = Add(tab, "training-rally", 0, Zone.FateDiscard);
        var eng = MatchEngine.FromTestState(tab);
        var targets = Targets(eng, 0, "wl-imperial-explorers-dojo-terrain", d.Id);
        Check(targets.SequenceEqual([own.Id]), "only a Terrain in your own discard pile is a target: " + string.Join(",", targets));
        Check(Rejection(eng, Use(eng, 0, "wl-imperial-explorers-dojo-terrain", d.Id, foes.Id)).Length > 0 && Rejection(eng, Use(eng, 0, "wl-imperial-explorers-dojo-terrain", d.Id, notTerrain.Id)).Length > 0, "another player's Terrain and a non-Terrain are refused");
    }

    private static void JadeEmbrace() => Windows("pe-jade-embrace-remove", (s, w) =>
    {
        var shugenja = PutKw(s, "training-shugenja", w.Me, w, "Earth");
        var spell = Attach(s, JadeEmbraceCard, shugenja);
        var foe = Put(s, "training-dual-attacker", w.Foe, w);
        return new(w.Me, "pe-jade-embrace-remove", spell.Id, foe.Id, Extra: [shugenja.Id]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed && !e.State.Cards[f.Extra![0]].Bowed, "the Spell bowed, not the Shugenja");
        Check(e.CardForceForTest(f.Target!) == 0, "this Shugenja is Earth: the target's Force is 0");
        var choice = e.QueryActions(f.Me).Where(a => a.Type == "chooseEffect" && a.Enabled && a.Command is not null).Select(a => a.Command!).First(c => c.Choice == "training-weapon-attack");
        Accept(e, choice);
        Check(e.AbilityIdsForTest(f.Target!).SequenceEqual(["training-honor-cost-attack"]), "the chosen ability is gone and the other stays: " + string.Join(",", e.AbilityIdsForTest(f.Target!)));
        Eq(e.State.Cards[f.Target!].Zone, w.Battle ? Zone.Battlefield : Zone.Home, "he is not destroyed");
    });

    private static void SeishiRehonor()
    {
        Windows("seishi-rehonor", (s, w) =>
        {
            var seishi = Add(s, Seishi, w.Me, Zone.Home);
            var shamed = Put(s, "training-lion-samurai", w.Foe, w); shamed.Dishonorable = true;
            Put(s, "training-crane-samurai", w.Foe, w);
            return new(w.Me, "seishi-rehonor", seishi.Id, shamed.Id);
        }, (before, e, f, w) =>
        {
            Check(!e.State.Cards[f.Target!].Dishonorable, "he is rehonored");
            Check(e.State.Cards[f.Card].Bowed, "Seishi bowed as the cost");
        });
        var tab = Table(Phase.Action, 0);
        var sei = Add(tab, Seishi, 0, Zone.Home);
        var shame = Add(tab, "training-lion-samurai", 1, Zone.Home); shame.Dishonorable = true;
        var ownShame = Add(tab, "training-crane-samurai", 0, Zone.Home); ownShame.Dishonorable = true;
        var honest = Add(tab, "training-lion-courtier", 1, Zone.Home);
        var eng = MatchEngine.FromTestState(tab);
        var targets = Targets(eng, 0, "seishi-rehonor", sei.Id);
        Check(targets.Contains(shame.Id) && targets.Contains(ownShame.Id) && !targets.Contains(honest.Id), "any dishonorable Personality is a target and an honorable one is not: " + string.Join(",", targets));
        Check(Rejection(eng, Use(eng, 0, "seishi-rehonor", sei.Id, honest.Id)).Length > 0 && !eng.State.Cards[sei.Id].Bowed, "an honorable target is refused and costs nothing");
    }

    private static void OnyxMantle() => Windows("pe-onyx-ranged", (s, w) =>
    {
        var wearer = PutKw(s, "training-crane-samurai", w.Me, w, "Onyx Champion");
        var mantle = Attach(s, Mantle, wearer);
        var foe = Put(s, "training-lion-samurai", w.Foe, w); Force(foe, -3); // Force 1
        var tough = Put(s, "training-lion-veteran", w.Foe, w); // Force 6
        return new(w.Me, "pe-onyx-ranged", mantle.Id, foe.Id, Extra: [wearer.Id, tough.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the Ranged 5 Attack destroyed the Force 1 enemy");
        Eq(e.State.Cards[f.Extra![1]].Zone, w.Battle ? Zone.Battlefield : Zone.Home, "the other enemy was not attacked");
        Check(!e.State.Cards[f.Card].Bowed && !e.State.Cards[f.Extra[0]].Bowed, "the action has no cost");
    });

    private const string Matsu = "oracle-l5r-4658", Horibe = "oracle-l5r-6885", TomikoExp = "oracle-l5r-10466", RingOfAir = "oracle-l5r-6333", RingOfFire = "oracle-l5r-6335", OgreSavageryCard = "oracle-l5r-5681",
        PoisonCup = "oracle-l5r-10702", Conviction = "oracle-l5r-6247", Naga = "oracle-l5r-6343", SearchingCard = "oracle-l5r-6526", TseluExp = "oracle-l5r-10511", HorseArchers = "oracle-l5r-3456", MotoHouseGuard = "oracle-l5r-5329",
        MountainCard = "oracle-l5r-7459", SycophantsCard = "oracle-l5r-10338", Kodo = "oracle-l5r-142", CourageCard = "oracle-l5r-7995", Resounds = "oracle-l5r-10699", TempestOfFlame = "oracle-l5r-7866",
        LegionCard = "oracle-l5r-10515", DiscoveryCard = "oracle-l5r-10730", DeadCard = "oracle-l5r-9391", ClanNeedsYou = "oracle-l5r-9687";

    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);

    private static void LadyMatsusRage() => Windows("lady-matsus-rage-bow-destroy", (s, w) =>
    {
        var matsu = Add(s, Matsu, w.Me, Zone.Celestial);
        var attacker = Put(s, "training-lion-samurai", w.Me, w);
        var horibe = Put(s, Horibe, w.Me, w);
        var target = Put(s, "training-crane-samurai", w.Foe, w);
        return new(w.Me, "lady-matsus-rage-bow-destroy", matsu.Id, target.Id, attacker.Id, Extra: [horibe.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "she was discarded as the cost");
        Check(e.State.Cards[f.Target!].Bowed, "the target is bowed");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "and not yet destroyed");
        Check(e.State.PendingLadyMatsusRageWatches.Any(x => x.TargetId == f.Target), "a watch waits for it to straighten this turn");
        if (e.State.PriorityPlayerId != f.Me) Accept(e, Pass(e));
        Accept(e, Batch336Support.Offer(e, f.Me, "horibe-straighten", f.Extra![0], f.Target!));
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "after the next time it straightens it is destroyed");
    }, only: ["battle-attacker"], why: scenario => scenario == "battle-defender" ? "performer requirement" : "timing is unavailable now");

    private static void TomikoBowDraw()
    {
        Windows("wl-pc-tomiko-bow-draw", (s, w) =>
        {
            var tomiko = Add(s, TomikoExp, w.Me, Zone.Home);
            var ring = Add(s, RingOfAir, w.Me, Zone.Home);
            Put(s, "training-lion-samurai", w.Foe, w);
            return new(w.Me, "wl-pc-tomiko-bow-draw", tomiko.Id, ring.Id);
        }, (before, e, f, w) =>
        {
            Check(e.State.Cards[f.Target!].Bowed, "the Ring is bowed");
            Eq(e.State.Players[f.Me].Hand.Count, before.Players[f.Me].Hand.Count + 1, "a card was drawn");
            Check(!e.State.Cards[f.Card].Bowed, "Tomiko is not bowed: the bow is an effect, not a cost");
        });
        var tab = Table(Phase.Action, 0);
        var tom = Add(tab, TomikoExp, 0, Zone.Home);
        var good = Add(tab, RingOfAir, 0, Zone.Home); var bowed = Add(tab, RingOfFire, 0, Zone.Home, bowed: true); var theirs = Add(tab, RingOfAir, 1, Zone.Home);
        var eng = MatchEngine.FromTestState(tab);
        var targets = Targets(eng, 0, "wl-pc-tomiko-bow-draw", tom.Id);
        Check(targets.Contains(good.Id) && !targets.Contains(bowed.Id) && !targets.Contains(theirs.Id), "only your own unbowed Ring is a target: " + string.Join(",", targets));
    }

    private static void OgreSavagery()
    {
        Windows("ogre-savagery-bow", (s, w) =>
        {
            var card = Add(s, OgreSavageryCard, w.Me, Zone.Hand);
            var oni = Put(s, "training-seeds5-shadowlands-oni", w.Me, w);
            var enemy = Put(s, "training-lion-samurai", w.Foe, w);
            return new(w.Me, "ogre-savagery-bow", card.Id, enemy.Id, oni.Id);
        }, (before, e, f, w) =>
        {
            Check(e.State.Cards[f.Target!].Bowed, "the enemy Personality is bowed");
            Check(!e.State.Cards[f.Performer!].Bowed, "the performer is not");
        });
        var tab = Table(Phase.Battle, 1);
        var card = Add(tab, OgreSavageryCard, 1, Zone.Hand);
        var human = Add(tab, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(tab, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var eng = MatchEngine.FromTestState(tab);
        Check(!Options(eng, 1, "ogre-savagery-bow", card.Id, human.Id).Any(o => o.Enabled), "a human Personality may not perform it");
    }

    private static void PoisonTheCup() => Windows("w739-poison-cup", (s, w) =>
    {
        var shugenja = PutKw(s, "training-crane-samurai", w.Me, w, "Shugenja");
        var spell = Attach(s, PoisonCup, shugenja);
        var target = Put(s, "training-lion-samurai", w.Foe, w); target.Tokens.Add(new CardToken("w739-token-0", "Honor", "test", "Force", 1));
        return new(w.Me, "w739-poison-cup", spell.Id, target.Id, Extra: [shugenja.Id]);
    }, (before, e, f, w) =>
    {
        Check(e.HasKeyword(f.Extra![0], "Shadowlands"), "the Shugenja has Shadowlands");
        var tokens = e.State.Cards[f.Target!].Tokens;
        Check(tokens.Count == 1 && tokens[0] is { Type: "Plague", Stat: "Chi", Amount: -1 }, "the token was replaced by a -1C Plague token");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor - 1, "1 Honor lost");
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
    });

    private static void RelentlessConviction()
    {
        Windows("relentless-conviction-destroy", (s, w) =>
        {
            var card = Add(s, Conviction, w.Me, Zone.Hand);
            Put(s, "training-crane-samurai", w.Me, w); // a unit of yours at the battlefield
            var shamed = Put(s, "training-lion-samurai", w.Foe, w); shamed.Dishonorable = true;
            Put(s, "training-lion-courtier", w.Foe, w);
            return new(w.Me, "relentless-conviction-destroy", card.Id, shamed.Id);
        }, (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the dishonorable Personality is destroyed");
            Eq(e.State.Cards.Values.Count(c => c.Zone == Zone.Dead), 1, "and nobody else");
        });
        var tab = Table(Phase.Battle, 1);
        var card = Add(tab, Conviction, 1, Zone.Hand);
        Add(tab, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var honorable = Add(tab, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var eng = MatchEngine.FromTestState(tab);
        Check(!Targets(eng, 1, "relentless-conviction-destroy", card.Id).Contains(honorable.Id), "an honorable Personality is no target");
        Check(Rejection(eng, Use(eng, 1, "relentless-conviction-destroy", card.Id, honorable.Id)).Length > 0 && eng.State.Cards[honorable.Id].Zone == Zone.Battlefield, "and is refused");
    }

    private static void DarkNagaRise() => Windows("dark-naga-rise-recruit", (s, w) =>
    {
        var card = Add(s, Naga, w.Me, Zone.Hand);
        Add(s, "training-nonhuman-personality", w.Me, Zone.Home); Add(s, "training-nonhuman-personality", w.Me, Zone.Home);
        var province = s.Players[w.Me].Provinces.First(p => !p.Destroyed && p.CardId is not null);
        var previous = s.Cards[province.CardId!]; previous.Zone = Zone.DynastyDiscard; previous.ProvinceIndex = null; previous.FaceUp = true;
        var recruit = Add(s, "training-nonhuman-personality", w.Me, Zone.Province, province: province.Index);
        province.CardId = recruit.Id;
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "dark-naga-rise-recruit", card.Id, recruit.Id, Province: province.Index, Extra: [recruit.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the Nonhuman entered play at home");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed) > before.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed), "his Gold Cost was paid (sources bowed)");
        var refill = e.State.Players[f.Me].Provinces.First(p => p.Index == f.Province).CardId;
        Check(refill is not null && refill != f.Target && e.State.Cards[refill].FaceUp, "the province refilled face-up");
    });

    private static void SearchingForAnswers() => Windows("p5-searching-look", (s, w) =>
    {
        var card = Add(s, SearchingCard, w.Me, Zone.Hand);
        Put(s, "training-lion-samurai", w.Foe, w);
        var top = Add(s, "training-crane-samurai", w.Me, Zone.DynastyDeck); s.Players[w.Me].DynastyDeck.Insert(0, top.Id);
        s.Players[w.Me].Honor = 10;
        return new(w.Me, "p5-searching-look", card.Id, null, null, Extra: [top.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.Type, "lookArrange", "the look is open");
        Check(e.GetView(f.Me).PendingLook is not null && e.GetView(w.Foe).PendingLook is null, "only the Defender sees the cards");
        var gold = e.State.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed);
        Accept(e, e.QueryActions(f.Me).First(o => o.Type == "arrangeTopCards" && (o.Command!.CardIds ?? []).Contains("0:play")).Command!);
        Settle(e);
        var top = e.State.Cards[f.Extra![0]];
        Check(top.Zone == Zone.Battlefield && top.ProvinceIndex == 0 && top.OwnerId == f.Me, "the Personality is in play at the current battlefield");
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed), gold, "no Gold was paid");
        Batch336Support.RunUntil(e, () => !e.State.UnresolvedBattlefields.Contains(0) || e.State.Cards[top.Id].Zone != Zone.Battlefield);
        Check(e.State.Cards[top.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "he is discarded after the battle ends (or destroyed in it): " + e.State.Cards[top.Id].Zone);
    }, only: ["battle-defender"], why: scenario => scenario == "battle-attacker" ? "Only the Defender" : "timing is unavailable now");

    private static void TseluSearch() => Windows("wl-pc-tselu-search", (s, w) =>
    {
        var tselu = Add(s, TseluExp, w.Me, Zone.Home);
        SetDeck(s, w.Me, Zone.FateDeck, HorseArchers, MotoHouseGuard, "training-follower", "training-item");
        var keep = Add(s, "training-rally", w.Me, Zone.Hand); var toss = Add(s, "training-item", w.Me, Zone.Hand);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl-pc-tselu-search", tselu.Id, Extra: [keep.Id, toss.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opens");
        var eligible = e.GetView(f.Me).PendingSearch!.Entries!.Where(en => en.Eligible).Select(en => en.Definition.Id).ToArray();
        Check(eligible.SequenceEqual([HorseArchers]), "only the non-Unique Cavalry Follower qualifies: " + string.Join(",", eligible));
        Check(e.GetView(w.Foe).PendingSearch is null, "the opponent sees none of it");
        Accept(e, new GameCommand("selectSearchCard", f.Me, e.Version, Choice: HorseArchers));
        var found = e.State.Cards.Values.Single(c => c.DefinitionId == HorseArchers && c.OwnerId == f.Me);
        Check(e.State.Players[f.Me].Hand.Contains(found.Id), "shown and put in the hand");
        Check(e.State.ShownCards.Any(c => c.DefinitionId == HorseArchers), "and shown to the opponent");
        Eq(e.State.PendingChoice?.Type, "searchDiscard", "then discard a card");
        Accept(e, new GameCommand("chooseSearchDiscard", f.Me, e.Version, TargetId: f.Extra![1]));
        Eq(e.State.Cards[f.Extra[1]].Zone, Zone.FateDiscard, "the chosen card is discarded");
        Check(e.State.Players[f.Me].Hand.Contains(f.Extra[0]) && e.State.Players[f.Me].Hand.Contains(found.Id), "the rest of the hand stays");
        Eq(e.State.Players[f.Me].FateDeck.Count, 3, "the Fate deck lost the Follower");
    });

    private static void StandAsTheMountain() => Windows("mountain-straighten", (s, w) =>
    {
        var card = Add(s, MountainCard, w.Me, Zone.Hand);
        var high1 = Put(s, "training-high-force-personality", w.Me, w, bowed: true); var high2 = Put(s, "training-high-force-personality", w.Me, w, bowed: true);
        var low = Put(s, "training-lion-samurai", w.Me, w, bowed: true);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "mountain-straighten", card.Id, Sel: [Pick("targets", high1.Id, high2.Id)], Extra: [high1.Id, high2.Id, low.Id]);
    }, (before, e, f, w) =>
    {
        Check(!e.State.Cards[f.Extra![0]].Bowed && !e.State.Cards[f.Extra[1]].Bowed, "both chosen 7-Force performers are straightened");
        Check(e.State.Cards[f.Extra[2]].Bowed, "the 4-Force Personality, not chosen, stays bowed");
    });

    private static void Sycophants() => Windows("wl-sycophants-straighten", (s, w) =>
    {
        var kodo = Add(s, Kodo, w.Me, Zone.Home, bowed: true);
        var syco = Add(s, SycophantsCard, w.Me, Zone.Attached, parent: kodo.Id);
        var holdings = Enumerable.Range(0, 3).Select(_ => Add(s, "training-border-keep", w.Me, Zone.Home, bowed: true).Id).ToArray();
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl-sycophants-straighten", syco.Id, Extra: [kodo.Id, .. holdings]);
    }, (before, e, f, w) =>
    {
        var kodo = f.Extra![0];
        Check(!e.State.Cards[kodo].Bowed, "Straighten this Personality"); Check(e.State.Cards[f.Card].Bowed, "bowing the Follower is the cost");
        Check(e.WlItemsMiscExtraUsesForTest(kodo, "kodo-straighten") > 0, "his once-a-turn Political action may be used a second time");
        if (e.State.PriorityPlayerId != f.Me) Accept(e, Pass(e));
        Accept(e, Batch336Support.Offer(e, f.Me, "kodo-straighten", kodo, f.Extra[1]));
        if (e.State.PriorityPlayerId != f.Me) Accept(e, Pass(e));
        Accept(e, Batch336Support.Offer(e, f.Me, "kodo-straighten", kodo, f.Extra[2]));
        if (e.State.PriorityPlayerId != f.Me) Accept(e, Pass(e));
        Check(!e.State.Cards[f.Extra[1]].Bowed && !e.State.Cards[f.Extra[2]].Bowed, "he used it twice this turn");
        Check(Options(e, f.Me, "kodo-straighten", kodo).All(o => !o.Enabled), "but not a third time");
    });

    private static void CourageOfTheMantis() => Windows("courage-mantis-move", (s, w) =>
    {
        var card = Add(s, CourageCard, w.Me, Zone.Hand);
        var mantis = Add(s, "training-moshi-ikako", w.Me, Zone.Home);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "courage-mantis-move", card.Id, null, mantis.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Performer!].Zone, Zone.Battlefield, "the Mantis moved to the current battlefield"); Eq(e.State.Cards[f.Performer!].ProvinceIndex, (int?)0);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == f.Me), "an additional Battle action is granted");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void ThunderResounds() => Windows("tf-thunder-resounds-copy", (s, w) =>
    {
        var shugenja = Put(s, "training-shugenja", w.Me, w); var resounds = Attach(s, Resounds, shugenja);
        var fire = Put(s, "training-seeds5-fire-shugenja", w.Foe, w); var tempest = Attach(s, TempestOfFlame, fire);
        return new(w.Me, "tf-thunder-resounds-copy", resounds.Id, tempest.Id);
    }, (before, e, f, w) =>
    {
        var granted = e.State.Cards[f.Card].GrantedAbilities.Single();
        Eq(granted.AbilityId, "tempest-of-flame-attack"); Eq(granted.Duration, GrantDuration.Turn); Eq(granted.CopiedFromName, "Tempest of Flame");
        Check(e.AbilityIdsForTest(f.Card).Contains("tempest-of-flame-attack"), "the Spell now has the copied ability");
    });

    private static void ThunderousLegion()
    {
        Windows("thunderous-legion-destroy", (s, w) =>
        {
            var wielder = Put(s, "training-crane-samurai", w.Me, w); var legion = Attach(s, LegionCard, wielder);
            var bare = Put(s, "training-lion-samurai", w.Foe, w);
            var armed = Put(s, "training-lion-courtier", w.Foe, w); Attach(s, "training-follower", armed);
            return new(w.Me, "thunderous-legion-destroy", legion.Id, bare.Id, Extra: [armed.Id]);
        }, (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the bare enemy Personality is destroyed");
            Check(e.State.Cards[f.Extra![0]].Zone != Zone.Dead, "the Personality that has an attachment was untouched");
        });
        var tab = Table(Phase.Battle, 1);
        var wield = Add(tab, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var leg = Add(tab, LegionCard, 1, Zone.Attached, province: 0, parent: wield.Id);
        var arm = Add(tab, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var fol = Add(tab, "training-follower", 0, Zone.Attached, province: 0, parent: arm.Id);
        var eng = MatchEngine.FromTestState(tab);
        var targets = Targets(eng, 1, "thunderous-legion-destroy", leg.Id);
        Check(!targets.Contains(arm.Id), "a Personality that has an attachment is no target: " + string.Join(",", targets));
        Check(Rejection(eng, Use(eng, 1, "thunderous-legion-destroy", leg.Id, arm.Id)).Length > 0 && eng.State.Cards[arm.Id].Zone == Zone.Battlefield, "and is refused");
        Check(targets.Contains(fol.Id), "his attachment-free Follower is a target");
    }

    private static void AdjacentBooster() => Windows("training-adjacent-boost", (s, w) =>
    {
        s.UnresolvedBattlefields = [0, 1, 2];
        var booster = Put(s, "training-adjacent-booster", w.Me, w);
        var next = Add(s, "training-lion-samurai", w.Foe, Zone.Battlefield, province: 1);
        Add(s, "training-lion-courtier", w.Foe, Zone.Battlefield, province: 2); // two provinces away: not adjacent
        Put(s, "training-crane-samurai", w.Foe, w);
        return new(w.Me, "training-adjacent-boost", booster.Id, next.Id, Extra: [next.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.CardForceForTest(f.Target!), CardCatalog.Get("training-lion-samurai").Force + 2, "+2F for the Personality at the adjacent battlefield");
        var far = e.State.Cards.Values.Single(c => c.DefinitionId == "training-lion-courtier" && c.Zone == Zone.Battlefield && c.ProvinceIndex == 2);
        Eq(e.CardForceForTest(far.Id), CardCatalog.Get("training-lion-courtier").Force, "nobody else");
    });

    private static void UnexpectedDiscovery() => Windows("w741-discovery", (s, w) =>
    {
        s.UnresolvedBattlefields = [0, 1];
        var card = Add(s, DiscoveryCard, w.Me, Zone.Hand);
        var mine = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "w741-discovery", card.Id, mine.Id, Province: 1);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield); Eq(e.State.Cards[f.Target!].ProvinceIndex, (int?)1);
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void WrathfulDead()
    {
        Windows("wrathful-dead-destroy", (s, w) =>
        {
            var dead = Put(s, DeadCard, w.Me, w);
            var shamed = Put(s, "training-lion-samurai", w.Foe, w); shamed.Dishonorable = true;
            var honored = Put(s, "training-lion-courtier", w.Foe, w);
            return new(w.Me, "wrathful-dead-destroy", dead.Id, shamed.Id, Extra: [honored.Id]);
        }, (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the attachment-free Personality led by a dishonorable Personality is destroyed");
            Check(e.State.Cards[f.Extra![0]].Zone != Zone.Dead, "an honorable unit is untouched");
        });
        var tab = Table(Phase.Battle, 1);
        var deadman = Add(tab, DeadCard, 1, Zone.Battlefield, province: 0);
        var shame = Add(tab, "training-lion-samurai", 0, Zone.Battlefield, province: 0); shame.Dishonorable = true;
        var fol = Add(tab, "training-follower", 0, Zone.Attached, province: 0, parent: shame.Id);
        var fine = Add(tab, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var eng = MatchEngine.FromTestState(tab);
        var targets = Targets(eng, 1, "wrathful-dead-destroy", deadman.Id);
        Check(targets.Contains(fol.Id) && !targets.Contains(shame.Id) && !targets.Contains(fine.Id), "a bare Follower in a dishonorable unit is a target, the armed leader and an honorable unit are not: " + string.Join(",", targets));
        Accept(eng, Batch336Support.Offer(eng, 1, "wrathful-dead-destroy", deadman.Id, fol.Id));
        Check(eng.State.Cards[fol.Id].Zone != Zone.Attached && eng.State.Cards[shame.Id].Zone == Zone.Battlefield, "only the Follower was destroyed");
    }

    private static void YourClanNeedsYou() => Windows("your-clan-needs-you", (s, w) =>
    {
        var card = Add(s, ClanNeedsYou, w.Me, Zone.Hand);
        var recruit = Add(s, "training-nonhuman-personality", w.Me, Zone.Province, province: 1);
        s.Players[w.Me].Provinces[1].CardId = recruit.Id;
        s.Players[0].Provinces[0].Strength = 0;
        var enemy = Put(s, "training-nonhuman-personality", w.Foe, w);
        enemy.Modifiers = [new("test-fixture", "test-fixture", "Force", -2)]; // Force 2 -> 0, so the recruit survives
        return new(w.Me, "your-clan-needs-you", card.Id, recruit.Id, Extra: [recruit.Id]);
    }, (before, e, f, w) =>
    {
        var recruit = e.State.Cards[f.Target!];
        Eq(recruit.Zone, Zone.Battlefield, "brought into play at the current battlefield"); Eq(recruit.ProvinceIndex, (int?)0);
        Check(!recruit.Bowed, "he enters play straightened");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
        Batch336Support.RunUntil(e, () => e.State.PendingChoice?.Type == "yourClanNeedsYouProvince");
        Eq(e.State.PendingChoice?.PlayerId, f.Me, "after the battle ends its player chooses a province");
        Accept(e, new GameCommand("chooseYourClanNeedsYouProvince", f.Me, e.Version, Province: 2));
        Eq(e.State.Cards[f.Target!].Zone, Zone.Province, "he is put into one of your provinces, face-up"); Check(e.State.Cards[f.Target!].FaceUp, "face-up");
        Eq(e.State.Players[f.Me].Provinces[2].CardId, f.Target);
    });
}
