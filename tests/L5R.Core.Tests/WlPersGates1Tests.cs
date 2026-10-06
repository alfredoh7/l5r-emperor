using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #674, cluster "pers-gates-1": personality-wave-128, fourteen Gates of Chaos Personalities wired from the printed text of their scans (Asahina Kitiaru - exp, Asako Ifukube, Asako Karachu - exp,
// Boyoh Mercenary, Daidoji Tametaka - exp, Daidoji Tobei, Doji Razan, Hida Gojiro, Hoshoku-sha, Ide Hinobu, Isawa Hajime, Kitsune Yuko, Legulus, M'rika - exp). Every scenario is an explicit position on
// the economy table (Batch336Support.Table: player 0 the Crane Defender, player 1 the Lion Attacker, province 0 the current battlefield in a Battle) driven only through QueryActions and the commands the
// engine offers. The mechanics are in src/L5R.Core/WlPersGates1.cs.
internal static class WlPersGates1Tests
{
    private const string Wave = "personality-wave-128";
    private const string Kitiaru = "oracle-l5r-10623", Ifukube = "oracle-l5r-10648", Karachu = "oracle-l5r-10647", Boyoh = "oracle-l5r-10669", Tametaka = "oracle-l5r-10627", Tobei = "oracle-l5r-10628",
        Razan = "oracle-l5r-10626", Gojiro = "oracle-l5r-10617", Hoshoku = "oracle-l5r-10670", Hinobu = "oracle-l5r-10672", Hajime = "oracle-l5r-10649", Yuko = "oracle-l5r-10641", Legulus = "oracle-l5r-10668",
        Mrika = "oracle-l5r-10661";
    private const string Hiromi = "oracle-l5r-9558", Legionnaire = "oracle-l5r-10688";
    private const string Crane = "training-crane-samurai", Lion = "training-lion-samurai", Follower = "training-follower", FudoPersonality = "training-fudo-personality", Focus = "training-focus-strategy",
        RingAir = "training-false-ring-of-air", RingWater = "training-false-ring-of-water";
    private static readonly string[] AllIds = [Kitiaru, Ifukube, Karachu, Boyoh, Tametaka, Tobei, Razan, Gojiro, Hoshoku, Hinobu, Hajime, Yuko, Legulus, Mrika];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId) => Batch336Support.Offer(e, player, ability, cardId, targetId);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static void PassWindow(MatchEngine e) { for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static GameCommand Use(MatchEngine e, int player, string ability, CardInstance card, string? target = null) => new("useAbility", player, e.Version, card.Id, AbilityId: ability, TargetId: target);
    private static void SetDeck(MatchState s, int player, params string[] defs)
    {
        var deck = s.Players[player].FateDeck;
        foreach (var id in deck.ToArray()) s.Cards.Remove(id);
        deck.Clear();
        foreach (var def in defs) { var c = Add(s, def, player, Zone.FateDeck); c.FaceUp = false; deck.Add(c.Id); }
    }
    private static string[] Offered(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "selectSearchCard").Select(a => a.Command!.Choice!).Order(StringComparer.Ordinal).ToArray();

    private static void RejectedWithoutChange(MatchEngine e, GameCommand command, string? contains = null)
    {
        var before = Json(e);
        var error = Batch336Support.Rejection(e, command);
        if (contains is not null) Check(error.Contains(contains, StringComparison.OrdinalIgnoreCase), $"rejection says '{contains}': {error}");
        Eq(Json(e), before, "a rejected action changes nothing");
    }

    public static void Run(TestRunner runner) => runner.Run("wl-pers-gates-1", [
        ("Published shape: the fourteen Personalities carry the printed stats, keywords, text, abilities and effects of their scans, name the Gates of Chaos printing, ship art and are in the wave manifest", PublishedShape),
        ("Asahina Kitiaru - exp: bow to search the discard pile (never the deck while it holds a Ring), put the Ring in the hand and discard another card", KitiaruDiscardPile),
        ("Asahina Kitiaru - exp: with no Ring in the discard pile the Fate deck is searched and shuffled; with none anywhere nothing is discarded; a lone Ring in the hand owes no discard", KitiaruDeckAndEdges),
        ("Asahina Kitiaru - exp: a bowed Kitiaru and the other player are refused and change nothing", KitiaruRefused),
        ("Asako Ifukube: Destined draws as he enters play and Expendable draws as he is destroyed", IfukubeDestinedExpendable),
        ("Asako Karachu - exp: one Open action gives him Cavalry, Conqueror or Naval until the turn ends, once", KarachuKeywords),
        ("Boyoh Mercenary: entering play makes his controller discard a card (nothing owed with an empty hand)", BoyohEntryDiscard),
        ("Boyoh Mercenary: each Boyoh in the army that destroys a province draws a card for his controller, in a battle's resolution and nowhere else", BoyohProvinceDraw),
        ("Daidoji Tametaka - exp: he loses any duel against Yoritomo Hiromi whichever side he is on, and wins as before against anyone else", TametakaLosesToHiromi),
        ("Daidoji Tametaka - exp: Battle: reduce an enemy Personality's or Follower's Force to 0; nothing else is a target", TametakaZeroesForce),
        ("Daidoji Tobei: Destined and the entering Honor loss; bow for a Ranged 2 Attack", TobeiEntryAndAttack),
        ("Doji Razan: Invest 1 Gold gives a +1C token (optional, declinable); Limited destroys a target Fudo card and then Razan", RazanInvestAndDestroy),
        ("Hida Gojiro and Isawa Hajime: the entering Honor loss and Invest (4 Gold for two +1F tokens, 3 Gold for two +1F Fire tokens)", GojiroAndHajimeInvest),
        ("Hoshoku-sha: Battle/Open destroys your target Personality and gives him +2F, even when the target is himself", HoshokuDestroy),
        ("Ide Hinobu: a printed production of 3 Gold that bows her", HinobuProduces),
        ("Kitsune Yuko: Invest 2 Gold to gain 1 Honor, and Limited bow to gain 1 Honor", YukoHonor),
        ("Legulus: the entering Honor loss, -3 strength against Melee and Ranged Attacks targeting his unit and the Yodotai Legionnaire search", LegulusAll),
        ("M'rika - exp: Open, bow: bow a target Personality (either player's), once she is unbowed", MrikaBows),
    ]);

    // ---- shape --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = Batch336Support.RepoRoot();
        Check(new HashSet<string>(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", Wave + ".txt"))).SetEquals(AllIds), "the manifest is exactly the wired cards");
        var rows = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", Wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(rows.Length, 14, "a provenance row per card");
        foreach (var row in rows) Eq(row.GetProperty("printing_set")[0].GetString(), "Gates of Chaos", row.GetProperty("card_id").GetString() + ": its printing");
        foreach (var id in AllIds) Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), id + " ships art");
        // id, name, clan, gold, force, chi, personal honor, honor requirement, keywords, abilities, effect, text start
        void Shape(string id, string name, string clan, int gold, int force, int chi, int ph, int? hr, string[] keywords, string[] abilities, string effect, string textStart)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == CardType.Personality && d.Name == name && d.Clan == clan && d.GoldCost == gold && d.Force == force && d.Chi == chi && d.PersonalHonor == ph && d.HonorRequirement == hr && !d.ScenarioOnly && !d.CreatedOnly,
                $"{name}: printed stats");
            Check(d.Keywords!.SequenceEqual(keywords), $"{name}: printed keywords {string.Join(",", d.Keywords!)}");
            Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", d.Abilities ?? [])}");
            Eq(d.Effect, effect, $"{name}: effect");
            Check(d.Text.StartsWith(textStart, StringComparison.Ordinal), $"{name}: printed text '{d.Text}'");
        }
        Shape(Kitiaru, "Asahina Kitiaru", "Crane", 7, 0, 5, 4, 10, ["Unique", "Courtier", "Crane Clan", "Experienced", "Monk"], [WlPersGates1Abilities.KitiaruSearch], "none", "Limited, Bow this card: Search your discard pile, then Fate deck, for a Ring.");
        Shape(Ifukube, "Asako Ifukube", "Phoenix", 6, 0, 4, 3, 12, ["Destined", "Expendable", "Earth", "Inquisitor", "Phoenix Clan", "Shugenja"], [], "none", "(Draw a card after your Destined card enters play.)");
        Shape(Karachu, "Asako Karachu", "Phoenix", 9, 4, 3, 2, 4, ["Unique", "Air", "Experienced", "Golden Arm", "Henshin", "Monk", "Phoenix Clan", "Protector of the Second City"],
            [WlPersGates1Abilities.KarachuCavalry, WlPersGates1Abilities.KarachuConqueror, WlPersGates1Abilities.KarachuNaval], "none", "Open: Give Karachu Cavalry, Conqueror, or Naval.");
        Shape(Boyoh, "Boyoh Mercenary", "Unaligned", 5, 3, 2, 0, null, ["Mercenary"], [], WlPersGates1Abilities.BoyohEffect, "After Boyoh Mercenary enters play, discard a card.");
        Shape(Tametaka, "Daidoji Tametaka", "Crane", 8, 4, 3, 2, 4, ["Unique", "Commander", "Crane Clan", "Experienced", "Iron Crane", "Samurai", "Scout"], [WlPersGates1Abilities.TametakaZero], WlPersGates1Abilities.TametakaEffect,
            "Tametaka loses any duel against Yoritomo Hiromi.");
        Shape(Tobei, "Daidoji Tobei", "Crane", 7, 3, 2, 0, null, ["Destined", "Crane Clan", "Fallen", "Samurai", "Scout"], [WlPersGates1Abilities.TobeiAttack], "none", "(Draw a card after your Destined card enters play.)");
        Shape(Razan, "Doji Razan", "Crane", 5, 2, 3, 3, 2, ["Duelist", "Courtier", "Crane Clan", "Magistrate", "Samurai"], [WlPersGates1Abilities.RazanInvest, WlPersGates1Abilities.RazanDestroy], "none", "Invest :g1:: Give Razan a +1C token.");
        Shape(Gojiro, "Hida Gojiro", "Crab", 5, 3, 3, 0, null, ["Berserker", "Crab Clan", "Fallen"], [WlPersGates1Abilities.GojiroInvest], "none", "Invest :g4:: Give Gojiro two +1F tokens.");
        Shape(Hoshoku, "Hoshoku-sha", "Unaligned", 7, 4, 1, 0, null, ["Nonhuman", "Wani"], [WlPersGates1Abilities.HoshokuDestroy], "none", "Battle/Open: Destroy your target Personality. Give Hoshoku-sha +2F.");
        Shape(Hinobu, "Ide Hinobu", "Unicorn", 6, 0, 3, 3, 4, ["Courtier", "Merchant", "Unicorn Clan"], [], "none", "Bow this card: Produce 3 Gold.");
        Shape(Hajime, "Isawa Hajime", "Phoenix", 5, 2, 3, 0, null, ["Fallen", "Fire", "Phoenix Clan", "Shugenja"], [WlPersGates1Abilities.HajimeInvest], "none", "Invest :g3:: Give Hajime two +1F Fire tokens.");
        Shape(Yuko, "Kitsune Yuko", "Mantis", 6, 1, 4, 3, 2, ["Earth", "Kitsune", "Mantis Clan", "Nonhuman", "Shugenja", "Spirit"], [WlPersGates1Abilities.YukoInvest, WlPersGates1Abilities.YukoHonor], "none", "Invest :g2:: Gain 1 Honor.");
        Shape(Legulus, "Legulus", "Unaligned", 10, 4, 4, 0, null, ["Conqueror", "Unique", "Commander", "Gaijin", "Yodotai"], [WlPersGates1Abilities.LegulusSearch], "none", "After Legulus enters play, lose 2 Honor.");
        Shape(Mrika, "M'rika", "Spider", 8, 1, 4, 3, null, ["Loyal", "Unique", "Artisan", "Bog Hag", "Experienced", "Nonhuman", "Shadowlands", "Shugenja", "Spider Clan"], [WlPersGates1Abilities.MrikaBow], "none", "Open, Bow this card: Bow a target Personality.");
        Eq(CardCatalog.Get(Tobei).EntryHonorLoss, 1, "Tobei: lose 1 Honor"); Eq(CardCatalog.Get(Gojiro).EntryHonorLoss, 1, "Gojiro: lose 1 Honor"); Eq(CardCatalog.Get(Hajime).EntryHonorLoss, 1, "Hajime: lose 1 Honor");
        Eq(CardCatalog.Get(Legulus).EntryHonorLoss, 2, "Legulus: lose 2 Honor");
        var legulus = CardCatalog.Get(Legulus);
        Check(legulus.AttackDefenseKind == "Both" && legulus.AttackDefenseAmount == -3 && legulus.AttackDefenseScope == "Unit", "Legulus: -3 strength to Melee and Ranged Attacks targeting cards in his unit");
        Eq(CardCatalog.Get(Hinobu).GoldProduction, 3, "Hinobu: produces 3 Gold");
        foreach (var id in AllIds.SelectMany(id => CardCatalog.Get(id).Abilities ?? [])) Check(AbilityCatalog.All.ContainsKey(id), id + " is catalogued");
        foreach (var spec in WlPersGates1Searches.All) Check(SearchCatalog.All.ContainsKey(spec.Id), spec.Id + " is a catalogued search");
        foreach (var id in new[] { WlPersGates1Abilities.RazanInvest, WlPersGates1Abilities.GojiroInvest, WlPersGates1Abilities.HajimeInvest, WlPersGates1Abilities.YukoInvest })
            Check(TraitReactions.Is(id), id + ": Invest is a trait, not a Reaction (R4)");
    }

    // ---- Asahina Kitiaru - exp ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Kitiaru, CardInstance Fodder) KitiaruRig(Action<MatchState>? arrange = null, bool fodder = true)
    {
        var s = Table(Phase.Action, 0);
        var kitiaru = Add(s, Kitiaru, 0, Zone.Home);
        var held = fodder ? Add(s, Focus, 0, Zone.Hand) : null;
        arrange?.Invoke(s);
        return (MatchEngine.FromTestState(s), kitiaru, held!);
    }

    private static void KitiaruDiscardPile()
    {
        var (e, kitiaru, fodder) = KitiaruRig(s => { Add(s, RingAir, 0, Zone.FateDiscard); SetDeck(s, 0, RingWater); });
        var ringInPile = e.State.Cards.Values.Single(c => c.DefinitionId == RingAir);
        Accept(e, Enabled(e, 0, WlPersGates1Abilities.KitiaruSearch, kitiaru.Id).Command!);
        Check(e.State.Cards[kitiaru.Id].Bowed, "Kitiaru bowed as the cost");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opened");
        Eq(string.Join(",", Offered(e, 0)), RingAir, "only the discard pile's Ring is offered: the deck is not searched while the pile holds one (R-011)");
        Check(!e.Execute(new GameCommand("passSearch", 0, e.Version)).Accepted, "he must take it");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingAir));
        Eq(e.State.Cards[ringInPile.Id].Zone, Zone.Hand, "the Ring is in his hand");
        Eq(e.State.PendingChoice?.Type, "searchDiscard", "he must now discard a card");
        var offered = e.QueryActions(0).Where(a => a.Type == "chooseSearchDiscard").Select(a => a.Command!.TargetId!).ToArray();
        Eq(string.Join(",", offered), fodder.Id, "only the other card of the hand: the Ring he just found cannot be the one discarded");
        RejectedWithoutChange(e, new GameCommand("chooseSearchDiscard", 0, e.Version, TargetId: ringInPile.Id));
        Accept(e, new GameCommand("chooseSearchDiscard", 0, e.Version, TargetId: fodder.Id));
        Eq(e.State.Cards[fodder.Id].Zone, Zone.FateDiscard, "the other card was discarded");
        Eq(e.State.Cards[ringInPile.Id].Zone, Zone.Hand, "the Ring stays in his hand");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null, "the search is over");
        Check(!e.State.Events.Any(x => x.Type == "deckShuffled"), "the Fate deck was never searched, so it was not shuffled");
        Eq(e.State.Players[0].FateDeck.Count, 1, "the deck still holds its Ring");
        Eq(e.State.PriorityPlayerId, 1, "the action ended");
        Check(!CanUse(e, 0, WlPersGates1Abilities.KitiaruSearch, kitiaru.Id), "a bowed Kitiaru may not search again");
    }

    private static void KitiaruDeckAndEdges()
    {
        // The pile holds no Ring: pass it, and the Fate deck is searched (only Rings qualify) and reshuffled.
        var (e, kitiaru, fodder) = KitiaruRig(s => { Add(s, Focus, 0, Zone.FateDiscard); SetDeck(s, 0, Focus, RingWater); });
        Accept(e, Enabled(e, 0, WlPersGates1Abilities.KitiaruSearch, kitiaru.Id).Command!);
        Check(Offered(e, 0).Length == 0, "the discard pile holds no Ring");
        Accept(e, new GameCommand("passSearch", 0, e.Version));
        Eq(string.Join(",", Offered(e, 0)), RingWater, "then the Fate deck: only its Ring qualifies");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RingWater));
        Accept(e, new GameCommand("chooseSearchDiscard", 0, e.Version, TargetId: fodder.Id));
        Check(e.State.Cards.Values.Single(c => c.DefinitionId == RingWater).Zone == Zone.Hand, "the Ring came from the deck");
        Check(e.State.Events.Any(x => x.Type == "deckShuffled"), "the searched deck is reshuffled");
        Eq(e.State.Cards[fodder.Id].Zone, Zone.FateDiscard, "and the other card was discarded");

        // No Ring anywhere: both places passed, nothing discarded (the discard follows a found Ring), the bow was the cost.
        var (f, kit2, held2) = KitiaruRig(s => SetDeck(s, 0, Focus));
        Accept(f, Enabled(f, 0, WlPersGates1Abilities.KitiaruSearch, kit2.Id).Command!);
        Accept(f, new GameCommand("passSearch", 0, f.Version));
        Accept(f, new GameCommand("passSearch", 0, f.Version));
        Check(f.State.PendingSearch is null && f.State.PendingChoice is null, "the search is over");
        Eq(f.State.Cards[held2.Id].Zone, Zone.Hand, "no Ring was found, so no card was discarded");
        Check(f.State.Cards[kit2.Id].Bowed, "Kitiaru stays bowed");

        // A found Ring and no other card in the hand: the discard cannot be satisfied and is skipped.
        var (g, kit3, _) = KitiaruRig(s => SetDeck(s, 0, RingAir), fodder: false);
        Accept(g, Enabled(g, 0, WlPersGates1Abilities.KitiaruSearch, kit3.Id).Command!);
        Accept(g, new GameCommand("passSearch", 0, g.Version));
        Accept(g, new GameCommand("selectSearchCard", 0, g.Version, Choice: RingAir));
        Check(g.State.PendingSearch is null && g.State.PendingChoice is null, "nothing else to discard: the search simply ends");
        Eq(g.State.Players[0].Hand.Count, 1, "the Ring alone is in his hand");
    }

    private static void KitiaruRefused()
    {
        var (bowed, kitiaru, _) = KitiaruRig(s => { SetDeck(s, 0, RingAir); s.Cards.Values.Single(c => c.DefinitionId == Kitiaru).Bowed = true; });
        Check(!CanUse(bowed, 0, WlPersGates1Abilities.KitiaruSearch, kitiaru.Id), "a bowed Kitiaru may not use it");
        RejectedWithoutChange(bowed, Use(bowed, 0, WlPersGates1Abilities.KitiaruSearch, kitiaru));
        var (f, kit2, _) = KitiaruRig(s => SetDeck(s, 0, RingAir));
        Check(!CanUse(f, 1, WlPersGates1Abilities.KitiaruSearch, kit2.Id), "the other player is not offered it");
        RejectedWithoutChange(f, new GameCommand("useAbility", 1, f.Version, kit2.Id, AbilityId: WlPersGates1Abilities.KitiaruSearch));
    }

    // ---- Asako Ifukube ----------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 0 (a Dragon Clan player: no Proclaim, no Clan discount) buys `def` from his first province at full price; returns the engine once the purchase is made.
    private static (MatchEngine E, string CardId, int HandBefore, int HonorBefore) Buy(string def, Action<MatchState>? tweak = null)
    {
        var d = CardCatalog.Get(def);
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = "Dragon"; s.Players[0].Honor = 30;
        for (var i = 0; i < d.GoldCost / 2 + 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var entering = s.Cards[s.Players[0].Provinces[0].CardId!]; entering.DefinitionId = def; entering.FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        var (hand, honor) = (e.State.Players[0].Hand.Count, Honor(e, 0));
        var option = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == entering.Id && a.Enabled && a.Command is { ClanDiscount: false });
        Accept(e, option.Command!);
        return (e, entering.Id, hand, honor);
    }

    private static void IfukubeDestinedExpendable()
    {
        var (e, id, hand, _) = Buy(Ifukube, s => SetDeck(s, 0, Focus, Focus, Focus));
        Eq(e.State.Cards[id].Zone, Zone.Home, "he entered play");
        Eq(e.State.Players[0].Hand.Count, hand + 1, "Destined: a card drawn as he entered play");
        var before = e.State.Players[0].Hand.Count;
        Eq(e.DestroyUnitsForTest(id), 1, "he was destroyed");
        Eq(e.State.Players[0].Hand.Count, before + 1, "Expendable: a card drawn as he was destroyed");
    }

    // ---- Asako Karachu - exp ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KarachuKeywords()
    {
        foreach (var (ability, keyword) in new[] { (WlPersGates1Abilities.KarachuCavalry, "Cavalry"), (WlPersGates1Abilities.KarachuConqueror, "Conqueror"), (WlPersGates1Abilities.KarachuNaval, "Naval") })
        {
            var s = Table(Phase.Action, 0);
            var karachu = Add(s, Karachu, 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            foreach (var other in new[] { WlPersGates1Abilities.KarachuCavalry, WlPersGates1Abilities.KarachuConqueror, WlPersGates1Abilities.KarachuNaval })
                Check(CanUse(e, 0, other, karachu.Id), other + " is offered");
            Check(!CanUse(e, 1, ability, karachu.Id), "the other player is not offered it");
            Accept(e, Enabled(e, 0, ability, karachu.Id).Command!);
            Check(e.State.Cards[karachu.Id].GrantedKeywords.Any(k => k.Keyword == keyword && k.Duration == GrantDuration.Turn), $"Karachu has {keyword} until the turn ends");
            Eq(e.State.Cards[karachu.Id].GrantedKeywords.Count, 1, "exactly the one keyword");
            foreach (var other in new[] { WlPersGates1Abilities.KarachuCavalry, WlPersGates1Abilities.KarachuConqueror, WlPersGates1Abilities.KarachuNaval })
                Check(!CanUse(e, 0, other, karachu.Id), other + ": one use of the action per turn");
        }
    }

    // ---- Boyoh Mercenary --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BoyohEntryDiscard()
    {
        var (e, id, hand, _) = Buy(Boyoh);
        Eq(e.State.Cards[id].Zone, Zone.Home, "he entered play");
        Check(hand > 0, "the fixture holds cards");
        Eq(e.State.PendingChoice?.Type, "forcedDiscard", "his controller must discard a card");
        Eq(e.State.PendingChoice?.PlayerId, 0, "and it is his controller who chooses");
        var offers = e.QueryActions(0).Where(a => a.Type == "forcedDiscard").ToArray();
        Eq(offers.Length, e.State.Players[0].Hand.Count, "every card of his hand is offered");
        Check(e.QueryActions(1).All(a => a.Type != "forcedDiscard"), "the other player is offered nothing");
        var chosen = offers[0];
        Accept(e, chosen.Command!);
        Eq(e.State.Players[0].Hand.Count, hand - 1, "one card was discarded");
        Eq(e.State.Cards[chosen.CardId!].Zone, Zone.FateDiscard, "the one he chose");
        Check(e.State.PendingChoice is null, "nothing more is owed");

        // An empty hand owes nothing.
        var (f, id2, _, _) = Buy(Boyoh, s => { foreach (var h in s.Players[0].Hand.ToArray()) s.Cards.Remove(h); s.Players[0].Hand.Clear(); });
        Eq(f.State.Cards[id2].Zone, Zone.Home, "he entered play");
        Check(f.State.PendingChoice?.Type != "forcedDiscard", "an empty hand owes no discard");
    }

    private static void BoyohProvinceDraw()
    {
        // The province-destruction record: Boyoh in the attacking army draws; one that is not in it, or is the other player's, does not; two draw two.
        var s = Table(Phase.Battle, 1);
        var one = Add(s, Boyoh, 1, Zone.Battlefield, province: 0); var two = Add(s, Boyoh, 1, Zone.Battlefield, province: 0); var third = Add(s, Boyoh, 1, Zone.Home);
        var defending = Add(s, Boyoh, 0, Zone.Battlefield, province: 0);
        SetDeck(s, 1, Focus, Focus, Focus, Focus); SetDeck(s, 0, Focus, Focus);
        var e = MatchEngine.FromTestState(s);
        var (mine, theirs) = (e.State.Players[1].Hand.Count, e.State.Players[0].Hand.Count);
        e.ProvinceDestroyedForTest(1, 0, 0, [one.Id, two.Id]);
        Eq(e.State.Players[1].Hand.Count, mine + 2, "each Boyoh of the army drew a card");
        Eq(e.State.Players[0].Hand.Count, theirs, "the other player drew nothing, and his Boyoh was not in the army");
        Check(e.State.Cards[third.Id].Zone == Zone.Home && e.State.Cards[defending.Id].Zone == Zone.Battlefield, "no other card moved");

        // A real battle: Boyoh (made unbeatable) attacks an undefended province and the resolution destroys it.
        var t = Table(Phase.Battle, 1);
        var boyoh = Add(t, Boyoh, 1, Zone.Battlefield, province: 0); boyoh.Modifiers.Add(new("test-source", "test", "Force", 30));
        SetDeck(t, 1, Focus, Focus, Focus, Focus);
        var f = MatchEngine.FromTestState(t);
        var handBefore = f.State.Players[1].Hand.Count;
        Batch336Support.RunUntil(f, () => f.State.Players[0].Provinces[0].Destroyed);
        Eq(f.State.Players[1].Hand.Count, handBefore + 1, "the army destroyed the province: Boyoh's controller drew a card");

        // The same battle without Boyoh draws nothing.
        var u = Table(Phase.Battle, 1);
        var plain = Add(u, Lion, 1, Zone.Battlefield, province: 0); plain.Modifiers.Add(new("test-source", "test", "Force", 30));
        SetDeck(u, 1, Focus, Focus, Focus, Focus);
        var g = MatchEngine.FromTestState(u);
        var handPlain = g.State.Players[1].Hand.Count;
        Batch336Support.RunUntil(g, () => g.State.Players[0].Provinces[0].Destroyed);
        Eq(g.State.Players[1].Hand.Count, handPlain, "without a Boyoh in the army nothing is drawn");
    }

    // ---- Daidoji Tametaka - exp -------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Strong(CardInstance card) { foreach (var stat in new[] { "Force", "Chi", "PersonalHonor" }) card.Modifiers.Add(new("test-source", "test", stat, 20)); }

    private static void RunDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
        Check(e.State.PendingDuel is null, "the duel is over");
    }

    // Player 0 duels with `mine` against player 1's `theirs` (Steel on Steel: the loser is destroyed); `strong` names which side's totals are far higher.
    private static (MatchEngine E, CardInstance Mine, CardInstance Theirs) Duel(string mineDef, string theirDef, bool mineStrong, bool theirsStrong)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, mineDef, 0, Zone.Battlefield, province: 0); var theirs = Add(s, theirDef, 1, Zone.Battlefield, province: 0);
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
        foreach (var owner in new[] { 0, 1 }) for (var i = 0; i < 2; i++) { var top = Add(s, Focus, owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, top.Id); }
        if (mineStrong) Strong(mine);
        if (theirsStrong) Strong(theirs);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        RunDuel(e);
        return (e, mine, theirs);
    }

    private static bool Destroyed(MatchEngine e, CardInstance card) => e.State.Cards[card.Id].Zone is Zone.Dead or Zone.DynastyDiscard or Zone.FateDiscard;

    private static void TametakaLosesToHiromi()
    {
        // Tametaka the challenger, far stronger than Hiromi: he would win, and loses.
        var (e, tametaka, hiromi) = Duel(Tametaka, Hiromi, mineStrong: true, theirsStrong: false);
        Check(Destroyed(e, tametaka), "Tametaka lost the duel he would have won");
        Check(!Destroyed(e, hiromi), "Hiromi won it");
        Check(e.State.Events.Any(x => x.Type == "duelAutoLoss"), "the loss is in the log");
        // Tametaka the challenged duelist: the same.
        var (f, hiromi2, tametaka2) = Duel(Hiromi, Tametaka, mineStrong: false, theirsStrong: true);
        Check(Destroyed(f, tametaka2) && !Destroyed(f, hiromi2), "challenged by Hiromi he still loses");
        // Hiromi is the stronger: the result is unchanged (Tametaka loses anyway).
        var (g, tametaka3, hiromi3) = Duel(Tametaka, Hiromi, mineStrong: false, theirsStrong: true);
        Check(Destroyed(g, tametaka3) && !Destroyed(g, hiromi3), "Hiromi stronger: Tametaka loses as usual");
        Check(!g.State.Events.Any(x => x.Type == "duelAutoLoss"), "and nothing is overridden");
        // Anyone else: Tametaka wins the duel he should win.
        var (h, tametaka4, other) = Duel(Tametaka, Lion, mineStrong: true, theirsStrong: false);
        Check(!Destroyed(h, tametaka4) && Destroyed(h, other), "against another Personality he wins as before");
        Check(!h.State.Events.Any(x => x.Type == "duelAutoLoss"), "no override against anyone else");
        // Hiromi against anyone else is not changed either (the text is Tametaka's).
        var (i, hiromi4, lion) = Duel(Hiromi, Lion, mineStrong: true, theirsStrong: false);
        Check(!Destroyed(i, hiromi4) && Destroyed(i, lion), "Hiromi alone is unaffected");
    }

    private static void TametakaZeroesForce()
    {
        var s = Table(Phase.Battle, 1);
        var tametaka = Add(s, Tametaka, 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, Follower, 0, Zone.Attached, province: 0, parent: enemy.Id);
        var ally = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(e.CardForceForTest(enemy.Id) > 0 && e.CardForceForTest(enemyFollower.Id) > 0, "the enemy cards have Force");
        var options = Batch336Support.Options(e, 1, WlPersGates1Abilities.TametakaZero, tametaka.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToHashSet();
        Check(options.SetEquals([enemy.Id, enemyFollower.Id]), "an enemy Personality or Follower: " + string.Join(",", options));
        RejectedWithoutChange(e, Use(e, 1, WlPersGates1Abilities.TametakaZero, tametaka, ally.Id));
        Accept(e, Offer(e, 1, WlPersGates1Abilities.TametakaZero, tametaka.Id, enemy.Id));
        Eq(e.CardForceForTest(enemy.Id), 0, "his Force is reduced to 0");
        Check(e.CardForceForTest(enemyFollower.Id) > 0 && e.CardForceForTest(ally.Id) > 0, "nothing else changed");
    }

    // ---- Daidoji Tobei -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TobeiEntryAndAttack()
    {
        var (e, id, hand, honor) = Buy(Tobei, s => SetDeck(s, 0, Focus, Focus));
        Eq(e.State.Cards[id].Zone, Zone.Home, "he entered play");
        Eq(Honor(e, 0), honor - 1, "After Tobei enters play, lose 1 Honor");
        Eq(e.State.Players[0].Hand.Count, hand + 1, "Destined: a card drawn");

        // Ranged 2 Attack: it destroys a 2F Follower in an enemy unit, and Tobei bows to make it.
        var s = Table(Phase.Battle, 1);
        var tobei = Add(s, Tobei, 1, Zone.Battlefield, province: 0);
        var host = Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var small = Add(s, Follower, 0, Zone.Attached, province: 0, parent: host.Id); // 2F
        var f = MatchEngine.FromTestState(s);
        Eq(f.AttackStrengthForTest(WlPersGates1Abilities.TobeiAttack, tobei.Id), 2, "Ranged 2");
        Accept(f, Offer(f, 1, WlPersGates1Abilities.TobeiAttack, tobei.Id, small.Id));
        PassWindow(f);
        Check(f.State.Cards[tobei.Id].Bowed, "Tobei bowed as the cost");
        Check(f.State.Cards[small.Id].Zone == Zone.FateDiscard, "the 2F Follower was destroyed by a strength-2 Attack");
        Check(!CanUse(f, 1, WlPersGates1Abilities.TobeiAttack, tobei.Id), "a bowed Tobei may not attack again");

        // A card with more Force than 2 survives (Attacks destroy cards whose Force is at most their strength).
        var t = Table(Phase.Battle, 1);
        var tobei2 = Add(t, Tobei, 1, Zone.Battlefield, province: 0);
        var strong = Add(t, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(t);
        Accept(g, Offer(g, 1, WlPersGates1Abilities.TobeiAttack, tobei2.Id, strong.Id)); PassWindow(g);
        Check(g.State.Cards[strong.Id].Zone == Zone.Battlefield, "a 6F Personality survives a strength-2 Attack");
        Check(g.State.Cards[tobei2.Id].Bowed, "and the bow was paid anyway");

        // Out of battle, or bowed, or the other player: refused.
        var u = Table(Phase.Action, 1);
        var tobei3 = Add(u, Tobei, 1, Zone.Home);
        var h = MatchEngine.FromTestState(u);
        Check(!CanUse(h, 1, WlPersGates1Abilities.TobeiAttack, tobei3.Id), "a Battle action is not offered in the Action Phase");
    }

    // ---- Doji Razan, Hida Gojiro, Isawa Hajime, Kitsune Yuko: Invest -----------------------------------------------------------------------------------------------------------------

    private static ActionOption InvestOption(MatchEngine e, string ability)
    {
        var option = e.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.Command!.AbilityId == ability);
        Check(option is not null, ability + " is offered in the entry window");
        return option!;
    }

    private static void RazanInvestAndDestroy()
    {
        var (e, id, _, _) = Buy(Razan);
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entry window is open for the Invest");
        var option = InvestOption(e, WlPersGates1Abilities.RazanInvest);
        Check(option.Enabled, option.Reason ?? ""); Eq(option.Gold!.Cost, 1, "Invest costs 1 Gold"); Eq(option.Gold.Kind, GoldCostKind.Fixed);
        Check(option.Command!.SourceIds!.Length > 0, "the engine suggests the Gold sources");
        var chi = e.PersonalityChiForTest(id);
        Accept(e, option.Command!);
        Check(option.Command.SourceIds.All(s => e.State.Cards[s].Bowed), "the Gold cost was paid");
        Eq(e.PersonalityChiForTest(id), chi + 1, "a +1C token");
        Eq(e.State.Cards[id].Tokens.Count(t => t.Type == "Invest" && t.Stat == "Chi" && t.Amount == 1), 1, "one +1C Invest token");

        // Declined: no token and no Gold spent.
        var (f, id2, _, _) = Buy(Razan);
        var chi2 = f.PersonalityChiForTest(id2);
        Accept(f, new GameCommand("declineReaction", f.State.PriorityPlayerId, f.Version));
        Eq(f.PersonalityChiForTest(id2), chi2, "declining Invest gives nothing");
        Check(f.State.Cards[id2].Tokens.Count == 0, "no token");

        // Limited: destroy a target Fudo card (either player's), then Razan.
        var s = Table(Phase.Action, 0);
        var razan = Add(s, Razan, 0, Zone.Home);
        var fudoOwn = Add(s, FudoPersonality, 0, Zone.Home); var fudoEnemy = Add(s, FudoPersonality, 1, Zone.Home); var plain = Add(s, Lion, 1, Zone.Home);
        var g = MatchEngine.FromTestState(s);
        var targets = Batch336Support.Options(g, 0, WlPersGates1Abilities.RazanDestroy, razan.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToHashSet();
        Check(targets.SetEquals([fudoOwn.Id, fudoEnemy.Id]), "any Fudo card: " + string.Join(",", targets));
        RejectedWithoutChange(g, Use(g, 0, WlPersGates1Abilities.RazanDestroy, razan, plain.Id));
        Accept(g, Offer(g, 0, WlPersGates1Abilities.RazanDestroy, razan.Id, fudoEnemy.Id));
        Check(Destroyed(g, g.State.Cards[fudoEnemy.Id]), "the Fudo card was destroyed");
        Check(Destroyed(g, g.State.Cards[razan.Id]), "and so was Razan");
        Check(g.State.Cards[fudoOwn.Id].Zone == Zone.Home && g.State.Cards[plain.Id].Zone == Zone.Home, "nothing else was");

        // His own Fudo card, equally.
        var s2 = Table(Phase.Action, 0);
        var razan2 = Add(s2, Razan, 0, Zone.Home); var mine = Add(s2, FudoPersonality, 0, Zone.Home);
        var h = MatchEngine.FromTestState(s2);
        Accept(h, Offer(h, 0, WlPersGates1Abilities.RazanDestroy, razan2.Id, mine.Id));
        Check(Destroyed(h, h.State.Cards[mine.Id]) && Destroyed(h, h.State.Cards[razan2.Id]), "his own Fudo card and Razan are destroyed");
    }

    private static void GojiroAndHajimeInvest()
    {
        var (e, gojiro, _, honor) = Buy(Gojiro);
        Eq(Honor(e, 0), honor - 1, "After Gojiro enters play, lose 1 Honor");
        var option = InvestOption(e, WlPersGates1Abilities.GojiroInvest);
        Eq(option.Gold!.Cost, 4, "Invest 4 Gold");
        var force = e.CardForceForTest(gojiro);
        Accept(e, option.Command!);
        Eq(e.CardForceForTest(gojiro), force + 2, "two +1F tokens");
        Eq(e.State.Cards[gojiro].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 2, "two tokens");
        Eq(Honor(e, 0), honor - 1, "Invest costs Gold, not Honor");

        var (f, hajime, _, honor2) = Buy(Hajime);
        Eq(Honor(f, 0), honor2 - 1, "After Hajime enters play, lose 1 Honor");
        var option2 = InvestOption(f, WlPersGates1Abilities.HajimeInvest);
        Eq(option2.Gold!.Cost, 3, "Invest 3 Gold");
        var force2 = f.CardForceForTest(hajime);
        Accept(f, option2.Command!);
        Eq(f.CardForceForTest(hajime), force2 + 2, "two +1F tokens");
        Eq(f.State.Cards[hajime].Tokens.Count(t => t.Type == "Fire" && t.Stat == "Force" && t.Amount == 1), 2, "two Fire tokens");

        // Declined, the loss still stands and nothing is given.
        var (g, hajime2, _, honor3) = Buy(Hajime);
        Accept(g, new GameCommand("declineReaction", g.State.PriorityPlayerId, g.Version));
        Eq(Honor(g, 0), honor3 - 1, "the entering loss is not optional");
        Check(g.State.Cards[hajime2].Tokens.Count == 0, "no tokens when declined");

        // An Invest the player cannot pay for is disabled and changes nothing.
        var (h, gojiro2, _, _) = Buy(Gojiro);
        var poor = h.State; foreach (var c in poor.Cards.Values.Where(c => c.OwnerId == 0 && !c.Bowed && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        var broke = MatchEngine.FromTestState(poor);
        var disabled = broke.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.Label.StartsWith("Invest 4 Gold"));
        Check(disabled is { Enabled: false }, "an unaffordable Invest is disabled");
        Check(broke.State.Cards[gojiro2].Tokens.Count == 0, "and nothing was given");
    }

    // ---- Hoshoku-sha --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HoshokuDestroy()
    {
        // Battle half: a Personality of his controller's is destroyed and Hoshoku-sha gets +2F; an enemy Personality is not a target.
        var s = Table(Phase.Battle, 1);
        var hoshoku = Add(s, Hoshoku, 1, Zone.Battlefield, province: 0);
        var own = Add(s, Lion, 1, Zone.Battlefield, province: 0); var enemy = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var force = e.CardForceForTest(hoshoku.Id);
        var targets = Batch336Support.Options(e, 1, WlPersGates1Abilities.HoshokuDestroy, hoshoku.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToHashSet();
        Check(targets.SetEquals([hoshoku.Id, own.Id]), "your Personality: himself or his ally, never an enemy: " + string.Join(",", targets));
        RejectedWithoutChange(e, Use(e, 1, WlPersGates1Abilities.HoshokuDestroy, hoshoku, enemy.Id));
        Accept(e, Offer(e, 1, WlPersGates1Abilities.HoshokuDestroy, hoshoku.Id, own.Id));
        Check(Destroyed(e, e.State.Cards[own.Id]), "the target was destroyed");
        Eq(e.CardForceForTest(hoshoku.Id), force + 2, "Hoshoku-sha has +2F");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield, "the enemy is untouched");

        // Open half: the same in the Action Phase.
        var t = Table(Phase.Action, 0);
        var hoshoku2 = Add(t, Hoshoku, 0, Zone.Home); var own2 = Add(t, Crane, 0, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        var force2 = f.CardForceForTest(hoshoku2.Id);
        Accept(f, Offer(f, 0, WlPersGates1Abilities.HoshokuDestroy, hoshoku2.Id, own2.Id));
        Check(Destroyed(f, f.State.Cards[own2.Id]), "the Open action destroys the target too");
        Eq(f.CardForceForTest(hoshoku2.Id), force2 + 2, "and gives +2F");

        // He may target himself: he is destroyed and there is nobody to give the Force to.
        var u = Table(Phase.Action, 0);
        var lone = Add(u, Hoshoku, 0, Zone.Home);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Offer(g, 0, WlPersGates1Abilities.HoshokuDestroy, lone.Id, lone.Id));
        Check(Destroyed(g, g.State.Cards[lone.Id]), "Hoshoku-sha destroyed himself");
        Check(g.State.PendingChoice is null, "the action finished");
    }

    // ---- Ide Hinobu -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HinobuProduces()
    {
        var s = Table(Phase.Action, 0);
        var hinobu = Add(s, Hinobu, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(hinobu.Id, 0), 3, "Bow this card: Produce 3 Gold");
        Check(e.PaymentOptions(0, 3).Any(o => o.SourceIds.Contains(hinobu.Id)) || e.PaymentOptions(0, 5).Any(o => o.SourceIds.Contains(hinobu.Id)), "she is among the payment sources");
        var t = Table(Phase.Action, 0);
        var bowed = Add(t, Hinobu, 0, Zone.Home, bowed: true);
        var f = MatchEngine.FromTestState(t);
        Check(f.PaymentOptions(0, 3).All(o => !o.SourceIds.Contains(bowed.Id)), "a bowed Hinobu produces nothing");
    }

    // ---- Kitsune Yuko ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void YukoHonor()
    {
        var (e, id, _, honor) = Buy(Yuko);
        Eq(Honor(e, 0), honor, "no entering effect");
        var option = InvestOption(e, WlPersGates1Abilities.YukoInvest);
        Eq(option.Gold!.Cost, 2, "Invest 2 Gold");
        Accept(e, option.Command!);
        Eq(Honor(e, 0), honor + 1, "Invest: gain 1 Honor");

        var s = Table(Phase.Action, 0);
        var yuko = Add(s, Yuko, 0, Zone.Home);
        var f = MatchEngine.FromTestState(s);
        var before = Honor(f, 0);
        Accept(f, Enabled(f, 0, WlPersGates1Abilities.YukoHonor, yuko.Id).Command!);
        Eq(Honor(f, 0), before + 1, "Limited, bow: gain 1 Honor");
        Check(f.State.Cards[yuko.Id].Bowed, "she bowed as the cost");
        Check(!CanUse(f, 0, WlPersGates1Abilities.YukoHonor, yuko.Id), "once");
        var t = Table(Phase.Action, 0);
        var bowed = Add(t, Yuko, 0, Zone.Home, bowed: true);
        var g = MatchEngine.FromTestState(t);
        RejectedWithoutChange(g, Use(g, 0, WlPersGates1Abilities.YukoHonor, bowed));
        Check(id.Length > 0, "bought");
    }

    // ---- Legulus ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LegulusAll()
    {
        var (e, id, _, honor) = Buy(Legulus);
        Eq(e.State.Cards[id].Zone, Zone.Home, "he entered play");
        Eq(Honor(e, 0), honor - 2, "After Legulus enters play, lose 2 Honor");

        // Limited: a Yodotai Legionnaire Follower from the Fate deck to the hand; nothing else qualifies.
        var s = Table(Phase.Action, 0);
        var legulus = Add(s, Legulus, 0, Zone.Home);
        SetDeck(s, 0, Focus, Legionnaire, Follower);
        var f = MatchEngine.FromTestState(s);
        Accept(f, Enabled(f, 0, WlPersGates1Abilities.LegulusSearch, legulus.Id).Command!);
        Eq(f.State.PendingChoice?.Type, "searchSelect", "the search opened");
        Eq(string.Join(",", Offered(f, 0)), Legionnaire, "only a Yodotai Legionnaire qualifies");
        Check(!f.Execute(new GameCommand("passSearch", 0, f.Version)).Accepted, "he must take it");
        Accept(f, new GameCommand("selectSearchCard", 0, f.Version, Choice: Legionnaire));
        Check(f.State.Cards.Values.Single(c => c.DefinitionId == Legionnaire).Zone == Zone.Hand, "the Legionnaire is in his hand");
        Check(f.State.Events.Any(x => x.Type == "deckShuffled"), "the searched deck is reshuffled");
        Check(!f.State.Cards[legulus.Id].Bowed, "Limited without a bow: Legulus stays straight");
        Check(!CanUse(f, 0, WlPersGates1Abilities.LegulusSearch, legulus.Id), "once per turn");
        // No Legionnaire in the deck: pass.
        var t = Table(Phase.Action, 0);
        var legulus2 = Add(t, Legulus, 0, Zone.Home); SetDeck(t, 0, Focus, Follower);
        var g = MatchEngine.FromTestState(t);
        Accept(g, Enabled(g, 0, WlPersGates1Abilities.LegulusSearch, legulus2.Id).Command!);
        Accept(g, new GameCommand("passSearch", 0, g.Version));
        Check(g.State.PendingSearch is null && g.State.PendingChoice is null, "nothing qualified: the search ended");

        // -3 strength: Tobei's strength-2 Attack on a 2F Follower of his unit is weaker still (2 - 3 < 2) and survives; without Legulus the same Attack destroys it.
        foreach (var protectedUnit in new[] { true, false })
        {
            var b = Table(Phase.Battle, 1);
            var tobei = Add(b, Tobei, 1, Zone.Battlefield, province: 0);
            var host = Add(b, protectedUnit ? Legulus : "training-ancestor", 0, Zone.Battlefield, province: 0);
            var small = Add(b, Follower, 0, Zone.Attached, province: 0, parent: host.Id);
            var h = MatchEngine.FromTestState(b);
            Accept(h, Offer(h, 1, WlPersGates1Abilities.TobeiAttack, tobei.Id, small.Id));
            PassWindow(h);
            Eq(h.State.Cards[small.Id].Zone, protectedUnit ? Zone.Attached : Zone.FateDiscard, protectedUnit ? "the Follower in Legulus's unit survives the weakened Attack" : "the same Attack destroys a Follower in another unit");
        }
    }

    // ---- M'rika - exp ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MrikaBows()
    {
        var s = Table(Phase.Action, 0);
        var mrika = Add(s, Mrika, 0, Zone.Home);
        var enemy = Add(s, Lion, 1, Zone.Home); var own = Add(s, Crane, 0, Zone.Home); var follower = Add(s, Follower, 1, Zone.Attached, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var targets = Batch336Support.Options(e, 0, WlPersGates1Abilities.MrikaBow, mrika.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(enemy.Id) && targets.Contains(own.Id) && !targets.Contains(follower.Id), "a target Personality, either player's, and nothing else: " + string.Join(",", targets));
        RejectedWithoutChange(e, Use(e, 0, WlPersGates1Abilities.MrikaBow, mrika, follower.Id));
        Check(!CanUse(e, 1, WlPersGates1Abilities.MrikaBow, mrika.Id), "the other player is not offered it");
        Accept(e, Offer(e, 0, WlPersGates1Abilities.MrikaBow, mrika.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the target is bowed");
        Check(e.State.Cards[mrika.Id].Bowed, "M'rika bowed as the cost");
        Check(!e.State.Cards[own.Id].Bowed, "nothing else bowed");
        Check(!CanUse(e, 0, WlPersGates1Abilities.MrikaBow, mrika.Id), "a bowed M'rika may not use it again");
        var t = Table(Phase.Action, 0);
        var bowed = Add(t, Mrika, 0, Zone.Home, bowed: true); var target = Add(t, Lion, 1, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        RejectedWithoutChange(f, Use(f, 0, WlPersGates1Abilities.MrikaBow, bowed, target.Id));
    }
}
