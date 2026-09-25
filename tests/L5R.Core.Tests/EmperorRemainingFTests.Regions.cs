using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group F: the Regions of region-wave-9 (a part of EmperorRemainingFTests).
internal static partial class EmperorRemainingFTests
{
    private static void RegionsPublishedShape()
    {
        var root = RepoRoot();
        void Region(string id, string name, string[] abilities, string text, params string[] keywords)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Region && c.Name == name && c.GoldCost == 0, $"{name}: type={c.Type} cost={c.GoldCost}");
            Eq(c.Effect, "none", $"{name}'s effect");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{name}'s abilities");
            Eq(string.Join(",", (c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal)), string.Join(",", keywords.OrderBy(k => k, StringComparer.Ordinal)), $"{name}'s keywords");
            Eq(c.Text, text, $"{name} prints the scan's text");
        }
        Region(BlightedRegion, "Blighted Region", ["blighted-region-move"], "Battle: If you are the Defender, bow this card and choose your performing Personality: Move him to the current battlefield. If he moved and is now opposed, straighten him and gain 2 Honor.", "Singular");
        Region(RefugeOfTheThreeSisters, "Refuge of the Three Sisters", ["refuge-three-sisters-search"],
            "Limited: Destroy this card and remove your performing unbowed Personality from the game: Search your Fate deck for a card with a lower Focus Value than your Personality's Chi. Show it and put it in your hand.", "Singular");
        Manifest(root, "region-wave-9", BlightedRegion, RefugeOfTheThreeSisters);
        Eq(PublishedContent.RegionWave9.Cards.Length, 2, "region-wave-9 holds exactly the wired Regions");
    }

    private static CardInstance AttachRegion(MatchState s, string definition, int owner, int province)
    {
        var region = Add(s, definition, owner, Zone.Region, province: province);
        s.Players[owner].Provinces[province].RegionId = region.Id;
        return region;
    }

    private static void BlightedRegionMove()
    {
        MatchState Setup(out CardInstance region, out CardInstance mover, bool enemyThere = true, bool moverBowed = true)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            region = AttachRegion(s, BlightedRegion, 0, 1);
            mover = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: moverBowed);
            if (enemyThere) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return s;
        }
        // The Defender with no unit at the battlefield: the Personality moves there, is opposed, straightens and the Region's controller gains 2 Honor.
        var s1 = Setup(out var region1, out var mover1);
        var e1 = MatchEngine.FromTestState(s1);
        var honor = Honor(e1, 0);
        Accept(e1, Enabled(e1, 0, "blighted-region-move", region1.Id, mover1.Id).Command!);
        DeclineWindows(e1);
        Check(e1.State.Cards[mover1.Id].Zone == Zone.Battlefield && e1.State.Cards[mover1.Id].ProvinceIndex == 0, "he moved to the current battlefield");
        Check(!e1.State.Cards[mover1.Id].Bowed, "he straightened");
        Eq(Honor(e1, 0), honor + 2, "gained 2 Honor");
        Check(e1.State.Cards[region1.Id].Bowed, "the Region bowed as its cost");
        // Not opposed (no enemy unit there): he moves but does not straighten and no Honor is gained.
        var s2 = Setup(out var region2, out var mover2, enemyThere: false);
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 0);
        Accept(e2, Enabled(e2, 0, "blighted-region-move", region2.Id, mover2.Id).Command!);
        DeclineWindows(e2);
        Check(e2.State.Cards[mover2.Id].Zone == Zone.Battlefield && e2.State.Cards[mover2.Id].Bowed, "he moved, unopposed, and stayed bowed");
        Eq(Honor(e2, 0), honor2, "no Honor without an opposed move");
        // Already at the battlefield: he does not move, so nothing else happens.
        var s3 = Setup(out var region3, out _);
        var already = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        var honor3 = Honor(e3, 0);
        Accept(e3, Enabled(e3, 0, "blighted-region-move", region3.Id, already.Id).Command!);
        DeclineWindows(e3);
        Check(e3.State.Cards[already.Id].Bowed && Honor(e3, 0) == honor3, "no move, no straighten, no Honor");
        // The Attacker may not use it.
        var s4 = Table(Phase.Battle, 0); s4.PriorityPlayerId = 0;
        var region4 = AttachRegion(s4, BlightedRegion, 0, 1);
        Add(s4, "training-crane-samurai", 0, Zone.Home);
        Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "blighted-region-move", region4.Id), "only the Defender may use it");
        // A bowed Region cannot pay its bow.
        var s5 = Setup(out var region5, out _); region5.Bowed = true;
        Check(!CanUse(MatchEngine.FromTestState(s5), 0, "blighted-region-move", region5.Id), "a bowed Region cannot be used");
    }

    private static void RefugeSearch()
    {
        MatchState Setup(out CardInstance region, out CardInstance performer, out CardInstance attachment, bool performerBowed = false)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            region = AttachRegion(s, RefugeOfTheThreeSisters, 0, 1);
            performer = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: performerBowed); // Chi 3
            attachment = Add(s, "training-item", 0, Zone.Attached, parent: performer.Id);
            var p = s.Players[0];
            foreach (var id in p.FateDeck.ToArray()) s.Cards.Remove(id);
            p.FateDeck.Clear();
            // Focus Values: a Strategy with Focus 3 (not below his Chi 3), one with 2, one with 1, and an Item (Focus 0).
            foreach (var d in new[] { "training-focus-strategy", "training-duel-challenge", "training-stat-cost-low-strategy", "training-item" })
            {
                var card = new CardInstance { Id = $"deck-{s.Cards.Count}", DefinitionId = d, OwnerId = 0, Zone = Zone.FateDeck, FaceUp = false };
                s.Cards.Add(card.Id, card); p.FateDeck.Add(card.Id);
            }
            return s;
        }
        var s1 = Setup(out var region1, out var performer1, out var attachment1);
        var e1 = MatchEngine.FromTestState(s1);
        Eq(CardCatalog.Get("training-focus-strategy").Focus, 3, "fixture: Focus 3");
        Accept(e1, Enabled(e1, 0, "refuge-three-sisters-search", region1.Id, performer1.Id).Command!);
        Check(e1.State.Cards[region1.Id].Zone == Zone.DynastyDiscard && e1.State.Players[0].Provinces[1].RegionId is null, "the Region was destroyed");
        Eq(e1.State.Cards[performer1.Id].Zone, Zone.RemovedFromGame, "the performer was removed from the game");
        Check(e1.State.Cards[attachment1.Id].Zone == Zone.FateDiscard, "his attachment was discarded");
        Eq(e1.State.PendingSearch?.SearchId, "refuge-three-sisters", "a search of the Fate deck is open");
        // A Focus 3 card is not below his Chi 3; the Focus 2 card is.
        Check(!e1.Execute(new GameCommand("selectSearchCard", 0, e1.Version, Choice: "training-focus-strategy")).Accepted, "a card with Focus 3 is not lower than Chi 3");
        Accept(e1, new GameCommand("selectSearchCard", 0, e1.Version, Choice: "training-duel-challenge"));
        Check(e1.State.Players[0].Hand.Any(id => e1.State.Cards[id].DefinitionId == "training-duel-challenge"), "the card was put in the hand");
        Check(e1.State.ShownCards.Any(c => c.DefinitionId == "training-duel-challenge"), "it was shown");
        // A bowed performer cannot be chosen.
        var s2 = Setup(out var region2, out var performer2, out _, performerBowed: true);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "refuge-three-sisters-search", region2.Id, performer2.Id), "the performing Personality must be unbowed");
        // A Limited action: not on the opponent's turn.
        var s3 = Setup(out var region3, out var performer3, out _); s3.ActivePlayerId = 1;
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "refuge-three-sisters-search", region3.Id, performer3.Id), "Limited: only on your own turn");
    }

    private static void RegionStraightens()
    {
        var s = Table(Phase.Action, 0);
        var region = AttachRegion(s, BlightedRegion, 0, 1); region.Bowed = true;
        var other = AttachRegion(s, BlightedRegion, 1, 1); other.Bowed = true;
        var e = MatchEngine.FromTestState(s);
        // Drive to player 1's Action Phase (his Straighten Phase has run) with offered commands only.
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        Check(!e.State.Cards[other.Id].Bowed, "the active player's Region straightened in his Straighten Phase");
        Check(e.State.Cards[region.Id].Bowed, "the other player's Region is still bowed on his turn");
    }
}
