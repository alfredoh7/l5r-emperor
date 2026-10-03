using L5R.Core;
using static RegressionAssertions;

// Issue #503, group P4: Shinjo Hayan's assignment after engaging and Ikoma Shinohara's attach from the hand (src/L5R.Core/EmperorP4Units.cs).
internal static partial class EmperorP4Tests
{
    private const string Hayan = "oracle-l5r-6883", Shinohara = "oracle-l5r-3639";

    private static (string Name, Action Body)[] UnitTests() => [
        ("Published shape (units): Shinjo Hayan and Ikoma Shinohara (personality-wave-108)", UnitsPublishedShape),
        ("Shinjo Hayan: after engaging he may assign his player's unbowed Unicorn Clan Personality at home to the current battlefield, once per turn; a bowed or non-Unicorn one is not offered", HayanAssigns),
        ("Shinjo Hayan: entering play, he discards his player's other Sensei Personalities", HayanSensei),
        ("Ikoma Shinohara: a Follower in the hand is attached to a targeted Personality of his player's at the battlefield, paying its Gold Cost; a Personality at home or an enemy one is refused", ShinoharaAttaches),
    ];

    private static void UnitsPublishedShape()
    {
        var hayan = CardCatalog.Get(Hayan);
        Check(hayan.Clan == "Unicorn" && hayan.GoldCost == 8 && hayan.Force == 4 && hayan.Chi == 3 && hayan.PersonalHonor == 2 && hayan.HonorRequirement == 4, "Hayan's stats");
        Eq(hayan.Effect, "discard-other-sensei");
        Shape(Hayan, CardType.Personality, "Shinjo Hayan", "hayan-assign",
            "After Hayan enters play: Discard all your other Sensei Personalities.\nAfter engaging: Once per turn, you may assign your (unbowed) Unicorn Clan Personality in your home to the current battlefield.",
            "Cavalry,Loyal,Samurai,Sensei,Unicorn Clan");
        var shinohara = CardCatalog.Get(Shinohara);
        Check(shinohara.Clan == "Lion" && shinohara.GoldCost == 7 && shinohara.Force == 4 && shinohara.Chi == 2 && shinohara.PersonalHonor == 3 && shinohara.HonorRequirement == 7, "Shinohara's stats");
        Shape(Shinohara, CardType.Personality, "Ikoma Shinohara", "shinohara-attach",
            "Battle: Target a Follower in your hand and one of your Personalities: Attach the Follower to the Personality, paying all costs.", "Commander,Lion Clan,Samurai");
        Manifest("personality-wave-108", Tenzan, Gozaru, Sarang, Nobukatsu, Hayan, Shinohara);
    }

    private static MatchState SelectionTable()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        return s;
    }

    private static void HayanAssigns()
    {
        var s = SelectionTable();
        var hayan = Add(s, Hayan, 0, Zone.Home, bowed: true); // a trait: usable while he is bowed
        Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        Add(s, Samurai, 1, Zone.Battlefield, province: 0);
        var unicorn = Add(s, Sarang, 0, Zone.Home);
        var tired = Add(s, Sarang, 0, Zone.Home, bowed: true);
        var crane = Add(s, Scout, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opened");
        var options = Options(e, 0, EmperorP4UnitAbilities.HayanAssign, hayan.Id);
        Check(options.Any(o => o.Enabled && o.TargetIds!.Contains(unicorn.Id)), "the unbowed Unicorn is offered");
        Check(!options.Any(o => o.TargetIds?.Contains(tired.Id) == true || o.TargetIds?.Contains(crane.Id) == true), "a bowed or non-Unicorn Personality is not");
        Accept(e, Offer(e, 0, EmperorP4UnitAbilities.HayanAssign, hayan.Id, unicorn.Id));
        Check(C(e, unicorn).Zone == Zone.Battlefield && C(e, unicorn).ProvinceIndex == 0, "assigned to the current battlefield");
        Eq(C(e, unicorn).AssignedToAttackTurn, e.State.TurnNumber, "as an attacker (his player is the Attacker)");
        Check(!CanUse(e, 0, EmperorP4UnitAbilities.HayanAssign, hayan.Id), "once per turn");
    }

    private static void HayanSensei()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var hayanId = s.Players[0].Provinces[0].CardId!; s.Cards[hayanId].DefinitionId = Hayan; s.Cards[hayanId].FaceUp = true;
        var hayan = s.Cards[hayanId];
        s.Players[0].Clan = "Unicorn"; // Hayan is Loyal
        var sensei = Add(s, Scout, 0, Zone.Home); GiveKeyword(sensei, "Sensei");
        var other = Add(s, Samurai, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: hayanId, SourceIds: e.PaymentOptions(0, 8).First().SourceIds));
        for (var i = 0; i < 4 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(C(e, hayan).Zone, Zone.Home, "Hayan entered play");
        Eq(C(e, sensei).Zone, Zone.DynastyDiscard, "the other Sensei was discarded");
        Eq(C(e, other).Zone, Zone.Home, "a non-Sensei stays");
    }

    private static void ShinoharaAttaches()
    {
        var s = Table();
        var shinohara = Add(s, Shinohara, 1, Zone.Battlefield, province: 0);
        var host = Add(s, Samurai, 1, Zone.Battlefield, province: 0);
        var away = Add(s, Samurai, 1, Zone.Home);
        var enemy = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var follower = Add(s, Follower, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var gold = e.QueryActions(1).First(a => a.Type == "useAbility" && a.Command?.AbilityId == EmperorP4UnitAbilities.ShinoharaAttach && a.Enabled).Gold;
        Check(gold is not null && gold.Payments.Any(), "the attach is priced through the Gold path");
        Reject(e, Use(e, 1, EmperorP4UnitAbilities.ShinoharaAttach, shinohara.Id, selections: [Pick("attachment", follower.Id), Pick("personality", away.Id)]));
        Reject(e, Use(e, 1, EmperorP4UnitAbilities.ShinoharaAttach, shinohara.Id, selections: [Pick("attachment", follower.Id), Pick("personality", enemy.Id)]));
        var option = e.QueryActions(1).First(a => a.Type == "useAbility" && a.Command?.AbilityId == EmperorP4UnitAbilities.ShinoharaAttach && a.Enabled);
        Accept(e, option.Command! with { Selections = [Pick("attachment", follower.Id), Pick("personality", host.Id)], TargetId = null });
        for (var i = 0; i < 4 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(C(e, follower).AttachedTo, host.Id, "the Follower is attached to the targeted Personality");
        Check(!e.State.Players[1].Hand.Contains(follower.Id), "it left the hand");
        Check(e.State.Cards.Values.Any(c => c.OwnerId == 1 && c.Bowed && c.Zone is Zone.Stronghold or Zone.Home && c.Id != away.Id), "its Gold Cost was paid");
    }
}
