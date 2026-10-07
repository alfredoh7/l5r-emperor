using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

internal static class Wave689Tests
{
    private static void Check(bool value, string message) => Batch336Support.Check(value, message);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    public static void Run(TestRunner runner) => runner.Run("wave-689", [
        ("P2: Renyu discounts only other expensive Personalities and Respect taxes both players", RecruitPrices),
        ("P11: Kojinrue can attack bowed, ordinary Personalities cannot", KojinrueAssignment),
        ("P11: Natsumi, Jin-e and Colossus enforce their specific attachment restrictions", Attachments),
        ("P11: Jin-e pays bow and gives both targets a persistent two-stat Poison token", Poison),
        ("P7: Gyoken and Legion each negate only one enemy destruction per game", GameWatches),
        ("P7: watch IDs remain unique after save, consumption and load", WatchIds),
        ("P7: Earth's Protection offers both signs to a defender, only a bonus to an attacker, and rejects overflow", EarthChoices),
        ("P2: all six Monks waive recruit cost only with their named Ring", RingWaivers),
        ("P2: Ujirou combines Mantis and Defender discounts", Ujirou),
        ("P2: Kanaye discounts paid Strategy actions but not Personality actions", Kanaye),
        ("P11: controller-only Political bars also apply to late target selection", PoliticalTargets),
        ("P11: Reprieve applies to all enemy actions and expires at the phase boundary", Reprieve),
        ("P11: Sudden Blockade refuses first-turn use and locks only the named province until next turn", Blockade),
        ("P2: Hamuro draws three and leaves play; Okazaki grants temporary Conqueror", LimitedEffects),
        ("P2: Rokkaku's private top-four look permits an optional bottom card and reorder", Rokkaku),
        ("P2: Shinjo Tselu's discard interrupts targeting before the announced action resolves", Tselu),
        ("P2: Nishoji's duel stat uses printed Force despite live Force modifiers", Nishoji),
        ("P7: Earth's Protection pays province cost and grants Force and one linked bow-or-move negation", EarthEffect),
    ]);

    private static void RecruitPrices()
    {
        var state = Batch336Support.Table(Phase.Dynasty, 0);
        var id = state.Players[0].Provinces[0].CardId!;
        state.Cards[id].DefinitionId = "training-crane-veteran"; state.Cards[id].FaceUp = true;
        var plain = MatchEngine.FromTestState(state).QuotePurchase(0, id).Cost;
        Batch336Support.Add(state, "oracle-l5r-10275", 0, Zone.Home);
        var discounted = MatchEngine.FromTestState(state).QuotePurchase(0, id).Cost;
        Check(discounted == Math.Max(0, plain - 2), "Renyu gives the printed-cost discount");
        // Respect applies regardless of who controls the Kata.
        foreach (var controller in new[] { 0, 1 })
        {
            var copy = MatchEngine.FromTestState(state).State;
            copy.Players[0].Honor = -20;
            Batch336Support.Add(copy, "oracle-l5r-10952", controller, Zone.Home);
            var quote = MatchEngine.FromTestState(copy).QuotePurchase(0, id);
            Check(quote.Cost == discounted + 2, "Respect taxes a recruit by either player");
        }
    }

