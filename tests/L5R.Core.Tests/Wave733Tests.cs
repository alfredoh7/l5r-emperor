using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #733, wave batch 2 (src/L5R.Core/Wave733.cs; waves personality-wave-151, follower-wave-63, strategy-wave-135, stronghold-wave-47): the recruit-gate, Proclaim and Honor cards, proved on the
// published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the Crane, player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave733Tests
{
    private const string Hamada = "oracle-l5r-10308", Shindo = "oracle-l5r-10450", Kimura = "oracle-l5r-10451", Natsu = "oracle-l5r-10460", Otani = "oracle-l5r-10494", Hayamizu = "oracle-l5r-10503",
        Asp = "oracle-l5r-10504", Yashinko = "oracle-l5r-10860", Scribe = "oracle-l5r-10894", Gamble = "oracle-l5r-10545", Invocation = "oracle-l5r-10547", FalsePath = "oracle-l5r-10561",
        Zansho = "oracle-l5r-10505", Patronage = "oracle-l5r-10720", Blessing = "oracle-l5r-10558", Marriage = "oracle-l5r-10821";
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Keep = "training-border-keep", Marketplace = "training-marketplace", Challenge = "training-duel-challenge",
        FalseRingOfAir = "oracle-l5r-10531", FalseRingOfFire = "oracle-l5r-10533", RingOfAir = "oracle-l5r-6333";
    private static readonly string[] Wired = [Hamada, Shindo, Kimura, Natsu, Otani, Hayamizu, Asp, Yashinko, Scribe, Gamble, Invocation, FalsePath];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, null, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static CardInstance Stronghold(MatchState s, int player) => s.Cards.Values.First(c => c.OwnerId == player && c.Zone == Zone.Stronghold);

    public static void Run(TestRunner runner) => runner.Run("wave-733", [
        ("Published as printed: twelve cards in four profiles with their stats, abilities and effects; the four deferred cards are not published", PublishedShape),
        ("The gates: Asp, Shindo, Natsu, Otani and Hayamizu refuse what they print; Hamada is Loyal to Scorpion but Crab may control him; Crane may Proclaim Yashinko", Gates),
        ("Bayushi Hamada and Tsi Hayamizu: +1F for each Crab Clan Personality or Courtier of yours, and for each Mine you control", HamadaAndHayamizuForce),
        ("Ikoma Natsu: Open gives a target Personality Cavalry for the turn", NatsuCavalry),
        ("Suzume Shindo: the challenger has the first opportunity to focus only in duels he issues", ShindoFirstFocus),
        ("Ichiro Otani: Battle duel of Force without Item bonuses, and the loser is bowed", OtaniDuel),
        ("Kimura: free with the Ring of Air; Air Limited discards a card and the next Proclaim this turn gains at least its Focus Value", KimuraProclaim),
        ("Yoritomo Yashinko: bows for 3 Gold in a payment, and only unbowed", YashinkoGold),
        ("Court Scribe: the next gain or loss from your cards is one larger, once, this turn, never a Proclaim or the other player's cards", CourtScribe),
        ("Invocation: until your next turn begins every Honor gain is 1 smaller unless the player controls a Spirit", InvocationReduces),
        ("Delicate Gamble: bows your Courtier, discards the top card of the opponent's Fate deck and the chosen player gains or loses its Focus Value or 3; a loser ignores Honor Requirements", DelicateGamble),
        ("The False Path: Monk and Fudo, Fudo's price and Honor Requirement, no Honor loss from your own Fudo cards, no Enlightenment, and the Fudo Ring search", FalsePathCard),
    ]);

    // ---- rigs ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The first province card of player 0 becomes the named card, face up, in the Dynasty phase; player 0 is the Crane with 40 Honor unless the setup says otherwise.
    private static (MatchState State, CardInstance Card) Recruit(string definition, Action<MatchState>? setup = null)
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        s.Players[0].Honor = 40;
        var card = s.Cards[s.Players[0].Provinces[0].CardId!];
        card.DefinitionId = definition; card.FaceUp = true;
        setup?.Invoke(s);
        return (s, card);
    }
    private static string? Error(MatchState s, CardInstance card, bool viaReserve = false) => MatchEngine.FromTestState(s).QuotePurchase(0, card.Id, viaReserve: viaReserve).Error;

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect)>
        {
            [Hamada] = (CardType.Personality, [], "crab-players-may-control"),
            [Shindo] = (CardType.Personality, [], "will-not-join-spider-player"),
            [Kimura] = (CardType.Personality, ["w733-kimura-proclaim"], "none"),
            [Natsu] = (CardType.Personality, ["w733-natsu-cavalry"], "will-not-join-unicorn-player"),
            [Otani] = (CardType.Personality, ["w733-otani-duel"], "will-not-enter-honor-nonnegative"),
            [Hayamizu] = (CardType.Personality, [], "will-not-join-scorpion-player"),
            [Asp] = (CardType.Personality, [], "will-not-join-human-controller"),
            [Yashinko] = (CardType.Personality, [], "crane-players-may-proclaim"),
            [Scribe] = (CardType.Follower, ["w733-scribe-honor"], "none"),
            [Gamble] = (CardType.Strategy, ["w733-gamble"], "none"),
            [Invocation] = (CardType.Strategy, ["w733-invocation"], "none"),
            [FalsePath] = (CardType.Stronghold, ["w733-falsepath-search"], "w733-false-path")
        };
        Eq(expected.Count, 12, "twelve cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect, $"{d.Name} is published as wired");
            Check(!string.IsNullOrWhiteSpace(d.Text) && !d.CreatedOnly, $"{d.Name} prints its text and is playable");
        }
        var path = CardCatalog.Get(FalsePath);
        Check(path.GoldProduction == 3 && path.ProvinceStrength == 9 && path.StartingHonor == 0, "The False Path: production 3, strength 9, starting Honor 0");
        var hamada = CardCatalog.Get(Hamada);
        Check(hamada.Force == 5 && hamada.Chi == 3 && hamada.PersonalHonor == 2 && hamada.GoldCost == 8 && hamada.HonorRequirement is null, "Hamada's stats");
        foreach (var id in new[] { Zansho, Patronage, Blessing }) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is deferred, not published"); // Marriage was wired afterwards (Wave742.cs, issue #765)
        Eq(AbilityCatalog.All.Keys.Count(k => k.StartsWith("w733-", StringComparison.Ordinal)), 7, "seven w733 abilities");
        Check(!TraitReactions.Is("w733-kimura-proclaim"), "Kimura's action is not a trait");
    }

    private static void Gates()
    {
        // Asp Skirmisher: refused while the player controls a Human Personality (one without Nonhuman).
        var (s, asp) = Recruit(Asp);
        Eq(Error(s, asp), null, "no Human Personality: Asp joins");
        Fx(s, CraneSamurai, "w733-human", 0, Zone.Home);
        Check(Error(s, asp) is not null, "a Human Personality of the player's: Asp will not join");
        var (n, nasp) = Recruit(Asp);
        Fx(n, CraneSamurai, "w733-nonhuman", 0, Zone.Home, d => d with { Keywords = ["Samurai", "Nonhuman"] });
        Eq(Error(n, nasp), null, "a Nonhuman Personality is not Human");
        // The Clan refusals, and the Honor refusal.
        foreach (var (card, clan) in new[] { (Shindo, "Spider"), (Natsu, "Unicorn"), (Hayamizu, "Scorpion") })
        {
            var (a, ac) = Recruit(card, st => st.Players[0].Clan = clan);
            Check(Error(a, ac) is not null, $"{CardCatalog.Get(card).Name} will not join a {clan} Clan player");
            var (b, bc) = Recruit(card);
            Eq(Error(b, bc), null, $"{CardCatalog.Get(card).Name} joins a Crane player");
        }
        foreach (var (honor, allowed) in new[] { (5, false), (0, false), (-1, true) })
        {
            var (a, ac) = Recruit(Otani, st => st.Players[0].Honor = honor);
            Check(allowed == (Error(a, ac) is null), $"Otani at Honor {honor}: {(allowed ? "joins" : "will not enter play")}");
        }
        // Hamada is Loyal to the Scorpion: a Crane player is refused, a Crab player and a Scorpion player are not.
        foreach (var (clan, allowed) in new[] { ("Crane", false), ("Lion", false), ("Crab", true), ("Scorpion", true) })
        {
            var (a, ac) = Recruit(Hamada, st => st.Players[0].Clan = clan);
            Check(allowed == (Error(a, ac) is null), $"Hamada for a {clan} player: {(allowed ? "may control" : "refused")}");
        }
        // Yoritomo Yashinko: a Crane player may Proclaim her, a Lion player may not.
        var (y, yc) = Recruit(Yashinko);
        Check(MatchEngine.FromTestState(y).QuotePurchase(0, yc.Id) is { Error: null, ProclaimEligible: true }, "a Crane player may Proclaim Yashinko");
        var (l, lc) = Recruit(Yashinko, st => st.Players[0].Clan = "Lion");
        Check(!MatchEngine.FromTestState(l).QuotePurchase(0, lc.Id).ProclaimEligible, "a Lion player may not");
    }

    private static void HamadaAndHayamizuForce()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var hamada = Add(s, Hamada, 0, Zone.Home);
        var hayamizu = Add(s, Hayamizu, 0, Zone.Home);
        var baseline = (hamada: (int)Call(MatchEngine.FromTestState(s), "CardForce", hamada)!, hayamizu: (int)Call(MatchEngine.FromTestState(s), "CardForce", hayamizu)!);
        Eq(baseline.hamada, 5, "Hamada's printed Force with nothing to count");
        Eq(baseline.hayamizu, 2, "Hayamizu's printed Force with no Mine");
        Fx(s, CraneSamurai, "w733-crab", 0, Zone.Home, d => d with { Clan = "Crab", Keywords = ["Samurai", "Crab Clan"] });
        Fx(s, CraneSamurai, "w733-courtier", 0, Zone.Home, d => d with { Keywords = ["Courtier"] });
        Fx(s, CraneSamurai, "w733-both", 0, Zone.Home, d => d with { Clan = "Crab", Keywords = ["Courtier", "Crab Clan"] });
        Fx(s, CraneSamurai, "w733-other", 0, Zone.Home, d => d with { Keywords = ["Samurai"] });
        Fx(s, CraneSamurai, "w733-enemy-crab", 1, Zone.Home, d => d with { Clan = "Crab", Keywords = ["Samurai", "Crab Clan"] });
        Fx(s, Keep, "w733-mine-1", 0, Zone.Home, d => d with { Keywords = ["Mine"] });
        Fx(s, Keep, "w733-mine-2", 0, Zone.Home, d => d with { Keywords = ["Mine"] });
        Fx(s, Keep, "w733-enemy-mine", 1, Zone.Home, d => d with { Keywords = ["Mine"] });
        Fx(s, Keep, "w733-farm", 0, Zone.Home, d => d with { Keywords = ["Farm"] });
        var e = MatchEngine.FromTestState(s);
        Eq((int)Call(e, "CardForce", e.State.Cards[hamada.Id])!, 5 + 3, "+1F for the Crab Personality, the Courtier and the one that is both (once), not the others, not the enemy's");
        Eq((int)Call(e, "CardForce", e.State.Cards[hayamizu.Id])!, 2 + 2, "+1F for each of his two Mines, not the enemy's or a Farm");
        // Out of play they show their printed Force.
        var (r, rc) = Recruit(Hamada);
        Fx(r, CraneSamurai, "w733-crab-2", 0, Zone.Home, d => d with { Clan = "Crab", Keywords = ["Samurai", "Crab Clan"] });
        Eq((int)Call(MatchEngine.FromTestState(r), "CardForce", rc)!, 5, "in a province he has only his printed Force");
    }

    private static void NatsuCavalry()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var natsu = Add(s, Natsu, 0, Zone.Home);
        var other = Fx(s, LionSamurai, "w733-target", 1, Zone.Home);
        var own = Fx(s, CraneSamurai, "w733-own", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!e.HasKeyword(other.Id, "Cavalry") && !e.HasKeyword(own.Id, "Cavalry"), "neither is Cavalry to begin with");
        Accept(e, Batch336Support.Offer(e, 0, Wave733Abilities.NatsuCavalry, natsu.Id, other.Id));
        Check(e.HasKeyword(other.Id, "Cavalry"), "the target Personality is Cavalry");
        Check(!e.HasKeyword(own.Id, "Cavalry"), "only the target");
        Check(!e.State.Cards[natsu.Id].Bowed, "no bow: Open only");
    }

    private static (MatchEngine E, CardInstance Challenger, CardInstance Challenged, GameCommand Command) DuelTable(string challengerId)
    {
        var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var challenger = Add(s, challengerId, 0, Zone.Battlefield, province: 0);
        var challenged = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, Challenge, 0, Zone.Hand);
        foreach (var player in new[] { 0, 1 }) for (var i = 0; i < 3; i++) Add(s, "training-item", player, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return (e, challenger, challenged, new GameCommand("useAbility", 0, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: challenged.Id, AbilityId: Challenge));
    }

    private static void Begin(MatchEngine e, GameCommand challengeCommand)
    {
        Accept(e, challengeCommand);
        if (e.State.PendingDuel is { Stage: "AwaitingAccept" }) Accept(e, new("acceptDuel", 1, e.Version));
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingDuel?.Stage, "Focusing", "the duel is focusing");
    }

    private static void ShindoFirstFocus()
    {
        var (e, _, _, command) = DuelTable(Shindo);
        Begin(e, command);
        Eq(e.State.PendingDuel!.TurnPlayerId, 0, "Shindo's controller, the challenger, focuses or strikes first");
        Check(e.State.PendingDuel.Rules is { FirstToFocusPlayerId: 0 }, "the rule is the duel's");
        // A duel Shindo is not the challenger of is unchanged: the challenged player acts first.
        var (f, _, _, command2) = DuelTable(CraneSamurai);
        Begin(f, command2);
        Eq(f.State.PendingDuel!.TurnPlayerId, 1, "an ordinary duel starts with the challenged player");
        // Shindo as the challenged Personality gives the challenger no such right to his opponent's side: his own side's challenger is not Shindo.
        var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var challenger = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0); var shindo = Add(s, Shindo, 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, Challenge, 0, Zone.Hand);
        foreach (var player in new[] { 0, 1 }) for (var i = 0; i < 3; i++) Add(s, "training-item", player, Zone.Hand);
        var g = MatchEngine.FromTestState(s);
        Begin(g, new GameCommand("useAbility", 0, g.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: shindo.Id, AbilityId: Challenge));
        Eq(g.State.PendingDuel!.TurnPlayerId, 1, "Shindo challenged: the usual order");
    }

    private static void OtaniDuel()
    {
        var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var otani = Add(s, Otani, 0, Zone.Battlefield, province: 0);
        var enemy = Fx(s, LionSamurai, "w733-enemy", 1, Zone.Battlefield, d => d with { Force = 2, Chi = 2 }); enemy.ProvinceIndex = 0;
        Fx(s, "training-item", "w733-axe", 1, Zone.Attached, d => d with { Force = 9 }, parent: enemy.Id);
        for (var player = 0; player < 2; player++) for (var i = 0; i < 3; i++) Add(s, "training-item", player, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var offer = Batch336Support.Offer(e, 0, Wave733Abilities.OtaniDuel, otani.Id, enemy.Id);
        Accept(e, offer);
        if (e.State.PendingDuel is { Stage: "AwaitingAccept" }) Accept(e, new("acceptDuel", 1, e.Version));
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingDuel?.Stage, "Focusing", "the duel is focusing");
        Check(e.State.PendingDuel!.Rules is { ExcludeItemBonuses: true }, "Items give no bonuses in this duel");
        Eq(e.State.PendingDuel.Stat, "Force", "a duel of Force");
        // The enemy's Force 9 Item adds nothing: Otani (6) beats the 2F enemy and the loser is bowed.
        Accept(e, new("strike", e.State.PendingDuel.TurnPlayerId, e.Version));
        for (var guard = 0; e.State.PendingDuel is { } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "DuelistRefocus" ? new("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new("strike", duel.TurnPlayerId, e.Version));
        Eq(e.State.LastDuel!.Result!.WinnerId, otani.Id, "Otani wins: the Item's +9 is left out");
        Check(e.State.Cards[enemy.Id].Bowed && !e.State.Cards[otani.Id].Bowed, "the loser is bowed, the winner is not");
    }

    private static void KimuraProclaim()
    {
        // Free with the Ring of Air, her printed 7 without it.
        foreach (var ring in new[] { true, false })
        {
            var (s, card) = Recruit(Kimura, st => { if (ring) Add(st, RingOfAir, 0, Zone.Home); });
            Eq(MatchEngine.FromTestState(s).QuotePurchase(0, card.Id).Cost, ring ? 0 : 7, ring ? "ignoring Gold Cost with the Ring of Air" : "the printed Gold Cost");
        }
        // Air Limited: discard a card; the next Proclaim this turn gains at least its Focus Value.
        foreach (var (focus, expected) in new[] { (5, 5), (1, 3) })
        {
            var s = Batch336Support.Table(Phase.Action, 0);
            var kimura = Add(s, Kimura, 0, Zone.Home);
            var discard = Fx(s, "training-item", "w733-discard", 0, Zone.Hand, d => d with { Focus = focus });
            var e = MatchEngine.FromTestState(s);
            Accept(e, Batch336Support.Use(e, 0, Wave733Abilities.KimuraProclaim, kimura.Id, null, null, Batch336Support.Pick("discard", discard.Id)));
            Check(e.State.Cards[discard.Id].Zone == Zone.FateDiscard && !e.State.Players[0].Hand.Contains(discard.Id), "the card was discarded as the cost");
            var honor = Honor(e, 0);
            e.GainHonorForTest(0, 2, "Other gain", null);
            Eq(Honor(e, 0), honor + 2, "an ordinary gain is untouched and does not use the boost");
            e.GainHonorForTest(0, 3, "Proclaim", null, proclaim: true);
            Eq(Honor(e, 0), honor + 2 + expected, focus > 3 ? "the Proclaim gains the Focus Value (5)" : "a Focus Value below the usual gain does not lower it");
            e.GainHonorForTest(0, 3, "Proclaim", null, proclaim: true);
            Eq(Honor(e, 0), honor + 2 + expected + 3, "only the next Proclaim");
        }
        // Once the turn has moved on the boost is gone, and it never serializes unless stored.
        var t = Batch336Support.Table(Phase.Action, 0);
        var k = Add(t, Kimura, 0, Zone.Home); var d2 = Fx(t, "training-item", "w733-discard-2", 0, Zone.Hand, d => d with { Focus = 5 });
        var run = MatchEngine.FromTestState(t);
        Check(!JsonSerializer.Serialize(run.State).Contains("Wave733HonorBoosts", StringComparison.Ordinal), "nothing is saved until a boost is stored");
        Accept(run, Batch336Support.Use(run, 0, Wave733Abilities.KimuraProclaim, k.Id, null, null, Batch336Support.Pick("discard", d2.Id)));
        var next = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(run.State))!);
        var next2 = Batch336Support.Edit(next, st => st.TurnNumber += 2);
        var before = Honor(next2, 0);
        next2.GainHonorForTest(0, 3, "Proclaim", null, proclaim: true);
        Eq(Honor(next2, 0), before + 3, "a later turn's Proclaim is not boosted");
    }

    private static void YashinkoGold()
    {
        var (s, province) = Recruit(CraneSamurai, st => { });
        province.Printed = CardCatalog.Get(CraneSamurai) with { Id = "w733-subject", Keywords = ["Samurai"], GoldCost = 3, HonorRequirement = null };
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var yashinko = Add(s, Yashinko, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var token = yashinko.Id + "#yashinko";
        var source = e.QuotePurchase(0, province.Id).Sources!.Single(x => x.Id == token);
        Check(source.Kind == "bow-reaction" && source.Produced == 3, "Yashinko is a source producing 3");
        Accept(e, new("purchase", 0, e.Version, CardId: province.Id, SourceIds: [token]));
        Check(e.State.Cards[province.Id].Zone == Zone.Home && e.State.Cards[yashinko.Id].Bowed, "she paid 3 and bowed");
        // Bowed, she offers nothing.
        var (b, bprovince) = Recruit(CraneSamurai);
        bprovince.Printed = CardCatalog.Get(CraneSamurai) with { Id = "w733-subject", Keywords = ["Samurai"], GoldCost = 3, HonorRequirement = null };
        foreach (var card in b.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var bowed = Add(b, Yashinko, 0, Zone.Home, bowed: true);
        Check(!MatchEngine.FromTestState(b).QuotePurchase(0, bprovince.Id).Sources!.Any(x => x.Id == bowed.Id + "#yashinko"), "a bowed Yashinko is not a source");
    }

    private static void CourtScribe()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var scribe = Add(s, Scribe, 0, Zone.Attached, parent: host.Id);
        var mine = Fx(s, "training-item", "w733-mine-card", 0, Zone.Home);
        var theirs = Fx(s, "training-item", "w733-their-card", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Use(e, 0, Wave733Abilities.ScribeHonor, scribe.Id));
        Check(e.State.Cards[scribe.Id].Bowed, "bowed as the cost");
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        e.GainHonorForTest(0, 2, "Proclaim", mine.Id, proclaim: true);
        Eq(Honor(e, 0), h0 + 2, "a Proclaiming gain is not from your cards");
        e.GainHonorForTest(0, 2, "The other player's card", theirs.Id);
        Eq(Honor(e, 0), h0 + 4, "a gain from the other player's card is not boosted");
        e.GainHonorForTest(0, 2, "Rulebook", null);
        Eq(Honor(e, 0), h0 + 6, "a change with no card is not from your cards");
        e.GainHonorForTest(1, -2, "Your card makes him lose", mine.Id);
        Eq(Honor(e, 1), h1 - 3, "the next change from your cards, even a loss of the other player's Honor, is one larger");
        e.GainHonorForTest(0, 2, "Your card again", mine.Id);
        Eq(Honor(e, 0), h0 + 8, "only the next one");
        // A gain from your own card is boosted too, and a boost does not outlive the turn.
        var s2 = Batch336Support.Table(Phase.Action, 0);
        var host2 = Add(s2, CraneSamurai, 0, Zone.Home); var scribe2 = Add(s2, Scribe, 0, Zone.Attached, parent: host2.Id); var card2 = Fx(s2, "training-item", "w733-own", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Batch336Support.Use(e2, 0, Wave733Abilities.ScribeHonor, scribe2.Id));
        var before = Honor(e2, 0);
        e2.GainHonorForTest(0, 2, "Own card", card2.Id);
        Eq(Honor(e2, 0), before + 3, "a gain from your own card is one larger");
        var s3 = Batch336Support.Table(Phase.Action, 0);
        var host3 = Add(s3, CraneSamurai, 0, Zone.Home); var scribe3 = Add(s3, Scribe, 0, Zone.Attached, parent: host3.Id); var card3 = Fx(s3, "training-item", "w733-own", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Batch336Support.Use(e3, 0, Wave733Abilities.ScribeHonor, scribe3.Id));
        var later = Batch336Support.Edit(e3, st => st.TurnNumber += 2);
        var b3 = Honor(later, 0);
        later.GainHonorForTest(0, 2, "Own card", card3.Id);
        Eq(Honor(later, 0), b3 + 2, "a later turn's gain is not boosted");
    }

    private static void InvocationReduces()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var inv = Add(s, Invocation, 0, Zone.Hand);
        Fx(s, CraneSamurai, "w733-spirit", 1, Zone.Home, d => d with { Keywords = ["Samurai", "Spirit"] });
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Use(e, 0, Wave733Abilities.Invocation, inv.Id));
        Check(e.State.Cards[inv.Id].Zone == Zone.FateDiscard, "the Strategy was spent");
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        e.GainHonorForTest(0, 3, "Gain", null);
        Eq(Honor(e, 0), h0 + 2, "a player with no Spirit gains 1 less");
        e.GainHonorForTest(1, 3, "Gain", null);
        Eq(Honor(e, 1), h1 + 3, "a player who controls a Spirit gains in full");
        e.GainHonorForTest(0, -3, "Loss", null);
        Eq(Honor(e, 0), h0 + 2 - 3, "losses are untouched");
        // It lasts through the other player's turn and ends when the turn after that begins.
        var through = Batch336Support.Edit(e, st => st.TurnNumber += 1);
        var t0 = Honor(through, 0);
        through.GainHonorForTest(0, 3, "Gain", null);
        Eq(Honor(through, 0), t0 + 2, "still in force on the opponent's turn");
        var ended = Batch336Support.Edit(e, st => st.TurnNumber += 2);
        var u0 = Honor(ended, 0);
        ended.GainHonorForTest(0, 3, "Gain", null);
        Eq(Honor(ended, 0), u0 + 3, "over when his own next turn begins");
    }

    private static void DelicateGamble()
    {
        foreach (var (focus, amount) in new[] { (2, 2), (5, 3) })
        {
            var s = Batch336Support.Table(Phase.Action, 0);
            var courtier = Fx(s, CraneSamurai, "w733-courtier", 0, Zone.Home, d => d with { Keywords = ["Courtier"] });
            var gamble = Add(s, Gamble, 0, Zone.Hand);
            var top = Fx(s, "training-item", "w733-top", 1, Zone.FateDeck, d => d with { Focus = focus });
            s.Players[1].FateDeck.Insert(0, top.Id);
            var e = MatchEngine.FromTestState(s);
            var h0 = Honor(e, 0); var h1 = Honor(e, 1);
            Accept(e, Batch336Support.Offer(e, 0, Wave733Abilities.Gamble, gamble.Id, courtier.Id));
            Check(e.State.Cards[courtier.Id].Bowed, "the Courtier was bowed as the cost");
            Eq(e.State.Cards[top.Id].Zone, Zone.FateDiscard, "the top card of the other player's Fate deck was discarded");
            Eq(e.State.PendingChoice?.Type, "effectChoice", "the acting player chooses who and which way");
            var offered = e.QueryActions(0).Where(a => a.Enabled && a.Type == "chooseEffect").ToArray();
            Eq(offered.Length, 4, "four options: each player, gain or lose");
            Check(offered.All(a => a.Label.Contains(amount.ToString(), StringComparison.Ordinal)), $"each names {amount} Honor");
            Accept(e, offered.Single(a => a.Command!.Choice == "opponent-lose").Command!);
            Eq(Honor(e, 1), h1 - amount, $"the opponent lost the Focus Value or 3 ({amount})");
            Check(e.State.Players[1].IgnoresHonorRequirements, "he who lost Honor ignores Honor Requirements this game");
            Eq(Honor(e, 0), h0, "the chooser's Honor is unchanged");
        }
        // Gaining does not grant that.
        var t = Batch336Support.Table(Phase.Action, 0);
        var c2 = Fx(t, CraneSamurai, "w733-courtier", 0, Zone.Home, d => d with { Keywords = ["Courtier"] });
        var g2 = Add(t, Gamble, 0, Zone.Hand);
        var top2 = Fx(t, "training-item", "w733-top", 1, Zone.FateDeck, d => d with { Focus = 1 });
        t.Players[1].FateDeck.Insert(0, top2.Id);
        var f = MatchEngine.FromTestState(t);
        var s0 = Honor(f, 0);
        Accept(f, Batch336Support.Offer(f, 0, Wave733Abilities.Gamble, g2.Id, c2.Id));
        Accept(f, f.QueryActions(0).Single(a => a.Enabled && a.Type == "chooseEffect" && a.Command!.Choice == "self-gain").Command!);
        Eq(Honor(f, 0), s0 + 1, "he gained his own Honor");
        Check(!f.State.Players[0].IgnoresHonorRequirements, "a gain grants no waiver");
        // Only your own unbowed Courtier is a target.
        var u = Batch336Support.Table(Phase.Action, 0);
        var bowed = Fx(u, CraneSamurai, "w733-bowed", 0, Zone.Home, d => d with { Keywords = ["Courtier"] }, bowed: true);
        var enemy = Fx(u, LionSamurai, "w733-enemy-courtier", 1, Zone.Home, d => d with { Keywords = ["Courtier"] });
        var plain = Fx(u, CraneSamurai, "w733-plain", 0, Zone.Home);
        var g3 = Add(u, Gamble, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(u);
        foreach (var bad in new[] { bowed, enemy, plain })
            Check(!Batch336Support.Options(g, 0, Wave733Abilities.Gamble, g3.Id).Any(a => a.Enabled && (a.TargetIds?.Contains(bad.Id) ?? false)), $"{bad.Id} is not a legal target");
    }

    private static void FalsePathCard()
    {
        // Keyword rules: a non-Unique Monk gains Fudo and a non-Unique Fudo Personality gains Monk, in and out of play; a Unique one does not; without the Stronghold nothing changes.
        var (s, province) = Recruit(CraneSamurai, st => Stronghold(st, 0).DefinitionId = FalsePath);
        province.Printed = CardCatalog.Get(CraneSamurai) with { Id = "w733-monk", Keywords = ["Monk"], GoldCost = 5, HonorRequirement = 30 };
        var unique = Fx(s, CraneSamurai, "w733-unique-monk", 0, Zone.Home, d => d with { Keywords = ["Monk", "Unique"] });
        var fudo = Fx(s, CraneSamurai, "w733-fudo", 0, Zone.Home, d => d with { Keywords = ["Fudo"] });
        var enemyMonk = Fx(s, LionSamurai, "w733-enemy-monk", 1, Zone.Home, d => d with { Keywords = ["Monk"] });
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(province.Id, "Fudo") && e.HasKeyword(province.Id, "Monk"), "a Monk in a province has Monk and Fudo");
        Check(e.HasKeyword(fudo.Id, "Monk") && e.HasKeyword(fudo.Id, "Fudo"), "a Fudo Personality in play has Monk and Fudo");
        Check(!e.HasKeyword(unique.Id, "Fudo"), "a Unique Monk does not");
        Check(!e.HasKeyword(enemyMonk.Id, "Fudo"), "the other player's Monk does not");
        // The price: Fudo Personalities join for 2 less, and the Honor Requirement (30 against 40 Honor... set to 0) is ignored.
        Eq(e.QuotePurchase(0, province.Id).Cost, 3, "5 less 2 for a Fudo Personality");
        var low = Batch336Support.Edit(e, st => st.Players[0].Honor = 0);
        Check(low.QuotePurchase(0, province.Id).Error is null, "the Honor Requirement of a Fudo card is ignored");
        var (plain, pprovince) = Recruit(CraneSamurai);
        pprovince.Printed = CardCatalog.Get(CraneSamurai) with { Id = "w733-monk", Keywords = ["Monk"], GoldCost = 5, HonorRequirement = 30 };
        plain.Players[0].Honor = 0;
        var without = MatchEngine.FromTestState(plain);
        Check(!without.HasKeyword(pprovince.Id, "Fudo"), "without the Stronghold no keyword is given");
        Check(without.QuotePurchase(0, pprovince.Id).Error is not null, "and the Honor Requirement stands");
        Eq(without.QuotePurchase(0, pprovince.Id).Cost, 5, "and the price is the printed one");
        // No Honor lost from a Fudo card you own; a non-Fudo card, or another player's Fudo card, still costs Honor.
        var h = Batch336Support.Table(Phase.Action, 0);
        Stronghold(h, 0).DefinitionId = FalsePath;
        var ownFudo = Fx(h, CraneSamurai, "w733-own-fudo", 0, Zone.Home, d => d with { Keywords = ["Fudo"] });
        var ownPlain = Fx(h, CraneSamurai, "w733-own-plain", 0, Zone.Home);
        var theirFudo = Fx(h, LionSamurai, "w733-their-fudo", 1, Zone.Home, d => d with { Keywords = ["Fudo"] });
        var he = MatchEngine.FromTestState(h);
        var before = Honor(he, 0);
        he.GainHonorForTest(0, -3, "Own Fudo", ownFudo.Id);
        Eq(Honor(he, 0), before, "no Honor lost from a Fudo card you own");
        he.GainHonorForTest(0, -3, "Own plain", ownPlain.Id);
        Eq(Honor(he, 0), before - 3, "a non-Fudo card still costs Honor");
        he.GainHonorForTest(0, -2, "Their Fudo", theirFudo.Id);
        Eq(Honor(he, 0), before - 5, "another player's Fudo card still costs Honor");
        he.GainHonorForTest(0, 2, "Own Fudo gain", ownFudo.Id);
        Eq(Honor(he, 0), before - 3, "gains are untouched");
        // You may not win by Enlightenment.
        Check(he.EnlightenmentBarred(0) && !he.EnlightenmentBarred(1), "Enlightenment is barred for its controller only");
        Check(!MatchEngine.FromTestState(Batch336Support.Table(Phase.Action, 0)).EnlightenmentBarred(0), "and not without the Stronghold");
        // Limited, bow: search the discard pile, then the deck, for a Fudo Ring.
        var f = Batch336Support.Table(Phase.Action, 0);
        var path = Stronghold(f, 0); path.DefinitionId = FalsePath;
        var inDiscard = Add(f, FalseRingOfAir, 0, Zone.FateDiscard);
        var inDeck = Add(f, FalseRingOfFire, 0, Zone.FateDeck); f.Players[0].FateDeck.Insert(0, inDeck.Id);
        var fe = MatchEngine.FromTestState(f);
        Accept(fe, Batch336Support.Use(fe, 0, Wave733Abilities.FalsePathSearch, path.Id));
        Check(fe.State.Cards[path.Id].Bowed, "bowed as the cost");
        Check(fe.State.PendingSearch is not null, "the search opens");
        Accept(fe, new GameCommand("selectSearchCard", 0, fe.Version, Choice: FalseRingOfAir));
        Check(fe.State.Cards[inDiscard.Id].Zone == Zone.Hand && fe.State.Players[0].Hand.Contains(inDiscard.Id), "the Fudo Ring from the discard pile is in the hand");
        Eq(fe.State.Cards[inDeck.Id].Zone, Zone.FateDeck, "the deck was not searched: the discard pile came first");
        Check(!Batch336Support.CanUse(fe, 0, Wave733Abilities.FalsePathSearch, path.Id), "once: bowed");
    }
}
