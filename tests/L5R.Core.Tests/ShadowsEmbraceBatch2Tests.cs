using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// The Shadow's Embrace pass (issue #476), batch 2: the 19 Personalities of personality-wave-75, driven through QueryActions and the commands it offers. Every scenario
// is an explicit position on the economy table (player 0 the Crane, player 1 the Lion). The other four candidates (Kitsune Kichi, Kuni Renyu, Shosuro Keirei, Zlkyt)
// stay unwired and unpublished (see BLOCKERS in tools/content/build_card_inventory.py).
internal static class ShadowsEmbraceBatch2Tests
{
    private const string Sorano = "oracle-l5r-9826", Yoyugi = "oracle-l5r-9817", Longtooth = "oracle-l5r-9860", Mrika = "oracle-l5r-9852", Rukia = "oracle-l5r-9830",
        Morio = "oracle-l5r-9861", Natsumi = "oracle-l5r-9839", Niiro = "oracle-l5r-9856", Quelsa = "oracle-l5r-9857", Kudome = "oracle-l5r-9840", Shirou = "oracle-l5r-9841",
        JongHo = "oracle-l5r-9862", MinHee = "oracle-l5r-9863", Nobu = "oracle-l5r-9846", Ryoken = "oracle-l5r-9847", Shugorei = "oracle-l5r-9820", Yamazaki = "oracle-l5r-9853",
        Ikke = "oracle-l5r-9809", Kanahashi = "oracle-l5r-9833";
    private const string SoranoBase = "oracle-l5r-4395", LionAncestor = "oracle-l5r-9785", Temple = "oracle-l5r-8094", Gates = "oracle-l5r-10707";
    private const string Lion = "training-lion-samurai", Crane = "training-crane-samurai", Follower = "training-follower", Item = "training-item", Weapon = "training-weapon",
        Spell = "training-spell", Shugenja = "training-shugenja";
    private static readonly string[] Wired = [Sorano, Yoyugi, Longtooth, Mrika, Rukia, Morio, Natsumi, Niiro, Quelsa, Kudome, Shirou, JongHo, MinHee, Nobu, Ryoken, Shugorei, Yamazaki, Ikke, Kanahashi];
    // Issue #501: Zlkyt (oracle-l5r-9828) is wired and published in personality-wave-95 (Mendon's item 19), so it left this list.
    // Issue #503: Shosuro Keirei - exp (oracle-l5r-9845) is now wired and published in personality-wave-100 (EmperorP2Keirei.cs).
    private static readonly string[] Unwired = ["oracle-l5r-9829", "oracle-l5r-9808"];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    // Player abilities (Equip (Battle), Tactical Advantage) are enabled only with a published Stronghold: Shinden Asahina, which has no Effect of its own.
    private static MatchState PublishedTable(Phase phase = Phase.Battle, int actor = 1)
    {
        var s = Table(phase, actor);
        foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-6855";
        return s;
    }
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static MatchEngine Priority(MatchEngine e, int player) => Edit(e, st => { st.PriorityPlayerId = player; st.ConsecutivePasses = 0; });
    private static int Force(MatchEngine e, CardInstance c) => e.CardForceForTest(c.Id);
    private static CardInstance Card(MatchEngine e, CardInstance c) => e.State.Cards[c.Id];

