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
        var temples = CardCatalog.Get(TenThousandTemples);
        Check(temples.Type == CardType.Region && temples.Effect == "ten-thousand-temples" && temples.Abilities!.SequenceEqual(["ten-thousand-temples-honor"]), "Ten Thousand Temples");
        Eq(temples.Text, "Before you lose Honor, destroy this card: Negate the Honor loss.\nLimited: If your Family Honor is 9 or lower, bow this card: Gain 1 Honor.", "Ten Thousand Temples prints the scan's text");
        Eq(string.Join(",", temples.Keywords!.OrderBy(k => k, StringComparer.Ordinal)), "Singular,Temple", "Ten Thousand Temples' keywords");
        Manifest(root, "region-wave-9", BlightedRegion, RefugeOfTheThreeSisters, TenThousandTemples);
        Eq(PublishedContent.RegionWave9.Cards.Length, 3, "region-wave-9 holds exactly the wired Regions");
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

    private const string TenThousandTemples = "oracle-l5r-7908";

    private static void TenThousandTemplesTests()
    {
        // Player 1's Tsujiken - exp dishonors player 0's Personality; his controller would lose 1 Honor. Player 0 controls Ten Thousand Temples: he is asked before the loss.
        MatchState Setup(out CardInstance temple, out CardInstance tsujiken, out CardInstance victim, bool templeOwnedByOpponent = false)
        {
            var s = Table(Phase.Action, 1); s.PriorityPlayerId = 1;
            temple = AttachRegion(s, TenThousandTemples, templeOwnedByOpponent ? 1 : 0, 1);
            tsujiken = Add(s, TsujikenExp, 1, Zone.Home);
            victim = Add(s, "training-crane-samurai", 0, Zone.Home);
            s.Players[0].Honor = 12;
            return s;
        }
        var s1 = Setup(out var temple1, out var tsujiken1, out var victim1);
        var e1 = MatchEngine.FromTestState(s1);
        Accept(e1, Offer(e1, 1, "tsujiken-exp-dishonor", tsujiken1.Id, victim1.Id));
        Eq(e1.State.PendingChoice?.Type, "honorLossDecision", "the Temple's controller is asked before he loses Honor");
        Eq(e1.State.PriorityPlayerId, 0, "player 0 decides");
        Eq(Honor(e1, 0), 12, "nothing has happened yet");
        Check(!e1.State.Cards[victim1.Id].Dishonorable && !e1.State.Cards[tsujiken1.Id].Bowed, "the action is held whole: no bow, no dishonor yet");
        var options = e1.QueryActions(0).Where(o => o.Type == "honorLossChoice").Select(o => o.Label).ToArray();
        Check(options.Length == 2 && options.Any(o => o.StartsWith("Destroy")) && options.Any(o => o.StartsWith("Lose")), "destroy it to negate the loss, or lose the Honor: " + string.Join("; ", options));
        Check(!e1.QueryActions(1).Any(o => o.Type == "honorLossChoice"), "only the losing player answers");
        Accept(e1, e1.QueryActions(0).First(o => o.Type == "honorLossChoice" && o.Label.StartsWith("Destroy")).Command!);
        Eq(Honor(e1, 0), 12, "the Honor loss was negated");
        Check(e1.State.Cards[temple1.Id].Zone == Zone.DynastyDiscard && e1.State.Players[0].Provinces[1].RegionId is null, "Ten Thousand Temples was destroyed");
        Check(e1.State.Cards[victim1.Id].Dishonorable && e1.State.Cards[tsujiken1.Id].Bowed, "the rest of the action resolved");
        Check(e1.State.PendingHonorLossDecision is null, "no decision is left open");
        // Declined: the loss happens and the Temple stays.
        var e2 = MatchEngine.FromTestState(Setup(out var temple2, out var tsujiken2, out var victim2));
        Accept(e2, Offer(e2, 1, "tsujiken-exp-dishonor", tsujiken2.Id, victim2.Id));
        Accept(e2, e2.QueryActions(0).First(o => o.Type == "honorLossChoice" && o.Label.StartsWith("Lose")).Command!);
        Eq(Honor(e2, 0), 11, "he lost the Honor");
        Eq(e2.State.Cards[temple2.Id].Zone, Zone.Region, "and kept the Temple");
        // Not the opponent's loss; not without a Temple (nothing is asked).
        var e3 = MatchEngine.FromTestState(Setup(out _, out var tsujiken3, out var victim3, templeOwnedByOpponent: true));
        Accept(e3, Offer(e3, 1, "tsujiken-exp-dishonor", tsujiken3.Id, victim3.Id));
        Check(e3.State.PendingChoice?.Type != "honorLossDecision", "a Temple only protects its own controller's Honor");
        Eq(Honor(e3, 0), 11, "he lost the Honor (player 1's Temple is not his)");

        // Limited: with Family Honor 9 or lower, bow it to gain 1 Honor.
        MatchState Limited(int honor, out CardInstance region)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            region = AttachRegion(s, TenThousandTemples, 0, 1);
            s.Players[0].Honor = honor;
            return s;
        }
        var l9 = MatchEngine.FromTestState(Limited(9, out var region9));
        Accept(l9, Enabled(l9, 0, "ten-thousand-temples-honor", region9.Id).Command!);
        Eq(Honor(l9, 0), 10, "gained 1 Honor");
        Check(l9.State.Cards[region9.Id].Bowed, "bowed as its cost");
        var l10 = MatchEngine.FromTestState(Limited(10, out var region10));
        Check(!CanUse(l10, 0, "ten-thousand-temples-honor", region10.Id), "not with Family Honor above 9");
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
