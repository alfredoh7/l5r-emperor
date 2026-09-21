using static Support337;
using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Created-card follow-up (issue #406, Mendon's answer 14): a created Holding enters play at home alongside the other Holdings, unbowed or bowed as the text says; a created Terrain
// enters play at the current battlefield, and with no current battlefield (or a Terrain already there) the creating action is refused with a reason. Driven through the actions the
// engine offers, with synthetic creators (no published card creating a Holding or a Terrain is wired: each needs another mechanic, listed in the PR).
internal static class CreatedHoldingsTerrainTests
{
    private const string Charter = "oracle-l5r-10806", QuestionableResources = "oracle-l5r-10024", HoldingMaker = "training-holding-maker", CreatedTerrain = "training-created-terrain", PublicRecords = "oracle-l5r-6095";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Charter (2 Gold) and Questionable Resources (3 Gold) are created-only Holdings with art, sha256 and a manifest; every creator, fixtures included, still resolves", PublishedShape),
            ("A created Holding enters play at home alongside the other Holdings, unbowed and producing its Gold, or bowed and producing none until it straightens; nothing is drawn, shuffled or paid", HoldingsEnterAtHome),
            ("A created Holding that is destroyed or found in a discard pile is removed from the game, never left in a pile", HoldingsAreNeverPiled),
            ("A created Terrain enters play at the current battlefield on its creator's side, blocks a second Terrain there (created or played) and is removed from the game when the battle ends", TerrainEntersAtTheBattlefield),
            ("With no current battlefield a Terrain may not be created: the action is offered disabled with a reason and refused, in the Action phase and while a battle is only being selected", NoBattlefieldNoTerrain),
            ("Specs are checked as data: a bowed Terrain or Personality, several Holdings or Terrains, and a Strategy that is no Terrain are Verify failures naming the creator", SpecRules),
        ];
        runner.Run("created-holdings-terrain", tests);
    }

    private static void Check(bool ok, string? message = null) => Expect(ok, message ?? "Assertion failed");
    private static CardInstance[] CreatedOf(MatchEngine e, string definitionId) => e.State.Cards.Values.Where(c => c.DefinitionId == definitionId && c.Id.StartsWith("created-", StringComparison.Ordinal)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    private static CardInstance Created(MatchEngine e, string definitionId) => CreatedOf(e, definitionId).Single();
    private static GameCommand Cmd(MatchEngine e, string type) => new(type, e.State.PriorityPlayerId, e.Version);
    private static int MaxGold(MatchEngine e, int player) { var n = 0; while (e.PaymentOptions(player, n + 1).Length > 0) n++; return n; }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        foreach (var (id, production, text) in new[] { (Charter, 2, "Bow this card: Produce 2 Gold."), (QuestionableResources, 3, "Bow this card: Produce 3 Gold.") })
        {
            var def = CardCatalog.Get(id);
            Eq(def.Type, CardType.Holding, id); Check(def.CreatedOnly && !def.ScenarioOnly, id + " is a published created-only card"); Eq(def.GoldProduction, production); Eq(def.GoldCost, 0);
            Check(def.Text == "May not be included in decks.\n" + text, def.Text); Check(ArtShipped(id), id + " art is shipped");
            var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/holding-wave-4-sources.json"))).RootElement.EnumerateArray().Single(x => x.GetProperty("card_id").GetString() == id);
            var receipt = Directory.GetFiles(Path.Combine(root, "assets/reference/oracle"), $"{id}-printing-*-image-1.json", SearchOption.AllDirectories).Select(f => JsonDocument.Parse(File.ReadAllText(f)).RootElement).First();
            var sha = source.GetProperty("image_sha256").GetString();
            Check(sha == receipt.GetProperty("sha256").GetString() || receipt.TryGetProperty("working_copy", out var copy) && sha == copy.GetProperty("sha256").GetString(), id + ": the recorded scan is the receipt's");
            Eq(source.GetProperty("source_url").GetString(), $"https://oracleofthevoid.com/#cardid={id.Replace("oracle-l5r-", "")}");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/holding-wave-4.txt")).Contains(id), id + " is in the wave manifest");
        }
        Eq(PublishedContent.HoldingWave4.Cards.Length, 2);
        Check(CardCatalog.PlayableFromDecks.All(c => c.Id is not (Charter or QuestionableResources or CreatedTerrain)), "no created scan is playable from a deck");
        Eq(string.Join("; ", CreatedCards.Verify()), "");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["training-create-charter"].Creates!).Id, Charter);
        Eq(CreatedCards.Resolve(AbilityCatalog.All["training-create-bowed-holding"].Creates!).Id, QuestionableResources);
        Eq(CreatedCards.Resolve(AbilityCatalog.All["training-create-terrain"].Creates!).Id, CreatedTerrain);
        Check(CreatedCards.IsTerrain(CardCatalog.Get(CreatedTerrain)) && !CreatedCards.IsTerrain(CardCatalog.Get(Charter)), "a Terrain is a Strategy with the Terrain keyword");
        Check(AbilityCatalog.All["training-create-bowed-holding"].Creates!.Describe().Contains("bowed"), "the description says bowed");
    }

    private static void HoldingsEnterAtHome()
    {
        var s = Table(Phase.Action);
        var maker = Put(s, HoldingMaker, 0, Zone.Home);
        var records = Put(s, PublicRecords, 0, Zone.Home);
        var e = Engine(s);
        var gold = MaxGold(e, 0); var rng = e.State.RngState; var hands = e.State.Players.Select(p => p.Hand.Count + p.FateDeck.Count + p.DynastyDeck.Count).ToArray();
        Accept(e, Offer(e, 0, "training-create-charter", maker.Id));
        var charter = Created(e, Charter);
        Eq(charter.Id, "created-0001"); Eq(charter.Zone, Zone.Home); Eq(charter.OwnerId, 0); Check(charter.FaceUp && !charter.Bowed && charter.ProvinceIndex is null && charter.Printed is null);
        Eq(MaxGold(e, 0), gold + 2, "an unbowed created Charter produces 2 Gold like any Holding");
        Eq(e.State.RngState, rng); Check(hands.SequenceEqual(e.State.Players.Select(p => p.Hand.Count + p.FateDeck.Count + p.DynastyDeck.Count)), "no card is drawn, shuffled or paid for");
        Check(e.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created Charter at home." && x.CardId == charter.Id), "the log");
        foreach (var viewer in new[] { 0, 1 })
        {
            var home = e.GetView(viewer).Players[0].Home;
            Check(home.Any(c => c.Id == records.Id) && home.Any(c => c.Id == charter.Id && c.Definition is { Name: "Charter", CreatedOnly: true, GoldProduction: 2 }), $"player {viewer} sees it at home alongside the other Holding");
        }
        // The rest of a creating action ("Lose 2 Honor") is done once the Holding exists.
        var tax = Table(Phase.Action);
        var taxer = Put(tax, HoldingMaker, 0, Zone.Home);
        var et = Engine(tax);
        var honor = et.State.Players[0].Honor;
        Accept(et, Offer(et, 0, "training-create-charter-tax", taxer.Id));
        Check(Created(et, Charter).Zone == Zone.Home && et.State.Players[0].Honor == honor - 2, "the Holding is created and the Honor lost");
        // Bowed as the text says: a bowed Holding produces nothing until it straightens.
        var t = Table(Phase.Action);
        var maker2 = Put(t, HoldingMaker, 0, Zone.Home);
        var f = Engine(t);
        var before = MaxGold(f, 0);
        Accept(f, Offer(f, 0, "training-create-bowed-holding", maker2.Id));
        var resources = Created(f, QuestionableResources);
        Check(resources.Zone == Zone.Home && resources.Bowed && resources.FaceUp, "at home, bowed");
        Eq(MaxGold(f, 0), before, "a bowed Holding produces no Gold");
        Check(f.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created Questionable Resources (bowed) at home."), "the log says bowed");
        Check(f.GetView(1).Players[0].Home.Single(c => c.Id == resources.Id).Bowed, "public: bowed");
        var straight = f.State; straight.Cards[resources.Id].Bowed = false;
        Eq(MaxGold(Engine(straight), 0), before + 3, "straightened, it produces its 3 Gold");
        // The Defender's Holding is the Defender's own.
        var u = Table(Phase.Action, actor: 1);
        var maker3 = Put(u, HoldingMaker, 1, Zone.Home);
        var g = Engine(u);
        Accept(g, Offer(g, 1, "training-create-charter", maker3.Id));
        Eq(Created(g, Charter).OwnerId, 1);
        Check(g.GetView(0).Players[1].Home.Any(c => c.Definition?.Id == Charter), "at the other player's home");
    }

    private static void HoldingsAreNeverPiled()
    {
        var s = Table(Phase.Action);
        var maker = Put(s, HoldingMaker, 0, Zone.Home);
        var e = Engine(s);
        Accept(e, Offer(e, 0, "training-create-charter", maker.Id));
        var charter = Created(e, Charter);
        var st = e.State; st.Cards[charter.Id].Zone = Zone.DynastyDiscard; st.PriorityPlayerId = 1;
        var gone = Engine(st);
        Accept(gone, Cmd(gone, "pass"));
        Eq(gone.State.Cards[charter.Id].Zone, Zone.RemovedFromGame, "a created Holding in a discard pile is removed from the game");
        Check(!gone.GetView(0).Players[0].Discard.Any(c => c.Id == charter.Id) && gone.GetView(0).Players[0].RemovedFromGame.Any(c => c.Id == charter.Id), "in the removed pile, in no discard pile");
    }

    private static (MatchEngine Engine, CardInstance Maker, CardInstance Second) BattleWithMakers()
    {
        var s = Table(Phase.Battle);
        var maker = Put(s, HoldingMaker, 0, Zone.Battlefield, province: 0);
        var second = Put(s, HoldingMaker, 0, Zone.Battlefield, province: 0);
        Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return (Engine(s), maker, second);
    }

    private static void TerrainEntersAtTheBattlefield()
    {
        var (e, maker, second) = BattleWithMakers();
        var rng = e.State.RngState;
        Accept(e, Offer(e, 0, "training-create-terrain", maker.Id));
        var terrain = Created(e, CreatedTerrain);
        Eq(terrain.Zone, Zone.Battlefield); Eq(terrain.ProvinceIndex, (int?)0); Eq(terrain.OwnerId, 0); Check(terrain.FaceUp && !terrain.Bowed); Eq(terrain.Id, "created-0001"); Eq(e.State.RngState, rng);
        Check(e.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created Training created Terrain at the battlefield." && x.CardId == terrain.Id), string.Join("|", e.State.Events.Where(x => x.Type == "cardCreated").Select(x => x.Message)));
        Check(e.HasKeyword(terrain.Id, "Terrain"), "it is a Terrain to every rule");
        foreach (var viewer in new[] { 0, 1 }) Check(e.GetView(viewer).Players[0].Army.Any(c => c.Id == terrain.Id && c.Definition is { CreatedOnly: true, Name: "Training created Terrain" }), $"player {viewer} sees it at the battlefield");
        // One Terrain per battlefield: a second creation is disabled with the reason, and so is playing a Terrain from a hand.
        var st = e.State; st.PriorityPlayerId = 0;
        var again = Engine(st);
        var offer = Offers(again, 0, "training-create-terrain").Single(o => o.Command?.CardId == second.Id || o.CardId == second.Id);
        Check(!offer.Enabled && offer.Reason!.Contains("already has a Terrain in play"), Describe(again, 0, "training-create-terrain"));
        Reject(again, new GameCommand("useAbility", 0, again.Version, CardId: second.Id, AbilityId: "training-create-terrain"));
        var hand = Put(st, "training-terrain", 0, Zone.Hand);
        var play = Engine(st).QueryActions(0).Single(o => o.Type == "playStrategy" && o.CardId == hand.Id);
        Check(!play.Enabled && play.Reason!.Contains("already has a Terrain in play"), "a created Terrain blocks a played one");
        // Removed from the game when the battle ends, like every created card.
        while (e.State.PendingChoice is not null) Accept(e, Cmd(e, "declineReaction"));
        while (e.State.Phase == Phase.Battle && e.State.CurrentBattleProvince is not null) Accept(e, Cmd(e, "pass"));
        Eq(e.State.Cards[terrain.Id].Zone, Zone.RemovedFromGame, "removed from the game when the battle ends");
        Check(!e.GetView(0).Players[0].Discard.Any(c => c.Id == terrain.Id) && e.GetView(0).Players[0].RemovedFromGame.Any(c => c.Id == terrain.Id), "in the removed pile, in no discard pile");
        // The Defender's Personality creates it on the Defender's side of the same battlefield.
        var s2 = Table(Phase.Battle);
        var theirs = Put(s2, HoldingMaker, 1, Zone.Battlefield, province: 0); Put(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        s2.PriorityPlayerId = 1;
        var d = Engine(s2);
        Accept(d, Offer(d, 1, "training-create-terrain", theirs.Id));
        Eq(Created(d, CreatedTerrain).OwnerId, 1);
    }

    private static void NoBattlefieldNoTerrain()
    {
        var s = Table(Phase.Action);
        var maker = Put(s, HoldingMaker, 0, Zone.Home);
        var e = Engine(s);
        var offer = Offers(e, 0, "training-create-terrain-open").Single();
        Check(!offer.Enabled && offer.Reason!.Contains("current battlefield"), "disabled with the reason: " + Describe(e, 0, "training-create-terrain-open"));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: maker.Id, AbilityId: "training-create-terrain-open"));
        Check(CreatedOf(e, CreatedTerrain).Length == 0, "nothing was created");
        var t = Table(Phase.BattleSelection);
        t.CurrentBattleProvince = null; t.UnresolvedBattlefields = [0];
        Put(t, HoldingMaker, 0, Zone.Home);
        var f = Engine(t);
        Check(!Offers(f, 0, "training-create-terrain").Any(o => o.Enabled) && !Offers(f, 0, "training-create-terrain-open").Any(o => o.Enabled), "not while a battle is only being selected");
        // An Open action is an Action-phase action, so there is no way to take it while a battle is under way.
        var battle = Table(Phase.Battle);
        Put(battle, HoldingMaker, 0, Zone.Battlefield, province: 0); Put(battle, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!Offers(Engine(battle), 0, "training-create-terrain-open").Any(o => o.Enabled), "an Open action is not a Battle action");
    }

    private static void SpecRules()
    {
        var scans = CreatedCards.Scans.Append(new CardDefinition("fake-created-strategy", "Fake created Strategy", CardType.Strategy, CreatedOnly: true)).ToArray();
        string[] Problems(CreatedCardSpec spec) => CreatedCards.Verify([("training-create-terrain", spec)], scans);
        var bad = new (CreatedCardSpec Spec, string Expect)[] {
            (new(CardType.Strategy, ProxyId: CreatedTerrain) { EntersBowed = true }, "bowed"),
            (new(CardType.Personality, ProxyId: "oracle-l5r-9785") { EntersBowed = true }, "bowed"),
            (new(CardType.Holding, ProxyId: Charter, Count: 2), "only one Holding or Terrain"),
            (new(CardType.Strategy, ProxyId: CreatedTerrain, Count: 2), "only one Holding or Terrain"),
            (new(CardType.Strategy, ProxyId: "fake-created-strategy"), "cannot create yet"),
        };
        foreach (var (spec, expect) in bad)
        {
            var problems = Problems(spec);
            Check(problems.Length > 0 && problems.All(p => p.StartsWith("training-create-terrain:")) && problems.Any(p => p.Contains(expect)), $"{spec.Describe()}: expected a problem naming '{expect}'; got [{string.Join(" | ", problems)}]");
        }
        Eq(string.Join("; ", Problems(new(CardType.Holding, ProxyId: QuestionableResources) { EntersBowed = true })), "");
        Eq(string.Join("; ", Problems(new(CardType.Strategy, ProxyId: CreatedTerrain))), "");
    }
}
