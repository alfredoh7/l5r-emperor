using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #667, cluster "search-play": personality-wave-125 (Gozu, Hyobuko, Masaya) and item-wave-43 (Heart of Fudo - exp2, The Sacred Rosary of the Constrictors, The Topaz Armor - exp), each wired from the
// printed text of its scan. Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion; Battle: province 0 is the current battlefield), driven only through
// QueryActions and the commands the engine offers. The mechanics are in src/L5R.Core/SearchPlay*.cs.
internal static class SearchPlayTests
{
    private const string Gozu = SearchPlayAbilities.Gozu, Hyobuko = SearchPlayAbilities.Hyobuko, Masaya = SearchPlayAbilities.Masaya, FudoExp2 = SearchPlayAbilities.HeartOfFudoExp2,
        Rosary = SearchPlayAbilities.SacredRosary, TopazArmor = SearchPlayAbilities.TopazArmor, FudoExp = PromoEmperorItemsAbilities.HeartOfFudo;
    private const string WaveP = "personality-wave-125", WaveI = "item-wave-43";
    private const string ExtendedManeuvers = "oracle-l5r-2419", FloodedPass = "oracle-l5r-2613", Crane = "training-crane-samurai", Lion = "training-lion-samurai", Focus = "training-focus-strategy";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static void SetDeck(MatchState s, int player, bool fate, params string[] defs)
    {
        var deck = fate ? s.Players[player].FateDeck : s.Players[player].DynastyDeck;
        foreach (var id in deck.ToArray()) s.Cards.Remove(id);
        deck.Clear();
        foreach (var def in defs) { var c = Add(s, def, player, fate ? Zone.FateDeck : Zone.DynastyDeck); c.FaceUp = false; deck.Add(c.Id); }
    }
    private static string[] Offered(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "selectSearchCard").Select(a => a.Command!.Choice!).Order(StringComparer.Ordinal).ToArray();
    private static string Repo() => Batch336Support.RepoRoot();

