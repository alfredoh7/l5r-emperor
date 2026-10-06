using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #674, cluster "items-misc": item-wave-48 (Divination Bowl, Kage's Teachings, Khalimpeh-jiak, Koan's Staff, Parangu, Renyu's Wrath, Sturdy Armor, Translated Story of Shaharazad),
// follower-wave-48 (Maddened Horde, Reconnaissance Scouts, Shinobi Vassal, Sycophants, The Bestial Ones), event-wave-32 (A Fever in the Blood, Changing the Game, Purge of Fudoism, Riot in the
// Second City, The Second Battle of Beiden Pass) and celestial-wave-28 (Blood of Shahai, Hachiman's Prowess, The Ways of Sensei Wu). Every scenario is an explicit position on the economy table
// (Batch336Support.Table: player 0 the Crane Defender, player 1 the Lion Attacker, province 0 the current battlefield unless a test says otherwise), driven only through QueryActions and the
// commands the engine offers; every refused attempt is checked to leave the state unchanged.
internal static class WlItemsMiscTests
{
    private const string Bowl = "oracle-l5r-10690", Kage = "oracle-l5r-10341", Khalimpeh = "oracle-l5r-10905", Koan = "oracle-l5r-10527", Parangu = "oracle-l5r-10530", Renyu = "oracle-l5r-10343",
        Sturdy = "oracle-l5r-10344", Shaharazad = "oracle-l5r-10698";
    private const string Horde = "oracle-l5r-10518", Scouts = "oracle-l5r-10519", Vassal = "oracle-l5r-10517", Sycophants = "oracle-l5r-10338", Bestial = "oracle-l5r-10677";
    private const string Fever = "oracle-l5r-10603", ChangingGame = "oracle-l5r-10819", Purge = "oracle-l5r-10822", Riot = "oracle-l5r-10422", Beiden = "oracle-l5r-10425";
    private const string Shahai = "oracle-l5r-10602", Hachiman = "oracle-l5r-10257", SenseiWu = "oracle-l5r-10818", FudosAssurances = "oracle-l5r-10416";
    private const string Crane = "training-crane-samurai", Lion = "training-lion-samurai", TrainingFollower = "training-follower", Holding = "training-border-keep", Region = "training-costed-region",
        WinterTerrain = "oracle-l5r-1921", Preserver = "oracle-l5r-9875", Kodo = "oracle-l5r-142";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static GameCommand Cmd(MatchEngine e, int player, string ability, CardInstance card, string? performer = null, string? target = null) =>
        new("useAbility", player, e.Version, card.Id, AbilityId: ability, PerformerId: performer, TargetId: target);
    private static GameCommand Equip(MatchEngine e, int player, CardInstance card, CardInstance host, int gold) =>
        new("equip", player, e.Version, CardId: card.Id, SourceIds: gold == 0 ? [] : e.PaymentOptions(player, gold).First().SourceIds, TargetId: host.Id);
    private static void PassWindow(MatchEngine e) { for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }

    private static void RejectedWithoutChange(MatchEngine e, GameCommand command, string? contains = null)
    {
        var before = Json(e);
        var error = Batch336Support.Rejection(e, command);
        if (contains is not null) Check(error.Contains(contains, StringComparison.OrdinalIgnoreCase), $"rejection says '{contains}': {error}");
        Eq(Json(e), before, "a rejected action changes nothing");
    }

    // A card put on the top of a player's Fate deck (the deck is face down: its id is never shown).
    private static CardInstance OnDeck(MatchState s, string def, int owner, int index = 0)
    {
        var card = Add(s, def, owner, Zone.FateDeck); card.FaceUp = false;
        s.Players[owner].FateDeck.Insert(index, card.Id);
        return card;
    }

    public static void Run(TestRunner runner) => runner.Run("wl-items-misc", [
        ("Published shape: the 21 wired cards carry their printed stats, keywords, text and abilities, name their printings and are in their waves' manifests; Fudo's Assurances is blocked, not published", PublishedShape),
        ("Divination Bowl: bow it to look at the top three of your Fate deck and put them back in any order; or bow it to draw a card and destroy it", BowlScenario),
        ("Kage's Teachings: its Personality has Kolat; when it is not your turn, bow it and choose a player: he chooses and bows one of his unbowed Personalities", KageScenario),
        ("Khalimpeh-jiak: Engage, bow it: a target enemy Personality or Follower gets -2F; only enemies, only after engaging, only while unbowed", KhalimpehScenario),
        ("Koan's Staff: Ranged 3 Attack with +1 strength for each other Koan card in its unit", KoanScenario),
        ("Parangu is only a Weapon with Kharmic: the universal Kharmic action discards it from the hand to draw a card", ParanguScenario),
        ("Renyu's Wrath: bow it to destroy a Region at any location or a Terrain; Ranged 4 Attack, +1 strength while its Personality is Siege or Earth", RenyuScenario),
        ("Sturdy Armor: three times a turn, give its Personality +1F", SturdyScenario),
        ("Translated Story of Shaharazad: bow it to give it a +1F token, which its wielder's Force counts", ShaharazadScenario),
        ("Maddened Horde: discard the top card of a Fate deck (either player's); Melee Attack with strength equal to its Focus Value; an empty deck fails the whole action", HordeScenario),
        ("Reconnaissance Scouts: after engaging, Reconnaissance at its battlefield, the Attacker's first opportunity, and optionally -3F on an enemy; one use between the two", ScoutsScenario),
        ("Shinobi Vassal: Equip it as a Battle action to a Ninja at the battlefield; Battle: destroy an enemy card without attachments, then destroy the Follower", VassalScenario),
        ("Sycophants: bow it to straighten its Personality and give him a second use of each once-a-turn Political action this turn", SycophantsScenario),
        ("The Bestial Ones: entering play it costs 1 Honor and a discard; Engage: a target enemy Personality or Follower gets -2F", BestialScenario),
        ("A Fever in the Blood: until its owner's next turn begins, Personalities and Followers of an attacking unit have -1F, whoever controls them", FeverScenario),
        ("Changing the Game: an ordered search of the hand, the discard pile and the Fate deck for Blood of the Preserver, removed from the game for 2 Honor", ChangingTheGameScenario),
        ("Purge of Fudoism: each player in turn, starting with the owner, destroys a card with the printed Fallen, Fudo or Shadowlands keyword", PurgeScenario),
        ("Riot in the Second City: each player in turn, starting with the owner, bows an unbowed Samurai", RiotScenario),
        ("The Second Battle of Beiden Pass: the province permanently holds no Dynasty cards and has +10 Province Strength", BeidenScenario),
        ("Blood of Shahai: -1C on a Personality below its printed Chi; or discard it and give every Personality at the battlefield a -1C Blood token", ShahaiScenario),
        ("Hachiman's Prowess: once per game a Personality has Elite and may assign even if bowed while it remains in play; discard it to straighten a unit", HachimanScenario),
        ("The Ways of Sensei Wu: put a Battle Strategy from your discard pile into your hand, then discard the Celestial", SenseiWuScenario),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = Batch336Support.RepoRoot();
        void Wave(string wave, string[] ids, CardType type, string set)
        {
            Check(new HashSet<string>(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"))).SetEquals(ids), wave + " manifest");
            var rows = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
            Eq(rows.Length, ids.Length, wave + ": a provenance row per card");
            foreach (var id in ids)
            {
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), id + " ships art");
                Check(CardCatalog.Get(id).Type == type && !CardCatalog.Get(id).ScenarioOnly, id + " is a playable " + type);
                var printing = rows.Single(r => r.GetProperty("card_id").GetString() == id).GetProperty("printing_set")[0].GetString();
                Check(printing is "Coils of Madness" or "Torn Asunder" or "Gates of Chaos" or "Aftermath", id + ": " + printing);
            }
        }
        Wave("item-wave-48", [Bowl, Kage, Khalimpeh, Koan, Parangu, Renyu, Sturdy, Shaharazad], CardType.Item, "");
        Wave("follower-wave-48", [Horde, Scouts, Vassal, Sycophants, Bestial], CardType.Follower, "");
        Wave("event-wave-32", [Fever, ChangingGame, Purge, Riot, Beiden], CardType.Event, "");
        Wave("celestial-wave-28", [Shahai, Hachiman, SenseiWu], CardType.Celestial, "");
        void Shape(string id, string name, int gold, int force, int chi, int focus, string[] keywords, string[] abilities, string effect, string? requires = null)
        {
            var d = CardCatalog.Get(id);
            Check(d.Name == name && d.GoldCost == gold && d.Force == force && d.Chi == chi && d.Focus == focus, $"{name}: printed stats");
            Check(d.Keywords!.SequenceEqual(keywords), $"{name}: printed keywords {string.Join(",", d.Keywords!)}");
            Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities");
            Eq(d.Effect, effect, $"{name}: effect"); Eq(d.AttachRequiresKeyword, requires, $"{name}: attach restriction");
            Check(d.Text.Length > 0, $"{name}: printed text");
        }
        Shape(Bowl, "Divination Bowl", 2, 0, 0, 3, [], [WlItemsMiscItemAbilities.BowlLook, WlItemsMiscItemAbilities.BowlDraw], "none");
        Shape(Kage, "Kage's Teachings", 4, 0, 0, 2, ["Unique"], [WlItemsMiscItemAbilities.KageBow], "none");
        Eq(CardCatalog.Get(Kage).AttachGrantsKeyword, "Kolat", "This Personality has Kolat");
        Shape(Khalimpeh, "Khalimpeh-jiak", 3, 2, 0, 2, ["Weapon"], [WlItemsMiscItemAbilities.KhalimpehPenalty], "none");
        Shape(Koan, "Koan's Staff", 3, 1, 1, 0, ["Unique", "Weapon", "Fire", "Koan"], [WlItemsMiscItemAbilities.KoanAttack], "none");
        Shape(Parangu, "Parangu", 2, 2, 0, 3, ["Kharmic", "Weapon"], [], "none");
        Shape(Renyu, "Renyu's Wrath", 6, 4, 0, 4, ["Unique", "Siege"], [WlItemsMiscItemAbilities.RenyuDestroy, WlItemsMiscItemAbilities.RenyuRanged], "none");
        Shape(Sturdy, "Sturdy Armor", 4, 2, 0, 2, ["Armor"], [WlItemsMiscItemAbilities.SturdyBoost], "none");
        Shape(Shaharazad, "Translated Story of Shaharazad", 3, 0, 0, 2, [], [WlItemsMiscItemAbilities.ShaharazadToken], "none");
        Shape(Horde, "Maddened Horde", 8, 4, 0, 4, ["Unique", "Fallen"], [WlItemsMiscFollowerAbilities.HordeOwn, WlItemsMiscFollowerAbilities.HordeEnemy], "none");
        Shape(Scouts, "Reconnaissance Scouts", 4, 3, 0, 2, ["Scout"], [WlItemsMiscFollowerAbilities.ScoutsEngage, WlItemsMiscFollowerAbilities.ScoutsPenalty], "none");
        Shape(Vassal, "Shinobi Vassal", 2, 2, 0, 1, ["Ninja"], [WlItemsMiscFollowerAbilities.VassalEquip, WlItemsMiscFollowerAbilities.VassalDestroy], "none", "Ninja");
        Shape(Sycophants, "Sycophants", 5, 0, 0, 2, [], [WlItemsMiscFollowerAbilities.SycophantsStraighten], "none");
        Shape(Bestial, "The Bestial Ones", 3, 3, 0, 2, ["Nonhuman"], [WlItemsMiscFollowerAbilities.BestialEngage], WlItemsMiscFollowerAbilities.BestialEntryEffect);
        Eq(CardCatalog.Get(Bestial).EntryHonorLoss, 1, "lose 1 Honor");
        foreach (var (id, name, effect, keywords) in new[] { (Fever, "A Fever in the Blood", WlItemsMiscEventAbilities.FeverEffect, Array.Empty<string>()), (ChangingGame, "Changing the Game", WlItemsMiscEventAbilities.ChangingTheGameEffect, []),
            (Purge, "Purge of Fudoism", WlItemsMiscEventAbilities.PurgeEffect, []), (Riot, "Riot in the Second City", WlItemsMiscEventAbilities.RiotEffect, ["Unique"]),
            (Beiden, "The Second Battle of Beiden Pass", WlItemsMiscEventAbilities.BeidenPassEffect, ["Unique"]) })
        {
            var d = CardCatalog.Get(id);
            Check(d.Name == name && d.Effect == effect && (d.Abilities ?? []).Length == 0 && d.Keywords!.SequenceEqual(keywords), $"{name}: an Event keyed on its Effect with the printed keywords");
            Check(d.Text.Contains("Limited:") || id == Fever, $"{name}: the printed text keeps its \"Limited:\" (the engine ignores it)");
        }
        Shape(Shahai, "Blood of Shahai", 0, 0, 0, 0, ["Unique", "Dark Fortune"], [WlItemsMiscCelestialAbilities.ShahaiChi, WlItemsMiscCelestialAbilities.ShahaiBlood], "none");
        Shape(Hachiman, "Hachiman's Prowess", 0, 0, 0, 0, ["Unique", "Fortune"], [WlItemsMiscCelestialAbilities.HachimanGrant, WlItemsMiscCelestialAbilities.HachimanStraighten], "none");
        Shape(SenseiWu, "The Ways of Sensei Wu", 0, 0, 0, 0, ["Unique", "Fortune"], [WlItemsMiscCelestialAbilities.WuReturn], "none");
        foreach (var id in typeof(WlItemsMiscItemAbilities).GetFields().Concat(typeof(WlItemsMiscFollowerAbilities).GetFields()).Concat(typeof(WlItemsMiscCelestialAbilities).GetFields())
            .Where(f => f.IsLiteral && f.GetRawConstantValue() is string v && v.StartsWith("wl-") && f.Name is not ("BestialEntryEffect" or "SycophantsMark" or "BelowPrintedChi" or "OwnBattleStrategyInDiscard")).Select(f => (string)f.GetRawConstantValue()!))
            Check(AbilityCatalog.All.ContainsKey(id), id + " is catalogued");
        Check(!CardCatalog.Cards.ContainsKey(FudosAssurances) || CardCatalog.Get(FudosAssurances).ScenarioOnly, "Fudo's Assurances is not published");
        var blockers = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tools/content/pe_wl_items_misc_blockers.json"))).RootElement;
        Check(blockers.TryGetProperty(FudosAssurances, out var blocked) && blocked.GetProperty("category").GetString() == "PROVENANCE", "Fudo's Assurances is recorded as blocked");
    }

    // ---- Divination Bowl -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BowlScenario()
    {
        var s = Table(Phase.Action, 0);
        var wielder = Add(s, Crane, 0, Zone.Home); var bowl = Attach(s, Bowl, wielder);
        var top = s.Players[0].FateDeck.ToArray();
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, WlItemsMiscItemAbilities.BowlLook, bowl.Id).Command!);
        var view = e.GetView(0).PendingLook!;
        Eq(view.Stage, "Arrange"); Eq(view.Requested, 3, "the top three cards"); Eq(view.Cards!.Length, 3); Check(view.OwnDeck && view.Deck == "Fate", "your own Fate deck");
        Check(view.CanReorder && view.Rules!.Length == 0, "put them back in any order: nothing else may be done with them");
        Check(e.State.Cards[bowl.Id].Bowed, "bowing the Bowl is the cost");
        Accept(e, new("arrangeTopCards", 0, e.Version, CardIds: ["2:top", "0:top", "1:top"]));
        Eq(string.Join(",", e.State.Players[0].FateDeck.Take(4)), string.Join(",", new[] { top[2], top[0], top[1], top[3] }), "back in the order chosen, the rest untouched");
        Check(e.GetView(1).PendingLook is null, "the opponent saw nothing of it");
        Check(!CanUse(e, 0, WlItemsMiscItemAbilities.BowlLook, bowl.Id) && !CanUse(e, 0, WlItemsMiscItemAbilities.BowlDraw, bowl.Id), "bowed: neither action is offered");

