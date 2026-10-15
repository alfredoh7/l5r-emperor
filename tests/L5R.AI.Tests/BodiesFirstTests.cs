using L5R.AI;
using L5R.Core;

// Issue #963 (bot loop, round 3): the early-economy rule for the whole game while the bot is short of Personalities. See TrainingOpponent.Prudence.cs (BodiesFirst).
internal static class BodiesFirstTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Bodies first: late in the game a bot short of Personalities with one it can pay for keeps its Gold instead of equipping; with enough Personalities, or the policy off, it equips as before", Decision),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static MatchView Table(int ownPersonalities, bool somethingToBuy)
    {
        var def = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.GoldCost > 0 && d.GoldCost <= 2);
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var self = view.Players[0] with { TurnsTaken = 6 };
        if (somethingToBuy) self = self with { Provinces = self.Provinces.Select((p, i) => i == 0 ? p with { Card = new CardView("p-card", def, 0, Zone.Province, true, false, null, null, def.Force) } : p).ToArray() };
        else self = self with { Provinces = self.Provinces.Select(p => p with { Card = null }).ToArray() };
        var spell = new CardView("spell", CardCatalog.Cards.Values.First(d => d.Type == CardType.Spell) with { GoldCost = 2 }, 0, Zone.Hand, true, false, null, null, 0);
        var home = Enumerable.Range(0, ownPersonalities).Select(i => new CardView("own-" + i, BattlePolicyTests.Person(), 0, Zone.Home, true, false, null, null, 4)).ToArray();
        // Gold capacity: the stronghold's production is what pays for the Personality in the Province.
        return view with { Phase = Phase.Action, PendingChoice = null, ActivePlayerId = 0, PriorityPlayerId = 0, Players = [self with { Hand = [spell], HandCount = 1, Home = home }, view.Players[1] with { Home = [] }] };
    }

    private static void Decision()
    {
        var on = new BotContext { UseBodiesFirst = true };
        var off = new BotContext();
        string? Choose(MatchView v, BotContext c)
        {
            var equip = new ActionOption("equip", "Equip", true, null, new("equip", 0, v.Version, "spell", TargetId: "own-0"), "spell", ["own-0"], Gold: new GoldQuote(2, "Printed", "", null, []));
            return TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v), equip], c).Command?.Type;
        }
        var short1 = Table(1, somethingToBuy: true);
        Check(Choose(short1, on) == "pass", "one Personality, a recruit waiting: the Gold is kept for the Dynasty phase");
        Check(Choose(short1, off) == "equip", "policy off: unchanged behaviour, it equips");
        Check(Choose(Table(4, somethingToBuy: true), on) == "equip", "four Personalities: enough bodies, it equips as before");
        Check(Choose(Table(1, somethingToBuy: false), on) == "equip", "nothing to recruit: the Gold is spent as before");
    }
}
