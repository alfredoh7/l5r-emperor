using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #503, pass group P6 (EmperorP6.cs): Kitsune Haruki, Daigotsu Susaiken, Kuni Renyu, Otomo Suikihime, Shosuro Ietsuno (personality-wave-116), Perfect Sacrifice (strategy-wave-102),
// Incredible Tragedy, In Search of Guidance, Dark Experiments, Distant Expansion (event-wave-29), Mining Apparatus (item-wave-38), Illusory Defense (spell-wave-46). Every position is built
// explicitly and driven through QueryActions and the commands the engine offers.
internal static class EmperorP6Tests
{
    private const string Haruki = "oracle-l5r-4462", Susaiken = "oracle-l5r-1771", Renyu = "oracle-l5r-9808", Suikihime = "oracle-l5r-10094", Ietsuno = "oracle-l5r-7110",
        PerfectSacrifice = "oracle-l5r-5923", IncredibleTragedy = "oracle-l5r-10030", InSearchOfGuidance = "oracle-l5r-3723", DarkExperiments = "oracle-l5r-10029",
        DistantExpansion = "oracle-l5r-9792", MiningApparatus = "oracle-l5r-10120", IllusoryDefense = "oracle-l5r-3666", HarukisDeer = "oracle-l5r-9999", ForgottenTemple = "oracle-l5r-8094";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    // The two halves of a Battle/Limited action share one label, so they are told apart by the offered command's ability id.
    private static bool Offered(MatchEngine e, int player, string ability, string cardId, string? performerId = null) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.CardId == cardId && a.Command?.AbilityId == ability && (performerId is null || a.Command.PerformerId == performerId));
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void RunUntil(MatchEngine e, Func<bool> stop) => Batch336Support.RunUntil(e, stop);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static GameCommand[] EventChoices(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "eventChoice" && a.Enabled && a.Command is not null).Select(a => a.Command!).ToArray();

    public static void Run(TestRunner runner) => runner.Run("emperor-p6", [
        ("Published shape: the twelve wired cards print their scans' stats, keywords and text, carry their abilities and Effects, ship their art and are in their manifests", PublishedShape),
        ("Kitsune Haruki: after he enters play his Reaction creates Haruki's Deer (the published Proxy) at home; it is once per entry", HarukiCreatesDeer),
        ("Perfect Sacrifice (Battle): each Personality and Follower of the performing Samurai's unit gains a Force bonus equal to its current Force; his unit is destroyed before the turn ends", PerfectSacrificeBattle),
        ("Perfect Sacrifice (Limited) and refusals: a non-Samurai may not perform it; the Limited half works at home in the Action Phase and shares the one use", PerfectSacrificeLimited),
        ("Incredible Tragedy: revealed, it stays in play until its owner's second turn from now begins; meanwhile no Personality may be assigned during Cavalry Maneuvers (Infantry is unaffected)", IncredibleTragedyTests),
        ("In Search of Guidance: the search finds a Sensei Personality and refills the Event's province with him face-up; with no Sensei the province refills face-down", InSearchOfGuidanceTests),
        ("Dark Experiments: the chosen province permanently has -2 strength and loses its Region; later a Region attaching there is destroyed and a Holding bought from it has a -1 Gold Production token", DarkExperimentsTests),
        ("Daigotsu Susaiken: when one of his controller's provinces refills he may have it refilled face-up, once per turn; declining spends nothing; the other player's refills are not offered", SusaikenRefills),
        ("Distant Expansion: revealed, it stays in play until its owner's third turn from now ends; in his Dynasty Phase he may refill face-up once per destroyed province of his, not without one", DistantExpansionTests),
        ("Shosuro Ietsuno: after a card refills his battlefield's province his controller sees it and may switch it with the same player's card in another province; not when he is elsewhere", IetsunoRefillTrigger),
        ("Shosuro Ietsuno, Battle: target an enemy card without attachments: bow it; a card with attachments is not a target", IetsunoBow),
        ("Kuni Renyu, Battle: the target's controller bows the unit and destroys its Shadowlands cards, or Renyu's controller creates a strength-4 province to the left of his leftmost one", RenyuPurge),
        ("Kuni Renyu defending: the created province renumbers the battle -- the units, the current and unresolved battlefields follow their province, which is still the one fought", RenyuProvinceDuringDefence),
        ("Kuni Renyu's trait: Honor losses from an action that targeted him are reduced to 0; the same action against another Personality costs its Honor", RenyuHonorShield),
        ("Otomo Suikihime: once per game, after she enters play, her controller gains a province to the left of his leftmost one; after she next leaves play his leftmost province is destroyed", SuikihimeLands),
        ("Otomo Suikihime, refusals: not with another player's Forgotten Temple in play; she may not be assigned, will not become dishonorable and another player's Political action may not target her", SuikihimeTraits),
        ("Mining Apparatus: removing it and Holdings with total base Gold Cost 10 or more gains a province to the left of the leftmost one; a total below 10 is refused; the offered default meets it", MiningApparatusTests),
        ("Illusory Defense: its controller's Air Shugenja have +1C; bowing performing Shugenja with 10 or more total Chi and removing it creates a leftmost province that never holds a Dynasty card", IllusoryDefenseTests),
    ]);

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, params string[] ids)
        {
            Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0)), string.Join(",", ids), $"{wave}'s manifest");
            Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
            foreach (var id in ids)
            {
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
                Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
                Check(!CardCatalog.Get(id).ScenarioOnly && !CardCatalog.Get(id).CreatedOnly, $"{id} is playable");
            }
        }
        void Card(string id, string name, CardType type, string effect, string abilities, string keywords, string text)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == type, $"{id} is {name} ({type})");
            Eq(c.Effect, effect, $"{name}'s Effect");
            Eq(string.Join(",", c.Abilities ?? []), abilities, $"{name}'s abilities");
            Eq(string.Join(",", (c.Keywords ?? []).Order(StringComparer.Ordinal)), keywords, $"{name}'s keywords");
            Eq(c.Text, text, $"{name} prints its scan's text");
        }
        Card(Haruki, "Kitsune Haruki", CardType.Personality, "none", "haruki-create", "Earth,Mantis Clan,Shugenja",
            "Reaction: After Haruki enters play: Create a 2F/2C/3PH Mantis Clan • Nonhuman • Deer • Spirit Personality with the ability, \"Limited: If another player has higher Family Honor than you: Gain 2 Honor.\"");
        var haruki = CardCatalog.Get(Haruki);
        Check(haruki.Clan == "Mantis" && haruki.GoldCost == 6 && haruki.Force == 0 && haruki.Chi == 2 && haruki.PersonalHonor == 3 && haruki.HonorRequirement == 4, "Haruki's stats");
        Card(Susaiken, "Daigotsu Susaiken", CardType.Personality, "discard-other-sensei", "", "Conqueror,Loyal,Samurai,Sensei,Shadowlands,Spider Clan",
            "After Susaiken enters play: Discard all your other Sensei Personalities.\nOnce per turn, you may refill one of your provinces face-up.");
        var susaiken = CardCatalog.Get(Susaiken);
        Check(susaiken.Clan == "Spider" && susaiken.GoldCost == 7 && susaiken.Force == 4 && susaiken.Chi == 3 && susaiken.PersonalHonor == 0 && susaiken.HonorRequirement is null, "Susaiken's stats");
        Card(Renyu, "Kuni Renyu", CardType.Personality, "renyu-honor-shield", "renyu-purge", "Crab Clan,Daimyo,Earth,Jade,Loyal,Shugenja,Unique,Witch Hunter",
            "After an action targets Renyu: Reduce any Honor losses from its effects to 0.\nBattle: Target an enemy unit: Its controller may choose to bow it and destroy all Shadowlands cards in it. If he did not choose this, create a province with a base strength of 4 to the left of your leftmost province.");
        var renyu = CardCatalog.Get(Renyu);
        Check(renyu.Clan == "Crab" && renyu.GoldCost == 8 && renyu.Force == 4 && renyu.Chi == 4 && renyu.PersonalHonor == 3 && renyu.HonorRequirement is null, "Renyu's stats");
        Card(Suikihime, "Otomo Suikihime", CardType.Personality, "suikihime-governor", "suikihime-lands", "Courtier,Imperial,Second City Governor,Unique",
            "You may not assign, and will not move, Suikihime to battlefields. Suikihime will not become dishonorable. Other players' Political actions may not target Suikihime.\nReaction: Once per game, after Suikihime enters play, if no other player controls The Forgotten Temple: Suikihime grants you new lands in the Colonies. Gain a province to the left of your leftmost province. After the next time this game Suikihime leaves play, destroy your leftmost province.");
        var suikihime = CardCatalog.Get(Suikihime);
        Check(suikihime.Clan == "Unaligned" && suikihime.GoldCost == 10 && suikihime.Force == 0 && suikihime.Chi == 3 && suikihime.PersonalHonor == 4 && suikihime.HonorRequirement == 8, "Suikihime's stats");
        Card(Ietsuno, "Shosuro Ietsuno", CardType.Personality, "ietsuno-refill-look", "ietsuno-bow", "Assassin,Ninja,Scorpion Clan",
            "After a card from a deck refills Ietsuno's battlefield's province: Look at it. You may switch it with a card owned by the same player in another province.\nBattle: Target an enemy card without attachments: Bow it.");
        var ietsuno = CardCatalog.Get(Ietsuno);
        Check(ietsuno.Clan == "Scorpion" && ietsuno.GoldCost == 7 && ietsuno.Force == 4 && ietsuno.Chi == 3 && ietsuno.PersonalHonor == 0, "Ietsuno's stats (Ninja is a keyword, not a Clan)");
        Manifest("personality-wave-116", Susaiken, Haruki, Ietsuno, Renyu, Suikihime);
        Card(PerfectSacrifice, "Perfect Sacrifice", CardType.Strategy, "none", "perfect-sacrifice-battle,perfect-sacrifice-limited", "Dark Virtue,Perfection",
            "Battle/Limited: Choose your performing Samurai: Give each Personality and Follower in his unit a Force bonus equal to its current Force. Before this turn ends, destroy his unit.");
        Check(CardCatalog.Get(PerfectSacrifice).Focus == 2 && CardCatalog.Get(PerfectSacrifice).GoldCost == 0, "Perfect Sacrifice's Focus and cost");
        Manifest("strategy-wave-102", PerfectSacrifice);
        Card(IncredibleTragedy, "Incredible Tragedy", CardType.Event, "incredible-tragedy", "", "Unique", "Until your second turn from now begins, Personalities may not be assigned during the Cavalry Maneuvers Segment.");
        Card(InSearchOfGuidance, "In Search of Guidance", CardType.Event, "in-search-of-guidance", "", "Unique", "Search your Dynasty deck for a Sensei Personality. Refill this province with him, face-up.");
        Card(DarkExperiments, "Dark Experiments", CardType.Event, "dark-experiments", "", "Unique",
            "Choose a province. Permanently give it -2 strength and destroy any Region attached to it. Until the game ends, after a Region attaches to it, destroy the Region, and after a Holding enters play from it, give the Holding a -1 Gold Production token.");
        Card(DistantExpansion, "Distant Expansion", CardType.Event, "distant-expansion", "", "Unique",
            "Until your third turn from now ends, you have the trait, \"In your Dynasty Phase, one time for each of your provinces destroyed this game, you may refill one of your provinces face-up.\"");
        Manifest("event-wave-29", InSearchOfGuidance, DistantExpansion, DarkExperiments, IncredibleTragedy);
        Card(MiningApparatus, "Mining Apparatus", CardType.Item, "none", "mining-apparatus-province", "Unique",
            "Limited: Remove this card and any number of Holdings you control with total base Gold Cost of 10 or higher from the game: Gain a province to the left of your leftmost province.");
        Check(CardCatalog.Get(MiningApparatus).GoldCost == 10 && CardCatalog.Get(MiningApparatus).Focus == 4, "Mining Apparatus' cost and Focus");
        Manifest("item-wave-38", MiningApparatus);
        Card(IllusoryDefense, "Illusory Defense", CardType.Spell, "illusory-defense-air-chi", "illusory-defense-province", "Air,Ritual",
            "Your Air Shugenja have +1C.\nLimited: Bow one or more of your performing Shugenja with 10 or greater total Chi, and remove this card from the game: Create a province to the left of your leftmost province which permanently does not hold Dynasty cards.");
        Check(CardCatalog.Get(IllusoryDefense).GoldCost == 8 && CardCatalog.Get(IllusoryDefense).Focus == 2, "Illusory Defense's cost and Focus");
        Manifest("spell-wave-46", IllusoryDefense);
        Eq(AbilityCatalog.All["haruki-create"].Timing, "Reaction"); Eq(AbilityCatalog.All["perfect-sacrifice-battle"].Timing, "Battle"); Eq(AbilityCatalog.All["perfect-sacrifice-limited"].Timing, "Limited");
        Eq(AbilityCatalog.All["ietsuno-bow"].Timing, "Battle"); Eq(AbilityCatalog.All["renyu-purge"].Timing, "Battle"); Eq(AbilityCatalog.All["suikihime-lands"].Timing, "Reaction");
        Check(AbilityCatalog.All["suikihime-lands"].OncePerGame, "Suikihime's Reaction is once per game");
        Eq(AbilityCatalog.All["mining-apparatus-province"].Timing, "Limited"); Eq(AbilityCatalog.All["illusory-defense-province"].Timing, "Limited");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["haruki-create"].Creates!).Id, HarukisDeer, "Haruki creates the published Haruki's Deer");
    }

    // ---- Kitsune Haruki -------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine Buying(string definition, int owner = 0, Action<MatchState>? prepare = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Dynasty; s.ActivePlayerId = owner; s.PriorityPlayerId = owner;
        var card = s.Cards[s.Players[owner].Provinces[0].CardId!]; card.DefinitionId = definition; card.FaceUp = true;
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", owner, Zone.Home);
        s.Players[owner].Honor = 20;
        prepare?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static void Buy(MatchEngine e, string cardId, int owner = 0)
    {
        var quote = e.QuotePurchase(owner, cardId);
        Check(quote.Error is null, quote.Error ?? "");
        Accept(e, new GameCommand("purchase", owner, e.Version, CardId: cardId, SourceIds: quote.Payments[0].SourceIds));
    }

    private static void HarukiCreatesDeer()
    {
        var e = Buying(Haruki, prepare: s => s.Players[0].Clan = "Mantis");
        var id = e.State.Players[0].Provinces[0].CardId!;
        Buy(e, id);
        Check(e.State.PendingChoice?.Type is "proclaim" or "entryReaction", "an entering-play window opened");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: "haruki-create"));
        var deer = e.State.Cards.Values.Single(c => c.DefinitionId == HarukisDeer);
        Eq(deer.OwnerId, 0); Eq(deer.Zone, Zone.Home, "the Deer is created at home");
        Check((CardCatalog.Get(HarukisDeer).Abilities ?? []).Contains("haruki-deer-honor"), "the Deer carries its Limited ability");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: "haruki-create")); // once per entry
        Eq(e.State.Cards.Values.Count(c => c.DefinitionId == HarukisDeer), 1, "only one Deer");
    }

    // ---- Perfect Sacrifice ----------------------------------------------------------------------------------------------------------------------------------------

    private static void PerfectSacrificeBattle()
    {
        var s = Table(Phase.Battle, 1);
        var samurai = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(samurai, "Samurai");
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: samurai.Id);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, PerfectSacrifice, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var samuraiForce = e.CardForceForTest(samurai.Id); var followerForce = e.CardForceForTest(follower.Id);
        Check(!Offered(e, 1, "perfect-sacrifice-limited", strategy.Id), "the Limited half is not offered in a battle");
        Check(Offered(e, 1, "perfect-sacrifice-battle", strategy.Id, samurai.Id), "the Battle half is");
        Accept(e, Use(e, 1, "perfect-sacrifice-battle", strategy.Id, performerId: samurai.Id));
        Eq(e.CardForceForTest(samurai.Id), samuraiForce * 2, "the Samurai's Force bonus equals his Force");
        Eq(e.CardForceForTest(follower.Id), followerForce * 2, "the Follower's Force bonus equals its own Force");
        Check(e.State.DelayedEffects.Any(d => d.CardId == samurai.Id && d.Kind == "destroy" && d.Until == "turn-end"), "his unit is to be destroyed before the turn ends");
    }

    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 300 && !stop(e); i++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var options = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).ToArray();
            var pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase")
                ?? throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type}): " + string.Join(", ", e.QueryActions(mover).Select(a => a.Type)));
            var command = pick.Command!;
            if (command.Type == "discardHand") command = command with { CardIds = e.State.Players[mover].Hand.Take(e.State.Players[mover].Hand.Count - 8).ToArray() };
            Accept(e, command);
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber})");
        return e;
    }

    private static void PerfectSacrificeLimited()
    {
        var s = Table(Phase.Action, 1);
        var samurai = Add(s, "training-lion-samurai", 1, Zone.Home); GiveKeyword(samurai, "Samurai");
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: samurai.Id);
        var plain = Add(s, "training-crane-samurai", 1, Zone.Home);
        var strategy = Add(s, PerfectSacrifice, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Offered(e, 1, "perfect-sacrifice-limited", strategy.Id, plain.Id), "a Personality without Samurai may not perform it");
        Check(Offered(e, 1, "perfect-sacrifice-limited", strategy.Id, samurai.Id), "the Samurai may");
        Reject(e, Use(e, 1, "perfect-sacrifice-limited", strategy.Id, performerId: plain.Id));
        Check(!Offered(e, 1, "perfect-sacrifice-battle", strategy.Id), "the Battle half is not offered in the Action Phase");
        var force = e.CardForceForTest(samurai.Id);
        Accept(e, Use(e, 1, "perfect-sacrifice-limited", strategy.Id, performerId: samurai.Id));
        Eq(e.CardForceForTest(samurai.Id), force * 2, "doubled at home too");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was played");
        var turn = e.State.TurnNumber;
        Passing(e, x => x.State.TurnNumber > turn);
        Check(e.State.Cards[samurai.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "the Samurai was destroyed before the turn ended");
        Check(e.State.Cards[follower.Id].Zone is not Zone.Attached, "his Follower went with his unit");
        Check(e.State.Cards[plain.Id].Zone == Zone.Home, "nobody else was destroyed");
    }

    // ---- Events: reveal helpers (the group F convention: player 1's province 3, at the start of his turn) ------------------------------------------------------------

    private static CardInstance FaceDownIn(MatchState s, string def, int owner, int province)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); card.FaceUp = false; s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }

    private static MatchEngine Reveal(string eventDefinition, out CardInstance evt, Action<MatchState>? prepare = null, Func<MatchEngine, bool>? stop = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        foreach (var other in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Province)) other.DefinitionId = "training-marketplace";
        evt = FaceDownIn(s, eventDefinition, 1, 3);
        prepare?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => stop?.Invoke(e) == true || e.State.PendingChoice is { Type: "eventChoice" } || e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        return e;
    }

    // ---- Incredible Tragedy ---------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine Maneuvers(Phase phase, bool tragedy, out CardInstance cavalry, out CardInstance infantry)
    {
        var s = Table(Phase.Action, 1);
        s.Phase = phase; s.UnresolvedBattlefields = [0, 1, 2, 3]; s.CurrentBattleProvince = null;
        cavalry = Add(s, "training-lion-samurai", 1, Zone.Home); GiveKeyword(cavalry, "Cavalry");
        infantry = Add(s, "training-lion-samurai", 1, Zone.Home);
        if (tragedy) { var t = Add(s, IncredibleTragedy, 0, Zone.EventInPlay); t.ExpiresAtTurnsTaken = s.Players[0].TurnsTaken + 2; }
        return MatchEngine.FromTestState(s);
    }

    private static void IncredibleTragedyTests()
    {
        var e = Reveal(IncredibleTragedy, out var tragedy);
        Eq(e.State.Cards[tragedy.Id].Zone, Zone.EventInPlay, "it stays in play");
        Eq(e.State.Cards[tragedy.Id].ExpiresAtTurnsTaken, e.State.Players[1].TurnsTaken + 2, "until its owner's second turn from now begins");
        Check(e.State.Players[1].Provinces[3].CardId != tragedy.Id && e.State.Players[1].Provinces[3].CardId is not null, "its province refilled");
        // Cavalry Maneuvers: with the Event in play no Personality is offered or accepted, of either player.
        var barred = Maneuvers(Phase.CavalryDeclaration, true, out var cav, out _);
        Check(e.QueryActions(1) is not null, "sanity");
        var offer = barred.QueryActions(1).Where(a => a.Type == "attack").ToArray();
        Check(offer.All(a => !a.Enabled || (a.Command?.Assignments?.Count ?? 0) == 0), "no Cavalry unit is offered for assignment");
        Reject(barred, new GameCommand("attack", 1, barred.Version, Assignments: new Dictionary<string, int> { [cav.Id] = 0 }));
        var free = Maneuvers(Phase.CavalryDeclaration, false, out var cav2, out _);
        Accept(free, new GameCommand("attack", 1, free.Version, Assignments: new Dictionary<string, int> { [cav2.Id] = 0 }));
        // Infantry Maneuvers are not touched.
        var infantryPhase = Maneuvers(Phase.InfantryDeclaration, true, out _, out var inf);
        Accept(infantryPhase, new GameCommand("attack", 1, infantryPhase.Version, Assignments: new Dictionary<string, int> { [inf.Id] = 0 }));
    }

    // ---- In Search of Guidance -------------------------------------------------------------------------------------------------------------------------------------

    private static void InSearchOfGuidanceTests()
    {
        string senseiId = "";
        var e = Reveal(InSearchOfGuidance, out var evt, s =>
        {
            var sensei = Add(s, "training-sensei-personality", 1, Zone.DynastyDeck); sensei.FaceUp = false;
            s.Players[1].DynastyDeck.Insert(3, sensei.Id); senseiId = sensei.Id;
        }, x => x.State.PendingSearch is not null);
        Check(e.State.PendingSearch is not null, "the search opened");
        var take = e.QueryActions(1).FirstOrDefault(a => a.Type == "selectSearchCard" && a.Enabled);
        Check(take is not null, "the Sensei is offered");
        Check(!e.QueryActions(1).Any(a => a.Type == "passSearch" && a.Enabled), "a qualifying Sensei must be taken");
        Accept(e, take!.Command!);
        RunUntil(e, () => e.State.PendingChoice is null && e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        var province = e.State.Players[1].Provinces[3];
        Eq(province.CardId, senseiId, "the Sensei refilled the Event's province");
        Check(e.State.Cards[senseiId].FaceUp && e.State.Cards[senseiId].Zone == Zone.Province, "face-up");
        Check(!e.State.Players[1].DynastyDeck.Contains(senseiId), "out of the deck");
        Eq(e.State.Cards[evt.Id].Zone, Zone.DynastyDiscard, "the Event was discarded");
        // No Sensei: nothing is found and the province refills face-down as usual.
        var none = Reveal(InSearchOfGuidance, out _, stop: x => x.State.PendingSearch is not null);
        var pass = none.QueryActions(1).FirstOrDefault(a => a.Type == "passSearch" && a.Enabled);
        Check(pass is not null, "with no Sensei the search is passed");
        Accept(none, pass!.Command!);
        RunUntil(none, () => none.State.PendingChoice is null && none.State.ActivePlayerId == 1 && none.State.Phase == Phase.Action);
        var refilled = none.State.Players[1].Provinces[3].CardId;
        Check(refilled is not null && none.State.Cards[refilled].Zone == Zone.Province, "refilled from the deck");
    }

    // ---- Dark Experiments ----------------------------------------------------------------------------------------------------------------------------------------

    private static void DarkExperimentsTests()
    {
        string regionId = "";
        var e = Reveal(DarkExperiments, out var evt, s =>
        {
            var region = Add(s, "training-bow-region", 0, Zone.Region, province: 1); s.Players[0].Provinces[1].RegionId = region.Id; regionId = region.Id;
        });
        Eq(e.State.PendingChoice?.Type, "eventChoice", "its player chooses a province");
        var before = e.ProvinceStrengthForTest(0, 1);
        var options = EventChoices(e, 1);
        Eq(options.Length, 8, "any province of either player");
        Eq(options[0].Choice!.Split(':')[0], "0", "the opponent's provinces are listed first");
        Eq(options[^1].Choice!.Split(':')[0], "1", "the chooser's own provinces are listed last");
        Accept(e, options.Single(c => c.Choice == "0:1"));
        Eq(StrengthNextTurn(e, 0, 1), before - 2, "permanently -2 strength (it outlasts the turn)");
        Check(e.State.Players[0].Provinces[1].DarkExperimentsMarked, "the province is marked");
        Eq(DarkExperimentsMark.Strength, -2, "the board marker shows the strength change the engine applies"); Eq(DarkExperimentsMark.Production, -1, "the board marker shows the Gold Production token the engine gives");
        Check(e.GetView(0).Players[0].Provinces.Where(p => p.Index != 1).All(p => !p.DarkExperimentsMarked), "only the chosen province carries the mark in the view");
        Eq(e.State.Cards[regionId].Zone, Zone.DynastyDiscard, "its Region was destroyed");
        Check(e.GetView(0).Players[0].Provinces[1].DarkExperimentsMarked, "the mark is public in the view");
        Eq(e.State.Cards[evt.Id].Zone, Zone.DynastyDiscard, "the Event was discarded");
        // Later: a Region revealed in the marked province attaches and is destroyed.
        var later = Reveal("training-bow-region", out var region2, s => { s.Players[1].Provinces[3].DarkExperimentsMarked = true; });
        Eq(later.State.Cards[region2.Id].Zone, Zone.DynastyDiscard, "a Region attaching to the marked province is destroyed");
        Check(later.State.Players[1].Provinces[3].RegionId is null, "nothing stays attached");
        var unmarked = Reveal("training-bow-region", out var region3);
        Eq(unmarked.State.Cards[region3.Id].Zone, Zone.Region, "an unmarked province keeps its Region");
        // A Holding bought from the marked province has a -1 Gold Production token.
        var buy = Buying("training-marketplace", prepare: s => s.Players[0].Provinces[0].DarkExperimentsMarked = true);
        var holding = buy.State.Players[0].Provinces[0].CardId!;
        var printed = buy.GoldProduced(holding, 0);
        Buy(buy, holding);
        Check(buy.State.Cards[holding].Tokens.Any(t => t.Stat == "GoldProduction" && t.Amount == -1), "a -1 Gold Production token");
        Eq(buy.GoldProduced(holding, 0), printed - 1, "it produces 1 less Gold");
        var plain = Buying("training-marketplace");
        var plainHolding = plain.State.Players[0].Provinces[0].CardId!;
        Buy(plain, plainHolding);
        Check(plain.State.Cards[plainHolding].Tokens.Count == 0, "no token from an unmarked province");
    }

    // ---- Daigotsu Susaiken ---------------------------------------------------------------------------------------------------------------------------------------

    private static void SusaikenRefills()
    {
        var e = Buying("training-crane-samurai", prepare: s =>
        {
            Add(s, Susaiken, 0, Zone.Home);
            var second = s.Cards[s.Players[0].Provinces[1].CardId!]; second.DefinitionId = "training-crane-samurai"; second.FaceUp = true;
        });
        var first = e.State.Players[0].Provinces[0].CardId!;
        Buy(e, first);
        CloseEntryWindows(e);
        Eq(e.State.PendingChoice?.Type, "eventChoice", "the refill is offered face-up");
        Eq(e.State.PriorityPlayerId, 0, "its controller decides");
        var refill = e.State.Players[0].Provinces[0].CardId!;
        Check(!e.State.Cards[refill].FaceUp, "the refill is face down until he decides");
        var options = EventChoices(e, 0);
        Check(options.Any(c => c.Choice == "no"), "he may decline");
        Accept(e, options.Single(c => c.Choice!.StartsWith("susaiken:")));
        Check(e.State.Cards[refill].FaceUp, "refilled face-up");
        Check(e.State.PendingChoice is null && e.State.Phase == Phase.Dynasty && e.State.PriorityPlayerId == 0, "the Dynasty Phase carries on");
        // Once per turn: the next refill is not offered.
        Buy(e, e.State.Players[0].Provinces[1].CardId!);
        CloseEntryWindows(e);
        Check(e.State.PendingChoice is null, "only once per turn");
        Check(!e.State.Cards[e.State.Players[0].Provinces[1].CardId!].FaceUp, "the second refill stays face down");
        // Declining spends nothing.
        var d = Buying("training-crane-samurai", prepare: s =>
        {
            Add(s, Susaiken, 0, Zone.Home);
            var second = s.Cards[s.Players[0].Provinces[1].CardId!]; second.DefinitionId = "training-crane-samurai"; second.FaceUp = true;
        });
        Buy(d, d.State.Players[0].Provinces[0].CardId!); CloseEntryWindows(d);
        Accept(d, EventChoices(d, 0).Single(c => c.Choice == "no"));
        Check(!d.State.Cards[d.State.Players[0].Provinces[0].CardId!].FaceUp, "declined: face down");
        Buy(d, d.State.Players[0].Provinces[1].CardId!); CloseEntryWindows(d);
        Eq(d.State.PendingChoice?.Type, "eventChoice", "declining did not spend the turn's use");
        // The other player's Susaiken is not asked about this player's refill.
        var o = Buying("training-crane-samurai", prepare: s => Add(s, Susaiken, 1, Zone.Home));
        Buy(o, o.State.Players[0].Provinces[0].CardId!); CloseEntryWindows(o);
        Check(o.State.PendingChoice is null, "only its controller's provinces");
    }

    private static void CloseEntryWindows(MatchEngine e)
    {
        for (var guard = 0; guard < 6 && e.State.PendingChoice is { Type: not "eventChoice" } pending; guard++)
        {
            var pass = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            if (pass is null) throw new Exception($"no pass in {pending.Type} for {pending.PlayerId}: " + string.Join("; ", e.QueryActions(e.State.PriorityPlayerId).Select(a => $"{a.Type} {a.Enabled}")) + " | priority " + e.State.PriorityPlayerId);
            Accept(e, pass.Command!);
        }
    }

    // ---- Distant Expansion ----------------------------------------------------------------------------------------------------------------------------------------

    private static void DistantExpansionTests()
    {
        var e = Reveal(DistantExpansion, out var evt);
        Eq(e.State.Cards[evt.Id].Zone, Zone.EventInPlay, "it stays in play");
        Eq(e.State.Cards[evt.Id].ExpiresAtTurnsTaken, e.State.Players[1].TurnsTaken + 4, "it leaves as its owner's fourth turn from now begins (after his third turn from now ended)");
        MatchEngine Dynasty(int destroyed) => Buying("training-crane-samurai", prepare: s =>
        {
            Add(s, DistantExpansion, 0, Zone.EventInPlay);
            for (var i = 0; i < destroyed; i++) { var p = s.Players[0].Provinces[3 - i]; p.Destroyed = true; if (p.CardId is { } c) s.Cards.Remove(c); p.CardId = null; }
            var second = s.Cards[s.Players[0].Provinces[1].CardId!]; second.DefinitionId = "training-crane-samurai"; second.FaceUp = true;
        });
        var one = Dynasty(1);
        Buy(one, one.State.Players[0].Provinces[0].CardId!); CloseEntryWindows(one);
        var options = EventChoices(one, 0);
        Check(options.Any(c => c.Choice!.StartsWith("distant:")), "one destroyed province: a face-up refill is offered");
        Accept(one, options.Single(c => c.Choice!.StartsWith("distant:")));
        Check(one.State.Cards[one.State.Players[0].Provinces[0].CardId!].FaceUp, "refilled face-up");
        Buy(one, one.State.Players[0].Provinces[1].CardId!); CloseEntryWindows(one);
        Check(one.State.PendingChoice is null, "one use per destroyed province this Dynasty Phase");
        var none = Dynasty(0);
        Buy(none, none.State.Players[0].Provinces[0].CardId!); CloseEntryWindows(none);
        Check(none.State.PendingChoice is null, "no destroyed province: nothing offered");
        // Another player's Distant Expansion gives this player nothing.
        var theirs = Buying("training-crane-samurai", prepare: s =>
        {
            Add(s, DistantExpansion, 1, Zone.EventInPlay);
            s.Players[0].Provinces[3].Destroyed = true; s.Players[1].Provinces[3].Destroyed = true;
        });
        Buy(theirs, theirs.State.Players[0].Provinces[0].CardId!); CloseEntryWindows(theirs);
        Check(theirs.State.PendingChoice is null, "only its owner has the trait");
    }

    // ---- Shosuro Ietsuno ------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine IetsunoTable(bool atThisBattlefield, out CardInstance reserve)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Add(s, Ietsuno, 1, atThisBattlefield ? Zone.Battlefield : Zone.Home, province: atThisBattlefield ? 0 : null);
        if (!atThisBattlefield) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        reserve = s.Cards[s.Players[0].Provinces[0].CardId!]; reserve.DefinitionId = "training-reserve-personality"; reserve.FaceUp = true;
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        return MatchEngine.FromTestState(s);
    }

    private static void RecruitReserve(MatchEngine e, string cardId)
    {
        var option = e.QueryActions(0).FirstOrDefault(a => a.Type == "recruitReserve" && a.CardId == cardId && a.Enabled && a.Command?.ClanDiscount != true && a.Command?.ClanAllegiance != true);
        Check(option is not null, "the Reserve recruit is offered: " + string.Join("; ", e.QueryActions(0).Select(a => $"{a.Type} {a.Enabled} {a.Reason}")));
        Accept(e, option!.Command!);
        CloseEntryWindows(e);
    }

    private static void IetsunoRefillTrigger()
    {
        var e = IetsunoTable(true, out var reserve);
        RecruitReserve(e, reserve.Id);
        Eq(e.State.PendingChoice?.Type, "eventChoice", "Ietsuno's decision opened");
        Eq(e.State.PriorityPlayerId, 1, "his controller decides");
        var refilled = e.State.Players[0].Provinces[0].CardId!;
        Check(e.GetView(1).ProvinceSights?.Any(v => v.OwnerId == 0 && v.Province == 0 && v.DefinitionId == e.State.Cards[refilled].DefinitionId) == true, "he has looked at the refilled card");
        Check(e.GetView(0).ProvinceSights is null || !e.GetView(0).ProvinceSights!.Any(v => v.Province == 0 && v.OwnerId == 0), "the other player did not");
        var options = EventChoices(e, 1);
        Check(options.Any(c => c.Choice == "none"), "he may not switch");
        var other = e.State.Players[0].Provinces[2].CardId!;
        Accept(e, options.Single(c => c.TargetId == other));
        Eq(e.State.Players[0].Provinces[0].CardId, other, "the other card now fills the battlefield's province");
        Eq(e.State.Players[0].Provinces[2].CardId, refilled, "the refilled card moved to the other province");
        Eq(e.State.Cards[refilled].ProvinceIndex, 2); Eq(e.State.Cards[other].ProvinceIndex, 0);
        Eq(e.State.PriorityPlayerId, 1, "the battle carries on with the priority the recruit left (the other player's)");
        // Declining keeps the cards where they are.
        var d = IetsunoTable(true, out var r2);
        RecruitReserve(d, r2.Id);
        var kept = d.State.Players[0].Provinces[0].CardId!;
        Accept(d, EventChoices(d, 1).Single(c => c.Choice == "none"));
        Eq(d.State.Players[0].Provinces[0].CardId, kept, "nothing switched");
        // Ietsuno at home: not his battlefield's province.
        var away = IetsunoTable(false, out var r3);
        RecruitReserve(away, r3.Id);
        Check(away.State.PendingChoice is null, "no decision when he is not at the refilled province's battlefield");
    }

    private static void IetsunoBow()
    {
        var s = Table(Phase.Battle, 1);
        var ietsuno = Add(s, Ietsuno, 1, Zone.Battlefield, province: 0);
        var bare = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var armed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: armed.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 1, "ietsuno-bow", ietsuno.Id).Any(a => a.Enabled && (a.TargetIds ?? []).Contains(armed.Id)), "a card with attachments is not a target");
        Accept(e, Offer(e, 1, "ietsuno-bow", ietsuno.Id, bare.Id));
        Check(e.State.Cards[bare.Id].Bowed, "the target is bowed");
    }

    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Options(e, player, ability, cardId);

    // ---- Kuni Renyu -----------------------------------------------------------------------------------------------------------------------------------------------

    private static void RenyuPurge()
    {
        MatchEngine Table1(out CardInstance renyu, out CardInstance target, out CardInstance follower, out CardInstance plainFollower)
        {
            var s = Table(Phase.Battle, 1);
            renyu = Add(s, Renyu, 1, Zone.Battlefield, province: 0);
            target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: target.Id); GiveKeyword(follower, "Shadowlands");
            plainFollower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: target.Id);
            return MatchEngine.FromTestState(s);
        }
        var e = Table1(out var renyu, out var target, out var slFollower, out var plain);
        Accept(e, Offer(e, 1, "renyu-purge", renyu.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the target's controller decides");
        Eq(e.State.PriorityPlayerId, 0);
        Accept(e, new GameCommand("chooseEitherOr", 0, e.Version, Choice: "purge"));
        Check(e.State.Cards[target.Id].Bowed && e.State.Cards[plain.Id].Bowed, "the unit is bowed");
        Check(e.State.Cards[slFollower.Id].Zone != Zone.Attached, "its Shadowlands card is destroyed");
        Eq(e.State.Cards[plain.Id].Zone, Zone.Attached, "its other cards stay");
        Eq(e.State.Players[1].Provinces.Count, 4, "no province created");
        // Refused: Renyu's controller (the Attacker here) creates a strength-4 province to the left of his leftmost one.
        var r = Table1(out var renyu2, out var target2, out _, out _);
        var oldLeftmost = r.State.Players[1].Provinces[0].CardId;
        Accept(r, Offer(r, 1, "renyu-purge", renyu2.Id, target2.Id));
        Accept(r, new GameCommand("chooseEitherOr", 0, r.Version, Choice: "refuse"));
        var provinces = r.State.Players[1].Provinces;
        Eq(provinces.Count, 5, "a province was created");
        Eq(provinces[0].Index, 0); Eq(provinces[0].PrintedStrength, 4, "base strength 4"); Eq(provinces[0].Strength, 4);
        Check(provinces[0].CardId is not null && !r.State.Cards[provinces[0].CardId!].FaceUp, "filled face-down from the Dynasty deck");
        Eq(provinces[1].CardId, oldLeftmost, "the old leftmost province is now the second");
        Eq(r.State.Cards[oldLeftmost!].ProvinceIndex, 1, "its card follows it");
        Check(!r.State.Cards[target2.Id].Bowed, "the unit was not bowed");
        Eq(r.State.CurrentBattleProvince, 0, "the Defender's battle is untouched");
        // A unit with no Shadowlands card: bowed only.
        Check(r.GetView(1).Players[1].Provinces.Length == 5, "the view shows five provinces");
    }

    private static void RenyuProvinceDuringDefence()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var renyu = Add(s, Renyu, 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var elsewhere = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 2);
        s.UnresolvedBattlefields = [0, 2];
        var e = MatchEngine.FromTestState(s);
        var fought = e.State.Players[0].Provinces[0].CardId;
        var strength = e.ProvinceStrengthForTest(0, 0);
        Accept(e, Offer(e, 0, "renyu-purge", renyu.Id, attacker.Id));
        Accept(e, new GameCommand("chooseEitherOr", 1, e.Version, Choice: "refuse"));
        Eq(e.State.Players[0].Provinces.Count, 5, "the Defender gained a province");
        Eq(e.State.CurrentBattleProvince, 1, "the current battlefield follows its province");
        Eq(string.Join(",", e.State.UnresolvedBattlefields.Order()), "1,3", "so do the unresolved battlefields");
        Eq(e.State.Cards[renyu.Id].ProvinceIndex, 1); Eq(e.State.Cards[attacker.Id].ProvinceIndex, 1); Eq(e.State.Cards[elsewhere.Id].ProvinceIndex, 3, "units follow their battlefield");
        Eq(e.State.Players[0].Provinces[1].CardId, fought, "the province fought over is the same");
        Eq(e.ProvinceStrengthForTest(0, 1), strength, "with its strength");
        Eq(e.State.Players[0].Provinces[0].PrintedStrength, 4, "the new province has base strength 4");
        // The battle resolves at the right province: both pass, the Attacker's 4F against Renyu's 4F ties (no province destroyed); the new province is untouched.
        Accept(e, PassC(e)); Accept(e, PassC(e));
        Check(e.State.Players[0].Provinces.All(p => !p.Destroyed), "no province was destroyed in a tie");
        Check(e.State.CurrentBattleProvince is null or 3, "the battle at the renumbered province resolved");
    }

    private static void RenyuHonorShield()
    {
        var s = Table(Phase.Battle, 1);
        var renyu = Add(s, Renyu, 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var striker = Add(s, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 1, "training-honor-strike", striker.Id, renyu.Id));
        Eq(Honor(e, 0), honor, "the loss from an action that targeted Renyu is reduced to 0");
        Check(e.State.Events.Any(x => x.Type == "honorLossReduced"), "announced");
        var s2 = Table(Phase.Battle, 1);
        Add(s2, Renyu, 0, Zone.Battlefield, province: 0);
        var other2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var striker2 = Add(s2, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 0);
        Accept(e2, Offer(e2, 1, "training-honor-strike", striker2.Id, other2.Id));
        Eq(Honor(e2, 0), honor2 - 2, "another Personality targeted: the loss stands");
        _ = other;
    }

    // ---- Otomo Suikihime ------------------------------------------------------------------------------------------------------------------------------------------

    private static void SuikihimeLands()
    {
        var e = Buying(Suikihime);
        var id = e.State.Players[0].Provinces[0].CardId!;
        Buy(e, id);
        Check(e.State.PendingChoice?.Type is "proclaim" or "entryReaction", "her entering-play window opened");
        var leftmostCard = e.State.Players[0].Provinces[1].CardId; // province 0 was refilled by the purchase
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: "suikihime-lands"));
        var provinces = e.State.Players[0].Provinces;
        Eq(provinces.Count, 5, "a province gained");
        Eq(provinces[0].Index, 0); Eq(provinces[0].PrintedStrength, CardCatalog.Get(e.State.Cards[e.State.Players[0].StrongholdId].DefinitionId).ProvinceStrength, "at the Stronghold's base strength");
        Eq(provinces[2].CardId, leftmostCard, "the row moved one to the right");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: "suikihime-lands"));
        CloseEntryWindows(e);
        // After she leaves play, her controller's leftmost province is destroyed (after the command that removed her).
        var gone = Batch336Support.Edit(e, st => { st.Cards[id].Zone = Zone.DynastyDiscard; });
        var newLeftmost = gone.State.Players[0].Provinces[0];
        Check(!newLeftmost.Destroyed, "not yet");
        Accept(gone, new GameCommand("endDynasty", 0, gone.Version));
        Check(gone.State.Players[0].Provinces[0].Destroyed, "the leftmost province was destroyed");
        Check(gone.State.Players[0].Provinces.Skip(1).All(p => !p.Destroyed), "only that one");
        Eq(gone.State.P6.LeavesPlayWatches.Count, 0, "the watch is spent");
    }

    private static void SuikihimeTraits()
    {
        // The Forgotten Temple controlled by another player: her Reaction is not available.
        var t = Buying(Suikihime, prepare: s => Add(s, ForgottenTemple, 1, Zone.Home));
        var id = t.State.Players[0].Provinces[0].CardId!;
        Buy(t, id);
        Reject(t, new GameCommand("useAbility", 0, t.Version, CardId: id, AbilityId: "suikihime-lands"));
        Eq(t.State.Players[0].Provinces.Count, 4, "no province gained");
        // May not assign.
        var s = Table(Phase.Action, 0); s.Phase = Phase.InfantryDeclaration; s.UnresolvedBattlefields = [0, 1, 2, 3]; s.CurrentBattleProvince = null;
        var suiki = Add(s, Suikihime, 0, Zone.Home); var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Where(a => a.Type == "attack" && a.Command?.Assignments is not null).All(a => !a.Command!.Assignments!.ContainsKey(suiki.Id)), "she is not offered for assignment");
        Reject(e, new GameCommand("attack", 0, e.Version, Assignments: new Dictionary<string, int> { [suiki.Id] = 0 }));
        Accept(e, new GameCommand("attack", 0, e.Version, Assignments: new Dictionary<string, int> { [samurai.Id] = 0 }));
        // Will not become dishonorable; another player's Political action may not target her; her controller's may.
        var b = Table(Phase.Battle, 1);
        var her = Add(b, Suikihime, 0, Zone.Battlefield, province: 0);
        var dishonorer = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0); dishonorer.GrantedAbilities.Add(new("training-dishonor-target", "test-fixture", GrantDuration.Permanent));
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 1, "training-dishonor-target", dishonorer.Id, her.Id));
        Check(!eb.State.Cards[her.Id].Dishonorable, "she will not become dishonorable");
        var p = Table(Phase.Action, 1);
        var hers = Add(p, Suikihime, 0, Zone.Home); var otherPersonality = Add(p, "training-crane-samurai", 0, Zone.Home);
        var political = Add(p, "training-political-strategy", 1, Zone.Hand);
        var ownPolitical = Add(p, "training-political-strategy", 0, Zone.Hand);
        for (var i = 0; i < 3; i++) { Add(p, "training-marketplace", 1, Zone.Home); Add(p, "training-marketplace", 0, Zone.Home); }
        var ep = MatchEngine.FromTestState(p);
        Check(!Options(ep, 1, "training-bow-any", political.Id).Any(a => a.Enabled && (a.TargetIds ?? []).Contains(hers.Id)), "another player's Political action may not target her");
        Check(Options(ep, 1, "training-bow-any", political.Id).Any(a => a.Enabled && (a.TargetIds ?? []).Contains(otherPersonality.Id)), "it may target another Personality");
        var viaOwn = Batch336Support.Edit(ep, st => { st.PriorityPlayerId = 0; });
        Check(Options(viaOwn, 0, "training-bow-any", ownPolitical.Id).Any(a => a.Enabled && (a.TargetIds ?? []).Contains(hers.Id)), "her controller's Political action may");
    }

    // ---- Mining Apparatus -----------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine MiningTable(out CardInstance apparatus, out CardInstance[] mines)
    {
        var s = Table(Phase.Action, 0);
        foreach (var h in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding).ToArray()) s.Cards.Remove(h.Id);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        apparatus = Add(s, MiningApparatus, 0, Zone.Attached, parent: host.Id);
        mines = Enumerable.Range(0, 5).Select(_ => Add(s, "training-marketplace", 0, Zone.Home)).ToArray(); // base Gold Cost 2 each
        return MatchEngine.FromTestState(s);
    }

    private static void MiningApparatusTests()
    {
        var e = MiningTable(out var apparatus, out var mines);
        var offered = Options(e, 0, "mining-apparatus-province", apparatus.Id).FirstOrDefault(a => a.Enabled);
        Check(offered is not null, "offered with a default selection that meets the total");
        Eq(offered!.Command!.Selections!.Single().CardIds!.Length, 5, "the default is the fewest Holdings reaching 10");
        Reject(e, Use(e, 0, "mining-apparatus-province", apparatus.Id, selections: Pick("holdings", mines.Take(4).Select(m => m.Id).ToArray()))); // total 8
        var leftmost = e.State.Players[0].Provinces[0].CardId;
        Accept(e, Use(e, 0, "mining-apparatus-province", apparatus.Id, selections: Pick("holdings", mines.Select(m => m.Id).ToArray())));
        Check(mines.All(m => e.State.Cards[m.Id].Zone == Zone.RemovedFromGame), "the Holdings were removed from the game");
        Eq(e.State.Cards[apparatus.Id].Zone, Zone.RemovedFromGame, "so was the Apparatus");
        Eq(e.State.Players[0].Provinces.Count, 5, "a province gained");
        Eq(e.State.Players[0].Provinces[1].CardId, leftmost, "to the left of the leftmost one");
        Check(e.State.Players[0].Provinces[0].CardId is not null, "the new province refilled");
        // With less than 10 in Holdings it is not offered.
        var poor = MiningTable(out var a2, out var m2);
        var less = Batch336Support.Edit(poor, st => st.Cards.Remove(m2[0].Id));
        Check(!Options(less, 0, "mining-apparatus-province", a2.Id).Any(a => a.Enabled), "Holdings totalling 8 do not qualify");
    }

    // ---- Illusory Defense ----------------------------------------------------------------------------------------------------------------------------------------

    private static void IllusoryDefenseTests()
    {
        var s = Table(Phase.Action, 0);
        var air = Add(s, "training-seeds5-air-shugenja", 0, Zone.Home);
        var spell = Add(s, IllusoryDefense, 0, Zone.Attached, parent: air.Id);
        var air2 = Add(s, "training-seeds5-air-shugenja", 0, Zone.Home);
        var plain = Add(s, "training-shugenja", 0, Zone.Home);
        var enemyAir = Add(s, "training-seeds5-air-shugenja", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.PersonalityChiForTest(air.Id), 4, "your Air Shugenja have +1C"); Eq(e.PersonalityChiForTest(air2.Id), 4);
        Eq(e.PersonalityChiForTest(plain.Id), 3, "not a non-Air Shugenja"); Eq(e.PersonalityChiForTest(enemyAir.Id), 3, "not another player's");
        Reject(e, Use(e, 0, "illusory-defense-province", spell.Id, selections: Pick("performers", air.Id, air2.Id))); // 8 total Chi
        Check(Options(e, 0, "illusory-defense-province", spell.Id).Any(a => a.Enabled), "offered: the three reach 11");
        Accept(e, Use(e, 0, "illusory-defense-province", spell.Id, selections: Pick("performers", air.Id, air2.Id, plain.Id)));
        Check(e.State.Cards[air.Id].Bowed && e.State.Cards[air2.Id].Bowed && e.State.Cards[plain.Id].Bowed, "the performing Shugenja are bowed");
        Eq(e.State.Cards[spell.Id].Zone, Zone.RemovedFromGame, "the Spell is removed from the game");
        Eq(e.PersonalityChiForTest(air2.Id), 3, "its +1C is gone with it");
        var provinces = e.State.Players[0].Provinces;
        Eq(provinces.Count, 5, "a province created");
        Check(provinces[0].HoldsNoDynastyCards && provinces[0].CardId is null, "to the left of the leftmost, holding no Dynasty card");
        Check(e.GetView(0).Players[0].Provinces[0].HoldsNoDynastyCards, "public in the view");
        // It is never refilled: an Event revealed ... the Dynasty Phase refills leave it empty.
        var later = Batch336Support.Edit(e, st => { st.Phase = Phase.Dynasty; st.PriorityPlayerId = 0; });
        Accept(later, new GameCommand("endDynasty", 0, later.Version));
        Check(later.State.Players[0].Provinces[0].CardId is null, "still empty");
        // A lone Shugenja below 10 Chi cannot take it.
        var s2 = Table(Phase.Action, 0);
        var lone = Add(s2, "training-seeds5-air-shugenja", 0, Zone.Home);
        var spell2 = Add(s2, IllusoryDefense, 0, Zone.Attached, parent: lone.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!Options(e2, 0, "illusory-defense-province", spell2.Id).Any(a => a.Enabled), "4 total Chi does not qualify");
    }
}