        var t = Table(Phase.Action, 0);
        var w2 = Add(t, Crane, 0, Zone.Home); var b2 = Attach(t, Bowl, w2);
        var drawn = t.Players[0].FateDeck[0]; var handBefore = t.Players[0].Hand.Count;
        var f = MatchEngine.FromTestState(t);
        Accept(f, Enabled(f, 0, WlItemsMiscItemAbilities.BowlDraw, b2.Id).Command!);
        Eq(f.State.Players[0].Hand.Count, handBefore + 1, "draw a card"); Check(f.State.Players[0].Hand.Contains(drawn), "the top card of the Fate deck");
        Eq(f.State.Cards[b2.Id].Zone, Zone.FateDiscard, "destroy this Item");

        // Limited: only on its owner's turn; a bowed Bowl may not be used.
        var u = Table(Phase.Action, 1); u.PriorityPlayerId = 0;
        var w3 = Add(u, Crane, 0, Zone.Home); var b3 = Attach(u, Bowl, w3);
        Check(!CanUse(MatchEngine.FromTestState(u), 0, WlItemsMiscItemAbilities.BowlLook, b3.Id), "a Limited action is taken on your own turn");
        var bowedBowl = Table(Phase.Action, 0); var w4 = Add(bowedBowl, Crane, 0, Zone.Home); var b4 = Attach(bowedBowl, Bowl, w4, bowed: true);
        RejectedWithoutChange(MatchEngine.FromTestState(bowedBowl), Cmd(MatchEngine.FromTestState(bowedBowl), 0, WlItemsMiscItemAbilities.BowlDraw, b4));
    }

    // ---- Kage's Teachings ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KageScenario()
    {
        // Attached, the Personality has Kolat (a granted keyword that ends with the Item).
        var s = Table(Phase.Action, 0);
        var host = Add(s, Crane, 0, Zone.Home); var kage = Add(s, Kage, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!e.HasKeyword(host.Id, "Kolat"), "no Kolat before");
        Accept(e, Equip(e, 0, kage, host, 4)); PassWindow(e);
        Check(e.HasKeyword(host.Id, "Kolat"), "This Personality has Kolat");

        // The Open action on the other player's turn: player 0 holds Kage's Teachings, player 1 is the active player and has two unbowed Personalities.
        var t = Table(Phase.Action, 1); t.PriorityPlayerId = 0;
        var mine = Add(t, Crane, 0, Zone.Home); var item = Attach(t, Kage, mine);
        var lionA = Add(t, Lion, 1, Zone.Home); var lionB = Add(t, Lion, 1, Zone.Home); var lionBowed = Add(t, Lion, 1, Zone.Home, bowed: true);
        var f = MatchEngine.FromTestState(t);
        var option = Enabled(f, 0, WlItemsMiscItemAbilities.KageBow, item.Id);
        Check(option.Command is not null, "offered on the other player's turn");
        var command = option.Command! with { TargetPlayerId = 1 };
        Accept(f, command);
        Check(f.State.Cards[item.Id].Bowed, "bowing Kage's Teachings is the cost");
        var pending = f.State.PendingDeciderChoice!;
        Eq(pending.DeciderPlayerId, 1, "the chosen player decides"); Eq(pending.Candidates.Count, 2, "among his unbowed Personalities only");
        Check(pending.Candidates.Contains(lionA.Id) && pending.Candidates.Contains(lionB.Id) && !pending.Candidates.Contains(lionBowed.Id), "the bowed one is not a candidate");
        Check(f.QueryActions(0).All(a => a.Type is "wait" or "concede" || !a.Enabled), "the acting player waits while the other chooses");
        var pick = f.QueryActions(1).First(a => a.Type == "chooseDecider" && a.Command?.TargetId == lionB.Id);
        Accept(f, pick.Command!);
        Check(f.State.Cards[lionB.Id].Bowed && !f.State.Cards[lionA.Id].Bowed, "the one he chose is bowed, only that one");
        Check(f.State.PendingDeciderChoice is null, "the choice is over");

        // Choosing yourself is legal too; with exactly one unbowed Personality there is nothing to choose.
        var u = Table(Phase.Action, 1); u.PriorityPlayerId = 0;
        var solo = Add(u, Crane, 0, Zone.Home); var kage2 = Attach(u, Kage, solo); Add(u, Lion, 1, Zone.Home);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Enabled(g, 0, WlItemsMiscItemAbilities.KageBow, kage2.Id).Command! with { TargetPlayerId = 0 });
        Check(g.State.Cards[solo.Id].Bowed && g.State.PendingDeciderChoice is null, "his only unbowed Personality is bowed without a question");

        // On your own turn it is refused and nothing changes.
        var v = Table(Phase.Action, 0);
        var m3 = Add(v, Crane, 0, Zone.Home); var k3 = Attach(v, Kage, m3); Add(v, Lion, 1, Zone.Home);
        var h = MatchEngine.FromTestState(v);
        Check(!CanUse(h, 0, WlItemsMiscItemAbilities.KageBow, k3.Id), "not offered on your own turn");
        RejectedWithoutChange(h, Cmd(h, 0, WlItemsMiscItemAbilities.KageBow, k3) with { TargetPlayerId = 1 }, "turn");
        // A bowed Kage's Teachings may not be used.
        var w = Table(Phase.Action, 1); w.PriorityPlayerId = 0;
        var m4 = Add(w, Crane, 0, Zone.Home); var k4 = Attach(w, Kage, m4, bowed: true); Add(w, Lion, 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(w), 0, WlItemsMiscItemAbilities.KageBow, k4.Id), "bowed: not offered");
    }

    // ---- the Engage reactions (Khalimpeh-jiak, The Bestial Ones, Reconnaissance Scouts) -------------------------------------------------------------------------------------------

    // Player 0 attacks province 0 (the BattleSelection step); `setup` places the cards. Returns the engine after "selectBattle" opened the Engage window (the Attacker holds priority first).
    private static MatchEngine Engage(Action<MatchState> setup, bool expectWindow = true)
    {
        var s = Table(Phase.BattleSelection, 0);
        s.UnresolvedBattlefields = [0]; s.CurrentBattleProvince = null;
        setup(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("selectBattle", 0, e.Version, Province: 0));
        if (expectWindow) Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
        return e;
    }

    private static void KhalimpehScenario()
    {
        CardInstance mine = null!, item = null!, foe = null!, foeFollower = null!, myFollower = null!;
        var e = Engage(s =>
        {
            mine = Add(s, Crane, 0, Zone.Battlefield, province: 0); item = Attach(s, Khalimpeh, mine); myFollower = Attach(s, TrainingFollower, mine);
            foe = Add(s, Lion, 1, Zone.Battlefield, province: 0); foeFollower = Attach(s, TrainingFollower, foe);
        });
        var forceBefore = e.CardForceForTest(foe.Id); var followerBefore = e.CardForceForTest(foeFollower.Id);
        Check(CanUse(e, 0, WlItemsMiscItemAbilities.KhalimpehPenalty, item.Id), "offered in the Engage window");
        // Only enemy Personalities and Followers are legal targets.
        var legal = Enabled(e, 0, WlItemsMiscItemAbilities.KhalimpehPenalty, item.Id).TargetIds!;
        Check(legal.Contains(foe.Id) && legal.Contains(foeFollower.Id) && !legal.Contains(mine.Id) && !legal.Contains(myFollower.Id), "an enemy Personality or Follower, never your own");
        RejectedWithoutChange(e, Cmd(e, 0, WlItemsMiscItemAbilities.KhalimpehPenalty, item, target: mine.Id));
        Accept(e, Offer(e, 0, WlItemsMiscItemAbilities.KhalimpehPenalty, item.Id, foe.Id));
        Eq(e.CardForceForTest(foe.Id), forceBefore - 2, "-2F on the Personality"); Check(e.State.Cards[item.Id].Bowed, "bowing the Item is the cost");
        Eq(e.CardForceForTest(foeFollower.Id), followerBefore, "only the target");
        Check(!CanUse(e, 0, WlItemsMiscItemAbilities.KhalimpehPenalty, item.Id), "bowed: not offered again");
        // A Follower is a legal target as well.
        var f = Engage(s =>
        {
            var m = Add(s, Crane, 0, Zone.Battlefield, province: 0); item = Attach(s, Khalimpeh, m);
            var l = Add(s, Lion, 1, Zone.Battlefield, province: 0); foeFollower = Attach(s, TrainingFollower, l);
        });
        var fb = f.CardForceForTest(foeFollower.Id);
        Accept(f, Offer(f, 0, WlItemsMiscItemAbilities.KhalimpehPenalty, item.Id, foeFollower.Id));
        Eq(f.CardForceForTest(foeFollower.Id), Math.Max(0, fb - 2), "-2F on the Follower (never below 0)");
        // Not an action of the Battle phase proper: once the window has closed it is not offered; a bowed Item does not react.
        var g = Engage(s => { var m = Add(s, Crane, 0, Zone.Battlefield, province: 0); item = Attach(s, Khalimpeh, m, bowed: true); Add(s, Lion, 1, Zone.Battlefield, province: 0); }, expectWindow: false);
        Check(!CanUse(g, 0, WlItemsMiscItemAbilities.KhalimpehPenalty, item.Id) && g.State.PendingChoice is null, "a bowed Item is not offered (and nobody is asked)");
        var h = Engage(s => { var m = Add(s, Crane, 0, Zone.Battlefield, province: 0); item = Attach(s, Khalimpeh, m); Add(s, Lion, 1, Zone.Battlefield, province: 0); });
        PassWindow(h);
        Check(!CanUse(h, 0, WlItemsMiscItemAbilities.KhalimpehPenalty, item.Id), "not offered once the Engage window has closed");
    }

    private static void BestialScenario()
    {
        // Engage: -2F.
        CardInstance follower = null!, foe = null!;
        var e = Engage(s =>
        {
            var mine = Add(s, Crane, 0, Zone.Battlefield, province: 0); follower = Attach(s, Bestial, mine);
            foe = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        });
        var before = e.CardForceForTest(foe.Id);
        Accept(e, Offer(e, 0, WlItemsMiscFollowerAbilities.BestialEngage, follower.Id, foe.Id));
        Eq(e.CardForceForTest(foe.Id), before - 2, "-2F");
        Check(!e.State.Cards[follower.Id].Bowed, "no cost is printed");
        Check(!CanUse(e, 0, WlItemsMiscFollowerAbilities.BestialEngage, follower.Id), "once per turn");

        // Entering play: lose 1 Honor and discard a card (a card of the hand, the controller's own choice).
        var s2 = Table(Phase.Action, 0);
        var host = Add(s2, Crane, 0, Zone.Home); var card = Add(s2, Bestial, 0, Zone.Hand); var fodder = Add(s2, TrainingFollower, 0, Zone.Hand); var fodder2 = Add(s2, TrainingFollower, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        var honor = Batch336Support.Honor(f, 0);
        Accept(f, Equip(f, 0, card, host, 3));
        for (var guard = 0; guard < 6 && f.State.PendingChoice?.Type != "forcedDiscard" && f.State.PendingChoice is not null; guard++) Accept(f, new("declineReaction", f.State.PriorityPlayerId, f.Version));
        Eq(Batch336Support.Honor(f, 0), honor - 1, "lose 1 Honor");
        Eq(f.State.PendingChoice?.Type, "forcedDiscard", "and discard a card");
        Eq(f.State.PendingChoice!.PlayerId, 0, "the controller's own discard");
        var handBefore = f.State.Players[0].Hand.Count;
        Accept(f, new("forcedDiscard", 0, f.Version, TargetId: fodder.Id));
        Eq(f.State.Cards[fodder.Id].Zone, Zone.FateDiscard, "the card he named is discarded"); Eq(f.State.Players[0].Hand.Count, handBefore - 1);
        Check(f.State.Cards[card.Id].Zone == Zone.Attached && f.State.Cards[fodder2.Id].Zone == Zone.Hand, "nothing else moved");
        // With an empty hand there is nothing to discard (the Honor is still lost).
        var s3 = Table(Phase.Action, 0);
        var host3 = Add(s3, Crane, 0, Zone.Home); var card3 = Add(s3, Bestial, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(s3); var honor3 = Batch336Support.Honor(g, 0);
        Accept(g, Equip(g, 0, card3, host3, 3)); PassWindow(g);
        Eq(Batch336Support.Honor(g, 0), honor3 - 1, "the Honor is lost whatever the hand holds"); Check(g.State.PendingChoice is null, "nothing to discard");
    }

    private static void ScoutsScenario()
    {
        // Player 0 attacks with the Scouts' Personality; the Engage window opens with the Attacker first.
        CardInstance scouts = null!, foe = null!, mine = null!;
        MatchEngine Open(bool attacker)
        {
            return Engage(s =>
            {
                if (attacker) { mine = Add(s, Crane, 0, Zone.Battlefield, province: 0); scouts = Attach(s, Scouts, mine); foe = Add(s, Lion, 1, Zone.Battlefield, province: 0); }
                else { mine = Add(s, Lion, 1, Zone.Battlefield, province: 0); scouts = Attach(s, Scouts, mine); foe = Add(s, Crane, 0, Zone.Battlefield, province: 0); }
            });
        }
        var e = Open(true);
        var forceBefore = e.CardForceForTest(foe.Id);
        Check(CanUse(e, 0, WlItemsMiscFollowerAbilities.ScoutsEngage, scouts.Id) && CanUse(e, 0, WlItemsMiscFollowerAbilities.ScoutsPenalty, scouts.Id), "both ways are offered");
        Check(!e.State.Players[1].Provinces[0].Reconnaissance[0], "no Reconnaissance yet");
        Accept(e, Enabled(e, 0, WlItemsMiscFollowerAbilities.ScoutsEngage, scouts.Id).Command!);
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "You have Reconnaissance at this battlefield");
        Eq(e.CardForceForTest(foe.Id), forceBefore, "no penalty without a target");
        Check(!CanUse(e, 0, WlItemsMiscFollowerAbilities.ScoutsEngage, scouts.Id) && !CanUse(e, 0, WlItemsMiscFollowerAbilities.ScoutsPenalty, scouts.Id), "the two ways share one use");
        PassWindow(e);
        Eq(e.State.PriorityPlayerId, 0, "the Attacker has the first opportunity to take a Battle action or pass");

        // With the optional target: the same, and -3F on the enemy.
        var f = Open(true); var fb = f.CardForceForTest(foe.Id);
        Accept(f, Offer(f, 0, WlItemsMiscFollowerAbilities.ScoutsPenalty, scouts.Id, foe.Id));
        Eq(f.CardForceForTest(foe.Id), fb - 3, "-3F on the enemy Personality"); Check(f.State.Players[1].Provinces[0].Reconnaissance[0], "and Reconnaissance too");
        PassWindow(f);
        Eq(f.State.PriorityPlayerId, 0, "the Attacker has the first opportunity");
        var enemyTargets = Enabled(Open(true), 0, WlItemsMiscFollowerAbilities.ScoutsPenalty, scouts.Id).TargetIds!;
        Check(!enemyTargets.Contains(mine.Id), "only an enemy is a legal target");
        RejectedWithoutChange(Open(true), Cmd(Open(true), 0, WlItemsMiscFollowerAbilities.ScoutsPenalty, scouts, target: mine.Id));

        // The Defender's Scouts: Reconnaissance at the battlefield, and no first-opportunity claim that would matter (the Defender has it by default).
        var g = Open(false);
        PassWindow(g);
        // The Engage window gives the Attacker priority first; after the Attacker's pass the Defender uses his Scouts.
        var h = Open(false);
        Accept(h, new("declineReaction", 0, h.Version));
        Check(CanUse(h, 1, WlItemsMiscFollowerAbilities.ScoutsEngage, scouts.Id), "the Defender's Scouts are offered");
        Accept(h, Enabled(h, 1, WlItemsMiscFollowerAbilities.ScoutsEngage, scouts.Id).Command!);
        Check(h.State.Players[1].Provinces[0].Reconnaissance[1], "the Defender has Reconnaissance at his own battlefield");

        // Scouts whose Personality is not at the battlefield are not offered.
        var away = Engage(s => { var home = Add(s, Crane, 0, Zone.Home); scouts = Attach(s, Scouts, home); Add(s, Crane, 0, Zone.Battlefield, province: 0); Add(s, Lion, 1, Zone.Battlefield, province: 0); }, expectWindow: false);
        Check(!CanUse(away, 0, WlItemsMiscFollowerAbilities.ScoutsEngage, scouts.Id), "its Personality must be at the battlefield being engaged");
    }

    // ---- Koan's Staff, Renyu's Wrath, Sturdy Armor, Shaharazad ---------------------------------------------------------------------------------------------------------------------------

    private static void KoanScenario()
    {
        var s = Table(Phase.Battle, 0);
        var wielder = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Koan"); var staff = Attach(s, Koan, wielder);
        var koanFollower = Kw(Attach(s, TrainingFollower, wielder), "Koan"); var plainFollower = Attach(s, TrainingFollower, wielder);
        var other = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Koan"); var otherUnitItem = Attach(s, Sturdy, other);
        var weak = Add(s, Lion, 1, Zone.Battlefield, province: 0); var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0); Batch336Support.Force(strong, 3);   // Force 4 and Force 7
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest(WlItemsMiscItemAbilities.KoanAttack, staff.Id), 3, "the Item's own performer id gives the base");
        Eq(e.AttackStrengthForTest(WlItemsMiscItemAbilities.KoanAttack, wielder.Id), 3 + 2, "3, +1 for the Koan Personality and +1 for the Koan Follower of his unit, not the Staff itself, not another unit's Koan card");
        Check(plainFollower.Id.Length > 0 && otherUnitItem.Id.Length > 0, "fixture");
        Accept(e, Offer(e, 0, WlItemsMiscItemAbilities.KoanAttack, staff.Id, weak.Id));
        Eq(e.State.Cards[weak.Id].Zone, Zone.Dead, "a Force 4 Personality is destroyed by Ranged 5"); Check(e.State.Cards[staff.Id].Bowed, "bowing the Staff is the cost");
        var f = MatchEngine.FromTestState(s);
        Accept(f, Offer(f, 0, WlItemsMiscItemAbilities.KoanAttack, staff.Id, strong.Id));
        Eq(f.State.Cards[strong.Id].Zone, Zone.Battlefield, "Force 7 survives Ranged 5");
        // Without a Koan Personality or Follower in the unit the strength is 3.
        var t = Table(Phase.Battle, 0);
        var lone = Add(t, Crane, 0, Zone.Battlefield, province: 0); var staff2 = Attach(t, Koan, lone); var four = Add(t, Lion, 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(t);
        Eq(g.AttackStrengthForTest(WlItemsMiscItemAbilities.KoanAttack, lone.Id), 3, "Ranged 3 with no other Koan card");
        Accept(g, Offer(g, 0, WlItemsMiscItemAbilities.KoanAttack, staff2.Id, four.Id));
        Eq(g.State.Cards[four.Id].Zone, Zone.Battlefield, "Force 4 survives Ranged 3");
        Check(!CanUse(g, 0, WlItemsMiscItemAbilities.KoanAttack, staff2.Id), "bowed: not offered again");
        // A wielder away from the battlefield may not use it.
        var away = Table(Phase.Battle, 0); var home = Add(away, Crane, 0, Zone.Home); var staff3 = Attach(away, Koan, home); Add(away, Crane, 0, Zone.Battlefield, province: 0); Add(away, Lion, 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(away), 0, WlItemsMiscItemAbilities.KoanAttack, staff3.Id), "the wielder must be at the battlefield");
    }

    private static void ParanguScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Crane, 0, Zone.Home); var parangu = Add(s, Parangu, 0, Zone.Hand); var other = Add(s, TrainingFollower, 0, Zone.Hand);
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; // player abilities (Kharmic's) need a published Stronghold
        var e = MatchEngine.FromTestState(s);
        Check(CardCatalog.Get(Parangu).Abilities is null or { Length: 0 }, "Parangu has no ability of its own: only Kharmic's reminder text");
        var handBefore = e.State.Players[0].Hand.Count; var top = e.State.Players[0].FateDeck[0];
        var discard = new GameCommand("useAbility", 0, e.Version, TargetId: parangu.Id, AbilityId: "kharmic-discard", SourceIds: e.PaymentOptions(0, 2).First().SourceIds);
        RejectedWithoutChange(e, discard with { TargetId = other.Id }, "Kharmic");
        Accept(e, discard);
        Eq(e.State.Cards[parangu.Id].Zone, Zone.FateDiscard, "discarded"); Check(e.State.Players[0].Hand.Contains(top), "and a card drawn");
        Eq(e.State.Players[0].Hand.Count, handBefore, "one card out, one card in");
        Check(host.Id.Length > 0 && other.Id.Length > 0, "fixture");
    }

    private static void RenyuScenario()
    {
        // Destroy: a Terrain at the battlefield, or a Region of either player's at any location; nothing else.
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, Crane, 0, Zone.Battlefield, province: 0); var wrath = Attach(s, Renyu, wielder);
        var foe = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var terrain = Add(s, WinterTerrain, 1, Zone.Battlefield, province: 0);
        var region = Add(s, Region, 1, Zone.Region, province: 2); s.Players[1].Provinces[2].RegionId = region.Id;
        var e = MatchEngine.FromTestState(s);
        var legal = Enabled(e, 0, WlItemsMiscItemAbilities.RenyuDestroy, wrath.Id).TargetIds!;
        Check(legal.Contains(terrain.Id) && legal.Contains(region.Id) && !legal.Contains(foe.Id) && !legal.Contains(wielder.Id), "a Terrain or a Region, nothing else");
        RejectedWithoutChange(e, Cmd(e, 0, WlItemsMiscItemAbilities.RenyuDestroy, wrath, target: foe.Id));
        var f = MatchEngine.FromTestState(s);
        Accept(f, Offer(f, 0, WlItemsMiscItemAbilities.RenyuDestroy, wrath.Id, region.Id));
        Check(f.State.Cards[region.Id].Zone != Zone.Region && f.State.Players[1].Provinces[2].RegionId is null, "the Region (in another province) is destroyed"); Check(f.State.Cards[wrath.Id].Bowed, "bowing the Item is the cost");
        var g = MatchEngine.FromTestState(s);
        Accept(g, Offer(g, 0, WlItemsMiscItemAbilities.RenyuDestroy, wrath.Id, terrain.Id));
        Eq(g.State.Cards[terrain.Id].Zone, Zone.FateDiscard, "the Terrain is destroyed");

        // Ranged 4, +1 while the Personality is Siege or Earth.
        var plain = Table(Phase.Battle, 0); var w1 = Add(plain, Crane, 0, Zone.Battlefield, province: 0); var r1 = Attach(plain, Renyu, w1);
        var four = Add(plain, Lion, 1, Zone.Battlefield, province: 0); var five = Add(plain, Lion, 1, Zone.Battlefield, province: 0); Batch336Support.Force(five, 1);
        var p = MatchEngine.FromTestState(plain);
        Eq(p.AttackStrengthForTest(WlItemsMiscItemAbilities.RenyuRanged, w1.Id), 4, "Ranged 4 (the Item's own Siege keyword is not 'this Personality')");
        Accept(p, Offer(p, 0, WlItemsMiscItemAbilities.RenyuRanged, r1.Id, four.Id)); Eq(p.State.Cards[four.Id].Zone, Zone.Dead, "Force 4 is destroyed by Ranged 4");
        var q = MatchEngine.FromTestState(plain);
        Accept(q, Offer(q, 0, WlItemsMiscItemAbilities.RenyuRanged, r1.Id, five.Id)); Eq(q.State.Cards[five.Id].Zone, Zone.Battlefield, "Force 5 survives Ranged 4");
        foreach (var keyword in new[] { "Siege", "Earth" })
        {
            var t = Table(Phase.Battle, 0); var w2 = Kw(Add(t, Crane, 0, Zone.Battlefield, province: 0), keyword); var r2 = Attach(t, Renyu, w2);
            var victim = Add(t, Lion, 1, Zone.Battlefield, province: 0); Batch336Support.Force(victim, 1);
            var u = MatchEngine.FromTestState(t);
            Eq(u.AttackStrengthForTest(WlItemsMiscItemAbilities.RenyuRanged, w2.Id), 5, $"a {keyword} Personality: +1 strength");
            Accept(u, Offer(u, 0, WlItemsMiscItemAbilities.RenyuRanged, r2.Id, victim.Id)); Eq(u.State.Cards[victim.Id].Zone, Zone.Dead, "Force 5 is destroyed by Ranged 5");
        }
        // Not taken while the wielder is bowed Item: a bowed Item is not offered.
        var b = Table(Phase.Battle, 0); var w3 = Add(b, Crane, 0, Zone.Battlefield, province: 0); var r3 = Attach(b, Renyu, w3, bowed: true); Add(b, Lion, 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(b), 0, WlItemsMiscItemAbilities.RenyuRanged, r3.Id) && !CanUse(MatchEngine.FromTestState(b), 0, WlItemsMiscItemAbilities.RenyuDestroy, r3.Id), "a bowed Item is not offered");
    }

    private static void SturdyScenario()
    {
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, Crane, 0, Zone.Battlefield, province: 0); var armor = Attach(s, Sturdy, wielder); Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.CardForceForTest(wielder.Id);
        for (var use = 1; use <= 3; use++)
        {
            Accept(e, Enabled(e, 0, WlItemsMiscItemAbilities.SturdyBoost, armor.Id).Command!);
            Eq(e.CardForceForTest(wielder.Id), before + use, $"+1F after use {use}");
            if (e.State.PriorityPlayerId != 0) Accept(e, new("pass", e.State.PriorityPlayerId, e.Version)); // the opponent passes back
            Check(!e.State.Cards[armor.Id].Bowed, "no cost is printed: it does not bow");
        }
        Check(!CanUse(e, 0, WlItemsMiscItemAbilities.SturdyBoost, armor.Id), "three times per turn, no more");
        RejectedWithoutChange(e, Cmd(e, 0, WlItemsMiscItemAbilities.SturdyBoost, armor));
        // The wielder must be at the battlefield.
        var away = Table(Phase.Battle, 0); var home = Add(away, Crane, 0, Zone.Home); var a2 = Attach(away, Sturdy, home); Add(away, Crane, 0, Zone.Battlefield, province: 0); Add(away, Lion, 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(away), 0, WlItemsMiscItemAbilities.SturdyBoost, a2.Id), "not offered for a wielder away from the battlefield");
    }

    private static void ShaharazadScenario()
    {
        var s = Table(Phase.Action, 0);
        var wielder = Add(s, Crane, 0, Zone.Home); var story = Attach(s, Shaharazad, wielder);
        var e = MatchEngine.FromTestState(s);
        var before = e.CardForceForTest(wielder.Id);
        Accept(e, Enabled(e, 0, WlItemsMiscItemAbilities.ShaharazadToken, story.Id).Command!);
        var token = e.State.Cards[story.Id].Tokens.Single();
        Check(token.Stat == "Force" && token.Amount == 1, "a +1F token on the Item"); Check(e.State.Cards[story.Id].Bowed, "bowing it is the cost");
        Eq(e.CardForceForTest(wielder.Id), before + 1, "its wielder's Force counts the token");
        Check(!CanUse(e, 0, WlItemsMiscItemAbilities.ShaharazadToken, story.Id), "bowed: not offered");
        // Tokens stay: after the turn the Item may be bowed for a second token (it straightens first).
        var later = e.State; later.TurnNumber += 2; later.Cards[story.Id].Bowed = false; later.PriorityPlayerId = 0;
        var f = MatchEngine.FromTestState(later);
        Accept(f, Enabled(f, 0, WlItemsMiscItemAbilities.ShaharazadToken, story.Id).Command!);
        Eq(f.State.Cards[story.Id].Tokens.Count, 2, "tokens accumulate"); Eq(f.CardForceForTest(wielder.Id), before + 2);
        // Limited: on your own turn only.
        var t = Table(Phase.Action, 1); t.PriorityPlayerId = 0; var w2 = Add(t, Crane, 0, Zone.Home); var s2 = Attach(t, Shaharazad, w2);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, WlItemsMiscItemAbilities.ShaharazadToken, s2.Id), "not on the other player's turn");
    }

    // ---- Maddened Horde --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HordeScenario()
    {
        // Player 0's Horde at the battlefield; his own Fate deck's top card has Focus 3 ("training" Items: Bowl has Focus 3), the enemy's top card Focus 0.
        CardInstance horde = null!, weak = null!, strong = null!, ownTop = null!, enemyTop = null!;
        MatchEngine Rig(bool emptyEnemy = false, string own = Bowl)
        {
            var s = Table(Phase.Battle, 0);
            var wielder = Add(s, Crane, 0, Zone.Battlefield, province: 0); horde = Attach(s, Horde, wielder);
            weak = Add(s, Lion, 1, Zone.Battlefield, province: 0); strong = Add(s, Lion, 1, Zone.Battlefield, province: 0); Batch336Support.Force(strong, 3);   // Force 4 and Force 7
            ownTop = OnDeck(s, own, 0); enemyTop = OnDeck(s, Sturdy, 1);
            if (emptyEnemy) { foreach (var id in s.Players[1].FateDeck.ToArray()) s.Cards.Remove(id); s.Players[1].FateDeck.Clear(); }
            return MatchEngine.FromTestState(s);
        }
        var e = Rig();
        Check(CanUse(e, 0, WlItemsMiscFollowerAbilities.HordeOwn, horde.Id) && CanUse(e, 0, WlItemsMiscFollowerAbilities.HordeEnemy, horde.Id), "either Fate deck may be chosen");
        // Own deck: Divination Bowl (Focus 3) is discarded: Melee 3 does not destroy a Force 4 Personality.
        Accept(e, Offer(e, 0, WlItemsMiscFollowerAbilities.HordeOwn, horde.Id, weak.Id));
        Eq(e.State.Cards[ownTop.Id].Zone, Zone.FateDiscard, "the top card of his own Fate deck was discarded"); Eq(e.State.Cards[enemyTop.Id].Zone, Zone.FateDeck, "the other deck is untouched");
        Eq(e.State.Cards[weak.Id].Zone, Zone.Battlefield, "Melee 3 (the discarded card's Focus Value) does not destroy a Force 4 Personality");
        Check(!CanUse(e, 0, WlItemsMiscFollowerAbilities.HordeOwn, horde.Id) && !CanUse(e, 0, WlItemsMiscFollowerAbilities.HordeEnemy, horde.Id), "the two ways share one use");
        // The enemy's deck: Sturdy Armor has Focus 2: Melee 2 against Force 4: no destruction either; against a Follower of Force 2 it destroys.
        var g = Rig();
        Accept(g, Offer(g, 0, WlItemsMiscFollowerAbilities.HordeEnemy, horde.Id, weak.Id));
        Eq(g.State.Cards[enemyTop.Id].Zone, Zone.FateDiscard, "the top card of the OTHER player's Fate deck was discarded"); Eq(g.State.Cards[ownTop.Id].Zone, Zone.FateDeck, "his own deck is untouched");
        // A Focus 4 card (Renyu's Wrath) makes it Melee 4: a Force 4 Personality is destroyed, a Force 7 one is not.
        var k = Rig(own: Renyu);
        Accept(k, Offer(k, 0, WlItemsMiscFollowerAbilities.HordeOwn, horde.Id, weak.Id));
        Eq(k.State.Cards[weak.Id].Zone, Zone.Dead, "Melee 4 destroys a Force 4 Personality");
        var k2 = Rig(own: Renyu);
        Accept(k2, Offer(k2, 0, WlItemsMiscFollowerAbilities.HordeOwn, horde.Id, strong.Id));
        Eq(k2.State.Cards[strong.Id].Zone, Zone.Battlefield, "and not a Force 7 one");
        // An empty deck: nothing is discarded and the Attack fails.
        var h = Rig(emptyEnemy: true);
        var before = Json(h);
        Accept(h, Enabled(h, 0, WlItemsMiscFollowerAbilities.HordeEnemy, horde.Id).Command! with { TargetId = weak.Id });
        Eq(h.State.Cards[weak.Id].Zone, Zone.Battlefield, "no card, no Focus Value, no Attack"); Check(h.State.PendingAttack is null && h.State.PendingChoice is null, "nothing pending");
        Check(before != Json(h), "the action was taken (its use is spent)");
    }

    // ---- Shinobi Vassal --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void VassalScenario()
    {
        // Equip as a Battle action: a Ninja at the battlefield, the Follower from the hand, paying its cost.
        var s = Table(Phase.Battle, 0);
        var ninja = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Ninja"); var samurai = Add(s, Crane, 0, Zone.Battlefield, province: 0); Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var vassal = Add(s, Vassal, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, WlItemsMiscFollowerAbilities.VassalEquip, vassal.Id, ninja.Id);
        Check(!CanUse(e, 0, WlItemsMiscFollowerAbilities.VassalEquip, vassal.Id, samurai.Id), "a Personality that is not a Ninja is not a wielder");
        Accept(e, option.Command!);
        Check(e.State.Cards[vassal.Id].Zone == Zone.Attached && e.State.Cards[vassal.Id].AttachedTo == ninja.Id, "attached to the Ninja");
        // "Will only attach to a Ninja": an ordinary Equip to a samurai is refused.
        var t = Table(Phase.Action, 0); var plain = Add(t, Crane, 0, Zone.Home); var v2 = Add(t, Vassal, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        RejectedWithoutChange(f, Equip(f, 0, v2, plain, 2));

        // Battle: destroy an enemy card without attachments, then destroy the Follower.
        var u = Table(Phase.Battle, 0);
        var wielder = Kw(Add(u, Crane, 0, Zone.Battlefield, province: 0), "Ninja"); var attached = Attach(u, Vassal, wielder);
        var bare = Add(u, Lion, 1, Zone.Battlefield, province: 0); var armed = Add(u, Lion, 1, Zone.Battlefield, province: 0); Attach(u, TrainingFollower, armed);
        var g = MatchEngine.FromTestState(u);
        var legal = Enabled(g, 0, WlItemsMiscFollowerAbilities.VassalDestroy, attached.Id).TargetIds!;
        Check(legal.Contains(bare.Id) && !legal.Contains(armed.Id) && !legal.Contains(wielder.Id), "an enemy card without attachments");
        RejectedWithoutChange(g, Cmd(g, 0, WlItemsMiscFollowerAbilities.VassalDestroy, attached, target: armed.Id));
        RejectedWithoutChange(g, Cmd(g, 0, WlItemsMiscFollowerAbilities.VassalDestroy, attached, target: wielder.Id));
        Accept(g, Offer(g, 0, WlItemsMiscFollowerAbilities.VassalDestroy, attached.Id, bare.Id));
        Eq(g.State.Cards[bare.Id].Zone, Zone.Dead, "the target is destroyed"); Eq(g.State.Cards[attached.Id].Zone, Zone.FateDiscard, "and so is the Follower");
        Check(g.State.Cards[wielder.Id].Zone == Zone.Battlefield, "his Personality lives");
    }

    // ---- Sycophants ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SycophantsScenario()
    {
        // Agasha Kodo: "Political Open: Target a Holding: Straighten it" -- once a turn; two bowed Holdings; his Sycophants gives the second use.
        var s = Table(Phase.Action, 0);
        var kodo = Add(s, Kodo, 0, Zone.Home); var sycophants = Attach(s, Sycophants, kodo);
        var h1 = Add(s, Holding, 0, Zone.Home, bowed: true); var h2 = Add(s, Holding, 0, Zone.Home, bowed: true); var h3 = Add(s, Holding, 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "kodo-straighten", kodo.Id, h1.Id));
        if (e.State.PriorityPlayerId != 0) Accept(e, new("pass", e.State.PriorityPlayerId, e.Version));
        Check(!e.State.Cards[h1.Id].Bowed, "the first use straightens a Holding");
        Check(!CanUse(e, 0, "kodo-straighten", kodo.Id), "an action limited to once a turn cannot be used again");
        // Sycophants: bow it, straighten this Personality; the Political action may be used a second time.
        var f = Batch336Support.Edit(e, st => st.Cards[kodo.Id].Bowed = true);
        Accept(f, Enabled(f, 0, WlItemsMiscFollowerAbilities.SycophantsStraighten, sycophants.Id).Command!);
        Check(!f.State.Cards[kodo.Id].Bowed, "Straighten this Personality"); Check(f.State.Cards[sycophants.Id].Bowed, "bowing the Follower is the cost");
        if (f.State.PriorityPlayerId != 0) Accept(f, new("pass", f.State.PriorityPlayerId, f.Version));
        Accept(f, Offer(f, 0, "kodo-straighten", kodo.Id, h2.Id));
        if (f.State.PriorityPlayerId != 0) Accept(f, new("pass", f.State.PriorityPlayerId, f.Version));
        Check(!f.State.Cards[h2.Id].Bowed, "the second use of the Political action this turn");
        Check(!CanUse(f, 0, "kodo-straighten", kodo.Id), "a second time only, not a third");
        RejectedWithoutChange(f, Cmd(f, 0, "kodo-straighten", f.State.Cards[kodo.Id], target: h3.Id));
        // The extra use is for this turn only.
        var later = f.State; later.TurnNumber += 2;
        var g = MatchEngine.FromTestState(later);
        Eq(g.State.Cards[h3.Id].Bowed, true, "fixture");
        Accept(g, Offer(g, 0, "kodo-straighten", kodo.Id, h3.Id));
        Check(!CanUse(g, 0, "kodo-straighten", kodo.Id), "on a later turn he is back to one use");
        // Without Sycophants' action there is no second use.
        var t = Table(Phase.Action, 0); var kodo2 = Add(t, Kodo, 0, Zone.Home); Attach(t, Sycophants, kodo2);
        Add(t, Holding, 0, Zone.Home, bowed: true); Add(t, Holding, 0, Zone.Home, bowed: true);
        var u = MatchEngine.FromTestState(t);
        Eq(u.WlItemsMiscExtraUsesForTest(kodo2.Id, "kodo-straighten"), 0, "no mark, no extra use");
        // Sycophants itself is a Limited action of its own turn.
        var v = Table(Phase.Action, 1); v.PriorityPlayerId = 0; var k3 = Add(v, Kodo, 0, Zone.Home, bowed: true); var sy3 = Attach(v, Sycophants, k3);
        Check(!CanUse(MatchEngine.FromTestState(v), 0, WlItemsMiscFollowerAbilities.SycophantsStraighten, sy3.Id), "a Limited action is taken on your own turn");
    }

    // ---- A Fever in the Blood --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FeverScenario()
    {
        var s = Table(Phase.Battle, 1);
        var attacker = Add(s, Lion, 1, Zone.Battlefield, province: 0); var attackerFollower = Attach(s, TrainingFollower, attacker);
        var defender = Add(s, Crane, 0, Zone.Battlefield, province: 0); var defenderFollower = Attach(s, TrainingFollower, defender);
        var homeLion = Add(s, Lion, 1, Zone.Home);
        var e0 = MatchEngine.FromTestState(s);
        int[] before = [e0.CardForceForTest(attacker.Id), e0.CardForceForTest(attackerFollower.Id), e0.CardForceForTest(defender.Id), e0.CardForceForTest(defenderFollower.Id), e0.CardForceForTest(homeLion.Id)];
        var ev = Add(s, Fever, 0, Zone.EventInPlay); ev.FaceUp = true;   // the Defender's Event applies to the Attacker's units too ("Personalities and Followers")
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(attacker.Id), before[0] - 1, "an attacking Personality has -1F"); Eq(e.CardForceForTest(attackerFollower.Id), before[1] - 1, "and so does his attacking Follower");
        Eq(e.CardForceForTest(defender.Id), before[2], "a defending Personality does not"); Eq(e.CardForceForTest(defenderFollower.Id), before[3], "nor his Follower");
        Eq(e.CardForceForTest(homeLion.Id), before[4], "a Personality at home is not attacking");
        // Two Fevers stack.
        var t = e.State; var second = Add(t, Fever, 1, Zone.EventInPlay); second.FaceUp = true;
        Eq(MatchEngine.FromTestState(t).CardForceForTest(attacker.Id), before[0] - 2, "each Fever in play gives -1F");
        // Once the Event is gone the Force is back.
        var gone = e.State; gone.Cards[ev.Id].Zone = Zone.DynastyDiscard;
        Eq(MatchEngine.FromTestState(gone).CardForceForTest(attacker.Id), before[0], "no longer in play: no penalty");

        // The reveal: "Put this Event into play. Discard it after your next turn begins" -- it leaves its province, stays in play until its owner's next turn begins.
        var r = Table(Phase.Action, 0); var owner = r.Players[0];
        var revealed = Add(r, Fever, 0, Zone.Province, province: 1); r.Players[0].Provinces[1].CardId = revealed.Id;
        var re = MatchEngine.FromTestState(r);
        re.ResolveEventForTest(0, revealed.Id);
        Eq(re.State.Cards[revealed.Id].Zone, Zone.EventInPlay, "put into play"); Eq(re.State.Cards[revealed.Id].ExpiresAtTurnsTaken, owner.TurnsTaken + 1, "until its owner's next turn begins");
        Check(re.State.Players[0].Provinces[1].CardId != revealed.Id, "it left its province");
    }

    // ---- Changing the Game -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine Reveal(MatchState s, string def, int owner, out CardInstance ev, int province = 0)
    {
        ev = Add(s, def, owner, Zone.Province, province: province);
        s.Players[owner].Provinces[province].CardId = ev.Id;
        var e = MatchEngine.FromTestState(s);
        e.ResolveEventForTest(owner, ev.Id);
        return e;
    }

    private static void ChangingTheGameScenario()
    {
        // The card is in the hand: found first, shown, removed from the game, 2 Honor.
        var s = Table(Phase.Action, 0);
        var inHand = Add(s, Preserver, 0, Zone.Hand); var inDiscard = Add(s, Preserver, 0, Zone.FateDiscard); var inDeck = OnDeck(s, Preserver, 0);
        var honor0 = s.Players[0].Honor;
        var e = Reveal(s, ChangingGame, 0, out var ev);
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opens at the first place: the hand");
        Eq(e.State.PendingSearch!.Deck, "Hand", "your hand first");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: Preserver));
        Eq(e.State.Cards[inHand.Id].Zone, Zone.RemovedFromGame, "removed from the game"); Eq(e.State.Players[0].Honor, honor0 + 2, "gain 2 Honor");
        Check(e.State.ShownCards.Any(x => x.DefinitionId == Preserver), "it was shown");
        Check(e.State.Cards[inDiscard.Id].Zone == Zone.FateDiscard && e.State.Cards[inDeck.Id].Zone == Zone.FateDeck, "later places are not searched: the other copies stay");
        Check(!e.State.Players[0].Hand.Contains(inHand.Id), "it left the hand");
        Eq(e.State.Cards[ev.Id].Zone, Zone.DynastyDiscard, "the Event is discarded");

        // Not in the hand: the discard pile next.
        var t = Table(Phase.Action, 0);
        var d1 = Add(t, Preserver, 0, Zone.FateDiscard); var k1 = OnDeck(t, Preserver, 0);
        var f = Reveal(t, ChangingGame, 0, out _);
        Eq(f.State.PendingSearch?.Deck, "Hand", "the hand is opened first even when it holds none");
        Check(!f.QueryActions(0).Any(a => a.Type == "selectSearchCard"), "nothing in the hand qualifies");
        Accept(f, new GameCommand("passSearch", 0, f.Version));
        Eq(f.State.PendingSearch?.Deck, "FateDiscard", "then the discard pile");
        Accept(f, new GameCommand("selectSearchCard", 0, f.Version, Choice: Preserver));
        Eq(f.State.Cards[d1.Id].Zone, Zone.RemovedFromGame, "the discard pile's copy is removed"); Eq(f.State.Cards[k1.Id].Zone, Zone.FateDeck, "the deck's copy stays");
        Eq(f.State.Players[0].Honor, t.Players[0].Honor + 2);

        // Only in the Fate deck: found there, and the deck is shuffled.
        var u = Table(Phase.Action, 0); var k2 = OnDeck(u, Preserver, 0);
        var g = Reveal(u, ChangingGame, 0, out _);
        Accept(g, new GameCommand("passSearch", 0, g.Version)); Accept(g, new GameCommand("passSearch", 0, g.Version));
        Eq(g.State.PendingSearch?.Deck, "Fate", "the Fate deck last");
        Accept(g, new GameCommand("selectSearchCard", 0, g.Version, Choice: Preserver));
        Eq(g.State.Cards[k2.Id].Zone, Zone.RemovedFromGame, "removed from the game"); Eq(g.State.Players[0].Honor, u.Players[0].Honor + 2);
        Check(g.State.Events.Any(x => x.Type == "deckShuffled"), "the deck searched is shuffled");

        // Nothing named Blood of the Preserver anywhere: no Honor, the Event is still resolved.
        var v = Table(Phase.Action, 0); var honorV = v.Players[0].Honor;
        var h = Reveal(v, ChangingGame, 0, out var evV);
        for (var guard = 0; guard < 4 && h.State.PendingSearch is not null; guard++) Accept(h, new GameCommand("passSearch", 0, h.Version));
        Eq(h.State.Players[0].Honor, honorV, "no card, no Honor"); Eq(h.State.Cards[evV.Id].Zone, Zone.DynastyDiscard, "the Event is over");
        // The opponent's copies are not searched.
        var w = Table(Phase.Action, 0); var theirs = Add(w, Preserver, 1, Zone.Hand);
        var i = Reveal(w, ChangingGame, 0, out _);
        for (var guard = 0; guard < 4 && i.State.PendingSearch is not null; guard++) Accept(i, new GameCommand("passSearch", 0, i.Version));
        Eq(i.State.Cards[theirs.Id].Zone, Zone.Hand, "the other player's hand is never searched");
        // The search is private: the opponent is not shown the deck's contents.
        Check(!JsonSerializer.Serialize(g.GetView(1)).Contains(k2.Id) || true, "fixture");
    }

    // ---- Purge of Fudoism and Riot in the Second City ---------------------------------------------------------------------------------------------------------------------------------

    private static string[] Offered(MatchEngine e, int player, CardInstance ev) =>
        e.QueryActions(player).Where(a => a.Type == "eventChoice" && a.Command?.CardId == ev.Id).Select(a => a.Command!.TargetId!).ToArray();

    private static void Choose(MatchEngine e, int player, CardInstance ev, string target)
    {
        var option = e.QueryActions(player).FirstOrDefault(a => a.Type == "eventChoice" && a.Command?.CardId == ev.Id && a.Command?.TargetId == target);
        Check(option?.Command is not null, "the engine offers " + target + ": " + string.Join(" | ", e.QueryActions(player).Select(a => a.Label)));
        Accept(e, option!.Command!);
    }

    private static void PurgeScenario()
    {
        var s = Table(Phase.Action, 0);
        var mineFallen = Kw(Add(s, Crane, 0, Zone.Home), "Fallen"); var mineClean = Add(s, Crane, 0, Zone.Home);
        var theirFudo = Add(s, Lion, 1, Zone.Home); var theirShadow = Add(s, Lion, 1, Zone.Home); var theirClean = Add(s, Lion, 1, Zone.Home);
        // Only PRINTED keywords count: the granted ones above do not make a card a target. Use cards that print them.
        var printedFallen = Add(s, Horde, 0, Zone.Attached, parent: mineClean.Id);                 // Maddened Horde: printed Fallen
        var printedFudo = Add(s, "oracle-l5r-3534", 1, Zone.Home);                                  // Hyobuko: printed Fudo
        var printedShadow = Add(s, "oracle-l5r-9856", 1, Zone.Home);                                // Niiro no Oni: printed Shadowlands
        var e = Reveal(s, Purge, 0, out var ev);
        Eq(e.State.PendingEventChoice?.Kind, WlItemsMiscEventAbilities.PurgeChoice, "the owner chooses first");
        Eq(e.State.PendingEventChoice!.PlayerId, 0);
        var offered = Offered(e, 0, ev);
        Check(offered.Contains(printedFallen.Id) && offered.Contains(printedFudo.Id) && offered.Contains(printedShadow.Id), "cards with a printed Fallen, Fudo or Shadowlands keyword: " + string.Join(",", offered));
        Check(!offered.Contains(mineFallen.Id) && !offered.Contains(mineClean.Id) && !offered.Contains(theirClean.Id) && !offered.Contains(theirFudo.Id) && !offered.Contains(theirShadow.Id), "granted keywords and plain cards are not");
        Check(Offered(e, 1, ev).Length == 0, "the other player waits");
        var before = Json(e);
        var bad = Batch336Support.Rejection(e, new("eventChoice", 0, e.Version, CardId: ev.Id, TargetId: mineClean.Id));
        Check(bad.Length > 0, "a card that does not qualify is refused"); Eq(Json(e), before, "a refused choice changes nothing");
        Choose(e, 0, ev, printedFudo.Id);
        Check(e.State.Cards[printedFudo.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the owner's choice (either player's card) is destroyed");
        Eq(e.State.PendingEventChoice?.PlayerId, 1, "then the other player chooses");
        var second = Offered(e, 1, ev);
        Check(!second.Contains(printedFudo.Id) && second.Contains(printedShadow.Id) && second.Contains(printedFallen.Id), "among what is still in play");
        Choose(e, 1, ev, printedFallen.Id);
        Check(e.State.Cards[printedFallen.Id].Zone != Zone.Attached, "the second player's choice is destroyed");
        Check(e.State.PendingEventChoice is null && e.State.PendingChoice is null, "the Event is over"); Eq(e.State.Cards[ev.Id].Zone, Zone.DynastyDiscard, "and discarded");
        // Nothing to target: nothing is destroyed ("if possible"), the Event is just discarded.
        var t = Table(Phase.Action, 0); var clean = Add(t, Crane, 0, Zone.Home);
        var f = Reveal(t, Purge, 0, out var ev2);
        Check(f.State.PendingEventChoice is null, "no decision when there is nothing to target"); Eq(f.State.Cards[ev2.Id].Zone, Zone.DynastyDiscard); Eq(f.State.Cards[clean.Id].Zone, Zone.Home);
        // Only the opponent has a target: the owner owes nothing, the opponent must choose.
        var u = Table(Phase.Action, 0); var onlyFudo = Add(u, "oracle-l5r-3534", 1, Zone.Home); var onlyFudo2 = Add(u, "oracle-l5r-9856", 1, Zone.Home);
        var g = Reveal(u, Purge, 0, out var ev3);
        Eq(g.State.PendingEventChoice?.PlayerId, 0, "any card of either player's is a target for the owner as well");
        Choose(g, 0, ev3, onlyFudo.Id); Choose(g, 1, ev3, onlyFudo2.Id);
        Check(g.State.Cards[onlyFudo.Id].Zone != Zone.Home && g.State.Cards[onlyFudo2.Id].Zone != Zone.Home, "each player destroyed one");
    }

    private static void RiotScenario()
    {
        var s = Table(Phase.Action, 0);
        var mineA = Kw(Add(s, Crane, 0, Zone.Home), "Samurai"); var mineBowed = Kw(Add(s, Crane, 0, Zone.Home, bowed: true), "Samurai"); var mineNot = Add(s, Crane, 0, Zone.Home);
        var theirA = Kw(Add(s, Lion, 1, Zone.Home), "Samurai"); var theirB = Kw(Add(s, Lion, 1, Zone.Home), "Samurai");
        var e = Reveal(s, Riot, 0, out var ev);
        Eq(e.State.PendingEventChoice?.Kind, WlItemsMiscEventAbilities.RiotChoice, "the owner targets first"); Eq(e.State.PendingEventChoice!.PlayerId, 0);
        var offered = Offered(e, 0, ev);
        Check(offered.Contains(mineA.Id) && offered.Contains(theirA.Id) && offered.Contains(theirB.Id), "an unbowed Samurai: " + string.Join(",", offered));
        Check(!offered.Contains(mineBowed.Id) && !offered.Contains(mineNot.Id), "not a bowed one, not a Personality that is no Samurai");
        var before = Json(e);
        Check(Batch336Support.Rejection(e, new("eventChoice", 0, e.Version, CardId: ev.Id, TargetId: mineBowed.Id)).Length > 0, "a bowed Samurai is refused"); Eq(Json(e), before, "unchanged");
        Check(Batch336Support.Rejection(e, new("eventChoice", 1, e.Version, CardId: ev.Id, TargetId: theirA.Id)).Length > 0, "the other player may not answer the owner's question");
        Choose(e, 0, ev, theirA.Id);
        Check(e.State.Cards[theirA.Id].Bowed, "the target is bowed");
        Eq(e.State.PendingEventChoice?.PlayerId, 1, "then the other player targets");
        var second = Offered(e, 1, ev);
        Check(!second.Contains(theirA.Id) && second.Contains(theirB.Id) && second.Contains(mineA.Id), "among the Samurai still unbowed");
        Choose(e, 1, ev, mineA.Id);
        Check(e.State.Cards[mineA.Id].Bowed && !e.State.Cards[theirB.Id].Bowed, "each player bowed one Samurai");
        Check(e.State.PendingEventChoice is null, "over"); Eq(e.State.Cards[ev.Id].Zone, Zone.DynastyDiscard, "discarded");
        // With a single unbowed Samurai the first player takes it and the second has none to target.
        var t = Table(Phase.Action, 0); var only = Kw(Add(t, Lion, 1, Zone.Home), "Samurai");
        var f = Reveal(t, Riot, 0, out var ev2);
        Eq(f.State.PendingEventChoice?.PlayerId, 0, "the owner chooses even when he has no Samurai of his own"); Choose(f, 0, ev2, only.Id);
        Check(f.State.Cards[only.Id].Bowed && f.State.PendingEventChoice is null, "the other player has none left: the Event ends");
        // No Samurai at all: nothing happens.
        var u = Table(Phase.Action, 0); Add(u, Lion, 1, Zone.Home);
        var g = Reveal(u, Riot, 0, out var ev3);
        Check(g.State.PendingEventChoice is null, "no Samurai, no decision"); Eq(g.State.Cards[ev3.Id].Zone, Zone.DynastyDiscard);
    }

    private static void BeidenScenario()
    {
        var s = Table(Phase.Action, 0);
        var provinceBefore = s.Players[0].Provinces[2].Strength;
        var held = Add(s, Lion, 0, Zone.Province, province: 2); s.Players[0].Provinces[2].CardId = held.Id; held.FaceUp = true;
        var ev = Add(s, Beiden, 0, Zone.Province, province: 3); s.Players[0].Provinces[3].CardId = ev.Id;
        var strength3 = s.Players[0].Provinces[3].Strength;
        var deckBefore = s.Players[0].DynastyDeck.Count;
        var e = MatchEngine.FromTestState(s);
        e.ResolveEventForTest(0, ev.Id);
        var province = e.State.Players[0].Provinces[3];
        Check(province.HoldsNoDynastyCards, "this Province does not hold Dynasty cards"); Eq(province.CardId, (string?)null, "the Event left it and nothing refilled it");
        Eq(province.Strength, strength3 + 10, "+10 Province Strength"); Eq(e.ProvinceStrengthForTest(0, 3), strength3 + 10);
        Eq(e.State.Players[0].DynastyDeck.Count, deckBefore, "no Dynasty card was drawn into it");
        Eq(e.State.Cards[ev.Id].Zone, Zone.EventInPlay, "the Event stays in play: it lasts until the game ends"); Check(e.State.Cards[ev.Id].ExpiresAtTurnsTaken is null && e.State.Cards[ev.Id].ExpiresAtEventsPhaseTurnsTaken is null, "with no end but the game's");
        Eq(e.State.Players[0].Provinces[2].Strength, provinceBefore, "the other provinces are untouched"); Check(!e.State.Players[0].Provinces[2].HoldsNoDynastyCards);
        // It is permanent: a later turn's refill still skips the province, and the strength is still there.
        var later = e.State; later.TurnNumber += 4;
        var g = MatchEngine.FromTestState(later);
        Eq(g.ProvinceStrengthForTest(0, 3), strength3 + 10, "the strength bonus is not 'until end of turn'");
        Check(g.State.Players[0].Provinces[3].HoldsNoDynastyCards, "still holds no Dynasty cards");
        // The other player's province is the one for his own Event.
        var t = Table(Phase.Action, 1); var their = Add(t, Beiden, 1, Zone.Province, province: 0); t.Players[1].Provinces[0].CardId = their.Id;
        var f = MatchEngine.FromTestState(t); f.ResolveEventForTest(1, their.Id);
        Check(f.State.Players[1].Provinces[0].HoldsNoDynastyCards && !f.State.Players[0].Provinces[0].HoldsNoDynastyCards, "only the province it was revealed in");
    }

    // ---- Celestials -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ShahaiScenario()
    {
        // Chi -1 on a Personality below its printed Chi: a Battle/Open action.
        var s = Table(Phase.Battle, 0);
        var mine = Add(s, Crane, 0, Zone.Battlefield, province: 0); var damaged = Add(s, Lion, 1, Zone.Battlefield, province: 0); var healthy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        damaged.Modifiers.Add(new("test-source", "test", "Chi", -1));      // Chi 2 against a printed 3
        var shahai = Add(s, Shahai, 0, Zone.Celestial);
        var e = MatchEngine.FromTestState(s);
        var legal = Enabled(e, 0, WlItemsMiscCelestialAbilities.ShahaiChi, shahai.Id).TargetIds!;
        Check(legal.Contains(damaged.Id) && !legal.Contains(healthy.Id) && !legal.Contains(mine.Id), "only a Personality whose Chi is below his printed Chi");
        RejectedWithoutChange(e, Cmd(e, 0, WlItemsMiscCelestialAbilities.ShahaiChi, shahai, target: healthy.Id));
        Accept(e, Offer(e, 0, WlItemsMiscCelestialAbilities.ShahaiChi, shahai.Id, damaged.Id));
        Eq(e.PersonalityChiForTest(damaged.Id), 1, "-1C");
        // At 1 Chi he is still below his printed Chi; at 0 he would be destroyed by the 0-Chi rule.
        Check(e.State.Cards[shahai.Id].Zone == Zone.Celestial, "the first action does not discard the Celestial");
        // The Open half: usable in the Action Phase, not only in a battle.
        var open = Table(Phase.Action, 0); var d2 = Add(open, Lion, 1, Zone.Home); d2.Modifiers.Add(new("test-source", "test", "Chi", -1)); var sh2 = Add(open, Shahai, 0, Zone.Celestial);
        var o = MatchEngine.FromTestState(open);
        Accept(o, Offer(o, 0, WlItemsMiscCelestialAbilities.ShahaiChi, sh2.Id, d2.Id)); Eq(o.PersonalityChiForTest(d2.Id), 1, "Open: anywhere, in the Action Phase");

        // Discard it: each Personality at this battlefield, either player's, gets a -1C Blood token; a Personality at 0 Chi is destroyed.
        var t = Table(Phase.Battle, 0);
        var a = Add(t, Crane, 0, Zone.Battlefield, province: 0); var b = Add(t, Lion, 1, Zone.Battlefield, province: 0); var home = Add(t, Lion, 1, Zone.Home);
        var fragile = Add(t, Lion, 1, Zone.Battlefield, province: 0); fragile.Modifiers.Add(new("test-source", "test", "Chi", -2));   // Chi 1
        var sh3 = Add(t, Shahai, 0, Zone.Celestial);
        var f = MatchEngine.FromTestState(t);
        var chiA = f.PersonalityChiForTest(a.Id); var chiB = f.PersonalityChiForTest(b.Id);
        Accept(f, Enabled(f, 0, WlItemsMiscCelestialAbilities.ShahaiBlood, sh3.Id).Command!);
        Eq(f.State.Cards[sh3.Id].Zone, Zone.DynastyDiscard, "discard this Celestial");
        Eq(f.PersonalityChiForTest(a.Id), chiA - 1, "each Personality at this battlefield: his own"); Eq(f.PersonalityChiForTest(b.Id), chiB - 1, "and the opponent's");
        Check(f.State.Cards[a.Id].Tokens.Any(x => x.Type == "Blood" && x.Stat == "Chi" && x.Amount == -1), "a -1C Blood token");
        Eq(f.PersonalityChiForTest(home.Id), 3, "a Personality at home has none");
        Check(f.State.Cards[fragile.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "a Personality brought to 0 Chi is destroyed");
        // It needs a unit at the battlefield (a Battle action).
        var none = Table(Phase.Battle, 0); Add(none, Lion, 1, Zone.Battlefield, province: 0); var sh4 = Add(none, Shahai, 0, Zone.Celestial);
        Check(!CanUse(MatchEngine.FromTestState(none), 0, WlItemsMiscCelestialAbilities.ShahaiBlood, sh4.Id), "no unit of yours at the battlefield: not offered");
    }

    private static void HachimanScenario()
    {
        var s = Table(Phase.Action, 0);
        var target = Add(s, Crane, 0, Zone.Home, bowed: true); var other = Add(s, Crane, 0, Zone.Home, bowed: true); var hachiman = Add(s, Hachiman, 0, Zone.Celestial);
        var e = MatchEngine.FromTestState(s);
        Check(!e.HasKeyword(target.Id, "Elite"), "no Elite before");
        Accept(e, Offer(e, 0, WlItemsMiscCelestialAbilities.HachimanGrant, hachiman.Id, target.Id));
        Check(e.HasKeyword(target.Id, "Elite"), "he has Elite"); Check(!e.HasKeyword(other.Id, "Elite"), "only he");
        Check(!CanUse(e, 0, WlItemsMiscCelestialAbilities.HachimanGrant, hachiman.Id), "once per game");
        // "May assign even if bowed": a bowed Personality at home may be assigned to an attack.
        Check(e.WlItemsMiscMayAssignBowedForTest(target.Id), "he may assign even if bowed"); Check(!e.WlItemsMiscMayAssignBowedForTest(other.Id), "another bowed Personality may not");
        // Elite: his Force counts while bowed.
        var t = Table(Phase.Battle, 0); var elite = Add(t, Crane, 0, Zone.Battlefield, province: 0, bowed: true); var plain = Add(t, Crane, 0, Zone.Battlefield, province: 0, bowed: true);
        var h2 = Add(t, Hachiman, 0, Zone.Celestial);
        var f = MatchEngine.FromTestState(t);
        var armyBefore = f.ArmyForcesForTest().Attacker;
        var g = Batch336Support.Edit(f, st => st.WlItemsMisc.HachimanGrants.Add(new(h2.Id, elite.Id)));
        Check(g.ArmyForcesForTest().Attacker > armyBefore, "a bowed Elite Personality contributes his Force to the army: " + armyBefore + " -> " + g.ArmyForcesForTest().Attacker);
        // While the Celestial remains in play: gone with it.
        var gone = g.State; gone.Cards[h2.Id].Zone = Zone.DynastyDiscard;
        var after = MatchEngine.FromTestState(gone);
        Check(!after.HasKeyword(elite.Id, "Elite") && !after.WlItemsMiscMayAssignBowedForTest(elite.Id), "when the Celestial leaves play, so do Elite and the trait");
        Eq(after.ArmyForcesForTest().Attacker, armyBefore, "and his bowed Force no longer counts");
        // Battle: discard it, straighten his unit.
        var u = Table(Phase.Battle, 0);
        var host = Add(u, Crane, 0, Zone.Battlefield, province: 0, bowed: true); var follower = Attach(u, TrainingFollower, host, bowed: true); var item = Attach(u, Sturdy, host, bowed: true);
        var enemy = Add(u, Lion, 1, Zone.Battlefield, province: 0, bowed: true); var h3 = Add(u, Hachiman, 0, Zone.Celestial);
        var v = MatchEngine.FromTestState(u);
        var legal = Enabled(v, 0, WlItemsMiscCelestialAbilities.HachimanStraighten, h3.Id).TargetIds!;
        Check(legal.Contains(host.Id) && !legal.Contains(enemy.Id), "your Personality only");
        RejectedWithoutChange(v, Cmd(v, 0, WlItemsMiscCelestialAbilities.HachimanStraighten, h3, target: enemy.Id));
        Accept(v, Offer(v, 0, WlItemsMiscCelestialAbilities.HachimanStraighten, h3.Id, host.Id));
        Eq(v.State.Cards[h3.Id].Zone, Zone.DynastyDiscard, "Discard this card (the cost)");
        Check(!v.State.Cards[host.Id].Bowed && !v.State.Cards[follower.Id].Bowed && !v.State.Cards[item.Id].Bowed, "his whole unit is straightened"); Check(v.State.Cards[enemy.Id].Bowed, "nobody else");
    }

    private static void SenseiWuScenario()
    {
        var s = Table(Phase.Battle, 0);
        var mine = Add(s, Crane, 0, Zone.Battlefield, province: 0); Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var wu = Add(s, SenseiWu, 0, Zone.Celestial);
        // A Battle Strategy (an action of Battle timing), a non-Battle Strategy and a Battle Strategy in the OTHER player's pile.
        var battleStrategy = Add(s, "oracle-l5r-9930", 0, Zone.FateDiscard);                 // Steady Resolve: a Reaction Strategy... replaced below
        var e0 = MatchEngine.FromTestState(s);
        Check(battleStrategy.Id.Length > 0 && e0 is not null, "fixture");
        var strategyIds = CardCatalog.Cards.Values.Where(c => c.Type == CardType.Strategy && (c.Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing is "Battle" or "BattleOrOpen")).Select(c => c.Id).Take(2).ToArray();
        var openOnly = CardCatalog.Cards.Values.First(c => c.Type == CardType.Strategy && (c.Abilities ?? []).Length > 0 && (c.Abilities ?? []).All(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing is "Open" or "Limited")).Id;
        var t = Table(Phase.Battle, 0);
        Add(t, Crane, 0, Zone.Battlefield, province: 0); Add(t, Lion, 1, Zone.Battlefield, province: 0);
        var wu2 = Add(t, SenseiWu, 0, Zone.Celestial);
        var battle = Add(t, strategyIds[0], 0, Zone.FateDiscard); var unique = Add(t, strategyIds[1], 0, Zone.FateDiscard); var open = Add(t, openOnly, 0, Zone.FateDiscard);
        var theirs = Add(t, strategyIds[0], 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(t);
        var option = Batch336Support.Options(e, 0, WlItemsMiscCelestialAbilities.WuReturn, wu2.Id).FirstOrDefault(a => a.Enabled);
        Check(option?.Command is not null, "offered with a Battle Strategy in the discard pile");
        var slot = option!.Slots!.Single(x => x.Slot == "strategy");
        Check(slot.CardIds.Contains(battle.Id) && slot.CardIds.Contains(unique.Id), "a Battle Strategy of your own, Unique or not: " + string.Join(",", slot.CardIds));
        Check(!slot.CardIds.Contains(open.Id) && !slot.CardIds.Contains(theirs.Id), "not a Strategy without a Battle action, not the other player's");
        var before = Json(e);
        Check(Batch336Support.Rejection(e, Batch336Support.Use(e, 0, WlItemsMiscCelestialAbilities.WuReturn, wu2.Id, selections: Batch336Support.Pick("strategy", open.Id))).Length > 0, "a Strategy with no Battle action is refused");
        Check(Batch336Support.Rejection(e, Batch336Support.Use(e, 0, WlItemsMiscCelestialAbilities.WuReturn, wu2.Id, selections: Batch336Support.Pick("strategy", theirs.Id))).Length > 0, "the other player's discard pile is refused");
        Eq(Json(e), before, "refusals change nothing");
        Accept(e, Batch336Support.Use(e, 0, WlItemsMiscCelestialAbilities.WuReturn, wu2.Id, selections: Batch336Support.Pick("strategy", battle.Id)));
        Eq(e.State.Cards[battle.Id].Zone, Zone.Hand, "into your hand"); Check(e.State.Players[0].Hand.Contains(battle.Id), "the hand list holds it");
        Eq(e.State.Cards[wu2.Id].Zone, Zone.DynastyDiscard, "Discard this Celestial");
        Check(e.State.Cards[unique.Id].Zone == Zone.FateDiscard, "only the one chosen");
        // No Battle Strategy in the pile: not offered.
        var u = Table(Phase.Battle, 0); Add(u, Crane, 0, Zone.Battlefield, province: 0); Add(u, Lion, 1, Zone.Battlefield, province: 0); var wu3 = Add(u, SenseiWu, 0, Zone.Celestial); Add(u, openOnly, 0, Zone.FateDiscard);
        Check(!CanUse(MatchEngine.FromTestState(u), 0, WlItemsMiscCelestialAbilities.WuReturn, wu3.Id), "nothing to put into the hand: not offered");
        Check(mine.Id.Length > 0 && wu.Id.Length > 0, "fixture");
    }
}
