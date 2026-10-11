using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #821: the six cards Mendon's rulings of 2026-10-10 unblocked (src/L5R.Core/Wave821.cs, ArmyRestrictions.cs, GiftOffer.cs), proved on the published cards themselves. Player 0 is the Crane, player 1 the Lion;
// every position is explicit, driven through QueryActions and the commands the engine offers, and a refused command must leave the state unchanged.
internal static class Wave821Tests
{
    private const string Kaiji = Wave821Abilities.Kaiji, Ryuzo = Wave821Abilities.Ryuzo, Kaito = Wave821Abilities.Kaito, Satoru = Wave821Abilities.Satoru, Tsumaro = Wave821Abilities.Tsumaro,
        Governor = Wave821Abilities.Governor, Illusion = Wave821Abilities.Illusion, Estate = Wave821Abilities.ImperialEstate;
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, int? province = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, province);
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
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static int Force(MatchEngine e, CardInstance card) => (int)Call(e, "PersonalityForce", e.State.Cards[card.Id])!;
    private static GameCommand Attack(MatchEngine e, params (CardInstance Unit, int Battlefield)[] units) =>
        new("attack", e.State.PriorityPlayerId, e.Version, Assignments: units.ToDictionary(u => u.Unit.Id, u => u.Battlefield));
    private static string RepoRoot() => Batch336Support.RepoRoot();

    public static void Run(TestRunner runner) => runner.Run("wave-821", [
        ("Published as printed: the five Personalities, The Governor's Estate and the created Imperial Estate, with stats, abilities, text and manifests", PublishedShape),
        ("Hida Kaiji: he is never assigned into an army that holds another Personality of his side, and no Personality is assigned into his; the opposing army is never considered", KaijiAssignment),
        ("Hida Kaiji: the offered assignment never names him with others, and offers him alone; the other Personalities are assigned at the other battlefields", KaijiOffers),
        ("Hida Kaiji: +2F while unopposed, +1F for each Personality opposing him with 6 or lower printed Force", KaijiForce),
        ("Isawa Ryuzo: Fire Open pairs two Personalities with more Madness tokens than printed Personal Honor, who then will not be assigned to the same army; opposed they may stand", RyuzoPair),
        ("Isawa Ryuzo: pairs stack, three on one Personality, and a pair does not touch the pairs the same cards have with others", RyuzoStacks),
    ]);

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        (string Id, string Wave, CardType Type, int Cost, int Force, int Chi, int Honor, int? Requirement, string[] Abilities, string Effect)[] cards = [
            (Kaiji, "personality-wave-821", CardType.Personality, 12, 7, 2, 0, null, [], Wave821Abilities.KaijiEffect),
            (Ryuzo, "personality-wave-821", CardType.Personality, 5, 3, 3, 0, null, [Wave821Abilities.RyuzoPair], "none"),
            (Kaito, "personality-wave-821", CardType.Personality, 2, 0, 2, 0, null, [], Wave821Abilities.KaitoEffect),
            (Satoru, "personality-wave-821", CardType.Personality, 7, 3, 3, 3, 7, [Wave821Abilities.SatoruGift], "none"),
            (Tsumaro, "personality-wave-821", CardType.Personality, 9, 2, 5, 4, 10, [Wave821Abilities.TsumaroIllusion], "none")];
        foreach (var (id, wave, type, cost, force, chi, honor, requirement, abilities, effect) in cards)
        {
            var def = CardCatalog.Get(id);
            Eq(def.Type, type, id); Eq(def.GoldCost, cost, id + " cost"); Eq(def.Force, force, id + " Force"); Eq(def.Chi, chi, id + " Chi"); Eq(def.PersonalHonor, honor, id + " Personal Honor");
            Eq(def.HonorRequirement, requirement, id + " Honor Requirement"); Eq(string.Join(",", def.Abilities ?? []), string.Join(",", abilities), id + " abilities"); Eq(def.Effect, effect, id + " Effect");
            Check(!def.CreatedOnly && !def.ScenarioOnly, id + " is a published, deck-legal card");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Contains(id), id + " is in the wave manifest");
        }
        Eq(CardCatalog.Get(Ryuzo).EntryHonorLoss, 2, "Ryuzo loses 2 Honor on entering play"); Eq(CardCatalog.Get(Kaito).EntryHonorLoss, 2, "Kaito loses 2 Honor on entering play");
        var governor = CardCatalog.Get(Governor);
        Eq(governor.Type, CardType.Stronghold); Eq(governor.ProvinceStrength, 7, "Strength"); Eq(governor.GoldProduction, 4, "production"); Eq(governor.StartingHonor, 8, "starting Honor");
        Eq(string.Join(",", governor.Abilities ?? []), string.Join(",", new[] { Wave821Abilities.GovernorCreate, Wave821Abilities.GovernorHonor, Wave821Abilities.GovernorUp, Wave821Abilities.GovernorDown }), "its four abilities");
        Eq(governor.Effect, Wave821Abilities.GovernorEffect);
        Check(governor.Text.Contains("Twice per turn, after your Dynasty Phase begins"), "the printed text is kept");
        var estate = CardCatalog.Get(Estate);
        Eq(estate.Type, CardType.Holding); Eq(estate.GoldCost, 0, "cost 0"); Eq(estate.GoldProduction, 2, "produces 2 Gold"); Check(estate.CreatedOnly, "created-only: never in a deck");
        Check(estate.Text.Contains("Bow this card: Produce 2 Gold."), "its printed trait");
        var creates = AbilityCatalog.All[Wave821Abilities.GovernorCreate];
        Eq(creates.Trigger, Wave821Abilities.DynastyBegins); Eq(creates.UsesPerTurn, 2, "twice per turn"); Check(creates.Creates is { EntersBowed: true }, "it creates the Estate bowed");
        Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/holding-wave-821.txt")).Contains(Estate), "the Estate is in its manifest");
        var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/holding-wave-821-sources.json"))).RootElement.EnumerateArray().Single();
        Eq(source.GetProperty("printing_set").EnumerateArray().Single().GetString(), "User-supplied scan", "the scan is the one Mendon supplied");
        CreatedCards.Resolve(creates.Creates!); // the creator's printed card resolves to exactly the Estate scan
    }

    // ---- Hida Kaiji -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Infantry Maneuvers of player 0: Hida Kaiji and two Crane Personalities at home, a Lion Personality at home for player 1.</summary>
    private static (MatchEngine Engine, CardInstance Kaiji, CardInstance A, CardInstance B) Maneuvers(Action<MatchState, CardInstance, CardInstance, CardInstance>? arrange = null)
    {
        var s = Batch336Support.Table(Phase.InfantryDeclaration, 0);
        var kaiji = Add(s, Kaiji, 0, Zone.Home); var a = Add(s, CraneSamurai, 0, Zone.Home); var b = Add(s, CraneSamurai, 0, Zone.Home);
        Add(s, LionSamurai, 1, Zone.Home);
        arrange?.Invoke(s, kaiji, a, b);
        return (MatchEngine.FromTestState(s), kaiji, a, b);
    }

    private static void KaijiAssignment()
    {
        var (e, kaiji, a, b) = Maneuvers();
        NoChange(e, Attack(e, (kaiji, 0), (a, 0)), "Kaiji with another Personality at one battlefield");
        NoChange(e, Attack(e, (a, 0), (kaiji, 0)), "the same, in the other order");
        // Another battlefield is another army.
        (e, kaiji, a, b) = Maneuvers();
        Accept(e, Attack(e, (kaiji, 0), (a, 1), (b, 1)));
        Eq(e.State.Cards[kaiji.Id].Zone, Zone.Battlefield, "Kaiji assigned"); Eq(e.State.Cards[a.Id].ProvinceIndex, 1, "the others went elsewhere");
        // Kaiji alone is fine, and so are the others without him.
        (e, kaiji, a, b) = Maneuvers();
        Accept(e, Attack(e, (kaiji, 0)));
        (e, kaiji, a, b) = Maneuvers();
        Accept(e, Attack(e, (a, 0), (b, 0)));
        // Already there: nobody is assigned into his army, and he is not assigned into theirs; being moved is another matter (Mendon: only assigning is barred).
        (e, kaiji, a, b) = Maneuvers((s, k, x, y) => { k.Zone = Zone.Battlefield; k.ProvinceIndex = 0; });
        NoChange(e, Attack(e, (a, 0)), "another Personality assigned into Kaiji's army");
        Accept(e, Attack(e, (a, 1)));
        (e, kaiji, a, b) = Maneuvers((s, k, x, y) => { x.Zone = Zone.Battlefield; x.ProvinceIndex = 0; });
        NoChange(e, Attack(e, (kaiji, 0)), "Kaiji assigned into an army that already holds a Personality");
        // The opposing army is not considered: Kaiji goes where enemy Personalities already stand.
        (e, kaiji, a, b) = Maneuvers((s, k, x, y) => { var enemy = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0); });
        Accept(e, Attack(e, (kaiji, 0)));
        // A Personality of the OTHER side at the battlefield does not stop another of his own side's going there.
        (e, kaiji, a, b) = Maneuvers((s, k, x, y) => { Add(s, LionSamurai, 1, Zone.Battlefield, province: 0); });
        Accept(e, Attack(e, (a, 0), (b, 0)));
    }

    private static void KaijiOffers()
    {
        var (e, kaiji, a, b) = Maneuvers();
        var attacks = e.QueryActions(0).Where(o => o.Type == "attack" && o.Enabled).ToArray();
        var everyone = attacks.Where(o => !o.Label.Contains("alone")).ToArray();
        Check(everyone.Length > 0 && everyone.All(o => o.TargetIds is { } ids && ids.Contains(a.Id) && ids.Contains(b.Id) && !ids.Contains(kaiji.Id)), "the 'assign everyone' command leaves Kaiji out");
        var alone = attacks.Where(o => o.Label.Contains("Hida Kaiji alone")).ToArray();
        Check(alone.Length > 0 && alone.All(o => o.TargetIds is { Length: 1 } ids && ids[0] == kaiji.Id), "Kaiji is offered on his own");
        foreach (var offered in everyone.Concat(alone))
            Check(offered.Command is not null && MatchEngine.FromTestState(e.State).Execute(offered.Command).Accepted, "an offered command is one the engine accepts: " + offered.Label);
        // With no restricted card in play nothing changes: every Personality is on the one command.
        var s = Batch336Support.Table(Phase.InfantryDeclaration, 0);
        var x = Add(s, CraneSamurai, 0, Zone.Home); var y = Add(s, CraneSamurai, 0, Zone.Home); Add(s, LionSamurai, 1, Zone.Home);
        var plain = MatchEngine.FromTestState(s);
        Check(plain.QueryActions(0).Where(o => o.Type == "attack").All(o => o.TargetIds is { } ids && ids.Contains(x.Id) && ids.Contains(y.Id)), "without Kaiji the offer is unchanged");
        Check(!plain.QueryActions(0).Any(o => o.Label.Contains("alone")), "and there is no 'alone' option");
    }

    private static void KaijiForce()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var kaiji = Add(s, Kaiji, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(Force(e, kaiji), 9, "unopposed: 7 + 2");
        s = Batch336Support.Table(Phase.Battle, 0);
        kaiji = Add(s, Kaiji, 0, Zone.Battlefield, province: 0);
        Fx(s, LionSamurai, "foe-6", 1, Zone.Battlefield, d => d with { Force = 6 }, province: 0);
        Fx(s, LionSamurai, "foe-3", 1, Zone.Battlefield, d => d with { Force = 3 }, province: 0);
        Fx(s, LionSamurai, "foe-7", 1, Zone.Battlefield, d => d with { Force = 7 }, province: 0);
        e = MatchEngine.FromTestState(s);
        Eq(Force(e, kaiji), 9, "opposed by 6F, 3F and 7F: +1 for each of the first two (printed Force 6 or lower), none for the 7F: 7 + 2");
        s = Batch336Support.Table(Phase.Battle, 0);
        kaiji = Add(s, Kaiji, 0, Zone.Battlefield, province: 0);
        Fx(s, LionSamurai, "big", 1, Zone.Battlefield, d => d with { Force = 8 }, province: 0);
        Eq(Force(MatchEngine.FromTestState(s), kaiji), 7, "opposed only by a Personality over 6 printed Force: nothing");
        // At home he is at no battlefield, so neither term applies.
        s = Batch336Support.Table(Phase.Action, 0);
        kaiji = Add(s, Kaiji, 0, Zone.Home);
        Eq(Force(MatchEngine.FromTestState(s), kaiji), 7, "at home: his printed Force");
        // Printed Force, not current: a modified enemy still counts by what it prints.
        s = Batch336Support.Table(Phase.Battle, 0);
        kaiji = Add(s, Kaiji, 0, Zone.Battlefield, province: 0);
        var buffed = Fx(s, LionSamurai, "buffed", 1, Zone.Battlefield, d => d with { Force = 4 }, province: 0);
        Batch336Support.Force(buffed, 10);
        Eq(Force(MatchEngine.FromTestState(s), kaiji), 8, "a 4F enemy given +10F still prints 4: +1");
    }

    // ---- Isawa Ryuzo ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Mad(MatchState s, string id, int owner, int tokens, int honor = 0, Zone zone = Zone.Home)
    {
        var card = Fx(s, CraneSamurai, id, owner, zone, d => d with { PersonalHonor = honor }, province: zone == Zone.Battlefield ? 0 : null);
        for (var i = 0; i < tokens; i++) card.Tokens.Add(new($"{id}-t{i}", "Madness", "test"));
        return card;
    }

    private static GameCommand PairCommand(MatchEngine e, CardInstance ryuzo, CardInstance x, CardInstance y) =>
        Batch336Support.Use(e, 0, Wave821Abilities.RyuzoPair, ryuzo.Id, selections: Batch336Support.Pick(Wave821Abilities.PairSlot, x.Id, y.Id));

    private static void RyuzoPair()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var ryuzo = Add(s, Ryuzo, 0, Zone.Home);
        var a = Mad(s, "mad-a", 0, 1); var b = Mad(s, "mad-b", 1, 1); var sane = Mad(s, "sane", 0, 0); var steady = Mad(s, "steady", 0, 2, honor: 2);
        var e = MatchEngine.FromTestState(s);
        NoChange(e, PairCommand(e, ryuzo, a, sane), "a Personality with no Madness token");
        NoChange(e, PairCommand(e, ryuzo, a, steady), "two Madness tokens against a printed Personal Honor of 2 is not more");
        NoChange(e, PairCommand(e, ryuzo, a, a), "the same Personality twice");
        Accept(e, PairCommand(e, ryuzo, a, b));
        Eq(e.State.Restrictions.Items.Count(r => r.Kind == RestrictionKind.NoSameArmy), 1, "one pair is stored");
        // Same army: refused. Opposed at one battlefield: allowed (they are on different sides). Different battlefields: allowed.
        var next = Batch336Support.Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; st.Cards[b.Id].OwnerId = 0; st.Cards[ryuzo.Id].Bowed = false; });
        NoChange(next, Attack(next, (a, 0), (b, 0)), "the paired Personalities in one army");
        Accept(next, Attack(next, (a, 0), (b, 1)));
        // On opposite sides at one battlefield nothing is refused: a defender and an attacker stand there.
        var opposed = Batch336Support.Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; st.Cards[b.Id].Zone = Zone.Battlefield; st.Cards[b.Id].ProvinceIndex = 0; });
        Accept(opposed, Attack(opposed, (a, 0)));
        Eq(opposed.State.Cards[a.Id].Zone, Zone.Battlefield, "the Personality opposed by his pair's partner was assigned");
        // The restriction ends with the turn.
        var later = e.State; later.TurnNumber++; later.Phase = Phase.InfantryDeclaration; later.ActivePlayerId = 0; later.PriorityPlayerId = 0; later.Cards[b.Id].OwnerId = 0;
        var after = MatchEngine.FromTestState(later);
        Call(after, "RestrictionsTurnBegin");
        Accept(after, Attack(after, (a, 0), (b, 0)));
    }

    private static void RyuzoStacks()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var r1 = Add(s, Ryuzo, 0, Zone.Home); var r2 = Add(s, Ryuzo, 0, Zone.Home); var r3 = Add(s, Ryuzo, 0, Zone.Home);
        var a = Mad(s, "a", 0, 1); var b = Mad(s, "b", 0, 1); var c = Mad(s, "c", 0, 1); var d = Mad(s, "d", 0, 1);
        var e = MatchEngine.FromTestState(s);
        // Ryuzo is not Unique: three copies in play, each used once, pair A with B, C and D in turn.
        Accept(e, PairCommand(e, r1, a, b));
        e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, PairCommand(e, r2, a, c));
        e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, PairCommand(e, r3, a, d));
        Eq(e.State.Restrictions.Items.Count(r => r.Kind == RestrictionKind.NoSameArmy), 3, "three stacked pairs");
        var attack = Batch336Support.Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; });
        CardInstance Of(MatchEngine m, CardInstance x) => m.State.Cards[x.Id];
        NoChange(attack, Attack(attack, (Of(attack, a), 0), (Of(attack, b), 0)), "A with B");
        NoChange(attack, Attack(attack, (Of(attack, a), 0), (Of(attack, c), 0)), "A with C");
        NoChange(attack, Attack(attack, (Of(attack, a), 0), (Of(attack, d), 0)), "A with D");
        // The pairs are only those named: B, C and D may stand together, and A with the Ryuzo themselves.
        Accept(attack, Attack(attack, (Of(attack, b), 0), (Of(attack, c), 0), (Of(attack, d), 0)));
        var other = Batch336Support.Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; });
        Accept(other, Attack(other, (Of(other, a), 0), (Of(other, r1), 0)));
        // The offered "assign everyone" command stays one the engine accepts, A being left out of it when it cannot stand with the rest.
        var offers = Batch336Support.Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; });
        foreach (var offered in offers.QueryActions(0).Where(o => o.Type == "attack" && o.Enabled))
            Check(offered.Command is not null && MatchEngine.FromTestState(offers.State).Execute(offered.Command).Accepted, "an offered command is accepted: " + offered.Label);
    }
}
