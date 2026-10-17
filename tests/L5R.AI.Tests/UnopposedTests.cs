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
        ("Issue #1003: an unopposed attacker short of the Province may also cut its strength (aimed at the attacked Province) and move Personalities to the battle; it never does either once the Province falls", ProvinceAndMoves),
        ("Issue #1003: it moves its own Personalities to another battlefield only when their Force changes that battle's outcome, and plays Honor for itself or from the opponent; optional bows and destroys stay barred", OtherBattleHonorAndBows),
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

    private static string? PickCommand(MatchView view, ActionOption use, out GameCommand? command, BotContext? memory = null)
    {
        command = TrainingOpponent.Choose(view, [BattlePolicyTests.Pass(view), use], memory ?? new BotContext()).Command;
        return command is { } c ? c.AbilityId ?? c.Type : "none";
    }

    private static void ProvinceAndMoves()
    {
        var lower = BattlePolicyTests.Use(Unopposed(6, 7, true), "genji-province-lower") with { Provinces = [0, 1] };
        var short6 = Unopposed(6, 7, botAttacks: true);
        Check(PickCommand(short6, lower, out var cut) == "genji-province-lower" && cut!.Province == 0 && cut.TargetPlayerId == 1, "short of the Province: it lowers the strength of the Province under attack (the opponent's, province 0)");
        Check(PickCommand(Unopposed(8, 7, true), lower, out _) == "pass", "once the Province falls it stops");
        var raise = BattlePolicyTests.Use(short6, "genji-province-raise") with { Provinces = [0, 1] };
        Check(PickCommand(short6, raise, out _) == "pass", "raising a Province's strength is never useful to the attacker");
        var move = BattlePolicyTests.Use(short6, "help-shadows-move", "own-main");
        Check(PickCommand(short6, move, out _) == "help-shadows-move", "short of the Province: moving a Personality to the battle adds Force and is played");
        Check(PickCommand(Unopposed(8, 7, true), move, out _) == "pass", "once the Province falls the move to this battle is not needed");
    }

    private static void OtherBattleHonorAndBows()
    {
        // Seat 0 holds 9 + a spare 3 (12) at province 0 against nothing, Strength 7: margin 4. At province 1 it has 3 against 5.
        CardView[] spare = [BattlePolicyTests.Unit("own-spare", 0, 3), BattlePolicyTests.Unit("own-b", 0, 3, province: 1)];
        MatchView Table(int enemyAtOther)
        {
            var v = BattlePolicyTests.Battle(9, 0, 7, true, spare);
            return v with { Players = [v.Players[0], v.Players[1] with { Army = [BattlePolicyTests.Unit("enemy-b", 1, enemyAtOther, province: 1)] }] };
        }
        var mover = AbilityCatalog.All.First(p => p.Value.Move is { Movers: MoveSpec.Source, Destination: MoveSpec.AnyOther } && p.Value.Target == "none").Key;
        ActionOption Move(MatchView v) => BattlePolicyTests.Use(v, mover, "own-spare") with { Movement = new MoveDestinationView(1, ["own-spare"], false), Provinces = [1] };
        Check(PickCommand(Table(5), Move(Table(5)), out _) == mover, "3 more Force at province 1 lifts 3 against 5 to 6 against 5: the move changes that battle and the spare Force can be spared here");
        Check(PickCommand(Table(20), Move(Table(20)), out _) == "pass", "against 20 Force it changes nothing there: it stays");
        // Honor: for itself, or taken from the opponent, short of the Province or not; and the bow that optionally hits an opposing card is not played.
        var short6 = Unopposed(6, 7, botAttacks: true);
        var gain = AbilityCatalog.All.First(p => p.Value.Timing == "Battle" && p.Value.Target == "none" && AbilityIntent.Of(p.Value) is { HonorGain: true, IsReaction: false, Side: not TargetSide.Enemy }
            && !System.Text.RegularExpressions.Regex.IsMatch(p.Value.Label ?? "", "not|if|unless", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).Key;
        Check(PickCommand(short6, BattlePolicyTests.Use(short6, gain), out _) == gain, "short of the Province: Honor for itself is still worth taking");
        // The opponent's Personality is at another battlefield, so the battle being fought is still unopposed.
        var elsewhere = short6 with { Players = [short6.Players[0], short6.Players[1] with { Army = [BattlePolicyTests.Unit("enemy-b", 1, 5, province: 1)] }] };
        Check(PickCommand(elsewhere, BattlePolicyTests.Use(elsewhere, "training-honor-strike", "own-main", "enemy-b"), out _) == "training-honor-strike", "short of the Province: taking Honor from the opponent is worth it");
        Check(PickCommand(short6, BattlePolicyTests.Use(short6, "phantom-visage-bow", "own-main", "enemy-main"), out _) == "pass", "an optional bow of an opposing card is not played against nothing");
        Check(PickCommand(short6, BattlePolicyTests.Use(short6, "unparalleled-sword-attack"), out _) == "pass", "nor is a Melee Attack");
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
