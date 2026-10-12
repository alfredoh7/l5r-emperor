using System.Reflection;
using L5R.Core;
using static RegressionAssertions;

// Issue #859: Tamori Kazushige's Open and Togashi Korimi's Interrupt (src/L5R.Core/Wave859.cs), on the published cards.
internal static class Wave859Tests
{
    private const string Kazushige = "oracle-l5r-7777", Korimi = "oracle-l5r-8545", LionSamurai = "training-lion-samurai";

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false) => Batch336Support.Add(s, def, owner, zone, bowed);
    private static void Check(bool ok, string message) => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message) => Batch336Support.Eq(actual, expected, message);
    private static void Call(MatchEngine e, string name, params object[] args) => typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args);

    public static void Run(TestRunner runner) => runner.Run("wave-859", [
        ("Tamori Kazushige: on another player's turn his Open straightens a Personality of the active player's before the phase ends, and gains 2 Honor unless it was assigned to attack", KazushigeOpen),
        ("Togashi Korimi: after he enters play he may bow to draw a card", KorimiInterrupt),
    ]);

    private static void KazushigeOpen()
    {
        Check((CardCatalog.Get(Kazushige).Abilities ?? []).Contains(Wave859Abilities.KazushigeStraighten), "the card carries the Open");
        var s = Batch336Support.Table(Phase.Action, 1);
        var k = Add(s, Kazushige, 0, Zone.Home);
        var target = Add(s, LionSamurai, 1, Zone.Home, bowed: true);
        s.PriorityPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        var honor = Batch336Support.Honor(e, 0);
        Accept(e, Batch336Support.Offer(e, 0, Wave859Abilities.KazushigeStraighten, k.Id, target.Id));
        Check(e.State.MisakiWatches.Any(w => w.Kind == Wave859Abilities.KazushigeKind && w.TargetId == target.Id), "a watch is set");
        // Before the phase ends he straightens; before the turn ends, not assigned to attack: 2 Honor to the acting player.
        var phaseEnd = MatchEngine.FromTestState(e.State);
        Call(phaseEnd, "ResolveShadowsEmbraceBatch1PhaseEnd", Phase.AttackDeclaration);
        Check(!phaseEnd.State.Cards[target.Id].Bowed, "he is straightened");
        Call(phaseEnd, "ResolveShadowsEmbraceBatch1TurnEnd");
        Eq(Batch336Support.Honor(phaseEnd, 0), honor + 2, "2 Honor for the player who acted");
        // Assigned to attack: no Honor.
        var attacked = e.State; attacked.Cards[target.Id].AssignedToAttackTurn = attacked.TurnNumber;
        var ae = MatchEngine.FromTestState(attacked);
        var before = Batch336Support.Honor(ae, 0);
        Call(ae, "ResolveShadowsEmbraceBatch1TurnEnd");
        Eq(Batch336Support.Honor(ae, 0), before, "no Honor when he was assigned to attack");
        // Only on another player's turn.
        var own = Batch336Support.Table(Phase.Action, 0);
        var k2 = Add(own, Kazushige, 0, Zone.Home); Add(own, LionSamurai, 1, Zone.Home);
        Check(!Batch336Support.CanUse(MatchEngine.FromTestState(own), 0, Wave859Abilities.KazushigeStraighten, k2.Id), "not on your own turn");
    }

    private static void KorimiInterrupt()
    {
        Check((CardCatalog.Get(Korimi).Abilities ?? []).Contains(Wave859Abilities.KorimiDraw), "the card carries the Interrupt");
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        var k = Add(s, Korimi, 0, Zone.Province);
        s.Players[0].Provinces[0].CardId = k.Id; k.ProvinceIndex = 0; k.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var recruit = e.QueryActions(0).First(a => a.Type == "purchase" && a.Command!.CardId == k.Id && a.Enabled);
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, recruit.Command!);
        Eq(e.State.PendingChoice?.Type, "entryReaction", "entering play opens the Reaction window");
        Accept(e, Batch336Support.Enabled(e, 0, Wave859Abilities.KorimiDraw, k.Id).Command!);
        Eq(e.State.Players[0].Hand.Count, hand + 1, "a card was drawn");
        Check(e.State.Cards[k.Id].Bowed, "Korimi bowed as the cost");
    }
}
