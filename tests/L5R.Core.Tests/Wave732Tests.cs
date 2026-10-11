using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #732, wave batch 1 (src/L5R.Core/Wave732.cs; waves personality-wave-146, follower-wave-59, item-wave-59, spell-wave-66, strategy-wave-130, holding-wave-64): the payment and cost cards,
// proved on the published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the Crane, player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave732Tests
{
    private const string Veil = "oracle-l5r-10350", Truce = "oracle-l5r-10401", Dojo = "oracle-l5r-10432", Chambers = "oracle-l5r-10436", Asukai = "oracle-l5r-10444", Inoue = "oracle-l5r-10464",
        Sengmai = "oracle-l5r-10499", Kikumura = "oracle-l5r-10659", Embargo = "oracle-l5r-10723", Treasures = "oracle-l5r-10751", Court = "oracle-l5r-10752", Kurohime = "oracle-l5r-10840",
        BlackRiders = "oracle-l5r-10892", Coin = "oracle-l5r-10911", Copse = "oracle-l5r-2391", Axe = "oracle-l5r-10340", Duty = "oracle-l5r-10370";
    private const string Keep = "training-border-keep", Marketplace = "training-marketplace", CraneSamurai = "training-crane-samurai", Subject = "w732-subject", Conscripts = "oracle-l5r-10004",
        Archive = "oracle-l5r-9802", Kiho = "oracle-l5r-7557";
    private static readonly string[] All = [Veil, Truce, Dojo, Chambers, Asukai, Inoue, Sengmai, Kikumura, Embargo, Treasures, Court, Kurohime, BlackRiders, Coin, Copse];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static CardDefinition Def(CardInstance c) => c.Printed ?? CardCatalog.Get(c.DefinitionId);

    public static void Run(TestRunner runner) => runner.Run("wave-732", [
        ("Published as printed: fifteen cards in six profiles with their stats, production, abilities and effects; the two deferred cards are not published", PublishedShape),
        ("Tsujiken's Coin: an attached Item that bows to produce 2 Gold for a payment", CoinProduces),
        ("Opening the Veil: the Stronghold produces 1 more for each Spirit Personality or Follower of its owner, for each Veil", VeilProduction),
        ("Truce: a hand Strategy that discards itself to produce 2 Gold, only for a Recruit of a Personality aligned with another Clan", TruceToken),
        ("Ever-Winter Copse: bows for 1 Gold only beside another Holding and only for a Nonhuman; never suggested alone", CopseToken),
        ("The Amethyst Court: produces 2 itself, or bows a Courtier or Artisan of yours for his Chi", CourtToken),
        ("Colonial Dojo: pays the printed cost of a non-Unique Samurai Recruit and nothing else", DojoProduction),
        ("Suikihime's Chambers: 2 Gold, or 4 for a Unique Personality; Limited, bow: gain 1 Honor once", ChambersProduction),
        ("Trade Embargo: Open, 4 Gold, every non-Unique Holding produces 1 less this turn; one copy per turn", TradeEmbargo),
        ("Uncovering Treasures: Limited, 2 Gold puts it into play; then it bows for 2 Gold and is discarded", UncoveringTreasures),
        ("Morito Inoue: straightens and lends another player's cheaper Holding for the turn; a Holding costing his Force or more is no target", InoueLends),
        ("Black Riders: the same with Gold Cost up to the Follower's Force, and a lent Whispering Archive discounts the borrower's Spells", RidersLend),
        ("Doji Kurohime: straightens an Honor-producing Holding that has not produced Gold this turn", KurohimeStraightens),
        ("Asukai: enters play free with the Ring of Earth; other players' cards do not affect his controller's Provinces", AsukaiRingAndProvinces),
        ("Sengmai: free with the Ring of Fire, will not enter play at 0 Honor or above, and puts a Kiho from the discard pile into the hand", SengmaiEntry),
        ("Daigotsu Kikumura: Invest 4 creates two 1F Followers on him; 2 for a Dragon Clan player; optional, once, only on entering play", KikumuraInvest),
    ]);

    // ---- rigs ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Dynasty phase, player 0 (Crane) active, every printed producer of player 0 bowed (so the only Gold is what a test adds), and province 0 holds a face-up fixture Personality.
    private static (MatchState State, CardInstance Province) Rig(Func<CardDefinition, CardDefinition>? tweak = null)
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var province = s.Cards[s.Players[0].Provinces[0].CardId!];
        province.DefinitionId = CraneSamurai; province.FaceUp = true;
        var def = CardCatalog.Get(CraneSamurai) with { Id = Subject, Keywords = ["Samurai"], GoldCost = 3, HonorRequirement = null };
        province.Printed = tweak is null ? def : tweak(def);
        return (s, province);
    }

    private static GameCommand Purchase(MatchEngine e, CardInstance province, params string[] sources) => new("purchase", 0, e.Version, CardId: province.Id, SourceIds: sources);
    private static bool Entered(MatchEngine e, CardInstance province) => e.State.Cards[province.Id].Zone == Zone.Home;
    private static string[][] Suggestions(MatchEngine e, CardInstance province) => e.QuotePurchase(0, province.Id).Payments.Select(p => p.SourceIds.ToArray()).ToArray();
    private static bool Offers(MatchEngine e, CardInstance province, string sourceId) => e.QuotePurchase(0, province.Id).Sources!.Any(x => x.Id == sourceId);

    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }

    // Whether an enabled offer of the ability on the card lists the target, and a hand-built command that names it anyway.
    private static bool Offered(MatchEngine e, string ability, string cardId, string targetId) =>
        Batch336Support.Options(e, 0, ability, cardId).Any(a => a.Enabled && (a.TargetIds?.Contains(targetId) ?? false));
    private static GameCommand Forged(MatchEngine e, string ability, string cardId, string targetId) => new("useAbility", 0, e.Version, CardId: cardId, TargetId: targetId, AbilityId: ability);

    private static CardInstance Stronghold(MatchState s, int player) => s.Cards.Values.First(c => c.OwnerId == player && c.Zone == Zone.Stronghold);
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, null, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }

    // ---- the tests ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect, int Production)>
        {
            [Veil] = (CardType.Spell, [], "none", 0),
            [Truce] = (CardType.Strategy, [], "none", 0),
            [Dojo] = (CardType.Holding, [], "none", 0),
            [Chambers] = (CardType.Holding, ["w732-chambers-honor"], "none", 2),
            [Asukai] = (CardType.Personality, [], "none", 0),
            [Inoue] = (CardType.Personality, ["w732-inoue-lend"], "none", 0),
            [Sengmai] = (CardType.Personality, ["w732-sengmai-kiho"], "will-not-enter-honor-nonnegative", 0),
            [Kikumura] = (CardType.Personality, ["w732-kikumura-invest"], "none", 0),
            [Embargo] = (CardType.Strategy, ["w732-embargo"], "none", 0),
            [Treasures] = (CardType.Strategy, ["w732-treasures-play"], "none", 0),
            [Court] = (CardType.Holding, [], "none", 2),
            [Kurohime] = (CardType.Personality, ["w732-kurohime-straighten"], "none", 0),
            [BlackRiders] = (CardType.Follower, ["w732-riders-lend"], "none", 0),
            [Coin] = (CardType.Item, [], "none", 2),
            [Copse] = (CardType.Holding, [], "none", 0)
        };
        Eq(expected.Count, 15, "fifteen cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect && d.GoldProduction == want.Production, $"{d.Name} is published as wired");
            Check(!string.IsNullOrWhiteSpace(d.Text) && !d.CreatedOnly, $"{d.Name} prints its text and is playable");
            Check(AbilityCatalog.All.Keys.Where(k => k.StartsWith("w732-", StringComparison.Ordinal)).Count() == 8, "eight w732 abilities");
        }
        var inoue = CardCatalog.Get(Inoue); var kikumura = CardCatalog.Get(Kikumura); var asukai = CardCatalog.Get(Asukai);
        Check(inoue.Force == 3 && inoue.Chi == 3 && inoue.PersonalHonor == 1 && inoue.GoldCost == 6, "Morito Inoue's stats");
        Check(kikumura.Force == 2 && kikumura.Chi == 3 && kikumura.GoldCost == 4, "Kikumura's stats");
        Check(asukai.Force == 3 && asukai.Chi == 3 && asukai.PersonalHonor == 2 && asukai.GoldCost == 4 && asukai.HonorRequirement == 2, "Asukai's stats");
        Check(CardCatalog.Cards.ContainsKey(Axe) && !CardCatalog.Cards.ContainsKey(Duty), "Chiyurei's Axe was wired afterwards (Wave827.cs, issue #827); Duty Over All Things is deferred, not published");
        Check(AbilityCatalog.All["w732-kikumura-invest"].Cost == GoldCostRule.Fixed(4) && AbilityCatalog.All["w732-kikumura-invest"].Trigger == "own-enters-play", "Invest 4 on entering play");
        Check(TraitReactions.Is("w732-kikumura-invest") && TraitReactions.Is("w732-sengmai-kiho") && !TraitReactions.Is("w732-inoue-lend"), "the two traits are traits");
    }

    private static void CoinProduces()
    {
        var (s, province) = Rig(d => d with { GoldCost = 2 });
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var coin = Add(s, Coin, 0, Zone.Attached, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(coin.Id, 0), 2, "the Coin produces 2 Gold");
        Check(Suggestions(e, province).Any(p => p.SequenceEqual([coin.Id])), "the Coin alone pays 2");
        Accept(e, Purchase(e, province, coin.Id));
        Check(Entered(e, province) && e.State.Cards[coin.Id].Bowed && !e.State.Cards[host.Id].Bowed, "the Coin bowed, not its wielder");
        // Bowed, it produces nothing.
        var (b, bprovince) = Rig(d => d with { GoldCost = 2 });
        var bhost = Add(b, CraneSamurai, 0, Zone.Home); var bcoin = Add(b, Coin, 0, Zone.Attached, true, null, bhost.Id);
        var bowed = MatchEngine.FromTestState(b);
        Check(!Offers(bowed, bprovince, bcoin.Id), "a bowed Coin is not a source");
        NoChange(bowed, Purchase(bowed, bprovince, bcoin.Id), "a bowed Coin");
    }

    private static void VeilProduction()
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        var stronghold = Stronghold(s, 0);
        var baseline = MatchEngine.FromTestState(s).GoldProduced(stronghold.Id, 0);
        var shugenja = Fx(s, CraneSamurai, "w732-shugenja", 0, Zone.Home, d => d with { Keywords = ["Shugenja"] });
        Fx(s, CraneSamurai, "w732-spirit-1", 0, Zone.Home, d => d with { Keywords = ["Spirit"] });
        Fx(s, CraneSamurai, "w732-spirit-2", 0, Zone.Home, d => d with { Keywords = ["Spirit", "Samurai"] });
        Fx(s, CraneSamurai, "w732-human", 0, Zone.Home, d => d with { Keywords = ["Samurai"] });
        Fx(s, CraneSamurai, "w732-enemy-spirit", 1, Zone.Home, d => d with { Keywords = ["Spirit"] });
        Eq(MatchEngine.FromTestState(s).GoldProduced(stronghold.Id, 0), baseline, "no Veil in play: no change");
        var veil = Add(s, Veil, 0, Zone.Attached, parent: shugenja.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(stronghold.Id, 0), baseline + 2, "+1 for each of the two Spirits of yours");
        Eq(e.GoldProduced(Stronghold(s, 1).Id, 1), MatchEngine.FromTestState(Batch336Support.Table(Phase.Dynasty, 0)).GoldProduced(Stronghold(s, 1).Id, 1), "the other player's Stronghold is untouched");
        // A second Veil stacks; a Veil in the hand does nothing.
        var s2 = e.State; Add(s2, Veil, 0, Zone.Attached, parent: shugenja.Id);
        Eq(MatchEngine.FromTestState(s2).GoldProduced(stronghold.Id, 0), baseline + 4, "each Veil counts the Spirits");
        s2.Cards[veil.Id].Zone = Zone.Hand; s2.Cards[veil.Id].AttachedTo = null;
        Eq(MatchEngine.FromTestState(s2).GoldProduced(stronghold.Id, 0), baseline + 2, "only the Veil still in play counts");
    }

    private static void TruceToken()
    {
        // A Lion Personality for the Crane player: Truce is offered, pays 2, and is discarded as it pays.
        var (s, province) = Rig(d => d with { GoldCost = 2, Clan = "Lion", Keywords = ["Samurai", "Lion Clan"] });
        var truce = Add(s, Truce, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var token = truce.Id + "#truce";
        var source = e.QuotePurchase(0, province.Id).Sources!.Single(x => x.Id == token);
        Check(source.Kind == "hand" && source.Produced == 2, "Truce is a hand source producing 2");
        Check(Suggestions(e, province).Any(p => p.SequenceEqual([token])), "Truce alone pays the 2 Gold");
        Check(!JsonSerializer.Serialize(e.GetView(1)).Contains(truce.Id, StringComparison.Ordinal), "the other seat never sees it");
        Accept(e, Purchase(e, province, token));
        Check(Entered(e, province) && !e.State.Players[0].Hand.Contains(truce.Id) && e.State.Cards[truce.Id].Zone == Zone.FateDiscard, "Truce left the hand for the Fate discard pile");
        // A Personality of the player's own Clan, or of no Clan, does not qualify.
        foreach (var (clan, keywords) in new[] { ("Crane", new[] { "Samurai", "Crane Clan" }), ("Unaligned", new[] { "Samurai" }) })
        {
            var (a, aprovince) = Rig(d => d with { GoldCost = 2, Clan = clan, Keywords = keywords });
            var atruce = Add(a, Truce, 0, Zone.Hand);
            var other = MatchEngine.FromTestState(a);
            Check(!Offers(other, aprovince, atruce.Id + "#truce"), $"Truce is not offered for a {clan} Personality");
            NoChange(other, Purchase(other, aprovince, atruce.Id + "#truce"), $"a {clan} Personality");
        }
        // Truce is a printed Reaction: Cherry Blossom Festival bars it.
        var (c, cprovince) = Rig(d => d with { GoldCost = 2, Clan = "Lion", Keywords = ["Samurai", "Lion Clan"] });
        var ctruce = Add(c, Truce, 0, Zone.Hand);
        var festival = CardCatalog.Cards.Values.FirstOrDefault(d => d.Name == "Cherry Blossom Festival" && !d.ScenarioOnly);
        if (festival is not null)
        {
            Add(c, festival.Id, 1, Zone.EventInPlay);
            Check(!Offers(MatchEngine.FromTestState(c), cprovince, ctruce.Id + "#truce"), "Cherry Blossom Festival bars the Reaction");
        }
    }

    private static void CopseToken()
    {
        var (s, province) = Rig(d => d with { Keywords = ["Samurai", "Nonhuman"], GoldCost = 3 });
        var keep = Add(s, Keep, 0, Zone.Home);
        var copse = Add(s, Copse, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var token = copse.Id + "#copse";
        Eq(e.QuotePurchase(0, province.Id).Cost, 3, "the price");
        Check(Offers(e, province, token), "the Copse is listed for a Nonhuman");
        Check(Suggestions(e, province).Length > 0 && Suggestions(e, province).All(p => !p.Contains(token) || p.Contains(keep.Id)), "no suggestion names the Copse without another Holding");
        NoChange(e, Purchase(e, province, token), "the Copse alone");
        Accept(e, Purchase(e, province, keep.Id, token));
        Check(Entered(e, province) && e.State.Cards[copse.Id].Bowed && e.State.Cards[keep.Id].Bowed, "the payment bowed the Holding and the Copse");
        // Not a Nonhuman: not offered. No other Holding: nothing pays.
        var (h, hprovince) = Rig(d => d with { GoldCost = 3 });
        Add(h, Keep, 0, Zone.Home); Add(h, Copse, 0, Zone.Home);
        Check(!MatchEngine.FromTestState(h).QuotePurchase(0, hprovince.Id).Sources!.Any(x => x.Id.EndsWith("#copse", StringComparison.Ordinal)), "the Copse is for Nonhumans only");
        var (a, aprovince) = Rig(d => d with { Keywords = ["Samurai", "Nonhuman"], GoldCost = 3 });
        var alone = Add(a, Copse, 0, Zone.Home);
        var lone = MatchEngine.FromTestState(a);
        Check(Suggestions(lone, aprovince).Length == 0, "nothing pays: the Copse needs another Holding");
        NoChange(lone, Purchase(lone, aprovince, alone.Id + "#copse"), "no other Holding");
    }

    private static void CourtToken()
    {
        var (s, province) = Rig(d => d with { GoldCost = 3 });
        var court = Add(s, Court, 0, Zone.Home);
        var courtier = Fx(s, CraneSamurai, "w732-courtier", 0, Zone.Home, d => d with { Keywords = ["Courtier"], Chi = 3 });
        var artisan = Fx(s, CraneSamurai, "w732-artisan", 0, Zone.Home, d => d with { Keywords = ["Artisan"], Chi = 1 });
        Fx(s, CraneSamurai, "w732-samurai", 0, Zone.Home, d => d with { Keywords = ["Samurai"], Chi = 9 });
        Fx(s, CraneSamurai, "w732-enemy-courtier", 1, Zone.Home, d => d with { Keywords = ["Courtier"], Chi = 9 });
        Fx(s, CraneSamurai, "w732-bowed-courtier", 0, Zone.Home, d => d with { Keywords = ["Courtier"], Chi = 8 }, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var sources = e.QuotePurchase(0, province.Id).Sources!.Where(x => x.Id.StartsWith(court.Id + "#court:", StringComparison.Ordinal)).ToArray();
        Eq(sources.Length, 2, "one token for each unbowed Courtier or Artisan of yours");
        Check(sources.Select(x => x.Produced).Order().SequenceEqual([1, 3]), "each produces his Chi");
        Eq(e.GoldProduced(court.Id, 0), 2, "the Court's own production is 2");
        var token = court.Id + "#court:" + courtier.Id;
        // The Court's own bow and its token are two ways to bow one card.
        NoChange(e, Purchase(e, province, court.Id, token), "the Court bows only once");
        Accept(e, Purchase(e, province, token));
        Check(Entered(e, province) && e.State.Cards[court.Id].Bowed && e.State.Cards[courtier.Id].Bowed && !e.State.Cards[artisan.Id].Bowed, "the Court and the Courtier are bowed, the Artisan is not");
        // The Court alone pays 2 for a 2-cost card.
        var (t, tprovince) = Rig(d => d with { GoldCost = 2 });
        var tcourt = Add(t, Court, 0, Zone.Home);
        var plain = MatchEngine.FromTestState(t);
        Accept(plain, Purchase(plain, tprovince, tcourt.Id));
        Check(Entered(plain, tprovince) && plain.State.Cards[tcourt.Id].Bowed, "the printed 2 Gold");
    }

    private static void DojoProduction()
    {
        var (s, province) = Rig(d => d with { Keywords = ["Samurai"], GoldCost = 4 });
        var dojo = Add(s, Dojo, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Suggestions(e, province).Any(p => p.SequenceEqual([dojo.Id])), "the Dojo alone pays a 4-cost Samurai");
        Accept(e, Purchase(e, province, dojo.Id));
        Check(Entered(e, province) && e.State.Cards[dojo.Id].Bowed, "he entered play for 0 Gold of his own");
        foreach (var (name, tweak) in new (string, Func<CardDefinition, CardDefinition>)[]
        {
            ("a Unique Samurai", d => d with { Keywords = ["Samurai", "Unique"], GoldCost = 4 }),
            ("a non-Samurai", d => d with { Keywords = ["Shugenja"], GoldCost = 4 })
        })
        {
            var (a, aprovince) = Rig(tweak);
            var adojo = Add(a, Dojo, 0, Zone.Home);
            var other = MatchEngine.FromTestState(a);
            Check(!Offers(other, aprovince, adojo.Id), $"the Dojo is no source for {name}");
            NoChange(other, Purchase(other, aprovince, adojo.Id), name);
        }
        // It produces nothing for an action.
        Eq(MatchEngine.FromTestState(s).GoldProduced(dojo.Id, 0), 0, "no payment subject: nothing");
    }

    private static void ChambersProduction()
    {
        var (s, province) = Rig(d => d with { Keywords = ["Samurai", "Unique"], GoldCost = 4 });
        var chambers = Add(s, Chambers, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Suggestions(e, province).Any(p => p.SequenceEqual([chambers.Id])), "4 Gold alone pays a 4-cost Unique Personality");
        Accept(e, Purchase(e, province, chambers.Id));
        Check(Entered(e, province), "paid by the Chambers alone");
        var (a, aprovince) = Rig(d => d with { Keywords = ["Samurai"], GoldCost = 4 });
        var achambers = Add(a, Chambers, 0, Zone.Home);
        var plain = MatchEngine.FromTestState(a);
        Eq(plain.GoldProduced(achambers.Id, 0, new PaymentSubject(plain.State.Cards[aprovince.Id], PaymentKind.Recruit)), 2, "2 Gold for a non-Unique Personality");
        NoChange(plain, Purchase(plain, aprovince, achambers.Id), "2 Gold does not pay 4");
        // Limited, bow: gain 1 Honor.
        var table = Batch336Support.Table(Phase.Action, 0);
        var home = Add(table, Chambers, 0, Zone.Home);
        var run = MatchEngine.FromTestState(table);
        var honor = Batch336Support.Honor(run, 0);
        Accept(run, Batch336Support.Use(run, 0, "w732-chambers-honor", home.Id));
        Eq(Batch336Support.Honor(run, 0), honor + 1, "1 Honor");
        Check(run.State.Cards[home.Id].Bowed, "bowed as the cost");
        Check(!Batch336Support.CanUse(run, 0, "w732-chambers-honor", home.Id), "once, and not while bowed");
    }

    private static void TradeEmbargo()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var mine = Add(s, Keep, 0, Zone.Home); var theirs = Add(s, Keep, 1, Zone.Home);
        var unique = Add(s, Court, 0, Zone.Home);
        var embargo = Add(s, Embargo, 0, Zone.Hand);
        var second = Add(s, Embargo, 0, Zone.Hand);
        for (var i = 0; i < 4; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var before = (mine: e.GoldProduced(mine.Id, 0), theirs: e.GoldProduced(theirs.Id, 1), unique: e.GoldProduced(unique.Id, 0));
        Check(before.mine > 0, "the Keep produces Gold");
        var command = Batch336Support.Use(e, 0, Wave732Abilities.Embargo, embargo.Id);
        Accept(e, command with { SourceIds = e.PaymentOptions(0, 4).First().SourceIds });
        Eq(e.GoldProduced(mine.Id, 0), before.mine - 1, "your non-Unique Holding produces 1 less");
        Eq(e.GoldProduced(theirs.Id, 1), before.theirs - 1, "so does the other player's");
        Eq(e.GoldProduced(unique.Id, 0), before.unique, "a Unique Holding does not");
        Check(e.State.Cards[embargo.Id].Zone == Zone.FateDiscard, "the Strategy was spent");
        // One copy per turn.
        Check(!Batch336Support.CanUse(e, 0, Wave732Abilities.Embargo, second.Id), "a second copy cannot be played this turn");
        Check(e.State.EmbargoPlayTurns is not null && e.State.EmbargoPlayTurns[0] == e.State.TurnNumber, "the turn is recorded");
        // A Holding that enters later this turn is affected too (the modifier is read live) and the record is omitted from a match that never used it.
        Check(!JsonSerializer.Serialize(MatchEngine.FromTestState(Batch336Support.Table(Phase.Action, 0)).State).Contains("EmbargoPlayTurns", StringComparison.Ordinal), "the store is omitted until used");
        var copy = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!);
        Check(!Batch336Support.CanUse(copy, 0, Wave732Abilities.Embargo, second.Id) && copy.GoldProduced(mine.Id, 0) == before.mine - 1, "a saved game continues the same");
    }

    private static void UncoveringTreasures()
    {
        // In the hand it produces nothing; played, it sits in play.
        var s = Batch336Support.Table(Phase.Action, 0);
        var treasures = Add(s, Treasures, 0, Zone.Hand);
        for (var i = 0; i < 4; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var play = Batch336Support.Use(e, 0, Wave732Abilities.TreasuresPlay, treasures.Id);
        Accept(e, play with { SourceIds = e.PaymentOptions(0, 2).First().SourceIds });
        Check(e.State.Cards[treasures.Id].Zone == Zone.EventInPlay && !e.State.Players[0].Hand.Contains(treasures.Id), "put into play, not discarded");
        Check(!Batch336Support.CanUse(e, 0, Wave732Abilities.TreasuresPlay, treasures.Id), "it can be put into play only from the hand");
        // Then it bows for 2 Gold and is discarded as it pays.
        var (r, province) = Rig(d => d with { GoldCost = 2 });
        var inPlay = Add(r, Treasures, 0, Zone.EventInPlay);
        var pay = MatchEngine.FromTestState(r);
        var token = inPlay.Id + "#treasures";
        Check(Offers(pay, province, token), "an in-play Uncovering Treasures is a source");
        Accept(pay, Purchase(pay, province, token));
        Check(Entered(pay, province) && pay.State.Cards[inPlay.Id].Zone == Zone.FateDiscard, "it paid 2 and was discarded");
        // In the hand it is no source.
        var (h, hprovince) = Rig(d => d with { GoldCost = 2 });
        var inHand = Add(h, Treasures, 0, Zone.Hand);
        Check(!Offers(MatchEngine.FromTestState(h), hprovince, inHand.Id + "#treasures"), "a Strategy in the hand is not a source");
    }

    private static void InoueLends()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var inoue = Add(s, Inoue, 0, Zone.Home);
        var cheap = Fx(s, Keep, "w732-cheap-holding", 1, Zone.Home, d => d with { GoldCost = 2, GoldProduction = 3 }, bowed: true);
        var equal = Fx(s, Keep, "w732-equal-holding", 1, Zone.Home, d => d with { GoldCost = 3, GoldProduction = 3 }, bowed: true);
        var mine = Fx(s, Keep, "w732-own-holding", 0, Zone.Home, d => d with { GoldCost = 1 }, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Eq(Call(e, "CardForce", e.State.Cards[inoue.Id]), 3, "Inoue's Force");
        Check(!Offered(e, Wave732Abilities.InoueLend, inoue.Id, equal.Id), "a Holding costing his Force is not offered");
        NoChange(e, Forged(e, Wave732Abilities.InoueLend, inoue.Id, equal.Id), "a Holding costing his Force is not lower");
        Check(!Offered(e, Wave732Abilities.InoueLend, inoue.Id, mine.Id), "your own Holding is not offered");
        NoChange(e, Forged(e, Wave732Abilities.InoueLend, inoue.Id, mine.Id), "your own Holding");
        Accept(e, Batch336Support.Offer(e, 0, Wave732Abilities.InoueLend, inoue.Id, cheap.Id));
        Check(!e.State.Cards[cheap.Id].Bowed, "straightened");
        Check(!e.State.Cards[inoue.Id].Bowed, "Limited, no bow");
        Check(e.State.LentProducers.Any(l => l.CardId == cheap.Id && l.BorrowerId == 0), "lent to you");
        Check(Call(e, "ProducerControlledBy", e.State.Cards[cheap.Id], 0) is true && Call(e, "ProducerControlledBy", e.State.Cards[cheap.Id], 1) is false, "its Gold is yours, not its owner's");
        Eq(e.GoldProduced(cheap.Id, 0), 3, "it produces its 3 Gold for you");
        Check(!Batch336Support.CanUse(e, 0, Wave732Abilities.InoueLend, inoue.Id), "Limited: once per turn");
    }

    private static void RidersLend()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var riders = Add(s, BlackRiders, 0, Zone.Attached, parent: host.Id);
        var equal = Fx(s, Keep, "w732-equal-holding", 1, Zone.Home, d => d with { GoldCost = 2, GoldProduction = 3 }, bowed: true);
        var dear = Fx(s, Keep, "w732-dear-holding", 1, Zone.Home, d => d with { GoldCost = 3, GoldProduction = 3 }, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Eq(Call(e, "CardForce", e.State.Cards[riders.Id]), 2, "the Follower's Force");
        Check(!Offered(e, Wave732Abilities.RidersLend, riders.Id, dear.Id), "a dearer Holding is not offered");
        NoChange(e, Forged(e, Wave732Abilities.RidersLend, riders.Id, dear.Id), "a Holding costing more than its Force");
        // Gold Cost equal to or lower than its Force is a target.
        Accept(e, Batch336Support.Offer(e, 0, Wave732Abilities.RidersLend, riders.Id, equal.Id));
        Check(e.State.LentProducers.Any(l => l.CardId == equal.Id && l.BorrowerId == 0) && !e.State.Cards[equal.Id].Bowed, "straightened and lent");
        // Cost-reducing traits: a lent Whispering Archive discounts its borrower's Spells, and not its owner's (a fresh table: the Follower's Limited action is spent above).
        var t = Batch336Support.Table(Phase.Action, 0);
        var thost = Add(t, CraneSamurai, 0, Zone.Home);
        var triders = Add(t, BlackRiders, 0, Zone.Attached, parent: thost.Id);
        var tarchive = Fx(t, Archive, "w732-archive", 1, Zone.Home, d => d with { GoldCost = 2 }, bowed: true);
        var spell = CardCatalog.Cards.Values.First(c => c.Type == CardType.Spell && !c.ScenarioOnly && c.GoldCost > 0);
        var entering = Add(t, spell.Id, 0, Zone.Hand);
        var other = Add(t, spell.Id, 1, Zone.Hand);
        var fresh = MatchEngine.FromTestState(t);
        Eq(Call(fresh, "WhisperingArchiveDiscount", fresh.State.Cards[entering.Id], 0), 0, "not lent yet: no discount for the borrower");
        Accept(fresh, Batch336Support.Offer(fresh, 0, Wave732Abilities.RidersLend, triders.Id, tarchive.Id));
        Check(fresh.State.LentProducers.Any(l => l.CardId == tarchive.Id && l.BorrowerId == 0), "the Archive is lent");
        Eq(Call(fresh, "WhisperingArchiveDiscount", fresh.State.Cards[entering.Id], 0), 1, "the borrower's Spells enter for 1 less");
        Eq(Call(fresh, "WhisperingArchiveDiscount", fresh.State.Cards[other.Id], 1), 0, "the owner's do not while it is lent");
    }

    private static void KurohimeStraightens()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var kurohime = Add(s, Kurohime, 0, Zone.Home);
        var honor = Add(s, Chambers, 1, Zone.Home, bowed: true);
        var plain = Fx(s, Keep, "w732-plain-holding", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Check(!Offered(e, Wave732Abilities.KurohimeStraighten, kurohime.Id, plain.Id), "a Holding that produces no Honor is not offered");
        NoChange(e, Forged(e, Wave732Abilities.KurohimeStraighten, kurohime.Id, plain.Id), "a Holding that produces no Honor");
        // One that already produced Gold this turn is no target.
        var markedState = e.State; markedState.GoldProducedTurns[honor.Id] = markedState.TurnNumber;
        var (marked, mkurohime, mhonor) = (MatchEngine.FromTestState(markedState), kurohime.Id, honor.Id);
        Check(!Offered(marked, Wave732Abilities.KurohimeStraighten, mkurohime, mhonor), "it produced Gold this turn: not offered");
        NoChange(marked, Forged(marked, Wave732Abilities.KurohimeStraighten, mkurohime, mhonor), "it produced Gold this turn");
        Accept(e, Batch336Support.Offer(e, 0, Wave732Abilities.KurohimeStraighten, kurohime.Id, honor.Id));
        Check(!e.State.Cards[honor.Id].Bowed && e.State.Cards[kurohime.Id].Bowed, "straightened; Kurohime bowed as the cost");
    }

    private static void AsukaiRingAndProvinces()
    {
        // Free with the Ring of Earth, at his printed 4 Gold without it.
        foreach (var ring in new[] { true, false })
        {
            var s = DynastyTable();
            var province = s.Cards[s.Players[0].Provinces[0].CardId!];
            province.DefinitionId = Asukai; province.FaceUp = true;
            if (ring) Add(s, CardCatalog.Cards.Values.First(c => c.Type == CardType.Ring && c.Name == "Ring of Earth" && !c.ScenarioOnly).Id, 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Eq(e.QuotePurchase(0, province.Id).Cost, ring ? 0 : 4, ring ? "ignoring Gold Cost" : "the printed Gold Cost");
        }
        // Other players' cards do not affect his controller's Provinces; his controller's own effects still do.
        var t = Batch336Support.Table(Phase.Action, 0);
        var asukai = Add(t, Asukai, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(t);
        var before = e2.EffectiveProvinceStrengthForTest(0, 0);
        e2.GiveProvinceStrengthForTest(0, 0, -2, 1, "fixture");
        Eq(e2.EffectiveProvinceStrengthForTest(0, 0), before, "the other player's penalty is negated while Asukai is in play");
        e2.GiveProvinceStrengthForTest(0, 0, 2, 0, "fixture");
        Eq(e2.EffectiveProvinceStrengthForTest(0, 0), before + 2, "his controller's own bonus still applies");
        var t2 = Batch336Support.Table(Phase.Action, 0);
        var plain = MatchEngine.FromTestState(t2);
        var base2 = plain.EffectiveProvinceStrengthForTest(0, 0);
        plain.GiveProvinceStrengthForTest(0, 0, -2, 1, "fixture");
        Eq(plain.EffectiveProvinceStrengthForTest(0, 0), Math.Max(0, base2 - 2), "without Asukai the penalty applies");
        Check(asukai.Zone == Zone.Home, "Asukai stayed in play");
    }

    private static MatchState DynastyTable() => Batch336Support.Table(Phase.Dynasty, 0);

    private static void SengmaiEntry()
    {
        // The gate: a Family Honor of 0 or above refuses the Recruit; below 0 is allowed.
        foreach (var (honor, allowed) in new[] { (5, false), (0, false), (-1, true) })
        {
            var s = Batch336Support.Table(Phase.Dynasty, 0);
            var province = s.Cards[s.Players[0].Provinces[0].CardId!];
            province.DefinitionId = Sengmai; province.FaceUp = true; s.Players[0].Honor = honor;
            Add(s, CardCatalog.Cards.Values.First(c => c.Type == CardType.Ring && c.Name == "Ring of Fire" && !c.ScenarioOnly).Id, 0, Zone.Home);
            var quote = MatchEngine.FromTestState(s).QuotePurchase(0, province.Id);
            Check(allowed ? quote.Error is null && quote.Cost == 0 : quote.Error is not null, $"Honor {honor}: {(allowed ? "free with the Ring of Fire" : "refused")} ({quote.Error})");
        }
        // Without the Ring he costs his printed 4.
        var n = Batch336Support.Table(Phase.Dynasty, 0);
        var np = n.Cards[n.Players[0].Provinces[0].CardId!]; np.DefinitionId = Sengmai; np.FaceUp = true; n.Players[0].Honor = -3;
        Eq(MatchEngine.FromTestState(n).QuotePurchase(0, np.Id).Cost, 4, "no Ring: the printed Gold Cost");
        // After he enters play, a Kiho in the discard pile goes to the hand.
        var s2 = MatchEngine.CreateEconomyScenario().State;
        var card = s2.Cards[s2.Players[0].Provinces[0].CardId!]; card.DefinitionId = Sengmai; card.FaceUp = true; s2.Players[0].Honor = -3;
        var kiho = Add(s2, Kiho, 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s2);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.QuotePurchase(0, card.Id).Payments[0].SourceIds));
        while (e.State.PendingChoice?.Type == "proclaim") Accept(e, new GameCommand("declineReaction", 0, e.Version));
        Check(e.State.PendingChoice?.Type is "entryReaction", $"the entering-play window is open ({e.State.PendingChoice?.Type})");
        Accept(e, Batch336Support.Use(e, 0, Wave732Abilities.SengmaiKiho, card.Id));
        Check(e.State.PendingSearch is not null, "the search opens");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: Kiho));
        Check(e.State.Players[0].Hand.Contains(kiho.Id) && e.State.Cards[kiho.Id].Zone == Zone.Hand, "the Kiho is in the hand");
    }

    private static void KikumuraInvest()
    {
        foreach (var (clan, price) in new[] { ("Crane", 4), ("Dragon", 2) })
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            var card = s.Cards[s.Players[0].Provinces[0].CardId!]; card.DefinitionId = Kikumura; card.FaceUp = true; s.Players[0].Clan = clan;
            for (var i = 0; i < 8; i++) Add(s, Marketplace, 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.QuotePurchase(0, card.Id).Payments[0].SourceIds));
            while (e.State.PendingChoice?.Type == "proclaim") Accept(e, new GameCommand("declineReaction", 0, e.Version));
            Check(e.State.PendingChoice?.Type is "entryReaction", "the entering-play window is open");
            var cost = e.QuoteAbilityGold(Batch336Support.Use(e, 0, Wave732Abilities.KikumuraInvest, card.Id));
            Eq(cost?.Cost, price, $"{clan} Clan player pays {price}");
            var cards = e.State.Cards.Count;
            NoChange(e, Batch336Support.Use(e, 0, Wave732Abilities.KikumuraInvest, card.Id), "the Invest unpaid");
            Accept(e, Batch336Support.Use(e, 0, Wave732Abilities.KikumuraInvest, card.Id) with { SourceIds = e.PaymentOptions(0, price).First().SourceIds });
            var followers = e.State.Cards.Values.Where(c => c.DefinitionId == Conscripts && c.Zone == Zone.Attached && c.AttachedTo == card.Id).ToArray();
            Eq(followers.Length, 2, "two 1F Followers attached to Kikumura");
            Eq(e.State.Cards.Count, cards + 2, "exactly two cards are created");
            Check(followers.All(f => Def(f).Force == 1 && Def(f).Type == CardType.Follower), "each is a 1F Follower");
            Check(!Batch336Support.CanUse(e, 0, Wave732Abilities.KikumuraInvest, card.Id), "once");
        }
        // Declining creates nothing; Invest is only for the moment of entering play.
        var d = MatchEngine.CreateEconomyScenario().State;
        var dc = d.Cards[d.Players[0].Provinces[0].CardId!]; dc.DefinitionId = Kikumura; dc.FaceUp = true;
        for (var i = 0; i < 8; i++) Add(d, Marketplace, 0, Zone.Home);
        var decline = MatchEngine.FromTestState(d);
        Accept(decline, new GameCommand("purchase", 0, decline.Version, CardId: dc.Id, SourceIds: decline.QuotePurchase(0, dc.Id).Payments[0].SourceIds));
        while (decline.State.PendingChoice?.Type == "proclaim") Accept(decline, new GameCommand("declineReaction", 0, decline.Version));
        var count = decline.State.Cards.Count;
        Accept(decline, new GameCommand("declineReaction", 0, decline.Version));
        Eq(decline.State.Cards.Count, count, "declining creates nothing");
        var table = Batch336Support.Table(Phase.Action, 0);
        var inPlay = Add(table, Kikumura, 0, Zone.Home);
        Check(!Batch336Support.CanUse(MatchEngine.FromTestState(table), 0, Wave732Abilities.KikumuraInvest, inPlay.Id), "Invest is paid only as the card enters play");
    }
}
