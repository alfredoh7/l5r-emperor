using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// The "pers-gates-2" cluster of issue #674 (personality-wave-134): thirteen Gates of Chaos Personalities wired from their scans' printed text. Every scenario is an explicit position on the
// economy table (Batch336Support.Table: player 0 the Crane, player 1 the Lion; Battle: province 0 is the current battlefield) driven through QueryActions and the commands the engine offers.
internal static class WlPersGates2Tests
{
    private const string Katagi = "oracle-l5r-10631", Rukia = "oracle-l5r-10642", Tsurao = "oracle-l5r-10652", Yoshie = "oracle-l5r-10676", Nigawa = "oracle-l5r-10655", Kodanshi = "oracle-l5r-10656",
        Chokichi = "oracle-l5r-10620", Gombei = "oracle-l5r-10644", Sayaka = "oracle-l5r-10671", Daisuki = "oracle-l5r-10622", Nobukai = "oracle-l5r-10658", Takashi = "oracle-l5r-10657",
        Hiromi = "oracle-l5r-10646";
    private const string Wave = "personality-wave-134";
    private const string Crane = "training-crane-samurai", Lion = "training-lion-samurai", Weapon = "training-weapon";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId) => Batch336Support.Offer(e, player, ability, cardId, targetId);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static int ForceOf(MatchEngine e, CardInstance c) => e.CardForceForTest(c.Id);
    private static void CloseWindows(MatchEngine e) { for (var guard = 0; guard < 12 && e.State.PendingChoice is { Type: "beforeActionReaction" or "engageReaction" or "attackReaction" }; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }

    private static void RejectedWithoutChange(MatchEngine e, GameCommand command)
    {
        var before = Json(e);
        Batch336Support.Rejection(e, command);
        Eq(Json(e), before, "a rejected action changes nothing");
    }

    public static void Run(TestRunner runner) => runner.Run("wl-pers-gates-2", [
        ("Published shape: the thirteen Personalities carry their scans' stats, keywords, text, entry Honor loss and abilities, name Gates of Chaos, ship art and are in personality-wave-134's manifest", PublishedShape),
        ("Mirumoto Katagi - exp: equips a non-Unique Weapon from your own discard pile as a Battle action, paying its cost; not a Unique or another player's Weapon, not away from the battle", KatagiEquip),
        ("Moshi Rukia - exp: bows an enemy card without attachments, bowing it, then destroying it when it is an attachment; a card with attachments or your own is refused", RukiaBow),
        ("Shiba Tsurao: -2F to an enemy Personality or Follower; refuses your own cards", TsuraoWeaken),
        ("Shinjo Yoshie: -3F to an enemy Infantry (non-Cavalry) Personality or Follower; she enters play for 1 Honor and draws (Destined)", YoshieWeaken),
        ("Shosuro Nigawa and Utaku Sayaka: Invest 3 as they enter play gives two +1F tokens; declining or an unpaid Invest changes nothing", InvestTokens),
        ("Soshi Kodanshi - exp: bows your unbowed Ninja and a Personality; a Fudo or Fallen one will not straighten this turn; wrong or bowed first targets are refused", KodanshiBow),
        ("Toritaka Chokichi - exp: bows a Spider Clan Personality, or an enemy card without attachments only while he has a Follower", ChokichiBow),
        ("Tsuruchi Gombei, Yogo Takashi: lose 1 Honor on entering play; Takashi also draws (Destined)", EntryTraits),
        ("Yogo Nobukai: -2F to a target Personality, only on your turn or while you control a Courtier", NobukaiWeaken),
        ("Yoritomo Hiromi - exp: a Ranged Attack whose strength is the Gold paid", HiromiAttack),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = Batch336Support.RepoRoot();
        var ids = new[] { Katagi, Rukia, Tsurao, Yoshie, Nigawa, Kodanshi, Chokichi, Gombei, Sayaka, Daisuki, Nobukai, Takashi, Hiromi };
        Check(new HashSet<string>(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", Wave + ".txt")).Where(l => l.Length > 0)).SetEquals(ids), "manifest pins exactly the thirteen");
        var rows = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", Wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(rows.Length, 13, "one provenance row each");
        foreach (var id in ids)
        {
            Eq(rows.Single(r => r.GetProperty("card_id").GetString() == id).GetProperty("printing_set")[0].GetString(), "Gates of Chaos", id + ": its printing");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), id + " ships art");
        }
        void Shape(string id, string name, string clan, int gold, int force, int chi, int ph, int? honorReq, string[] keywords, string[] abilities, int honorLoss, string textStart)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == CardType.Personality && d.Name == name && d.Clan == clan && d.GoldCost == gold && d.Force == force && d.Chi == chi && d.PersonalHonor == ph && d.HonorRequirement == honorReq && !d.ScenarioOnly, $"{name}: printed stats");
            Check(d.Keywords!.OrderBy(k => k).SequenceEqual(keywords.OrderBy(k => k)), $"{name}: printed keywords {string.Join(",", d.Keywords!)}");
            Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities");
            Eq(d.Effect, "none", $"{name}: effect"); Eq(d.EntryHonorLoss ?? 0, honorLoss, $"{name}: entry Honor loss");
            Check(d.Text.StartsWith(textStart), $"{name}: printed text: {d.Text}");
        }
        Shape(Katagi, "Mirumoto Katagi", "Dragon", 8, 4, 4, 3, 7, ["Kensai", "Unique", "Dragon Clan", "Experienced", "Samurai"], [WlPersGates2Abilities.KatagiEquip], 0, "Battle, ");
        Shape(Rukia, "Moshi Rukia", "Mantis", 8, 3, 4, 2, 2, ["Naval", "Unique", "Experienced", "Mantis Clan", "Shugenja", "Thunder"], ["hiroto-bow"], 0, "Battle: Bow a target enemy card without attachments. Destroy it if it is an attachment.");
        Shape(Tsurao, "Shiba Tsurao", "Phoenix", 8, 4, 4, 3, 6, ["Expendable", "Phoenix Clan", "Samurai", "Yojimbo"], [WlPersGates2Abilities.TsuraoWeaken], 0, "(Draw a card after your Expendable card is destroyed.)");
        Shape(Yoshie, "Shinjo Yoshie", "Unicorn", 7, 3, 1, 0, null, ["Cavalry", "Destined", "Commander", "Fallen", "Samurai", "Unicorn Clan"], ["hatsuto-weaken"], 1, "(Draw a card after your Destined card enters play.)");
        Shape(Nigawa, "Shosuro Nigawa", "Scorpion", 4, 2, 3, 2, 0, ["Magistrate", "Samurai", "Scorpion Clan"], [WlPersGates2Abilities.InvestTwo], 0, "Invest ");
        Shape(Kodanshi, "Soshi Kodanshi", "Scorpion", 8, 4, 4, 0, null, ["Unique", "Experienced", "Ninja", "Scorpion Clan"], [WlPersGates2Abilities.KodanshiBow], 0, "Limited: Bow your target unbowed Ninja Personality.");
        Shape(Chokichi, "Toritaka Chokichi", "Crab", 7, 4, 3, 2, 4, ["Unique", "Crab Clan", "Experienced", "Samurai", "Scout"], [WlPersGates2Abilities.ChokichiBow], 0, "Battle: Bow a target Spider Clan Personality");
        Shape(Gombei, "Tsuruchi Gombei", "Mantis", 4, 3, 1, 0, null, ["Naval", "Fallen", "Mantis Clan", "Samurai", "Scout"], [], 1, "After Gombei enters play, lose 1 Honor.");
        Shape(Sayaka, "Utaku Sayaka", "Unicorn", 5, 2, 3, 3, 4, ["Cavalry", "Battle Maiden", "Paragon", "Samurai", "Unicorn Clan"], [WlPersGates2Abilities.InvestTwo], 0, "Invest ");
        Shape(Daisuki, "Yasuki Daisuki", "Crab", 2, 0, 1, 1, null, ["Expendable", "Courtier", "Crab Clan", "Merchant"], [], 0, "(Draw a card after your Expendable card is destroyed.)");
        Shape(Nobukai, "Yogo Nobukai", "Scorpion", 5, 3, 3, 1, 0, ["Samurai", "Scorpion Clan", "Yojimbo"], [WlPersGates2Abilities.NobukaiWeaken], 0, "Open: If it is your turn or you control a Courtier");
        Shape(Takashi, "Yogo Takashi", "Scorpion", 5, 0, 4, 0, null, ["Destined", "Expendable", "Courtier", "Fallen", "Scorpion Clan"], [], 1, "(Draw a card after your Destined card enters play.)");
        Shape(Hiromi, "Yoritomo Hiromi", "Mantis", 14, 5, 5, 3, 5, ["Kensai", "Loyal", "Naval", "Unique", "Clan Champion", "Experienced", "Mantis Clan", "Samurai", "The Growing Storm", "Thunder"], [WlPersGates2Abilities.HiromiAttack], 0, "Battle, ");
        foreach (var id in new[] { WlPersGates2Abilities.KatagiEquip, WlPersGates2Abilities.TsuraoWeaken, WlPersGates2Abilities.InvestTwo, WlPersGates2Abilities.KodanshiBow, WlPersGates2Abilities.ChokichiBow,
            WlPersGates2Abilities.NobukaiWeaken, WlPersGates2Abilities.HiromiAttack, "hiroto-bow", "hatsuto-weaken" }) Check(AbilityCatalog.All.ContainsKey(id), id + " is catalogued");
        Check(ids.All(id => { var c = CardCatalog.Get(id); return (c.Abilities ?? []).Length > 0 || (c.EntryHonorLoss ?? 0) > 0 || c.Keywords!.Contains("Expendable"); }), "no card of the wave is blank");
    }

    // ---- Katagi ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KatagiEquip()
    {
        var s = Table(Phase.Battle, 0);
        var katagi = Add(s, Katagi, 0, Zone.Battlefield, province: 0);
        Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var mine = Add(s, Weapon, 0, Zone.FateDiscard); var theirs = Add(s, Weapon, 1, Zone.FateDiscard);
        var unique = Kw(Add(s, Weapon, 0, Zone.FateDiscard), "Unique"); var notWeapon = Add(s, "training-item", 0, Zone.FateDiscard);
        var farm = Add(s, "training-farm", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, WlPersGates2Abilities.KatagiEquip, katagi.Id).TargetIds!;
        Check(targets.SequenceEqual([mine.Id]), "only a non-Unique Weapon in his controller's discard pile: " + string.Join(",", targets));
        var command = Offer(e, 0, WlPersGates2Abilities.KatagiEquip, katagi.Id, mine.Id) with { SourceIds = [farm.Id] };
        foreach (var bad in new[] { theirs.Id, unique.Id, notWeapon.Id }) RejectedWithoutChange(e, command with { TargetId = bad });
        Accept(e, command);
        var w = e.State.Cards[mine.Id];
        Check(w.Zone == Zone.Attached && w.AttachedTo == katagi.Id, "attached to Katagi");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.CardId == mine.Id), "its cost went through the Gold flow");
        // Away from the battle (a Battle action needs a unit at the battlefield) he may not use it.
        var away = Table(Phase.Battle, 0);
        Add(away, Katagi, 0, Zone.Home); Add(away, Crane, 0, Zone.Battlefield, province: 0); Add(away, Lion, 1, Zone.Battlefield, province: 0); Add(away, Weapon, 0, Zone.FateDiscard); Add(away, "training-farm", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(away), 0, WlPersGates2Abilities.KatagiEquip), "Katagi at home: not a Battle action he can take");
    }

    // ---- Rukia ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RukiaBow()
    {
        var s = Table(Phase.Battle, 0);
        var rukia = Add(s, Rukia, 0, Zone.Battlefield, province: 0);
        var bare = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var armed = Add(s, Lion, 1, Zone.Battlefield, province: 0); Add(s, Weapon, 1, Zone.Attached, province: 0, parent: armed.Id);
        var host = Add(s, Lion, 1, Zone.Battlefield, province: 0); var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var own = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "hiroto-bow", rukia.Id).TargetIds!;
        Check(targets.Contains(bare.Id) && targets.Contains(follower.Id) && !targets.Contains(armed.Id) && !targets.Contains(own.Id), "bare enemy cards only: " + string.Join(",", targets));
        RejectedWithoutChange(e, Offer(e, 0, "hiroto-bow", rukia.Id, bare.Id) with { TargetId = armed.Id });
        RejectedWithoutChange(e, Offer(e, 0, "hiroto-bow", rukia.Id, bare.Id) with { TargetId = own.Id });
        var bow = MatchEngine.FromTestState(s);
        Accept(bow, Offer(bow, 0, "hiroto-bow", rukia.Id, bare.Id)); CloseWindows(bow);
        Check(bow.State.Cards[bare.Id].Bowed && bow.State.Cards[bare.Id].Zone == Zone.Battlefield, "a bare Personality is bowed, not destroyed");
        var kill = MatchEngine.FromTestState(s);
        Accept(kill, Offer(kill, 0, "hiroto-bow", rukia.Id, follower.Id)); CloseWindows(kill);
        Check(kill.State.Cards[follower.Id].Zone != Zone.Attached && !kill.State.Cards[host.Id].Bowed, "an attachment is destroyed; its host is untouched");
        // #748: the bow and the destruction are separate effects. A destruction that is negated (Gift Armor protects itself) still leaves the attachment bowed.
        var gift = CardCatalog.Cards.Values.First(c => c.Effect == "gift-armor");
        var s2 = Table(Phase.Battle, 0);
        var rukia2 = Add(s2, Rukia, 0, Zone.Battlefield, province: 0);
        var host2 = Add(s2, Lion, 1, Zone.Battlefield, province: 0); var armor = Add(s2, gift.Id, 1, Zone.Attached, province: 0, parent: host2.Id);
        var keep = MatchEngine.FromTestState(s2);
        Accept(keep, Offer(keep, 0, "hiroto-bow", rukia2.Id, armor.Id)); CloseWindows(keep);
        Check(keep.State.Cards[armor.Id].Zone == Zone.Attached && keep.State.Cards[armor.Id].Bowed, "a negated destruction does not undo the attachment's bow");
    }

    // ---- Tsurao, Yoshie ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TsuraoWeaken()
    {
        var s = Table(Phase.Battle, 0);
        var tsurao = Add(s, Tsurao, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0); var host = Add(s, Lion, 1, Zone.Battlefield, province: 0); var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var own = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, WlPersGates2Abilities.TsuraoWeaken, tsurao.Id).TargetIds!;
        Check(targets.Contains(enemy.Id) && targets.Contains(follower.Id) && !targets.Contains(own.Id) && !targets.Contains(tsurao.Id), "enemy Personalities and Followers only: " + string.Join(",", targets));
        RejectedWithoutChange(e, Offer(e, 0, WlPersGates2Abilities.TsuraoWeaken, tsurao.Id, enemy.Id) with { TargetId = own.Id });
        var before = ForceOf(e, enemy);
        Accept(e, Offer(e, 0, WlPersGates2Abilities.TsuraoWeaken, tsurao.Id, enemy.Id)); CloseWindows(e);
        Eq(ForceOf(e, enemy), before - 2, "-2F");
    }

    private static void YoshieWeaken()
    {
        var s = Table(Phase.Battle, 0);
        var yoshie = Add(s, Yoshie, 0, Zone.Battlefield, province: 0);
        var infantry = Add(s, Lion, 1, Zone.Battlefield, province: 0); var cavalry = Kw(Add(s, Lion, 1, Zone.Battlefield, province: 0), "Cavalry");
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "hatsuto-weaken", yoshie.Id).TargetIds!;
        Check(targets.Contains(infantry.Id) && !targets.Contains(cavalry.Id), "an Infantry (non-Cavalry) enemy only: " + string.Join(",", targets));
        RejectedWithoutChange(e, Offer(e, 0, "hatsuto-weaken", yoshie.Id, infantry.Id) with { TargetId = cavalry.Id });
        var before = ForceOf(e, infantry);
        Accept(e, Offer(e, 0, "hatsuto-weaken", yoshie.Id, infantry.Id)); CloseWindows(e);
        Eq(ForceOf(e, infantry), before - 3, "-3F");
        // Entering play: lose 1 Honor, and Destined draws a card.
        var (p, cardId) = Recruit(Yoshie, 7);
        var honor = Batch336Support.Honor(p, 0); var hand = p.State.Players[0].Hand.Count;
        Accept(p, new GameCommand("purchase", 0, p.Version, CardId: cardId, SourceIds: p.PaymentOptions(0, 7).First().SourceIds));
        CloseAllWindows(p);
        Eq(Batch336Support.Honor(p, 0), honor - 1, "lost 1 Honor"); Eq(p.State.Players[0].Hand.Count, hand + 1, "Destined drew a card");
    }

    // ---- Invest -----------------------------------------------------------------------------------------------------------------------------------------------------------

    // The economy scenario with plenty of Gold, the province card replaced by `definition`, ready to be purchased for `cost`.
    private static (MatchEngine Engine, string CardId) Recruit(string definition, int cost)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 10; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = definition; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Check(e.PaymentOptions(0, cost).Any(), "the table can pay " + cost);
        return (e, cardId);
    }

    private static void CloseAllWindows(MatchEngine e) { for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }

    private static void InvestTokens()
    {
        foreach (var (def, cost) in new[] { (Nigawa, 4), (Sayaka, 5) })
        {
            var (e, id) = Recruit(def, cost);
            var force = 0;
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: id, SourceIds: e.PaymentOptions(0, cost).First().SourceIds));
            Eq(e.State.PendingChoice?.Type, "entryReaction", def + ": the entering-play window is open");
            force = ForceOf(e, e.State.Cards[id]);
            RejectedWithoutChange(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: WlPersGates2Abilities.InvestTwo, SourceIds: []));
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: WlPersGates2Abilities.InvestTwo, SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
            Eq(ForceOf(e, e.State.Cards[id]), force + 2, def + ": two +1F tokens");
            Check(e.State.Cards[id].Tokens.Count(t => t.Type == "Invest") == 2, "the tokens are Invest tokens");
            // Declining the window leaves him with his printed Force.
            var (f, id2) = Recruit(def, cost);
            Accept(f, new GameCommand("purchase", 0, f.Version, CardId: id2, SourceIds: f.PaymentOptions(0, cost).First().SourceIds));
            var printed = ForceOf(f, f.State.Cards[id2]);
            CloseAllWindows(f);
            Eq(ForceOf(f, f.State.Cards[id2]), printed, def + ": declined");
        }
    }

    // ---- Kodanshi ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KodanshiBow()
    {
        MatchEngine Build(out CardInstance kodanshi, out CardInstance ninja, out CardInstance bowedNinja, out CardInstance plainEnemy, out CardInstance fallen, out CardInstance fudo, out CardInstance enemyNinja)
        {
            var s = Table(Phase.Action, 0);
            kodanshi = Add(s, Kodanshi, 0, Zone.Home);
            ninja = Kw(Add(s, Crane, 0, Zone.Home), "Ninja"); bowedNinja = Kw(Add(s, Crane, 0, Zone.Home, bowed: true), "Ninja");
            plainEnemy = Add(s, Lion, 1, Zone.Home); fallen = Kw(Add(s, Lion, 1, Zone.Home), "Fallen"); fudo = Kw(Add(s, Lion, 1, Zone.Home), "Fudo");
            enemyNinja = Kw(Add(s, Lion, 1, Zone.Home), "Ninja");
            return MatchEngine.FromTestState(s);
        }
        GameCommand Use(MatchEngine e, CardInstance k, string ninjaId, string targetId) =>
            Batch336Support.Use(e, 0, WlPersGates2Abilities.KodanshiBow, k.Id, null, null, Batch336Support.Pick("ninja", ninjaId), Batch336Support.Pick("target", targetId));

        var e = Build(out var kodanshi, out var ninja, out var bowedNinja, out var plain, out var fallen, out var fudo, out var enemyNinja);
        RejectedWithoutChange(e, Use(e, kodanshi, bowedNinja.Id, plain.Id));
        RejectedWithoutChange(e, Use(e, kodanshi, enemyNinja.Id, plain.Id));
        RejectedWithoutChange(e, Use(e, kodanshi, plain.Id, fallen.Id));
        Accept(e, Use(e, kodanshi, ninja.Id, plain.Id));
        Check(e.State.Cards[ninja.Id].Bowed && e.State.Cards[plain.Id].Bowed, "both bowed");
        Check(e.State.Cards[plain.Id].StraightenLockedTurn != e.State.TurnNumber, "an ordinary Personality is only bowed");
        foreach (var locked in new[] { "fallen", "fudo" })
        {
            var f = Build(out var k2, out var n2, out _, out _, out var fal2, out var fu2, out _);
            var victim = locked == "fallen" ? fal2 : fu2;
            Accept(f, Use(f, k2, n2.Id, victim.Id));
            Check(f.State.Cards[victim.Id].Bowed, locked + ": bowed");
            Eq(f.State.Cards[victim.Id].StraightenLockedTurn, f.State.TurnNumber, locked + ": will not straighten this turn");
        }
    }

    // ---- Chokichi ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ChokichiBow()
    {
        MatchEngine Build(bool follower, out CardInstance chokichi, out CardInstance spider, out CardInstance bare, out CardInstance armed, out CardInstance own, out CardInstance enemyFollower)
        {
            var s = Table(Phase.Battle, 0);
            chokichi = Add(s, Chokichi, 0, Zone.Battlefield, province: 0);
            if (follower) Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: chokichi.Id);
            spider = Kw(Add(s, Lion, 1, Zone.Battlefield, province: 0), "Spider Clan"); Add(s, Weapon, 1, Zone.Attached, province: 0, parent: spider.Id);
            bare = Add(s, Lion, 1, Zone.Battlefield, province: 0);
            armed = Add(s, Lion, 1, Zone.Battlefield, province: 0); Add(s, Weapon, 1, Zone.Attached, province: 0, parent: armed.Id);
            var host = Add(s, Lion, 1, Zone.Battlefield, province: 0); enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
            own = Add(s, Crane, 0, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(false, out var chokichi, out var spider, out var bare, out var armed, out var own, out var follower);
        var plain = Enabled(e, 0, WlPersGates2Abilities.ChokichiBow, chokichi.Id).TargetIds!;
        Check(plain.Contains(spider.Id) && !plain.Contains(bare.Id) && !plain.Contains(armed.Id) && !plain.Contains(own.Id) && !plain.Contains(follower.Id), "without a Follower: a Spider Clan Personality only: " + string.Join(",", plain));
        RejectedWithoutChange(e, Offer(e, 0, WlPersGates2Abilities.ChokichiBow, chokichi.Id, spider.Id) with { TargetId = bare.Id });
        Accept(e, Offer(e, 0, WlPersGates2Abilities.ChokichiBow, chokichi.Id, spider.Id)); CloseWindows(e);
        Check(e.State.Cards[spider.Id].Bowed, "the Spider Clan Personality is bowed even with attachments");
        var f = Build(true, out var c2, out var spider2, out var bare2, out var armed2, out var own2, out var follower2);
        var wide = Enabled(f, 0, WlPersGates2Abilities.ChokichiBow, c2.Id).TargetIds!;
        Check(wide.Contains(spider2.Id) && wide.Contains(bare2.Id) && wide.Contains(follower2.Id) && !wide.Contains(armed2.Id) && !wide.Contains(own2.Id), "with a Follower: also an enemy card without attachments: " + string.Join(",", wide));
        Accept(f, Offer(f, 0, WlPersGates2Abilities.ChokichiBow, c2.Id, bare2.Id)); CloseWindows(f);
        Check(f.State.Cards[bare2.Id].Bowed, "a bare enemy Personality is bowed");
    }

    // ---- entry traits -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void EntryTraits()
    {
        foreach (var (def, cost, draws) in new[] { (Gombei, 4, false), (Takashi, 5, true) })
        {
            var (e, id) = Recruit(def, cost);
            var honor = Batch336Support.Honor(e, 0); var hand = e.State.Players[0].Hand.Count;
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: id, SourceIds: e.PaymentOptions(0, cost).First().SourceIds));
            CloseAllWindows(e);
            Eq(Batch336Support.Honor(e, 0), honor - 1, def + ": lost 1 Honor on entering play");
            Eq(e.State.Players[0].Hand.Count, hand + (draws ? 1 : 0), def + ": Destined draw");
        }
        // Expendable (Daisuki, Takashi, Tsurao) is the engine's keyword: the draw on destruction is its own, pinned by the keyword on the published card.
        foreach (var id in new[] { Daisuki, Takashi, Tsurao }) Check(CardCatalog.Get(id).Keywords!.Contains("Expendable"), id + " is Expendable");
    }

    // ---- Nobukai ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NobukaiWeaken()
    {
        // Your turn: offered with no Courtier.
        var s = Table(Phase.Action, 0);
        var nobukai = Add(s, Nobukai, 0, Zone.Home); var enemy = Add(s, Lion, 1, Zone.Home); var own = Add(s, Crane, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, WlPersGates2Abilities.NobukaiWeaken, nobukai.Id).TargetIds!;
        Check(targets.Contains(enemy.Id) && targets.Contains(own.Id), "any Personality: " + string.Join(",", targets));
        var before = ForceOf(e, enemy);
        Accept(e, Offer(e, 0, WlPersGates2Abilities.NobukaiWeaken, nobukai.Id, enemy.Id));
        Eq(ForceOf(e, enemy), before - 2, "-2F");
        // Not your turn, no Courtier: refused with no change. With a Courtier: allowed.
        var t = Table(Phase.Action, 1); t.PriorityPlayerId = 0;
        var n2 = Add(t, Nobukai, 0, Zone.Home); var target = Add(t, Lion, 1, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        Check(!CanUse(f, 0, WlPersGates2Abilities.NobukaiWeaken, n2.Id), "another player's turn and no Courtier: not offered");
        RejectedWithoutChange(f, Batch336Support.Use(f, 0, WlPersGates2Abilities.NobukaiWeaken, n2.Id, target.Id));
        var u = Table(Phase.Action, 1); u.PriorityPlayerId = 0;
        var n3 = Add(u, Nobukai, 0, Zone.Home); var t3 = Add(u, Lion, 1, Zone.Home); Kw(Add(u, Crane, 0, Zone.Home), "Courtier");
        var g = MatchEngine.FromTestState(u);
        var force3 = ForceOf(g, t3);
        Accept(g, Offer(g, 0, WlPersGates2Abilities.NobukaiWeaken, n3.Id, t3.Id));
        Eq(ForceOf(g, t3), force3 - 2, "with a Courtier on another player's turn: -2F");
    }

    // ---- Hiromi -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HiromiAttack()
    {
        MatchEngine Build(out CardInstance hiromi, out CardInstance target)
        {
            var s = Table(Phase.Battle, 0);
            for (var i = 0; i < 3; i++) Add(s, "training-marketplace", 0, Zone.Home);
            hiromi = Add(s, Hiromi, 0, Zone.Battlefield, province: 0);
            target = Add(s, Lion, 1, Zone.Battlefield, province: 0); Batch336Support.Force(target, -2);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var hiromi, out var target);
        Eq(ForceOf(e, target), 2, "the target's Force is 2");
        var option = Enabled(e, 0, WlPersGates2Abilities.HiromiAttack, hiromi.Id);
        var quote = option.Gold!;
        Eq(quote.Kind, GoldCostKind.Variable);
        Check((quote.MaxAmount ?? 0) >= 2, "at least 2 Gold can be paid");
        GameCommand Pay(MatchEngine m, int amount) { var q = Enabled(m, 0, WlPersGates2Abilities.HiromiAttack, hiromi.Id); return q.Command! with { TargetId = target.Id, GoldAmount = amount, SourceIds = amount == 0 ? [] : q.Gold!.PaymentsByAmount![amount][0].SourceIds }; }
        var low = Build(out hiromi, out target);
        Accept(low, Pay(low, 1)); CloseWindows(low);
        Check(low.State.Cards[target.Id].Zone == Zone.Battlefield, "Ranged 1 does not destroy a Force 2 Personality");
        var high = Build(out hiromi, out target);
        Accept(high, Pay(high, 2)); CloseWindows(high);
        Eq(high.State.Cards[target.Id].Zone, Zone.Dead, "Ranged 2 destroys a Force 2 Personality");
        Check(high.State.Events.Any(x => x.Type == "payment"), "the Gold was paid");
    }
}
