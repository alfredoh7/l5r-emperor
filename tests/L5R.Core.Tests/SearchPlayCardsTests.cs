using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #667, cluster "search-play" (continued): Heart of Fudo - exp2, Hyobuko, Masaya, The Sacred Rosary of the Constrictors and The Topaz Armor - exp. Gozu and the published shapes are in SearchPlayTests.
internal static class SearchPlayCardsTests
{
    private const string Hyobuko = SearchPlayAbilities.Hyobuko, Masaya = SearchPlayAbilities.Masaya, FudoExp2 = SearchPlayAbilities.HeartOfFudoExp2, Rosary = SearchPlayAbilities.SacredRosary,
        TopazArmor = SearchPlayAbilities.TopazArmor, FudoExp = PromoEmperorItemsAbilities.HeartOfFudo;
    private const string Crane = "training-crane-samurai", Lion = "training-lion-samurai", Focus = "training-focus-strategy", EbisusHonesty = "oracle-l5r-2276", MegumisGuidance = "oracle-l5r-10028",
        RyoshunsGuidance = "oracle-l5r-6425", Weapon = "oracle-l5r-2418";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static void SetDeck(MatchState s, int player, Zone zone, params string[] defs)
    {
        var deck = zone == Zone.FateDeck ? s.Players[player].FateDeck : s.Players[player].DynastyDeck;
        foreach (var id in deck.ToArray()) s.Cards.Remove(id);
        deck.Clear();
        foreach (var def in defs) { var c = Add(s, def, player, zone); c.FaceUp = false; deck.Add(c.Id); }
    }
    private static string[] Offered(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "selectSearchCard").Select(a => a.Command!.Choice!).Order(StringComparer.Ordinal).ToArray();
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static GameCommand Equip(MatchEngine e, int player, CardInstance card, CardInstance host, int gold) =>
        new("equip", player, e.Version, CardId: card.Id, SourceIds: gold == 0 ? [] : e.PaymentOptions(player, gold).First().SourceIds, TargetId: host.Id);
    // A bot's walk through a decision: the first enabled answer, until nothing is pending (proof that every new decision is answerable, so an auto-played seat cannot stall in it).
    private static void FirstEnabledUntilClear(MatchEngine e, int guard = 30)
    {
        for (var n = 0; n < guard && (e.State.PendingChoice is not null || e.State.PendingSearch is not null); n++)
        {
            var seat = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var answer = e.QueryActions(seat).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type != "concede");
            Check(answer is not null, $"no enabled answer for player {seat} in {e.State.PendingChoice?.Type}");
            Accept(e, answer!.Command!);
        }
        Check(e.State.PendingChoice is null && e.State.PendingSearch is null, "the decision chain ended");
    }
    private static int Chosen(MatchEngine e, string cardId) => e.State.Cards[cardId].GrantedKeywords.Count(g => g.Source != "test-fixture");

    public static void Run(TestRunner runner) => runner.Run("search-play-cards", [
        ("Heart of Fudo - exp2: an unbowed wielder searches the Fate deck for any card (into the hand), bows, and the Item is destroyed; a bowed wielder only destroys it; rejected uses change nothing", FudoExp2Scenario),
        ("Hyobuko: Heart of Fudo (either version) attaches to him for no Gold, to no one else for free", HyobukoAttach),
        ("Hyobuko: discarding him is the cost; the search takes a Heart of Fudo from the discard pile first (never showing the deck), then the Fate deck, and passes when neither holds one", HyobukoSearch),
        ("Masaya: an Open action from his face-up province recruits him for 2 less Gold; at the end of the turn he is put on top of his Dynasty deck, unless he left play", MasayaRecruit),
        ("Masaya, edges: not offered face-down, in the Battle Phase, for another player's card or in play; too little Gold is listed disabled; rejected uses change nothing", MasayaEdges),
        ("The Sacred Rosary: discards all your Celestials, searches the Dynasty deck or discard pile for a Celestial and puts it into play with no province; a discard-pile source removes the Rosary from the game", RosaryScenario),
        ("The Sacred Rosary, edges: it attaches only to a Phoenix Clan or Naga Personality, a deck source is shuffled and keeps the Item, nothing found changes nothing but the discards, the opponent's Celestial stays", RosaryEdges),
        ("The Topaz Armor: attaches to a Topaz Champion for no Gold; the entry trait is a mandatory choice of two of eleven keywords, granted to the wielder until the Armor leaves play for any reason", TopazScenario),
        ("The Topaz Armor, edges: a fresh pair on every entry, a printed keyword is never lost with it, a non-Champion is refused, and nothing here can stall an auto-played seat", TopazEdges),
    ]);

    // ---- Heart of Fudo - exp2 -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FudoExp2Scenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Crane, 0, Zone.Home); var fudo = Attach(s, FudoExp2, host);
        SetDeck(s, 0, Zone.FateDeck, Focus, Weapon, Crane);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        var before = Json(e);
        Check(!CanUse(e, 1, SearchPlayAbilities.FudoFetch, fudo.Id), "the opponent may not use it");
        var offered = Enabled(e, 0, SearchPlayAbilities.FudoFetch, fudo.Id);
        Accept(e, offered.Command!);
        Check(e.State.Cards[host.Id].Bowed, "the Personality is bowed");
        Eq(e.State.Cards[fudo.Id].Zone, Zone.FateDiscard, "the Item is destroyed");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opened");
        Eq(Offered(e, 0).Length, 3, "any card of the Fate deck may be taken");
        Check(!e.Execute(new GameCommand("passSearch", 0, e.Version)).Accepted, "a card qualifies: the searcher must take one");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: Weapon));
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "the card is in the hand");
        Check(e.State.Cards.Values.Single(c => c.DefinitionId == Weapon && c.OwnerId == 0).Zone == Zone.Hand, "it is the chosen card");
        Check(e.State.ShownCards.Any(x => x.DefinitionId == Weapon) && e.State.Events.Any(x => x.Type == "deckShuffled"), "shown, and the deck is shuffled");
        Eq(e.State.PriorityPlayerId, 1, "the action ended: priority passes");
        Check(before != Json(e), "fixture");

        // A bowed wielder: the condition fails -- no search -- but the printed bow and destruction still happen.
        var t = Table(Phase.Action, 0);
        var host2 = Add(t, Crane, 0, Zone.Home, bowed: true); var fudo2 = Attach(t, FudoExp2, host2);
        SetDeck(t, 0, Zone.FateDeck, Focus, Weapon);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Enabled(f, 0, SearchPlayAbilities.FudoFetch, fudo2.Id).Command!);
        Check(f.State.PendingSearch is null && f.State.PendingChoice is null, "a bowed Personality: no search");
        Eq(f.State.Cards[fudo2.Id].Zone, Zone.FateDiscard, "the Item is destroyed anyway");
        Eq(f.State.Players[0].FateDeck.Count, 2, "the deck is untouched");

        // An empty Fate deck: nothing to find, the searcher passes.
        var u = Table(Phase.Action, 0);
        var host3 = Add(u, Crane, 0, Zone.Home); var fudo3 = Attach(u, FudoExp2, host3);
        SetDeck(u, 0, Zone.FateDeck);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Enabled(g, 0, SearchPlayAbilities.FudoFetch, fudo3.Id).Command!);
        Accept(g, new GameCommand("passSearch", 0, g.Version));
        Check(g.State.Cards[host3.Id].Bowed && g.State.Cards[fudo3.Id].Zone == Zone.FateDiscard, "bowed and destroyed with nothing found");

        // Rejected uses change nothing: another player's command.
        var v = Table(Phase.Action, 0);
        var host4 = Add(v, Crane, 0, Zone.Home); var fudo4 = Attach(v, FudoExp2, host4);
        var h = MatchEngine.FromTestState(v);
        var snapshot = Json(h);
        Check(!h.Execute(new GameCommand("useAbility", 1, h.Version, CardId: fudo4.Id, AbilityId: SearchPlayAbilities.FudoFetch)).Accepted, "another player's use is rejected");
        Eq(Json(h), snapshot, "and changes nothing");
        // A bot's walk through the decision ends it.
        var w = Table(Phase.Action, 0);
        var host5 = Add(w, Crane, 0, Zone.Home); var fudo5 = Attach(w, FudoExp2, host5);
        SetDeck(w, 0, Zone.FateDeck, Focus, Weapon);
        var k = MatchEngine.FromTestState(w);
        Accept(k, Enabled(k, 0, SearchPlayAbilities.FudoFetch, fudo5.Id).Command!);
        FirstEnabledUntilClear(k);
    }

    // ---- Hyobuko ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HyobukoAttach()
    {
        var s = Table(Phase.Action, 0);
        var hyobuko = Add(s, Hyobuko, 0, Zone.Home); var other = Add(s, Crane, 0, Zone.Home);
        var fudo = Add(s, FudoExp, 0, Zone.Hand); var fudo2 = Add(s, FudoExp2, 0, Zone.Hand); var weapon = Add(s, Weapon, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        // Heart of Fudo costs 2 (exp) / 4 (exp2); to Hyobuko it is free, to anyone else it is paid for.
        Check(!e.Execute(Equip(e, 0, fudo, other, 0)).Accepted, "a Heart of Fudo attached to another Personality is paid for");
        Check(!e.Execute(Equip(e, 0, weapon, hyobuko, 0)).Accepted, "another Item attached to Hyobuko is paid for");
        var gold = e.State.Players[0].Hand.Count;
        Accept(e, Equip(e, 0, fudo, hyobuko, 0));
        Eq(e.State.Cards[fudo.Id].AttachedTo, hyobuko.Id, "the Heart of Fudo attached to Hyobuko for no Gold");
        Check(e.State.Events.Where(x => x.Type == "payment").All(x => x.Message.Contains("paid 0 Gold")), "no Gold was paid");
        Check(gold > 0, "fixture");
        // exp2 too (a second Limited action is not available the same turn, so it is a fresh table).
        var t = Table(Phase.Action, 0);
        var hy2 = Add(t, Hyobuko, 0, Zone.Home); var fudo2b = Add(t, FudoExp2, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Equip(f, 0, fudo2b, hy2, 0));
        Eq(f.State.Cards[fudo2b.Id].AttachedTo, hy2.Id, "Heart of Fudo - exp2 attaches to him for no Gold too");
    }

    private static void HyobukoSearch()
    {
        MatchEngine Build(Action<MatchState, CardInstance> arrange, out CardInstance hy)
        {
            var s = Table(Phase.Action, 0);
            hy = Add(s, Hyobuko, 0, Zone.Home); var attachedWeapon = Attach(s, Weapon, hy);
            SetDeck(s, 0, Zone.FateDeck, Focus, FudoExp2);
            arrange(s, hy);
            return MatchEngine.FromTestState(s);
        }
        // The discard pile first: a Heart of Fudo there is the only candidate, the deck is never searched.
        var e = Build((s, hy) => Add(s, FudoExp, 0, Zone.FateDiscard), out var hyobuko);
        var attachment = e.State.Cards.Values.Single(c => c.AttachedTo == hyobuko.Id);
        var option = Enabled(e, 0, SearchPlayAbilities.HyobukoSearch, hyobuko.Id);
        Accept(e, option.Command!);
        Eq(e.State.Cards[hyobuko.Id].Zone, Zone.DynastyDiscard, "Hyobuko was discarded as the cost (not killed)");
        Eq(e.State.Cards[attachment.Id].Zone, Zone.FateDiscard, "his unit left play with him: the attached Item was discarded");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opened");
        var places = e.QueryActions(0).Where(a => a.Type == "selectSearchCard").Select(a => a.Command!.Choice).ToArray();
        Eq(places.Length, 1, "one qualifying card: the discard pile's Heart of Fudo");
        Check(!e.Execute(new GameCommand("passSearch", 0, e.Version)).Accepted, "he must take it (R-011)");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: FudoExp));
        var inHand = e.State.Cards.Values.Single(c => c.DefinitionId == FudoExp && c.Zone == Zone.Hand);
        Check(inHand.OwnerId == 0, "the Heart of Fudo is in his hand");
        Check(e.State.Players[0].FateDeck.Count == 2 && !e.State.Events.Any(x => x.Type == "deckShuffled"), "the Fate deck was not searched, so it was not shuffled");
        Eq(e.State.PriorityPlayerId, 1, "the action ended");

        // Nothing in the discard pile: the Fate deck is searched (and shuffled); only Heart of Fudo cards qualify.
        var f = Build((s, hy) => { }, out var hyobuko2);
        Accept(f, Enabled(f, 0, SearchPlayAbilities.HyobukoSearch, hyobuko2.Id).Command!);
        Check(Offered(f, 0).Length == 0, "the discard pile holds no Heart of Fudo");
        Accept(f, new GameCommand("passSearch", 0, f.Version));
        Eq(f.State.PendingChoice?.Type, "searchSelect", "then the Fate deck is searched");
        Eq(string.Join(",", Offered(f, 0)), FudoExp2, "only the Heart of Fudo of the deck qualifies (a different title does not)");
        Accept(f, new GameCommand("selectSearchCard", 0, f.Version, Choice: FudoExp2));
        Check(f.State.Cards.Values.Single(c => c.DefinitionId == FudoExp2 && c.OwnerId == 0).Zone == Zone.Hand, "the exp2 came from the deck");
        Check(f.State.Events.Any(x => x.Type == "deckShuffled"), "the searched deck is reshuffled");

        // Neither place holds one: both are passed; the discarding was still the cost.
        var g = Build((s, hy) => SetDeck(s, 0, Zone.FateDeck, Focus), out var hyobuko3);
        Accept(g, Enabled(g, 0, SearchPlayAbilities.HyobukoSearch, hyobuko3.Id).Command!);
        Accept(g, new GameCommand("passSearch", 0, g.Version));
        Accept(g, new GameCommand("passSearch", 0, g.Version));
        Check(g.State.PendingSearch is null && g.State.PendingChoice is null, "the search is over");
        Eq(g.State.Cards[hyobuko3.Id].Zone, Zone.DynastyDiscard, "Hyobuko stays discarded");

        // A bowed Hyobuko, or the opponent's use, is refused with nothing changed.
        var h = Build((s, hy) => hy.Bowed = true, out var hyobuko4);
        var snapshot = Json(h);
        Check(!CanUse(h, 0, SearchPlayAbilities.HyobukoSearch, hyobuko4.Id), "a bowed Hyobuko may not use it");
        Check(!h.Execute(new GameCommand("useAbility", 0, h.Version, CardId: hyobuko4.Id, AbilityId: SearchPlayAbilities.HyobukoSearch)).Accepted, "the command is rejected");
        Eq(Json(h), snapshot, "nothing changed");
        FirstEnabledUntilClear(f);
    }

    // ---- Masaya -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState MasayaTable(Phase phase, out CardInstance masaya, bool faceUp = true)
    {
        var s = Table(phase, 0);
        var province = s.Players[0].Provinces[0];
        if (province.CardId is { } old) s.Cards.Remove(old);
        masaya = Add(s, Masaya, 0, Zone.Province); masaya.FaceUp = faceUp; masaya.ProvinceIndex = 0; province.CardId = masaya.Id;
        return s;
    }

    private static void MasayaRecruit()
    {
        var s = MasayaTable(Phase.Action, out var masaya);
        var e = MatchEngine.FromTestState(s);
        var offered = Enabled(e, 0, SearchPlayAbilities.MasayaRecruit, masaya.Id);
        var quote = e.QuoteAbilityGold(offered.Command!)!;
        Eq(quote.Cost, 4, "his printed 6 Gold less 2");
        Accept(e, offered.Command!);
        Eq(e.State.Cards[masaya.Id].Zone, Zone.Home, "Masaya is in play");
        Check(e.State.Players[0].Provinces[0].CardId != masaya.Id, "his province refilled");
        Check(e.State.SearchPlayReturns.Any(r => r.CardId == masaya.Id && r.PlayerId == 0), "his return is owed before the turn ends");
        // Before the turn ends he is put on top of the Dynasty deck, face down.
        var deckBefore = e.State.Players[0].DynastyDeck.Count;
        var weapon = Add(e.State, Weapon, 0, Zone.Attached); // (a detached copy: the attachment fixture below is built on a state copy)
        e.SearchPlayTurnEndForTest();
        Eq(e.State.Cards[masaya.Id].Zone, Zone.DynastyDeck, "Masaya is back in the Dynasty deck");
        Eq(e.State.Players[0].DynastyDeck[0], masaya.Id, "on top of it");
        Check(!e.State.Cards[masaya.Id].FaceUp, "face down");
        Eq(e.State.Players[0].DynastyDeck.Count, deckBefore + 1, "one more card in the deck");
        Check(weapon.Id.Length > 0, "fixture");

        // The real turn end (nothing but offered commands): he is on top of the deck when the turn ends.
        var t = MasayaTable(Phase.Action, out var masaya2);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Enabled(f, 0, SearchPlayAbilities.MasayaRecruit, masaya2.Id).Command!);
        var turnBefore = f.State.TurnNumber; Check(turnBefore >= 1, "fixture");
        RunUntil(f, () => f.State.Cards[masaya2.Id].Zone is not (Zone.Home or Zone.Battlefield) || f.State.WinnerId is not null);
        Eq(f.State.Cards[masaya2.Id].Zone, Zone.DynastyDeck, "as the turn was about to end he was put on his Dynasty deck");
        Eq(f.State.Players[0].DynastyDeck[0], masaya2.Id, "on top of it");

        // If he left play meanwhile there is nothing to put on top: the owed effect is negated.
        var u = MasayaTable(Phase.Action, out var masaya3);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Enabled(g, 0, SearchPlayAbilities.MasayaRecruit, masaya3.Id).Command!);
        var h = Edit(g, st => st.Cards[masaya3.Id].Zone = Zone.Dead);
        var deckCount = h.State.Players[0].DynastyDeck.Count;
        h.SearchPlayTurnEndForTest();
        Eq(h.State.Cards[masaya3.Id].Zone, Zone.Dead, "a dead Masaya is not put on the deck");
        Eq(h.State.Players[0].DynastyDeck.Count, deckCount, "and the deck is unchanged");
    }

    private static void MasayaEdges()
    {
        var down = MasayaTable(Phase.Action, out var faceDown, faceUp: false);
        var e = MatchEngine.FromTestState(down);
        Check(!Options(e, 0, SearchPlayAbilities.MasayaRecruit, faceDown.Id).Any(o => o.Enabled), "a face-down Masaya cannot be used");
        // The Battle Phase: Open is an Action Phase action (R-030).
        var battle = MasayaTable(Phase.Battle, out var inBattle);
        Add(battle, Lion, 1, Zone.Battlefield, province: 0); Add(battle, Crane, 0, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(battle);
        Check(!Options(b, 0, SearchPlayAbilities.MasayaRecruit, inBattle.Id).Any(o => o.Enabled), "an Open action is not taken in the Battle Phase");
        // Another player's Masaya: refused with nothing changed.
        var s = MasayaTable(Phase.Action, out var masaya);
        var f = MatchEngine.FromTestState(s);
        var good = Enabled(f, 0, SearchPlayAbilities.MasayaRecruit, masaya.Id).Command!;
        var snapshot = Json(f);
        Check(!f.Execute(good with { PlayerId = 1 }).Accepted, "the opponent may not recruit him");
        Check(!f.Execute(good with { TargetId = null }).Accepted, "no target");
        Eq(Json(f), snapshot, "nothing changed");
        // Too little Gold: all production is bowed, so the action is listed disabled with its reason.
        var poor = MasayaTable(Phase.Action, out var masaya2);
        foreach (var c in poor.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        var g = MatchEngine.FromTestState(poor);
        Check(!Options(g, 0, SearchPlayAbilities.MasayaRecruit, masaya2.Id).Any(o => o.Enabled), "without the Gold the action is not enabled");
        // In play he has no province action.
        var played = MasayaTable(Phase.Action, out var masaya3); masaya3.Zone = Zone.Home; masaya3.ProvinceIndex = null; played.Players[0].Provinces[0].CardId = null;
        var h = MatchEngine.FromTestState(played);
        Check(!Options(h, 0, SearchPlayAbilities.MasayaRecruit, masaya3.Id).Any(o => o.Enabled), "a Masaya already in play cannot recruit himself");
    }

    // ---- The Sacred Rosary ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchState State, CardInstance Host, CardInstance Rosary, CardInstance InPlay) RosaryTable()
    {
        var s = Table(Phase.Action, 0);
        var host = Kw(Add(s, Crane, 0, Zone.Home), "Phoenix Clan");
        var rosary = Attach(s, Rosary, host);
        var inPlay = Add(s, EbisusHonesty, 0, Zone.Celestial);
        SetDeck(s, 0, Zone.DynastyDeck, "training-expensive-holding", MegumisGuidance);
        Add(s, RyoshunsGuidance, 0, Zone.DynastyDiscard);
        return (s, host, rosary, inPlay);
    }

    private static void RosaryScenario()
    {
        var (s, host, rosary, mine) = RosaryTable();
        var theirs = Add(s, EbisusHonesty, 1, Zone.Celestial);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, SearchPlayAbilities.RosaryCelestial, rosary.Id).Command!);
        Eq(e.State.Cards[mine.Id].Zone, Zone.DynastyDiscard, "his Celestial in play was discarded first");
        Eq(e.State.Cards[theirs.Id].Zone, Zone.Celestial, "the opponent's Celestial is untouched");
        Eq(e.State.PendingChoice?.Type, "searchDeck", "the searcher chooses the Dynasty deck or the discard pile");
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseSearchDeck").Select(a => a.Command!.Choice).ToArray();
        Check(choices.Contains(SearchDeck.Dynasty) && choices.Contains(SearchDeck.DynastyDiscard) && choices.Length == 2, "two places to search");
        // From the discard pile: the Celestial he just discarded and the one already there are both candidates.
        Accept(e, new GameCommand("chooseSearchDeck", 0, e.Version, Choice: SearchDeck.DynastyDiscard));
        var candidates = Offered(e, 0);
        Check(candidates.Contains(EbisusHonesty) && candidates.Contains(RyoshunsGuidance) && candidates.Length == 2, "the Celestials of the discard pile, the one just discarded included: " + string.Join(",", candidates));
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: RyoshunsGuidance));
        var entered = e.State.Cards.Values.Single(c => c.DefinitionId == RyoshunsGuidance && c.OwnerId == 0);
        Eq(entered.Zone, Zone.Celestial, "the Celestial is in play with no province");
        Check(entered.ProvinceIndex is null && e.State.Players[0].Provinces.All(p => p.CardId != entered.Id), "it took no province");
        Eq(e.State.Cards[rosary.Id].Zone, Zone.RemovedFromGame, "the discard pile was searched: the Rosary is removed from the game");
        Check(e.State.Cards[host.Id].Zone == Zone.Home, "its Personality stays");
        Check(e.State.ShownCards.Any(x => x.DefinitionId == RyoshunsGuidance), "the Celestial was shown");
        Check(!e.State.Events.Any(x => x.Type == "deckShuffled"), "a discard pile is not shuffled");
        Eq(e.State.PriorityPlayerId, 1, "the action ended");
        Eq(e.State.Players[0].Provinces.Count(p => p.CardId is not null), e.State.Players[0].Provinces.Count(p => p.CardId is not null), "fixture");
    }

    private static void RosaryEdges()
    {
        // The Dynasty deck as the source: shuffled, and the Rosary stays attached.
        var (s, host, rosary, mine) = RosaryTable();
        var e = MatchEngine.FromTestState(s);
        var provincesBefore = e.State.Players[0].Provinces.Select(p => p.CardId).ToArray();
        Accept(e, Enabled(e, 0, SearchPlayAbilities.RosaryCelestial, rosary.Id).Command!);
        Accept(e, new GameCommand("chooseSearchDeck", 0, e.Version, Choice: SearchDeck.Dynasty));
        Eq(string.Join(",", Offered(e, 0)), MegumisGuidance, "only the Celestial of the Dynasty deck qualifies");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: MegumisGuidance));
        Eq(e.State.Cards.Values.Single(c => c.DefinitionId == MegumisGuidance && c.OwnerId == 0).Zone, Zone.Celestial, "in play");
        Eq(e.State.Cards[rosary.Id].Zone, Zone.Attached, "searched the deck: the Rosary stays in play");
        Check(e.State.Events.Any(x => x.Type == "deckShuffled"), "the Dynasty deck is shuffled");
        Check(provincesBefore.SequenceEqual(e.State.Players[0].Provinces.Select(p => p.CardId)), "no province changed");
        Eq(e.State.Cards[mine.Id].Zone, Zone.DynastyDiscard, "the earlier Celestial was discarded");

        // Nothing found in the place chosen: the Rosary stays whichever place was searched, and nothing but the discards happened.
        var (t, host2, rosary2, mine2) = RosaryTable();
        SetDeck(t, 0, Zone.DynastyDeck, "training-expensive-holding");
        var f = MatchEngine.FromTestState(t);
        Accept(f, Enabled(f, 0, SearchPlayAbilities.RosaryCelestial, rosary2.Id).Command!);
        Accept(f, new GameCommand("chooseSearchDeck", 0, f.Version, Choice: SearchDeck.Dynasty));
        Check(Offered(f, 0).Length == 0, "no Celestial in the deck");
        Accept(f, new GameCommand("passSearch", 0, f.Version));
        Eq(f.State.Cards[rosary2.Id].Zone, Zone.Attached, "nothing was put into play, so the Rosary is not removed");
        Check(!f.State.Cards.Values.Any(c => c.OwnerId == 0 && c.Zone == Zone.Celestial), "no Celestial is in play");

        // Attach restriction: a Phoenix Clan or Naga Personality only.
        var u = Table(Phase.Action, 0);
        var crane = Add(u, Crane, 0, Zone.Home); var phoenix = Kw(Add(u, Crane, 0, Zone.Home), "Phoenix Clan"); var naga = Kw(Add(u, Crane, 0, Zone.Home), "Naga");
        var card = Add(u, Rosary, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(u);
        var snapshot = Json(g);
        Check(!g.Execute(Equip(g, 0, card, crane, 0)).Accepted, "a Crane Personality is refused");
        Eq(Json(g), snapshot, "nothing changed");
        Accept(g, Equip(g, 0, card, naga, 0));
        Eq(g.State.Cards[card.Id].AttachedTo, naga.Id, "a Naga Personality takes it (it costs nothing)");
        Check(phoenix.Id.Length > 0, "fixture");

        // Rejected: the opponent's use, a bowed wielder's use.
        var (v, host3, rosary3, _) = RosaryTable();
        var h = MatchEngine.FromTestState(v);
        var before = Json(h);
        Check(!h.Execute(new GameCommand("useAbility", 1, h.Version, CardId: rosary3.Id, AbilityId: SearchPlayAbilities.RosaryCelestial)).Accepted, "the opponent may not use it");
        Eq(Json(h), before, "nothing changed");

        // A bot's walk through the whole chain (the place, then the card) ends it.
        var (w, host4, rosary4, _) = RosaryTable();
        var k = MatchEngine.FromTestState(w);
        Accept(k, Enabled(k, 0, SearchPlayAbilities.RosaryCelestial, rosary4.Id).Command!);
        FirstEnabledUntilClear(k);
        Check(k.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Celestial) == 1, "exactly one Celestial is in play after the bot's walk");
        Check(host4.Id.Length > 0, "fixture");
    }

    // ---- The Topaz Armor ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Host, CardInstance Armor) TopazTable(params string[] hostKeywords)
    {
        var s = Table(Phase.Action, 0);
        var host = Kw(Add(s, Crane, 0, Zone.Home), ["Topaz Champion", .. hostKeywords]);
        var armor = Add(s, TopazArmor, 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), host, armor);
    }

    private static void TopazScenario()
    {
        var (e, host, armor) = TopazTable();
        // It attaches for no Gold ("ignoring costs"), and entering play starts a mandatory decision.
        Accept(e, Equip(e, 0, armor, host, 0));
        Eq(e.State.Cards[armor.Id].AttachedTo, host.Id, "attached to the Topaz Champion for no Gold");
        Eq(e.State.PendingChoice?.Type, "eventChoice", "the entry trait opened a decision");
        Eq(e.State.PendingChoice!.PlayerId, 0, "the Armor's controller chooses");
        var first = e.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
        Eq(first.Length, 11, "eleven keywords to choose from");
        Check(first.All(a => a.Enabled) && e.QueryActions(0).Count(a => a.Type is not ("eventChoice" or "concede")) == 0, "the choice is mandatory: no pass or decline is offered");
        Check(MatchEngine.TopazKeywordList.SequenceEqual(first.Select(a => a.Command!.Choice!)), "the printed list, in the printed order");
        var snapshot = Json(e);
        Check(!e.Execute(new GameCommand("eventChoice", 0, e.Version, CardId: armor.Id, Choice: "Ninja")).Accepted, "a keyword that is not on the list is refused");
        Check(!e.Execute(new GameCommand("pass", 0, e.Version)).Accepted, "a pass is refused");
        Eq(Json(e), snapshot, "nothing changed");
        Accept(e, new GameCommand("eventChoice", 0, e.Version, CardId: armor.Id, Choice: "Archer"));
        var second = e.QueryActions(0).Where(a => a.Type == "eventChoice").Select(a => a.Command!.Choice!).ToArray();
        Check(second.Length == 10 && !second.Contains("Archer"), "the second keyword is chosen from the ten that remain");
        Accept(e, new GameCommand("eventChoice", 0, e.Version, CardId: armor.Id, Choice: "Duelist"));
        Check(e.State.PendingChoice is null, "the decision is over");
        Eq(e.State.PriorityPlayerId, 1, "priority moves on as after any Equip");
        // The wielder has both keywords, as a Spirit's grant does.
        var granted = e.State.Cards[host.Id].GrantedKeywords.Where(g => g.Source == armor.Id).ToArray();
        Check(granted.Length == 2 && granted.All(g => g.Duration == GrantDuration.Attachment), "two keywords granted for as long as the Armor is attached");
        Check(e.State.Cards[host.Id].GrantedKeywords.Any(g => g.Keyword == "Archer") && e.State.Cards[host.Id].GrantedKeywords.Any(g => g.Keyword == "Duelist"), "Archer and Duelist");
        Check(e.State.SearchPlayTopazKeywords[armor.Id].SequenceEqual(new[] { "Archer", "Duelist" }), "the pair last chosen is remembered");
        // Leaving play any way takes them away: destroyed, returned to the hand, put on a deck.
        foreach (var how in new[] { "destroy", "hand", "deck" })
        {
            var (f, h2, a2) = TopazTable();
            Accept(f, Equip(f, 0, a2, h2, 0));
            Accept(f, new GameCommand("eventChoice", 0, f.Version, CardId: a2.Id, Choice: "Kensai"));
            Accept(f, new GameCommand("eventChoice", 0, f.Version, CardId: a2.Id, Choice: "Elite"));
            Check(f.State.Cards[h2.Id].GrantedKeywords.Count(g => g.Source == a2.Id) == 2, $"{how}: both keywords granted");
            LeavePlay(f, a2.Id, how);
            Check(f.State.Cards[a2.Id].Zone != Zone.Attached, $"{how}: the Armor left play");
            Check(!f.State.Cards[h2.Id].GrantedKeywords.Any(g => g.Source == a2.Id), $"{how}: the wielder lost both keywords");
        }
        // Destroyed by another player's action (Chikara - exp2's Battle action destroys an enemy attachment): the same loss through a real action.
        var t = Table(Phase.Action, 1);
        var enemyHost = Kw(Add(t, Crane, 1, Zone.Home), "Topaz Champion"); var enemyArmor = Add(t, TopazArmor, 1, Zone.Hand);
        var d = MatchEngine.FromTestState(t);
        Accept(d, Equip(d, 1, enemyArmor, enemyHost, 0));
        Accept(d, new GameCommand("eventChoice", 1, d.Version, CardId: enemyArmor.Id, Choice: "Magistrate"));
        Accept(d, new GameCommand("eventChoice", 1, d.Version, CardId: enemyArmor.Id, Choice: "Historian"));
        Check(d.State.Cards[enemyHost.Id].GrantedKeywords.Count(g => g.Source == enemyArmor.Id) == 2, "granted to the wielder");
        var moved = Edit(d, st =>
        {
            st.Phase = Phase.Battle; st.ActivePlayerId = 1; st.PriorityPlayerId = 0; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.ConsecutivePasses = 0;
            foreach (var c in st.Cards.Values.Where(c => c.Id == enemyHost.Id || c.Id == enemyArmor.Id)) { if (c.Id == enemyHost.Id) c.Zone = Zone.Battlefield; c.ProvinceIndex = 0; }
            Add(st, "oracle-l5r-1319", 0, Zone.Battlefield, province: 0);
        });
        var chikara = moved.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-1319");
        Accept(moved, Offer(moved, 0, "komatsu-destroy-attachment", chikara.Id, enemyArmor.Id));
        Check(!moved.State.Cards[enemyHost.Id].GrantedKeywords.Any(g => g.Source == enemyArmor.Id), "a destroyed Armor takes both keywords with it");
        Check(moved.State.Cards[enemyArmor.Id].Zone == Zone.FateDiscard, "the Armor was destroyed");
    }

    private static void LeavePlay(MatchEngine e, string cardId, string how)
    {
        var state = (MatchState)typeof(MatchEngine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(e)!;
        var card = state.Cards[cardId];
        var method = typeof(MatchEngine).GetMethod(how switch { "destroy" => "DestroyAttachment", "hand" => "ReturnToHand", _ => "ToOwnerDeck" }, BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(e, how == "deck" ? [card, "Top"] : [card]);
    }

    private static void TopazEdges()
    {
        // The engine's own leave-play paths remove the grants: a destroyed Armor (the Armor's own Reaction-free destruction by an attack on the unit is DestroyAttachment).
        var (e, host, armor) = TopazTable("Samurai");
        Accept(e, Equip(e, 0, armor, host, 0));
        Accept(e, new GameCommand("eventChoice", 0, e.Version, CardId: armor.Id, Choice: "Samurai"));
        Accept(e, new GameCommand("eventChoice", 0, e.Version, CardId: armor.Id, Choice: "Stalwart"));
        Check(e.State.Cards[host.Id].GrantedKeywords.Count(g => g.Source == armor.Id) == 1 && e.State.Cards[host.Id].GrantedKeywords.Any(g => g.Keyword == "Stalwart"),
            "a keyword the wielder already printed (Samurai) is not granted twice, so nothing of his own is lost with the Armor");
        Check(e.QueryActions(0).All(a => a.Type != "eventChoice") && e.State.PendingChoice is null, "the decision is over");

        // A Personality without Topaz Champion is refused, with nothing changed.
        var s = Table(Phase.Action, 0);
        var plain = Add(s, Crane, 0, Zone.Home); var card = Add(s, TopazArmor, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(s);
        var snapshot = Json(g);
        Check(!g.Execute(Equip(g, 0, card, plain, 0)).Accepted, "not a Topaz Champion: refused");
        Eq(Json(g), snapshot, "nothing changed");

        // Re-entering play chooses a fresh pair (the earlier pair is not kept).
        var (f, host2, armor2) = TopazTable();
        Accept(f, Equip(f, 0, armor2, host2, 0));
        Accept(f, new GameCommand("eventChoice", 0, f.Version, CardId: armor2.Id, Choice: "Courtier"));
        Accept(f, new GameCommand("eventChoice", 0, f.Version, CardId: armor2.Id, Choice: "Cavalry"));
        var h = Edit(f, st =>
        {
            var a = st.Cards[armor2.Id]; var w = st.Cards[host2.Id];
            w.GrantedKeywords.RemoveAll(k => k.Source == a.Id);
            a.Zone = Zone.Hand; a.AttachedTo = null; st.Players[0].Hand.Add(a.Id);
            st.Phase = Phase.Action; st.ActivePlayerId = 0; st.PriorityPlayerId = 0;
        });
        Accept(h, Equip(h, 0, h.State.Cards[armor2.Id], h.State.Cards[host2.Id], 0));
        Check(h.State.SearchPlayTopazKeywords[armor2.Id].Count == 0, "a fresh entry forgets the last pair until the new one is chosen");
        Eq(h.QueryActions(0).Count(a => a.Type == "eventChoice"), 11, "all eleven keywords are on offer again");
        FirstEnabledUntilClear(h);
        Eq(h.State.SearchPlayTopazKeywords[armor2.Id].Count, 2, "a new pair was chosen (first enabled answer for a bot)");
    }
}
