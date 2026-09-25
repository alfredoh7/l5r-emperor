using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), proxy group X: Goju Kobashi and Suiteiru no Oni (personality-wave-76) and the Spell Summon Swamp Spirits (spell-wave-24), blocked until now only because the
// Proxy Follower each creates had no scan -- Kobashi's Spawn (oracle-l5r-10008), Suiteiru's Podling (oracle-l5r-10021), Swamp Spirit (oracle-l5r-10014), all published CreatedOnly. Every scenario is an explicit
// position on the economy table (player 0 the Crane, player 1 the Lion), driven through QueryActions and the commands it offers.
internal static class EmperorProxyXTests
{
    private const string Kobashi = "oracle-l5r-2853", Suiteiru = "oracle-l5r-7618", SummonSwampSpirits = "oracle-l5r-7633",
        KobashisSpawn = "oracle-l5r-10008", SuiteirusPodling = "oracle-l5r-10021", SwampSpirit = "oracle-l5r-10014", Conscripts = "oracle-l5r-10004";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void ChiBonus(CardInstance card, int amount) => card.Modifiers.Add(new("test-source", "test", "Chi", amount));

    public static void Run(TestRunner runner) => runner.Run("emperor-proxy-X", [
        ("Published shape: Goju Kobashi, Suiteiru no Oni and Summon Swamp Spirits print their scans' stats, keywords and ability ids, and the three created-only Follower Proxies print 1F with their scans' keywords; all ship their art and are in the manifests, and every creator resolves its Proxy", PublishedShape),
        ("Goju Kobashi and Suiteiru no Oni: entering play costs 1 and 4 Honor", EntryHonorLosses),
        ("Goju Kobashi: destroy a target enemy attachment and, if one of your Ninja is also targeted, create a Kobashi's Spawn attached to him -- with no Ninja named only the destruction happens", KobashiConsumesAndCreates),
        ("Goju Kobashi: an attachment of your own, a non-Ninja Ninja target or a Battle action outside a battle is refused; the Ninja may be Kobashi himself", KobashiTargets),
        ("Suiteiru no Oni: destroying your unbowed Personality creates that many Podlings as his Chi, dealt to the hosts named and costing that much Honor", SuiteiruCreates),
        ("Suiteiru no Oni: hosts named twice take two Podlings each, a Chi bonus counts, and Suiteiru may destroy himself", SuiteiruHostsAndChi),
        ("Suiteiru no Oni: a bowed target, the target as a host, an enemy host, no host or more hosts than Followers are refused; the action is an Open action", SuiteiruRefusals),
        ("Summon Swamp Spirits: bow its Shugenja and target your Personality (the Shugenja or another) to attach a Swamp Spirit to him -- once a turn, not with a bowed Shugenja, not against an enemy Personality", SummonSwampSpiritsCreates),
    ]);