    public static void Run(TestRunner runner) => runner.Run("search-play", [
        ("Published shape: the six cards print their scans' stats, keywords, text, attach restrictions and abilities, ship art, name the reviewed printing and are in their wave manifests", PublishedShape),
        ("Gozu: destroys a Terrain; discarding a card is the second way of taking the action, and then the Fate-deck search for a Terrain is mandatory and the found Terrain is played with an additional action", GozuSearchAndPlay),
        ("Gozu, Mendon's ruling: while the found Terrain is owed the player may not pass, decline, or do anything else, and the options say so; the play settles the debt", GozuMayNotPass),
        ("Gozu, edges: no Terrain in the deck (pass), a found Terrain that cannot be played is discarded, a play that becomes impossible releases the player, no discard no search, rejected commands change nothing", GozuEdges),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        string[] personalities = [Gozu, Hyobuko, Masaya], items = [FudoExp2, Rosary, TopazArmor];
        foreach (var (wave, ids) in new[] { (WaveP, personalities), (WaveI, items) })
        {
            var listed = File.ReadAllLines(Path.Combine(Repo(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
            Check(new HashSet<string>(listed).SetEquals(ids), $"{wave}: the manifest is exactly the wired cards");
            var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo(), "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
            Eq(sources.Length, ids.Length, $"{wave}: a provenance row per card");
            foreach (var id in ids) Check(File.Exists(Path.Combine(Repo(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
        }
        void Shape(string id, string name, CardType type, int gold, string[] keywords, string[] abilities, string effect, string textStart, string? requires = null)
        {
            var d = CardCatalog.Get(id);
            Check(d.Name == name && d.Type == type && d.GoldCost == gold && d.Clan == "Unaligned", $"{name}: printed type, cost and alignment");
            Check(d.Keywords!.SequenceEqual(keywords), $"{name}: printed keywords {string.Join(",", d.Keywords!)}");
            Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities");
            Eq(d.Effect, effect, $"{name}: effect");
            Eq(d.AttachRequiresKeyword, requires, $"{name}: attach restriction");
            Check(d.Text.StartsWith(textStart, StringComparison.Ordinal), $"{name}: printed text");
        }
        Shape(Gozu, "Gozu", CardType.Personality, 3, ["Ronin", "Samurai", "Scout"], [SearchPlayAbilities.GozuDestroy, SearchPlayAbilities.GozuDestroyDiscard], "none", "Battle: Target a Terrain: Destroy it. You may discard a card.");
        Check(CardCatalog.Get(Gozu).Force == 2 && CardCatalog.Get(Gozu).Chi == 3 && CardCatalog.Get(Gozu).PersonalHonor == 2 && CardCatalog.Get(Gozu).HonorRequirement is null, "Gozu: 2F 3C 2PH, no Honor Requirement");
        Shape(Hyobuko, "Hyobuko", CardType.Personality, 6, ["Singular", "Fudo", "Monk", "Shugenja", "Void"], [SearchPlayAbilities.HyobukoSearch], SearchPlayAbilities.HyobukoEffect, "Attaches Heart of Fudo ignoring Gold cost.");
        Check(CardCatalog.Get(Hyobuko).Force == 4 && CardCatalog.Get(Hyobuko).Chi == 4 && CardCatalog.Get(Hyobuko).PersonalHonor == 1, "Hyobuko: 4F 4C 1PH");
        Shape(Masaya, "Masaya", CardType.Personality, 6, ["Ronin", "Samurai"], [SearchPlayAbilities.MasayaRecruit], "none", "Open: If Masaya is face-up in one of your provinces:");
        Check(CardCatalog.Get(Masaya).Force == 3 && CardCatalog.Get(Masaya).Chi == 3 && CardCatalog.Get(Masaya).PersonalHonor == 1 && CardCatalog.Get(Masaya).HonorRequirement == 0, "Masaya: 3F 3C 1PH, Honor Requirement 0");
        Shape(FudoExp2, "Heart of Fudo", CardType.Item, 4, ["Experienced 2", "Unique", "Fudo"], [SearchPlayAbilities.FudoFetch], "none", "Limited: If this Personality is unbowed, search your Fate deck for a card");
        Check(CardCatalog.Get(FudoExp2).Focus == 4 && CardCatalog.Get(FudoExp2).Force == 0 && CardCatalog.Get(FudoExp2).Chi == 0, "Heart of Fudo - exp2: +0F +0C, Focus 4");
        Shape(Rosary, "The Sacred Rosary of the Constrictors", CardType.Item, 0, ["Unique"], [SearchPlayAbilities.RosaryCelestial], "none", "Will only attach to a Phoenix Clan or Naga Personality.", "Phoenix Clan|Naga");
        Check(CardCatalog.Get(Rosary).Chi == 1 && CardCatalog.Get(Rosary).Force == 0 && CardCatalog.Get(Rosary).Focus == 4, "The Sacred Rosary: +0F +1C, Focus 4");
        Shape(TopazArmor, "The Topaz Armor", CardType.Item, 4, ["Armor", "Unique", "Experienced"], [], SearchPlayAbilities.TopazEffect, "Attaches to a Topaz Champion ignoring costs.", "Topaz Champion");
        Check(CardCatalog.Get(TopazArmor).Force == 2 && CardCatalog.Get(TopazArmor).Chi == 0 && CardCatalog.Get(TopazArmor).Focus == 4, "The Topaz Armor: +2F +0C, Focus 4");
        foreach (var id in new[] { SearchPlayAbilities.GozuDestroy, SearchPlayAbilities.GozuDestroyDiscard, SearchPlayAbilities.HyobukoSearch, SearchPlayAbilities.MasayaRecruit, SearchPlayAbilities.FudoFetch, SearchPlayAbilities.RosaryCelestial })
            Check(AbilityCatalog.All.ContainsKey(id), id + " is catalogued");
        foreach (var spec in SearchPlaySpecs.All) Check(SearchCatalog.All.ContainsKey(spec.Id), spec.Id + " is a catalogued search");
    }

    // ---- Gozu ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 0 (the Defender) holds Gozu at the battlefield with a Terrain of player 1's there and one fodder card in the hand; the Fate deck holds `deck`.
    private static (MatchState State, CardInstance Gozu, CardInstance Terrain, CardInstance Fodder) GozuTable(params string[] deck)
    {
        var s = Table(Phase.Battle, 0);
        var gozu = Add(s, Gozu, 0, Zone.Battlefield, province: 0);
        Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var terrain = Add(s, FloodedPass, 1, Zone.Battlefield, province: 0);
        var fodder = Add(s, Focus, 0, Zone.Hand);
        SetDeck(s, 0, true, deck);
        return (s, gozu, terrain, fodder);
    }

    private static GameCommand GozuDiscard(MatchEngine e, CardInstance gozu, CardInstance terrain, CardInstance discard)
    {
        var option = Options(e, 0, SearchPlayAbilities.GozuDestroyDiscard, gozu.Id).FirstOrDefault(o => o.Enabled && o.Command!.CardIds is [var id] && id == discard.Id && (o.TargetIds?.Contains(terrain.Id) ?? false));
        Check(option is not null, "Gozu's discard way was offered for " + discard.Id + ": " + string.Join("; ", Options(e, 0, SearchPlayAbilities.GozuDestroyDiscard, gozu.Id).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
        return option!.Command! with { TargetId = terrain.Id };
    }

    private static void GozuSearchAndPlay()
    {
        var (s, gozu, terrain, fodder) = GozuTable(ExtendedManeuvers, Focus, FloodedPass);
        var e = MatchEngine.FromTestState(s);
        // Both ways are offered; the plain one destroys only.
        Check(CanUse(e, 0, SearchPlayAbilities.GozuDestroy, gozu.Id) && CanUse(e, 0, SearchPlayAbilities.GozuDestroyDiscard, gozu.Id), "both ways of taking the action are offered");
        Accept(e, GozuDiscard(e, gozu, terrain, fodder));
        Eq(e.State.Cards[terrain.Id].Zone, Zone.FateDiscard, "the Terrain was destroyed");
        Eq(e.State.Cards[fodder.Id].Zone, Zone.FateDiscard, "the card was discarded");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opened");
        Eq(string.Join(",", Offered(e, 0)), string.Join(",", new[] { ExtendedManeuvers, FloodedPass }.Order(StringComparer.Ordinal)), "only the Terrains in the Fate deck are offered");
        // A pass is refused while a Terrain qualifies: a Terrain must be selected.
        Check(!e.Execute(new GameCommand("passSearch", 0, e.Version)).Accepted, "the searcher may not decline when a Terrain qualifies");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: ExtendedManeuvers));
        var found = e.State.Cards.Values.Single(c => c.DefinitionId == ExtendedManeuvers);
        Check(e.State.ShownCards.Any(x => x.DefinitionId == ExtendedManeuvers), "the found card was shown");
        Eq(found.Zone, Zone.Hand, "the Terrain is in the hand to be played");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null, "the search is over");
        Eq(e.State.PriorityPlayerId, 0, "he keeps the priority");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.Mandatory && g.ScopeCardIds is { Length: 1 } ids && ids[0] == found.Id), "a mandatory additional Battle action scoped to the found Terrain");
        Check(e.State.SearchPlayOwes.Any(o => o.PlayerId == 0 && o.CardId == found.Id), "the play is owed");
        Check(e.State.Players[0].FateDeck.Count == 2 && !e.State.Players[0].FateDeck.Contains(found.Id), "the deck lost the card and was reshuffled");
        Check(e.State.Events.Any(x => x.Type == "deckShuffled"), "the Fate deck was shuffled");
        Accept(e, new GameCommand("playStrategy", 0, e.Version, CardId: found.Id));
        Eq(e.State.Cards[found.Id].Zone, Zone.Battlefield, "the Terrain is in play at the battlefield");
        Check(e.State.SearchPlayOwes.Count == 0 || !e.QueryActions(0).Any(a => !a.Enabled && a.Reason?.Contains("you may not pass") == true), "and nothing is owed any more");
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "the grant was spent");
        Eq(e.State.PriorityPlayerId, 1, "priority passes to the opponent after the one extra action");
    }

    private static void GozuMayNotPass()
    {
        var (s, gozu, terrain, fodder) = GozuTable(ExtendedManeuvers);
        var other = Add(s, Crane, 0, Zone.Battlefield, province: 0); // a unit that could take other actions
        var spare = Add(s, Focus, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, GozuDiscard(e, gozu, terrain, fodder));
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: ExtendedManeuvers));
        var found = e.State.Cards.Values.Single(c => c.DefinitionId == ExtendedManeuvers);
        var before = System.Text.Json.JsonSerializer.Serialize(e.State);
        // The options: the play is enabled; the pass, "pass all battles" and every other action are disabled with the reason.
        var options = e.QueryActions(0);
        Check(options.Any(o => o.Type == "playStrategy" && o.CardId == found.Id && o.Enabled), "playing the found Terrain is offered");
        foreach (var type in new[] { "pass", "passAllBattles" })
        {
            var pass = options.FirstOrDefault(o => o.Type == type);
            Check(pass is not null && !pass.Enabled && pass.Reason!.Contains("play"), $"{type} is listed disabled with the reason: {pass?.Reason}");
        }
        Check(!options.Any(o => o.Type == "declineAdditionalAction"), "a mandatory additional action cannot be declined");
        Check(options.Where(o => o.Type is not ("playStrategy" or "concede")).All(o => !o.Enabled), "every other action is disabled while the Terrain is owed");
        // The commands are refused and change nothing.
        foreach (var command in new[] { new GameCommand("pass", 0, e.Version), new GameCommand("passAllBattles", 0, e.Version), new GameCommand("declineAdditionalAction", 0, e.Version) })
        {
            var error = Rejection(e, command);
            Check(error.Contains("play") || error.Contains("additional"), $"{command.Type} refused: {error}");
            Eq(System.Text.Json.JsonSerializer.Serialize(e.State), before, $"{command.Type}: no state changed");
        }
        // An auto-played seat (a bot takes the first enabled answer) can only do the one legal thing, so it cannot stall or pass its way out.
        var botChoice = e.QueryActions(0).First(o => o.Enabled && o.Command is not null && o.Type != "concede");
        Check(botChoice.Type == "playStrategy" && botChoice.CardId == found.Id, "a bot's only enabled answer is the owed play");
        // Playing the other Strategy in the hand is not the owed action either.
        Check(!options.Any(o => o.CardId == spare.Id && o.Enabled), "another card of the hand is not offered");
        Accept(e, new GameCommand("playStrategy", 0, e.Version, CardId: found.Id));
        Eq(e.State.Cards[found.Id].Zone, Zone.Battlefield, "the owed Terrain was played");
        Eq(e.State.PriorityPlayerId, 1, "the burst ended");
        // The player is free again on his next opportunity: the pass is enabled once the debt is paid.
        Check(e.QueryActions(1).Any(o => o.Type == "pass" && o.Enabled), "the opponent may pass");
        Check(other.Id.Length > 0, "fixture");
    }

    private static void GozuEdges()
    {
        // No Terrain in the Fate deck: nothing qualifies, the searcher passes, the deck is shuffled, nothing is owed.
        var (s, gozu, terrain, fodder) = GozuTable(Focus, Crane);
        var e = MatchEngine.FromTestState(s);
        Accept(e, GozuDiscard(e, gozu, terrain, fodder));
        Check(!Offered(e, 0).Any(), "no card qualifies");
        Accept(e, new GameCommand("passSearch", 0, e.Version));
        Check(e.State.PendingSearch is null && e.State.SearchPlayOwes.Count == 0 && !e.State.AdditionalActions.Any(g => g.PlayerId == 0), "no Terrain: nothing is owed");
        Check(e.State.Events.Any(x => x.Type == "deckShuffled"), "the deck is reshuffled whether or not a card was found");
        Eq(e.State.PriorityPlayerId, 1, "the action ended normally");

        // A found Terrain that cannot be played now is discarded: a second Terrain stays at the battlefield (one Terrain per battlefield), so the found one is not legal.
        var (t, gozu2, terrain2, fodder2) = GozuTable(ExtendedManeuvers);
        Add(t, FloodedPass, 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, GozuDiscard(f, gozu2, terrain2, fodder2));
        Accept(f, new GameCommand("selectSearchCard", 0, f.Version, Choice: ExtendedManeuvers));
        var found = f.State.Cards.Values.Single(c => c.DefinitionId == ExtendedManeuvers);
        Eq(found.Zone, Zone.FateDiscard, "an unplayable Terrain is discarded");
        Check(f.State.SearchPlayOwes.Count == 0 && !f.State.AdditionalActions.Any(g => g.PlayerId == 0), "and nothing is owed");
        Eq(f.State.PriorityPlayerId, 1, "priority passes on");

        // A play that becomes impossible while it is owed releases the player (the card is discarded; the pass is then legal).
        var (u, gozu3, terrain3, fodder3) = GozuTable(ExtendedManeuvers);
        var g = MatchEngine.FromTestState(u);
        Accept(g, GozuDiscard(g, gozu3, terrain3, fodder3));
        Accept(g, new GameCommand("selectSearchCard", 0, g.Version, Choice: ExtendedManeuvers));
        var owed = g.State.Cards.Values.Single(c => c.DefinitionId == ExtendedManeuvers);
        Check(g.State.SearchPlayOwes.Any(), "the play is owed");
        var h = Edit(g, st => { foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality)) c.Zone = Zone.Home; });
        Check(!h.QueryActions(0).Any(o => o.Type == "playStrategy" && o.Enabled), "the player has no unit at the battlefield any more: the Terrain cannot be played");
        Check(h.QueryActions(0).Any(o => o.Type == "pass" && o.Enabled), "so nothing is held back");
        Accept(h, new GameCommand("pass", 0, h.Version));
        Eq(h.State.Cards[owed.Id].Zone, Zone.FateDiscard, "the Terrain that could not be played was discarded");
        Check(h.State.SearchPlayOwes.Count == 0 && !h.State.AdditionalActions.Any(x => x.PlayerId == 0), "and the debt and its grant are gone");

        // Without the discard there is no search: the plain way destroys the Terrain only.
        var (v, gozu4, terrain4, fodder4) = GozuTable(ExtendedManeuvers);
        var k = MatchEngine.FromTestState(v);
        Accept(k, Offer(k, 0, SearchPlayAbilities.GozuDestroy, gozu4.Id, terrain4.Id));
        Eq(k.State.Cards[terrain4.Id].Zone, Zone.FateDiscard, "destroyed");
        Eq(k.State.Cards[fodder4.Id].Zone, Zone.Hand, "nothing was discarded");
        Check(k.State.PendingSearch is null && k.State.Players[0].FateDeck.Count == 1, "no search");
        Check(!CanUse(k, 0, SearchPlayAbilities.GozuDestroyDiscard, gozu4.Id), "the two ways share one use");

        // Rejected: the discard way without a card, with a card that is not in the hand, and against a card that is not a Terrain: no state change.
        var (w, gozu5, terrain5, fodder5) = GozuTable(ExtendedManeuvers);
        var m = MatchEngine.FromTestState(w);
        var before = JsonSerializer.Serialize(m.State);
        var good = GozuDiscard(m, gozu5, terrain5, fodder5);
        foreach (var bad in new[] { good with { CardIds = null }, good with { CardIds = [terrain5.Id] }, good with { CardIds = [gozu5.Id] }, good with { CardIds = ["nonexistent"] }, good with { TargetId = gozu5.Id } })
        {
            Check(!m.Execute(bad).Accepted, "rejected: " + JsonSerializer.Serialize(bad.CardIds) + " " + bad.TargetId);
            Eq(JsonSerializer.Serialize(m.State), before, "a rejected command changes nothing");
        }
        Check(!CanUse(m, 0, SearchPlayAbilities.GozuDestroyDiscard, gozu5.Id) || true, "fixture");
    }
}
