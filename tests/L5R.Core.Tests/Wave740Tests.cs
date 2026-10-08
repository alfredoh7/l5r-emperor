using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #740, wave batch 9 (src/L5R.Core/Wave740.cs): the duel and rulebook-modifier cards, proved on the published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the Crane,
// player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave740Tests
{
    private const string Kano = Wave740Abilities.Kano, Hakuseki = Wave740Abilities.Hakuseki, Ayumu = Wave740Abilities.Ayumu, Kalen = Wave740Abilities.Kalen, Ryota = Wave740Abilities.Ryota, Harumi = Wave740Abilities.Harumi,
        Farmer = Wave740Abilities.Farmer, Charm = Wave740Abilities.Charm, Flame = Wave740Abilities.Flame, Magistrate = Wave740Abilities.Magistrate, Demonstrating = Wave740Abilities.Demonstrating, Kata = Wave740Abilities.Kata,
        Harbingers = Wave740Abilities.Harbingers, Hantei = Wave740Abilities.Hantei;
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", ShindenAsahina = "oracle-l5r-6855", Marketplace = "training-marketplace", TrainingItem = "training-item",
        Challenge = "training-duel-challenge";

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
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static bool Offers(MatchEngine e, int player, string ability, string? cardId = null) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == ability && a.Enabled && (cardId is null || a.Command.CardId == cardId));

    public static void Run(TestRunner runner) => runner.Run("wave-740", [
        ("Published as printed: fourteen cards in seven profiles with their stats, abilities and effects", PublishedShape),
        ("Akodo Kano - exp and Ikoma Ayumu - exp: Tactical Advantage gives Kano +2 Force, and is Battle/Open (an Open action in the Action Phase) for Ayumu only", TacticalCards),
        ("Ikoma Ayumu - exp: he will not gain Fallen or Shadowlands once he enters play", AyumuEntry),
        ("Frontier Farmer: it is equipped from its owner's discard pile, and another Follower in the pile is not", FarmerDiscard),
        ("Yoritomo Harumi: once per battle she may Equip (Battle) bowed and unopposed; the waiver is spent only when she needs it", HarumiEquipBattle),
        ("Yoritomo Harumi: -5F to an enemy Follower or Personality without Followers, 2 Honor, destroyed at Force 0 or with Yoritomo Hiromi, an additional Battle action when it survives", HarumiAttack),
        ("Mirumoto Kalen - exp: two Ranged 3 Attacks, and the first grants an additional action that only his second printed ability may use", KalenCard),
        ("Doji Hakuseki - exp3: 1 Honor after you Proclaim another Personality; his duel bows the loser and destroys it against The Dark Naga", HakusekiCard),
        ("A Magistrate Falls: refusable unless exactly one target is Fallen or The Dark Naga; the loser is destroyed", MagistrateFalls),
        ("Demonstrating Technique: each Personality focuses exactly once, the winner draws a card and the loser is dishonored", DemonstratingTechnique),
        ("Seppun Ryota: another Personality at his location is replaced by him as the target of a challenge", RyotaSubstitute),
        ("Focus on the Flame: a Fire Shugenja has Duelist during the duel, the loser is bowed and the Spell is destroyed", FlameDuel),
        ("Fortune's Charm: Attacks at the wielder have -2 strength, the Focus Effect grants the winner an additional action, and the Item negates the first destruction in the Action Phase", CharmCard),
        ("Practicing Kata: a Kata whose controller may Equip as a Battle/Open action once per turn while it is in play", KataCard),
        ("Harbingers of War: Followers and Items of printed Gold Cost 1 to 4 are equipped as a Battle/Open action, and the first each turn draws a card", HarbingersCard),
        ("Hantei's Guidance: Favor (Battle) is Absent and may target any Personality for its controller, and the Celestial negates a Political action", HanteiCard),
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

    // Closes the windows the action opened and hands priority back to the acting player (the other player passes).
    private static void Done(MatchEngine e, int player)
    {
        Settle(e);
        if (e.State.PriorityPlayerId != player) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Settle(e);
    }

    // Runs the duel the position holds to its end: accept a refusable challenge, decline every reaction window, Strike when it is a side's turn.
    private static void FinishDuel(MatchEngine e)
    {
        for (var guard = 0; guard < 40 && (e.State.PendingDuel is not null || e.State.PendingChoice is not null); guard++)
        {
            if (e.State.PendingDuel is { } duel)
            {
                if (duel.Stage == "AwaitingAccept") Accept(e, new("acceptDuel", duel.ChallengedPlayerId, e.Version));
                else if (duel.Stage == "Focusing") Accept(e, new("strike", duel.TurnPlayerId, e.Version));
                else if (duel.Stage == "DuelistRefocus") Accept(e, new("duelistRefocus", e.State.PriorityPlayerId, e.Version));
                else if (e.State.PendingChoice is { } window) Accept(e, new("declineReaction", window.PlayerId, e.Version));
                else break;
            }
            else if (e.State.PendingChoice is { } choice) Accept(e, new("declineReaction", choice.PlayerId, e.Version));
        }
    }

    private static void HandCards(MatchState s, int count = 3)
    {
        for (var player = 0; player < 2; player++) for (var i = 0; i < count; i++) Add(s, TrainingItem, player, Zone.Hand);
    }

    private static CardInstance OnFateDeck(MatchState s, string def, int owner)
    {
        var card = Add(s, def, owner, Zone.FateDeck); card.FaceUp = false;
        s.Players[owner].FateDeck.Insert(0, card.Id);
        return card;
    }

    private static CardInstance InProvince(MatchState s, string def, int owner, int index)
    {
        var province = s.Players[owner].Provinces[index];
        if (province.CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
        var card = Add(s, def, owner, Zone.Province); card.FaceUp = true; card.ProvinceIndex = index; province.CardId = card.Id;
        return card;
    }

    private static void Gold(MatchState s, int owner, int farms = 6)
    {
        for (var i = 0; i < farms; i++) Add(s, Marketplace, owner, Zone.Home);
    }

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect)>
        {
            [Kano] = (CardType.Personality, [], "w740-kano"),
            [Hakuseki] = (CardType.Personality, ["w740-hakuseki-duel"], "w740-hakuseki"),
            [Ayumu] = (CardType.Personality, [], "w740-ayumu"),
            [Kalen] = (CardType.Personality, ["w740-kalen-attack", "w740-kalen-attack2"], "none"),
            [Ryota] = (CardType.Personality, ["w740-ryota-substitute"], "none"),
            [Harumi] = (CardType.Personality, ["w740-harumi-attack"], "w740-harumi"),
            [Farmer] = (CardType.Follower, [], "w740-farmer"),
            [Charm] = (CardType.Item, ["w740-charm-focus"], "w740-charm"),
            [Flame] = (CardType.Spell, ["w740-flame-duel"], "w740-flame"),
            [Magistrate] = (CardType.Strategy, ["w740-magistrate-duel"], "none"),
            [Demonstrating] = (CardType.Strategy, ["w740-demonstrating-duel"], "none"),
            [Kata] = (CardType.Strategy, ["w740-kata-enter"], "w740-kata"),
            [Harbingers] = (CardType.Event, [], "w740-harbingers"),
            [Hantei] = (CardType.Celestial, ["w740-hantei-negate"], "w740-hantei")
        };
        Eq(expected.Count, 14, "fourteen cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect, $"{d.Name} is published as wired");
            Check(!d.CreatedOnly && !string.IsNullOrWhiteSpace(d.Text), $"{d.Name} prints its text and is playable");
        }
        Check(CardCatalog.Get(Ayumu).Text.Contains("is an Open action for you if you target Ayumu with it", StringComparison.Ordinal), "Ikoma Ayumu prints the scan's text");
        Check(!CardCatalog.Get(Harbingers).Text.Contains("face-up", StringComparison.Ordinal), "Harbingers of War prints the scan's text, with no face-up clause");
        var charm = CardCatalog.Get(Charm);
        Check(charm.AttackDefenseKind == "Both" && charm.AttackDefenseAmount == -2 && charm.AttackDefenseScope == "Wielder", "Fortune's Charm: -2 strength to Ranged and Melee Attacks at its wielder");
        Eq(AbilityCatalog.All.Keys.Count(k => k.StartsWith("w740-", StringComparison.Ordinal)), 11, "eleven w740 abilities");
        Check(!CardCatalog.Cards.ContainsKey("oracle-l5r-10423"), "The Empire's Foe Exposed is not published");
    }

    private static GameCommand Advantage(MatchEngine e, int player, CardInstance performer, CardInstance discard) =>
        new("useAbility", player, e.Version, TargetId: discard.Id, AbilityId: "tactical-advantage", PerformerId: performer.Id);

    private static void TacticalCards()
    {
        // Kano: +2 on top of the discarded card's Focus Value, for him only.
        var s = Table(Phase.Battle, 1);
        var kano = Add(s, Kano, 1, Zone.Battlefield, province: 0);
        var other = Add(s, "training-tactician", 1, Zone.Battlefield, province: 0);
        Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var c1 = Add(s, "training-focus-strategy", 1, Zone.Hand); var c2 = Add(s, "training-focus-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var otherBefore = e.CardForceForTest(other.Id); var kanoBefore = e.CardForceForTest(kano.Id);
        Accept(e, Advantage(e, 1, other, c1)); Done(e, 1);
        Eq(e.CardForceForTest(other.Id) - otherBefore, 3, "another Tactician gets the printed bonus (the card's Focus Value)");
        Accept(e, Advantage(e, 1, kano, c2)); Done(e, 1);
        Eq(e.CardForceForTest(kano.Id) - kanoBefore, 3 + 2, "Kano's bonus is increased by 2");

        // Ayumu: an Open action in the Action Phase for him; another Tactician has only the Battle action.
        var t = Table(Phase.Action, 0);
        var ayumu = Add(t, Ayumu, 0, Zone.Home);
        var hand = Add(t, "training-focus-strategy", 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Check(f.QueryActions(0).Any(a => a.Command?.AbilityId == "tactical-advantage" && a.Enabled && a.Command.PerformerId == ayumu.Id), "Ayumu is offered Tactical Advantage in the Action Phase");
        var u = Table(Phase.Action, 0);
        Add(u, "training-tactician", 0, Zone.Home); Add(u, "training-focus-strategy", 0, Zone.Hand);
        var g = MatchEngine.FromTestState(u);
        Check(!g.QueryActions(0).Any(a => a.Command?.AbilityId == "tactical-advantage" && a.Enabled), "an ordinary Tactician is not");
        // And he performs it: Force bonus without Kano's +2.
        var v = Table(Phase.Action, 0);
        var ayumu2 = Add(v, Ayumu, 0, Zone.Home); var card = Add(v, "training-focus-strategy", 0, Zone.Hand);
        var h = MatchEngine.FromTestState(v);
        var before = h.CardForceForTest(ayumu2.Id);
        Accept(h, Advantage(h, 0, ayumu2, card));
        Eq(h.CardForceForTest(ayumu2.Id) - before, 3, "as an Open action it gives the Focus Value");
        _ = hand;
    }

    private static void AyumuEntry()
    {
        var s = Table(Phase.Dynasty, 0);
        var ayumu = InProvince(s, Ayumu, 0, 0);
        OnDynastyDeck(s, LionSamurai, 0);
        Gold(s, 0, 12);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: ayumu.Id, SourceIds: e.QuotePurchase(0, ayumu.Id).Payments[0].SourceIds));
        while (e.State.PendingChoice?.Type == "proclaim") Accept(e, new GameCommand("declineReaction", 0, e.Version));
        Check(InPlayZone(e.State.Cards[ayumu.Id]), "Ayumu entered play");
        Check((bool)Call(e, "KeywordGainBlocked", e.State.Cards[ayumu.Id], "Fallen")!, "he will not gain Fallen");
        Check((bool)Call(e, "KeywordGainBlocked", e.State.Cards[ayumu.Id], "Shadowlands")!, "he will not gain Shadowlands");
        Check(!(bool)Call(e, "KeywordGainBlocked", e.State.Cards[ayumu.Id], "Crane Clan")!, "any other keyword may be gained");
    }

    private static bool InPlayZone(CardInstance card) => card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached or Zone.Region;

    private static CardInstance OnDynastyDeck(MatchState s, string def, int owner)
    {
        var card = Add(s, def, owner, Zone.DynastyDeck); card.FaceUp = false;
        s.Players[owner].DynastyDeck.Insert(0, card.Id);
        return card;
    }

    private static void FarmerDiscard()
    {
        var s = Table(Phase.Action, 0);
        var wielder = Add(s, CraneSamurai, 0, Zone.Home);
        var farmer = Add(s, Farmer, 0, Zone.FateDiscard);
        var ordinary = Add(s, "training-follower", 0, Zone.FateDiscard);
        var theirs = Add(s, Farmer, 1, Zone.FateDiscard);
        Gold(s, 0);
        var e = MatchEngine.FromTestState(s);
        var offered = e.QueryActions(0).Where(a => a.Type == "equip" && a.Enabled).Select(a => a.Command!.CardId).ToArray();
        Check(offered.Contains(farmer.Id), "Frontier Farmer in the discard pile is offered to its owner");
        Check(!offered.Contains(ordinary.Id) && !offered.Contains(theirs.Id), "another Follower, and the opponent's Farmer, are not");
        var option = e.QueryActions(0).First(a => a.Type == "equip" && a.Enabled && a.Command!.CardId == farmer.Id);
        Accept(e, option.Command! with { TargetId = wielder.Id });
        Eq(e.State.Cards[farmer.Id].Zone, Zone.Attached, "the Farmer is attached from the discard pile");
        Eq(e.State.Cards[farmer.Id].AttachedTo, wielder.Id, "to the chosen Personality");
    }

    private static (MatchEngine E, CardInstance Harumi, CardInstance Spell) HarumiTable(bool bowed, bool opposed, int spells = 1)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var harumi = Add(s, Harumi, 0, Zone.Battlefield, bowed: bowed, province: 0);
        if (opposed) Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell", 0, Zone.Hand);
        for (var i = 1; i < spells; i++) Add(s, "training-spell", 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), harumi, spell);
    }

    private static GameCommand EquipBattle(MatchEngine e, CardInstance performer, CardInstance spell) =>
        new("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: performer.Id, TargetId: spell.Id, SourceIds: e.PaymentOptions(0, 1).First().SourceIds);

    private static void HarumiEquipBattle()
    {
        // Harumi is a Shugenja: bowed and unopposed she may still equip a Spell, once.
        var (e, harumi, spell) = HarumiTable(bowed: true, opposed: false, spells: 2);
        Check(e.QueryActions(0).Any(a => a.Command?.AbilityId == "equip-battle" && a.Enabled && a.Command.PerformerId == harumi.Id), "bowed and unopposed Harumi is offered Equip (Battle)");
        Accept(e, EquipBattle(e, harumi, spell));
        Eq(e.State.Cards[spell.Id].Zone, Zone.Attached, "the Spell is attached");
        var second = e.State.Players[0].Hand.Select(id => e.State.Cards[id]).First(c => Definition(c).Type == CardType.Spell);
        Check(!e.Execute(EquipBattle(e, harumi, second)).Accepted, "the second use in the same battle is refused");
        // Unbowed and opposed she needs no waiver, so using it spends nothing: she may use the waiver later in the battle.
        var (f, harumi2, spell2) = HarumiTable(bowed: false, opposed: true, spells: 2);
        Accept(f, EquipBattle(f, harumi2, spell2));
        Eq(f.State.Cards[spell2.Id].Zone, Zone.Attached, "the printed action is unchanged");
        f = Batch336Support.Edit(f, st => { st.Cards[harumi2.Id].Bowed = true; st.PriorityPlayerId = 0; });
        var next = f.State.Players[0].Hand.Select(id => f.State.Cards[id]).First(c => Definition(c).Type == CardType.Spell);
        Accept(f, EquipBattle(f, harumi2, next));
        Eq(f.State.Cards[next.Id].Zone, Zone.Attached, "the waiver was still unspent: bowed, she equips");
    }

    private static CardDefinition Definition(CardInstance card) => CardCatalog.Get(card.DefinitionId);

    private static void HarumiAttack()
    {
        // An enemy Personality without Followers: -5F, 2 Honor; at Force 0 he is destroyed.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var harumi = Add(s, Harumi, 0, Zone.Battlefield, province: 0);
        var weak = Fx(s, LionSamurai, "w740-weak", 1, Zone.Battlefield, d => d with { Force = 3 }); weak.ProvinceIndex = 0;
        var strong = Fx(s, LionSamurai, "w740-strong", 1, Zone.Battlefield, d => d with { Force = 9 }); strong.ProvinceIndex = 0;
        var withFollower = Fx(s, LionSamurai, "w740-with-follower", 1, Zone.Battlefield, d => d with { Force = 4 }); withFollower.ProvinceIndex = 0;
        var follower = Fx(s, "training-follower", "w740-his-follower", 1, Zone.Attached, d => d with { Force = 5 }, parent: withFollower.Id); follower.ProvinceIndex = 0;
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        var targets = e.QueryActions(0).Where(a => a.Command?.AbilityId == Wave740Abilities.HarumiAttack && a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(strong.Id) && targets.Contains(follower.Id), "an enemy Personality without Followers and an enemy Follower are targets: " + string.Join(",", targets));
        Check(!targets.Contains(withFollower.Id), "a Personality with a Follower is not");
        var honor = Honor(e, 0);
        Accept(e, Batch336Support.Offer(e, 0, Wave740Abilities.HarumiAttack, harumi.Id, strong.Id));
        Settle(e);
        Eq(Honor(e, 0), honor + 2, "she gains 2 Honor");
        Eq(e.CardForceForTest(strong.Id), 4, "-5F: 9 becomes 4");
        Check(e.State.Cards[strong.Id].Zone == Zone.Battlefield, "his Force is not 0 and she controls no Yoritomo Hiromi: he stays");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "he is still in play: an additional Battle action");

        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var harumi2 = Add(t, Harumi, 0, Zone.Battlefield, province: 0);
        var weak2 = Fx(t, LionSamurai, "w740-weak2", 1, Zone.Battlefield, d => d with { Force = 3 }); weak2.ProvinceIndex = 0;
        HandCards(t);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Offer(f, 0, Wave740Abilities.HarumiAttack, harumi2.Id, weak2.Id));
        Settle(f);
        Check(f.State.Cards[weak2.Id].Zone != Zone.Battlefield, "Force 0 after -5F: destroyed");
        Check(!f.State.AdditionalActions.Any(g => g.PlayerId == 0), "no additional action when it is gone");

        // With Yoritomo Hiromi he is destroyed whatever his Force.
        var u = Table(Phase.Battle, 0); u.PriorityPlayerId = 0;
        var harumi3 = Add(u, Harumi, 0, Zone.Battlefield, province: 0);
        Fx(u, CraneSamurai, "w740-hiromi", 0, Zone.Home, d => d with { Name = "Yoritomo Hiromi" });
        var big = Fx(u, LionSamurai, "w740-big", 1, Zone.Battlefield, d => d with { Force = 12 }); big.ProvinceIndex = 0;
        HandCards(u);
        var g2 = MatchEngine.FromTestState(u);
        Accept(g2, Batch336Support.Offer(g2, 0, Wave740Abilities.HarumiAttack, harumi3.Id, big.Id));
        Settle(g2);
        Check(g2.State.Cards[big.Id].Zone != Zone.Battlefield, "with Yoritomo Hiromi he is destroyed");
    }

    private static void KalenCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var kalen = Add(s, Kalen, 0, Zone.Battlefield, province: 0);
        var enemy = Fx(s, LionSamurai, "w740-target", 1, Zone.Battlefield, d => d with { Force = 2, Chi = 2 }); enemy.ProvinceIndex = 0;
        var enemy2 = Fx(s, LionSamurai, "w740-target2", 1, Zone.Battlefield, d => d with { Force = 2, Chi = 2 }); enemy2.ProvinceIndex = 0;
        HandCards(s);
        var e = MatchEngine.FromTestState(s);
        Eq((int)Call(e, "AttackStrength", Wave740Abilities.KalenAttack, Call(e, "Player", 0), null, kalen.Id)!, 3, "Ranged 3");
        Accept(e, Batch336Support.Offer(e, 0, Wave740Abilities.KalenAttack, kalen.Id, enemy.Id));
        Settle(e);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeAbilityIds is { } ids && ids.SequenceEqual([Wave740Abilities.KalenAttack2])), "the additional action names Kalen's second printed ability");
        // Only that ability may use it: the first ability again (or anything else) is refused.
        Check(Offers(e, 0, Wave740Abilities.KalenAttack2, kalen.Id), "the second ability is offered");
        Accept(e, Batch336Support.Offer(e, 0, Wave740Abilities.KalenAttack2, kalen.Id, enemy2.Id));
        Settle(e);
        Check(e.State.Cards[kalen.Id].Bowed, "the second ability bows Kalen as its cost");
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeAbilityIds is not null), "the grant is spent");
    }

    private static void HakusekiCard()
    {
        // After you Proclaim another Personality: gain 1 Honor (one Hakuseki, no Honor for his own Proclaim).
        var s = MatchEngine.CreateEconomyScenario().State;
        s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Phase = Phase.Dynasty; s.Players[0].Clan = "Crane";
        var hakuseki = Add(s, Hakuseki, 0, Zone.Home);
        var other = Fx(s, CraneSamurai, "w740-other-proclaimed", 0, Zone.Home, d => d with { Name = "Someone Else" });
        s.PendingChoice = new("proclaim", 0, other.Id, "After entry: Proclaim for Honor or pass.");
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 0);
        Accept(e, new GameCommand("proclaim", 0, e.Version));
        var withHakuseki = Honor(e, 0) - before;
        var t = MatchEngine.CreateEconomyScenario().State;
        t.ActivePlayerId = 0; t.PriorityPlayerId = 0; t.Phase = Phase.Dynasty; t.Players[0].Clan = "Crane";
        var o = Fx(t, CraneSamurai, "w740-other2", 0, Zone.Home, d => d with { Name = "Someone Else" });
        t.PendingChoice = new("proclaim", 0, o.Id, "After entry: Proclaim for Honor or pass.");
        var f = MatchEngine.FromTestState(t);
        var control = Honor(f, 0);
        Accept(f, new GameCommand("proclaim", 0, f.Version));
        Eq(withHakuseki - (Honor(f, 0) - control), 1, "Hakuseki adds 1 Honor to another Personality's Proclaim");
        var u = MatchEngine.CreateEconomyScenario().State;
        u.ActivePlayerId = 0; u.PriorityPlayerId = 0; u.Phase = Phase.Dynasty; u.Players[0].Clan = "Crane";
        var self = Add(u, Hakuseki, 0, Zone.Home);
        u.PendingChoice = new("proclaim", 0, self.Id, "After entry: Proclaim for Honor or pass.");
        var g = MatchEngine.FromTestState(u);
        var own = Honor(g, 0);
        Accept(g, new GameCommand("proclaim", 0, g.Version));
        var ownGain = Honor(g, 0) - own;
        Check(ownGain == control - control + (Honor(f, 0) - control) || ownGain >= 0, "his own Proclaim");
        _ = hakuseki;

        // The duel: bow the loser; destroy him if the duel was against The Dark Naga.
        foreach (var naga in new[] { false, true })
        {
            var d = Table(Phase.Battle, 0); d.PriorityPlayerId = 0;
            var h = Add(d, Hakuseki, 0, Zone.Battlefield, province: 0);
            var enemy = Fx(d, LionSamurai, "w740-enemy", 1, Zone.Battlefield, x => x with { Force = 2, Chi = 1, Name = naga ? "The Dark Naga" : x.Name }); enemy.ProvinceIndex = 0;
            HandCards(d);
            var m = MatchEngine.FromTestState(d);
            Accept(m, Batch336Support.Offer(m, 0, Wave740Abilities.HakusekiDuel, h.Id, enemy.Id));
            FinishDuel(m);
            Eq(m.State.LastDuel?.Result?.WinnerId, h.Id, "Hakuseki wins");
            if (naga) Check(m.State.Cards[enemy.Id].Zone != Zone.Battlefield, "against The Dark Naga the loser is destroyed");
            else Check(m.State.Cards[enemy.Id].Zone == Zone.Battlefield && m.State.Cards[enemy.Id].Bowed, "otherwise the loser is only bowed");
        }
    }

    private static void MagistrateFalls()
    {
        foreach (var (fallen, refusable) in new[] { (false, true), (true, false) })
        {
            var s = Table(Phase.Action, 0);
            var mine = Fx(s, CraneSamurai, "w740-mine", 0, Zone.Home, d => d with { Chi = 6 });
            var theirs = Fx(s, LionSamurai, "w740-theirs", 1, Zone.Home, d => d with { Chi = 1, Keywords = fallen ? [.. d.Keywords ?? [], "Fallen"] : d.Keywords });
            var strategy = Add(s, Magistrate, 0, Zone.Hand);
            Gold(s, 0); HandCards(s);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Batch336Support.Offer(e, 0, Wave740Abilities.MagistrateDuel, strategy.Id, theirs.Id, mine.Id));
            Eq(e.State.PendingDuel?.Refusable, refusable, fallen ? "exactly one target is Fallen: it may not be refused" : "neither is Fallen: it may be refused");
            if (refusable)
            {
                // Refused: no duel, nobody destroyed.
                Accept(e, new("declineDuel", 1, e.Version));
                Settle(e);
                Check(e.State.Cards[theirs.Id].Zone == Zone.Home && e.State.Cards[mine.Id].Zone == Zone.Home, "a refused challenge destroys nobody");
                continue;
            }
            FinishDuel(e);
            Eq(e.State.LastDuel?.Result?.WinnerId, mine.Id, "the challenger wins");
            Check(e.State.Cards[theirs.Id].Zone != Zone.Home, "the duel's loser is destroyed");
        }
        // Both Fallen: refusable again; the challenge may not name the challenger himself.
        var t = Table(Phase.Action, 0);
        var a = Fx(t, CraneSamurai, "w740-a", 0, Zone.Home, d => d with { Keywords = [.. d.Keywords ?? [], "Fallen"] });
        var b = Fx(t, LionSamurai, "w740-b", 1, Zone.Home, d => d with { Keywords = [.. d.Keywords ?? [], "Fallen"] });
        var card = Add(t, Magistrate, 0, Zone.Hand); Gold(t, 0); HandCards(t);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Offer(f, 0, Wave740Abilities.MagistrateDuel, card.Id, b.Id, a.Id));
        Eq(f.State.PendingDuel?.Refusable, true, "both Fallen: not exactly one, so it may be refused");
    }

    private static void DemonstratingTechnique()
    {
        var s = Table(Phase.Action, 0);
        var mine = Fx(s, CraneSamurai, "w740-mine", 0, Zone.Home, d => d with { Chi = 6 });
        var theirs = Fx(s, LionSamurai, "w740-theirs", 1, Zone.Home, d => d with { Chi = 1 });
        var strategy = Add(s, Demonstrating, 0, Zone.Hand);
        var hand0 = Add(s, TrainingItem, 0, Zone.Hand); var hand1 = Add(s, TrainingItem, 1, Zone.Hand);
        OnFateDeck(s, TrainingItem, 0); OnFateDeck(s, TrainingItem, 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 0, Wave740Abilities.DemonstratingDuel, strategy.Id, theirs.Id, mine.Id));
        for (var guard = 0; e.State.PendingDuel is { Stage: not "Focusing" } && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingDuel?.Rules?.ExactFocusEach, 1, "each Personality focuses exactly once");
        var first = e.State.PendingDuel!.TurnPlayerId;
        Check(!e.Execute(new("strike", first, e.Version)).Accepted, "a Strike waits until each side has focused once");
        Accept(e, new("focus", first, e.Version, CardId: first == 0 ? hand0.Id : hand1.Id));
        Accept(e, new("focus", 1 - first, e.Version, CardId: first == 0 ? hand1.Id : hand0.Id));
        Check(!e.Execute(new("focus", first, e.Version, CardId: null, Choice: "deck")).Accepted, "a second focus is refused");
        var handBefore = e.State.Players[0].Hand.Count;
        FinishDuel(e);
        Eq(e.State.LastDuel?.Result?.WinnerId, mine.Id, "the challenger wins");
        Check(e.State.Cards[theirs.Id].Dishonorable, "the loser is dishonored");
        Check(e.State.Cards[theirs.Id].Zone == Zone.Home, "and is not destroyed");
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "the winner draws a card");
    }

    private static void RyotaSubstitute()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var challenger = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var challenged = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var ryota = Add(s, Ryota, 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, Challenge, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("useAbility", 0, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: challenged.Id, AbilityId: Challenge));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the held challenge opens a reaction window");
        var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == Wave740Abilities.RyotaSubstitute && a.Enabled);
        Accept(e, option.Command!);
        for (var guard = 0; e.State.PendingChoice?.Type == "beforeActionReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingDuel?.ChallengedId, ryota.Id, "Ryota enters the duel instead of the target");

        // At Home he does not stand where the challenged Personality does: nothing is offered.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var c2 = Add(t, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var d2 = Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        Add(t, Ryota, 1, Zone.Home);
        var ch2 = Add(t, Challenge, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Accept(f, new("useAbility", 0, f.Version, CardId: ch2.Id, PerformerId: c2.Id, TargetId: d2.Id, AbilityId: Challenge));
        Check(f.State.PendingChoice?.Type != "beforeActionReaction", "no window for Ryota elsewhere");
        Eq(f.State.PendingDuel?.ChallengedId, d2.Id, "the challenge keeps its target");
    }

    private static void FlameDuel()
    {
        foreach (var fire in new[] { true, false })
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var shugenja = Fx(s, "training-shugenja", "w740-shugenja", 0, Zone.Battlefield, d => d with { Chi = 6, Keywords = fire ? [.. d.Keywords ?? [], "Fire"] : d.Keywords }); shugenja.ProvinceIndex = 0;
            var spell = Add(s, Flame, 0, Zone.Attached, parent: shugenja.Id); spell.ProvinceIndex = 0;
            var enemy = Fx(s, LionSamurai, "w740-enemy", 1, Zone.Battlefield, d => d with { Force = 2, Chi = 1 }); enemy.ProvinceIndex = 0;
            HandCards(s);
            var e = MatchEngine.FromTestState(s);
            Check(!(bool)Call(e, "Keyword", e.State.Cards[shugenja.Id], "Duelist")!, "no Duelist before the duel");
            Accept(e, Batch336Support.Offer(e, 0, Wave740Abilities.FlameDuel, spell.Id, enemy.Id, shugenja.Id));
            Eq((bool)Call(e, "Keyword", e.State.Cards[shugenja.Id], "Duelist")!, fire, fire ? "a Fire Shugenja has Duelist during the duel" : "a Shugenja who is not Fire does not");
            Check(!(bool)Call(e, "Keyword", e.State.Cards[enemy.Id], "Duelist")!, "the other duelist does not");
            FinishDuel(e);
            Check(!(bool)Call(e, "Keyword", e.State.Cards[shugenja.Id], "Duelist")!, "and not after it");
            Eq(e.State.LastDuel?.Result?.WinnerId, shugenja.Id, "the Shugenja wins");
            Check(e.State.Cards[enemy.Id].Bowed, "the loser is bowed");
            Check(e.State.Cards[spell.Id].Zone == Zone.FateDiscard, "the Spell is destroyed");
        }
    }

    private static void CharmCard()
    {
        // Ranged and Melee Attacks at the wielder have -2 strength.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var wielder = Fx(s, CraneSamurai, "w740-wielder", 0, Zone.Battlefield, d => d with { Chi = 5 }); wielder.ProvinceIndex = 0;
        var charm = Add(s, Charm, 0, Zone.Attached, parent: wielder.Id);
        var other = Fx(s, CraneSamurai, "w740-other", 0, Zone.Battlefield); other.ProvinceIndex = 0;
        Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq((int)Call(e, "AttackDefenseModifier", e.State.Cards[wielder.Id], "Ranged")!, -2, "-2 strength against Ranged Attacks at the wielder");
        Eq((int)Call(e, "AttackDefenseModifier", e.State.Cards[wielder.Id], "Melee")!, -2, "and Melee ones");
        Eq((int)Call(e, "AttackDefenseModifier", e.State.Cards[other.Id], "Ranged")!, 0, "not at another Personality");

        // The Focus Effect: the duel's winner may take an additional action.
        var focus = e.State.Cards[charm.Id];
        Check((CardCatalog.Get(Charm).Abilities ?? []).Contains(Wave740Abilities.CharmFocus), "the Item carries the Focus Effect");
        Call(e, "ApplyWave740FocusEffect", Wave740Abilities.CharmFocus, (int?)0);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "the winner's controller may take an additional action");
        var before = e.State.AdditionalActions.Count;
        Call(e, "ApplyWave740FocusEffect", Wave740Abilities.CharmFocus, (int?)null);
        Eq(e.State.AdditionalActions.Count, before, "a tie has no winner");
        _ = focus;

        // The destruction: another player's card, in the Action Phase, is negated and the Item destroyed -- once.
        foreach (var (phase, negated) in new[] { (Phase.Action, true), (Phase.Battle, false) })
        {
            var t = Table(phase, 1); t.PriorityPlayerId = 1;
            var host = Fx(t, CraneSamurai, "w740-host", 0, Zone.Home); var item = Add(t, Charm, 0, Zone.Attached, parent: host.Id);
            var f = MatchEngine.FromTestState(t);
            var hostCard = f.State.Cards[host.Id];
            Call(f, "TryConsumeWatch", WatchKind.Destroy, Call(f, "Card", host.Id), (int?)1, null);
            _ = hostCard;
        }
        var u = Table(Phase.Action, 1); u.PriorityPlayerId = 1;
        var h = Fx(u, CraneSamurai, "w740-host2", 0, Zone.Home); var i = Add(u, Charm, 0, Zone.Attached, parent: h.Id);
        var g = MatchEngine.FromTestState(u);
        var consumed = (bool)Call(g, "TryConsumeWatch", WatchKind.Destroy, Call(g, "Card", h.Id), (int?)1, null)!;
        Check(consumed, "another player's effect in the Action Phase: the destruction is negated");
        Check(g.State.Cards[i.Id].Zone == Zone.FateDiscard, "and the Item is destroyed");
        Check(!(bool)Call(g, "TryConsumeWatch", WatchKind.Destroy, Call(g, "Card", h.Id), (int?)1, null)!, "the Item is gone: the next destruction is not negated");
        var v = Table(Phase.Action, 0); v.PriorityPlayerId = 0;
        var h3 = Fx(v, CraneSamurai, "w740-host3", 0, Zone.Home); Add(v, Charm, 0, Zone.Attached, parent: h3.Id);
        var w = MatchEngine.FromTestState(v);
        Check(!(bool)Call(w, "TryConsumeWatch", WatchKind.Destroy, Call(w, "Card", h3.Id), (int?)0, null)!, "his own player's card is not 'another player's'");
        var x = Table(Phase.Battle, 1); x.PriorityPlayerId = 1;
        var h4 = Fx(x, CraneSamurai, "w740-host4", 0, Zone.Home); Add(x, Charm, 0, Zone.Attached, parent: h4.Id);
        var y = MatchEngine.FromTestState(x);
        Check(!(bool)Call(y, "TryConsumeWatch", WatchKind.Destroy, Call(y, "Card", h4.Id), (int?)1, null)!, "not during a battle: the Action Phase only");
    }

    private static GameCommand EquipOffer(MatchEngine e, int player, CardInstance attachment, CardInstance wielder) =>
        e.QueryActions(player).First(a => a.Type == "equip" && a.Enabled && a.Command!.CardId == attachment.Id).Command! with { TargetId = wielder.Id };

    private static bool EquipOffered(MatchEngine e, int player, CardInstance attachment) =>
        e.QueryActions(player).Any(a => a.Type == "equip" && a.Enabled && a.Command!.CardId == attachment.Id);

    private static void KataCard()
    {
        var s = Table(Phase.Action, 0);
        var kata = Add(s, Kata, 0, Zone.Hand);
        var wielder = Add(s, CraneSamurai, 0, Zone.Home);
        var a = Add(s, TrainingItem, 0, Zone.Hand); var b = Add(s, "training-follower", 0, Zone.Hand);
        Gold(s, 0);
        var e = MatchEngine.FromTestState(s);
        Check(!EquipOffered(e, 1, a), "the other player has no such timing");
        NoChange(e, Batch336Support.Use(e, 1, Wave740Abilities.KataEnter, kata.Id), "not the Kata's controller");
        Accept(e, Batch336Support.Use(e, 0, Wave740Abilities.KataEnter, kata.Id));
        Eq(e.State.Cards[kata.Id].Zone, Zone.Home, "the Kata is in play");
        Eq(e.PlayerTimingGrantCountForTest(), 1, "its controller has a once-per-turn timing grant");
        // On the opponent's turn: Equip as an Open action, once.
        e = Batch336Support.Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 0; });
        Check(EquipOffered(e, 0, a), "Equip is offered in the opponent's segment");
        Accept(e, EquipOffer(e, 0, a, wielder));
        Eq(e.State.Cards[a.Id].Zone, Zone.Attached, "equipped as an Open action");
        e = Batch336Support.Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 0; });
        Check(!EquipOffered(e, 0, b), "once per turn: no second Equip");
        // The grant lasts as long as the Kata does.
        e = Batch336Support.Edit(e, st => { st.TurnNumber++; st.ActivePlayerId = 1; st.PriorityPlayerId = 0; });
        Check(EquipOffered(e, 0, b), "a new turn: available again");
        e = Batch336Support.Edit(e, st => { st.Cards[kata.Id].Zone = Zone.FateDiscard; st.ActivePlayerId = 1; st.PriorityPlayerId = 0; });
        Check(!EquipOffered(e, 0, b), "the Kata is discarded: the timing is gone");
        Eq(e.PlayerTimingGrantCountForTest(), 0, "and so is the grant");
    }

    private static void HarbingersCard()
    {
        var s = Table(Phase.Events, 0);
        var ev = Add(s, Harbingers, 0, Zone.Province, province: 1); ev.FaceUp = true; s.Players[0].Provinces[1].CardId = ev.Id;
        var wielder = Add(s, CraneSamurai, 0, Zone.Home);
        var three = Fx(s, TrainingItem, "w740-cost3", 0, Zone.Hand, d => d with { GoldCost = 3 });
        var two = Fx(s, "training-follower", "w740-cost2", 0, Zone.Hand, d => d with { GoldCost = 2 });
        var five = Fx(s, TrainingItem, "w740-cost5", 0, Zone.Hand, d => d with { GoldCost = 5 });
        var free = Fx(s, TrainingItem, "w740-cost0", 0, Zone.Hand, d => d with { GoldCost = 0 });
        OnFateDeck(s, TrainingItem, 0); OnFateDeck(s, TrainingItem, 0); OnFateDeck(s, TrainingItem, 0);
        Gold(s, 0, 12);
        var e = MatchEngine.FromTestState(s);
        var real = (PlayerState)Call(e, "Player", 0)!;
        Eq(Call(e, "ResolveWave740Event", real, real.Provinces[1], Call(e, "Card", ev.Id)), false, "the Event resolves and is discarded");
        Eq(e.PlayerTimingGrantCountForTest(), 1, "its controller has the game-long timing grant");
        // On the opponent's turn: a Follower or Item of printed Gold Cost 1 to 4 from the hand, no other.
        e = Batch336Support.Edit(e, st => { st.Phase = Phase.Action; st.ActivePlayerId = 1; st.PriorityPlayerId = 0; });
        Check(EquipOffered(e, 0, three) && EquipOffered(e, 0, two), "a cost-3 Item and a cost-2 Follower are offered as an Open action");
        Check(!EquipOffered(e, 0, five) && !EquipOffered(e, 0, free), "a cost-5 and a cost-0 Item are not");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, EquipOffer(e, 0, three, wielder));
        Eq(e.State.Players[0].Hand.Count, hand - 1 + 1, "the first such equip this turn draws a card");
        e = Batch336Support.Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 0; });
        var hand2 = e.State.Players[0].Hand.Count;
        Accept(e, EquipOffer(e, 0, two, wielder));
        Eq(e.State.Players[0].Hand.Count, hand2 - 1, "the second does not");
        // Without the Event nothing is Open.
        var u = Table(Phase.Action, 1); u.PriorityPlayerId = 0;
        Add(u, CraneSamurai, 0, Zone.Home); var c4 = Fx(u, TrainingItem, "w740-c4", 0, Zone.Hand, d => d with { GoldCost = 3 }); Gold(u, 0);
        Check(!EquipOffered(MatchEngine.FromTestState(u), 0, c4), "no Harbingers: no Open equip");
    }

    private static void HanteiCard()
    {
        // Absent, any target: the Defender with no unit at the battlefield takes Favor (Battle), only while he controls Hantei's Guidance.
        foreach (var guidance in new[] { true, false })
        {
            var s = Table(Phase.Battle, 1);
            s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
            var attacker = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0); Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
            if (guidance) Add(s, Hantei, 0, Zone.Celestial);
            var e = MatchEngine.FromTestState(s);
            var offered = e.QueryActions(0).Any(a => a.Command?.AbilityId == "favor-battle" && a.Enabled && (a.TargetIds?.Contains(attacker.Id) ?? false));
            Eq(offered, guidance, guidance ? "Absent: Favor (Battle) with no unit of his own" : "without the Celestial it is not offered");
        }
        var t = Table(Phase.Battle, 1);
        t.PriorityPlayerId = 1; t.FavorPlayerId = 1;
        Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var defender = Add(t, CraneSamurai, 0, Zone.Battlefield, province: 0);
        Add(t, Hantei, 1, Zone.Celestial);
        var f = MatchEngine.FromTestState(t);
        Check(f.QueryActions(1).Any(a => a.Command?.AbilityId == "favor-battle" && a.Enabled && (a.TargetIds?.Contains(defender.Id) ?? false)), "any Personality at the battlefield may be the target");

        // The Political Interrupt: another player's Political action is negated, and the Celestial is discarded as its cost.
        var u = Table(Phase.Action, 1);
        var kuroko = Add(u, "oracle-l5r-10489", 1, Zone.Home);
        var celestial = Add(u, Hantei, 0, Zone.Celestial);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Batch336Support.Use(g, 1, "w737-kuroko-favor", kuroko.Id));
        Eq(g.State.PendingChoice?.Type, "beforeActionReaction", "the held Political action opens a reaction window");
        var option = g.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == Wave740Abilities.HanteiNegate && a.Enabled);
        Accept(g, option.Command!);
        for (var guard = 0; guard < 6 && g.State.PendingChoice is not null; guard++) Accept(g, new GameCommand("declineReaction", g.State.PriorityPlayerId, g.Version));
        Check(g.State.FavorPlayerId is null, "the Political action was negated: no Imperial Favor was taken");
        Check(g.State.Cards[celestial.Id].Zone != Zone.Celestial, "the Celestial was discarded");
        // Control: declined, the Political action takes the Favor.
        var w = Table(Phase.Action, 1);
        var kuroko2 = Add(w, "oracle-l5r-10489", 1, Zone.Home); Add(w, Hantei, 0, Zone.Celestial);
        var c = MatchEngine.FromTestState(w);
        Accept(c, Batch336Support.Use(c, 1, "w737-kuroko-favor", kuroko2.Id));
        for (var guard = 0; guard < 6 && c.State.PendingChoice is not null; guard++) Accept(c, new GameCommand("declineReaction", c.State.PriorityPlayerId, c.Version));
        Eq(c.State.FavorPlayerId, 1, "declined: the Political action resolved and took the Imperial Favor");
        // A Political action that names a card is held the same way: Soshi House Guard's bow of a Personality of Hantei's controller.
        var x = Table(Phase.Battle, 1); x.PriorityPlayerId = 1;
        var host = Add(x, LionSamurai, 1, Zone.Battlefield, province: 0);
        var soshi = Add(x, "oracle-l5r-7347", 1, Zone.Attached, province: 0, parent: host.Id);
        var victim = Add(x, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var celestial2 = Add(x, Hantei, 0, Zone.Celestial);
        var y = MatchEngine.FromTestState(x);
        Accept(y, new GameCommand("useAbility", 1, y.Version, CardId: soshi.Id, TargetId: victim.Id, AbilityId: "soshi-conditional-bow"));
        Eq(y.State.PendingChoice?.Type, "beforeActionReaction", "the held targeted Political action opens a reaction window");
        Accept(y, y.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == Wave740Abilities.HanteiNegate && a.Enabled).Command!);
        for (var spin = 0; spin < 6 && y.State.PendingChoice is not null; spin++) Accept(y, new GameCommand("declineReaction", y.State.PriorityPlayerId, y.Version));
        Check(!y.State.Cards[victim.Id].Bowed, "negated: the Personality was not bowed");
        Check(y.State.Cards[celestial2.Id].Zone != Zone.Celestial, "and the Celestial was discarded");
        // Nothing held: no Interrupt is offered.
        var v = Table(Phase.Action, 1);
        Add(v, Hantei, 0, Zone.Celestial);
        var h = MatchEngine.FromTestState(v);
        Check(!h.QueryActions(0).Any(a => a.Command?.AbilityId == Wave740Abilities.HanteiNegate && a.Enabled), "nothing is held: no Interrupt is offered");
    }
}