    // ---- Published shape ------------------------------------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, params string[] ids)
        {
            var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0);
            Check(listed.OrderBy(l => l, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(i => i, StringComparer.Ordinal)), $"{wave} manifest lists exactly the wired cards");
            foreach (var id in ids)
            {
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
                Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
            }
        }
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        var kobashi = CardCatalog.Get(Kobashi);
        Check(kobashi.Type == CardType.Personality && kobashi.Name == "Goju Kobashi" && kobashi.Clan == "Spider" && kobashi.GoldCost == 7 && kobashi.Force == 4 && kobashi.Chi == 3 && kobashi.PersonalHonor == 0 && kobashi.HonorRequirement is null
            && Keywords(Kobashi) == "Consumer,Ninja,Shadowlands,Spider Clan" && kobashi.EntryHonorLoss == 1 && kobashi.Abilities!.SequenceEqual(["kobashi-consume"]) && !kobashi.CreatedOnly, "Goju Kobashi's scan");
        var suiteiru = CardCatalog.Get(Suiteiru);
        Check(suiteiru.Type == CardType.Personality && suiteiru.Name == "Suiteiru no Oni" && suiteiru.Clan == "Unaligned" && suiteiru.GoldCost == 8 && suiteiru.Force == 5 && suiteiru.Chi == 3 && suiteiru.PersonalHonor == 0
            && Keywords(Suiteiru) == "Nonhuman,Oni,Shadowlands" && suiteiru.EntryHonorLoss == 4 && suiteiru.Abilities!.SequenceEqual(["suiteiru-create"]) && !suiteiru.CreatedOnly, "Suiteiru no Oni's scan");
        var spell = CardCatalog.Get(SummonSwampSpirits);
        Check(spell.Type == CardType.Spell && spell.Name == "Summon Swamp Spirits" && spell.GoldCost == 2 && spell.Focus == 3 && Keywords(SummonSwampSpirits) == "Maho" && spell.Abilities!.SequenceEqual(["summon-swamp-spirits-create"])
            && spell.Text.StartsWith("Limited: Bow this Shugenja and target your Personality: Create a 1F Shadowlands"), "Summon Swamp Spirits' scan");
        foreach (var (id, name, keywords) in new[] { (KobashisSpawn, "Kobashi's Spawn", "Ninja,Shadowlands"), (SuiteirusPodling, "Suiteiru's Podling", "Nonhuman,Oni,Shadowlands"), (SwampSpirit, "Swamp Spirit", "Nonhuman,Shadowlands,Spirit,Swamp Spirit") })
        {
            var proxy = CardCatalog.Get(id);
            Check(proxy.Type == CardType.Follower && proxy.Name == name && proxy.CreatedOnly && proxy.Force == 1 && proxy.Chi == 0 && proxy.GoldCost == 0 && (proxy.Abilities ?? []).Length == 0 && proxy.Effect == "none" && Keywords(id) == keywords,
                $"{name}: a created-only 1F Follower printing {keywords}");
        }
        Manifest("personality-wave-76", Kobashi, Suiteiru, KobashisSpawn, SuiteirusPodling);
        Manifest("spell-wave-24", SummonSwampSpirits, SwampSpirit);
        Eq(string.Join(",", PublishedContent.PersonalityWave76.Cards.Select(c => c.Id).Order(StringComparer.Ordinal)), string.Join(",", new[] { Kobashi, Suiteiru, KobashisSpawn, SuiteirusPodling }.Order(StringComparer.Ordinal)), "personality-wave-76");
        Eq(string.Join(",", PublishedContent.SpellWave24.Cards.Select(c => c.Id).Order(StringComparer.Ordinal)), string.Join(",", new[] { SummonSwampSpirits, SwampSpirit }.Order(StringComparer.Ordinal)), "spell-wave-24");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["kobashi-consume"].Creates!).Id, KobashisSpawn, "Kobashi creates Kobashi's Spawn");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["suiteiru-create"].Creates!).Id, SuiteirusPodling, "Suiteiru creates Suiteiru's Podlings");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["summon-swamp-spirits-create"].Creates!).Id, SwampSpirit, "Summon Swamp Spirits creates a Swamp Spirit");
        Eq(string.Join(" | ", CreatedCards.Verify()), "", "every creator resolves to a scan the engine can create");
        Eq(AbilityCatalog.All["kobashi-consume"].Timing, "Battle"); Eq(AbilityCatalog.All["suiteiru-create"].Timing, "Open"); Eq(AbilityCatalog.All["summon-swamp-spirits-create"].Timing, "Limited");
    }

    // ---- Entering play --------------------------------------------------------------------------------------------------------------------------------
    // Recruits a card sitting face-up in player 0's first province, the economy scenario's own purchase flow.
    private static MatchEngine Recruit(string definitionId, out string cardId)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 9; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definitionId).GoldCost).First().SourceIds));
        return e;
    }

    private static void EntryHonorLosses()
    {
        var kobashi = Recruit(Kobashi, out _);
        Eq(Honor(kobashi, 0), 19, "Kobashi: 20 - 1 Honor entering play");
        var suiteiru = Recruit(Suiteiru, out _);
        Eq(Honor(suiteiru, 0), 16, "Suiteiru: 20 - 4 Honor entering play");
    }

    // ---- Goju Kobashi ---------------------------------------------------------------------------------------------------------------------------------
    private static (MatchEngine Engine, CardInstance Kobashi, CardInstance Ninja, CardInstance Enemy, CardInstance Attachment) KobashiRig()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var kobashi = Add(s, Kobashi, 0, Zone.Battlefield, province: 0);
        var ninja = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(ninja, "Ninja");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var attachment = Add(s, Conscripts, 1, Zone.Attached, province: 0, parent: enemy.Id);
        return (MatchEngine.FromTestState(s), kobashi, ninja, enemy, attachment);
    }

    private static bool Gone(MatchEngine e, string id) => !e.State.Cards.TryGetValue(id, out var c) || c.Zone != Zone.Attached;

    private static void KobashiConsumesAndCreates()
    {
        var (e, kobashi, ninja, enemy, attachment) = KobashiRig();
        var offer = Enabled(e, 0, "kobashi-consume", kobashi.Id);
        var attachments = offer.Slots!.Single(x => x.Slot == "attachment"); var ninjas = offer.Slots!.Single(x => x.Slot == "ninja");
        Check(attachments.CardIds.Contains(attachment.Id) && attachments.Min == 1 && attachments.Max == 1, "the enemy attachment is offered as the first, required target");
        Check(ninjas.Min == 0 && ninjas.Max == 1 && ninjas.CardIds.Contains(ninja.Id) && ninjas.CardIds.Contains(kobashi.Id) && !ninjas.CardIds.Contains(enemy.Id), "one of your Ninja (Kobashi included) is the optional second target");
        Accept(e, offer.Command! with { TargetId = null, Selections = [Pick("attachment", attachment.Id), Pick("ninja", ninja.Id)] });
        Check(Gone(e, attachment.Id), "the enemy attachment was destroyed");
        var spawn = e.State.Cards.Values.Single(c => c.DefinitionId == KobashisSpawn);
        Check(spawn.AttachedTo == ninja.Id && spawn.OwnerId == 0 && spawn.Zone == Zone.Attached, "Kobashi's Spawn is attached to the chosen Ninja");
        Eq(e.CardForceForTest(spawn.Id), 1, "the Follower has its 1F");

        // No Ninja named: only the destruction happens ("You may").
        var (e2, kobashi2, ninja2, _, attachment2) = KobashiRig();
        Accept(e2, Enabled(e2, 0, "kobashi-consume", kobashi2.Id).Command! with { TargetId = null, Selections = [Pick("attachment", attachment2.Id)] });
        Check(Gone(e2, attachment2.Id), "the attachment was destroyed all the same");
        Check(!e2.State.Cards.Values.Any(c => c.DefinitionId == KobashisSpawn), "no Follower was created without a Ninja");
        Check(ninja2.Zone == Zone.Battlefield, "the Ninja is untouched");
    }

    private static void KobashiTargets()
    {
        var (e, kobashi, ninja, enemy, attachment) = KobashiRig();
        // Kobashi himself is a Ninja and may be the host.
        Accept(e, Enabled(e, 0, "kobashi-consume", kobashi.Id).Command! with { TargetId = null, Selections = [Pick("attachment", attachment.Id), Pick("ninja", kobashi.Id)] });
        Check(e.State.Cards.Values.Single(c => c.DefinitionId == KobashisSpawn).AttachedTo == kobashi.Id, "the Spawn attached to Kobashi himself");

        var (e2, kobashi2, ninja2, enemy2, attachment2) = KobashiRig();
        var offer = Enabled(e2, 0, "kobashi-consume", kobashi2.Id);
        // Not a Ninja, and not an enemy Ninja.
        var s = e2.State; var notNinja = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var enemyNinja = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); GiveKeyword(enemyNinja, "Ninja");
        var e3 = MatchEngine.FromTestState(s);
        var offer3 = Enabled(e3, 0, "kobashi-consume", kobashi2.Id);
        var ninjas = offer3.Slots!.Single(x => x.Slot == "ninja").CardIds;
        Check(!ninjas.Contains(notNinja.Id) && !ninjas.Contains(enemyNinja.Id) && ninjas.Contains(ninja2.Id), "only your own Ninja Personalities are candidates");
        Rejection(e3, offer3.Command! with { TargetId = null, Selections = [Pick("attachment", attachment2.Id), Pick("ninja", notNinja.Id)] });
        Rejection(e3, offer3.Command! with { TargetId = null, Selections = [Pick("attachment", attachment2.Id), Pick("ninja", enemyNinja.Id)] });
        Check(!Gone(e3, attachment2.Id) == true, "a refused command destroyed nothing");
        // Two Ninja are not one Ninja; and the attachment must be an enemy's.
        Rejection(e3, offer3.Command! with { TargetId = null, Selections = [Pick("attachment", attachment2.Id), Pick("ninja", ninja2.Id, kobashi2.Id)] });
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 0;
        var k4 = Add(s4, Kobashi, 0, Zone.Battlefield, province: 0); var own = Add(s4, Conscripts, 0, Zone.Attached, province: 0, parent: k4.Id);
        var e4 = MatchEngine.FromTestState(s4);
        Check(!CanUse(e4, 0, "kobashi-consume", k4.Id), "no enemy attachment, no action: your own attachment is not a target");
        // A Battle action needs a battle.
        var s5 = Table(Phase.Action, 0); s5.PriorityPlayerId = 0;
        var k5 = Add(s5, Kobashi, 0, Zone.Home); var enemy5 = Add(s5, "training-lion-samurai", 1, Zone.Home); Add(s5, Conscripts, 1, Zone.Attached, parent: enemy5.Id);
        Check(!CanUse(MatchEngine.FromTestState(s5), 0, "kobashi-consume", k5.Id), "a Battle action is not available in the Action phase");
    }

    // ---- Suiteiru no Oni ------------------------------------------------------------------------------------------------------------------------------
    private static (MatchEngine Engine, CardInstance Suiteiru, CardInstance Victim, CardInstance A, CardInstance B, CardInstance Enemy) SuiteiruRig(Action<MatchState, CardInstance>? arrange = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var suiteiru = Add(s, Suiteiru, 0, Zone.Home);
        var victim = Add(s, "training-crane-samurai", 0, Zone.Home);   // 3 Chi
        var a = Add(s, "training-crane-veteran", 0, Zone.Home); var b = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        arrange?.Invoke(s, victim);
        s.Players[0].Honor = 20;
        return (MatchEngine.FromTestState(s), suiteiru, victim, a, b, enemy);
    }

    private static GameCommand Create(ActionOption offer, string victim, params string[] hosts) => offer.Command! with { TargetId = null, Selections = [Pick("target", victim), Pick("hosts", hosts)] };
    private static string enemyOf(MatchEngine e) => e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).Id;
    private static string[] Podlings(MatchEngine e, string host) => e.State.Cards.Values.Where(c => c.DefinitionId == SuiteirusPodling && c.AttachedTo == host).Select(c => c.Id).ToArray();

    private static void SuiteiruCreates()
    {
        var (e, suiteiru, victim, a, b, _) = SuiteiruRig();
        Eq(CardCatalog.Get("training-crane-samurai").Chi, 3, "the fixture victim has 3 Chi");
        var offer = Enabled(e, 0, "suiteiru-create", suiteiru.Id);
        var targets = offer.Slots!.Single(x => x.Slot == "target"); var hosts = offer.Slots!.Single(x => x.Slot == "hosts");
        Check(targets.CardIds.Contains(victim.Id) && targets.CardIds.Contains(suiteiru.Id) && targets.Max == 1, "your unbowed Personalities (Suiteiru included) are the targets");
        Check(hosts.Min == 1 && hosts.CardIds.Contains(a.Id) && hosts.CardIds.Contains(b.Id) && !hosts.CardIds.Contains(enemyOf(e)) && !hosts.CardIds.Contains(targets.CardIds[0]), "your Personalities other than the default target are the hosts");
        // Two distinct hosts: three Podlings dealt in turn -- two to the first, one to the second.
        Accept(e, Create(offer, victim.Id, a.Id, b.Id));
        Check(e.State.Cards[victim.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the target was destroyed as the cost");
        Eq(Podlings(e, a.Id).Length, 2, "two Podlings on the first host"); Eq(Podlings(e, b.Id).Length, 1, "one on the second");
        Check(e.State.Cards.Values.Where(c => c.DefinitionId == SuiteirusPodling).All(c => c.OwnerId == 0 && c.Zone == Zone.Attached), "all three are attached Podlings you control");
        Eq(Honor(e, 0), 17, "20 - 3 Honor: one per Follower created");
        Check(Podlings(e, a.Id).All(id => e.CardForceForTest(id) == 1), "each Podling has its 1F");
    }

    private static void SuiteiruHostsAndChi()
    {
        // Naming a host twice gives him two Followers: [A, A, B] is 2 + 1, [A, A, A] is all three on one host.
        var (e, suiteiru, victim, a, b, _) = SuiteiruRig();
        Accept(e, Create(Enabled(e, 0, "suiteiru-create", suiteiru.Id), victim.Id, b.Id, b.Id, a.Id));
        Eq(Podlings(e, b.Id).Length, 2, "the host named twice takes two"); Eq(Podlings(e, a.Id).Length, 1, "the other takes one");
        var (e2, suiteiru2, victim2, a2, _, _) = SuiteiruRig();
        Accept(e2, Create(Enabled(e2, 0, "suiteiru-create", suiteiru2.Id), victim2.Id, a2.Id, a2.Id, a2.Id));
        Eq(Podlings(e2, a2.Id).Length, 3, "three shares to one host");
        // A single host takes them all, however many the Chi.
        var (e3, suiteiru3, victim3, _, b3, _) = SuiteiruRig();
        Accept(e3, Create(Enabled(e3, 0, "suiteiru-create", suiteiru3.Id), victim3.Id, b3.Id));
        Eq(Podlings(e3, b3.Id).Length, 3, "one host named once takes every Follower");

        // His Chi as it is now: a +1 Chi bonus makes four Followers and four Honor.
        var (e4, suiteiru4, victim4, a4, b4, _) = SuiteiruRig((_, v) => ChiBonus(v, 1));
        Accept(e4, Create(Enabled(e4, 0, "suiteiru-create", suiteiru4.Id), victim4.Id, a4.Id, b4.Id));
        Eq(e4.State.Cards.Values.Count(c => c.DefinitionId == SuiteirusPodling), 4, "four Followers for 4 Chi");
        Eq(Honor(e4, 0), 16, "four Honor lost");
        // A Chi penalty counts too.
        var (e5, suiteiru5, victim5, a5, _, _) = SuiteiruRig((_, v) => ChiBonus(v, -1));
        Accept(e5, Create(Enabled(e5, 0, "suiteiru-create", suiteiru5.Id), victim5.Id, a5.Id));
        Eq(e5.State.Cards.Values.Count(c => c.DefinitionId == SuiteirusPodling), 2, "two Followers for 2 Chi");
        Eq(Honor(e5, 0), 18, "two Honor lost");

        // Suiteiru may destroy himself (he is your unbowed Personality): his Chi (3) is the count; the Followers go to the others.
        var (e6, suiteiru6, _, a6, b6, _) = SuiteiruRig();
        Accept(e6, Create(Enabled(e6, 0, "suiteiru-create", suiteiru6.Id), suiteiru6.Id, a6.Id, b6.Id));
        Check(e6.State.Cards[suiteiru6.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "Suiteiru destroyed himself");
        Eq(Podlings(e6, a6.Id).Length + Podlings(e6, b6.Id).Length, 3, "three Podlings created for his Chi");
        Eq(Honor(e6, 0), 17, "three Honor lost");
    }

    private static void SuiteiruRefusals()
    {
        var (e, suiteiru, victim, a, b, enemy) = SuiteiruRig();
        var offer = Enabled(e, 0, "suiteiru-create", suiteiru.Id);
        Rejection(e, Create(offer, victim.Id, victim.Id));                       // the destroyed target cannot host
        Rejection(e, Create(offer, victim.Id, enemy.Id));                        // an enemy Personality is not one of yours
        Rejection(e, Create(offer, victim.Id));                                  // one or more hosts
        Rejection(e, Create(offer, victim.Id, a.Id, a.Id, b.Id, b.Id));          // four hosts for three Followers
        Rejection(e, Create(offer, enemy.Id, a.Id));                             // an enemy Personality is not your target
        Check(e.State.Cards[victim.Id].Zone == Zone.Home && !e.State.Cards.Values.Any(c => c.DefinitionId == SuiteirusPodling) && Honor(e, 0) == 20, "nothing happened");
        // Three hosts naming each Personality once each also works when each is a real host (3 Followers).
        Accept(e, Create(offer, victim.Id, a.Id, b.Id, suiteiru.Id));
        Eq(e.State.Cards.Values.Count(c => c.DefinitionId == SuiteirusPodling), 3, "one Podling to each of three hosts, Suiteiru included");
        // A bowed Personality is not an unbowed one.
        var (e2, suiteiru2, victim2, a2, _, _) = SuiteiruRig((s, v) => v.Bowed = true);
        Rejection(e2, Create(Enabled(e2, 0, "suiteiru-create", suiteiru2.Id), victim2.Id, a2.Id));
        Check(!Enabled(e2, 0, "suiteiru-create", suiteiru2.Id).Slots!.Single(x => x.Slot == "target").CardIds.Contains(victim2.Id), "a bowed Personality is not offered as the target");
        // The Follower must be one the host could carry: a host that refuses Followers refuses the action.
        var (e3, suiteiru3, victim3, a3, _, _) = SuiteiruRig();
        var refusing = Add(e3.State, "oracle-l5r-9839", 0, Zone.Home);   // Natsumi: "Will not attach Followers or Items."
        var e4 = MatchEngine.FromTestState(e3.State);
        Rejection(e4, Create(Enabled(e4, 0, "suiteiru-create", suiteiru3.Id), victim3.Id, refusing.Id));
        // Not in the Action phase's Battle timing: an Open action is not offered to the opponent out of turn.
        Check(!CanUse(e, 1, "suiteiru-create", suiteiru.Id), "the opponent may not use your Suiteiru");
    }

    // ---- Summon Swamp Spirits -------------------------------------------------------------------------------------------------------------------------
    private static void SummonSwampSpiritsCreates()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        var spell = Add(s, SummonSwampSpirits, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var options = Options(e, 0, "summon-swamp-spirits-create", spell.Id).Where(o => o.Enabled).ToArray();
        Check(options.Any(o => o.TargetIds!.Contains(other.Id) && o.TargetIds.Contains(shugenja.Id)) && !options.Any(o => o.TargetIds!.Contains(enemy.Id)), "your Personalities are the targets, an enemy's is not");
        Accept(e, Offer(e, 0, "summon-swamp-spirits-create", spell.Id, other.Id));
        Check(e.State.Cards[shugenja.Id].Bowed, "its Shugenja bowed as the cost");
        var spirit = e.State.Cards.Values.Single(c => c.DefinitionId == SwampSpirit);
        Check(spirit.AttachedTo == other.Id && spirit.OwnerId == 0 && spirit.Zone == Zone.Attached, "the Swamp Spirit is attached to the target, not to the Shugenja");
        Eq(e.CardForceForTest(spirit.Id), 1, "the Swamp Spirit has its 1F");
        // A Limited action: once a turn (and the Shugenja is bowed now anyway).
        Check(!CanUse(e, 0, "summon-swamp-spirits-create", spell.Id), "no second use this turn, and not with a bowed Shugenja");

        // Attached to himself; an enemy target is refused; a bowed Shugenja cannot pay the cost.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0; s2.Players[0].TurnsTaken = 3; s2.Players[1].TurnsTaken = 3;
        var sh2 = Add(s2, "training-shugenja", 0, Zone.Home); var sp2 = Add(s2, SummonSwampSpirits, 0, Zone.Attached, province: 0, parent: sh2.Id); var foe2 = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var own = Offer(e2, 0, "summon-swamp-spirits-create", sp2.Id, sh2.Id);
        Rejection(e2, own with { TargetId = foe2.Id });
        Accept(e2, own);
        Check(e2.State.Cards.Values.Single(c => c.DefinitionId == SwampSpirit).AttachedTo == sh2.Id, "or to the Shugenja himself");
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0; s3.Players[0].TurnsTaken = 3; s3.Players[1].TurnsTaken = 3;
        var sh3 = Add(s3, "training-shugenja", 0, Zone.Home, bowed: true); var sp3 = Add(s3, SummonSwampSpirits, 0, Zone.Attached, province: 0, parent: sh3.Id);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "summon-swamp-spirits-create", sp3.Id), "a bowed Shugenja cannot be bowed as the cost");
    }
}
