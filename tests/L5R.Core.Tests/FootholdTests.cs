using L5R.Core;
using static RegressionAssertions;

// Issue #766: Foothold of the Mad (src/L5R.Core/Foothold.cs, wave stronghold-wave-48), proved on the published card with Mendon's 2026-10-08 ruling: the going-first Honor of 39 then 0, no UI trace,
// the Honor-loss immunity, the Fallen discount, Madness tokens as -1F tokens, and the two Limited bow abilities. Player 0 is the Foothold player unless a test says otherwise.
internal static class FootholdTests
{
    private const string Foothold = "oracle-l5r-10562", Lion = "training-lion-stronghold";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    public static void Run(TestRunner runner) => runner.Run("foothold-of-the-mad", [
        ("Published as printed: a Stronghold with Gold 0, Province Strength 0, Starting Honor 0, its Effect and its two Limited abilities", PublishedShape),
        ("Going first: a Foothold player counts as 39 Honor, then every Foothold player's Honor is 0, silently; two Foothold players fall back to the tie break", GoingFirst),
        ("No Honor loss from cards you own: an enemy card's loss and a rulebook loss still land, the other player is unaffected", NoLossFromOwnCards),
        ("Fallen Personalities join the Foothold player for 2 less Gold, never below 0; others and the opponent's Fallen pay in full", FallenDiscount),
        ("Madness tokens are -1F tokens while a Foothold is in play (either player's): each token, Personalities and Followers, stacking; none without one or on other cards", MadnessTokens),
        ("Limited, bow: a target card gets a Madness token (a -1F one), once, as the Stronghold's own action", MadnessAbility),
        ("Limited, bow: Holdings enter play paying 2 less this turn only, Holdings only, the user's only; the mark survives a save", HoldingDiscount),
    ]);

    private static MatchState Table(string? player0 = Foothold, string? player1 = null, Phase phase = Phase.Dynasty)
    {
        var s = Batch336Support.Table(phase, 0);
        if (player0 is not null) s.Cards[s.Players[0].StrongholdId].DefinitionId = player0;
        if (player1 is not null) s.Cards[s.Players[1].StrongholdId].DefinitionId = player1;
        return s;
    }

    private static CardInstance Variant(MatchState s, string id, int owner, Zone zone, int cost, params string[] keywords)
    {
        var card = Batch336Support.Add(s, "training-crane-samurai", owner, zone, province: zone == Zone.Province ? 0 : null);
        card.Printed = CardCatalog.Get("training-crane-samurai") with { Id = id, GoldCost = cost, Keywords = keywords };
        return card;
    }

    private static void PublishedShape()
    {
        var card = CardCatalog.Get(Foothold);
        Check(card.Type == CardType.Stronghold && card.GoldProduction == 0 && card.ProvinceStrength == 0 && card.StartingHonor == 0 && card.Effect == FootholdAbilities.Effect, "printed stats and Effect");
        Check(card.Abilities!.SequenceEqual([FootholdAbilities.MadnessAbility, FootholdAbilities.HoldingsAbility]), "its two Limited abilities");
        foreach (var id in card.Abilities!) Check(AbilityCatalog.All[id].Timing == "Limited", id + " is Limited");
    }

    private static void GoingFirst()
    {
        // Foothold against a Lion Stronghold (6 Honor): Foothold goes first on its 39, then starts at 0; the Lion player is untouched.
        var s = Table(Foothold, Lion);
        s.Players[0].Honor = 7; s.Players[1].Honor = 6;
        Eq(MatchEngine.FirstPlayerAtSetup(s, 1), 0, "a Foothold player goes first against a higher printed Honor");
        var swapped = Table(Lion, Foothold); swapped.Players[0].Honor = 8; swapped.Players[1].Honor = 5;
        Eq(MatchEngine.FirstPlayerAtSetup(swapped, 0), 1, "in either seat");
        MatchEngine.ApplyFootholdStartHonor(s);
        Eq(s.Players[0].Honor, 0, "the Foothold player's Honor is 0");
        Eq(s.Players[1].Honor, 6, "the other player's Honor is unchanged");
        // Two Foothold players: both count 39, so the tie break decides.
        var both = Table(Foothold, Foothold);
        Eq(MatchEngine.FirstPlayerAtSetup(both, 0), 0, "a tie goes to the tie break (0)");
        Eq(MatchEngine.FirstPlayerAtSetup(both, 1), 1, "a tie goes to the tie break (1)");
        // No Foothold: the higher Honor goes first, as before.
        var none = Table(Lion, Lion); none.Players[0].Honor = 5; none.Players[1].Honor = 7;
        Eq(MatchEngine.FirstPlayerAtSetup(none, 0), 1, "without a Foothold the higher Honor goes first");
        // Silent: no event is emitted by the start step, and a built match shows no Foothold change.
        var e = MatchEngine.FromTestState(Table(Foothold, Lion));
        Check(e.State.Events.All(ev => !ev.Message.Contains("Foothold", StringComparison.OrdinalIgnoreCase)), "nothing about it is reported");
    }