    // The offered, enabled option of an ability whose command matches (a performer, a card), for abilities whose label does not name the performer.
    private static ActionOption Option(MatchEngine e, int player, string ability, Func<GameCommand, bool>? matches = null)
    {
        var options = e.QueryActions(player).Where(o => o.Command?.AbilityId == ability).ToArray();
        var found = options.FirstOrDefault(o => o.Enabled && (matches is null || matches(o.Command!)));
        Check(found is not null, $"{ability} was not offered enabled to player {player}: " + string.Join("; ", e.QueryActions(player).Where(o => o.Label.Length > 0 && o.Type == "useAbility").Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
        return found!;
    }

    private static void Duel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingDuel is { } duel && guard < 8; guard++)
            Accept(e, duel.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", duel.TurnPlayerId, e.Version));
    }

    public static void Run(TestRunner runner) => runner.Run("shadows-embrace-batch-2", [
        ("Published shape: exactly the 19 wired Personalities are published in personality-wave-75, with their catalogue stats, Effects, ability ids, art and manifest; the four blocked ones are not", PublishedShape),
        ("Kitsu Sorano - exp: overlaying her base printing opens her entering-play Reaction, which creates two Lion Ancestors at home", SoranoOverlayCreates),
        ("Kitsuki Yoyugi: a non-Unique or Unicorn Clan target is locked for free, a Unique non-Unicorn one costs the bow; the target's printed abilities may not be used this turn", YoyugiLock),
        ("Longtooth: will not attach cards (Equip never offers him); destroys an enemy Follower or Personality without Followers", LongtoothRefusesAndDestroys),
        ("M'rika: her Loyal restriction is waived under The Forgotten Temple only; entering play gains her controller 2 Honor", MrikaLoyalAndHonor),
        ("Moshi Rukia: returns an opposed attachment to its owner's hand and grants an additional Battle action; none is opposed without enemy units", RukiaReturn),
        ("Moto Morio: any of his controller's Followers (even bowed) performs a Melee Attack with strength equal to its Force", MorioAttack),
        ("Natsumi: will not attach Followers or Items (a Spell still attaches); even bowed she reinforces a battlefield where she would be opposed and straightens", NatsumiRefusesAndReinforces),
        ("Niiro no Oni: loses 3 Honor entering play, will not attach cards; Melee 5 Attack, and a -5F token on a target still in play", NiiroAttackAndToken),
        ("Quelsa: +2F while he has a Spell; Equip (Battle) attaches a Spell to him for 2 less Gold", QuelsaForceAndDiscount),
        ("Shiba Kudome - exp: her duel destroys the loser, and a winning Kudome gets a +1F Fire token only while her controller controls a Shugenja", KudomeDuel),
        ("Shiba Shirou: +2F while his controller controls a Shugenja; Melee 4 Attack", ShirouForceAndAttack),
        ("Shinjo Jong-Ho: after a Tactical action he performed (Tactical Advantage), bows an opposing card with lower Force than his", JongHoTacticalBow),
        ("Shinjo Min-Hee: returns a Follower from her discard pile to hand; her Tactical Battle destroys (removing a Dragon Clan target from the game) and trades a Follower in hand for a draw", MinHeeReturnAndDestroy),
        ("Shosuro Nobu and Yoritomo Kanahashi: move an enemy Personality home, Kanahashi bowing a Crane Clan one; Kanahashi's -2 Gold Cost on other players' cards in play", NobuAndKanahashi),
        ("Shosuro Ryoken: dishonors a target as he enters play; after a resolution he was at in which his controller destroyed the army, the enemy leader loses 2 Honor", RyokenEntryAndResolution),
        ("Shugorei: -2F on his controller's turn; gains 2 Honor only for a Dragon Clan player none of whose provinces were destroyed since his last turn ended", ShugoreiForceAndHonor),
        ("Yamazaki - exp: negates the first loss of exactly 1 Honor each phase; his Fear Battle straightens another Personality and, only with a Weapon, bows an enemy card", YamazakiNegateAndThreaten),
        ("Yasuki Ikke: for 2 Gold, negate another player's Battle action on his Merchant or make that player lose 2 Honor; moves his Merchant home from away", IkkeReactionAndMove),
    ]);

    private static void PublishedShape()
    {
        var root = RepoRoot();
        Eq(string.Join(",", PublishedContent.PersonalityWave75.Cards.Select(c => c.Id).Order(StringComparer.Ordinal)), string.Join(",", Wired.Order(StringComparer.Ordinal)),
            "personality-wave-75 publishes exactly the wired Personalities");
        foreach (var unwired in Unwired) Check(!CardCatalog.Cards.ContainsKey(unwired), $"{unwired} stays unpublished");
        foreach (var abilityId in Wired.SelectMany(id => CardCatalog.Get(id).Abilities ?? [])) Check(AbilityCatalog.All.ContainsKey(abilityId), $"{abilityId} is catalogued");
        Check(CreatedCards.Verify().Length == 0, "every creator resolves: " + string.Join("; ", CreatedCards.Verify()));

        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string clan, int force, int chi, int cost, int ph, string keywords, string[] abilities, string effect = "none", int entryLoss = 0)
        {
            var c = CardCatalog.Get(id);
            Check(c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && c.PersonalHonor == ph && Keywords(id) == keywords && c.Effect == effect && (c.EntryHonorLoss ?? 0) == entryLoss,
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} ph={c.PersonalHonor} keywords={Keywords(id)} effect={c.Effect} entryLoss={c.EntryHonorLoss}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
        }
        Stats(Sorano, "Lion", 0, 4, 7, 4, "Experienced,Lion Clan,Shugenja,Unique,Water", ["sorano-create"]);
        Stats(Yoyugi, "Dragon", 3, 4, 7, 3, "Courtier,Dragon Clan,Duelist,Magistrate", ["yoyugi-lock", "yoyugi-lock-free"]);
        Stats(Longtooth, "Unicorn", 4, 2, 9, 0, "Cavalry,Dog,Experienced Unicorn War Dogs,Nonhuman,Unicorn Clan,Unique", ["longtooth-destroy"], "se2-refuses-all-attachments");
        Stats(Mrika, "Spider", 1, 3, 4, 3, "Artisan,Bog Hag,Loyal,Nonhuman,Shadowlands,Shugenja,Spider Clan", [], "se2-mrika");
        Stats(Rukia, "Mantis", 3, 3, 6, 2, "Mantis Clan,Naval,Shugenja,Thunder", ["rukia-return"]);
        Stats(Morio, "Unicorn", 4, 3, 8, 1, "Cavalry,Commander,Samurai,Unicorn Clan", ["morio-attack"]);
        Stats(Natsumi, "Phoenix", 3, 4, 7, 3, "Nonhuman,Phoenix Clan,Ryu,Shugenja,Water", ["natsumi-move"], "se2-refuses-followers-and-items");
        Stats(Niiro, "Unaligned", 6, 3, 6, 0, "Nonhuman,Oni,Shadowlands", ["niiro-attack"], "se2-refuses-all-attachments", 3);
        Stats(Quelsa, "Unaligned", 6, 3, 7, 0, "Jakla,Naga,Nonhuman,Pearl,Shugenja", [], "se2-quelsa");
        Stats(Kudome, "Phoenix", 4, 4, 8, 3, "Duelist,Experienced,Fire,Legion of Flame,Loyal,Phoenix Clan,Samurai,Unique,Yojimbo", ["kudome-exp-duel"]);
        Stats(Shirou, "Phoenix", 3, 4, 8, 3, "Duelist,Phoenix Clan,Samurai,Yojimbo", ["shirou-attack"], "se2-shirou-force");
        Stats(JongHo, "Unicorn", 3, 3, 8, 2, "Cavalry,Samurai,Tactician,Unicorn Clan", ["jongho-bow"]);
        Stats(MinHee, "Unicorn", 6, 4, 12, 3, "Cavalry,Commander,Loyal,Samurai,Tactician,The Khan,Unicorn Clan,Unique", ["minhee-return", "minhee-destroy"]);
        Stats(Nobu, "Scorpion", /* issue #492, ruling R5 */ 5, 3, 8, 0, "Ninja,Scorpion Clan", ["nobu-move-home"]);
        Stats(Ryoken, "Scorpion", 3, 2, 6, 0, "Magistrate,Samurai,Scorpion Clan", ["ryoken-dishonor", "ryoken-honor-loss"]);
        Stats(Shugorei, "Dragon", 5, 2, 7, 2, "Dragon Clan,Earth,Golem,Nonhuman,Protector,Shugenja,Spirit", ["shugorei-honor"], "se2-shugorei-force");
        Stats(Yamazaki, "Spider", 5, 3, 9, 1, "Experienced,Kensai,Monk,Order of the Spider,Spider Clan,Unique", ["yamazaki-exp-threaten"], "se2-yamazaki-negate");
        Stats(Ikke, "Crab", 2, 3, 7, 1, "Courtier,Crab Clan,Merchant,Tax Collector", ["ikke-negate", "ikke-honor-loss", "ikke-move-home"]);
        Stats(Kanahashi, "Mantis", 4, 2, 8, 1, "Magistrate,Mantis Clan,Naval,Samurai", ["kanahashi-move-home"], "se2-kanahashi-gold-cost");
        Check(AbilityCatalog.All["yoyugi-lock"].ActionKeywords!.Contains("Political") && AbilityCatalog.All["minhee-destroy"].ActionKeywords!.Contains("Tactical")
            && AbilityCatalog.All["yamazaki-exp-threaten"].ActionKeywords!.Contains("Fear") && AbilityCatalog.All["ryoken-honor-loss"].ActionKeywords!.Contains("Political"), "the printed action keywords are tagged");

        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-75.txt")).Where(l => l.Length > 0);
        Eq(string.Join(",", listed.Order(StringComparer.Ordinal)), string.Join(",", Wired.Order(StringComparer.Ordinal)), "the manifest lists exactly the wired cards");
        foreach (var id in Wired)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        foreach (var id in Unwired) Check(!File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is not shipped");
    }

    private static void SoranoOverlayCreates()
    {
        var s = Table(Phase.Dynasty, 1);
        var basePrinting = Add(s, SoranoBase, 1, Zone.Home);
        var exp = s.Cards[s.Players[1].Provinces[0].CardId!];
        exp.DefinitionId = Sorano; exp.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var overlay = e.QueryActions(1).First(o => o.Type == "overlay" && o.CardId == exp.Id);
        Check(overlay.Enabled, "the overlay is offered: " + overlay.Reason);
        Accept(e, overlay.Command!);
        Eq(Card(e, basePrinting).Zone, Zone.RemovedFromGame, "the base printing left the game");
        Eq(e.State.PendingChoice?.Type, "entryReaction", "overlaying opens her entering-play window");
        Accept(e, Enabled(e, 1, "sorano-create", exp.Id).Command!);
        var created = e.State.Cards.Values.Where(c => c.DefinitionId == LionAncestor && c.OwnerId == 1).ToArray();
        Eq(created.Length, 2, "two Lion Ancestors were created");
        Check(created.All(c => c.Zone == Zone.Home), "at home");
    }

    private static void YoyugiLock()
    {
        // Player 1's turn in the Action Phase; Min-Hee (Unique, Unicorn Clan) may take her Limited action.
        var s = Table(Phase.Action, 1);
        var minhee = Add(s, MinHee, 1, Zone.Home);
        Add(s, Follower, 1, Zone.FateDiscard);
        var kudome = Add(s, Kudome, 1, Zone.Home); // Unique, Phoenix Clan
        var yoyugi = Add(s, Yoyugi, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 1, "minhee-return", minhee.Id), "before the lock, Min-Hee's Limited action is offered");

        var free = Priority(e, 0);
        Check(!(Options(free, 0, "yoyugi-lock", yoyugi.Id).SelectMany(o => o.TargetIds ?? []).Contains(minhee.Id)), "a Unicorn Clan target owes no bow");
        Check(!(Options(free, 0, "yoyugi-lock-free", yoyugi.Id).SelectMany(o => o.TargetIds ?? []).Contains(kudome.Id)), "a Unique non-Unicorn target is not free");
        Accept(free, Offer(free, 0, "yoyugi-lock-free", yoyugi.Id, minhee.Id));
        Check(!Card(free, yoyugi).Bowed, "no bow for a Unicorn Clan target");
        Check(!CanUse(Priority(free, 1), 1, "minhee-return", minhee.Id), "her printed abilities may not be used this turn");
        Check(!CanUse(Priority(free, 0), 0, "yoyugi-lock", yoyugi.Id), "the two halves are one printed action: used once");

        var paid = Priority(e, 0);
        Accept(paid, Offer(paid, 0, "yoyugi-lock", yoyugi.Id, kudome.Id));
        Check(Card(paid, yoyugi).Bowed, "a Unique non-Unicorn target costs the bow");
        Check(paid.State.DisabledAbilities.Any(d => d.CardId == kudome.Id && d.UsageKey == "kudome-exp-duel"), "Kudome's duel is locked");
        Check(CanUse(Priority(paid, 1), 1, "minhee-return", minhee.Id), "another Personality's abilities are untouched");
    }

