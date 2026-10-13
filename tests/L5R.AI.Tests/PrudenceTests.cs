using L5R.AI;
using L5R.Core;

// Issue #902 (Mendon's bot notes, 2026-10-12): general prudence, with nothing that names a card. See TrainingOpponent.Prudence.cs and docs/BOT_LOGIC.md.
internal static class PrudenceTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Prudence: the intent reads an effect on both sides, a created Personality and a claim on the first action of a battle from the printed text", IntentReadings),
        ("Prudence: an effect on both sides favors the bot only when its cheapest unit is worth 3 or more less than the opponent's, or it has none and the opponent has one", MutualRule),
        ("Prudence: the bot passes instead of an effect on both sides with nothing of its own to give up on equal terms, and plays it with no Personality of its own against one of the opponent's", MutualDecision),
        ("Prudence: a bow is aimed at an unbowed card; when every legal target is already bowed the bot keeps the action", BowedTargets),
        ("Prudence: a claim on the first action of a battle is never made by the Defender, nor by an Attacker without units on both sides", FirstActionClaim),
        ("Prudence: in the first turns the Action phase does not spend Gold on cards from the hand", EarlyEconomy),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static readonly BotContext Prudent = new();
    /// <summary>Prudence alone: the Honor an effect costs is weighed separately (issue #870) and would hide it for a high-Honor deck.</summary>
    private static readonly BotContext PrudentOnly = new() { UseHonorAware = false };
    private static readonly BotContext Prior = new() { UsePrudence = false };

    private static void IntentReadings()
    {
        Check(AbilityIntent.Of("mysterious-deaths-destroy").Mutual, "Each player ... Destroy all targets is an effect on both sides");
        Check(AbilityIntent.Of("heavily-engaged-bow").Mutual, "so is Each player ... Bow all targets");
        Check(AbilityIntent.Of("overlooked-palace-create").CreatesPersonality, "create a ... Personality");
        Check(AbilityIntent.Of("sneak-attack-claim").ClaimsFirstOpportunity, "has the first opportunity");
        Check(!AbilityIntent.Of("training-bow-at-battle").Mutual, "an ordinary bow is not");
    }

    private static void MutualRule()
    {
        Check(!TrainingOpponent.MutualFavorsBot([], []), "nobody has a unit: nothing to gain");
        Check(!TrainingOpponent.MutualFavorsBot([5], [5]), "equal cheapest units: no");
        Check(!TrainingOpponent.MutualFavorsBot([5], [7]), "2 less is not enough");
        Check(TrainingOpponent.MutualFavorsBot([5], [8]), "3 less is");
        Check(TrainingOpponent.MutualFavorsBot([9, 3], [8, 12]), "only the cheapest of each side counts");
        Check(TrainingOpponent.MutualFavorsBot([], [4]), "no Personality of its own and the opponent has one: yes");
        Check(!TrainingOpponent.MutualFavorsBot([4], []), "the opponent has none: no");
        Check(TrainingOpponent.MutualFavorsBot([6], [6], extraOwn: 0), "a created Personality worth 0 makes it worth playing against a unit worth 6");
        Check(!TrainingOpponent.MutualFavorsBot([6], [2], extraOwn: 0), "but not against one worth 2");
    }

    /// <summary>An Action phase late enough that the economy rule is over, with the given Personalities at home on each side.</summary>
    private static MatchView ActionTable(CardView[] own, CardView[] theirs)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        return view with
        {
            Phase = Phase.Action, PendingChoice = null, ActivePlayerId = 0, PriorityPlayerId = 0,
            Players = [view.Players[0] with { TurnsTaken = 6, Home = own, Army = [] }, view.Players[1] with { Home = theirs, Army = [] }]
        };
    }

    private static CardView Home(string id, int owner, int cost) =>
        new(id, BattlePolicyTests.Person() with { GoldCost = cost }, owner, Zone.Home, true, false, null, null, 3);

    private static void MutualDecision()
    {
        var mutual = "mysterious-deaths-destroy";
        ActionOption Play(MatchView v) => new("useAbility", mutual, true, null, new("useAbility", 0, v.Version, CardId: "hand-card", AbilityId: mutual), "hand-card");
        var even = ActionTable([Home("a", 0, 5)], [Home("b", 1, 5)]);
        Check(TrainingOpponent.Choose(even, [BattlePolicyTests.Pass(even), Play(even)], PrudentOnly).Command?.Type == "pass", "units of equal worth on both sides: it passes");
        var cheaper = ActionTable([Home("a", 0, 2)], [Home("b", 1, 6)]);
        var cheaperDecision = TrainingOpponent.Choose(cheaper, [BattlePolicyTests.Pass(cheaper), Play(cheaper)], PrudentOnly);
        Check(cheaperDecision.Command?.AbilityId == mutual, "its cheapest unit is worth 4 less than the opponent's: it plays it: " + cheaperDecision.Command?.Type + " " + cheaperDecision.Reason + " units " + string.Join(",", TrainingOpponent.UnitValues(cheaper.Players[0])) + " vs " + string.Join(",", TrainingOpponent.UnitValues(cheaper.Players[1])));
        var bare = ActionTable([], [Home("b", 1, 5)]);
        Check(TrainingOpponent.Choose(bare, [BattlePolicyTests.Pass(bare), Play(bare)], PrudentOnly).Command?.AbilityId == mutual, "no unit of its own against the opponent's: it plays it");
        var empty = ActionTable([], []);
        Check(TrainingOpponent.Choose(empty, [BattlePolicyTests.Pass(empty), Play(empty)], PrudentOnly).Command?.Type == "pass", "nobody has a unit: it passes");
    }

    private static void BowedTargets()
    {
        static bool BowCost(AbilityDefinition a) => (a.Label ?? "").Split(':') is { Length: > 1 } parts && parts[0].Contains("Bow", StringComparison.OrdinalIgnoreCase);
        var bow = AbilityCatalog.All.Where(p => !p.Key.StartsWith("training-", StringComparison.Ordinal) && p.Value.Timing == "Battle" && p.Value.Target == "enemy-personality" && AbilityIntent.Of(p.Value) is { Rank: EffectRank.Bow, IsReaction: false } && !BowCost(p.Value) && !p.Value.IssuesDuel)
            .OrderBy(p => p.Key, StringComparer.Ordinal).First().Key;
        var up = BattlePolicyTests.Battle(8, 4, 7, enemyExtra: [BattlePolicyTests.Unit("enemy-up", 1, 2, 0)]);
        var down = BattlePolicyTests.Battle(8, 4, 7, enemyExtra: [BattlePolicyTests.Unit("enemy-down", 1, 2, 0, bowed: true)]);
        var both = BattlePolicyTests.Battle(8, 4, 7, enemyExtra: [BattlePolicyTests.Unit("enemy-down", 1, 2, 0, bowed: true)]);
        Check(TrainingOpponent.Choose(up, [BattlePolicyTests.Pass(up), BattlePolicyTests.Use(up, bow, target: "enemy-up", targets: ["enemy-up"])], Prudent).Command?.AbilityId == bow, "an unbowed target is bowed");
        Check(TrainingOpponent.Choose(down, [BattlePolicyTests.Pass(down), BattlePolicyTests.Use(down, bow, target: "enemy-down", targets: ["enemy-down"])], Prudent).Command?.Type == "pass", "a bowed target is not bowed again: the action is kept");
        var choice = TrainingOpponent.Choose(both, [BattlePolicyTests.Pass(both), BattlePolicyTests.Use(both, bow, target: "enemy-down", targets: ["enemy-down", "enemy-main"])], Prudent).Command;
        Check(choice?.AbilityId == bow && choice.TargetId == "enemy-main", "with a bowed and an unbowed target it picks the unbowed one: " + choice?.TargetId);
    }

    private static void FirstActionClaim()
    {
        var claim = "sneak-attack-claim";
        ActionOption Window(MatchView v) => new("useAbility", claim, true, null, new("useAbility", 0, v.Version, CardId: "hand-card", AbilityId: claim), "hand-card");
        ActionOption Decline(MatchView v) => new("declineReaction", "Pass reaction", true, null, new("declineReaction", 0, v.Version));
        var defending = BattlePolicyTests.Battle(6, 4, 7, botAttacks: false) with { PendingChoice = new PendingChoice("engageReaction", 0, null, "Engage", null) };
        Check(TrainingOpponent.Choose(defending, [Decline(defending), Window(defending)], Prudent).Command?.Type == "declineReaction", "as the Defender it never claims");
        var alone = BattlePolicyTests.Battle(6, 4, 7) with { PendingChoice = new PendingChoice("engageReaction", 0, null, "Engage", null) };
        alone = alone with { Players = [alone.Players[0], alone.Players[1] with { Army = [] }] };
        Check(TrainingOpponent.Choose(alone, [Decline(alone), Window(alone)], Prudent).Command?.Type == "declineReaction", "an Attacker facing no units does not claim");
        var empty = BattlePolicyTests.Battle(6, 4, 7) with { PendingChoice = new PendingChoice("engageReaction", 0, null, "Engage", null) };
        empty = empty with { Players = [empty.Players[0] with { Army = [] }, empty.Players[1]] };
        Check(TrainingOpponent.Choose(empty, [Decline(empty), Window(empty)], Prudent).Command?.Type == "declineReaction", "an Attacker with no unit of its own at the battle does not claim");
    }

    private static void EarlyEconomy()
    {
        var def = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.GoldCost > 0);
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var self = view.Players[0] with { TurnsTaken = 1, Home = [] };
        var withBuy = self with { Provinces = self.Provinces.Select((p, i) => i == 0 ? p with { Card = new CardView("p-card", def, 0, Zone.Province, true, false, null, null, def.Force) } : p).ToArray() };
        var action = view with { Phase = Phase.Action, PendingChoice = null, ActivePlayerId = 0, PriorityPlayerId = 0, Players = [withBuy, view.Players[1]] };
        var spell = new CardView("spell", CardCatalog.Cards.Values.First(d => d.Type == CardType.Spell) with { GoldCost = 2 }, 0, Zone.Hand, true, false, null, null, 0);
        action = action with { Players = [action.Players[0] with { Hand = [spell], HandCount = 1 }, action.Players[1]] };
        var equip = new ActionOption("equip", "Equip", true, null, new("equip", 0, action.Version, "spell", TargetId: "own-main"), "spell", ["own-main"], Gold: new GoldQuote(2, "Printed", "", null, []));
        var host = BattlePolicyTests.Unit("own-main", 0, 4);
        action = action with { Players = [action.Players[0] with { Home = [host with { Zone = Zone.Home, ProvinceIndex = null }] }, action.Players[1]] };
        Check(TrainingOpponent.Choose(action, [BattlePolicyTests.Pass(action), equip], Prudent).Command?.Type == "pass", "turn one, something to buy next: the Gold is kept");
        var late = action with { Players = [action.Players[0] with { TurnsTaken = 6 }, action.Players[1]] };
        Check(TrainingOpponent.Choose(late, [BattlePolicyTests.Pass(late), equip], Prudent).Command?.Type == "equip", "later in the game the Gold is spent as before");
    }
}
