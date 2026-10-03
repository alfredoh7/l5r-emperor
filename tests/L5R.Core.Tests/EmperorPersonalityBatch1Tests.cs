using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Personality pass (issue #477), batch 1: the 11 of the batch's 13 candidate Spider/Shadowlands Personalities wired in personality-wave-68, driven through QueryActions and the
// commands it offers on explicit positions of the economy table (player 0 the Crane, player 1 the Lion). Goju Kobashi and Suiteiru no Oni stay unwired and unpublished
// (tools/content/ee_batch1_blockers.json).
internal static class EmperorPersonalityBatch1Tests
{
    private const string Niiro = "oracle-l5r-1363", Aya = "oracle-l5r-1704", Gyoken = "oracle-l5r-1720", Matsuda = "oracle-l5r-1744", Negishi = "oracle-l5r-1751", Misaki = "oracle-l5r-1749",
        Ekichu = "oracle-l5r-2286", Oyoto = "oracle-l5r-2855", Onchi = "oracle-l5r-5590", Nosloc = "oracle-l5r-5626", Ugulu = "oracle-l5r-8929";
    private static readonly string[] Wired = [Niiro, Aya, Gyoken, Matsuda, Negishi, Misaki, Ekichu, Oyoto, Onchi, Nosloc, Ugulu];
    // Goju Kobashi (2853) and Suiteiru no Oni (7618) were unwired here until their Proxy scans arrived; issue #492 (proxy group X) wired and published them in personality-wave-76 (EmperorProxyXTests).
    private static readonly string[] Unwired = [];

    public static void Run(TestRunner runner) => runner.Run("emperor-personality-batch1", [
        ("Published shape: personality-wave-68 publishes exactly the 11 wired cards with their printed stats, keywords, Effect and ability ids, ships their art and lists them in its manifest; Kobashi and Suiteiru are published later, in personality-wave-76 (issue #492)", PublishedShape),
        ("Entry Honor loss: each Personality loses exactly the printed amount when it enters play, and none of the six bare ones has an ability of its own", EntryHonorLosses),
        ("Daigotsu Aya: after entering play her controller may put any Dark Virtue from his discard pile into his hand (nothing else, nobody else's), or pass", AyaEntryRetrieval),
        ("Daigotsu Aya: Open gives her Cavalry, Duelist or Tactician for the turn -- one choice a turn, and it wears off", AyaKeywordChoice),
        ("Daigotsu Negishi: Battle/Open straightens a target Personality of yours (bowed or not), never an enemy one", NegishiStraighten),
        ("Daigotsu Misaki: a Follower attaching to her pays 2 less Gold (from a hand, to a minimum of 0), a Follower attaching to another Personality does not", MisakiDiscount),
        ("Daigotsu Misaki: bows her performing Follower for a Melee Attack whose strength is that Follower's Force -- destroys a target with Force at most that, a stronger one is a legal target that survives; a bowed Follower cannot perform", MisakiAttack),
        ("Ekichu no Oni: three Battle actions -- bow an enemy card, destroy an enemy card without attachments, move an enemy Personality home; each with its printed target restriction", EkichuActions),
        ("Ninube Onchi: Ninja Open removes all of a target Personality's abilities until the turn ends -- his own printed actions and the next turn's are unaffected, as is Onchi", OnchiRemoval),
        ("Ugulu no Oni: negates a Human Personality's movement to Ugulu's battlefield -- not a Nonhuman's, not a movement to another battlefield, not while Ugulu is at home", UguluNegation),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId);
    private static MatchState Battle(int attacker = 0) { var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0; return s; }
    private static bool HasGranted(MatchEngine e, string cardId, string keyword) => e.State.Cards[cardId].GrantedKeywords.Any(g => g.Keyword == keyword);

    // Player 0 recruits the card from his first province with plenty of Gold and 20 Honor.
    private static (MatchEngine Engine, string CardId) Recruit(string definitionId, Action<MatchState>? tweak = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 9; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definitionId).GoldCost).First().SourceIds));
        return (e, cardId);
    }

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Eq(listed.Count(l => l.Length > 0), ids.Length, $"{wave} lists exactly the wired cards");
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string name, string clan, int force, int chi, int cost, string keywords, string[] abilities, int? entryLoss, string effect = "none")
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Personality && !c.CreatedOnly && !c.ScenarioOnly && c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && c.PersonalHonor == 0
                    && c.HonorRequirement is null && Keywords(id) == keywords && c.Effect == effect && c.EntryHonorLoss == entryLoss,
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} keywords={Keywords(id)} effect={c.Effect} entry={c.EntryHonorLoss}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
        }
        Stats(Niiro, "Chuda Niiro", "Unaligned", 4, 3, 4, "Shadowlands,Shugenja", [], 2);
        Stats(Aya, "Daigotsu Aya", "Spider", 5, 4, 9, "Dark Perfection,Paragon,Samurai,Shadowlands,Soul of Daigotsu Setsuko Experienced,Spider Clan,Unique", ["aya-entry-dark-virtue", "aya-cavalry", "aya-duelist", "aya-tactician"], 2);
        Stats(Gyoken, "Daigotsu Gyoken", "Spider", 4, 3, 6, "Samurai,Shadowlands,Spider Clan", [], 1);
        Stats(Matsuda, "Daigotsu Matsuda", "Spider", 5, 4, 7, "Obsidian Claw,Paragon,Samurai,Shadowlands,Soul of Daigotsu Kurai,Spider Clan", [], 1);
        Stats(Negishi, "Daigotsu Negishi", "Spider", 5, 2, 7, "Samurai,Shadowlands,Soul of Daigotsu Arima,Spider Clan", ["negishi-straighten"], 1);
        Stats(Misaki, "Daigotsu Misaki", "Spider", 6, 4, 10, "Commander,Conqueror,Samurai,Shadowlands,Spider Clan", ["misaki-follower-attack"], null, "misaki-follower-discount");
        Stats(Ekichu, "Ekichu no Oni", "Unaligned", 10, 4, 12, "Nonhuman,Oni,Shadowlands,Unique", ["tsuji-bow", "kuronada-destroy", "mihato-move"], 8);
        Stats(Oyoto, "Goju Oyoto", "Spider", /* issue #492, ruling R5: Ninja is a keyword, not a Clan */ 1, 3, 4, "Ninja,Shadowlands,Shugenja,Soul of Goju Utsuei,Spider Clan", [], 3);
        Stats(Onchi, "Ninube Onchi", "Spider", 4, 3, 8, "Cavalry,Ninja,Shadowlands,Shugenja,Spider Clan", ["onchi-remove-abilities"], 2);
        Stats(Nosloc, "Nosloc no Oni", "Unaligned", 6, 4, 6, "Cavalry,Nonhuman,Oni,Shadowlands", [], 4);
        Stats(Ugulu, "Ugulu no Oni", "Unaligned", 4, 2, 4, "Nonhuman,Oni,Shadowlands", [], 5, "ugulu-negate-human-movement");
        Manifest(root, "personality-wave-68", Wired);
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is not in the catalogue: an unwired card is never published");
        Check(AbilityCatalog.All["onchi-remove-abilities"].ActionKeywords!.SequenceEqual(["Ninja"]), "Onchi's action is a Ninja action");
    }

    private static void EntryHonorLosses()
    {
        foreach (var (id, loss) in new[] { (Niiro, 2), (Aya, 2), (Gyoken, 1), (Matsuda, 1), (Negishi, 1), (Ekichu, 8), (Oyoto, 3), (Onchi, 2), (Nosloc, 4), (Ugulu, 5) })
        {
            var (e, _) = Recruit(id);
            Eq(Honor(e, 0), 20 - loss, $"{CardCatalog.Get(id).Name} lost {loss} Honor entering play");
        }
        // Misaki prints no entry loss.
        var (m, _) = Recruit(Misaki);
        Eq(Honor(m, 0), 20, "Misaki costs no Honor");
        foreach (var id in new[] { Niiro, Gyoken, Matsuda, Oyoto, Nosloc, Ugulu }) Check(CardCatalog.Get(id).Abilities!.Length == 0, $"{CardCatalog.Get(id).Name} has no ability of his own");
    }

    private static void AyaEntryRetrieval()
    {
        string dv = "", virtue2 = "", strategy = "", enemyVirtue = "";
        var (e, ayaId) = Recruit(Aya, s =>
        {
            dv = Add(s, "training-dark-virtue-open", 0, Zone.FateDiscard).Id;   // a Dark Virtue with no Battle action: still a Dark Virtue
            virtue2 = Add(s, "training-dark-virtue", 0, Zone.FateDiscard).Id;
            strategy = Add(s, "training-move-battle-strategy", 0, Zone.FateDiscard).Id; // not a Dark Virtue
            enemyVirtue = Add(s, "training-dark-virtue", 1, Zone.FateDiscard).Id;       // another player's discard pile
        });
        Eq(Honor(e, 0), 18, "the loss is mandatory");
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entering-play window is open");
        var offers = Options(e, 0, "aya-entry-dark-virtue", ayaId).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(offers.Contains(dv) && offers.Contains(virtue2), "both of his own Dark Virtues are offered");
        Check(!offers.Contains(strategy) && !offers.Contains(enemyVirtue), "a non-Dark-Virtue and an opponent's Dark Virtue are not");
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, "aya-entry-dark-virtue", ayaId, dv));
        Eq(e.State.Cards[dv].Zone, Zone.Hand, "the Dark Virtue is in his hand");
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "one card gained");
        Eq(e.State.Cards[virtue2].Zone, Zone.FateDiscard, "only one was taken");
        Eq(Honor(e, 0), 18, "no further Honor change");

        // "You may": declining leaves the discard pile alone.
        var (e2, aya2) = Recruit(Aya, s => Add(s, "training-dark-virtue", 0, Zone.FateDiscard));
        Eq(e2.State.PendingChoice?.Type, "entryReaction", "window open");
        for (var guard = 0; guard < 6 && e2.State.PendingChoice is not null; guard++) Accept(e2, new GameCommand("declineReaction", e2.State.PriorityPlayerId, e2.Version));
        Check(e2.State.Cards.Values.Any(c => c.OwnerId == 0 && c.Zone == Zone.FateDiscard && CardCatalog.Get(c.DefinitionId).Name == "Training Dark Virtue"), "declining took nothing");
        Check(aya2.Length > 0);

        // Nothing to take: no target is offered.
        var (e3, aya3) = Recruit(Aya);
        Check(!CanUse(e3, 0, "aya-entry-dark-virtue", aya3), "with no Dark Virtue in the discard pile the Reaction has no target");
    }

    private static void AyaKeywordChoice()
    {
        var s = Table(Phase.Action, 0);
        var aya = Add(s, Aya, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        foreach (var id in new[] { "aya-cavalry", "aya-duelist", "aya-tactician" }) Check(CanUse(e, 0, id, aya.Id), $"{id} is offered");
        Accept(e, Enabled(e, 0, "aya-duelist", aya.Id).Command!);
        Check(HasGranted(e, aya.Id, "Duelist"), "Aya has Duelist");
        Check(!HasGranted(e, aya.Id, "Cavalry") && !HasGranted(e, aya.Id, "Tactician"), "only the chosen keyword");
        Eq(e.State.Cards[aya.Id].GrantedKeywords.Single(g => g.Keyword == "Duelist").Duration, "Turn", "for the turn");
        foreach (var id in new[] { "aya-cavalry", "aya-duelist", "aya-tactician" }) Check(!CanUse(e, 0, id, aya.Id), $"{id} shares the once-a-turn use");
        // Each keyword is its own choice.
        foreach (var id in new[] { "aya-cavalry", "aya-tactician" })
        {
            var s2 = Table(Phase.Action, 0);
            var aya2 = Add(s2, Aya, 0, Zone.Home);
            var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, Enabled(e2, 0, id, aya2.Id).Command!);
            Check(HasGranted(e2, aya2.Id, id == "aya-cavalry" ? "Cavalry" : "Tactician"), id);
        }
    }

    private static void NegishiStraighten()
    {
        foreach (var phase in new[] { Phase.Battle, Phase.Action })
        {
            var s = Table(phase, 0); s.PriorityPlayerId = 0;
            var negishi = phase == Phase.Battle ? Bf(s, Negishi, 0) : Add(s, Negishi, 0, Zone.Home);
            var ally = phase == Phase.Battle ? Bf(s, "training-crane-samurai", 0, bowed: true) : Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
            var enemy = phase == Phase.Battle ? Bf(s, "training-lion-samurai", 1, bowed: true) : Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
            var e = MatchEngine.FromTestState(s);
            var targets = Enabled(e, 0, "negishi-straighten", negishi.Id).TargetIds!;
            Check(targets.Contains(ally.Id) && !targets.Contains(enemy.Id), $"{phase}: only his own Personalities are offered");
            Accept(e, Offer(e, 0, "negishi-straighten", negishi.Id, ally.Id));
            Check(!e.State.Cards[ally.Id].Bowed, $"{phase}: the ally was straightened");
            Check(e.State.Cards[enemy.Id].Bowed, $"{phase}: the enemy was not");
        }
    }

    private static void MisakiDiscount()
    {
        var s = Table(Phase.Action, 0);
        for (var i = 0; i < 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var follower = Add(s, "training-cavalry-follower", 0, Zone.Hand);   // Gold Cost 2, Force 2, no Honor Requirement
        var misaki = Add(s, Misaki, 0, Zone.Home);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(0).First(a => a.Type == "equip" && a.CardId == follower.Id);
        Check(option.Enabled, option.Reason ?? "");
        Check(option.TargetGold is not null, "the price now depends on the wielder");
        Eq(option.TargetGold![misaki.Id].Cost, 0, "0 Gold for Misaki (2 - 2)");
        Eq(option.TargetGold[other.Id].Cost, 2, "2 Gold for any other Personality");
        Check(option.TargetGold[misaki.Id].Basis.Contains("− 2"), option.TargetGold[misaki.Id].Basis);
        // Paying nothing bows nothing.
        var free = option.Command! with { TargetId = misaki.Id, SourceIds = [] };
        Accept(e, free);
        Eq(e.State.Cards[follower.Id].AttachedTo, misaki.Id, "the Follower attached to Misaki for free");
        Check(e.State.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding).All(c => !c.Bowed), "no Gold source was bowed");

        // A pricier Follower: 2 less, not free.
        var expensive = CardCatalog.Cards.Values.First(c => c.Type == CardType.Follower && !c.ScenarioOnly && !c.CreatedOnly && c.GoldCost >= 4 && c.HonorRequirement is null or 0 && c.AttachRequiresKeyword is null);
        var s2 = Table(Phase.Action, 0);
        for (var i = 0; i < 4; i++) Add(s2, "training-marketplace", 0, Zone.Home);
        var f2 = Add(s2, expensive.Id, 0, Zone.Hand);
        var misaki2 = Add(s2, Misaki, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var option2 = e2.QueryActions(0).First(a => a.Type == "equip" && a.CardId == f2.Id);
        Eq(option2.TargetGold![misaki2.Id].Cost, expensive.GoldCost - 2, "two less than the printed cost");

        // An Item is not a Follower: no discount.
        var s3 = Table(Phase.Action, 0);
        for (var i = 0; i < 2; i++) Add(s3, "training-marketplace", 0, Zone.Home);
        var item = Add(s3, "training-item", 0, Zone.Hand);
        var misaki3 = Add(s3, Misaki, 0, Zone.Home);
        var plain3 = Add(s3, "training-crane-samurai", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        var option3 = e3.QueryActions(0).First(a => a.Type == "equip" && a.CardId == item.Id);
        Check(option3.TargetGold is null || option3.TargetGold[misaki3.Id].Cost == option3.TargetGold[plain3.Id].Cost, "an Item pays the same on Misaki");
    }

    private static void MisakiAttack()
    {
        const string Squire = "training-cavalry-follower"; // Force 2
        var s = Battle();
        var misaki = Bf(s, Misaki, 0);
        var follower = Add(s, Squire, 0, Zone.Attached, province: 0, parent: misaki.Id);
        var weak = Bf(s, "training-lion-samurai", 1);   // Force 4, lowered to 2
        Force(weak, -2);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "misaki-follower-attack", misaki.Id, follower.Id);
        Check(option.TargetIds!.Contains(weak.Id), "the enemy Personality is a target");
        Accept(e, Offer(e, 0, "misaki-follower-attack", misaki.Id, weak.Id, follower.Id));
        Check(e.State.Cards[follower.Id].Bowed, "the performing Follower was bowed as the cost");
        Check(!e.State.Cards[misaki.Id].Bowed, "Misaki himself was not");
        Eq(e.State.Cards[weak.Id].Zone, Zone.Dead, "Force <= the Follower's Force: destroyed");

        // A stronger target is still a legal target (core rules) but survives: the strength is the Follower's Force (2), not Misaki's (6).
        var s2 = Battle();
        var misaki2 = Bf(s2, Misaki, 0);
        var follower2 = Add(s2, Squire, 0, Zone.Attached, province: 0, parent: misaki2.Id);
        var strong = Bf(s2, "training-lion-samurai", 1);   // Force 4
        var e2 = MatchEngine.FromTestState(s2);
        Check(Batch336Support.Options(e2, 0, "misaki-follower-attack", misaki2.Id, follower2.Id).FirstOrDefault()?.TargetIds?.Contains(strong.Id) ?? false, "a target with more Force than the Follower is still offered");
        Accept(e2, Offer(e2, 0, "misaki-follower-attack", misaki2.Id, strong.Id, follower2.Id));
        Check(e2.State.Cards[follower2.Id].Bowed, "the cost is still paid");
        Eq(e2.State.Cards[strong.Id].Zone, Zone.Battlefield, "but the Force-4 target survives the strength-2 Attack (Misaki's own Force 6 is not used)");

        // The strength follows the Follower's live Force.
        var s3 = Battle();
        var misaki3 = Bf(s3, Misaki, 0);
        var follower3 = Add(s3, Squire, 0, Zone.Attached, province: 0, parent: misaki3.Id);
        Force(follower3, 1);                       // Force 3
        var target3 = Bf(s3, "training-lion-samurai", 1);
        Force(target3, -1);                        // Force 3
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "misaki-follower-attack", misaki3.Id, target3.Id, follower3.Id));
        Eq(e3.State.Cards[target3.Id].Zone, Zone.Dead, "a Follower buffed to Force 3 strikes a Force 3 target");

        // A bowed Follower cannot perform.
        var s4 = Battle();
        var misaki4 = Bf(s4, Misaki, 0);
        Add(s4, Squire, 0, Zone.Attached, bowed: true, province: 0, parent: misaki4.Id);
        Bf(s4, "training-lion-samurai", 1);
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "misaki-follower-attack", misaki4.Id), "no unbowed Follower, no Attack");
        // No Follower at all.
        var s5 = Battle();
        var misaki5 = Bf(s5, Misaki, 0);
        Bf(s5, "training-lion-samurai", 1);
        Check(!CanUse(MatchEngine.FromTestState(s5), 0, "misaki-follower-attack", misaki5.Id), "no Follower, no Attack");
    }

    private static void EkichuActions()
    {
        // Bow an enemy card (with or without attachments).
        var s = Battle();
        var ekichu = Bf(s, Ekichu, 0);
        var armed = Bf(s, "training-lion-samurai", 1);
        Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: armed.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "tsuji-bow", ekichu.Id, armed.Id));
        Check(e.State.Cards[armed.Id].Bowed, "an enemy card with attachments was bowed");

        // Destroy an enemy card without attachments -- one with attachments is not offered.
        var s2 = Battle();
        var ekichu2 = Bf(s2, Ekichu, 0);
        var armed2 = Bf(s2, "training-lion-samurai", 1);
        Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: armed2.Id);
        var bare = Bf(s2, "training-lion-samurai", 1);
        var e2 = MatchEngine.FromTestState(s2);
        var targets = Enabled(e2, 0, "kuronada-destroy", ekichu2.Id).TargetIds!;
        Check(targets.Contains(bare.Id) && !targets.Contains(armed2.Id), "only a card without attachments is offered");
        Accept(e2, Offer(e2, 0, "kuronada-destroy", ekichu2.Id, bare.Id));
        Eq(e2.State.Cards[bare.Id].Zone, Zone.Dead, "destroyed");
        Eq(e2.State.Cards[armed2.Id].Zone, Zone.Battlefield, "the armed one is untouched");

        // Move an enemy Personality home -- not one of his own.
        var s3 = Battle();
        var ekichu3 = Bf(s3, Ekichu, 0);
        var ally = Bf(s3, "training-crane-samurai", 0);
        var enemy = Bf(s3, "training-lion-samurai", 1);
        var e3 = MatchEngine.FromTestState(s3);
        var moveTargets = Enabled(e3, 0, "mihato-move", ekichu3.Id).TargetIds!;
        Check(moveTargets.Contains(enemy.Id) && !moveTargets.Contains(ally.Id), "only an enemy Personality is offered");
        Accept(e3, Offer(e3, 0, "mihato-move", ekichu3.Id, enemy.Id));
        Eq(e3.State.Cards[enemy.Id].Zone, Zone.Home, "moved home");

        // Each is its own action: after the bow the destroy and the move are still available, the bow is not.
        var s4 = Battle();
        var ekichu4 = Bf(s4, Ekichu, 0);
        var t1 = Bf(s4, "training-lion-samurai", 1);
        var t2 = Bf(s4, "training-lion-samurai", 1);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "tsuji-bow", ekichu4.Id, t1.Id));
        Accept(e4, new GameCommand("pass", 1, e4.Version));
        Check(!CanUse(e4, 0, "tsuji-bow", ekichu4.Id), "the bow was used this turn");
        Check(CanUse(e4, 0, "kuronada-destroy", ekichu4.Id) && CanUse(e4, 0, "mihato-move", ekichu4.Id), "the other two actions are separate");
        Check(t2.Id.Length > 0);
    }

    private static void OnchiRemoval()
    {
        const string Donor = "training-skin-donor"; // Open: "Training: bow a target Personality" (training-bow-any) among its actions
        var s = Table(Phase.Action, 0);
        var onchi = Add(s, Onchi, 0, Zone.Home);
        var donor = Add(s, Donor, 1, Zone.Home);
        var ownDonor = Add(s, Donor, 0, Zone.Home);
        var victim = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var opponentTurn = Edit(e, st => st.PriorityPlayerId = 1);
        Check(CanUse(opponentTurn, 1, "training-bow-any", donor.Id), "before: the enemy Personality has a usable ability");
        var targets = Enabled(e, 0, "onchi-remove-abilities", onchi.Id).TargetIds!;
        Check(targets.Contains(donor.Id) && targets.Contains(ownDonor.Id), "any Personality is a target");
        Accept(e, Offer(e, 0, "onchi-remove-abilities", onchi.Id, donor.Id));
        Check(!CanUse(e, 1, "training-bow-any", donor.Id), "after: none of his abilities may be used");
        var back = Edit(e, st => st.PriorityPlayerId = 0);
        Check(CanUse(back, 0, "training-bow-any", ownDonor.Id), "another Personality of the same kind is unaffected");
        var rejection = Rejection(e, Use(e, 1, "training-bow-any", donor.Id, victim.Id));
        Check(rejection.Contains("may not be used"), rejection);

        // It lasts until the turn ends only.
        var later = Edit(e, st => { st.TurnNumber += 1; st.ActivePlayerId = 1; st.PriorityPlayerId = 1; });
        Check(CanUse(later, 1, "training-bow-any", donor.Id), "next turn his abilities are back");

        // Onchi's own Ninja Open: a Ninja action only from a Ninja (he is one), once a turn.
        Check(!CanUse(back, 0, "onchi-remove-abilities", onchi.Id), "used once this turn");
    }

    private static void UguluNegation()
    {
        // A Human Personality of either player moving to Ugulu's battlefield is negated; the movement source is a hand Strategy that moves a Personality from home.
        var s = Battle();
        var ugulu = Bf(s, Ugulu, 1);
        var mover = Add(s, "training-crane-samurai", 0, Zone.Home);
        var strategy = Add(s, "training-move-battle-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-move-to-battle", strategy.Id, mover.Id));
        Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "the Human Personality stayed home: his movement was negated");
        Check(e.State.Cards[ugulu.Id].Zone == Zone.Battlefield);

        // A Nonhuman Personality is not affected.
        var s2 = Battle();
        Bf(s2, Ugulu, 1);
        var beast = Add(s2, "training-crane-samurai", 0, Zone.Home);
        beast.GrantedKeywords.Add(new("Nonhuman", "test-fixture", GrantDuration.Permanent));
        var strategy2 = Add(s2, "training-move-battle-strategy", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "training-move-to-battle", strategy2.Id, beast.Id));
        Eq(e2.State.Cards[beast.Id].Zone, Zone.Battlefield, "a Nonhuman moved to Ugulu's battlefield");

        // Another battlefield: Ugulu at province 1 does not guard province 0.
        var s3 = Battle();
        var elsewhere = Add(s3, Ugulu, 1, Zone.Battlefield, province: 1);
        var mover3 = Add(s3, "training-crane-samurai", 0, Zone.Home);
        var strategy3 = Add(s3, "training-move-battle-strategy", 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "training-move-to-battle", strategy3.Id, mover3.Id));
        Eq(e3.State.Cards[mover3.Id].Zone, Zone.Battlefield, "Ugulu at another battlefield negates nothing here");
        Check(elsewhere.Id.Length > 0);

        // Ugulu at home has no battlefield.
        var s4 = Battle();
        Add(s4, Ugulu, 1, Zone.Home);
        var mover4 = Add(s4, "training-crane-samurai", 0, Zone.Home);
        var strategy4 = Add(s4, "training-move-battle-strategy", 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "training-move-to-battle", strategy4.Id, mover4.Id));
        Eq(e4.State.Cards[mover4.Id].Zone, Zone.Battlefield, "Ugulu at home negates nothing");

        // His controller's own Human is negated too, and a Human already at that battlefield is simply not moving.
        var s5 = Battle();
        Bf(s5, Ugulu, 0);
        var mover5 = Add(s5, "training-crane-samurai", 0, Zone.Home);
        var strategy5 = Add(s5, "training-move-battle-strategy", 0, Zone.Hand);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 0, "training-move-to-battle", strategy5.Id, mover5.Id));
        Eq(e5.State.Cards[mover5.Id].Zone, Zone.Home, "the controller's own Human is negated as well");
    }
}