    private static void NoLossFromOwnCards()
    {
        var s = Table();
        var own = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemy = Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Home);
        s.Players[0].Honor = 10; s.Players[1].Honor = 10;
        var e = MatchEngine.FromTestState(s);
        e.GainHonorForTest(0, -3, "own card", own.Id);
        Eq(e.State.Players[0].Honor, 10, "no loss from a card the Foothold player owns");
        e.GainHonorForTest(0, -2, "enemy card", enemy.Id);
        Eq(e.State.Players[0].Honor, 8, "a loss from the enemy's card lands");
        e.GainHonorForTest(0, -1, "rulebook", null);
        Eq(e.State.Players[0].Honor, 7, "a loss with no source card lands");
        e.GainHonorForTest(0, 2, "own gain", own.Id);
        Eq(e.State.Players[0].Honor, 9, "gains are untouched");
        e.GainHonorForTest(1, -3, "own card", enemy.Id);
        Eq(e.State.Players[1].Honor, 7, "the other player loses Honor from his own cards as usual");
    }

    private static int Price(MatchState s, string cardId) => MatchEngine.FromTestState(s).QuotePurchaseForTest(s.Cards[cardId].OwnerId, cardId);

    private static void FallenDiscount()
    {
        MatchState Fixture(bool foothold, string keyword, int cost, int owner = 0)
        {
            var s = foothold ? Table() : Table(null);
            var card = Variant(s, "fx-fallen", owner, Zone.Province, cost, keyword);
            s.Players[owner].Provinces[0].CardId = card.Id; card.FaceUp = true; card.ProvinceIndex = 0;
            s.ActivePlayerId = owner; s.PriorityPlayerId = owner;
            return s;
        }
        string Id(MatchState s) => s.Cards.Values.Single(c => c.Printed?.Id == "fx-fallen").Id;
        var with = Fixture(true, "Fallen", 5); var without = Fixture(false, "Fallen", 5);
        Eq(Price(without, Id(without)), 5, "the fixture's printed price");
        Eq(Price(with, Id(with)), 3, "a Fallen Personality costs the Foothold player 2 less");
        Eq(Price(Fixture(true, "Fallen", 1), Id(Fixture(true, "Fallen", 1))), 0, "never below 0");
        var plain = Fixture(true, "Samurai", 5);
        Eq(Price(plain, Id(plain)), 5, "a Personality that is not Fallen pays in full");
        var enemy = Table(Foothold, null); var enemyCard = Variant(enemy, "fx-fallen", 1, Zone.Province, 5, "Fallen");
        enemy.Players[1].Provinces[0].CardId = enemyCard.Id; enemyCard.FaceUp = true; enemyCard.ProvinceIndex = 0; enemy.ActivePlayerId = 1; enemy.PriorityPlayerId = 1;
        Eq(Price(enemy, enemyCard.Id), 5, "the opponent's Fallen Personality is not discounted by the Foothold player's Stronghold");
    }

    private static CardInstance Token(MatchEngine e, string cardId) => e.State.Cards[cardId];

    private static void MadnessTokens()
    {
        // A Personality at home with a printed Force: the Madness tokens come from the engine's own token creation.
        var s0 = Table();
        var person = Batch336Support.Add(s0, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s0);
        var before = e.CardForceForTest(person.Id);
        e.MadnessForTest(person.Id); e.MadnessForTest(person.Id);
        Eq(e.CardForceForTest(person.Id), Math.Max(0, before - 2), "two Madness tokens are -2F");
        Check(Token(e, person.Id).Tokens.Count(t => t.Type == "Madness" && t.Stat == "Force" && t.Amount == -1) == 2, "each is a -1F Madness token");
        var s4 = Table(); var host = Batch336Support.Add(s4, "training-crane-samurai", 0, Zone.Home);
        var follower = Batch336Support.Add(s4, "training-follower", 0, Zone.Attached, parent: host.Id);
        var e4 = MatchEngine.FromTestState(s4); var followerBefore = e4.CardForceForTest(follower.Id);
        e4.MadnessForTest(follower.Id);
        Eq(e4.CardForceForTest(follower.Id), Math.Max(0, followerBefore - 1), "a Follower has -1F for its Madness token too");
        // The rule is global: the opponent's Foothold applies to the Foothold-less player's cards.
        var s1 = Table(Lion, Foothold); var other = Batch336Support.Add(s1, "training-crane-samurai", 0, Zone.Home);
        var e1 = MatchEngine.FromTestState(s1); var base1 = e1.CardForceForTest(other.Id);
        e1.MadnessForTest(other.Id);
        Eq(e1.CardForceForTest(other.Id), Math.Max(0, base1 - 1), "either player's Foothold applies to every Personality");
        // Without a Foothold the token carries no penalty.
        var s2 = Table(null); var third = Batch336Support.Add(s2, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2); var base2 = e2.CardForceForTest(third.Id);
        e2.MadnessForTest(third.Id);
        Eq(e2.CardForceForTest(third.Id), base2, "no Foothold, no penalty");
        Check(e2.State.Cards[third.Id].Tokens.Single().Stat is null, "the token is a plain Madness token");
        // A card that is neither a Personality nor a Follower gets a plain token even with a Foothold in play.
        var s3 = Table(); var holding = Batch336Support.Add(s3, "training-marketplace", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3); e3.MadnessForTest(holding.Id);
        Check(e3.State.Cards[holding.Id].Tokens.Single().Stat is null, "a Holding's Madness token has no Force penalty");
    }

    private static void MadnessAbility()
    {
        var s = Table(Foothold, null, Phase.Action);
        var target = Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var stronghold = e.State.Players[0].StrongholdId;
        var option = Batch336Support.Options(e, 0, FootholdAbilities.MadnessAbility, stronghold).FirstOrDefault(o => o.Enabled);
        Check(option is not null, "the ability is offered on the Stronghold");
        var before = e.CardForceForTest(target.Id);
        var chosen = Batch336Support.Offer(e, 0, FootholdAbilities.MadnessAbility, stronghold, target.Id);
        Accept(e, chosen);
        Check(e.State.Cards[stronghold].Bowed, "the Stronghold bowed as the cost");
        Eq(e.State.Cards[target.Id].Tokens.Count(t => t.Type == "Madness"), 1, "the target has a Madness token");
        Eq(e.CardForceForTest(target.Id), Math.Max(0, before - 1), "and it is -1F");
        Check(!Batch336Support.CanUse(e, 0, FootholdAbilities.MadnessAbility, stronghold) && !Batch336Support.CanUse(e, 0, FootholdAbilities.HoldingsAbility, stronghold), "a bowed Stronghold offers neither Limited ability");
    }

    private static void HoldingDiscount()
    {
        var s = Table(Foothold, null, Phase.Action);
        var e = MatchEngine.FromTestState(s);
        var stronghold = e.State.Players[0].StrongholdId;
        Accept(e, Batch336Support.Enabled(e, 0, FootholdAbilities.HoldingsAbility, stronghold).Command!);
        Check(e.State.Cards[stronghold].Bowed, "the Stronghold bowed as the cost");
        var saved = System.Text.Json.JsonSerializer.Deserialize<MatchState>(System.Text.Json.JsonSerializer.Serialize(e.State))!;
        Eq(saved.FootholdHoldingDiscounts![0], saved.TurnNumber, "the mark is saved with the turn it was made");
        // Holdings only, this turn only, the user's only.
        int Quote(MatchState st, string kind, int owner)
        {
            var card = Variant(st, kind == "holding" ? "fx-holding" : "fx-person", owner, Zone.Province, 5);
            if (kind == "holding") card.Printed = CardCatalog.Get("training-marketplace") with { Id = "fx-holding", GoldCost = 5 };
            st.Players[owner].Provinces[0].CardId = card.Id; card.FaceUp = true; card.ProvinceIndex = 0;
            return MatchEngine.FromTestState(st).QuotePurchaseForTest(owner, card.Id);
        }
        Eq(Quote(saved, "holding", 0), 3, "a Holding costs the user 2 less this turn");
        var saved2 = System.Text.Json.JsonSerializer.Deserialize<MatchState>(System.Text.Json.JsonSerializer.Serialize(e.State))!;
        Eq(Quote(saved2, "person", 0), 5, "a Personality does not");
        var saved3 = System.Text.Json.JsonSerializer.Deserialize<MatchState>(System.Text.Json.JsonSerializer.Serialize(e.State))!; saved3.TurnNumber += 1;
        Eq(Quote(saved3, "holding", 0), 5, "the turn after, the Holding costs its price again");
        var saved4 = System.Text.Json.JsonSerializer.Deserialize<MatchState>(System.Text.Json.JsonSerializer.Serialize(e.State))!;
        Eq(Quote(saved4, "holding", 1), 5, "the opponent's Holding is not discounted");
    }
}
