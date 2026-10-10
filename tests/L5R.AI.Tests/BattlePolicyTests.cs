using L5R.AI;
using L5R.Core;

// Issue #788: Mendon's six bot changes, each pinned with a small synthetic table. The policy is card-agnostic, so the abilities used here are found in the catalogue by what
// they do rather than by name.
internal static class BattlePolicyTests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("Battle odds: the attacker needs more than defenders plus Strength; the spare Force is what a unit may leave with (16 v 4, Strength 7 spares 4)", Odds),
        ("Pass when won (rule 1): a winning defender or a province-destroying attacker passes instead of acting, unless the action gains or takes Honor", PassWhenWon),
        ("Pass when won (rule 1): a spare unit moves to an opposed battlefield, a needed one stays, and a threatening enemy Personality is still removed", WinningMoves),
        ("In-play abilities come before hand cards when the enemy has little to answer with (rule 1)", InPlayFirst),
        ("Attack targets (rule 2): the highest Force the attack can destroy, ties to the highest unit gold, augments counted", AttackTargets),
        ("Bow targets (rule 3): most attachments, then gold; without attachments a destroyer, a bow ability, then other Personalities", BowTargets),
        ("Losing defender (rule 4): sends a non-contributing unit home to save it, a fighting unit only when the Province still holds", LosingDefender),
        ("First-action reactions (rule 5): claimed only when the battle is not already won", EngageReactions),
        ("Border Keep (rule 6): refreshes once with 22 Gold and three stocked provinces, never with 21", BorderKeep),
        ("Trade attacks: a battle won outright is worth making without razing, a tie only when the defenders are worth clearly more, attachments and Personalities count", TradeAttacks),
        ("Trade attacks: the bot declares and assigns such an attack, and not when the rule is off or the defence is too strong", TradeAttackChoice),
    ];

    private static CardView Home(string id, int owner, int force, int gold, string? attachedTo = null, CardType? type = null)
    {
        var def = type is null ? Person() with { GoldCost = gold } : CardCatalog.Cards.Values.First(d => d.Type == type) with { GoldCost = gold };
        return new CardView(id, def, owner, Zone.Home, true, false, null, attachedTo, force);
    }

    private static void TradeAttacks()
    {
        CardView[] none = [];
        // Won outright without razing: one Force 6 unit against a Force 4 defender worth 5 Gold plus the Personality bonus.
        var mine = new[] { Home("a", 0, 6, 3) };
        var enemy = new[] { Home("d", 1, 4, 5) };
        var win = BattlePolicy.PlanTrade(mine, mine, enemy);
        Check(win is { Tie: false, Losses: 0, Kills: 7, Net: 7 }, $"a won battle nets the defenders' worth (7): {win}");
        Check(BattlePolicy.PlanTrade([Home("w", 0, 3, 3)], [Home("w", 0, 3, 3)], enemy) is null, "Force 3 against a Force 4 defence loses the army for nothing: no trade");
        Check(BattlePolicy.PlanTrade(mine, mine, none) is null, "nothing to destroy, nothing to trade");
        // A tie destroys both armies: worthwhile only when the defenders are worth clearly more than the attackers.
        var even = new[] { Home("a", 0, 4, 2) };
        var rich = BattlePolicy.PlanTrade(even, even, [Home("d", 1, 4, 9)]);
        Check(rich is { Tie: true, Net: 3 }, $"a tie against a 9 Gold defender (11 with the bonus) for a 2 Gold attacker (4): net 3, {rich}");
        Check(BattlePolicy.PlanTrade(even, even, [Home("d", 1, 4, 3)]) is null, "a tie against a cheaper defender is a bad trade");
        // Attachments are part of the defending unit's worth.
        var armed = new[] { Home("d", 1, 4, 3), Home("blade", 1, 0, 6, "d", CardType.Item) };
        var withItem = BattlePolicy.PlanTrade(even, even, armed);
        Check(withItem is { Tie: true } && withItem.Kills == 11, $"the attachment's 6 Gold is destroyed with its Personality: kills {withItem?.Kills}");
        // Sends the smallest sufficient subset with a spare point of Force, keeping the rest home.
        var big = Home("big", 0, 9, 4); var small = Home("small", 0, 2, 1);
        var subset = BattlePolicy.PlanTrade([big, small], [big, small], enemy);
        Check(subset is { UnitIds: ["big"] }, $"the Force 9 unit alone beats a Force 4 defence with room to spare: [{string.Join(",", subset?.UnitIds ?? [])}]");
    }

    private static MatchView AttackView(int ownForce, int enemyForce, int enemyGold, int strength)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        ProvinceView[] Provinces(int owner) => view.Players[owner].Provinces.Select(p => p with { Strength = strength, Destroyed = false }).ToArray();
        return view with
        {
            Phase = Phase.AttackDeclaration, ActivePlayerId = 0, PriorityPlayerId = 0,
            Players = [view.Players[0] with { Home = [Home("a", 0, ownForce, 3)], Army = [], Provinces = Provinces(0) },
                view.Players[1] with { Home = [Home("d", 1, enemyForce, enemyGold)], Army = [], Provinces = Provinces(1) }]
        };
    }

    private static void TradeAttackChoice()
    {
        ActionOption Declare(MatchView v) => new("declareAttack", "Declare", true, null, new("declareAttack", 0, v.Version));
        ActionOption Attack(MatchView v) => new("attack", "Attack", true, null, new("attack", 0, v.Version, Assignments: []));
        var on = new BotContext { UseTradeAttacks = true };
        var strong = AttackView(6, 4, 5, 9); // beats the defender, cannot raze a Strength 9 Province
        Check(TrainingOpponent.Choose(strong, [Pass(strong), Declare(strong)], on).Command?.Type == "declareAttack", "a battle it can win outright is declared even though the Province would hold");
        var assigned = TrainingOpponent.Choose(strong, [Pass(strong), Attack(strong)], on).Command;
        Check(assigned is { Type: "attack", Assignments: { Count: 1 } } && assigned.Assignments.ContainsKey("a"), "the winning unit is assigned to the attack");
        var off = TrainingOpponent.Choose(strong, [Pass(strong), Declare(strong)]).Command;
        Check(off?.Type == "pass", "the rule is off by default: the old caution holds, no declaration");
        var weak = AttackView(3, 4, 5, 9);
        Check(TrainingOpponent.Choose(weak, [Pass(weak), Declare(weak)], on).Command?.Type == "pass", "a defence that would win is not attacked");
        var razing = AttackView(12, 4, 5, 3); // 12 > 4 + 3: the ordinary planner razes the Province
        Check(TrainingOpponent.Choose(razing, [Pass(razing), Declare(razing)], on).Command?.Type == "declareAttack", "an attack that razes the Province is unchanged");
    }

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static CardDefinition Person(int force = 4) => CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force == force);
    private static CardView Unit(string id, int owner, int force, int province = 0, bool bowed = false, CardDefinition? def = null) =>
        new(id, def ?? Person(), owner, Zone.Battlefield, true, bowed, province, null, force);
    /// <summary>True when the ability's cost (the text before the colon) bows something, which would take Force out of the battle.</summary>
    private static bool BowCost(AbilityDefinition a) => (a.Label ?? "").Split(':') is { Length: > 1 } parts && parts[0].Contains("Bow", StringComparison.OrdinalIgnoreCase);
    private static string FindAbility(Func<string, AbilityDefinition, AbilityIntent, bool> test) =>
        AbilityCatalog.All.Where(p => !p.Key.StartsWith("training-", StringComparison.Ordinal) && test(p.Key, p.Value, AbilityIntent.Of(p.Value))).OrderBy(p => p.Key, StringComparer.Ordinal).First().Key;

    /// <summary>A battle at province 0 with seat 0 (the bot) holding <paramref name="own"/> Force against <paramref name="enemy"/>; every Province has Strength <paramref name="strength"/>.</summary>
    private static MatchView Battle(int own, int enemy, int strength, bool botAttacks = true, CardView[]? ownExtra = null, CardView[]? enemyExtra = null, int enemyHand = 5)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        ProvinceView[] Provinces(int owner) => view.Players[owner].Provinces.Select(p => p with { Strength = strength }).ToArray();
        var mine = new List<CardView> { Unit("own-main", 0, own) }; mine.AddRange(ownExtra ?? []);
        var theirs = new List<CardView> { Unit("enemy-main", 1, enemy) }; theirs.AddRange(enemyExtra ?? []);
        return view with
        {
            Phase = Phase.Battle, CurrentBattleProvince = 0, ActivePlayerId = botAttacks ? 0 : 1, PriorityPlayerId = 0,
            Players = [view.Players[0] with { Army = mine.ToArray(), Home = [], Provinces = Provinces(0) },
                view.Players[1] with { Army = theirs.ToArray(), Home = [], HandCount = enemyHand, Provinces = Provinces(1) }]
        };
    }

    private static ActionOption Pass(MatchView v) => new("pass", "Pass", true, null, new("pass", 0, v.Version));
    private static ActionOption Use(MatchView v, string id, string? card = "own-main", string? target = null, string[]? targets = null) =>
        new("useAbility", id, true, null, new("useAbility", 0, v.Version, CardId: card, AbilityId: id, TargetId: target), card, targets);
    private static string Chose(MatchView v, params ActionOption[] options) => TrainingOpponent.Choose(v, options).Command is { } c ? c.AbilityId ?? c.Type : "none";

    private static void Odds()
    {
        var o = new BattlePolicy.Odds(true, 16, 4, 7);
        Check(o.Satisfied && o.Winning && o.Margin == 4, "16 against 4 with Strength 7 destroys the Province and spares 4 Force: " + o.Margin);
        Check(!new BattlePolicy.Odds(true, 11, 4, 7).Satisfied && new BattlePolicy.Odds(true, 11, 4, 7).Winning && new BattlePolicy.Odds(true, 12, 4, 7).Satisfied, "11 against 4 with Strength 7 wins the battle but not the Province (needs more than 11)");
        Check(new BattlePolicy.Odds(false, 5, 5, 7).Satisfied && !new BattlePolicy.Odds(false, 4, 5, 7).Winning, "a tie goes to the defender");
        var holding = new BattlePolicy.Odds(false, 8, 12, 7);
        Check(!holding.WouldFallAfterLosing(0) && !holding.WouldFallAfterLosing(3) && holding.WouldFallAfterLosing(4), "a defender 8 v 12 holds a Strength 7 Province, and would lose it only after losing 4 Force");
    }

    private static void PassWhenWon()
    {
        var plain = FindAbility((id, a, i) => a.Timing == "Battle" && a.Target == "none" && !i.IsReaction && i.Rank == EffectRank.Help && !i.HonorGain && !i.HonorLoss && !i.Draws);
        var gain = FindAbility((id, a, i) => a.Timing is "Battle" or "BattleOrOpen" && a.Target == "none" && !i.IsReaction && i.HonorGain && !i.HonorLoss && i.Rank == EffectRank.Help && !BowCost(a));
        var winning = Battle(16, 4, 7);
        Check(Chose(winning, Pass(winning), Use(winning, plain)) == "pass", "a province-destroying attacker passes instead of acting");
        Check(Chose(winning, Pass(winning), Use(winning, gain)) == gain, "but an action that gains Honor is still taken");
        var short_ = Battle(8, 4, 7);
        Check(Chose(short_, Pass(short_), Use(short_, plain)) == plain, "an attacker that does not yet destroy the Province keeps acting");
        var defender = Battle(6, 4, 7, botAttacks: false);
        Check(Chose(defender, Pass(defender), Use(defender, plain)) == "pass", "a defender that is winning passes");
        var losing = Battle(3, 6, 7, botAttacks: false);
        Check(Chose(losing, Pass(losing), Use(losing, plain)) == plain, "a defender that is losing keeps acting");
    }

    private static void WinningMoves()
    {
        var move = FindAbility((id, a, i) => a.Timing is "Battle" or "BattleOrOpen" && BattlePolicy.Moves(id) && a.Target.StartsWith("own", StringComparison.Ordinal));
        var spare = Unit("own-spare", 0, 3, 0);
        var needed = Unit("own-needed", 0, 6, 0);
        var opposedElsewhere = Unit("enemy-elsewhere", 1, 2, 1);
        MatchView View(CardView mover) => Battle(16 - mover.Force, 4, 7, ownExtra: [mover], enemyExtra: [opposedElsewhere]);
        ActionOption MoveOption(MatchView v, CardView mover) => new("useAbility", move, true, null, new("useAbility", 0, v.Version, CardId: "own-main", AbilityId: move, TargetId: mover.Id), "own-main", [mover.Id], [1], Movement: new MoveDestinationView(1, [], true));
        var v1 = View(spare); var v2 = View(needed);
        Check(Chose(v1, Pass(v1), MoveOption(v1, spare)) == move, "a unit whose Force fits inside the spare margin moves to the opposed battlefield");
        Check(Chose(v2, Pass(v2), MoveOption(v2, needed)) == "pass", "a unit the win depends on stays");
        var threatDef = CardCatalog.Cards.Values.FirstOrDefault(d => d.Type == CardType.Personality && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && AbilityIntent.Of(a) is { Rank: EffectRank.Destroy or EffectRank.Bow, Side: not TargetSide.Own } && a.Timing is "Battle" or "BattleOrOpen") == true);
        Check(threatDef is not null, "the catalogue has a Personality that destroys or bows");
        var threat = Unit("enemy-threat", 1, 2, 0, def: threatDef);
        var bow = FindAbility((id, a, i) => a.Timing == "Battle" && i.Rank == EffectRank.Bow && i.Side == TargetSide.Enemy && !i.IsReaction && a.Target == "enemy-personality" && !BowCost(a) && !a.IssuesDuel && (a.Label ?? "").StartsWith("Bow", StringComparison.Ordinal));
        var fight = Battle(16, 4, 7, enemyExtra: [threat]);
        Check(Chose(fight, Pass(fight), Use(fight, bow, target: "enemy-threat", targets: ["enemy-threat"])) == bow, "an enemy that could undo the result is bowed even though the battle is won");
        var harmless = Battle(16, 4, 7);
        Check(Chose(harmless, Pass(harmless), Use(harmless, bow, target: "enemy-main", targets: ["enemy-main"])) == "pass", "bowing an enemy that cannot do anything is not needed");
    }

    private static void InPlayFirst()
    {
        var plain = FindAbility((id, a, i) => a.Timing == "Battle" && a.Target == "none" && !i.IsReaction && i.Rank == EffectRank.Help && !i.HonorGain && !i.HonorLoss && !i.Draws);
        var calm = Battle(8, 4, 7, enemyHand: 0);
        var inPlay = new ActionOption("useAbility", plain, true, null, new("useAbility", 0, calm.Version, CardId: "own-main", AbilityId: plain), "own-main");
        var fromHand = new ActionOption("useAbility", plain, true, null, new("useAbility", 0, calm.Version, CardId: "hand-1", AbilityId: plain), "hand-1");
        var withHand = calm with { Players = [calm.Players[0] with { Hand = [new CardView("hand-1", Person(2), 0, Zone.Hand, true, false, null, null, 0)] }, calm.Players[1]] };
        var decision = TrainingOpponent.Choose(withHand, [Pass(withHand), fromHand, inPlay]).Command;
        Check(decision?.CardId == "own-main", "against an enemy with nothing in hand the ability already in play comes before the one from the hand: " + decision?.CardId);
    }

    private static void AttackTargets()
    {
        CardView T(string id, int force, int gold) => Unit(id, 1, force, def: CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.GoldCost == gold)) with { Force = force };
        var a = T("a", 3, 4); var b = T("b", 3, 6); var c = T("c", 5, 8); var d = T("d", 2, 9);
        var all = new List<CardView> { a, b, c, d };
        var order = new[] { a, b, c, d }.OrderByDescending(t => BattlePolicy.AttackTargetScore(t, 4, all)).Select(t => t.Id).ToArray();
        Check(order[0] == "b" && order[1] == "a" && order[2] == "d" && order[3] == "c", "strength 4: the Force 3 targets first (higher gold wins), then Force 2, and the Force 5 target it cannot destroy last: " + string.Join(",", order));
        var pumped = new[] { a, b, c, d }.OrderByDescending(t => BattlePolicy.AttackTargetScore(t, 6, all)).First().Id;
        Check(pumped == "c", "an augment that lifts the attack to 6 makes the Force 5 target the best");
        var follower = new CardView("fol", Person(1), 1, Zone.Battlefield, true, false, 0, "a", 1);
        Check(BattlePolicy.UnitGold(a, [a, follower]) == a.Definition!.GoldCost + follower.Definition!.GoldCost, "attachments and followers add to the unit's printed gold");
        var attack = FindAbility((id, ab, i) => ab.Target == "attack" && BattlePolicy.AttackStrength(id) is 4);
        Check(BattlePolicy.AttackStrength(attack) == 4, "the attack's printed strength is read from its label");
    }

    private static void BowTargets()
    {
        CardView P(string id, int force, CardDefinition? def = null) => Unit(id, 1, force, def: def ?? Person());
        CardView Att(string id, string host) => new(id, CardCatalog.Cards.Values.First(d => d.Type == CardType.Item), 1, Zone.Battlefield, true, false, 0, host, 0);
        var one = P("one", 4); var two = P("two", 3); var none = P("none", 5);
        var all = new List<CardView> { one, two, none, Att("i1", "one"), Att("i2", "two"), Att("i3", "two") };
        Check(BattlePolicy.BowOrder([one, two, none], all, true).First().Id == "two", "most attachments first");
        var destroyer = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Target == "attack") == true);
        var plain = P("plain", 5); var shooter = P("shooter", 2, destroyer);
        Check(BattlePolicy.BowOrder([plain, shooter], [plain, shooter], false).First().Id == "shooter", "without attachments a Personality that destroys (an Attack counts) comes before a stronger plain one");
        var destroyingItem = CardCatalog.Cards.Values.FirstOrDefault(d => d.Type == CardType.Item && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && AbilityIntent.Of(a).Rank == EffectRank.Destroy) == true);
        if (destroyingItem is not null)
        {
            var item = new CardView("item", destroyingItem, 1, Zone.Battlefield, true, false, 0, "plain", 0);
            Check(BattlePolicy.BowOrder([plain, shooter, item], [plain, shooter, item], false).First().Id == "shooter", "a destroying Personality outranks a destroying attachment");
            Check(BattlePolicy.BowOrder([plain, item], [plain, item], false).First().Id == "item", "a destroying attachment outranks a plain Personality");
        }
    }

    private static void LosingDefender()
    {
        var home = FindAbility((id, a, i) => a.Timing == "Battle" && BattlePolicy.SendsHome(id) && a.Target == "defending");
        var bowed = Unit("own-bowed", 0, 0, 0, bowed: true);
        MatchView Defend(int own, int enemy, CardView[]? extra = null) => Battle(own, enemy, 7, botAttacks: false, ownExtra: extra);
        ActionOption Send(MatchView v, string unit) => Use(v, home, "own-main", unit, [unit]);
        var saveBowed = Defend(3, 6, [bowed]);
        Check(Chose(saveBowed, Pass(saveBowed), Send(saveBowed, "own-bowed")) == home, "a bowed unit that adds no Force is sent home to be saved");
        var holds = Defend(5, 6);
        Check(Chose(holds, Pass(holds), Send(holds, "own-main")) == home, "a lone fighting unit (5 v 6) may leave: the attacker's 6 does not beat 0 plus Strength 7");
        var falls = Defend(5, 14, [Unit("own-two", 0, 4, 0)]);
        Check(Chose(falls, Pass(falls), Send(falls, "own-two")) == "pass", "but not when leaving would let the Province fall (9 v 14 holds Strength 7; 5 v 14 does not)");
    }

    private static void EngageReactions()
    {
        var reaction = FindAbility((id, a, i) => a.Timing == "Reaction" && !i.Negates && a.Target != "none" && i.Rank == EffectRank.Bow && i.Side == TargetSide.Enemy);
        var close = Battle(6, 6, 7) with { PendingChoice = new("engageReaction", 0, null, "Engage") };
        var won = Battle(16, 4, 7) with { PendingChoice = new("engageReaction", 0, null, "Engage") };
        ActionOption React(MatchView v) => new("useAbility", reaction, true, null, new("useAbility", 0, v.Version, CardId: "own-main", AbilityId: reaction, TargetId: "enemy-main"), "own-main", ["enemy-main"]);
        ActionOption Decline(MatchView v) => new("declineReaction", "Decline", true, null, new("declineReaction", 0, v.Version));
        Check(Chose(close, Decline(close), React(close)) == reaction, "an unsettled battle: the first-action reaction is played");
        Check(Chose(won, Decline(won), React(won)) == "declineReaction", "an already won battle: the bot would only pass with the first action, so it declines");
    }

    private static void BorderKeep()
    {
        var holding = CardCatalog.Cards.Values.First(d => d.Type == CardType.Holding && d.GoldProduction >= 3);
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        MatchView WithGold(int gold)
        {
            var own = view.Players[0];
            var stronghold = own.Stronghold with { Definition = own.Stronghold.Definition! with { GoldProduction = gold - holding.GoldProduction * 2 } };
            var provinces = own.Provinces.Select((p, i) => p with { Destroyed = false, Card = i < 3 ? new CardView("prov-" + i, holding, 0, Zone.Province, true, false, i, null, 0) : null }).ToArray();
            var homes = new[] { new CardView("h1", holding, 0, Zone.Home, true, false, null, null, 0), new CardView("h2", holding, 0, Zone.Home, true, false, null, null, 0) };
            return view with { Phase = Phase.Action, ActivePlayerId = 0, PriorityPlayerId = 0, Players = [own with { Stronghold = stronghold, Home = homes, Provinces = provinces }, view.Players[1]] };
        }
        ActionOption Border(MatchView v) => new("useAbility", "border-game", true, null, new("useAbility", 0, v.Version, CardId: "keep", AbilityId: "border-game", CardIds: []), "keep", ["prov-0", "prov-1", "prov-2"]);
        var rich = WithGold(22);
        var decision = TrainingOpponent.Choose(rich, [Pass(rich), Border(rich)]).Command;
        Check(decision?.AbilityId == "border-game" && decision.CardIds is { Length: 3 }, "22 Gold and three stocked provinces: the Border Keep refreshes all three");
        var poor = WithGold(21);
        var held = TrainingOpponent.Choose(poor, [Pass(poor), Border(poor)]).Command;
        Check(held?.AbilityId != "border-game" || held.CardIds is not { Length: 3 }, "21 Gold: the refresh is not taken under the new rule");
    }
}