    private static void LongtoothRefusesAndDestroys()
    {
        var s = Table(Phase.Action, 0);
        var longtooth = Add(s, Longtooth, 0, Zone.Home);
        var crane = Add(s, Crane, 0, Zone.Home);
        var follower = Add(s, Follower, 0, Zone.Hand);
        var item = Add(s, Item, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        foreach (var card in new[] { follower, item })
        {
            var equip = e.QueryActions(0).First(o => o.Type == "equip" && o.CardId == card.Id);
            Check(equip.Enabled && equip.TargetIds!.Contains(crane.Id) && !equip.TargetIds!.Contains(longtooth.Id), $"{card.DefinitionId} is offered to the Crane samurai, never to Longtooth");
            Reject(e, equip.Command! with { TargetId = longtooth.Id });
        }

        var b = Table(Phase.Battle, 0);
        var lt = Add(b, Longtooth, 0, Zone.Battlefield, province: 0);
        var lone = Add(b, Lion, 1, Zone.Battlefield, province: 0);
        var escorted = Add(b, Crane, 1, Zone.Battlefield, province: 0);
        var escort = Add(b, Follower, 1, Zone.Attached, province: 0, parent: escorted.Id);
        var eb = MatchEngine.FromTestState(b);
        var targets = Options(eb, 0, "longtooth-destroy", lt.Id).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(lone.Id) && targets.Contains(escort.Id) && !targets.Contains(escorted.Id), "a Follower or a Personality without Followers: " + string.Join(",", targets));
        Accept(eb, Offer(eb, 0, "longtooth-destroy", lt.Id, lone.Id));
        Eq(Card(eb, lone).Zone, Zone.Dead, "destroyed");
    }