    private static void KojinrueAssignment()
    {
        var state = Batch336Support.Table(Phase.InfantryDeclaration, 0);
        state.UnresolvedBattlefields = [0, 1, 2, 3];
        var kojinrue = Batch336Support.Add(state, "oracle-l5r-10284", 0, Zone.Home, bowed: true);
        var ordinary = Batch336Support.Add(state, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(state);
        Reject(e, new("attack", 0, e.Version, Assignments: new() { [ordinary.Id] = 0 }));
        Accept(e, new("attack", 0, e.Version, Assignments: new() { [kojinrue.Id] = 0 }));
        Check(e.State.Cards[kojinrue.Id].Zone == Zone.Battlefield && e.State.Cards[kojinrue.Id].Bowed, "Kojinrue attacks without straightening");
    }

    private static void Attachments()
    {
        foreach (var id in new[] { "oracle-l5r-10864", "oracle-l5r-10868", "oracle-l5r-10666" })
        {
            var state = Batch336Support.Table(Phase.Action, 0);
            var host = Batch336Support.Add(state, id, 0, Zone.Home);
            var e = MatchEngine.FromTestState(state);
            var follower = CardCatalog.Get("training-follower");
            var item = CardCatalog.Get("training-item");
            Check((bool)Call(e, "RestrictionRefusesAttachment", follower, host)! == (id != "oracle-l5r-10868"), "Follower bar matches printing");
            Check((bool)Call(e, "RestrictionRefusesAttachment", item, host)! == (id != "oracle-l5r-10868" || item.Keywords.Contains("Armor")), "Item bar matches printing");
            if (id == "oracle-l5r-10666") Check((bool)Call(e, "RestrictionBarsChallenge", host)!, "Colossus will not challenge");
        }
    }

    private static void Poison()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var jin = Batch336Support.Add(state, "oracle-l5r-10868", 0, Zone.Home);
        var other = Batch336Support.Add(state, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(state);
        Accept(e, Batch336Support.Offer(e, 0, Wave9701Abilities.JinE, jin.Id, other.Id));
        Check(e.State.Cards[jin.Id].Bowed, "Bow cost paid");
        foreach (var cardId in new[] { jin.Id, other.Id })
            Check(JsonSerializer.Serialize(e.State.Cards[cardId]).Contains("Poison"), "Both Personalities receive Poison");
    }

    private static void GameWatches()
    {
        foreach (var id in new[] { Wave9601Abilities.Gyoken, Wave9601Abilities.Legion })
        {
            var state = Batch336Support.Table();
            var host = Batch336Support.Add(state, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var card = Batch336Support.Add(state, id, 0, id == Wave9601Abilities.Legion ? Zone.Attached : Zone.Battlefield, province: 0, parent: id == Wave9601Abilities.Legion ? host.Id : null);
            var e = MatchEngine.FromTestState(state);
            Check(!e.TryConsumeWatch(WatchKind.Destroy, card, 0), "Own effects do not spend the watch");
            Check(e.TryConsumeWatch(WatchKind.Destroy, card, 1), "First enemy destruction negated");
            var later = e.State; later.TurnNumber++;
            var loaded = MatchEngine.FromTestState(later);
            Check(!loaded.TryConsumeWatch(WatchKind.Destroy, loaded.State.Cards[card.Id], 1), "Next turn does not reset a game watch");
        }
    }

    private static void WatchIds()
    {
        var state = Batch336Support.Table();
        var card = Batch336Support.Add(state, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(state);
        var first = e.AddNegationWatch(new() { Kind = WatchKind.Bow, TargetCardId = card.Id, OwnerPlayerId = 0 });
        Check(e.TryConsumeWatch(WatchKind.Bow, card, 1), "First watch consumed");
        var loadedState = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!;
        var loaded = MatchEngine.FromTestState(loadedState);
        var second = loaded.AddNegationWatch(new() { Kind = WatchKind.Move, TargetCardId = card.Id, OwnerPlayerId = 0 });
        Check(first.Id != second.Id, "Consumed IDs cannot be reused after loading");
    }

    private static void EarthChoices()
    {
        var state = Batch336Support.Table();
        var e = MatchEngine.FromTestState(state);
        var defender = ((string Key, string Label)[]?)Call(e, "Wave9601ChoiceOptions", Wave9601Abilities.EarthsProtection, 0);
        var attacker = ((string Key, string Label)[]?)Call(e, "Wave9601ChoiceOptions", Wave9601Abilities.EarthsProtection, 1);
        Check(defender!.Any(c => c.Key == "1") && defender!.Any(c => c.Key == "-1"), "Defender can choose bonus or penalty");
        Check(attacker!.All(c => int.Parse(c.Key) > 0), "Attacker can choose only a bonus");
        try
        {
            Call(e, "ValidateWave9601", Wave9601Abilities.EarthsProtection, new GameCommand("useAbility", 0, e.Version, Choice: int.MinValue.ToString()), state.Players[0]);
            throw new Exception("Malformed amount accepted");
        }
        catch (Exception error) when (error.GetType().Name == "RuleViolation") { }
    }

    private static void RingWaivers()
    {
        foreach (var (id, name) in new[] { (10474, "Void"), (10483, "Void"), (10461, "Earth"), (10442, "Earth"), (10467, "Water"), (10491, "Air") })
        {
            var state = Batch336Support.Table(Phase.Dynasty, 0);
            var card = state.Cards[state.Players[0].Provinces[0].CardId!];
            card.DefinitionId = "oracle-l5r-" + id; card.FaceUp = true;
            var without = MatchEngine.FromTestState(state).QuotePurchase(0, card.Id);
            var ring = CardCatalog.Cards.Values.First(c => c.Type == CardType.Ring && c.Name == (name == "Void" ? "Ring of the Void" : "Ring of " + name) && !c.ScenarioOnly);
            Batch336Support.Add(state, ring.Id, 0, Zone.Celestial);
            var with = MatchEngine.FromTestState(state).QuotePurchase(0, card.Id);
            Check(without.Cost > 0 && with.Cost == 0, "Named Ring waives cost for " + id);
            var wrongOwner = MatchEngine.FromTestState(state).State;
            wrongOwner.Cards.Values.First(c => c.DefinitionId == ring.Id).OwnerId = 1;
            Check(MatchEngine.FromTestState(wrongOwner).QuotePurchase(0, card.Id).Cost == without.Cost, "Opponent's Ring does not waive cost");
        }
    }

    private static void Ujirou()
    {
        var state = Batch336Support.Table(Phase.Battle, 1);
        var card = state.Cards[state.Players[0].Provinces[0].CardId!];
        card.DefinitionId = "oracle-l5r-10842"; card.FaceUp = true;
        state.Players[1].Clan = "Mantis";
        var e = MatchEngine.FromTestState(state);
        Check(e.QuotePurchase(0, card.Id, viaReserve: true).Cost == Math.Max(0, CardCatalog.Get(card.DefinitionId).GoldCost - 6), "Both discounts stack");
    }

    private static void Kanaye()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var source = Batch336Support.Add(state, "oracle-l5r-10396", 0, Zone.Hand);
        var before = MatchEngine.FromTestState(state);
        var command = new GameCommand("useAbility", 0, before.Version, CardId: source.Id, AbilityId: Wave9701Abilities.Blockade, TargetPlayerId: 1, Province: 0);
        var price = before.QuoteAbilityGold(command)!.Cost;
        Batch336Support.Add(state, "oracle-l5r-10470", 0, Zone.Home);
        var after = MatchEngine.FromTestState(state);
        Check(after.QuoteAbilityGold(command)!.Cost == Math.Max(0, price - 1), "Kanaye discounts a Strategy's ability");
        var personality = Batch336Support.Add(state, "oracle-l5r-10868", 0, Zone.Home);
        Check(MatchEngine.FromTestState(state).QuoteAbilityGold(command with { CardId = personality.Id, AbilityId = Wave9701Abilities.JinE }) is null, "No price created for a free Personality action");
    }

    private static bool RuleRefuses(Action action)
    {
        try { action(); return false; }
        catch (Exception error) when (error.GetType().Name == "RuleViolation") { return true; }
    }

    private static void PoliticalTargets()
    {
        foreach (var id in new[] { "oracle-l5r-10837", "oracle-l5r-10666" })
        {
            var state = Batch336Support.Table(Phase.Action, 0);
            var target = Batch336Support.Add(state, id, 0, Zone.Home);
            var actor = Batch336Support.Add(state, "training-crane-samurai", 0, Zone.Home);
            var e = MatchEngine.FromTestState(state);
            var command = new GameCommand("useAbility", 0, e.Version, CardId: actor.Id, TargetId: target.Id);
            var political = new AbilityDefinition("Open", "Political test", "personality", ActionKeywords: ["Political"]);
            Check(RuleRefuses(() => Call(e, "RequireTargetNotBarredForAction", actor, target, command, political)), "Own Political late target refused");
            Check(!RuleRefuses(() => Call(e, "RequireTargetNotBarredForAction", actor, target, command, political with { ActionKeywords = [] })), "Own non-Political target allowed");
            Check(!RuleRefuses(() => Call(e, "RequireTargetNotBarredForAction", actor, target, command with { PlayerId = 1 }, political)), "Enemy Political target allowed");
        }
    }

    private static void Reprieve()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var source = Batch336Support.Add(state, "oracle-l5r-10713", 0, Zone.Hand);
        var target = Batch336Support.Add(state, "training-crane-samurai", 0, Zone.Home);
        var actor = Batch336Support.Add(state, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(state);
        Accept(e, Batch336Support.Offer(e, 0, Wave9701Abilities.Reprieve, source.Id, target.Id));
        var command = new GameCommand("useAbility", 1, e.Version, CardId: actor.Id, TargetId: target.Id);
        var ability = new AbilityDefinition("Open", "test", "personality");
        Check(RuleRefuses(() => Call(e, "RequireTargetNotBarredForAction", actor, target, command, ability)), "Enemy plain action barred");
        Check(!RuleRefuses(() => Call(e, "RequireTargetNotBarredForAction", target, target, command with { PlayerId = 0, CardId = target.Id }, ability)), "Owner's plain action allowed");
        var next = Batch336Support.Edit(e, s => s.Phase = Phase.Dynasty);
        Check(!RuleRefuses(() => Call(next, "RequireTargetNotBarredForAction", actor, target, command, ability)), "Phase boundary expires bar");
    }

    private static void Blockade()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var source = Batch336Support.Add(state, "oracle-l5r-10396", 0, Zone.Hand);
        state.Players[0].TurnsTaken = 1;
        var e = MatchEngine.FromTestState(state);
        Check(!Batch336Support.CanUse(e, 0, Wave9701Abilities.Blockade, source.Id), "First turn blocked");
        var later = e.State; later.Players[0].TurnsTaken = 2;
        var enabled = MatchEngine.FromTestState(later);
        // Run the feature callback with a validated target; payment coverage is shared P2.
        Call(enabled, "RunWave9701", Wave9701Abilities.Blockade, new GameCommand("useAbility", 0, enabled.Version, TargetPlayerId: 1, Province: 0), later.Players[0], source, null);
        var lockedCard = enabled.State.Cards[enabled.State.Players[1].Provinces[0].CardId!];
        var freeCard = enabled.State.Cards[enabled.State.Players[1].Provinces[1].CardId!];
        Check((string?)Call(enabled, "ProvinceEntryLockError", lockedCard) is not null, "Named province locked");
        Check((string?)Call(enabled, "ProvinceEntryLockError", freeCard) is null, "Other province unlocked");
        var nextTurn = Batch336Support.Edit(enabled, s => s.TurnNumber++);
        Check((string?)Call(nextTurn, "ProvinceEntryLockError", lockedCard) is null, "Next turn expires lock");
    }

    private static void LimitedEffects()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var hamuro = Batch336Support.Add(state, "oracle-l5r-10474", 0, Zone.Home);
        var e = MatchEngine.FromTestState(state);
        var before = e.State.Players[0].Hand.Count;
        Accept(e, Batch336Support.Enabled(e, 0, Wave9501Abilities.Hamuro, hamuro.Id).Command!);
        Check(e.State.Players[0].Hand.Count == before + 3 && e.State.Cards[hamuro.Id].Zone != Zone.Home, "Draw three and remove Hamuro");
        var later = Batch336Support.Table(Phase.Action, 0);
        var okazaki = Batch336Support.Add(later, "oracle-l5r-10467", 0, Zone.Home);
        var mantis = Batch336Support.Add(later, "oracle-l5r-10470", 0, Zone.Home);
        var grant = MatchEngine.FromTestState(later);
        Accept(grant, Batch336Support.Offer(grant, 0, Wave9501Abilities.Okazaki, okazaki.Id, mantis.Id));
        Check((bool)Call(grant, "Keyword", grant.State.Cards[mantis.Id], "Conqueror")!, "Conqueror granted");
        Call(grant, "FinishTurn");
        Check(!(bool)Call(grant, "Keyword", grant.State.Cards[mantis.Id], "Conqueror")!, "Conqueror expires at turn end");
    }

