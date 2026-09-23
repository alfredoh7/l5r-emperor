using static RegressionAssertions;
using System.Text.RegularExpressions;
using L5R.Core;

// "Actions that move Personalities to a battle may be played as Absent Battle actions by either player" (rules lead, 2026-09-19;
// docs/decisions/2026-09-19-battle-rulings-round-2.md, ruling 1). The flag is AbilityDefinition.MovesToBattle; every test reaches the
// abilities through QueryActions and the commands it offers.
internal static class BattleAbsentMovesTests
{
    private const string MoveStrategy = "training-move-battle-strategy", HomeStrategy = "training-move-home-strategy", MoveAbility = "training-move-to-battle", HomeAbility = "training-move-home";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Move-to-battle: every effect case that moves a Personality to a battlefield is on an ability marked MovesToBattle, and every marked ability has such a case (the flag is derived from the effect, not a hand-kept id list)", FlagMatchesEffectCases),
            ("Move-to-battle: the marked abilities are exactly the enumerated wired set", MarkedInventoryPinned),
            ("Empty battle: it is still fought -- passing is always offered to both players, and two passes resolve it", EmptyBattleIsStillFoughtAndPassable),
            ("Move-to-battle: with no unit anywhere, EITHER player may play a move-to-battle action from hand (Attacker and Defender), then the other may too; pass stays available", EitherPlayerWithoutPresence),
            ("Move-to-battle: an action that moves a Personality HOME is not one -- it keeps the ordinary presence rule, and only a unit at the battlefield enables it", HomeMoveNeedsPresence),
            ("Move-to-battle: every marked real card is offered, enabled, to a player with no unit at the battlefield (the ones the old id list missed included)", RealCardsOfferedWithoutPresence),
            ("The Earth Flows: Defender only; the Attacker is shown disabled and rejected, and the attachment's wielder need not be at the battlefield", EarthFlowsDefenderOnly),
        ];
        runner.Run("battle-absent-moves", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"am-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    // A battle at province 0 in its Combat Segment; attacker is ActivePlayerId; nobody has a unit there unless the test adds one.
    private static MatchState Battle(int attacker, int priority)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.ActivePlayerId = attacker; s.PriorityPlayerId = priority;
        s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        return s;
    }

    private static ActionOption? Offer(MatchEngine e, int player, string ability, string? card = null) =>
        e.QueryActions(player).FirstOrDefault(o => o.Type == "useAbility" && o.Command?.AbilityId == ability && (card is null || o.CardId == card));
    private static bool PassOffered(MatchEngine e, int player) => e.QueryActions(player).Any(o => o.Type == "pass" && o.Enabled);
    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);

    // ---- the flag is the effect ---------------------------------------------------------------------------------
    private static string[] IdsOfCase(string line) => Regex.Matches(line, "case \"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();

    // The ability ids whose effect case (in RunAbilityEffects) moves a Personality to a battlefield.
    private static SortedSet<string> IdsMovingToABattlefield(out List<string[]> groups)
    {
        var path = Path.Combine(Support337.RepoRoot(), "src/L5R.Core/CardAbilities.cs");
        var lines = File.ReadAllLines(path);
        var start = Array.FindIndex(lines, l => l.Contains("private void RunAbilityEffects("));
        var end = Array.FindIndex(lines, start, l => l.Contains("private void CompleteAbilityResolution("));
        Check(start > 0 && end > start, "RunAbilityEffects located");
        groups = [];
        var found = new SortedSet<string>(StringComparer.Ordinal);
        for (var i = start; i < end; i++)
        {
            var moves = Regex.IsMatch(lines[i], @"\b(Try)?MoveToBattlefield\(") || Regex.IsMatch(lines[i], @"BringProvincePersonalityIntoPlay\([^;]*CurrentBattleProvince");
            if (!moves) continue;
            var caseLine = i;
            while (caseLine > start && !Regex.IsMatch(lines[caseLine], "^ {12}case \"")) caseLine--;
            var group = new List<string>();
            for (var j = caseLine; j >= start && Regex.IsMatch(lines[j], "^ {12}case \""); j--) group.AddRange(IdsOfCase(lines[j]));
            groups.Add(group.ToArray());
            foreach (var id in group) found.Add(id);
        }
        return found;
    }

    private static void FlagMatchesEffectCases()
    {
        var found = IdsMovingToABattlefield(out var groups);
        Check(groups.Count >= 12, "the scan found the effect cases (" + groups.Count + ")");
        // A move whose destination rule is "the current battlefield" (MoveDestinations.cs, issue #336) brings Personalities to the battle just as a direct
        // MoveToBattlefield case does, through ExecuteMove -- so it is scanned by its MoveSpec. (Predator's adjacent-battlefield move is not one: it takes units OUT
        // of the current battle, and its text requires him to be at it; see MoveDestinationTests.PredatorIsNotAbsent.)
        foreach (var (id, ability) in AbilityCatalog.All.Where(p => p.Value.Move?.Destination == MoveSpec.CurrentBattlefield)) { found.Add(id); groups.Add([id]); }
        foreach (var group in groups)
            Check(group.Any(id => AbilityCatalog.All[id].MovesToBattle), $"an effect that moves a Personality to a battlefield ({string.Join(",", group)}) is not on an ability marked MovesToBattle");
        foreach (var flagged in AbilityCatalog.All.Where(p => p.Value.MovesToBattle).Select(p => p.Key))
            Check(found.Contains(flagged), $"{flagged} is marked MovesToBattle but no effect case moves a Personality to a battlefield");
    }

    private static void MarkedInventoryPinned()
    {
        var marked = string.Join(",", AbilityCatalog.All.Where(p => p.Value.MovesToBattle).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal));
        Eq(marked, "charge-into-danger-move,dislodge-the-foe,earth-flows-move,hawado-move-battlefield,houken-exp-straighten,intervening-spirit-move,mai-move-force-penalty,michi-move,requesting-reinforcements-recruit,return-to-action-move,rikyou-move,sanctuary-of-the-waves-move,skipping-the-puddle-move,sound-of-thunder-move,takasho-move,training-move-current,training-move-to-battle,trusting-instinct-move,your-clan-needs-you");
        foreach (var (id, ability) in AbilityCatalog.All.Where(p => p.Value.MovesToBattle))
            Check(ability.Timing is "Battle" or "BattleOrOpen", $"{id}: a move to the battle is a Battle action");
    }

    // ---- an empty battle ---------------------------------------------------------------------------------------
    private static void EmptyBattleIsStillFoughtAndPassable()
    {
        var s = Battle(attacker: 1, priority: 0);
        var e = MatchEngine.FromTestState(s);
        Check(PassOffered(e, 0), "the Defender can pass in a battle with no unit at all");
        Accept(e, Pass(e));
        Check(PassOffered(e, 1), "and the Attacker can");
        Accept(e, Pass(e));
        Check(e.GetView(0).Events.Any(ev => ev.Type == "battleResolved"), "two passes resolved it: an empty battle is fought, not skipped");
        Eq(e.State.UnresolvedBattlefields.Contains(0), false);
    }

    // ---- either player, no presence ---------------------------------------------------------------------------
    private static void EitherPlayerWithoutPresence()
    {
        foreach (var attacker in new[] { 0, 1 })
        {
            var s = Battle(attacker, priority: attacker);
            var first = Add(s, "training-crane-samurai", attacker, Zone.Home); var second = Add(s, "training-lion-samurai", 1 - attacker, Zone.Home);
            var strategyA = Add(s, MoveStrategy, attacker, Zone.Hand); var strategyB = Add(s, MoveStrategy, 1 - attacker, Zone.Hand);
            Eq(s.Cards.Values.Count(c => c.Zone == Zone.Battlefield), 0, "precondition: no unit anywhere");
            var e = MatchEngine.FromTestState(s);
            foreach (var (player, strategy, mover) in new[] { (attacker, strategyA, first), (1 - attacker, strategyB, second) })
            {
                Eq(e.State.PriorityPlayerId, player);
                var offer = Offer(e, player, MoveAbility, strategy.Id);
                Check(offer is not null && offer.Enabled, $"player {player} (attacker {attacker}) is offered the move: {offer?.Reason}");
                Check(offer!.TargetIds!.Contains(mover.Id), "his own Personality at home is a target");
                Check(PassOffered(e, player), "and passing is still offered");
                Accept(e, offer.Command! with { TargetId = mover.Id });
                Eq(e.State.Cards[mover.Id].Zone, Zone.Battlefield); Eq(e.State.Cards[mover.Id].ProvinceIndex, (int?)0);
                Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was played");
            }
            Check(PassOffered(e, e.State.PriorityPlayerId), "both units are in; passing is offered");
        }
    }

    private static void HomeMoveNeedsPresence()
    {
        var s = Battle(attacker: 1, priority: 0);
        var mover = Add(s, "training-crane-samurai", 0, Zone.Home); var strategy = Add(s, HomeStrategy, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var offer = Offer(e, 0, HomeAbility, strategy.Id);
        Check(offer is null || !offer.Enabled, "a move HOME is not a move to the battle: without a unit at the battlefield it is not playable");
        if (offer is not null) Check((offer.Reason ?? "").Contains("unit"), "and the reason is the missing unit: " + offer.Reason);
        Reject(e, new("useAbility", 0, e.Version, CardId: strategy.Id, AbilityId: HomeAbility, TargetId: mover.Id));
        // With a unit at the battlefield the same action is legal, so it is the presence rule that stopped it above.
        var s2 = Battle(attacker: 1, priority: 0);
        var mover2 = Add(s2, "training-crane-samurai", 0, Zone.Home); var strategy2 = Add(s2, HomeStrategy, 0, Zone.Hand); Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        var offer2 = Offer(e2, 0, HomeAbility, strategy2.Id);
        Check(offer2 is { Enabled: true }, "with a unit there it is playable: " + offer2?.Reason);
        Accept(e2, offer2!.Command! with { TargetId = mover2.Id });
    }

    // ---- the real cards -------------------------------------------------------------------------------------------
    private static string DefinitionWith(string ability) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Abilities?.Contains(ability) == true).Select(c => c.Id).First();

    // The acting player has a Personality (and a Water Shugenja, for Sanctuary of the Waves) at home and NO unit at the battlefield, where the enemy has a unit.
    private static (MatchState S, CardInstance Card) RigFor(string ability, int actor, bool actorAttacks = false)
    {
        var attacker = actorAttacks ? actor : 1 - actor;
        var s = Battle(attacker, priority: actor);
        var definition = CardCatalog.Get(DefinitionWith(ability));
        Add(s, "training-lion-veteran", 1 - actor, Zone.Battlefield, province: 0);
        var water = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.Keywords?.Contains("Water") == true && c.Keywords.Contains("Shugenja"));
        var host = Add(s, water.Id, actor, Zone.Home);
        var maiden = Add(s, "training-crane-veteran", actor, Zone.Home); if (ability == "sound-of-thunder-move") maiden.GrantedKeywords.Add(new("Battle Maiden", "test-fixture", GrantDuration.Permanent));
        CardInstance card;
        if (definition.Type == CardType.Personality) card = Add(s, definition.Id, actor, Zone.Home, bowed: ability is "hawado-move-battlefield" or "houken-exp-straighten");
        else if (definition.Type == CardType.Spell) card = Add(s, definition.Id, actor, Zone.Attached, parent: host.Id);
        // A Kata's action is taken from play, where it sits at the right of home.
        else if (ability == "skipping-the-puddle-move") card = Add(s, definition.Id, actor, Zone.Home);
        else card = Add(s, definition.Id, actor, Zone.Hand);
        Add(s, "training-lion-samurai", actor, Zone.DynastyDiscard);
        // Dislodge the Foe (issue #264): the performing Tacticians stand at ANOTHER battlefield, with the Personality who opposes them there.
        if (ability == "dislodge-the-foe") { s.UnresolvedBattlefields = [0, 1]; Add(s, "training-tactician", actor, Zone.Battlefield, province: 1); Add(s, "training-lion-samurai", 1 - actor, Zone.Battlefield, province: 1); }
        return (s, card);
    }

    private static void RealCardsOfferedWithoutPresence()
    {
        var failures = new List<string>();
        // Requesting Reinforcements (Seeds of Decay pass, issue #453) is performed by "your performing Scout" AT the battlefield (bowed as its cost), so its acting player always
        // has a unit there: the no-presence position this test rigs cannot arise for it. It is still marked (its effect brings a Personality to the battle).
        foreach (var ability in AbilityCatalog.All.Where(p => p.Value.MovesToBattle && !p.Key.StartsWith("training-") && p.Key != "requesting-reinforcements-recruit").Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
        {
            var offeredTo = new List<int>();
            var reasons = new List<string>();
            foreach (var actor in new[] { 0, 1 })
            {
                var (s, card) = RigFor(ability, actor);
                var e = MatchEngine.FromTestState(s);
                Eq(s.Cards.Values.Count(c => c.OwnerId == actor && c.Zone == Zone.Battlefield && c.ProvinceIndex == 0), 0, "precondition: the actor has no unit at the battlefield");
                var offer = Offer(e, actor, ability, card.Id);
                if (offer is { Enabled: true }) offeredTo.Add(actor); else reasons.Add($"player {actor}: {(offer is null ? "not offered" : offer.Reason)}");
            }
            if (offeredTo.Count == 0) failures.Add($"{ability} ({string.Join("; ", reasons)})");
        }
        Check(failures.Count == 0, "not offered to a player with no unit at the battlefield: " + string.Join(" | ", failures));
    }

    private static void EarthFlowsDefenderOnly()
    {
        var earthFlows = DefinitionWith("earth-flows-move");
        foreach (var (actor, allowed) in new[] { (0, true), (1, true), (0, false), (1, false) })
        {
            var (s, card) = RigFor("earth-flows-move", actor, actorAttacks: !allowed);
            var mover = s.Cards.Values.First(c => c.OwnerId == actor && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality && c.Id != card.AttachedTo);
            Eq(card.DefinitionId, earthFlows);
            var e = MatchEngine.FromTestState(s);
            var offer = Offer(e, actor, "earth-flows-move", card.Id);
            Eq(offer is { Enabled: true }, allowed, $"player {actor} is the {(allowed ? "Defender" : "Attacker")}: " + offer?.Reason);
            if (allowed)
            {
                Check(offer!.TargetIds!.Contains(mover.Id), "any Personality is a target, home ones included");
                Accept(e, offer.Command! with { TargetId = mover.Id });
                Eq(e.State.Cards[mover.Id].Zone, Zone.Battlefield);
            }
            else Reject(e, new("useAbility", actor, e.Version, CardId: card.Id, AbilityId: "earth-flows-move", TargetId: mover.Id));
        }
    }
}
