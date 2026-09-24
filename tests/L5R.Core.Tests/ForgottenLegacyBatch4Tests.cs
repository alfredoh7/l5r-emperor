using L5R.Core;
using static RegressionAssertions;

// Forgotten Legacy pass (issue #467), batch 4: the ten Strategies of strategy-wave-55, driven through QueryActions and the commands it offers. Every scenario is
// an explicit position on the economy table (player 0 the Crane, player 1 the Lion), with training fixtures granted whichever keyword a card's text requires.
internal static class ForgottenLegacyBatch4Tests
{
    private const string HeartOfDarkness = "oracle-l5r-3013", Intimidation = "oracle-l5r-3790", NeverBeyondMyReach = "oracle-l5r-5533", PearlsAndSpirits = "oracle-l5r-5906",
        ReturningHome = "oracle-l5r-6299", RiseOfTheDarkNaga = "oracle-l5r-6343", SeekingTheTruth = "oracle-l5r-6566", SiegeVolley = "oracle-l5r-7248",
        SteadfastDefense = "oracle-l5r-7477", SunderingStrike = "oracle-l5r-7650";

    // The six Strategies of the batch left unwired (each with a blocker note in tools/content/build_card_inventory.py): never published.
    private static readonly string[] Unwired = ["oracle-l5r-3426", "oracle-l5r-5648", "oracle-l5r-5939", "oracle-l5r-7696", "oracle-l5r-8257", "oracle-l5r-9208"];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);

    public static void Run(TestRunner runner) => runner.Run("forgotten-legacy-batch-4", [
        ("Published shape: strategy-wave-55 is exactly the ten wired Strategies, each with its catalogue stats, ability ids and art, and the batch's six unwired Strategies are not published", PublishedShape),
        ("Heart of Darkness: played as a Terrain; before the battle resolves its controller bows zero, one or two cards there with 4 or greater Force -- a card with less is refused, and bowing none changes nothing", HeartOfDarknessScenario),
        ("Intimidation: a Berserker at home and another player's Personality at home both move to the current battlefield and each moved unit straightens; a Personality already at the battlefield is not a legal target", IntimidationScenario),
        ("Never Beyond My Reach: with no unit at the battlefield, an unbowed Kensai at an adjacent battlefield with a Weapon makes a Ranged 6 Attack there; without a Weapon he may not perform", NeverBeyondMyReachScenario),
        ("Pearls & Spirits: the sacrifice Attack destroys a human Shugenja as its cost and strikes with his Force plus Chi counted without his destroyed Items; a Nonhuman is only bowed; the Force or Chi Attack uses the chosen stat", PearlsAndSpiritsScenario),
        ("Returning Home: after a battle in which its player destroyed the province as the Attacker it creates a bowed 3-Gold Holding and draws a card; not offered to a player whose attack failed", ReturningHomeScenario),
        ("Rise of the Dark Naga: with two Nonhuman Personalities in play, a face-up Nonhuman in a province enters play at home paying his cost and the province refills face-up; refused with only one Nonhuman", RiseOfTheDarkNagaScenario),
        ("Seeking the Truth: after engaging, its Scout gains Reconnaissance and a non-Unique Battle Strategy returns from the discard pile; the player who then destroys nothing discards a card at random after the battle, one who destroys the army does not", SeekingTheTruthScenario),
        ("Siege Volley: a Ranged 4 Attack, and an Absent strength penalty or bonus to the current province equal to its player's Castles", SiegeVolleyScenario),
        ("Steadfast Defense: straightens its performing Monk; only with a Temple may it also target a Personality or Follower, which its player then bows or straightens", SteadfastDefenseScenario),
        ("Sundering Strike: moves the target enemy Personality home; paying exactly his attachment's Gold Cost minus four destroys that attachment, and any other amount is refused", SunderingStrikeScenario),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Eq(listed.Length, ids.Length, "the manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void Shape(string id, string name, int gold, string keywords, params string[] abilities)
    {
        var card = CardCatalog.Get(id);
        Check(card.Type == CardType.Strategy && card.Name == name && card.GoldCost == gold, $"{name}: {card.Type} {card.Name} {card.GoldCost}");
        Eq(string.Join(",", card.Keywords ?? []), keywords, $"{name}'s keywords");
        Check(card.Abilities!.SequenceEqual(abilities), $"{name}'s abilities: {string.Join(",", card.Abilities!)}");
    }

    private static void PublishedShape()
    {
        var wired = new[] { HeartOfDarkness, Intimidation, NeverBeyondMyReach, PearlsAndSpirits, ReturningHome, RiseOfTheDarkNaga, SeekingTheTruth, SiegeVolley, SteadfastDefense, SunderingStrike };
        Check(PublishedContent.StrategyWave55.Cards.Select(c => c.Id).SequenceEqual(wired), "strategy-wave-55 publishes exactly the wired cards");
        Shape(HeartOfDarkness, "Heart of Darkness", 0, "Terrain", "heart-of-darkness-bow");
        Shape(Intimidation, "Intimidation", 0, "Fear", "intimidation-move");
        Shape(NeverBeyondMyReach, "Never Beyond My Reach", 0, "", "never-beyond-reach-attack");
        Shape(PearlsAndSpirits, "Pearls & Spirits", 0, "Pearl", "pearls-spirits-sacrifice-attack", "pearls-spirits-force-attack", "pearls-spirits-chi-attack");
        Shape(ReturningHome, "Returning Home", 2, "", "returning-home-create");
        Shape(RiseOfTheDarkNaga, "Rise of the Dark Naga", 0, "", "dark-naga-rise-recruit");
        Shape(SeekingTheTruth, "Seeking the Truth", 0, "Recon", "seeking-truth-recon");
        Shape(SiegeVolley, "Siege Volley", 0, "Siege", "siege-volley-attack", "siege-volley-raise", "siege-volley-lower");
        Shape(SteadfastDefense, "Steadfast Defense", 0, "Kiho", "steadfast-defense-straighten");
        Shape(SunderingStrike, "Sundering Strike", 0, "", "sundering-strike");
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and so not published");
        Manifest(Batch336Support.RepoRoot(), "strategy-wave-55", wired);
    }

    // Passes for whoever must act until `stop` holds (the battle phase only).
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 60 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), $"did not reach the expected position in {e.State.Phase} ({e.State.PendingChoice?.Type})");
    }

    private static bool Fought(MatchEngine e, string message) => e.GetView(0).Events.Any(ev => ev.Type == "battleResolved" && ev.Message.Contains(message));

    private static void HeartOfDarknessScenario()
    {
        // Played from the hand as a Terrain (the generic Terrain rule).
        var p = Table(Phase.Battle, 0);
        Add(p, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(p, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var fromHand = Add(p, HeartOfDarkness, 0, Zone.Hand);
        var pe = MatchEngine.FromTestState(p);
        // Offered as a legal action (a published Terrain was once playable by command only, never listed -- issue #467).
        var terrainOption = pe.QueryActions(0).SingleOrDefault(a => a.Type == "playStrategy" && a.CardId == fromHand.Id);
        Check(terrainOption is { Enabled: true, Command: not null }, "Heart of Darkness is offered enabled as a playStrategy action");
        Accept(pe, terrainOption!.Command!);
        Eq(pe.State.Cards[fromHand.Id].Zone, Zone.Battlefield, "Heart of Darkness entered play at the current battlefield");

        // In play at the battlefield, controlled by the Defender (player 0): the Attacker's Force 4 and Force 5 Personalities outfight his Force 3 one.
        (MatchEngine E, CardInstance Lion, CardInstance Veteran, CardInstance Crane) Build()
        {
            var s = Table(Phase.Battle, 1);
            var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var veteran = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
            var crane = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, HeartOfDarkness, 0, Zone.Battlefield, province: 0);
            return (MatchEngine.FromTestState(s), lion, veteran, crane);
        }
        {
            var (e, lion, veteran, crane) = Build();
            PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
            Eq(e.State.PendingChoice!.PlayerId, 0, "the Terrain's controller chooses");
            var offered = e.QueryActions(0).Where(o => o.Type == "beforeBattleOptional").ToArray();
            Check(offered.Any(o => o.Command!.Choice == "decline"), "bowing no card is offered");
            Check(offered.Any(o => o.Command!.TargetId == lion.Id && o.Command.CardIds is null) && offered.Any(o => o.Command!.TargetId == veteran.Id && o.Command.CardIds is null), "each 4+ Force card alone");
            Check(offered.Any(o => o.Command!.TargetId == lion.Id && o.Command.CardIds is [var second] && second == veteran.Id), "and the two together");
            Check(!offered.Any(o => o.Command!.TargetId == crane.Id || o.Command.CardIds?.Contains(crane.Id) == true), "a Force 3 card is not offered");
            Reject(e, new GameCommand("beforeBattleOptional", 0, e.Version, Choice: "use", TargetId: crane.Id));
            Reject(e, new GameCommand("beforeBattleOptional", 0, e.Version, Choice: "use", TargetId: lion.Id, CardIds: [lion.Id]));
            Accept(e, offered.Single(o => o.Command!.TargetId == lion.Id && o.Command.CardIds is [_]).Command!);
            Check(Fought(e, "attacking Force 0, defending Force 3"), "both attackers were bowed before the totals");
            Eq(e.State.Cards[lion.Id].Zone, Zone.Dead, "the Defender won");
        }
        {
            var (e, lion, _, _) = Build();
            PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
            Accept(e, new GameCommand("beforeBattleOptional", 0, e.Version, Choice: "decline"));
            Check(Fought(e, "attacking Force 9, defending Force 3"), "zero cards bowed: nothing changed");
            Check(e.State.Cards[lion.Id].Zone != Zone.Dead, "the Attacker won");
        }
    }

    private static void IntimidationScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Intimidation, 0, Zone.Hand);
        var berserker = Kw(Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true), "Berserker");
        var enemyHome = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var enemyThere = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "intimidation-move", card.Id, berserker.Id);
        Check(option.TargetIds!.Contains(enemyHome.Id) && !option.TargetIds.Contains(enemyThere.Id), "only another player's Personality NOT at the current battlefield is a target");
        Accept(e, option.Command! with { TargetId = enemyHome.Id });
        foreach (var moved in new[] { berserker, enemyHome })
        {
            Check(e.State.Cards[moved.Id].Zone == Zone.Battlefield && e.State.Cards[moved.Id].ProvinceIndex == 0, $"{moved.Id} moved to the current battlefield");
            Check(!e.State.Cards[moved.Id].Bowed, $"{moved.Id}'s unit straightened because it moved");
        }

        // A non-Berserker may not perform.
        var s2 = Table(Phase.Battle, 0);
        var card2 = Add(s2, Intimidation, 0, Zone.Hand);
        var plain = Add(s2, "training-crane-samurai", 0, Zone.Home);
        Add(s2, "training-lion-samurai", 1, Zone.Home);
        Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "intimidation-move", card2.Id, plain.Id), "a Personality who is not a Berserker cannot perform");
    }

    private static void NeverBeyondMyReachScenario()
    {
        MatchState Build(bool weapon, out CardInstance card, out CardInstance kensai, out CardInstance enemy)
        {
            var s = Table(Phase.Battle, 0); s.UnresolvedBattlefields = [0, 1];
            card = Add(s, NeverBeyondMyReach, 0, Zone.Hand);
            kensai = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 1), "Kensai");
            if (weapon) Add(s, "training-weapon", 0, Zone.Attached, province: 1, parent: kensai.Id);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
            enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            return s;
        }
        var e = MatchEngine.FromTestState(Build(true, out var card, out var kensai, out var enemy));
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.ProvinceIndex == 0), 0, "precondition: player 0 has no unit at the current battlefield");
        Accept(e, Offer(e, 0, "never-beyond-reach-attack", card.Id, enemy.Id, kensai.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "the Ranged 6 Attack destroyed the Force 6 target at the current battlefield");
        Eq(e.State.Cards[kensai.Id].ProvinceIndex, (int?)1, "the Kensai never left the adjacent battlefield");

        var e2 = MatchEngine.FromTestState(Build(false, out var card2, out var kensai2, out _));
        Check(!CanUse(e2, 0, "never-beyond-reach-attack", card2.Id, kensai2.Id), "without a Weapon he may not perform from an adjacent battlefield");
    }

    private static void PearlsAndSpiritsScenario()
    {
        // A human Shugenja (3F/3C, +1F from a Weapon) is destroyed as the cost: the Attack's strength is 3 + 3 = 6, his Weapon destroyed with him (the Blood Wave
        // ruling), so a Force 7 target survives it.
        var s = Table(Phase.Battle, 0);
        var card = Add(s, PearlsAndSpirits, 0, Zone.Hand);
        var shugenja = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Shugenja");
        Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: shugenja.Id);
        var seven = Add(s, "training-high-force-personality", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "pearls-spirits-sacrifice-attack", card.Id, seven.Id, shugenja.Id));
        Eq(e.State.Cards[shugenja.Id].Zone, Zone.Dead, "the human Shugenja was destroyed as the cost");
        Check(e.State.Cards[seven.Id].Zone == Zone.Battlefield, "strength 6 (no Item bonus once destroyed) does not reach Force 7");

        // A Nonhuman Shugenja is only bowed; 3 + 3 = 6 destroys a Force 6 target.
        var s2 = Table(Phase.Battle, 0);
        var card2 = Add(s2, PearlsAndSpirits, 0, Zone.Hand);
        var naga = Kw(Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Shugenja", "Nonhuman");
        var six = Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "pearls-spirits-sacrifice-attack", card2.Id, six.Id, naga.Id));
        Check(e2.State.Cards[naga.Id].Zone == Zone.Battlefield && e2.State.Cards[naga.Id].Bowed, "the Nonhuman Shugenja is bowed, not destroyed");
        Eq(e2.State.Cards[six.Id].Zone, Zone.Dead, "Force plus Chi (6) destroyed the Force 6 target");

        // The second action: his Force (5) or his Chi (4). A Ranged Attack targets only a card its strength reaches (RequireLegalAttackTarget), so the Force 5 target
        // is legal for the Force Attack and not for the Chi Attack, which reaches the Force 4 one.
        foreach (var (ability, defId) in new[] { ("pearls-spirits-force-attack", "training-crane-veteran"), ("pearls-spirits-chi-attack", "training-lion-samurai") })
        {
            var s3 = Table(Phase.Battle, 0);
            var card3 = Add(s3, PearlsAndSpirits, 0, Zone.Hand);
            var veteran = Kw(Add(s3, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Shugenja");
            var five = Add(s3, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
            var target = defId == "training-crane-veteran" ? five : Add(s3, defId, 1, Zone.Battlefield, province: 0);
            var e3 = MatchEngine.FromTestState(s3);
            if (ability == "pearls-spirits-chi-attack")
                Check(!Batch336Support.Options(e3, 0, ability, card3.Id, veteran.Id).Any(o => o.Enabled && o.TargetIds!.Contains(five.Id)), "the Chi Attack (4) cannot target the Force 5 card");
            Accept(e3, Offer(e3, 0, ability, card3.Id, target.Id, veteran.Id));
            Check(e3.State.Cards[veteran.Id].Bowed && e3.State.Cards[veteran.Id].Zone == Zone.Battlefield, $"{ability}: the Shugenja bowed and stayed");
            Eq(e3.State.Cards[target.Id].Zone, Zone.Dead, $"{ability}: the target its chosen stat reaches was destroyed");
        }
    }

    private static void ReturningHomeScenario()
    {
        // Player 1 attacks province 0 with overwhelming Force: the defender and the province are destroyed.
        var s = Table(Phase.Battle, 1);
        var attacker = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        attacker.Modifiers.Add(new("test-source", "test", "Force", 30));
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, ReturningHome, 1, Zone.Hand);
        s.Players[1].FateDeck.Add(Add(s, "training-lion-samurai", 1, Zone.FateDeck).Id);
        var e = MatchEngine.FromTestState(s);
        PassUntil(e, () => e.State.PendingChoice?.Type == "afterResolutionReaction");
        Check(e.State.PendingAfterResolution!.ProvinceDestroyedBy == 1, "precondition: the Attacker destroyed the province");
        var holdingsBefore = e.State.Cards.Values.Count(c => c.OwnerId == 1 && c.Zone == Zone.Home && c.DefinitionId == "oracle-l5r-10024");
        var handBefore = e.State.Players[1].Hand.Count;
        Accept(e, Enabled(e, 1, "returning-home-create", card.Id).Command!);
        var created = e.State.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Home && c.DefinitionId == "oracle-l5r-10024").ToArray();
        Eq(created.Length, holdingsBefore + 1, "a Holding was created at home");
        Check(created.All(h => h.Bowed), "it entered play bowed");
        Eq(CardCatalog.Get("oracle-l5r-10024").GoldProduction, 3, "the created Holding produces 3 Gold");
        Eq(e.State.Players[1].Hand.Count, handBefore - 1 + 1, "Returning Home left the hand and a card was drawn");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "Returning Home was played");

        // The Attacker lost: no province destroyed by him, so it is not offered.
        var t = Table(Phase.Battle, 1);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var defender = Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        defender.Modifiers.Add(new("test-source", "test", "Force", 30));
        Add(t, ReturningHome, 1, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        PassUntil(f, () => f.State.CurrentBattleProvince is null);
        Check(!f.GetView(1).Events.Any(ev => ev.Type == "cardEnteredPlay" && ev.Message.Contains("Returning Home")), "nothing was created");
    }

    private static void RiseOfTheDarkNagaScenario()
    {
        MatchState Build(int nonhumansInPlay, out CardInstance card, out CardInstance recruit, out int provinceIndex)
        {
            var s = Table(Phase.Action, 0);
            card = Add(s, RiseOfTheDarkNaga, 0, Zone.Hand);
            for (var i = 0; i < nonhumansInPlay; i++) Add(s, "training-nonhuman-personality", 0, Zone.Home);
            var province = s.Players[0].Provinces.First(p => !p.Destroyed && p.CardId is not null);
            var previous = s.Cards[province.CardId!]; previous.Zone = Zone.DynastyDiscard; previous.ProvinceIndex = null; previous.FaceUp = true;
            recruit = Add(s, "training-nonhuman-personality", 0, Zone.Province, province: province.Index);
            province.CardId = recruit.Id;
            provinceIndex = province.Index;
            return s;
        }
        var e = MatchEngine.FromTestState(Build(2, out var card, out var recruit, out var index));
        var goldBefore = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed);
        Accept(e, Offer(e, 0, "dark-naga-rise-recruit", card.Id, recruit.Id));
        Eq(e.State.Cards[recruit.Id].Zone, Zone.Home, "the Nonhuman entered play at home");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed) > goldBefore, "his Gold Cost was paid (production sources bowed)");
        var refill = e.State.Players[0].Provinces[index].CardId;
        Check(refill is not null && e.State.Cards[refill].FaceUp, "the province refilled face-up");

        var e2 = MatchEngine.FromTestState(Build(1, out var card2, out var recruit2, out _));
        Check(!CanUse(e2, 0, "dark-naga-rise-recruit", card2.Id), "with only one Nonhuman Personality in play it is not playable");
        Reject(e2, new GameCommand("useAbility", 0, e2.Version, card2.Id, AbilityId: "dark-naga-rise-recruit", TargetId: recruit2.Id));
    }

    private static void SeekingTheTruthScenario()
    {
        // Player 0 attacks province 0 with a Scout; the Engage window opens when the battle is selected.
        MatchEngine Build(bool scoutWins, out CardInstance card, out CardInstance scout, out CardInstance battleStrategy, out CardInstance openStrategy)
        {
            var s = Table(Phase.BattleSelection, 0); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            (scoutWins ? scout : enemy).Modifiers.Add(new("test-source", "test", "Force", 30));
            card = Add(s, SeekingTheTruth, 0, Zone.Hand);
            Add(s, "training-lion-samurai", 0, Zone.Hand); Add(s, "training-crane-samurai", 0, Zone.Hand); // hand fillers
            battleStrategy = Add(s, "oracle-l5r-6047", 0, Zone.FateDiscard); // Precision: a non-Unique Strategy with a Battle action
            openStrategy = Add(s, "oracle-l5r-99", 0, Zone.FateDiscard); // A Weak Poison: Open only
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
            return e;
        }
        {
            var e = Build(false, out var card, out var scout, out var battleStrategy, out var openStrategy);
            var option = Enabled(e, 0, "seeking-truth-recon", card.Id, scout.Id);
            var slot = option.Slots!.Single(x => x.Slot == "strategy");
            Check(slot.CardIds.Contains(battleStrategy.Id) && !slot.CardIds.Contains(openStrategy.Id), "only the non-Unique BATTLE Strategy is offered");
            Accept(e, option.Command! with { Selections = [Pick("strategy", battleStrategy.Id)], TargetId = null });
            Check(e.State.Players[0].Hand.Contains(battleStrategy.Id), "the Battle Strategy returned to the hand");
            Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "its player has Reconnaissance at the battlefield");
            var handAfterReaction = e.State.Players[0].Hand.Count;
            PassUntil(e, () => e.State.CurrentBattleProvince is null);
            Eq(e.State.Cards[scout.Id].Zone, Zone.Dead, "precondition: the Scout's army lost and destroyed nothing");
            Eq(e.State.Players[0].Hand.Count, handAfterReaction - 1, "a card was discarded at random once the battle ended");
            Check(e.GetView(0).Events.Any(ev => ev.Type == "randomDiscard"), "the random discard is announced");
        }
        {
            var e = Build(true, out var card, out var scout, out _, out _);
            Accept(e, Enabled(e, 0, "seeking-truth-recon", card.Id, scout.Id).Command!);
            var handAfterReaction = e.State.Players[0].Hand.Count;
            PassUntil(e, () => e.State.CurrentBattleProvince is null);
            Eq(e.State.Players[0].Hand.Count, handAfterReaction, "the enemy army was destroyed: no discard");
            Check(!e.GetView(0).Events.Any(ev => ev.Type == "randomDiscard"), "and none is announced");
        }
    }

    private static void SiegeVolleyScenario()
    {
        // The Attack.
        var s = Table(Phase.Battle, 0);
        var card = Add(s, SiegeVolley, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "siege-volley-attack", card.Id, enemy.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "the Ranged 4 Attack destroyed the Force 4 target");

        // The Absent strength change: player 0 has no unit at the battlefield and controls two more Castles.
        foreach (var raise in new[] { true, false })
        {
            var t = Table(Phase.Battle, 0);
            var volley = Add(t, SiegeVolley, 0, Zone.Hand);
            Add(t, "training-castle", 0, Zone.Home); Add(t, "training-castle", 0, Zone.Home);
            Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var castles = t.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && (CardCatalog.Get(c.DefinitionId).Keywords ?? []).Contains("Castle"));
            var f = MatchEngine.FromTestState(t);
            var before = f.State.Players[1].Provinces[0].Strength;
            var ability = raise ? "siege-volley-raise" : "siege-volley-lower";
            Accept(f, Enabled(f, 0, ability, volley.Id).Command!);
            Eq(f.State.Players[1].Provinces[0].Strength, before + (raise ? castles : -castles), $"{ability}: changed by the {castles} Castles");
        }
    }

    private static void SteadfastDefenseScenario()
    {
        MatchEngine Build(bool temple, out CardInstance card, out CardInstance monk, out CardInstance enemy)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, SteadfastDefense, 0, Zone.Hand);
            monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true), "Monk");
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (temple) Add(s, "training-temple-holding", 0, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(false, out var card, out var monk, out var enemy);
            var option = Enabled(e, 0, "steadfast-defense-straighten", card.Id, monk.Id);
            Check(option.Slots!.Single(x => x.Slot == "target").CardIds.Length == 0, "without a Temple nothing may be targeted");
            Reject(e, option.Command! with { Selections = [Pick("target", enemy.Id)], TargetId = null });
            Accept(e, option.Command!);
            Check(!e.State.Cards[monk.Id].Bowed, "the Monk straightened");
            Check(e.State.PendingChoice?.Type != "effectChoice" && !e.State.Cards[enemy.Id].Bowed, "no target, no choice");
        }
        foreach (var bow in new[] { true, false })
        {
            var e = Build(true, out var card, out var monk, out var enemy);
            if (!bow) e = Batch336Support.Edit(e, st => st.Cards[enemy.Id].Bowed = true);
            var option = Enabled(e, 0, "steadfast-defense-straighten", card.Id, monk.Id);
            Check(option.Slots!.Single(x => x.Slot == "target").CardIds.Contains(enemy.Id), "with a Temple the enemy Personality is a legal target");
            Accept(e, option.Command! with { Selections = [Pick("target", enemy.Id)], TargetId = null });
            Check(!e.State.Cards[monk.Id].Bowed, "the Monk straightened");
            Eq(e.State.PendingChoice?.Type, "effectChoice", "bow or straighten is chosen now");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == (bow ? "bow" : "straighten")).Command!);
            Eq(e.State.Cards[enemy.Id].Bowed, bow, bow ? "the target bowed" : "the target straightened");
        }
    }

    private static void SunderingStrikeScenario()
    {
        MatchEngine Build(out CardInstance card, out CardInstance enemy, out CardInstance weapon)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, SunderingStrike, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            weapon = Add(s, "training-costly-weapon", 1, Zone.Attached, province: 0, parent: enemy.Id);
            weapon.Modifiers.Add(new("test-source", "test", "GoldCost", 3)); // Gold Cost 3 + 3 = 6: "minus four" is 2
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(out var card, out var enemy, out var weapon);
            var option = Enabled(e, 0, "sundering-strike", card.Id);
            Check(option.Slots!.Single(x => x.Slot == "attachment").CardIds.SequenceEqual([weapon.Id]), "one of HIS attachments is the optional second target");
            Accept(e, option.Command! with { Selections = [Pick("target", enemy.Id)], TargetId = null });
            Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "moved home");
            Eq(e.State.Cards[weapon.Id].AttachedTo, enemy.Id, "no attachment targeted: it stays");
        }
        {
            var e = Build(out var card, out var enemy, out var weapon);
            var command = new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "sundering-strike", Selections: [Pick("target", enemy.Id), Pick("attachment", weapon.Id)]);
            Reject(e, command with { GoldAmount = 0 });
            Reject(e, command with { GoldAmount = 3, SourceIds = e.PaymentOptions(0, 3).First().SourceIds });
            Accept(e, command with { GoldAmount = 2, SourceIds = e.PaymentOptions(0, 2).First().SourceIds });
            Eq(e.State.Cards[weapon.Id].Zone, Zone.FateDiscard, "the attachment was destroyed for its Gold Cost minus four");
            Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "and the Personality moved home");
        }
    }
}
