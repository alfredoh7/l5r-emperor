using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition group P3 (issue #503): new Reaction trigger points, announcement/interrupt windows and priority overrides (src/L5R.Core/EmperorP3.cs). Every position is built explicitly and
// driven through QueryActions and the commands the engine offers; every ability is exercised end to end, with a positive and a negative or edge case.
internal static partial class EmperorP3Tests
{
    private const string Toson = "oracle-l5r-7177", Tenbatsu = "oracle-l5r-1778", Akeha = "oracle-l5r-1621", Erosion = "oracle-l5r-2363", Centering = "oracle-l5r-1277",
        DeepSnows = "oracle-l5r-1921", RequestAuthorization = "oracle-l5r-6265", IronWill = "oracle-l5r-3813",
        BurnTheTowers = "oracle-l5r-1180", AsakoChukage = "oracle-l5r-10072", AtAnyCost = "oracle-l5r-639";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static CardInstance FateTop(MatchState s, int owner, string def = "training-focus-strategy") { var c = Add(s, def, owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, c.Id); return c; }
    private static int ForceMods(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);
    private static GameCommand Pass(MatchEngine e) => new("declineReaction", e.State.PriorityPlayerId, e.Version);
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 80 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), "the stop was not reached");
    }
    private static bool Offered(MatchEngine e, int player, string ability) => e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);

    // The engage window at province 0, player 0 attacking (a battle position on the economy scenario).
    private static MatchState EngageTable()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        return s;
    }
    private static void Engage(MatchEngine e) { Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0)); Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens"); }

    public static void Run(TestRunner runner) => runner.Run("emperor-p3", [
        ("Published shape: personality-wave-104, spell-wave-34 and strategy-wave-90 print their scans' stats, keywords and text, carry their abilities, ship their art and are in their manifests", PublishedShape),
        ("Shosuro Toson - exp2: after engaging at his battlefield a Personality discarded from its province gives his controller the first opportunity, which Toson must perform", TosonClaims),
        ("Shosuro Toson - exp2, edges: a discarded non-Personality claims nothing (the Defender keeps the first opportunity); not offered away from the battlefield", TosonEdges),
        ("Daigotsu Tenbatsu: after engaging, the top three Fate cards are put back in any order; a Dark Virtue discarded draws a card", TenbatsuLook),
        ("Daigotsu Tenbatsu, edges: declining the discard draws nothing; with no Dark Virtue in hand nothing is asked; the Engage window is given back", TenbatsuEdges),
        ("Daidoji Akeha: after her player's Recon action from a Strategy resolves she draws a card; not after a Recon action from another card, nor after another player's", AkehaDraw),
        ("Daidoji Akeha, Recon Limited: discard a card to search the Fate deck for a non-Unique card with a Recon ability; a Unique or non-Recon card is not eligible", AkehaSearch),
        ("Erosion: -1 province strength for other players while its Shugenja is Water; after an action moved a Personality to the current battlefield, give him -4F", ErosionScenario),
        ("Erosion, edges: no penalty to its own player's provinces or when the Shugenja is not Water; no Reaction after a move home", ErosionEdges),
        ("Centering the Soul: after an action bowed one of its player's Rings, bow the Spell to straighten it; Void Shugenja may destroy it for an additional use of the Ring this turn", CenteringScenario),
        ("Centering the Soul, edges: an opponent's bowed Ring is not a target; the destroying play needs a Void Shugenja; the two plays share one use", CenteringEdges),
        ("Deep Snows: after a Battle action resolves with no Terrain at the battlefield it enters play there; then its trait bows every Personality an action moves", DeepSnowsScenario),
        ("Deep Snows, edges: not offered after a non-Battle action or while a Terrain stands there; never played as an ordinary Terrain", DeepSnowsEdges),
        ("Request Authorization, Battle: bow a Courtier at any location to move a target Personality home", RequestMove),
        ("Request Authorization, Reaction: before another player's Battle action that moves Personalities to the battle, negate a target's movement there; not offered against an action that moves nobody there", RequestNegate),
        ("Iron Will: another player's Battle action by a lower-Personal-Honor Personality targets a Paragon -- its costs are ignored, its effects negated, and that player may take an additional Battle action", IronWillScenario),
        ("Iron Will, edges: not offered when the performer's Personal Honor is not lower or the target is not a Paragon", IronWillEdges),
        .. PartB,
    ]);

    // ---- published shape --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

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
        void Person(string id, string name, string clan, int gold, int force, int chi, int ph, int? hr, string keywords, string abilities, string text)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Personality && c.Name == name && c.Clan == clan && c.GoldCost == gold && c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.HonorRequirement == hr, $"{name}'s stats");
            Eq(string.Join(",", (c.Keywords ?? []).Order(StringComparer.Ordinal)), keywords, $"{name}'s keywords");
            Eq(string.Join(",", c.Abilities ?? []), abilities, $"{name}'s abilities"); Eq(c.Effect, "none"); Eq(c.Text, text, $"{name}'s text");
        }
        Person(Akeha, "Daidoji Akeha", "Crane", 8, 5, 3, 3, 0, "Crane Clan,Daimyo,Iron Crane,Loyal,Samurai,Scout,Unique", "akeha-recon-draw,akeha-recon-search",
            "Reaction: After your Recon action from a Strategy resolves: Draw a card.\nRecon Limited: Discard a card: Search your Fate deck for a non-Unique card with a Recon ability, show it, and put it in your hand.");
        Person(Tenbatsu, "Daigotsu Tenbatsu", "Spider", 6, 3, 2, 0, null, "Dark Prophet,Paragon,Samurai,Spider Clan", "tenbatsu-engage-look",
            "Reaction: After engaging at Tenbatsu's battlefield: Look at the top three cards of your Fate deck. Put them back in any order. You may discard a Dark Virtue; if you did, draw a card.");
        Person(Toson, "Shosuro Toson", "Scorpion", 9, 4, 4, 1, null, "Daimyo,Experienced 2,Loyal,Ninja,Scorpion Clan,Unique", "toson-exp2-engage",
            "Reaction: After engaging at Toson's battlefield: Discard a card from the current battlefield's province. If this discarded a Personality, you have the first opportunity to take a Battle action in this battle, which must be performed by Toson.");
        Manifest("personality-wave-104", Akeha, "oracle-l5r-1747", Tenbatsu, "oracle-l5r-3093", "oracle-l5r-4189", Toson, "oracle-l5r-9100");
        void Other(string id, CardType type, string name, int focus, string keywords, string effect, string abilities, string text)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == type && c.Name == name && c.GoldCost == 0 && c.Focus == focus, $"{name}'s stats");
            Eq(string.Join(",", c.Keywords ?? []), keywords, $"{name}'s keywords"); Eq(c.Effect, effect, $"{name}'s effect"); Eq(string.Join(",", c.Abilities ?? []), abilities); Eq(c.Text, text, $"{name}'s text");
        }
        Other(Centering, CardType.Spell, "Centering the Soul", 1, "Void", "none", "centering-soul-straighten,centering-soul-straighten-destroy",
            "Reaction: After an action resolves, bow this card and target one of your Rings the action bowed: Straighten the Ring. If this Shugenja is Void, you may destroy this card; if you did, you may use the Ring's base abilities an additional time this turn.");
        Other(Erosion, CardType.Spell, "Erosion", 3, "Water", "erosion-provinces", "erosion-weaken",
            "While this Shugenja is Water, other players' provinces have -1 strength.\nReaction: After the resolution of an action that moved a Personality to the current battlefield: Give him -4F.");
        Manifest("spell-wave-34", Centering, Erosion);
        Other(DeepSnows, CardType.Strategy, "Deep Snows", 1, "Terrain,Winter", "deep-snows-bow-moved", "deep-snows-enter",
            "After an action resolves: Bow any Personalities it moved. (Reactions trigger after traits.)\nReaction: After a Battle action resolves, if there are no Terrains in play at the current battlefield: Put this card into play there.");
        Other(IronWill, CardType.Strategy, "Iron Will", 3, "", "none", "iron-will-negate",
            "Reaction: After another player targets your Paragon with a Battle action performed by a Personality with lower Personal Honor: The action's costs are ignored and its effects are negated. That player may take an additional Battle action.");
        Other(RequestAuthorization, CardType.Strategy, "Request Authorization", 3, "Political", "none", "request-authorization-move,request-authorization-negate",
            "Battle: Bow your performing Courtier at any location and target a Personality: Move him home.\nReaction: Before another player's Battle action resolves, target a Personality: Negate his movement to the current battlefield from the action's effects.");
        Manifest("strategy-wave-90", DeepSnows, "oracle-l5r-2652", IronWill, RequestAuthorization);
        Eq(AbilityCatalog.All["centering-soul-straighten"].UsageKey, "centering-the-soul"); Eq(AbilityCatalog.All["centering-soul-straighten-destroy"].UsageKey, "centering-the-soul");
    }

    // ---- Shosuro Toson - exp2 ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine TosonTable(string provinceCard, out CardInstance toson, out CardInstance other, out CardInstance enemy, out CardInstance discarded)
    {
        var s = EngageTable();
        toson = Add(s, Toson, 0, Zone.Battlefield, province: 0);
        other = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var province = s.Players[1].Provinces[0];
        if (province.CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
        discarded = Add(s, provinceCard, 1, Zone.Province, province: 0);
        province.CardId = discarded.Id;
        return MatchEngine.FromTestState(s);
    }

    private static void TosonClaims()
    {
        var e = TosonTable("training-lion-courtier", out var toson, out var other, out var enemy, out var discarded);
        Engage(e);
        Accept(e, Enabled(e, 0, "toson-exp2-engage", toson.Id).Command!);
        Eq(e.State.Cards[discarded.Id].Zone, Zone.DynastyDiscard, "the province's Personality was discarded");
        Check(e.State.Players[1].Provinces[0].CardId is { } refill && refill != discarded.Id, "the province was refilled");
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.FirstActionPlayerId, 0, "Toson's controller holds the first opportunity");
        Eq(e.State.FirstActionRestriction?.PerformerId, toson.Id, "which Toson must perform");
        Check(Rejection(e, Use(e, 0, "training-bow-at-battle", other.Id, enemy.Id)).Contains("Shosuro Toson"), "another performer is refused, naming Toson");
        Accept(e, new GameCommand("pass", 0, e.Version));
        Check(e.State.FirstActionRestriction is null, "the limit binds only that first action (a pass spends it)");
    }

    private static void TosonEdges()
    {
        // A Holding in the province: it is discarded, but no opportunity is claimed.
        var e = TosonTable("training-marketplace", out var toson, out _, out _, out var holding);
        Engage(e);
        Accept(e, Enabled(e, 0, "toson-exp2-engage", toson.Id).Command!);
        Eq(e.State.Cards[holding.Id].Zone, Zone.DynastyDiscard, "the Holding was discarded");
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.FirstActionPlayerId, 1, "no claim: the Defender acts first");
        Check(e.State.FirstActionRestriction is null, "and no performer limit");
        // Toson at home: not at the battlefield being engaged.
        var s = EngageTable();
        var home = Add(s, Toson, 0, Zone.Home);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var h = MatchEngine.FromTestState(s);
        Accept(h, new GameCommand("selectBattle", 0, h.Version, Province: 0));
        Check(h.State.PendingChoice?.Type != "engageReaction" || !Offered(h, 0, "toson-exp2-engage"), "Toson at home has no Reaction to the engagement");
        Check(home.Id.Length > 0);
    }

    // ---- Daigotsu Tenbatsu ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine TenbatsuTable(bool darkVirtue, out CardInstance tenbatsu, out CardInstance virtue, out CardInstance[] top)
    {
        var s = EngageTable();
        tenbatsu = Add(s, Tenbatsu, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        virtue = Add(s, darkVirtue ? AtAnyCost : "training-focus-strategy", 0, Zone.Hand);
        var bottom = FateTop(s, 0);
        top = [FateTop(s, 0), FateTop(s, 0), FateTop(s, 0)];
        Check(bottom.Id.Length > 0);
        return MatchEngine.FromTestState(s);
    }

    private static void TenbatsuLook()
    {
        var e = TenbatsuTable(true, out var tenbatsu, out var virtue, out var top);
        Engage(e);
        Accept(e, Enabled(e, 0, "tenbatsu-engage-look", tenbatsu.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "lookArrange", "the look is open");
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["2:top", "1:top", "0:top"]));
        Eq(string.Join(",", e.State.Players[0].FateDeck.Take(3)), string.Join(",", top[0].Id, top[1].Id, top[2].Id), "the three cards were put back in the chosen order");
        Eq(e.State.PendingChoice?.Type, "p3DarkVirtueDiscard", "then the Dark Virtue decision");
        var options = e.QueryActions(0).Where(a => a.Type == "p3DiscardDarkVirtue").ToArray();
        Check(options.Length == 2 && options.Any(o => o.Command!.TargetId == virtue.Id) && options.Any(o => o.Command!.TargetId is null), "discard the Dark Virtue, or decline");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, options.First(o => o.Command!.TargetId == virtue.Id).Command!);
        Eq(e.State.Cards[virtue.Id].Zone, Zone.FateDiscard, "the Dark Virtue was discarded");
        Eq(e.State.Players[0].Hand.Count, hand, "and a card drawn in its place");
        Check(e.State.Players[0].Hand.Contains(top[0].Id), "the drawn card is the new top card");
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is given back");
    }

    private static void TenbatsuEdges()
    {
        var e = TenbatsuTable(true, out var tenbatsu, out var virtue, out _);
        Engage(e);
        Accept(e, Enabled(e, 0, "tenbatsu-engage-look", tenbatsu.Id).Command!);
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:top", "1:top", "2:top"]));
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, e.QueryActions(0).Single(a => a.Type == "p3DiscardDarkVirtue" && a.Command!.TargetId is null).Command!);
        Eq(e.State.Cards[virtue.Id].Zone, Zone.Hand, "declined: the Dark Virtue stays");
        Eq(e.State.Players[0].Hand.Count, hand, "and nothing is drawn");
        Check(!Offered(e, 0, "tenbatsu-engage-look"), "once a turn");
        // No Dark Virtue in hand: nothing is asked after the look.
        var n = TenbatsuTable(false, out var t2, out var plain, out _);
        Engage(n);
        Accept(n, Enabled(n, 0, "tenbatsu-engage-look", t2.Id).Command!);
        Accept(n, new GameCommand("arrangeTopCards", 0, n.Version, CardIds: ["0:top", "1:top", "2:top"]));
        Eq(n.State.PendingChoice?.Type, "engageReaction", "no Dark Virtue: straight back to the Engage window");
        Eq(n.State.Cards[plain.Id].Zone, Zone.Hand, "the other card is untouched");
    }

    // ---- Daidoji Akeha ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AkehaDraw()
    {
        MatchEngine Build(int burnOwner, out CardInstance akeha, out CardInstance burn)
        {
            var s = EngageTable();
            akeha = Add(s, Akeha, 0, Zone.Battlefield, province: 0);
            var scout = burnOwner == 0 ? akeha : Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (burnOwner == 1) GiveKeyword(scout, "Scout"); else Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            burn = Add(s, BurnTheTowers, burnOwner, Zone.Hand);
            FateTop(s, 0); FateTop(s, 1);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(0, out var akeha, out var burn);
        Engage(e);
        Accept(e, Enabled(e, 0, "burn-towers-recon", burn.Id, akeha.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the Recon Strategy's resolution opens the after-action window");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Enabled(e, 0, "akeha-recon-draw", akeha.Id).Command!);
        Eq(e.State.Players[0].Hand.Count, hand + 1, "Akeha drew a card");
        // Another player's Recon Strategy: nothing for Akeha.
        var o = Build(1, out var akeha2, out var burn2);
        Accept(o, new GameCommand("selectBattle", 0, o.Version, Province: 0));
        if (o.State.PriorityPlayerId != 1) Accept(o, Pass(o));
        Accept(o, Enabled(o, 1, "burn-towers-recon", burn2.Id).Command!);
        Check(!Offered(o, 0, "akeha-recon-draw"), "another player's Recon action gives Akeha nothing");
        Check(akeha2.Id.Length > 0);
    }

    private static void AkehaSearch()
    {
        var s = Table(Phase.Action, 0);
        var akeha = Add(s, Akeha, 0, Zone.Home);
        var discard = Add(s, "training-focus-strategy", 0, Zone.Hand);
        var recon = FateTop(s, 0, BurnTheTowers);
        var plain = FateTop(s, 0);
        var unique = FateTop(s, 0, Akeha); // a Unique card, with a Recon ability of its own
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "akeha-recon-search", akeha.Id, discard.Id));
        Eq(e.State.Cards[discard.Id].Zone, Zone.FateDiscard, "the card was discarded as the cost");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search is open");
        var view = e.GetView(0).PendingSearch!;
        var eligible = view.Entries!.Where(c => c.Eligible).Select(c => c.DefinitionId).ToArray();
        Eq(string.Join(",", eligible), BurnTheTowers, "only the non-Unique card with a Recon ability qualifies");
        Check(view.Entries!.Any(c => c.DefinitionId == Akeha && !c.Eligible) && view.Entries!.Any(c => c.DefinitionId == "training-focus-strategy" && !c.Eligible), "the Unique and the non-Recon cards are listed ineligible");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: BurnTheTowers));
        Check(e.State.Players[0].Hand.Contains(recon.Id), "the Recon card went to the hand");
        Check(plain.Id.Length > 0 && unique.Id.Length > 0);
    }

    // ---- Erosion -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine ErosionTable(bool water, out CardInstance erosion, out CardInstance mover, out CardInstance strategy)
    {
        var s = Table(Phase.Battle, 1);
        var shugenja = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        GiveKeyword(shugenja, "Shugenja"); if (water) GiveKeyword(shugenja, "Water");
        erosion = Add(s, Erosion, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        mover = Add(s, "training-lion-samurai", 1, Zone.Home);
        strategy = Add(s, "training-move-battle-strategy", 1, Zone.Hand);
        return MatchEngine.FromTestState(s);
    }

    private static void ErosionScenario()
    {
        var e = ErosionTable(true, out var erosion, out var mover, out var strategy);
        Eq(e.ProvinceStrengthForTest(1, 0), e.State.Players[1].Provinces[0].Strength - 1, "the other player's province has -1 strength while the Shugenja is Water");
        Accept(e, Offer(e, 1, "training-move-to-battle", strategy.Id, mover.Id));
        PassUntil(e, () => e.State.PendingChoice?.Type == "afterActionResolvedReaction" || e.State.PendingChoice is null);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "a move to the current battlefield opens the window");
        Accept(e, Offer(e, 0, "erosion-weaken", erosion.Id, mover.Id));
        Eq(ForceMods(e, mover.Id), -4, "the moved Personality has -4F");
    }

    private static void ErosionEdges()
    {
        var e = ErosionTable(false, out var erosion, out _, out _);
        Eq(e.ProvinceStrengthForTest(1, 0), e.State.Players[1].Provinces[0].Strength, "not Water: no penalty");
        Eq(e.ProvinceStrengthForTest(0, 0), e.State.Players[0].Provinces[0].Strength, "its own player's provinces are never lowered");
        // A move home: no Reaction.
        var s = Table(Phase.Battle, 1);
        var shugenja = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, Erosion, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        var target = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var incapacitated = Add(s, "oracle-l5r-3730", 1, Zone.Hand); // Incapacitated: move a defending Personality home
        var h = MatchEngine.FromTestState(s);
        Accept(h, Offer(h, 1, "incapacitated", incapacitated.Id, target.Id));
        PassUntil(h, () => h.State.Cards[target.Id].Zone == Zone.Home && h.State.PendingChoice?.Type is "afterActionResolvedReaction" or null);
        Eq(h.State.Cards[target.Id].Zone, Zone.Home, "the defender moved home");
        Check(!Offered(h, 0, "erosion-weaken"), "a move home offers Erosion nothing");
        Check(erosion.Id.Length > 0);
    }

    // ---- Centering the Soul ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine CenteringTable(bool voidShugenja, out CardInstance centering, out CardInstance ring, out CardInstance chukage)
    {
        var s = Table(Phase.Action, 0);
        var shugenja = Add(s, "training-crane-samurai", 0, Zone.Home);
        GiveKeyword(shugenja, "Shugenja"); if (voidShugenja) GiveKeyword(shugenja, "Void");
        centering = Add(s, Centering, 0, Zone.Attached, parent: shugenja.Id);
        chukage = Add(s, AsakoChukage, 0, Zone.Home);
        ring = Add(s, "training-ring-of-void", 0, Zone.Home);
        for (var i = 0; i < 6; i++) FateTop(s, 0);
        return MatchEngine.FromTestState(s);
    }

    private static void CenteringScenario()
    {
        var e = CenteringTable(false, out var centering, out var ring, out var chukage);
        Accept(e, Offer(e, 0, "chukage-draw", chukage.Id, ring.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "an action bowed a Ring");
        Check(!CanUse(e, 0, "centering-soul-straighten-destroy", centering.Id), "not Void: the destroying play is not offered");
        Accept(e, Offer(e, 0, "centering-soul-straighten", centering.Id, ring.Id));
        Check(!e.State.Cards[ring.Id].Bowed, "the Ring was straightened");
        Check(e.State.Cards[centering.Id].Bowed && e.State.Cards[centering.Id].Zone == Zone.Attached, "the Spell bowed as its cost and stays");
        // Void: the Spell is destroyed and the Ring may be used an additional time this turn.
        var v = CenteringTable(true, out var c2, out var ring2, out var chukage2);
        Accept(v, Offer(v, 0, "chukage-draw", chukage2.Id, ring2.Id));
        Accept(v, Offer(v, 0, "centering-soul-straighten-destroy", c2.Id, ring2.Id));
        Eq(v.State.Cards[c2.Id].Zone, Zone.FateDiscard, "the Spell was destroyed");
        Check(!v.State.Cards[ring2.Id].Bowed, "the Ring was straightened");
        PassUntil(v, () => v.State.PendingChoice is null);
        if (v.State.PriorityPlayerId != 0) Accept(v, new GameCommand("pass", 1, v.Version));
        v = Edit(v, st => st.PriorityPlayerId = 0);
        Accept(v, Enabled(v, 0, "ring-of-void-draw", ring2.Id).Command!);
        PassUntil(v, () => v.State.PendingChoice is null);
        // Straightened again (the Ring bows for its own action), it may be used a second time this turn -- the additional use.
        v = Edit(v, st => { st.Cards[ring2.Id].Bowed = false; st.PriorityPlayerId = 0; });
        Check(CanUse(v, 0, "ring-of-void-draw", ring2.Id), "the Ring's ability may be used an additional time this turn");
        Accept(v, Enabled(v, 0, "ring-of-void-draw", ring2.Id).Command!);
        PassUntil(v, () => v.State.PendingChoice is null);
        v = Edit(v, st => { st.Cards[ring2.Id].Bowed = false; st.PriorityPlayerId = 0; });
        Check(!CanUse(v, 0, "ring-of-void-draw", ring2.Id), "but only one additional time");
    }

    private static void CenteringEdges()
    {
        // The opponent's Ring bowed by an action is not "one of your Rings".
        var s = Table(Phase.Action, 1);
        var shugenja = Add(s, "training-crane-samurai", 0, Zone.Home);
        GiveKeyword(shugenja, "Shugenja");
        var centering = Add(s, Centering, 0, Zone.Attached, parent: shugenja.Id);
        var chukage = Add(s, AsakoChukage, 1, Zone.Home);
        var ring = Add(s, "training-ring-of-air", 1, Zone.Home);
        FateTop(s, 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "chukage-draw", chukage.Id, ring.Id));
        Check(!Offered(e, 0, "centering-soul-straighten"), "an opponent's Ring is no target");
        // One use for both plays.
        var v = CenteringTable(true, out var c2, out var ring2, out var chukage2);
        Accept(v, Offer(v, 0, "chukage-draw", chukage2.Id, ring2.Id));
        Accept(v, Offer(v, 0, "centering-soul-straighten", c2.Id, ring2.Id));
        Check(!CanUse(v, 0, "centering-soul-straighten-destroy", c2.Id), "the other play shares the used use (and the Spell is bowed)");
        Check(centering.Id.Length > 0);
    }

    // ---- Deep Snows ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine SnowsTable(out CardInstance snows, out CardInstance mover, out CardInstance strategy, out CardInstance bower, out CardInstance enemy)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        snows = Add(s, DeepSnows, 0, Zone.Hand);
        bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        mover = Add(s, "training-lion-samurai", 1, Zone.Home);
        strategy = Add(s, "training-move-battle-strategy", 1, Zone.Hand);
        return MatchEngine.FromTestState(s);
    }

    private static void DeepSnowsScenario()
    {
        var e = SnowsTable(out var snows, out var mover, out var strategy, out var bower, out var enemy);
        Check(!e.QueryActions(0).Any(a => a.Type == "playStrategy" && a.CardId == snows.Id), "Deep Snows is not played as an ordinary Terrain");
        Accept(e, Offer(e, 0, "training-bow-at-battle", bower.Id, enemy.Id));
        PassUntil(e, () => e.State.PendingChoice?.Type is "afterActionResolvedReaction" or null);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "a Battle action resolved");
        Accept(e, Enabled(e, 0, "deep-snows-enter", snows.Id).Command!);
        Eq(e.State.Cards[snows.Id].Zone, Zone.Battlefield, "Deep Snows is in play at the battlefield");
        PassUntil(e, () => e.State.PendingChoice is null);
        // Now the opponent moves a Personality to the battle: the trait bows him.
        if (e.State.PriorityPlayerId != 1) Accept(e, new GameCommand("pass", 0, e.Version));
        Accept(e, Offer(e, 1, "training-move-to-battle", strategy.Id, mover.Id));
        PassUntil(e, () => e.State.Cards[mover.Id].Zone == Zone.Battlefield && e.State.PendingChoice is null);
        Check(e.State.Cards[mover.Id].Bowed, "the Personality the action moved was bowed by Deep Snows' trait");
    }

    private static void DeepSnowsEdges()
    {
        // A non-Battle (Limited) action resolving: no offer. (An Action Phase table.)
        var s = Table(Phase.Action, 0);
        var snows = Add(s, DeepSnows, 0, Zone.Hand);
        var chukage = Add(s, AsakoChukage, 0, Zone.Home);
        var ring = Add(s, "training-ring-of-air", 0, Zone.Home);
        FateTop(s, 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "chukage-draw", chukage.Id, ring.Id));
        Check(!Offered(e, 0, "deep-snows-enter"), "not after a non-Battle action (and not outside a battle)");
        // A Terrain already there: not offered.
        var t = SnowsTable(out var snows2, out _, out _, out var bower, out var enemy);
        t = Edit(t, st => { var terrain = Add(st, "oracle-l5r-1921", 1, Zone.Battlefield, province: 0); terrain.FaceUp = true; });
        Accept(t, Offer(t, 0, "training-bow-at-battle", bower.Id, enemy.Id));
        PassUntil(t, () => t.State.PendingChoice?.Type is "afterActionResolvedReaction" or null);
        Check(!Offered(t, 0, "deep-snows-enter"), "a Terrain stands at the battlefield: not offered");
        Check(snows.Id.Length > 0 && snows2.Id.Length > 0);
    }

    // ---- Request Authorization ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RequestMove()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, RequestAuthorization, 0, Zone.Hand);
        var courier = Add(s, "training-crane-courtier", 0, Zone.Home);
        GiveKeyword(courier, "Courtier");
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "request-authorization-move", card.Id, enemy.Id, courier.Id));
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the target moved home");
        Check(e.State.Cards[courier.Id].Bowed, "the Courtier at home bowed as the cost");
        // No unbowed Courtier: not offered.
        var n = Table(Phase.Battle, 0);
        var card2 = Add(n, RequestAuthorization, 0, Zone.Hand);
        Add(n, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(n, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var ne = MatchEngine.FromTestState(n);
        Check(!CanUse(ne, 0, "request-authorization-move", card2.Id), "no Courtier: no performer");
    }

    private static void RequestNegate()
    {
        MatchEngine Build(out CardInstance card, out CardInstance mover, out CardInstance strategy)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            card = Add(s, RequestAuthorization, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            mover = Add(s, "training-lion-samurai", 1, Zone.Home);
            strategy = Add(s, "training-move-battle-strategy", 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var card, out var mover, out var strategy);
        Accept(e, Offer(e, 1, "training-move-to-battle", strategy.Id, mover.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the move to the battle is held");
        if (e.State.PriorityPlayerId != 0) Accept(e, Pass(e));
        Accept(e, Options(e, 0, "request-authorization-negate", card.Id).Single(a => a.Enabled && a.Command?.TargetId == mover.Id).Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "his movement to the current battlefield was negated");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Declined: he moves.
        var d = Build(out _, out var mover2, out var strategy2);
        Accept(d, Offer(d, 1, "training-move-to-battle", strategy2.Id, mover2.Id));
        PassUntil(d, () => d.State.PendingChoice is null);
        Eq(d.State.Cards[mover2.Id].Zone, Zone.Battlefield, "declined: he moved to the battle");
        // An action that moves nobody to the battle: no window for it.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 1;
        Add(s3, RequestAuthorization, 0, Zone.Hand);
        var target = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bower = Add(s3, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-bow-at-battle", bower.Id, target.Id));
        Check(e3.State.PendingChoice?.Type != "beforeActionReaction", "a bow moves nobody: no window");
    }

    // ---- Iron Will --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine IronWillTable(int paragonHonorBonus, bool paragon, out CardInstance ironWill, out CardInstance target, out CardInstance courier, out CardInstance request)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        ironWill = Add(s, IronWill, 0, Zone.Hand);
        target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        if (paragon) GiveKeyword(target, "Paragon");
        if (paragonHonorBonus != 0) target.Modifiers.Add(new("test-fixture", "test-fixture", "PersonalHonor", paragonHonorBonus));
        courier = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Personal Honor 3
        GiveKeyword(courier, "Courtier");
        request = Add(s, RequestAuthorization, 1, Zone.Hand);
        return MatchEngine.FromTestState(s);
    }

    private static void IronWillScenario()
    {
        var e = IronWillTable(2, true, out var ironWill, out var target, out var courier, out var request);
        Accept(e, Offer(e, 1, "request-authorization-move", request.Id, target.Id, courier.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the Battle action targeting the Paragon is held");
        if (e.State.PriorityPlayerId != 0) Accept(e, Pass(e));
        Accept(e, Enabled(e, 0, "iron-will-negate", ironWill.Id).Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "its effects were negated: the Paragon did not move");
        Check(!e.State.Cards[courier.Id].Bowed, "its costs were ignored: the Courtier did not bow");
        Eq(e.State.Cards[request.Id].Zone, Zone.FateDiscard, "the action was still taken (its Strategy is spent)");
        Eq(e.State.Cards[ironWill.Id].Zone, Zone.FateDiscard, "Iron Will is spent");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 1), "that player may take an additional Battle action");
        Eq(e.State.PriorityPlayerId, 1, "and he holds the opportunity to take it");
    }

    private static void IronWillEdges()
    {
        // Equal Personal Honor (3 against 3): not lower.
        var e = IronWillTable(1, true, out var ironWill, out var target, out var courier, out var request);
        Accept(e, Offer(e, 1, "request-authorization-move", request.Id, target.Id, courier.Id));
        Check(e.State.PendingChoice?.Type != "beforeActionReaction" || !Offered(e, 0, "iron-will-negate"), "equal Personal Honor: not offered");
        // Not a Paragon.
        var n = IronWillTable(2, false, out _, out var target2, out var courier2, out var request2);
        Accept(n, Offer(n, 1, "request-authorization-move", request2.Id, target2.Id, courier2.Id));
        Check(n.State.PendingChoice?.Type != "beforeActionReaction" || !Offered(n, 0, "iron-will-negate"), "not a Paragon: not offered");
        Check(ironWill.Id.Length > 0);
    }
}
