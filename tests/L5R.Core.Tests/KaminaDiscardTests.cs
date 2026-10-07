using L5R.Core;
using static RegressionAssertions;

// Akodo Kamina (issue #691): "Tactical Open, bow: Give a target Personality +4F. You may discard a card to ignore the cost of bowing Kamina."
internal static class KaminaDiscardTests
{
    private const string Kamina = "oracle-l5r-224", Boost = "kamina-boost";
    public static void Run(TestRunner runner) => runner.Run("kamina-discard", [
        ("Kamina's action is offered as 'Bow Kamina' or 'Discard a card', one discard choice per card of the hand, each readable by name", Offered),
        ("Paying with the bow bows Kamina and gives +4F", BowPays),
        ("Paying with a discard ignores the bow: the chosen hand card is discarded, Kamina stays unbowed and the target gets +4F", DiscardPays),
        ("The action is still once a turn however it was paid", OncePerTurn),
        ("A missing, wrong or extra choice is refused and changes nothing", Refused),
        ("An empty hand lists the discard choice disabled with its reason while the bow stays available", EmptyHand)
    ]);
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone)
    {
        var c = new CardInstance { Id = $"kd-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }
    private static (MatchEngine Engine, CardInstance Kamina, CardInstance Ally, CardInstance[] Hand) Setup(int handSize = 2)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var kamina = Add(s, Kamina, 0, Zone.Home); var ally = Add(s, "training-crane-samurai", 0, Zone.Home);
        var hand = Enumerable.Range(0, handSize).Select(_ => Add(s, "oracle-l5r-1450", 0, Zone.Hand)).ToArray();
        return (MatchEngine.FromTestState(s), kamina, ally, hand);
    }
    private static GameCommand Use(MatchEngine e, CardInstance kamina, CardInstance ally, string? costOption, params string[] cards) =>
        new("useAbility", 0, e.Version, CardId: kamina.Id, TargetId: ally.Id, AbilityId: Boost, CostOption: costOption, CardIds: cards.Length == 0 ? null : cards);
    private static bool HasBoost(CardInstance c) => c.Modifiers.Any(m => m.Stat == "Force" && m.Amount == 4);
    private static void Rejects(MatchEngine e, GameCommand command, string why) => Reject(e, command);

    private static void Offered()
    {
        var (e, kamina, _, hand) = Setup();
        var offers = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.AlternativeCost?.AbilityId == Boost).ToArray();
        Check(offers.Length == 3, "one bow and two discard choices; got " + offers.Length + ": " + string.Join("; ", offers.Select(o => o.Label)));
        Check(offers.All(o => o.Enabled && o.AlternativeCost is not null), "all enabled, each an alternative cost");
        Check(offers[0].AlternativeCost!.Id == "bow" && offers[0].AlternativeCost!.Label == "Bow Kamina", "the bow comes first: " + offers[0].AlternativeCost!.Label);
        Check(offers.Skip(1).All(o => o.AlternativeCost!.Id == "discard" && o.AlternativeCost.Label.StartsWith("Discard a card: ", StringComparison.Ordinal)), "the discards say they discard a card and which");
        Check(offers.Skip(1).Select(o => o.Command!.CardIds!.Single()).OrderBy(x => x).SequenceEqual(hand.Select(h => h.Id).OrderBy(x => x)), "one per hand card");
        Check(offers.Skip(1).All(o => o.Command!.CostOption == "discard") && offers[0].Command!.CardIds is null, "the bow names no card");
        Check(!e.State.Cards[kamina.Id].Bowed, "listing pays nothing");
    }

    private static void BowPays()
    {
        var (e, kamina, ally, hand) = Setup();
        Accept(e, Use(e, kamina, ally, "bow"));
        Check(e.State.Cards[kamina.Id].Bowed, "Kamina bowed");
        Check(HasBoost(e.State.Cards[ally.Id]), "+4F");
        Check(hand.All(h => e.State.Players[0].Hand.Contains(h.Id)), "nothing discarded");
    }

    private static void DiscardPays()
    {
        var (e, kamina, ally, hand) = Setup();
        Accept(e, Use(e, kamina, ally, "discard", hand[1].Id));
        Check(!e.State.Cards[kamina.Id].Bowed, "the bow was ignored");
        Check(HasBoost(e.State.Cards[ally.Id]), "+4F");
        Check(e.State.Cards[hand[1].Id].Zone == Zone.FateDiscard && !e.State.Players[0].Hand.Contains(hand[1].Id), "the chosen card was discarded");
        Check(e.State.Players[0].Hand.Contains(hand[0].Id), "the other card stayed");
    }

    private static void OncePerTurn()
    {
        var (e, kamina, ally, hand) = Setup();
        Accept(e, Use(e, kamina, ally, "discard", hand[0].Id));
        Rejects(e, Use(e, kamina, ally, "discard", hand[1].Id), "a second use in the same turn");
    }

    private static void Refused()
    {
        var (e, kamina, ally, hand) = Setup();
        Rejects(e, Use(e, kamina, ally, null), "no way to pay named");
        Rejects(e, Use(e, kamina, ally, "discard"), "a discard without a card");
        Rejects(e, Use(e, kamina, ally, "discard", hand[0].Id, hand[1].Id), "two cards");
        Rejects(e, Use(e, kamina, ally, "discard", ally.Id), "a card that is not in the hand");
        Rejects(e, Use(e, kamina, ally, "bow", hand[0].Id), "a card named on the bow option");
        Rejects(e, Use(e, kamina, ally, "pay"), "an unknown option");
    }

    private static void EmptyHand()
    {
        var (e, kamina, ally, _) = Setup(handSize: 0);
        var offers = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.AlternativeCost?.AbilityId == Boost).ToArray();
        Check(offers.Length == 2, "the bow and one disabled discard; got " + string.Join("; ", offers.Select(o => o.Label)));
        var bow = offers.Single(o => o.AlternativeCost!.Id == "bow"); var discard = offers.Single(o => o.AlternativeCost!.Id == "discard");
        Check(bow.Enabled && !discard.Enabled && discard.Reason is { Length: > 0 }, "bow enabled; discard disabled with a reason: " + discard.Reason);
        Accept(e, Use(e, kamina, ally, "bow"));
        Check(e.State.Cards[kamina.Id].Bowed && HasBoost(e.State.Cards[ally.Id]), "the bow still works");
    }
}
