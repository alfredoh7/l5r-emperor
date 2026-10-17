using L5R.AI;
using L5R.Core;

// Issue #997 (Mendon's playtest note): the bot still fired Ranged and Melee Attacks in battles nobody opposed. Unopposed on defense it passes; unopposed on offense it acts only to add the
// Force the Province still needs and stops once it has it. The abilities are real catalogue entries: a Melee Attack and a "+2F" boost.
internal static class UnopposedTests
{
    private const string Attack = "unparalleled-sword-attack", Boost = "kunji-boost";

    public static readonly (string Name, Action Test)[] Tests =
    [
        ("Unopposed defender: every battle action (an Attack, a Force boost) is barred and the bot passes", DefenderPasses),
        ("Unopposed attacker short of the Province: it plays the Force boost it lacks and never the Attack; once the Force destroys the Province it passes", AttackerStopsWhenSufficient),
        ("Opposed battle: the gate does not apply (an enemy Personality, bowed or not, makes it opposed) and the old bot is unchanged when the flag is off", OpposedUnchanged),
    ];

    private static MatchView Unopposed(int own, int strength, bool botAttacks, bool enemyBowed = false)
    {
        var view = BattlePolicyTests.Battle(own, 0, strength, botAttacks);
        // The enemy has no unit here: either none at all, or one that is bowed (still a Personality, so still opposing).
        return view with { Players = [view.Players[0], view.Players[1] with { Army = enemyBowed ? [BattlePolicyTests.Unit("enemy-bowed", 1, 3, bowed: true)] : [] }] };
    }

    private static string Pick(MatchView view, BotContext? memory = null, params string[] abilities)
    {
        var options = new List<ActionOption> { BattlePolicyTests.Pass(view) };
        options.AddRange(abilities.Select(id => BattlePolicyTests.Use(view, id)));
        return TrainingOpponent.Choose(view, options.ToArray(), memory ?? new BotContext()).Command is { } c ? c.AbilityId ?? c.Type : "none";
    }

    private static void DefenderPasses()
    {
        var view = Unopposed(6, 7, botAttacks: false);
        Check(Pick(view, null, Attack) == "pass", "an unopposed defender does not fire an Attack");
        Check(Pick(view, null, Boost) == "pass", "nor does it boost Force for a battle nobody fights");
        Check(Pick(view, null, Attack, Boost) == "pass", "nor either of them together");
    }

    private static void AttackerStopsWhenSufficient()
    {
        var short6 = Unopposed(6, 7, botAttacks: true);
        Check(Pick(short6, null, Attack) == "pass", "an Attack adds no Force against nothing: it passes");
        Check(Pick(short6, null, Attack, Boost) == Boost, "short of the Province it plays the Force boost it lacks, not the Attack");
        var enough = Unopposed(8, 7, botAttacks: true);
        Check(Pick(enough, null, Attack, Boost) == "pass", "8 Force against Strength 7 already destroys the Province: it stops augmenting");
    }

    private static void OpposedUnchanged()
    {
        var opposed = BattlePolicyTests.Battle(6, 4, 7, botAttacks: true);
        Check(Pick(opposed, null, Attack) == Attack, "an opposed attacker still plays its Attack");
        var bowedEnemy = Unopposed(6, 7, botAttacks: false, enemyBowed: true);
        Check(Pick(bowedEnemy, null, Attack) == Attack, "a bowed enemy Personality still opposes: the gate leaves the action to the ordinary rules");
        var off = new BotContext { UseUnopposedGate = false };
        Check(Pick(Unopposed(6, 7, botAttacks: false), off, Attack) == Attack, "with the gate off the bot behaves as it did before");
    }

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
