using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #735, wave batch 4 (src/L5R.Core/Wave735.cs): the keyword-rule, Home and Tireless cards, proved on the published cards themselves. Every position is explicit
// (Batch336Support.Table: player 0 the Crane, player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave735Tests
{
    private const string Hikano = Wave735Abilities.Hikano, Chun = Wave735Abilities.Chun, Gensai = Wave735Abilities.Gensai, Tatsuzo = Wave735Abilities.Tatsuzo, Shakash = Wave735Abilities.Shakash,
        Sorrow = Wave735Abilities.Sorrow, Tsiang = Wave735Abilities.Tsiang, Elephant = Wave735Abilities.Elephant, Scouts = Wave735Abilities.Scouts, Uji = Wave735Abilities.Uji,
        Anchored = Wave735Abilities.Anchored, Infusion = Wave735Abilities.Infusion, Drawing = Wave735Abilities.Drawing, Lash = Wave735Abilities.Lash, Sanction = Wave735Abilities.Sanction, Grasp = Wave735Abilities.Grasp;
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", ShindenAsahina = "oracle-l5r-6855", Marketplace = "training-marketplace", TrainingItem = "training-item",
        Ayumu = "oracle-l5r-3595", CrushingStrength = "oracle-l5r-1589";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null, int? province = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, province, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static CardInstance Now(MatchEngine e, CardInstance card) => e.State.Cards[card.Id];
    private static bool Has(MatchEngine e, CardInstance card, string keyword) => (bool)Call(e, "Keyword", Now(e, card), keyword)!;
    private static int ForceOf(MatchEngine e, CardInstance card) => e.CardForceForTest(card.Id);
    private static string RingId(string name) => CardCatalog.Cards.Values.First(c => c.Type == CardType.Ring && c.Name == name && !c.ScenarioOnly).Id;
    private static bool Offered(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);

    public static void Run(TestRunner runner) => runner.Run("wave-735", [
        ("Published as printed: sixteen cards in five profiles with their stats, abilities, effects and data fields", PublishedShape),
        ("Chuda Hikano: a Home action straightens your Oni from home; Maho Limited destroys an Ikoma Ayumu and then Hikano", HikanoCard),
        ("Chun: Human Personalities in his army (and no others) have Naval", ChunCard),
        ("Daidoji Gensai: only when opposed, moves from home to the battlefield and may discard a card for a Ranged Attack of that Focus Value", GensaiCard),
        ("Doji Tatsuzo: gives its player one Battle action without a unit at a battlefield where the target stands, this turn", TatsuzoCard),
        ("The Shakash - exp: other Nonhuman Personalities and Followers cost 1 less while you control three or fewer Personalities; moves your Personality to his location", ShakashCard),
        ("The Sorrow: Battle -4F, and the same as a Limited action only while you control a Courtier", SorrowCard),
        ("Tsiang, the Benevolent: free with the Ring of Water; a Home Water action straightens one or two cards in units", TsiangCard),
        ("Elephant Cavalry: attaches only to a Human and gives every card in its unit Cavalry", ElephantCard),
        ("Long-Range Scouts: a Home Battle/Open action transfers it to your Personality", ScoutsCard),
        ("Uji's Saboteurs: its Personality has Naval while Crane Clan; Battle destroys an enemy attachment", UjiCard),
        ("Anchored in Earth: a Remote Battle action gives your defending Spirit +3F and Elite from another location", AnchoredCard),
        ("Chikushudo's Infusion: its Shugenja has Cavalry while Shadowlands; Open gives a Nonhuman card Cavalry", InfusionCard),
        ("Drawing on All Things: its Shugenja has the elements of your Rings; Open bows your Ring and straightens him", DrawingCard),
        ("Scorching Lash: its Shugenja has Duelist while Fire; removes a non-Ring card in a discard pile to give +3F or -3F", LashCard),
        ("Suikihime's Sanction: a non-Loyal face-up Personality, in or out of play, permanently gains your Clan alignment", SanctionCard),
        ("The Inevitable Grasp of Conquest: your Personalities have Conqueror; Battle destroys your Province, protects them from other players' cards this battle and discards it", GraspCard),
    ]);

    // ---- fixtures ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState Table(Phase phase, int actor)
    {
        var s = Batch336Support.Table(phase, actor);
        foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = ShindenAsahina;
        s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        return s;
    }

    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PendingChoice!.PlayerId, e.Version));
    }

    // A Personality fixture with explicit stats and keywords.
    private static CardInstance P(MatchState s, string id, int owner, Zone zone, int force, int chi, int honor, string[] keywords, int? province = null, bool bowed = false) =>
        Fx(s, owner == 0 ? CraneSamurai : LionSamurai, id, owner, zone, d => d with { Force = force, Chi = chi, PersonalHonor = honor, Keywords = keywords, Abilities = [], Effect = "none", GoldCost = 3 }, bowed, province: province);

    private static void HandCards(MatchState s, int count = 3)
    {
        for (var player = 0; player < 2; player++) for (var i = 0; i < count; i++) Add(s, TrainingItem, player, Zone.Hand);
    }

    private static CardInstance Attach(MatchState s, string def, int owner, CardInstance host, string? id = null, Func<CardDefinition, CardDefinition>? tweak = null)
    {
        var card = id is null ? Add(s, def, owner, Zone.Attached, parent: host.Id) : Fx(s, def, id, owner, Zone.Attached, tweak, parent: host.Id);
        card.AttachedTo = host.Id; card.ProvinceIndex = host.ProvinceIndex;
        return card;
    }

    private static bool IsAt(CardInstance card, Zone zone, int? province = null) => card.Zone == zone && (province is null || card.ProvinceIndex == province);

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect)>
        {
            [Hikano] = (CardType.Personality, ["w735-hikano-straighten", "w735-hikano-maho"], "none"),
            [Chun] = (CardType.Personality, [], "w735-chun"),
            [Gensai] = (CardType.Personality, ["w735-gensai-move", "w735-gensai-attack"], "none"),
            [Tatsuzo] = (CardType.Personality, ["w735-tatsuzo-grant"], "none"),
            [Shakash] = (CardType.Personality, ["w735-shakash-move"], "w735-shakash"),
            [Sorrow] = (CardType.Personality, ["w735-sorrow-battle", "w735-sorrow-limited"], "none"),
            [Tsiang] = (CardType.Personality, ["w735-tsiang-straighten"], "none"),
            [Elephant] = (CardType.Follower, [], "w735-elephant"),
            [Scouts] = (CardType.Follower, ["w735-scouts-transfer"], "none"),
            [Uji] = (CardType.Follower, ["w735-uji-destroy"], "w735-uji"),
            [Anchored] = (CardType.Spell, ["w735-anchored-boost"], "none"),
            [Infusion] = (CardType.Spell, ["w735-infusion-cavalry"], "w735-infusion"),
            [Drawing] = (CardType.Spell, ["w735-drawing-bow"], "w735-drawing"),
            [Lash] = (CardType.Spell, ["w735-lash-plus", "w735-lash-minus"], "w735-lash"),
            [Sanction] = (CardType.Strategy, ["w735-sanction-grant"], "none"),
            [Grasp] = (CardType.Celestial, ["w735-grasp-destroy"], "w735-grasp"),
        };
        Eq(expected.Count, 16, "sixteen cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect, $"{d.Name} is published as wired");
            Check(!d.CreatedOnly && !string.IsNullOrWhiteSpace(d.Text), $"{d.Name} prints its text and is playable");
        }
        Eq(CardCatalog.Get(Hikano).EntryHonorLoss, 1, "Hikano loses 1 Honor on entering play");
        var elephant = CardCatalog.Get(Elephant);
        Check(elephant.AttachRequiresKeyword == "Human" && elephant.AttackDefenseKind == "Both" && elephant.AttackDefenseAmount == -2 && elephant.AttackDefenseScope == "Unit",
            "Elephant Cavalry: Human only, and Melee and Ranged Attacks on its unit have -2 strength");
        Check(CardCatalog.Get(Anchored).Text.StartsWith("Battle: Even if this unit is not at the current battlefield", StringComparison.Ordinal), "Anchored in Earth prints the scan's wording");
        Eq(AbilityCatalog.All.Keys.Count(k => k.StartsWith("w735-", StringComparison.Ordinal)), 18, "eighteen w735 abilities");
    }

    private static void HikanoCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var hikano = Add(s, Hikano, 0, Zone.Home);
        P(s, "w735-guard", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var oni = P(s, "w735-oni", 0, Zone.Battlefield, 3, 2, 1, ["Nonhuman", "Oni"], province: 0, bowed: true);
        var human = P(s, "w735-human", 0, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0, bowed: true);
        P(s, "w735-foe", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Check(Offered(e, 0, Wave735Abilities.HikanoStraighten, hikano.Id), "a Home action is offered with Hikano at home");
        Check(!Batch336Support.Options(e, 0, Wave735Abilities.HikanoStraighten, hikano.Id).Any(a => a.TargetIds?.Contains(human.Id) == true), "a Human is not an Oni");
        Accept(e, Batch336Support.Offer(e, 0, Wave735Abilities.HikanoStraighten, hikano.Id, oni.Id));
        Settle(e);
        Check(!Now(e, oni).Bowed && Now(e, hikano).Bowed, "the Oni straightened and Hikano bowed for the cost");

        // Maho Limited: the Ikoma Ayumu is destroyed, then Hikano.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var h2 = Add(t, Hikano, 0, Zone.Home);
        var ayumu = Add(t, Ayumu, 1, Zone.Home);
        var other = P(t, "w735-other", 1, Zone.Home, 2, 2, 1, ["Samurai"]);
        HandCards(t);
        var m = MatchEngine.FromTestState(t);
        Check(!Batch336Support.Options(m, 0, Wave735Abilities.HikanoMaho, h2.Id).Any(a => a.TargetIds?.Contains(other.Id) == true), "only an Ikoma Ayumu is a legal target");
        Accept(m, Batch336Support.Use(m, 0, Wave735Abilities.HikanoMaho, h2.Id, ayumu.Id));
        Settle(m);
        Check(!IsAt(Now(m, ayumu), Zone.Home) && !IsAt(Now(m, h2), Zone.Home), "Ayumu and then Hikano were destroyed");
    }

    private static void ChunCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var chun = Add(s, Chun, 0, Zone.Battlefield, province: 0);
        var human = P(s, "w735-human", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var spirit = P(s, "w735-spirit", 0, Zone.Battlefield, 2, 2, 1, ["Nonhuman", "Spirit"], province: 0);
        var away = P(s, "w735-away", 0, Zone.Home, 2, 2, 1, ["Samurai"]);
        var foe = P(s, "w735-foe", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(Has(e, chun, "Naval"), "Chun is Naval as printed");
        Check(Has(e, human, "Naval"), "a Human in his army has Naval");
        Check(!Has(e, spirit, "Naval"), "a Nonhuman in his army does not");
        Check(!Has(e, away, "Naval"), "a Human at home is not in his army");
        Check(!Has(e, foe, "Naval"), "the other player's Human is not in his army");
        var t = Table(Phase.Battle, 0);
        var lone = P(t, "w735-lone", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        Check(!Has(MatchEngine.FromTestState(t), lone, "Naval"), "without Chun nothing is granted");
    }

    private static void GensaiCard()
    {
        MatchEngine Rig(bool opposed, out CardInstance gensai, out CardInstance foe, out CardInstance focus, out CardInstance big)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            gensai = Add(s, Gensai, 0, Zone.Home);
            foe = P(s, "w735-foe", 1, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0);
            big = P(s, "w735-big", 1, Zone.Battlefield, 4, 2, 1, ["Samurai"], province: 0);
            if (!opposed) { s.Cards.Remove(foe.Id); s.Cards.Remove(big.Id); }
            focus = Fx(s, TrainingItem, "w735-focus3", 0, Zone.Hand, d => d with { Focus = 3 });
            Add(s, TrainingItem, 0, Zone.Hand); Add(s, TrainingItem, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        // Not opposed: neither action is offered.
        var calm = Rig(false, out var g0, out _, out _, out _);
        Check(!Offered(calm, 0, Wave735Abilities.GensaiMove, g0.Id) && !Offered(calm, 0, Wave735Abilities.GensaiAttack, g0.Id), "no enemy unit at the battlefield: Gensai would not be opposed");
        // The plain move.
        var e = Rig(true, out var gensai, out var foe, out var focus, out var big);
        Check(Offered(e, 0, Wave735Abilities.GensaiMove, gensai.Id), "opposed: the move is offered from home with no unit of his own at the battlefield");
        Accept(e, Batch336Support.Use(e, 0, Wave735Abilities.GensaiMove, gensai.Id));
        Settle(e);
        Check(IsAt(Now(e, gensai), Zone.Battlefield, 0), "Gensai moved to the current battlefield");
        // The move and the attack: the discarded card's Focus Value is the strength.
        var a = Rig(true, out var g2, out var foe2, out var focus2, out var big2);
        var option = Batch336Support.Options(a, 0, Wave735Abilities.GensaiAttack, g2.Id).FirstOrDefault(o => o.Enabled && o.Command!.CardIds is [var c] && c == focus2.Id && (o.TargetIds?.Contains(foe2.Id) ?? false));
        Check(option is not null, "the attack is offered once per card that can be discarded: " + string.Join("; ", Batch336Support.Options(a, 0, Wave735Abilities.GensaiAttack, g2.Id).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
        Accept(a, option!.Command! with { TargetId = foe2.Id });
        Settle(a);
        Check(IsAt(Now(a, g2), Zone.Battlefield, 0), "he moved first");
        Check(Now(a, focus2).Zone == Zone.FateDiscard, "the card was discarded");
        Check(!IsAt(Now(a, foe2), Zone.Battlefield), "Ranged 3 destroys the Force 3 Personality");
        Check(IsAt(Now(a, big2), Zone.Battlefield), "the Force 4 Personality is out of its reach");
        _ = (foe, focus, big);
    }

    private static void TatsuzoCard()
    {
        // The grant is an Open action of the Action phase; the battle comes later in the turn, with the target at its battlefield.
        MatchEngine Rig(out CardInstance tatsuzo, out CardInstance foe, out CardInstance first, out CardInstance second)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            tatsuzo = Add(s, Tatsuzo, 0, Zone.Home);
            foe = P(s, "w735-foe", 1, Zone.Home, 3, 2, 1, ["Samurai"]);
            Attach(s, TrainingItem, 1, foe, "w735-item1"); Attach(s, TrainingItem, 1, foe, "w735-item2");
            first = Add(s, CrushingStrength, 0, Zone.Hand); second = Add(s, CrushingStrength, 0, Zone.Hand);
            Add(s, TrainingItem, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        MatchEngine ToBattle(MatchEngine e, string foeId) => Batch336Support.Edit(e, st =>
        {
            st.Phase = Phase.Battle; st.ActivePlayerId = 1; st.PriorityPlayerId = 0; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0];
            var foe = st.Cards[foeId]; foe.Zone = Zone.Battlefield; foe.ProvinceIndex = 0;
            foreach (var attachment in st.Cards.Values.Where(c => c.AttachedTo == foeId)) attachment.ProvinceIndex = 0;
        });
        var e = Rig(out var tatsuzo, out var foe, out var first, out var second);
        Accept(e, Batch336Support.Use(e, 0, Wave735Abilities.TatsuzoGrant, tatsuzo.Id, foe.Id));
        Settle(e);
        var b = ToBattle(e, foe.Id);
        Check((bool)Call(b, "Wave735WaivesPresence", 0)!, "while the target is at the battlefield the waiver holds");
        Check(!(bool)Call(b, "Wave735WaivesPresence", 1)!, "it is the acting player's alone");
        var target = b.State.Cards.Values.First(c => c.AttachedTo == foe.Id);
        Check(Offered(b, 0, "crushing-destroy", first.Id), "a Battle action is offered with no unit of his own there");
        Accept(b, Batch336Support.Offer(b, 0, "crushing-destroy", first.Id, target.Id));
        Settle(b);
        Check(!IsAt(Now(b, target), Zone.Attached), "the Battle action resolved");
        Check(!(bool)Call(b, "Wave735WaivesPresence", 0)!, "that action ended the effect on him");
        Check(!Offered(b, 0, "crushing-destroy", second.Id), "a second Battle action is refused again");
        // Without the grant the same Battle action is refused.
        var plain = Rig(out _, out var foe0, out var first0, out _);
        var p0 = ToBattle(plain, foe0.Id);
        Check(!Offered(p0, 0, "crushing-destroy", first0.Id), "with no unit at the battlefield and no grant a Battle action is refused");
        // The waiver needs the target at the battlefield: once he has gone it does not hold.
        var t = Rig(out var t2, out var foe2, out _, out _);
        Accept(t, Batch336Support.Use(t, 0, Wave735Abilities.TatsuzoGrant, t2.Id, foe2.Id));
        Settle(t);
        var tb = ToBattle(t, foe2.Id);
        Check((bool)Call(tb, "Wave735WaivesPresence", 0)!, "granted");
        tb.MoveHomeByEffectForTest(foe2.Id, null);
        Check(!(bool)Call(tb, "Wave735WaivesPresence", 0)!, "the target left the battlefield: the waiver does not hold");
    }

    private static void ShakashCard()
    {
        MatchEngine Dynasty(int others, string[] keywords, out CardInstance province)
        {
            var s = Table(Phase.Dynasty, 0);
            Add(s, Shakash, 0, Zone.Home);
            for (var i = 0; i < others; i++) P(s, $"w735-p{i}", 0, Zone.Home, 2, 2, 1, ["Samurai"]);
            for (var i = 0; i < 8; i++) Add(s, Marketplace, 0, Zone.Home);
            province = s.Cards[s.Players[0].Provinces[0].CardId!];
            province.Printed = CardCatalog.Get(CraneSamurai) with { Id = "w735-entrant", Keywords = keywords, GoldCost = 5, Effect = "none", Abilities = [] };
            province.FaceUp = true;
            return MatchEngine.FromTestState(s);
        }
        var few = Dynasty(2, ["Nonhuman", "Spirit"], out var p1);
        Eq(few.QuotePurchase(0, p1.Id).Cost, 4, "three Personalities in play (Shakash and two others): a Nonhuman Personality costs 1 less");
        var many = Dynasty(3, ["Nonhuman", "Spirit"], out var p2);
        Eq(many.QuotePurchase(0, p2.Id).Cost, 5, "four Personalities: the printed price");
        var human = Dynasty(2, ["Samurai"], out var p3);
        Eq(human.QuotePurchase(0, p3.Id).Cost, 5, "a Human Personality is not discounted");

        // A Nonhuman Follower attaching: Elephant Cavalry costs 8, 7 with The Shakash.
        MatchEngine Equip(bool shakash)
        {
            var s = Table(Phase.Action, 0);
            if (shakash) Add(s, Shakash, 0, Zone.Home);
            Add(s, CraneSamurai, 0, Zone.Home);
            for (var i = 0; i < 9; i++) Add(s, Marketplace, 0, Zone.Home);
            Add(s, Elephant, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        int? Quote(MatchEngine e) => e.QueryActions(0).Where(a => a.Type == "equip" && a.Gold is not null && CardCatalog.Get(e.State.Cards[a.CardId!].DefinitionId).Id == Elephant).Select(a => a.Gold!.Cost).Cast<int?>().FirstOrDefault();
        var printed = CardCatalog.Get(Elephant).GoldCost;
        Eq(Quote(Equip(false)), printed, "no Shakash: the printed price");
        Eq(Quote(Equip(true)), printed - 1, "with The Shakash: a Nonhuman Follower costs 1 less");

        // Home Battle: move your target Personality at any location to his location.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var shakashHome = Add(s2, Shakash, 0, Zone.Home);
        P(s2, "w735-guard", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var away = P(s2, "w735-away", 0, Zone.Home, 2, 2, 1, ["Samurai"]);
        var there = P(s2, "w735-there", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        P(s2, "w735-foe", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        HandCards(s2);
        var m = MatchEngine.FromTestState(s2);
        Accept(m, Batch336Support.Use(m, 0, Wave735Abilities.ShakashMove, shakashHome.Id, there.Id));
        Settle(m);
        Check(IsAt(Now(m, there), Zone.Home), "Shakash at home: the Personality at the battlefield moved home to his location");
        _ = away;
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        var shakashFront = Add(s3, Shakash, 0, Zone.Battlefield, province: 0);
        var home3 = P(s3, "w735-home", 0, Zone.Home, 2, 2, 1, ["Samurai"]);
        P(s3, "w735-foe", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        HandCards(s3);
        var n = MatchEngine.FromTestState(s3);
        var foeCard = n.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        NoChange(n, Batch336Support.Use(n, 0, Wave735Abilities.ShakashMove, shakashFront.Id, foeCard.Id), "the target must be your own Personality");
        Accept(n, Batch336Support.Use(n, 0, Wave735Abilities.ShakashMove, shakashFront.Id, home3.Id));
        Settle(n);
        Check(IsAt(Now(n, home3), Zone.Battlefield, 0), "Shakash at the battlefield: the Personality at home moved there");
    }

    private static void SorrowCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var sorrow = Add(s, Sorrow, 0, Zone.Battlefield, province: 0);
        var foe = P(s, "w735-foe", 1, Zone.Battlefield, 5, 2, 1, ["Samurai"], province: 0);
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 0, Wave735Abilities.SorrowBattle, sorrow.Id, foe.Id));
        Settle(e);
        Eq(ForceOf(e, foe), 1, "-4F");
        // The Limited twin: only while you control a Courtier.
        MatchEngine Action(bool courtier, out CardInstance card, out CardInstance target)
        {
            var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
            card = Add(t, Sorrow, 0, Zone.Home);
            target = P(t, "w735-target", 1, Zone.Home, 5, 2, 1, ["Samurai"]);
            if (courtier) P(t, "w735-courtier", 0, Zone.Home, 1, 2, 1, ["Courtier"]);
            HandCards(t);
            return MatchEngine.FromTestState(t);
        }
        var without = Action(false, out var s0, out _);
        Check(!Offered(without, 0, Wave735Abilities.SorrowLimited, s0.Id), "no Courtier: no Limited action");
        var with = Action(true, out var s1, out var target);
        Check(Offered(with, 0, Wave735Abilities.SorrowLimited, s1.Id), "a Courtier: the Limited action is offered");
        Accept(with, Batch336Support.Use(with, 0, Wave735Abilities.SorrowLimited, s1.Id, target.Id));
        Settle(with);
        Eq(ForceOf(with, target), 1, "-4F as a Limited action");
    }

    private static void TsiangCard()
    {
        foreach (var ring in new[] { true, false })
        {
            var s = Table(Phase.Dynasty, 0);
            var province = s.Cards[s.Players[0].Provinces[0].CardId!];
            province.DefinitionId = Tsiang; province.FaceUp = true;
            if (ring) Add(s, RingId("Ring of Water"), 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Eq(e.QuotePurchase(0, province.Id).Cost, ring ? 0 : CardCatalog.Get(Tsiang).GoldCost, ring ? "ignoring Gold Cost with the Ring of Water" : "the printed Gold Cost");
        }
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var tsiang = Add(t, Tsiang, 0, Zone.Home);
        P(t, "w735-guard", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var mine = P(t, "w735-mine", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0, bowed: true);
        var foe = P(t, "w735-foe", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0, bowed: true);
        var third = P(t, "w735-third", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0, bowed: true);
        HandCards(t);
        var m = MatchEngine.FromTestState(t);
        NoChange(m, Batch336Support.Use(m, 0, Wave735Abilities.TsiangStraighten, tsiang.Id, null, null, Batch336Support.Pick("cards", mine.Id, foe.Id, third.Id)), "at most two cards");
        Accept(m, Batch336Support.Use(m, 0, Wave735Abilities.TsiangStraighten, tsiang.Id, null, null, Batch336Support.Pick("cards", mine.Id, foe.Id)));
        Settle(m);
        Check(!Now(m, mine).Bowed && !Now(m, foe).Bowed && Now(m, third).Bowed && Now(m, tsiang).Bowed, "two cards straightened (either player's), Tsiang bowed, the third left");
    }

    private static void ElephantCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var host = P(s, "w735-host", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var item = Attach(s, TrainingItem, 0, host);
        var elephant = Attach(s, Elephant, 0, host);
        var other = P(s, "w735-other", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(Has(e, host, "Cavalry") && Has(e, item, "Cavalry") && Has(e, elephant, "Cavalry"), "every card in its unit has Cavalry");
        Check(!Has(e, other, "Cavalry"), "a card outside the unit does not");
        // It attaches only to a Human.
        var t = Table(Phase.Action, 0);
        var human = P(t, "w735-human", 0, Zone.Home, 2, 2, 1, ["Samurai"]);
        var spirit = P(t, "w735-spirit", 0, Zone.Home, 2, 2, 1, ["Nonhuman", "Spirit"]);
        for (var i = 0; i < 9; i++) Add(t, Marketplace, 0, Zone.Home);
        var inHand = Add(t, Elephant, 0, Zone.Hand);
        var q = MatchEngine.FromTestState(t);
        var targets = q.QueryActions(0).Where(a => a.Type == "equip" && a.CardId == inHand.Id).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(human.Id) && !targets.Contains(spirit.Id), "offered for a Human, not for a Nonhuman");
    }

    private static void ScoutsCard()
    {
        // As a Battle action, with its Personality at home and a unit of his player's at the battlefield.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var home = P(s, "w735-home", 0, Zone.Home, 2, 2, 1, ["Samurai"]);
        var scouts = Attach(s, Scouts, 0, home);
        var front = P(s, "w735-front", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        P(s, "w735-foe", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 0, Wave735Abilities.ScoutsTransfer, scouts.Id, front.Id));
        Settle(e);
        Check(Now(e, scouts).AttachedTo == front.Id, "transferred to the Personality at the battlefield, from home");
        // As an Open action.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var a = P(t, "w735-a", 0, Zone.Home, 2, 2, 1, ["Samurai"]);
        var b = P(t, "w735-b", 0, Zone.Home, 2, 2, 1, ["Samurai"]);
        var follower = Attach(t, Scouts, 0, a);
        HandCards(t);
        var o = MatchEngine.FromTestState(t);
        Accept(o, Batch336Support.Use(o, 0, Wave735Abilities.ScoutsTransfer, follower.Id, b.Id));
        Settle(o);
        Check(Now(o, follower).AttachedTo == b.Id, "transferred as an Open action");
    }

    private static void UjiCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var crane = P(s, "w735-crane", 0, Zone.Battlefield, 2, 2, 1, ["Samurai", "Crane Clan"], province: 0);
        var uji = Attach(s, Uji, 0, crane);
        var plain = Fx(s, CraneSamurai, "w735-plain", 0, Zone.Battlefield, d => d with { Clan = "Lion", Keywords = ["Samurai"], Abilities = [], Effect = "none" }, province: 0);
        var ujiOnPlain = Attach(s, Uji, 0, plain, "w735-uji2", d => d);
        var foe = P(s, "w735-foe", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        var enemyItem = Attach(s, TrainingItem, 1, foe, "w735-enemy-item");
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Check(Has(e, crane, "Naval"), "a Crane Clan Personality has Naval");
        Check(!Has(e, plain, "Naval"), "another Clan's does not");
        Accept(e, Batch336Support.Offer(e, 0, Wave735Abilities.UjiDestroy, uji.Id, enemyItem.Id));
        Settle(e);
        Check(!IsAt(Now(e, enemyItem), Zone.Attached), "the enemy attachment is destroyed");
        _ = ujiOnPlain;
    }

    private static void AnchoredCard()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var shugenja = P(s, "w735-shugenja", 0, Zone.Home, 2, 2, 1, ["Shugenja", "Spirit"]);
        var spell = Attach(s, Anchored, 0, shugenja);
        var spirit = P(s, "w735-spirit", 0, Zone.Battlefield, 2, 2, 1, ["Nonhuman", "Spirit"], province: 0);
        var human = P(s, "w735-human", 0, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        P(s, "w735-foe", 1, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0);
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Check(!Batch336Support.Options(e, 0, Wave735Abilities.AnchoredBoost, spell.Id).Any(a => a.TargetIds?.Contains(human.Id) == true), "only a Spirit is a legal target");
        var force = ForceOf(e, spirit);
        Accept(e, Batch336Support.Offer(e, 0, Wave735Abilities.AnchoredBoost, spell.Id, spirit.Id));
        Settle(e);
        Eq(ForceOf(e, spirit), force + 3, "+3F");
        Check(Has(e, spirit, "Elite") && Now(e, spell).Bowed, "Elite, and the Spell bowed");
    }

    private static void InfusionCard()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shugenja = P(s, "w735-shugenja", 0, Zone.Home, 2, 2, 1, ["Shugenja", "Shadowlands"]);
        var spell = Attach(s, Infusion, 0, shugenja);
        var plain = P(s, "w735-plain", 0, Zone.Home, 2, 2, 1, ["Shugenja"]);
        Attach(s, Infusion, 0, plain, "w735-infusion2", d => d);
        var nonhuman = P(s, "w735-nonhuman", 1, Zone.Home, 2, 2, 1, ["Nonhuman"]);
        var human = P(s, "w735-human", 1, Zone.Home, 2, 2, 1, ["Samurai"]);
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Check(Has(e, shugenja, "Cavalry"), "a Shadowlands Shugenja has Cavalry");
        Check(!Has(e, plain, "Cavalry"), "another does not");
        Check(!Batch336Support.Options(e, 0, Wave735Abilities.InfusionCavalry, spell.Id).Any(a => a.TargetIds?.Contains(human.Id) == true), "a Human is not a Nonhuman card");
        Accept(e, Batch336Support.Offer(e, 0, Wave735Abilities.InfusionCavalry, spell.Id, nonhuman.Id));
        Settle(e);
        Check(Has(e, nonhuman, "Cavalry") && Now(e, spell).Bowed, "the Nonhuman card has Cavalry and the Spell bowed");
    }

    private static void DrawingCard()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shugenja = P(s, "w735-shugenja", 0, Zone.Home, 2, 2, 1, ["Shugenja"], bowed: true);
        var spell = Attach(s, Drawing, 0, shugenja);
        var fire = Add(s, RingId("Ring of Fire"), 0, Zone.Home);
        var water = Add(s, RingId("Ring of Water"), 0, Zone.Home, bowed: true);
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Check(Has(e, shugenja, "Fire") && Has(e, shugenja, "Water") && !Has(e, shugenja, "Earth"), "he has the element keywords of the Rings you control");
        Check(!Batch336Support.Options(e, 0, Wave735Abilities.DrawingBow, spell.Id).Any(a => a.TargetIds?.Contains(water.Id) == true), "a bowed Ring is not a legal target");
        Accept(e, Batch336Support.Offer(e, 0, Wave735Abilities.DrawingBow, spell.Id, fire.Id));
        Settle(e);
        Check(Now(e, fire).Bowed && !Now(e, shugenja).Bowed && Now(e, spell).Bowed, "the Ring bowed, the Shugenja straightened, the Spell bowed");
    }

    private static void LashCard()
    {
        MatchEngine Rig(string[] keywords, out CardInstance shugenja, out CardInstance spell, out CardInstance target, out CardInstance discard, out CardInstance ring)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            shugenja = P(s, "w735-shugenja", 0, Zone.Home, 2, 2, 1, keywords);
            spell = Attach(s, Lash, 0, shugenja);
            target = P(s, "w735-target", 1, Zone.Home, 4, 2, 1, ["Samurai"]);
            discard = Add(s, TrainingItem, 1, Zone.FateDiscard);
            ring = Add(s, RingId("Ring of the Void"), 1, Zone.FateDiscard);
            HandCards(s);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(["Shugenja", "Fire"], out var shugenja, out var spell, out var target, out var discard, out var ring);
        Check(Has(e, shugenja, "Duelist"), "a Fire Shugenja has Duelist");
        Check(!Has(Rig(["Shugenja", "Water"], out var w, out _, out _, out _, out _), w, "Duelist"), "a Water Shugenja does not");
        var plus = Batch336Support.Use(e, 0, Wave735Abilities.LashPlus, spell.Id, null, null, Batch336Support.Pick("personality", target.Id), Batch336Support.Pick("p24-discard", discard.Id));
        NoChange(e, Batch336Support.Use(e, 0, Wave735Abilities.LashPlus, spell.Id, null, null, Batch336Support.Pick("personality", target.Id), Batch336Support.Pick("p24-discard", ring.Id)), "a Ring cannot be removed");
        Accept(e, plus);
        Settle(e);
        Eq(ForceOf(e, target), 7, "+3F");
        Check(Now(e, discard).Zone == Zone.RemovedFromGame, "the card left the game as the cost");
        var m = Rig(["Shugenja", "Fire"], out _, out var spell2, out var target2, out var discard2, out _);
        Accept(m, Batch336Support.Use(m, 0, Wave735Abilities.LashMinus, spell2.Id, null, null, Batch336Support.Pick("personality", target2.Id), Batch336Support.Pick("p24-discard", discard2.Id)));
        Settle(m);
        Eq(ForceOf(m, target2), 1, "-3F");
    }

    private static void SanctionCard()
    {
        MatchEngine Rig(out CardInstance card, out CardInstance lion, out CardInstance loyal, out CardInstance down, out CardInstance up)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            card = Add(s, Sanction, 0, Zone.Hand);
            lion = P(s, "w735-lion", 1, Zone.Home, 2, 2, 1, ["Samurai", "Lion Clan"]);
            loyal = P(s, "w735-loyal", 1, Zone.Home, 2, 2, 1, ["Samurai", "Loyal"]);
            down = P(s, "w735-down", 1, Zone.Province, 2, 2, 1, ["Samurai"]);
            down.FaceUp = false; down.ProvinceIndex = 1;
            up = P(s, "w735-up", 1, Zone.Province, 2, 2, 1, ["Samurai", "Lion Clan"]);
            up.FaceUp = true; up.ProvinceIndex = 2;
            HandCards(s);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var card, out var lion, out var loyal, out var down, out var up);
        var targets = Batch336Support.Options(e, 0, Wave735Abilities.SanctionGrant, card.Id).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(lion.Id) && targets.Contains(up.Id), "a Personality in play, and one face-up in a Province");
        Check(!targets.Contains(loyal.Id) && !targets.Contains(down.Id), "not a Loyal one, not a face-down one");
        Check(!Has(e, lion, "Crane Clan"), "he is not Crane yet");
        Accept(e, Batch336Support.Use(e, 0, Wave735Abilities.SanctionGrant, card.Id, lion.Id));
        Settle(e);
        Check(Has(e, lion, "Crane Clan") && Has(e, lion, "Lion Clan"), "he permanently has the Crane Clan alignment as well");
        var f = Rig(out var card2, out _, out _, out _, out var up2);
        Accept(f, Batch336Support.Use(f, 0, Wave735Abilities.SanctionGrant, card2.Id, up2.Id));
        Settle(f);
        Check(Has(f, up2, "Crane Clan"), "a face-up Personality in a Province gains it too");
    }

    private static void GraspCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var grasp = Add(s, Grasp, 0, Zone.Celestial);
        var mine = P(s, "w735-mine", 0, Zone.Battlefield, 3, 2, 1, ["Samurai"], province: 0);
        P(s, "w735-foe", 1, Zone.Battlefield, 2, 2, 1, ["Samurai"], province: 0);
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Check(Has(e, mine, "Conqueror"), "your Personalities have Conqueror while it is in play");
        var provinces = e.State.Players[0].Provinces.Count(p => !p.Destroyed);
        NoChange(e, Batch336Support.Use(e, 0, Wave735Abilities.GraspDestroy, grasp.Id, null, null, new SlotSelection("province", ProvinceOwner: 1, Province: 0)), "only one of your own Provinces");
        Accept(e, Batch336Support.Use(e, 0, Wave735Abilities.GraspDestroy, grasp.Id, null, null, new SlotSelection("province", ProvinceOwner: 0, Province: 0)));
        Settle(e);
        Eq(e.State.Players[0].Provinces.Count(p => !p.Destroyed), provinces - 1, "one of your Provinces was destroyed");
        Check(Now(e, grasp).Zone == Zone.DynastyDiscard, "the Celestial was discarded");
        Check(!Has(e, Now(e, mine), "Conqueror"), "and with it the Conqueror rule");
        Check(e.IsImmuneForTest(mine.Id, ImmunityKind.Destroy, 1), "other players' cards will not destroy your Personalities this battle: " + JsonSerializer.Serialize(e.State.Immunities));
        Check(!e.IsImmuneForTest(mine.Id, ImmunityKind.Destroy, 0), "your own effects still can");
        Call(e, "Wave735EndBattle");
        Check(!e.IsImmuneForTest(mine.Id, ImmunityKind.Destroy, 1), "the immunity ends with the battle");
    }
}
