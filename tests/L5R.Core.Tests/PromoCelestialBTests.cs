using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Promotional-Celestial group B (strategy-wave-9201, celestial-wave-9202): A Brave New World, Economic Manipulation, Folklore, Gaining Experience and Fukurokujin's Wisdom. Every position is built explicitly
// and driven only through QueryActions and the commands it offers (engine side: src/L5R.Core/PromoCelestialB.cs).
internal static class PromoCelestialBTests
{
    private const string BraveNewWorld = "oracle-l5r-4", Economic = "oracle-l5r-2279", Folklore = "oracle-l5r-2621", Gaining = "oracle-l5r-2760", Fukurokujin = "oracle-l5r-2727";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool Has(MatchEngine e, string id, string keyword) => e.State.Cards[id].GrantedKeywords.Any(k => k.Keyword == keyword);

    public static void Run(TestRunner runner) => runner.Run("promo-celestial-B", [
        ("Published shape: the five cards print their scans' stats, keywords, text and ability ids, ship their art and are in their manifests", PublishedShape),
        ("A Brave New World: a performing Personality from home moves to the current battlefield and his unit is straightened; the discarded card is another card of the hand; the Strategy is spent", BraveMoves),
        ("A Brave New World, edges: it cannot discard itself, needs another card, takes only the player's own Personality, and a performer already at the battlefield neither moves nor straightens", BraveEdges),
        ("A Brave New World: a move negated by an effect leaves him where he was, bowed (Champion-style negation of movement)", BraveNegated),
        ("Folklore, Battle/Open: -3F to an unaligned Personality (until the end of the turn); an aligned one is not a legal target; usable as an Open action in the Action Phase", FolkloreForce),
        ("Folklore, Battle: a performing Human Personality moves a Nonhuman home; a Nonhuman performer, a Human target and an opponent's performer are rejected without any change", FolkloreMove),
        ("Gaining Experience: a performing Samurai permanently gains Cavalry or Tactician (one of the two); a non-Samurai or another player's Personality is not a legal performer", GainingExperience),
        ("Economic Manipulation: costs 4, 3 while the player controls a Merchant; destroys a Holding of base cost 4 or less whose controller controls five or more Holdings, and no other", EconomicManipulation),
        ("Economic Manipulation, edges: the rich controller's expensive Holding, and a poor controller's Holding are not offered; a Merchant of the opponent's gives no discount", EconomicEdges),
        ("Fukurokujin's Wisdom, trait: +1 maximum hand size for its controller only, and only while in play", FukurokujinHandSize),
        ("Fukurokujin's Wisdom, Battle: discard it, move one or more performing Personalities at the battlefield home and straighten the unit of each (attachments too); no performer, a performer at home or an opponent's is rejected", FukurokujinMove),
    ]);

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, params string[] ids)
        {
            Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0)), string.Join(",", ids), $"{wave}'s manifest");
            foreach (var id in ids)
            {
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
                Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
                Check(!CardCatalog.Get(id).ScenarioOnly, $"{id} is a published card");
            }
            var sources = File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json"));
            Check(sources.Contains("image_sha256") && sources.Contains("Promotional&ndash;Celestial"), $"{wave}: the receipts record each scan's sha256 and the Promotional-Celestial printing");
        }
        var brave = CardCatalog.Get(BraveNewWorld);
        Check(brave.Type == CardType.Strategy && brave.GoldCost == 0 && brave.Focus == 4 && brave.Name == "A Brave New World", $"Brave: {brave.Type} {brave.GoldCost}/{brave.Focus}");
        Eq(brave.Text, "Battle: Choose your performing Personality and discard a card: Move your Personality to the current battlefield. Straighten his unit if he moved.");
        Eq(string.Join(",", brave.Abilities ?? []), "promo-b-brave-new-world-move");
        var eco = CardCatalog.Get(Economic);
        Check(eco.Type == CardType.Strategy && eco.GoldCost == 4 && eco.Focus == 2, $"Economic: {eco.GoldCost}/{eco.Focus}");
        Eq(string.Join(",", eco.Keywords ?? []), "Kolat"); Eq(eco.Effect, "promo-b-economic-manipulation-cost");
        Eq(eco.Text, "This card has -1 Gold Cost if you control a Merchant.\nLimited: Target a Holding with a base Gold Cost of 4 or less controlled by a player who controls five or more Holdings: Destroy it.");
        var folk = CardCatalog.Get(Folklore);
        Check(folk.Type == CardType.Strategy && folk.GoldCost == 0 && folk.Focus == 4, $"Folklore: {folk.GoldCost}/{folk.Focus}");
        Eq(string.Join(",", folk.Abilities ?? []), "promo-b-folklore-force,promo-b-folklore-move");
        Eq(AbilityCatalog.All["promo-b-folklore-force"].Timing, "BattleOrOpen"); Eq(AbilityCatalog.All["promo-b-folklore-move"].Timing, "Battle");
        var gain = CardCatalog.Get(Gaining);
        Check(gain.Type == CardType.Strategy && gain.GoldCost == 0 && gain.Focus == 4, $"Gaining Experience: {gain.GoldCost}/{gain.Focus}");
        Eq(string.Join(",", gain.Abilities ?? []), "promo-b-gaining-experience-cavalry,promo-b-gaining-experience-tactician");
        Eq(gain.Text, "Open: Choose your performing Samurai: Permanently give him either Cavalry or Tactician.");
        Manifest("strategy-wave-9201", Economic, Folklore, Gaining, BraveNewWorld); // the manifest is sorted by id text
        var fuku = CardCatalog.Get(Fukurokujin);
        Check(fuku.Type == CardType.Celestial && fuku.GoldCost == 0 && fuku.Name == "Fukurokujin's Wisdom", $"Fukurokujin: {fuku.Type}");
        Eq(string.Join(",", fuku.Keywords ?? []), "Unique,Fortune"); Eq(fuku.Effect, "increase-own-hand-size"); Eq(string.Join(",", fuku.Abilities ?? []), "promo-b-fukurokujin-move-home");
        Eq(fuku.Text, "You have +1 maximum hand size.\nBattle: Discard this card and choose one or more of your performing Personalities: Move them home. Straighten the unit of each of them who moved.");
        Manifest("celestial-wave-9202", Fukurokujin);
    }

    // ---- A Brave New World ----------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Strategy, CardInstance Performer, CardInstance Fodder) BraveRig(bool performerHome = true, bool bowed = true, bool withEnemy = true)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var performer = performerHome ? Add(s, "training-crane-samurai", 1, Zone.Home, bowed: bowed) : Add(s, "training-crane-samurai", 1, Zone.Battlefield, bowed: bowed, province: 0);
        if (withEnemy) Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, BraveNewWorld, 1, Zone.Hand);
        var fodder = Add(s, "training-lion-samurai", 1, Zone.Hand);
        return (MatchEngine.FromTestState(s), strategy, performer, fodder);
    }

    private static void BraveMoves()
    {
        var (e, strategy, performer, fodder) = BraveRig();
        var s0 = e.State;
        var attachment = Add(s0, "training-nonhuman-personality", 1, Zone.Home); // a second card: a plain Personality stays put; the unit's attachment case is tested with Fukurokujin
        e = MatchEngine.FromTestState(s0);
        Check(e.State.Cards[performer.Id].Zone == Zone.Home && e.State.Cards[performer.Id].Bowed, "he starts bowed at home");
        Accept(e, Offer(e, 1, "promo-b-brave-new-world-move", strategy.Id, fodder.Id, performer.Id));
        Eq(e.State.Cards[performer.Id].Zone, Zone.Battlefield, "he moved to the battlefield");
        Eq(e.State.Cards[performer.Id].ProvinceIndex, 0);
        Check(!e.State.Cards[performer.Id].Bowed, "and his unit was straightened");
        Eq(e.State.Cards[fodder.Id].Zone, Zone.FateDiscard, "the chosen card was discarded as the cost");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Check(e.State.Cards[attachment.Id].Zone == Zone.Home, "nothing else moved");
    }

    private static void BraveEdges()
    {
        var (e, strategy, performer, fodder) = BraveRig();
        // Not itself: naming the Strategy as the card to discard is rejected with nothing changed.
        var offered = Offer(e, 1, "promo-b-brave-new-world-move", strategy.Id, fodder.Id, performer.Id);
        Check(!Options(e, 1, "promo-b-brave-new-world-move", strategy.Id, performer.Id).Any(o => o.TargetIds?.Contains(strategy.Id) ?? false), "the Strategy itself is not offered as the card to discard");
        Reject(e, offered with { TargetId = strategy.Id });
        // The opponent's Personality is not a performer.
        var s = e.State; var enemy = Add(s, "training-lion-samurai", 0, Zone.Home); e = MatchEngine.FromTestState(s);
        Reject(e, offered with { PerformerId = enemy.Id });
        // Only the Strategy in hand: nothing to discard.
        var lone = BraveRig(); var ls = lone.E.State; ls.Players[1].Hand.Remove(lone.Fodder.Id); ls.Cards.Remove(lone.Fodder.Id);
        Check(!CanUse(MatchEngine.FromTestState(ls), 1, "promo-b-brave-new-world-move", lone.Strategy.Id), "with no other card to discard the action is not on offer");
        // A performer already at the battlefield is a legal performer, but neither moves nor straightens: still bowed.
        var (at, atStrategy, atPerformer, atFodder) = BraveRig(performerHome: false);
        Accept(at, Offer(at, 1, "promo-b-brave-new-world-move", atStrategy.Id, atFodder.Id, atPerformer.Id));
        Check(at.State.Cards[atPerformer.Id].Bowed && at.State.Cards[atPerformer.Id].Zone == Zone.Battlefield, "he did not move, so his unit is not straightened");
        Eq(at.State.Cards[atFodder.Id].Zone, Zone.FateDiscard, "the cost was still paid");
        // Absent for either player: no unit of the player's at the battlefield is needed (the enemy alone stands there).
        Check(CanUse(e, 1, "promo-b-brave-new-world-move", strategy.Id, performer.Id), "offered with only the enemy at the battlefield");
    }

    private static void BraveNegated()
    {
        var (e, strategy, performer, fodder) = BraveRig();
        var s = e.State;
        s.NegatedEffects.Add(new("move", performer.Id));
        e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "promo-b-brave-new-world-move", strategy.Id, fodder.Id, performer.Id));
        Eq(e.State.Cards[performer.Id].Zone, Zone.Home, "the negated move left him at home");
        Check(e.State.Cards[performer.Id].Bowed, "and nothing was straightened");
    }

    // ---- Folklore -------------------------------------------------------------------------------------------------------------------------------------

    private static void FolkloreForce()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var unaligned = Add(s, "training-discard-attacher", 0, Zone.Battlefield, province: 0);
        var aligned = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Battle actions need a unit of the player's at the battle
        var strategy = Add(s, Folklore, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 1, "promo-b-folklore-force", strategy.Id).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(unaligned.Id) && !option.TargetIds!.Contains(aligned.Id), "only the unaligned Personality is a legal target");
        Reject(e, option.Command! with { TargetId = aligned.Id });
        Accept(e, Offer(e, 1, "promo-b-folklore-force", strategy.Id, unaligned.Id));
        Check(e.State.Cards[unaligned.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -3), "he has -3F");
        Check(!e.State.Cards[aligned.Id].Modifiers.Any(), "the aligned one is untouched");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // As an Open action in the Action Phase.
        var o = Table(Phase.Action, 0); o.PriorityPlayerId = 0;
        var target = Add(o, "training-nonhuman-personality", 1, Zone.Home); // Unaligned, Nonhuman: unaligned is all the first half asks
        var open = Add(o, Folklore, 0, Zone.Hand);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 0, "promo-b-folklore-force", open.Id, target.Id));
        Check(oe.State.Cards[target.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -3), "the Open half works in the Action Phase");
    }

    private static void FolkloreMove()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var human = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var oni = Add(s, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0);
        var man = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, Folklore, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 1, "promo-b-folklore-move", strategy.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(man.Id) ?? false)), "a Human is not a legal target");
        var command = Offer(e, 1, "promo-b-folklore-move", strategy.Id, oni.Id, human.Id);
        Reject(e, command with { TargetId = man.Id });
        var enemyPerformer = command with { PerformerId = oni.Id };
        Reject(e, enemyPerformer);
        Accept(e, command);
        Eq(e.State.Cards[oni.Id].Zone, Zone.Home, "the Nonhuman Personality moved home");
        Eq(e.State.Cards[man.Id].Zone, Zone.Battlefield, "the Human stayed");
        Eq(e.State.Cards[human.Id].Zone, Zone.Battlefield, "and so did the performer");
        // A Nonhuman performer is not offered.
        var n = Table(Phase.Battle, 1); n.PriorityPlayerId = 1;
        var nonhuman = Add(n, "training-nonhuman-personality", 1, Zone.Battlefield, province: 0);
        var target = Add(n, "training-seeds5-shadowlands-oni", 0, Zone.Battlefield, province: 0);
        var ns = Add(n, Folklore, 1, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(n), 1, "promo-b-folklore-move", ns.Id), "only a Human may perform it");
    }

    // ---- Gaining Experience ---------------------------------------------------------------------------------------------------------------------------

    private static void GainingExperience()
    {
        foreach (var (ability, keyword, other) in new[] { ("promo-b-gaining-experience-cavalry", "Cavalry", "Tactician"), ("promo-b-gaining-experience-tactician", "Tactician", "Cavalry") })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var samurai = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(samurai, "Samurai");
            var plain = Add(s, "training-lion-samurai", 0, Zone.Home);
            var enemySamurai = Add(s, "training-lion-samurai", 1, Zone.Home); GiveKeyword(enemySamurai, "Samurai");
            var strategy = Add(s, Gaining, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(CanUse(e, 0, ability, strategy.Id, samurai.Id), "a Samurai is a legal performer");
            Check(!CanUse(e, 0, ability, strategy.Id, plain.Id), "a Personality that is not a Samurai is not");
            Check(!CanUse(e, 0, ability, strategy.Id, enemySamurai.Id), "another player's Samurai is not");
            var command = Enabled(e, 0, ability, strategy.Id, samurai.Id).Command!;
            Reject(e, command with { PerformerId = plain.Id });
            Reject(e, command with { PerformerId = enemySamurai.Id });
            Accept(e, command);
            Check(e.State.Cards[samurai.Id].GrantedKeywords.Any(k => k.Keyword == keyword && k.Duration == GrantDuration.Permanent), $"he permanently has {keyword}");
            Check(!Has(e, samurai.Id, other), $"and not {other}: it is one or the other");
            Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        }
    }

    // ---- Economic Manipulation ------------------------------------------------------------------------------------------------------------------------

    // The economy scenario starts with Holdings of its own; the counting tests need to place every one.
    private static MatchState BareTable(Phase phase, int actor)
    {
        var s = Table(phase, actor);
        foreach (var holding in s.Cards.Values.Where(c => CardCatalog.Get(c.DefinitionId).Type == CardType.Holding).Select(c => c.Id).ToArray()) s.Cards.Remove(holding);
        return s;
    }

    private static (MatchState S, CardInstance Strategy, CardInstance Castle, CardInstance Expensive) EconomicTable(bool merchant = false, bool enemyMerchant = false, int gold = 2)
    {
        var s = BareTable(Phase.Action, 0); s.PriorityPlayerId = 0;
        for (var i = 0; i < gold; i++) Add(s, "training-farm", 0, Zone.Home);                   // 2 Gold each
        var castle = Add(s, "training-castle", 1, Zone.Home);                                  // base cost 2
        for (var i = 0; i < 4; i++) Add(s, "training-farm", 1, Zone.Home);                     // five Holdings for player 1 in all
        var expensive = Add(s, "training-expensive-holding", 1, Zone.Home);                    // base cost 6: six Holdings, but this one is too dear
        if (merchant) Add(s, "training-merchant", 0, Zone.Home);
        if (enemyMerchant) Add(s, "training-merchant", 1, Zone.Home);
        var strategy = Add(s, Economic, 0, Zone.Hand);
        return (s, strategy, castle, expensive);
    }

    private static int Cost(ActionOption option, string target) => option.TargetGold is { } tg && tg.TryGetValue(target, out var q) ? q.Cost : option.Gold!.Cost;

    private static void EconomicManipulation()
    {
        var (s, strategy, castle, expensive) = EconomicTable();
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "promo-b-economic-manipulation-destroy", strategy.Id);
        Check(option.TargetIds!.Contains(castle.Id) && !option.TargetIds!.Contains(expensive.Id), "the cheap Holding of the rich controller is a target, his expensive one is not");
        Eq(Cost(option, castle.Id), 4, "it costs its printed 4 Gold without a Merchant");
        Accept(e, Offer(e, 0, "promo-b-economic-manipulation-destroy", strategy.Id, castle.Id));
        Eq(e.State.Cards[castle.Id].Zone, Zone.DynastyDiscard, "the Holding was destroyed");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Eq(e.State.Cards[expensive.Id].Zone, Zone.Home, "nothing else was destroyed");
        // With a Merchant it costs 3 -- priced as played, the same quote the engine charges.
        var (ms, mStrategy, mCastle, _) = EconomicTable(merchant: true);
        var me = MatchEngine.FromTestState(ms);
        var mOption = Enabled(me, 0, "promo-b-economic-manipulation-destroy", mStrategy.Id);
        Eq(Cost(mOption, mCastle.Id), 3, "-1 Gold Cost while its controller controls a Merchant");
        Accept(me, Offer(me, 0, "promo-b-economic-manipulation-destroy", mStrategy.Id, mCastle.Id));
        Eq(me.State.Cards[mCastle.Id].Zone, Zone.DynastyDiscard, "the discounted play resolved");
        // Her own Holdings count for her: a player may target her own Holding when she controls five or more.
        var (os, oStrategy, _, _) = EconomicTable();
        for (var i = 0; i < 3; i++) Add(os, "training-farm", 0, Zone.Home);                    // five Holdings of her own (two producers + three)
        var own = Add(os, "training-castle", 0, Zone.Home);
        var oe = MatchEngine.FromTestState(os);
        Check(Enabled(oe, 0, "promo-b-economic-manipulation-destroy", oStrategy.Id).TargetIds!.Contains(own.Id), "any player's Holdings count, the performer's own included");
    }

    private static void EconomicEdges()
    {
        // Four Holdings: the controller is not rich enough.
        var s = BareTable(Phase.Action, 0); s.PriorityPlayerId = 0;
        for (var i = 0; i < 2; i++) Add(s, "training-farm", 0, Zone.Home);
        var poor = Add(s, "training-castle", 1, Zone.Home);
        for (var i = 0; i < 3; i++) Add(s, "training-farm", 1, Zone.Home);
        var strategy = Add(s, Economic, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "promo-b-economic-manipulation-destroy", strategy.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(poor.Id) ?? false)), "four Holdings is not five");
        // Only the player's own Merchant discounts: the opponent's gives nothing.
        var (es, eStrategy, eCastle, _) = EconomicTable(enemyMerchant: true);
        var ee = MatchEngine.FromTestState(es);
        Eq(Cost(Enabled(ee, 0, "promo-b-economic-manipulation-destroy", eStrategy.Id), eCastle.Id), 4, "another player's Merchant does not lower the cost");
    }

    // ---- Fukurokujin's Wisdom -------------------------------------------------------------------------------------------------------------------------

    private static void FukurokujinHandSize()
    {
        var s = Table(Phase.Action, 0);
        var before = MatchEngine.FromTestState(s);
        Eq(before.MaxHandSizeForTest(0), 8, "the printed hand size"); Eq(before.MaxHandSizeForTest(1), 8);
        var celestial = Add(s, Fukurokujin, 0, Zone.Celestial);
        var e = MatchEngine.FromTestState(s);
        Eq(e.MaxHandSizeForTest(0), 9, "+1 maximum hand size for its controller"); Eq(e.MaxHandSizeForTest(1), 8, "and nobody else");
        var gone = e.State; gone.Cards[celestial.Id].Zone = Zone.DynastyDiscard;
        Eq(MatchEngine.FromTestState(gone).MaxHandSizeForTest(0), 8, "not once it has left play");
    }

    private static void FukurokujinMove()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var celestial = Add(s, Fukurokujin, 1, Zone.Celestial);
        var a = Add(s, "training-crane-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var attached = Add(s, "training-lion-samurai", 1, Zone.Attached, bowed: true, province: 0, parent: a.Id);
        var b = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var home = Add(s, "training-crane-samurai", 1, Zone.Home, bowed: true);
        var enemy = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 1, "promo-b-fukurokujin-move-home", celestial.Id).First(o => o.Enabled);
        var offered = option.Slots!.Single(x => x.Slot == "performers").CardIds;
        Check(offered.Contains(a.Id) && offered.Contains(b.Id) && !offered.Contains(home.Id) && !offered.Contains(enemy.Id), "only the player's Personalities at the battlefield are candidates");
        Reject(e, Use(e, 1, "promo-b-fukurokujin-move-home", celestial.Id, null, null));                                       // no performer
        Reject(e, Use(e, 1, "promo-b-fukurokujin-move-home", celestial.Id, null, null, Pick("performers", enemy.Id)));         // the opponent's
        Reject(e, Use(e, 1, "promo-b-fukurokujin-move-home", celestial.Id, null, null, Pick("performers", home.Id)));          // at home
        Accept(e, Use(e, 1, "promo-b-fukurokujin-move-home", celestial.Id, null, null, Pick("performers", a.Id, b.Id)));
        Eq(e.State.Cards[a.Id].Zone, Zone.Home, "the first performer moved home"); Eq(e.State.Cards[b.Id].Zone, Zone.Home, "and the second");
        Check(!e.State.Cards[a.Id].Bowed && !e.State.Cards[b.Id].Bowed, "their units were straightened");
        Check(!e.State.Cards[attached.Id].Bowed, "including the attached card");
        Check(e.State.Cards[celestial.Id].Zone != Zone.Celestial, "the Celestial was discarded as the cost");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Battlefield, "the enemy was not moved");
        Check(e.State.Cards[home.Id].Bowed, "a Personality that was not a performer was not straightened");
    }
}
