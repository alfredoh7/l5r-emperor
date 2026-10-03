using L5R.Core;
using static Support337;
using static RegressionAssertions;

internal static class HisanoGuideTests
{
    private const string Hisano = "oracle-l5r-4464", Guide = "oracle-l5r-10020";

    public static void Run(TestRunner runner) => runner.Run("hisano-guide", [
        ("Hisano and the Guide have their reviewed printed definitions; the Guide is created-only", PublishedShape),
        ("Hisano creates her Guide once per game inside her entry Reaction window", CreatesOnEntry),
        ("The Guide moves an attacking enemy Personality home, not a defender", MovesAttacker)
    ]);

    private static void Check(bool condition, string message) => Expect(condition, message);

    private static void PublishedShape()
    {
        var hisano = CardCatalog.Get(Hisano);
        var guide = CardCatalog.Get(Guide);
        Eq(hisano.Type, CardType.Personality);
        Eq(hisano.Abilities!.Single(), HisanoGuideAbilities.Create);
        Eq(guide.Type, CardType.Personality);
        Eq(guide.Clan, "Mantis");
        Eq(guide.Force, 2);
        Eq(guide.Chi, 2);
        Eq(guide.PersonalHonor, 3);
        Eq(guide.Abilities!.Single(), HisanoGuideAbilities.Move);
        Check(guide.CreatedOnly && !hisano.CreatedOnly, "only the Guide is created-only");
        Check(new HashSet<string>(guide.Keywords!).SetEquals(["Unique", "Fox", "Mantis Clan", "Nonhuman", "Spirit"]), "the Guide's printed keywords");
        Check(guide.Text.Contains("May not be included in decks.") && guide.Text.Contains("Move him home."), "the Guide's complete text");
        Check(!CardCatalog.PlayableFromDecks.All(c => c.Id == Guide), "the Guide cannot enter a deck");
        Check(ArtShipped(Hisano) && ArtShipped(Guide), "both card faces are shipped");
        Eq(CreatedCards.Resolve(AbilityCatalog.All[HisanoGuideAbilities.Create].Creates!).Id, Guide);
        var manifest = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves/personality-wave-117.txt"));
        Check(manifest.Contains(Hisano) && manifest.Contains(Guide), "both ids are pinned");
    }

    private static void CreatesOnEntry()
    {
        var state = MatchEngine.CreateEconomyScenario().State;
        state.Players[0].Clan = "Mantis";
        var province = state.Cards[state.Players[0].Provinces[0].CardId!];
        province.DefinitionId = Hisano;
        province.FaceUp = true;
        for (var i = 0; i < 6; i++) Put(state, "training-marketplace", 0, Zone.Home);
        var engine = Engine(state);
        var quote = engine.QuotePurchase(0, province.Id);
        Check(quote.Error is null && quote.Payments.Length > 0, quote.Error ?? "Hisano must be affordable");
        Accept(engine, new GameCommand("purchase", 0, engine.Version, CardId: province.Id, SourceIds: quote.Payments[0].SourceIds));
        Check(engine.State.PendingChoice is { Type: "proclaim" or "entryReaction" }, "Hisano has an entry window");
        Accept(engine, new GameCommand("useAbility", 0, engine.Version, CardId: province.Id, AbilityId: HisanoGuideAbilities.Create));
        var guide = engine.State.Cards.Values.Single(c => c.DefinitionId == Guide);
        Check(guide.Zone == Zone.Home && guide.OwnerId == 0 && guide.Id.StartsWith("created-", StringComparison.Ordinal), "Guide enters at home as a created Personality");
        Check(engine.State.PendingChoice is { Type: "proclaim" or "entryReaction" }, "the creator's window remains open");
        Reject(engine, new GameCommand("useAbility", 0, engine.Version, CardId: province.Id, AbilityId: HisanoGuideAbilities.Create));
    }

    private static void MovesAttacker()
    {
        var state = Table(Phase.Battle, actor: 1);
        state.PriorityPlayerId = 0;
        var guide = Put(state, Guide, 0, Zone.Battlefield, province: 0);
        var attacker = Put(state, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var defender = Put(state, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var engine = Engine(state);
        Check(Offers(engine, 0, HisanoGuideAbilities.Move).Any(a => a.Enabled && a.TargetIds!.Contains(attacker.Id)), "the attacking enemy is a target");
        Check(!Offers(engine, 0, HisanoGuideAbilities.Move).Any(a => a.TargetIds?.Contains(defender.Id) == true), "a defender is not a target");
        Accept(engine, Offer(engine, 0, HisanoGuideAbilities.Move, guide.Id, attacker.Id));
        Eq(engine.State.Cards[attacker.Id].Zone, Zone.Home);
    }
}
