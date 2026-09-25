using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group F: event-wave-8, region-wave-9 and holding-wave-13, driven through QueryActions and the commands it offers. The cards left unwired
// are in tools/content/ee_remF_blockers.json and are not published.
internal static partial class EmperorRemainingFTests
{
    private const string AkodosGrave = "oracle-l5r-309", Fortifications = "oracle-l5r-2662", LuxuriousSilk = "oracle-l5r-4787", SilkWorks = "oracle-l5r-7265",
        TemplesOfGiseiToshi = "oracle-l5r-7898", TravelingPeddler = "oracle-l5r-8729",
        BlightedRegion = "oracle-l5r-1042", RefugeOfTheThreeSisters = "oracle-l5r-6226";

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
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static void Pass(MatchEngine e) => Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
    private static void DeclineWindows(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    public static void Run(TestRunner runner) => runner.Run("emperor-remaining-F", [
        ("Published shape: the wired Holdings print their stats, keywords, scan text, Effect and ability ids, ship their art and are listed in holding-wave-13's manifest", HoldingsPublishedShape),
        ("Traveling Peddler: bow and pay 3 Gold to draw a card, once per turn (Limited) -- not while bowed, not without the Gold", PeddlerDraw),
        ("Silk Works: produces 4 Gold", SilkWorksProduces),
        ("Fortifications: its controller's provinces have +1 strength while it is in play -- not the opponent's, not once it is gone", FortificationsStrength),
        ("Temples of Gisei Toshi: name a type, look at the top four Fate cards, show one of that type into the hand; the other cards stay where they were", TemplesLook),
        ("Akodo's Grave: bow and destroy it to destroy a Terrain in a battle -- not without a Terrain, not while bowed", AkodosGraveDestroy),
        ("Published shape: the wired Regions print their stats, keywords, scan text, Effect and ability ids, ship their art and are listed in region-wave-9's manifest", RegionsPublishedShape),
        ("Blighted Region: only the Defender; bows the Region and moves the performing Personality to the battle (Absent), straightening him and gaining 2 Honor if he moved and is opposed", BlightedRegionMove),
        ("Refuge of the Three Sisters: destroys the Region and removes the performing unbowed Personality from the game to search the Fate deck for a card with a Focus Value below his Chi", RefugeSearch),
        ("A bowed Region straightens with its province's owner's other cards at the start of his turn", RegionStraightens),
        ("Luxurious Silk: while bowed and without bowing, produces 2 Gold once a turn for an action's payment -- not for a recruit", LuxuriousSilkGold),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // ---- holding-wave-13 ------------------------------------------------------------------------------------------------------------------------------------

    private static void HoldingsPublishedShape()
    {
        var root = RepoRoot();
        void Holding(string id, string name, int cost, int production, string effect, string[] abilities, string text, params string[] keywords)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Holding && c.Name == name && c.GoldCost == cost && c.GoldProduction == production, $"{name}: type={c.Type} cost={c.GoldCost} production={c.GoldProduction}");
            Eq(c.Effect, effect, $"{name}'s effect");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{name}'s abilities");
            Eq(string.Join(",", (c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal)), string.Join(",", keywords.OrderBy(k => k, StringComparer.Ordinal)), $"{name}'s keywords");
            Eq(c.Text, text, $"{name} prints the scan's text");
        }
        Holding(AkodosGrave, "Akodo's Grave", 2, 2, "none", ["akodo-grave-destroy"], "Bow this card: Produce 2 Gold.\nBattle: Bow and destroy this card and target a Terrain: Destroy it.");
        Holding(Fortifications, "Fortifications", 0, 0, "fortifications-provinces", [], "Your provinces have +1 strength.", "Castle");
        Holding(LuxuriousSilk, "Luxurious Silk", 3, 2, "none", ["luxurious-silk-gold"], "Bow this card: Produce 2 Gold.\nReaction: Even if this card is bowed, when paying for an action: Produce 2 Gold.");
        Holding(SilkWorks, "Silk Works", 5, 4, "none", [], "Bow this card: Produce 4 Gold.");
        Holding(TemplesOfGiseiToshi, "Temples of Gisei Toshi", 2, 2, "none", ["temples-gisei-follower", "temples-gisei-item", "temples-gisei-spell"],
            "Bow this card: Produce 2 Gold.\nLimited: Bow this card: Name \"Follower\", \"Item\", or \"Spell\". Look at the top four cards of your Fate deck. You may show one of those cards that is of the type you named, then put it in your hand.", "Temple", "Unique");
        Holding(TravelingPeddler, "Traveling Peddler", 2, 2, "none", ["peddler-draw"], "Bow this card: Produce 2 Gold.\nLimited: Bow this card and pay 3 Gold: Draw a card.", "Singular");
        Manifest(root, "holding-wave-13", AkodosGrave, Fortifications, LuxuriousSilk, SilkWorks, TemplesOfGiseiToshi, TravelingPeddler);
    }

    private static void PeddlerDraw()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var peddler = Add(s, TravelingPeddler, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Enabled(e, 0, "peddler-draw", peddler.Id).Command!);
        Eq(e.State.Players[0].Hand.Count, hand + 1, "the Peddler drew a card");
        Check(e.State.Cards[peddler.Id].Bowed, "bowing the Peddler is part of the cost");
        Check(!CanUse(e, 0, "peddler-draw", peddler.Id), "bowed (and once a turn), it cannot draw again");
        // A bowed Peddler cannot draw at all.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var bowed = Add(s2, TravelingPeddler, 0, Zone.Home, bowed: true);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "peddler-draw", bowed.Id), "a bowed Peddler cannot draw");
        // Without 3 Gold to pay (every Gold source bowed), it cannot draw.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var poor = Add(s3, TravelingPeddler, 0, Zone.Home);
        foreach (var c in s3.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != poor.Id && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "peddler-draw", poor.Id), "no Gold to pay for it, no draw");
    }

    private static void SilkWorksProduces()
    {
        var s = Table(Phase.Action, 0);
        var works = Add(s, SilkWorks, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(works.Id, 0), 4, "Silk Works produces 4 Gold");
    }

    private static void FortificationsStrength()
    {
        var s = Table(Phase.Action, 0);
        var e0 = MatchEngine.FromTestState(s);
        var before = e0.GetView(0).Players[0].Provinces.Select(p => p.Strength).ToArray();
        var beforeOpp = e0.GetView(0).Players[1].Provinces.Select(p => p.Strength).ToArray();
        var s1 = Table(Phase.Action, 0);
        var fort = Add(s1, Fortifications, 0, Zone.Home);
        var e1 = MatchEngine.FromTestState(s1);
        var mine = e1.GetView(0).Players[0].Provinces.Select(p => p.Strength).ToArray();
        Check(mine.Select((v, i) => v - before[i]).All(d => d == 1), "each of its controller's provinces has +1 strength");
        Check(e1.GetView(0).Players[1].Provinces.Select(p => p.Strength).SequenceEqual(beforeOpp), "the opponent's provinces are unchanged");
        // Two of them stack; once one is gone the bonus is too.
        var s2 = Table(Phase.Action, 0); Add(s2, Fortifications, 0, Zone.Home); Add(s2, Fortifications, 0, Zone.Home);
        Check(MatchEngine.FromTestState(s2).GetView(0).Players[0].Provinces.Select((p, i) => p.Strength - before[i]).All(d => d == 2), "two Fortifications give +2");
        var s3 = Table(Phase.Action, 0); Add(s3, Fortifications, 0, Zone.DynastyDiscard);
        Check(MatchEngine.FromTestState(s3).GetView(0).Players[0].Provinces.Select((p, i) => p.Strength - before[i]).All(d => d == 0), "a discarded Fortifications gives nothing");
    }

    private static void TemplesLook()
    {
        MatchState Setup(out CardInstance temples, out string[] deck)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            temples = Add(s, TemplesOfGiseiToshi, 0, Zone.Home);
            var p = s.Players[0];
            foreach (var id in p.FateDeck.ToArray()) s.Cards.Remove(id);
            p.FateDeck.Clear();
            string[] definitions = ["training-crane-samurai", "training-item", "training-follower", "training-spell", "training-item", "training-follower"];
            var ids = new List<string>();
            foreach (var d in definitions)
            {
                var card = new CardInstance { Id = $"deck-{s.Cards.Count}", DefinitionId = d, OwnerId = 0, Zone = Zone.FateDeck, FaceUp = false };
                s.Cards.Add(card.Id, card); p.FateDeck.Add(card.Id); ids.Add(card.Id);
            }
            deck = ids.ToArray();
            return s;
        }
        // Naming Item: the Item among the top four (position 1) may be shown; a Follower (position 2) may not.
        var s1 = Setup(out var temples1, out var deck1);
        var e1 = MatchEngine.FromTestState(s1);
        Accept(e1, Enabled(e1, 0, "temples-gisei-item", temples1.Id).Command!);
        Eq(e1.State.PendingLook?.LookId, "temples-gisei-item", "a look at the Fate deck is open");
        Reject(e1, new("arrangeTopCards", 0, e1.Version, CardIds: ["0:top", "1:top", "2:show", "3:top"]));
        Accept(e1, new("arrangeTopCards", 0, e1.Version, CardIds: ["0:top", "1:show", "2:top", "3:top"]));
        Check(e1.State.Players[0].Hand.Contains(deck1[1]), "the shown Item is in the hand");
        Eq(string.Join(",", e1.State.Players[0].FateDeck.Take(5)), string.Join(",", new[] { deck1[0], deck1[2], deck1[3], deck1[4], deck1[5] }), "the other cards stay in their order");
        Check(e1.State.Cards[temples1.Id].Bowed, "bowing is the cost");
        Check(!CanUse(e1, 0, "temples-gisei-spell", temples1.Id) && !CanUse(e1, 0, "temples-gisei-item", temples1.Id), "one look a turn (Limited), whichever type was named");
        // "You may": showing nothing is legal and leaves the deck as it was; a card beyond the fourth is never seen.
        var s2 = Setup(out var temples2, out var deck2);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "temples-gisei-spell", temples2.Id).Command!);
        Accept(e2, new("arrangeTopCards", 0, e2.Version, CardIds: ["0:top", "1:top", "2:top", "3:show"]));
        Check(e2.State.Players[0].Hand.Contains(deck2[3]), "the named Spell was shown and taken");
        var s3 = Setup(out var temples3, out var deck3);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 0, "temples-gisei-follower", temples3.Id).Command!);
        Accept(e3, new("arrangeTopCards", 0, e3.Version, CardIds: ["0:top", "1:top", "2:top", "3:top"]));
        Eq(string.Join(",", e3.State.Players[0].FateDeck), string.Join(",", deck3), "showing nothing leaves the deck as it was");
        // A bowed Temples cannot look.
        var s4 = Setup(out var temples4, out _); temples4.Bowed = true;
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "temples-gisei-item", temples4.Id), "a bowed Temples cannot look");
    }

    private static void Reject(MatchEngine e, GameCommand command) => Check(!e.Execute(command).Accepted, $"expected {command.Type} to be rejected");

    private static void AkodosGraveDestroy()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var grave = Add(s, AkodosGrave, 0, Zone.Home);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var terrain = Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "akodo-grave-destroy", grave.Id, terrain.Id));
        DeclineWindows(e);
        Check(e.State.Cards[terrain.Id].Zone != Zone.Battlefield, "the Terrain was destroyed");
        Check(e.State.Cards[grave.Id].Zone is not (Zone.Home or Zone.Stronghold), "Akodo's Grave was destroyed as the cost");
        // No Terrain in play: nothing to target.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var grave2 = Add(s2, AkodosGrave, 0, Zone.Home);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "akodo-grave-destroy", grave2.Id), "no Terrain, no action");
        // A bowed Grave cannot pay the bow.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        var bowed = Add(s3, AkodosGrave, 0, Zone.Home, bowed: true);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s3, "training-terrain", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "akodo-grave-destroy", bowed.Id), "a bowed Grave cannot be used");
    }

    private static void LuxuriousSilkGold()
    {
        MatchState Setup(bool silkBowed, out CardInstance silk, out CardInstance spell, out CardInstance shugenja)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            silk = Add(s, LuxuriousSilk, 0, Zone.Home, bowed: silkBowed);
            var silkId = silk.Id;
            foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != silkId && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
            shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            spell = Add(s, "training-spell", 0, Zone.Hand);
            return s;
        }
        // Equipping a Spell (an action) with every other source bowed and Luxurious Silk itself bowed: its Reaction pays 2 Gold, and the Silk stays bowed.
        var s1 = Setup(true, out var silk1, out var spell1, out var shugenja1);
        var e1 = MatchEngine.FromTestState(s1);
        var cost = CardCatalog.Get("training-spell").GoldCost;
        Check(e1.PaymentOptions(0, Math.Min(cost, 2), new PaymentSubject(spell1, PaymentKind.Equip)).Any(p => p.SourceIds.SequenceEqual([silk1.Id])), "the bowed Silk pays 2 Gold for an action");
        // It produces exactly 2: a payment of more than 2 cannot be met by it alone.
        Check(!e1.PaymentOptions(0, 3, new PaymentSubject(spell1, PaymentKind.Equip)).Any(p => p.SourceIds.SequenceEqual([silk1.Id])), "2 Gold is all it produces");
        // For a recruit it produces nothing.
        Check(e1.PaymentOptions(0, 1, new PaymentSubject(Add(s1, "training-crane-samurai", 0, Zone.Hand), PaymentKind.Recruit)).Length == 0, "not for a recruit");
        // Once a turn.
        var s2 = Setup(true, out var silk2, out var spell2, out _);
        silk2.AbilityUses["luxurious-silk-gold"] = s2.TurnNumber;
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.PaymentOptions(0, 1, new PaymentSubject(spell2, PaymentKind.Equip)).Length == 0, "the Reaction is spent for the turn");
    }
}