    private static void Rokkaku()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var card = Batch336Support.Add(state, "oracle-l5r-10483", 0, Zone.Home);
        var e = MatchEngine.FromTestState(state);
        var oldDeck = e.State.Players[0].FateDeck.ToArray();
        Accept(e, Batch336Support.Enabled(e, 0, Wave9501Abilities.Rokkaku, card.Id).Command!);
        Check(e.GetView(0).PendingLook?.Cards?.Length == 4 && e.GetView(1).PendingLook is null, "Top four visible only to owner");
        HiddenIdsAbsent(e.GetView(1), oldDeck.Take(4));
        Reject(e, new("arrangeTopCards", 0, e.Version, CardIds: ["0:bottom", "1:bottom", "2:top", "3:top"]));
        Accept(e, new("arrangeTopCards", 0, e.Version, CardIds: ["3:top", "1:top", "2:top", "0:bottom"]));
        Check(e.State.Players[0].FateDeck.Take(3).SequenceEqual(new[] { oldDeck[3], oldDeck[1], oldDeck[2] }) && e.State.Players[0].FateDeck.Last() == oldDeck[0], "Reorder and bottom are honored");
    }

    private static void Tselu()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var jin = Batch336Support.Add(state, "oracle-l5r-10868", 0, Zone.Home);
        var tselu = Batch336Support.Add(state, "oracle-l5r-10328", 1, Zone.Home);
        var discard = Batch336Support.Add(state, "training-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(state);
        Accept(e, Batch336Support.Offer(e, 0, Wave9701Abilities.JinE, jin.Id, tselu.Id));
        Check(e.State.PendingChoice?.Type == "forcedDiscard" && !JsonSerializer.Serialize(e.State.Cards[tselu.Id]).Contains("Poison"), "Discard interrupts before Poison effect");
        Accept(e, new("forcedDiscard", 0, e.Version, TargetId: discard.Id));
        Check(e.State.Cards[discard.Id].Zone == Zone.FateDiscard && JsonSerializer.Serialize(e.State.Cards[tselu.Id]).Contains("Poison"), "Action resumes after discard");
    }

    private static void Nishoji()
    {
        var state = Batch336Support.Table();
        var card = Batch336Support.Add(state, "oracle-l5r-10442", 0, Zone.Home);
        Batch336Support.Force(card, 10);
        var e = MatchEngine.FromTestState(state);
        Check((int)Call(e, "DuelStatValue", card, "Chi")! == CardCatalog.Get(card.DefinitionId).Force, "Printed Force replaces duel stat");
    }

    private static void EarthEffect()
    {
        var state = Batch336Support.Table(); state.PriorityPlayerId = 0;
        var host = Batch336Support.Add(state, "oracle-l5r-10275", 0, Zone.Battlefield, province: 0);
        var spell = Batch336Support.Add(state, "oracle-l5r-10349", 0, Zone.Attached, province: 0, parent: host.Id);
        Batch336Support.Add(state, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(state);
        var before = e.ProvinceStrengthForTest(0, 0);
        var force = (int)Call(e, "CardForce", host)!;
        var options = Batch336Support.Options(e, 0, Wave9601Abilities.EarthsProtection, spell.Id);
        Check(options.Any(o => o.Enabled && o.Command!.Choice == "-1"), "Earth choices: " + string.Join("; ", options.Select(o => o.Label + " " + o.Reason)));
        var action = options.First(o => o.Enabled && o.Command!.Choice == "-1").Command!;
        Accept(e, action);
        Check(e.State.Cards[spell.Id].Bowed && e.ProvinceStrengthForTest(0, 0) == before - 1, "Bow and penalty are costs");
        Check((int)Call(e, "CardForce", e.State.Cards[host.Id])! == force + 1, "Equal positive Force bonus");
        var loaded = MatchEngine.FromTestState(e.State);
        Check(loaded.TryConsumeWatch(WatchKind.Bow, loaded.State.Cards[host.Id], 1), "Enemy bow negated");
        Check(!loaded.TryConsumeWatch(WatchKind.Move, loaded.State.Cards[host.Id], 1), "Paired movement watch consumed too");
    }
}