    private static void MrikaLoyalAndHonor()
    {
        MatchState Recruit(Action<MatchState> edit, out string mrikaId)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            mrikaId = s.Players[0].Provinces[0].CardId!;
            s.Cards[mrikaId].DefinitionId = Mrika; s.Cards[mrikaId].FaceUp = true;
            for (var i = 0; i < 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
            s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
            edit(s);
            return s;
        }
        // A Crane player with an ordinary Stronghold: Loyal (Spider Clan) refuses him.
        var crane = MatchEngine.FromTestState(Recruit(_ => { }, out var m1));
        Check(crane.QuotePurchase(0, m1).Error?.Contains("Loyal") == true, "Loyal refuses a Crane player: " + crane.QuotePurchase(0, m1).Error);
        // The same Crane player with The Forgotten Temple may ignore it (the Temple itself then forbids the Honor gain).
        var temple = MatchEngine.FromTestState(Recruit(st => st.Cards[st.Players[0].StrongholdId].DefinitionId = Temple, out var m2));
        Check(temple.QuotePurchase(0, m2).Error is null, "the Loyal restriction is ignored under The Forgotten Temple: " + temple.QuotePurchase(0, m2).Error);
        var templeHonor = Honor(temple, 0);
        Accept(temple, new GameCommand("purchase", 0, temple.Version, CardId: m2, SourceIds: temple.PaymentOptions(0, temple.QuotePurchase(0, m2).Cost).First().SourceIds));
        Eq(Card(temple, temple.State.Cards[m2]).Zone, Zone.Home, "recruited");
        Eq(Honor(temple, 0), templeHonor, "The Forgotten Temple: no Honor gain");
        // A Spider Clan player: entering play gains 2 Honor.
        var spider = MatchEngine.FromTestState(Recruit(st => st.Players[0].Clan = "Spider", out var m3));
        var honor = Honor(spider, 0);
        Accept(spider, new GameCommand("purchase", 0, spider.Version, CardId: m3, SourceIds: spider.PaymentOptions(0, spider.QuotePurchase(0, m3).Cost).First().SourceIds));
        // A Spider Clan player recruiting her gets the Proclaim window first; declining it, the entering-play trait follows.
        for (var guard = 0; guard < 4 && spider.State.PendingChoice?.Type == "proclaim"; guard++)
            Accept(spider, spider.QueryActions(spider.State.PriorityPlayerId).First(o => o.Type == "declineReaction").Command!);
        Eq(Honor(spider, 0), honor + 2, "After M'rika enters play: Gain 2 Honor");
    }

