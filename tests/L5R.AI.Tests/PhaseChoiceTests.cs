using L5R.AI;
using L5R.Core;

// Issue #998 (Mendon's playtest note): Battle/Open cards. Soshi House Guard ("if it is a Combat Segment, bow him; if he is dishonorable, his controller loses 1 Honor") is the case
// the note names: in the Open phase it does nothing to an honorable Personality, and against a dishonorable one it is the weaker use of a card whose Battle use bows. The other
// abilities are real catalogue entries too.
internal static class PhaseChoiceTests
{
    private const string Soshi = "soshi-conditional-bow", Straighten = "negishi-straighten", Boost = "kunji-boost", HonorGuard = "asako-house-guard-force-honor";

    public static readonly (string Name, Action Test)[] Tests =
    [
        ("Soshi House Guard in the Open phase: against an honorable Personality it does nothing and is never used; against a dishonorable one it is held for the Combat Segment", SoshiOpen),
        ("Soshi House Guard in a battle: it bows an unbowed Personality, does nothing to a bowed honorable one (passes), and still takes a dishonorable bowed one's Honor", SoshiBattle),
        ("A straighten is used only on a bowed card, in either phase; a Force boost is held in the Open phase and used in the battle; an ability that also gains Honor is not held", OthersAndFlag),
        ("The reading is exact: a text with an effect the bot cannot judge is never called pointless, and an unknown target is left to the ordinary rules", Reading),
    ];

    private static MatchView Open(Action<CardView[]>? _ = null) => BattlePolicyTests.Battle(6, 4, 7) with { Phase = Phase.Action, CurrentBattleProvince = null };
    private static MatchView WithEnemy(MatchView v, Func<CardView, CardView> change) => v with { Players = [v.Players[0], v.Players[1] with { Army = v.Players[1].Army.Select(c => c.Id == "enemy-main" ? change(c) : c).ToArray() }] };
    private static MatchView WithOwn(MatchView v, Func<CardView, CardView> change) => v with { Players = [v.Players[0] with { Army = v.Players[0].Army.Select(c => c.Id == "own-main" ? change(c) : c).ToArray() }, v.Players[1]] };

    private static string Pick(MatchView view, string ability, string? target, BotContext? memory = null)
    {
        var options = new[] { BattlePolicyTests.Pass(view), BattlePolicyTests.Use(view, ability, "own-main", target) };
        return TrainingOpponent.Choose(view, options, memory ?? new BotContext()).Command is { } c ? c.AbilityId ?? c.Type : "none";
    }

    private static void SoshiOpen()
    {
        var honorable = Open();
        Check(Pick(honorable, Soshi, "enemy-main") == "pass", "Open phase, honorable target: the Honor loss needs a dishonorable one and the bow needs a Combat Segment, so it does nothing");
        var dishonorable = WithEnemy(honorable, c => c with { Dishonorable = true });
        Check(Pick(dishonorable, Soshi, "enemy-main") == "pass", "Open phase, dishonorable target: 1 Honor is the weaker use; the card is held for the battle");
        Check(Pick(honorable, Soshi, "enemy-main", new BotContext { UsePhaseChoice = false }) == Soshi, "with the rule off the bot spends it anyway (the old behaviour)");
    }

    private static void SoshiBattle()
    {
        var battle = BattlePolicyTests.Battle(6, 4, 7);
        Check(Pick(battle, Soshi, "enemy-main") == Soshi, "in the battle it bows an unbowed opposed Personality");
        var bowed = WithEnemy(battle, c => c with { Bowed = true });
        Check(Pick(bowed, Soshi, "enemy-main") == "pass", "already bowed and honorable: nothing to do");
        var bowedDishonorable = WithEnemy(battle, c => c with { Bowed = true, Dishonorable = true });
        Check(Pick(bowedDishonorable, Soshi, "enemy-main") == Soshi, "already bowed but dishonorable: the Honor loss still happens");
    }

    private static void OthersAndFlag()
    {
        var open = Open();
        Check(Pick(open, Straighten, "own-main") == "pass", "Open phase: straightening a card that is already straight does nothing");
        var bowedOwn = WithOwn(open, c => c with { Bowed = true });
        Check(Pick(bowedOwn, Straighten, "own-main") == Straighten, "Open phase: straightening a bowed Personality is worth doing before the attack");
        var battleStraight = BattlePolicyTests.Battle(6, 4, 7);
        Check(Pick(battleStraight, Straighten, "own-main") == "pass", "in a battle too, straightening a card that is already straight does nothing");
        Check(Pick(WithOwn(battleStraight, c => c with { Bowed = true }), Straighten, "own-main") == Straighten, "in a battle, straightening a bowed Personality is used");
        Check(Pick(open, Boost, "own-main") == "pass", "Open phase: +2F matters only in a battle, so it is held");
        var battle = BattlePolicyTests.Battle(6, 4, 7);
        Check(Pick(battle, Boost, "own-main") == Boost, "in the battle the same +2F is used");
        Check(Pick(open, HonorGuard, "own-main") == HonorGuard, "Open phase: an ability that also gains Honor has a payoff outside combat and is not held");
    }

    private static void Reading()
    {
        var upright = BattlePolicyTests.Unit("t", 1, 3);
        var bowed = BattlePolicyTests.Unit("t", 1, 3, bowed: true);
        Check(TrainingOpponent.DoesNothingHere("Bow him", bowed, true), "a plain bow on a bowed card is a no-op");
        Check(!TrainingOpponent.DoesNothingHere("Bow him", upright, true), "a plain bow on an unbowed card is not");
        Check(!TrainingOpponent.DoesNothingHere("Bow him and draw a card", bowed, true), "an effect the bot cannot judge (it also draws) is never called pointless");
        Check(!TrainingOpponent.DoesNothingHere("if it is a combat segment, bow him; if he is dishonorable, his controller loses 1 Honor", null, true), "an unknown target is left to the ordinary rules");
        Check(TrainingOpponent.DoesNothingHere("if it is a combat segment, bow him; if he is dishonorable, his controller loses 1 Honor", upright, true), "the Soshi text in the Open phase on an honorable card");
        Check(!TrainingOpponent.DoesNothingHere("if it is a combat segment, bow him; if he is dishonorable, his controller loses 1 Honor", upright, false), "the same text in a Combat Segment on an unbowed card bows it");
        Check(TrainingOpponent.DoesNothingHere("Straighten him", upright, true) && !TrainingOpponent.DoesNothingHere("Straighten him", bowed, true), "straighten matters only on a bowed card");
        Check(TrainingOpponent.HeldForBattle("Give him +2F", AbilityIntent.Of(AbilityCatalog.All[Boost])) && !TrainingOpponent.HeldForBattle("Gain 1 Honor", AbilityIntent.Of(AbilityCatalog.All[HonorGuard])), "a Force change is held; Honor is not");
    }

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
