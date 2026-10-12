using L5R.AI;
using L5R.Core;

// Issue #870: the bot weighs the Honor an ability costs or gains by its deck's Honor tier, and does not play an Attack that cannot destroy its target (Mendon's playtest note: a Unicorn bot lost 4 Honor to
// a Ranged Attack that destroyed nothing). Each rule is pinned on a small synthetic battle; the old behaviour stays measurable with BotContext.UseHonorAware = false.
internal static class HonorAwareTests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("Reading the label: 'Lose 4 Honor' is the player's own loss; a controller or another player losing Honor is not", SelfLossReading),
        ("A Paragon's Strength Ranged Attack that cannot destroy its target is not played by any deck; the old bot played it", FruitlessAttack),
        ("A killing Attack that costs 4 Honor is passed by a high-Honor deck, played by a low-Honor deck, and played by anyone when it swings the battle from losing to winning", KillingAttack),
        ("An Attack that cannot destroy is valued only for the rest of its action (Lion's Charge still gains Honor); a pure Attack that cannot destroy is passed", RestOfTheAction),
        ("A high-Honor deck prefers the action that gains Honor (the move home) to the Attack that loses it", GainBeatsLoss),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private const string AttackId = "paragon-strength-attack", MoveId = "paragon-strength-move", CharmId = "lions-charge-attack";

    /// <summary>The bot (seat 0) on a high-, in-between- or low-Honor deck: the Stronghold's starting Honor and the decklist's average Personal Honor decide the tier.</summary>
    private static MatchView Deck(MatchView view, int startingHonor) =>
        view with { Players = [view.Players[0] with { Stronghold = view.Players[0].Stronghold with { Definition = view.Players[0].Stronghold.Definition! with { StartingHonor = startingHonor } } }, view.Players[1]] };

    private static BotContext Bot(double average, bool honorAware = true) => new() { DeckAveragePersonalHonor = average, UseHonorAware = honorAware };
    private static BotContext High(bool honorAware = true) => Bot(4.0, honorAware);
    private static BotContext Low(bool honorAware = true) => Bot(1.0, honorAware);

    /// <summary>An offer of one of the Paragon's Strength abilities (performed by the Paragon "own-main", its card in the bot's hand).</summary>
    private static ActionOption Offer(MatchView v, string ability, string? target = null, string[]? targets = null) =>
        new("useAbility", ability, true, null, new("useAbility", 0, v.Version, CardId: "paragon-card", AbilityId: ability, PerformerId: "own-main", TargetId: target), "paragon-card", targets ?? (target is null ? null : [target]));

    private static string Chose(MatchView v, BotContext bot, params ActionOption[] options) =>
        TrainingOpponent.Choose(v, [.. options, BattlePolicyTests.Pass(v)], bot).Command is { } c ? c.AbilityId ?? c.Type : "none";

    private static MatchView Battle(int own, int enemy, int paragonForce, params int[] enemyForces)
    {
        var view = BattlePolicyTests.Battle(own, enemy, 5);
        var paragon = view.Players[0].Army[0] with { Force = paragonForce };
        var theirs = view.Players[1].Army.Concat(enemyForces.Select((f, i) => BattlePolicyTests.Unit("enemy-" + i, 1, f))).ToArray();
        return Deck(view with { Players = [view.Players[0] with { Army = [paragon, .. view.Players[0].Army.Skip(1)] }, view.Players[1] with { Army = theirs }] }, 6);
    }

    private static void SelfLossReading()
    {
        Check(AbilityIntent.Of(AttackId) is { SelfHonorLoss: true, SelfHonorLossAmount: 4 }, "A Paragon's Strength: 'Lose 4 Honor' is the user's");
        Check(AbilityIntent.Of(MoveId) is { SelfHonorLoss: false, HonorGain: true }, "its other half gains Honor and loses none");
        Check(!AbilityIntent.Of(CharmId).SelfHonorLoss && AbilityIntent.Of(CharmId).HonorGain, "Lion's Charge gains Honor");
        Check(AbilityIntent.SelfLoss("Choose your performing Personality: Gain 1 Honor. Lose 2 Honor") is (true, 2), "a later sentence counts");
        Check(!AbilityIntent.SelfLoss("Target a Personality: Dishonor him. His controller loses 2 Honor").Loses, "his controller loses Honor: the opponent's");
        Check(!AbilityIntent.SelfLoss("All players lose 3 Honor").Loses, "all players: not the user's alone");
        Check(!AbilityIntent.SelfLoss("Target a player: He loses 2 Honor").Loses, "a chosen player");
    }

    private static void FruitlessAttack()
    {
        // The Paragon has 3 Force, so the Attack reaches 3; the enemy's only unit has 5.
        var v = Battle(3, 5, 3);
        var attack = Offer(v, AttackId, "enemy-main", ["enemy-main"]);
        Check(Chose(v, High(honorAware: false), attack) == AttackId, "the old bot attacks (and loses 4 Honor for nothing)");
        foreach (var (name, bot) in new[] { ("high", High()), ("low", Low()) })
            Check(Chose(v, bot, attack) != AttackId, $"the {name}-Honor bot passes an Attack that destroys nothing and costs 4 Honor");
    }

    private static void KillingAttack()
    {
        // Own 3 against 2 + 5: the Attack (strength 3) can destroy the Force 2 unit, but 3 against 5 is still losing.
        var v = Battle(3, 5, 3, 2);
        var attack = Offer(v, AttackId, "enemy-0", ["enemy-0"]);
        Check(Chose(v, High(), attack) != AttackId, "a high-Honor deck does not pay 4 Honor for a kill that wins nothing");
        Check(Chose(v, Low(), attack) == AttackId, "a low-Honor deck barely minds the Honor and takes the kill");
        // Own 4 against 3 + 2: destroying the Force 3 unit leaves 2, a loss turned into a win.
        var swing = Battle(4, 2, 4, 3);
        var kill = Offer(swing, AttackId, "enemy-0", ["enemy-0"]);
        Check(!BattlePolicy.Assess(swing)!.Winning, "the battle is being lost");
        Check(Chose(swing, High(), kill) == AttackId, "an Attack that turns the battle from losing to winning is played even by a high-Honor deck");
    }

    private static void RestOfTheAction()
    {
        // Lion's Charge: a Ranged 2 Attack (4 with a Paragon) and Gain 1 Honor. Against a Force 6 unit it destroys nothing, but the Honor is still worth a high-Honor deck's action.
        var v = Battle(3, 6, 3);
        var charge = new ActionOption("useAbility", CharmId, true, null, new("useAbility", 0, v.Version, CardId: "own-main", AbilityId: CharmId, TargetId: "enemy-main"), "own-main", ["enemy-main"]);
        Check(Chose(v, High(), charge) == CharmId, "the Attack cannot destroy, but the Honor it gains still makes the action worth taking");
        // A pure Attack with nothing else to it, aimed at a unit it cannot destroy, is passed.
        var pure = AbilityCatalog.All.First(p => p.Value.Target == "attack" && BattlePolicy.AttackStrength(p.Key) is 1 && AbilityIntent.Of(p.Value) is { HonorGain: false, SelfHonorLoss: false, Draws: false }).Key;
        var pureOffer = new ActionOption("useAbility", pure, true, null, new("useAbility", 0, v.Version, CardId: "own-main", AbilityId: pure, TargetId: "enemy-main"), "own-main", ["enemy-main"]);
        Check(Chose(v, High(), pureOffer) != pure, "a pure Attack of strength 1 against a Force 6 unit is passed");
        Check(Chose(v, High(honorAware: false), pureOffer) == pure, "the old bot played it");
    }

    private static void GainBeatsLoss()
    {
        // A killing Attack that costs 4 Honor against sending the enemy home for 2 Honor: the high-Honor deck takes the gain.
        var v = Battle(3, 5, 3, 2);
        var attack = Offer(v, AttackId, "enemy-0", ["enemy-0"]);
        var move = Offer(v, MoveId, "enemy-main", ["enemy-main"]);
        Check(Chose(v, High(), attack, move) == MoveId, "the high-Honor deck sends the attacker home and gains Honor");
    }
}