    private static void RukiaReturn()
    {
        var s = Table(Phase.Battle, 0);
        var rukia = Add(s, Rukia, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var item = Add(s, Item, 1, Zone.Attached, province: 0, parent: enemy.Id);
        var own = Add(s, Item, 0, Zone.Attached, province: 0, parent: rukia.Id);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "rukia-return", rukia.Id).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(item.Id) && targets.Contains(own.Id) && !targets.Contains(enemy.Id), "either side's attachment while both sides stand there: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, "rukia-return", rukia.Id, item.Id));
        Eq(Card(e, item).Zone, Zone.Hand, "the Item went to its owner's hand");
        Check(e.State.Players[1].Hand.Contains(item.Id), "its owner's");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action was granted");

        // With no enemy unit at the battlefield, Rukia's own attachment is not opposed.
        var s2 = Table(Phase.Battle, 0);
        var rukia2 = Add(s2, Rukia, 0, Zone.Battlefield, province: 0);
        Add(s2, Item, 0, Zone.Attached, province: 0, parent: rukia2.Id);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "rukia-return", rukia2.Id), "nothing is opposed");
    }

    private static void MorioAttack()
    {
        var s = Table(Phase.Battle, 0);
        var morio = Add(s, Morio, 0, Zone.Battlefield, province: 0);
        var bowedFollower = Add(s, Follower, 0, Zone.Attached, bowed: true, province: 0, parent: morio.Id);
        Batch336Support.Force(bowedFollower, 2); // 4F
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);  // 4F
        var e = MatchEngine.FromTestState(s);
        var option = Option(e, 0, "morio-attack", c => c.PerformerId == bowedFollower.Id);
        Check(option.TargetIds!.Contains(enemy.Id), "the enemy is a legal Attack target");
        Accept(e, option.Command! with { TargetId = enemy.Id });
        Eq(Card(e, enemy).Zone, Zone.Dead, "Melee 4 (the Follower's Force) destroyed the 4F enemy");

        var s2 = Table(Phase.Battle, 0);
        var morio2 = Add(s2, Morio, 0, Zone.Battlefield, province: 0);
        var weak = Add(s2, Follower, 0, Zone.Attached, province: 0, parent: morio2.Id); // 2F
        var enemy2 = Add(s2, Lion, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Option(e2, 0, "morio-attack", c => c.PerformerId == weak.Id).Command! with { TargetId = enemy2.Id }); // a target above the Attack's strength is still a legal target (core rule)
        Eq(Card(e2, enemy2).Zone, Zone.Battlefield, "Melee 2 cannot destroy a 4F enemy");
    }

    private static void NatsumiRefusesAndReinforces()
    {
        var s = Table(Phase.Action, 0);
        var natsumi = Add(s, Natsumi, 0, Zone.Home);
        var crane = Add(s, Crane, 0, Zone.Home);
        var follower = Add(s, Follower, 0, Zone.Hand);
        var item = Add(s, Item, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        foreach (var card in new[] { follower, item })
        {
            var equip = e.QueryActions(0).First(o => o.Type == "equip" && o.CardId == card.Id);
            Check(equip.TargetIds!.Contains(crane.Id) && !equip.TargetIds!.Contains(natsumi.Id), $"{card.DefinitionId} will not attach to Natsumi");
        }
        // A Spell still attaches (Equip (Battle)).
        var sb = PublishedTable(Phase.Battle, 0);
        var n2 = Add(sb, Natsumi, 0, Zone.Battlefield, province: 0);
        Add(sb, Lion, 1, Zone.Battlefield, province: 0);
        var spell = Add(sb, Spell, 0, Zone.Hand);
        var eb = MatchEngine.FromTestState(sb);
        Accept(eb, Option(eb, 0, "equip-battle", c => c.PerformerId == n2.Id).Command! with { TargetId = spell.Id });
        Eq(Card(eb, spell).AttachedTo, n2.Id, "the Spell attached to Natsumi");

        // Even bowed, from home: she moves to a battlefield where she would be opposed, and her unit straightens.
        var r = Table(Phase.Battle, 0);
        var n3 = Add(r, Natsumi, 0, Zone.Home, bowed: true);
        Add(r, Lion, 1, Zone.Battlefield, province: 0);
        var er = MatchEngine.FromTestState(r);
        Accept(er, Enabled(er, 0, "natsumi-move", n3.Id).Command!);
        Check(Card(er, n3).Zone == Zone.Battlefield && Card(er, n3).ProvinceIndex == 0 && !Card(er, n3).Bowed, "moved to the battlefield and straightened");
        // Where she would not be opposed, nothing happens.
        var q = Table(Phase.Battle, 0);
        var n4 = Add(q, Natsumi, 0, Zone.Home, bowed: true);
        Add(q, Crane, 0, Zone.Battlefield, province: 0);
        var eq = MatchEngine.FromTestState(q);
        Accept(eq, Use(eq, 0, "natsumi-move", n4.Id));
        Check(Card(eq, n4).Zone == Zone.Home && Card(eq, n4).Bowed, "unopposed: she stays home, bowed");
    }

    private static void NiiroAttackAndToken()
    {
        // Daigotsu Negishi - exp (5F, Gold Cost 9) survives: his destruction by a unit of lower total Gold Cost (Niiro's 6) is negated -- he is still in play for the token.
        var s = Table(Phase.Battle, 0);
        var niiro = Add(s, Niiro, 0, Zone.Battlefield, province: 0);
        var negishi = Add(s, "oracle-l5r-10227", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "niiro-attack", niiro.Id, negishi.Id));
        Eq(Card(e, negishi).Zone, Zone.Battlefield, "the hit on Negishi was negated");
        Check(Card(e, negishi).Tokens.Any(t => t.Stat == "Force" && t.Amount == -5), "he received a -5F token");
        Eq(Force(e, negishi), 0, "5F - 5");

        var s2 = Table(Phase.Battle, 0);
        var niiro2 = Add(s2, Niiro, 0, Zone.Battlefield, province: 0);
        var lion = Add(s2, Lion, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "niiro-attack", niiro2.Id, lion.Id));
        Eq(Card(e2, lion).Zone, Zone.Dead, "Melee 5 destroyed the 4F target");
        Check(Card(e2, lion).Tokens.Count == 0, "no token for a target no longer in play");

        // "After Niiro enters play: Lose 3 Honor."
        var r = MatchEngine.CreateEconomyScenario().State;
        var niiroInProvince = r.Players[0].Provinces[0].CardId!;
        r.Cards[niiroInProvince].DefinitionId = Niiro; r.Cards[niiroInProvince].FaceUp = true;
        for (var i = 0; i < 2; i++) Add(r, "training-marketplace", 0, Zone.Home);
        r.Phase = Phase.Dynasty; r.ActivePlayerId = 0; r.PriorityPlayerId = 0;
        var er = MatchEngine.FromTestState(r);
        var honor = Honor(er, 0);
        Accept(er, new GameCommand("purchase", 0, er.Version, CardId: niiroInProvince, SourceIds: er.PaymentOptions(0, er.QuotePurchase(0, niiroInProvince).Cost).First().SourceIds));
        Eq(Honor(er, 0), honor - 3, "lost 3 Honor");
    }

    private static void QuelsaForceAndDiscount()
    {
        var s = PublishedTable(Phase.Battle, 0);
        var quelsa = Add(s, Quelsa, 0, Zone.Battlefield, province: 0);
        var shugenja = Add(s, Shugenja, 0, Zone.Battlefield, province: 0);
        Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var gates = Add(s, Gates, 0, Zone.Hand); // a 4-Gold Spell
        for (var i = 0; i < 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(Force(e, quelsa), 6, "no Spell: printed Force");
        var toQuelsa = Option(e, 0, "equip-battle", c => c.PerformerId == quelsa.Id);
        var toOther = Option(e, 0, "equip-battle", c => c.PerformerId == shugenja.Id);
        Eq(e.QuoteAbilityGold(toQuelsa.Command! with { TargetId = gates.Id })!.Cost, 2, "Quelsa attaches the 4-Gold Spell for 2");
        Eq(e.QuoteAbilityGold(toOther.Command! with { TargetId = gates.Id })!.Cost, 4, "another Shugenja pays its printed 4");
        var command = toQuelsa.Command! with { TargetId = gates.Id };
        command = command with { SourceIds = e.QuoteAbilityGold(command)!.Payments.First().SourceIds };
        Accept(e, command);
        Eq(Card(e, gates).AttachedTo, quelsa.Id, "attached to Quelsa");
        Eq(Force(e, quelsa), 8, "+2F while he has a Spell");
    }

    private static void KudomeDuel()
    {
        MatchEngine Fight(bool shugenja, out CardInstance kudome, out CardInstance loser)
        {
            var s = Table(Phase.Battle, 0);
            kudome = Add(s, Kudome, 0, Zone.Battlefield, province: 0); // Chi 4
            loser = Add(s, Lion, 1, Zone.Battlefield, province: 0);     // Chi 3
            if (shugenja) Add(s, Shugenja, 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "kudome-exp-duel", kudome.Id, loser.Id));
            Duel(e);
            return e;
        }
        var e1 = Fight(true, out var k1, out var l1);
        Eq(Card(e1, l1).Zone, Zone.Dead, "the duel's loser is destroyed");
        Eq(Card(e1, k1).Tokens.Count(t => t.Type == "Fire" && t.Amount == 1), 1, "a winning Kudome with a Shugenja gets a +1F Fire token");
        var e2 = Fight(false, out var k2, out var l2);
        Eq(Card(e2, l2).Zone, Zone.Dead, "the loser is destroyed");
        Eq(Card(e2, k2).Tokens.Count, 0, "no Shugenja: no token");
    }

    private static void ShirouForceAndAttack()
    {
        var s = Table(Phase.Battle, 0);
        var shirou = Add(s, Shirou, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(Force(e, shirou), 3, "no Shugenja: printed Force");
        var withShugenja = Edit(e, st => Add(st, Shugenja, 0, Zone.Home));
        Eq(Force(withShugenja, shirou), 5, "+2F while his controller controls a Shugenja");
        Accept(e, Offer(e, 0, "shirou-attack", shirou.Id, enemy.Id));
        Eq(Card(e, enemy).Zone, Zone.Dead, "Melee 4 destroyed the 4F enemy");
    }

    private static void JongHoTacticalBow()
    {
        var s = PublishedTable(Phase.Battle, 0);
        var jongho = Add(s, JongHo, 0, Zone.Battlefield, province: 0); // 3F
        var weak = Add(s, Crane, 1, Zone.Battlefield, province: 0);    // 3F, made 1F below
        Batch336Support.Force(weak, -2);
        var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0);   // 4F
        Add(s, Crane, 0, Zone.Hand); // a card to discard (Focus 0)
        var e = MatchEngine.FromTestState(s);
        Accept(e, Option(e, 0, "tactical-advantage", c => c.PerformerId == jongho.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for Jong-Ho");
        var targets = Options(e, 0, "jongho-bow", jongho.Id).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(weak.Id) && !targets.Contains(strong.Id), "only an opposing card with lower Force: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, "jongho-bow", jongho.Id, weak.Id));
        Check(Card(e, weak).Bowed, "bowed");

        // Another Tactician's Tactical action is not Jong-Ho's.
        var s2 = PublishedTable(Phase.Battle, 0);
        var jongho2 = Add(s2, JongHo, 0, Zone.Battlefield, province: 0);
        var other = Add(s2, MinHee, 0, Zone.Battlefield, province: 0);
        Add(s2, Crane, 1, Zone.Battlefield, province: 0);
        Add(s2, Crane, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Option(e2, 0, "tactical-advantage", c => c.PerformerId == other.Id).Command!);
        Check(!Options(e2, 0, "jongho-bow", jongho2.Id).Any(o => o.Enabled), "no Reaction after a Tactical action he did not perform");
    }

    private static void MinHeeReturnAndDestroy()
    {
        var s = Table(Phase.Action, 0);
        var minhee = Add(s, MinHee, 0, Zone.Home);
        var discarded = Add(s, Follower, 0, Zone.FateDiscard);
        Add(s, Follower, 1, Zone.FateDiscard); // another player's: not hers
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "minhee-return", minhee.Id).SelectMany(o => o.TargetIds ?? []).ToArray();
        Eq(string.Join(",", targets), discarded.Id, "only a Follower in her own discard pile");
        Accept(e, Offer(e, 0, "minhee-return", minhee.Id, discarded.Id));
        Check(e.State.Players[0].Hand.Contains(discarded.Id) && Card(e, discarded).Zone == Zone.Hand, "the Follower is in her hand");

        var b = Table(Phase.Battle, 0);
        var mh = Add(b, MinHee, 0, Zone.Battlefield, province: 0);
        var dragon = Add(b, Yoyugi, 1, Zone.Battlefield, province: 0); // Dragon Clan, no attachments
        var inHand = Add(b, Follower, 0, Zone.Hand);
        var eb = MatchEngine.FromTestState(b);
        var hand = eb.State.Players[0].Hand.Count;
        Accept(eb, Use(eb, 0, "minhee-destroy", mh.Id, selections: [Pick("target", dragon.Id), Pick("follower", inHand.Id)]));
        Eq(Card(eb, dragon).Zone, Zone.RemovedFromGame, "a destroyed Dragon Clan card is removed from the game");
        Eq(Card(eb, inHand).Zone, Zone.FateDiscard, "the Follower was discarded");
        Eq(eb.State.Players[0].Hand.Count, hand, "and replaced by a draw");

        var c = Table(Phase.Battle, 0);
        var mh2 = Add(c, MinHee, 0, Zone.Battlefield, province: 0);
        var lion = Add(c, Lion, 1, Zone.Battlefield, province: 0);
        var ec = MatchEngine.FromTestState(c);
        var handBefore = ec.State.Players[0].Hand.Count;
        Accept(ec, Use(ec, 0, "minhee-destroy", mh2.Id, selections: [Pick("target", lion.Id)]));
        Eq(Card(ec, lion).Zone, Zone.Dead, "any other target is simply destroyed");
        Eq(ec.State.Players[0].Hand.Count, handBefore, "no Follower discarded: no draw");
    }

    private static void NobuAndKanahashi()
    {
        var s = Table(Phase.Battle, 0);
        var nobu = Add(s, Nobu, 0, Zone.Battlefield, province: 0);
        var kanahashi = Add(s, Kanahashi, 0, Zone.Battlefield, province: 0);
        var crane = Add(s, Crane, 1, Zone.Battlefield, province: 0);
        var lion = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var lion2 = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "nobu-move-home", nobu.Id, lion.Id));
        Check(Card(e, lion).Zone == Zone.Home && !Card(e, lion).Bowed, "Nobu moved him home");
        var ek = Priority(e, 0);
        Accept(ek, Offer(ek, 0, "kanahashi-move-home", kanahashi.Id, crane.Id));
        Check(Card(ek, crane).Zone == Zone.Home && Card(ek, crane).Bowed, "a Crane Clan target is moved home and bowed");
        var s2 = Table(Phase.Battle, 0);
        var k2 = Add(s2, Kanahashi, 0, Zone.Battlefield, province: 0);
        var l2 = Add(s2, Lion, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "kanahashi-move-home", k2.Id, l2.Id));
        Check(Card(e2, l2).Zone == Zone.Home && !Card(e2, l2).Bowed, "any other target is only moved home");

        // "Other players' Personalities and attachments (in play) have -2 Gold Cost": the Gold Cost a card reads (Gold and Steel's own comparison) goes down by 2 per Kanahashi of
        // another player's, never below 0, and never for his own controller's cards.
        var g = Table(Phase.Action, 0);
        Add(g, Kanahashi, 0, Zone.Home);
        var enemy = Add(g, Lion, 1, Zone.Home);       // Gold Cost 5
        var enemyItem = Add(g, Item, 1, Zone.Attached, parent: enemy.Id); // Gold Cost 2
        var mine = Add(g, Crane, 0, Zone.Home);        // Gold Cost 5
        var inHand = Add(g, Item, 1, Zone.Hand);
        var eg = MatchEngine.FromTestState(g);
        Eq(eg.CardGoldCostForTest(enemy.Id), 3, "another player's Personality in play: 5 - 2");
        Eq(eg.CardGoldCostForTest(enemyItem.Id), 0, "another player's attachment in play: 2 - 2");
        Eq(eg.CardGoldCostForTest(mine.Id), 5, "his own controller's Personality is unchanged");
        Eq(eg.CardGoldCostForTest(inHand.Id), 2, "a card out of play is unchanged");
    }

    private static void RyokenEntryAndResolution()
    {
        // Entering play: a Political Reaction dishonoring a target Personality.
        var r = MatchEngine.CreateEconomyScenario().State;
        var ryokenInProvince = r.Players[0].Provinces[0].CardId!;
        r.Cards[ryokenInProvince].DefinitionId = Ryoken; r.Cards[ryokenInProvince].FaceUp = true;
        for (var i = 0; i < 2; i++) Add(r, "training-marketplace", 0, Zone.Home);
        var victim = Add(r, Lion, 1, Zone.Home);
        r.Phase = Phase.Dynasty; r.ActivePlayerId = 0; r.PriorityPlayerId = 0;
        var er = MatchEngine.FromTestState(r);
        Accept(er, new GameCommand("purchase", 0, er.Version, CardId: ryokenInProvince, SourceIds: er.PaymentOptions(0, er.QuotePurchase(0, ryokenInProvince).Cost).First().SourceIds));
        Accept(er, Offer(er, 0, "ryoken-dishonor", ryokenInProvince, victim.Id));
        Check(Card(er, victim).Dishonorable, "the target was dishonored");

        // After a battle's resolution in which Ryoken's controller (the Attacker) destroyed the enemy army while Ryoken stood there: the enemy leader loses 2 Honor.
        MatchEngine Resolve(bool ryokenThere)
        {
            var s = Table(Phase.Battle, 0); s.ActivePlayerId = 0; s.PriorityPlayerId = 1;
            var ryoken = Add(s, Ryoken, 0, ryokenThere ? Zone.Battlefield : Zone.Home, province: ryokenThere ? 0 : null);
            var attacker = Add(s, Lion, 0, Zone.Battlefield, province: 0);
            Batch336Support.Force(attacker, 10);
            Add(s, Crane, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            for (var guard = 0; guard < 6 && e.State.PendingChoice?.Type != "afterResolutionReaction" && e.State.Phase == Phase.Battle; guard++)
                Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            return e;
        }
        var there = Resolve(true);
        Eq(there.State.PendingChoice?.Type, "afterResolutionReaction", "the after-resolution window opens for Ryoken");
        var ryokenCard = there.State.Cards.Values.First(c => c.DefinitionId == Ryoken);
        var honor = Honor(there, 1);
        Accept(there, Enabled(there, 0, "ryoken-honor-loss", ryokenCard.Id).Command!);
        Eq(Honor(there, 1), honor - 2, "the enemy leader lost 2 Honor");
        var away = Resolve(false);
        var awayRyoken = away.State.Cards.Values.First(c => c.DefinitionId == Ryoken);
        Check(away.State.PendingChoice?.Type != "afterResolutionReaction" || !CanUse(away, 0, "ryoken-honor-loss", awayRyoken.Id), "Ryoken at home: no Reaction");
    }

    private static void ShugoreiForceAndHonor()
    {
        var s = Table(Phase.Action, 0);
        s.Players[0].Clan = "Dragon";
        var shugorei = Add(s, Shugorei, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(Force(e, shugorei), 3, "-2F while it is his controller's turn");
        Eq(Force(Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 1; }), shugorei), 5, "printed Force on another player's turn");
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 0, "shugorei-honor", shugorei.Id).Command!);
        Eq(Honor(e, 0), honor + 2, "a Dragon Clan player with no province destroyed gains 2 Honor");
        var lost = Edit(MatchEngine.FromTestState(s), st => st.ProvinceDestroyedSinceTurnEnd[0] = true);
        Check(!CanUse(lost, 0, "shugorei-honor", shugorei.Id), "not after one of his provinces was destroyed");
        var crane = Edit(MatchEngine.FromTestState(s), st => st.Players[0].Clan = "Crane");
        Check(!CanUse(crane, 0, "shugorei-honor", shugorei.Id), "not for a player of another Clan");
    }

    private static void YamazakiNegateAndThreaten()
    {
        // "Fear Battle: Target another Personality: Yamazaki threatens him. Straighten him. If Yamazaki has a Weapon, you may target and bow an enemy card."
        var s = Table(Phase.Battle, 0);
        var yamazaki = Add(s, Yamazaki, 0, Zone.Battlefield, province: 0);
        var sword = Add(s, Weapon, 0, Zone.Attached, province: 0, parent: yamazaki.Id);
        var own = Add(s, Crane, 0, Zone.Battlefield, bowed: true, province: 0);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "yamazaki-exp-threaten", yamazaki.Id).SelectMany(o => o.Slots ?? []).First(sl => sl.Slot == "bow").CardIds;
        Check(targets.Contains(enemy.Id) && !targets.Contains(own.Id), "with a Weapon, an enemy card may be bowed");
        Accept(e, Use(e, 0, "yamazaki-exp-threaten", yamazaki.Id, selections: [Pick("target", own.Id), Pick("bow", enemy.Id)]));
        Check(!Card(e, own).Bowed && Card(e, enemy).Bowed, "straightened his target and bowed the enemy card");
        Check(Options(e, 0, "yamazaki-exp-threaten", yamazaki.Id).All(o => !(o.Slots ?? []).Any(sl => sl.Slot == "target" && sl.CardIds.Contains(yamazaki.Id))), "\"another Personality\": never himself");

        var s2 = Table(Phase.Battle, 0);
        var y2 = Add(s2, Yamazaki, 0, Zone.Battlefield, province: 0);
        Add(s2, Crane, 0, Zone.Battlefield, bowed: true, province: 0);
        var enemy2 = Add(s2, Lion, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        var noWeapon = Options(e2, 0, "yamazaki-exp-threaten", y2.Id).SelectMany(o => o.Slots ?? []).First(sl => sl.Slot == "bow").CardIds;
        Check(!noWeapon.Contains(enemy2.Id), "without a Weapon, no enemy card may be bowed");

        // "Once per phase, before you lose exactly 1 Honor: Negate the loss." Two losses of exactly 1 in the same phase: the first is negated, the second is not; a loss of 2 is
        // never touched; the next phase negates again.
        var h = Table(Phase.Action, 0);
        Add(h, Yamazaki, 0, Zone.Home);
        var eh = MatchEngine.FromTestState(h);
        var honor = Honor(eh, 0);
        eh.GainHonorForTest(0, -1, "a test loss");
        Eq(Honor(eh, 0), honor, "the first loss of exactly 1 this phase is negated");
        eh.GainHonorForTest(0, -1, "a test loss");
        Eq(Honor(eh, 0), honor - 1, "the second is not");
        eh.GainHonorForTest(0, -2, "a test loss");
        Eq(Honor(eh, 0), honor - 3, "a loss of 2 is not exactly 1");
        var next = Edit(eh, st => st.Phase = Phase.Dynasty);
        var before = Honor(next, 0);
        next.GainHonorForTest(0, -1, "a test loss");
        Eq(Honor(next, 0), before, "a new phase: negated again");
        _ = sword;
    }

    private static void IkkeReactionAndMove()
    {
        // Player 1 (the Lion) takes a Battle action targeting Ikke (Nobu's move home); Ikke's controller pays 2 Gold to negate its effects on him.
        MatchEngine Attacked(out CardInstance ikke, out CardInstance nobu)
        {
            var s = Table(Phase.Battle, 1);
            ikke = Add(s, Ikke, 0, Zone.Battlefield, province: 0);
            nobu = Add(s, Nobu, 1, Zone.Battlefield, province: 0);
            for (var i = 0; i < 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "nobu-move-home", nobu.Id, ikke.Id));
            return e;
        }
        var e1 = Attacked(out var ikke1, out _);
        var negate = Enabled(e1, 0, "ikke-negate", ikke1.Id).Command!;
        var gold = e1.QuoteAbilityGold(negate)!;
        Eq(gold.Cost, 2, "pay 2 Gold");
        Accept(e1, negate with { SourceIds = gold.Payments.First().SourceIds });
        for (var guard = 0; guard < 4 && e1.State.PendingChoice is not null; guard++) Accept(e1, new GameCommand("pass", e1.State.PriorityPlayerId, e1.Version));
        Eq(Card(e1, ikke1).Zone, Zone.Battlefield, "the move home of Ikke was negated");

        var e2 = Attacked(out var ikke2, out _);
        var honor = Honor(e2, 1);
        var loss = Enabled(e2, 0, "ikke-honor-loss", ikke2.Id).Command!;
        Accept(e2, loss with { SourceIds = e2.QuoteAbilityGold(loss)!.Payments.First().SourceIds });
        Eq(Honor(e2, 1), honor - 2, "the acting player lost 2 Honor");
        Check(!CanUse(e2, 0, "ikke-negate", ikke2.Id), "the two outcomes share one use");

        // "Even if Ikke is not at the current battlefield, target your Merchant: Move him home."
        var m = Table(Phase.Battle, 0);
        var ikkeHome = Add(m, Ikke, 0, Zone.Home);
        var merchant = Add(m, Ikke, 0, Zone.Battlefield, province: 0);
        Add(m, Lion, 1, Zone.Battlefield, province: 0);
        var em = MatchEngine.FromTestState(m);
        Accept(em, Offer(em, 0, "ikke-move-home", ikkeHome.Id, merchant.Id));
        Eq(Card(em, merchant).Zone, Zone.Home, "his Merchant moved home");
    }
}
