using System.Text.RegularExpressions;
using L5R.Core;

namespace L5R.AI;

// Issue #1003 (Mendon's follow-up to #997): what an attacker nobody opposes may still usefully do. Force modifiers (#997), and also: lower the attacked Province's strength, move Personalities to this
// battle, move Personalities to another battlefield when that changes the outcome there, gain Honor for itself, take Honor from the opponent. Melee and Ranged Attacks and cards that only
// optionally bow or destroy opposing cards stay barred: there is nothing to hit and nothing to be gained.
public static partial class TrainingOpponent
{
    private static readonly Regex LowersStrength = new(@"\bProvince\b[^.;:]*-\s?\d+\s+strength|\b(?:lower|reduce|decrease)\b[^.;:]*\bstrength\b", RegexOptions.IgnoreCase);

    private static int? AttackedProvince(Ctx x) => x.View.BattleStep?.BattlefieldProvince ?? x.View.CurrentBattleProvince;

    /// <summary>The ability gives a Province -N strength and can be aimed at the Province under attack.</summary>
    private static bool LowersAttackedProvince(Ctx x, ActionOption action, GameCommand command) =>
        action.Type == "useAbility" && command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)
        && LowersStrength.IsMatch(AbilityIntent.EffectText(ability.Label ?? "")) && AttackedProvince(x) is int province
        && (action.Provinces is null || action.Provinces.Contains(province));

    /// <summary>The Personalities the action moves, as far as the bot can tell now: its fixed movers plus the chosen target (or every offered target, when none is chosen yet).</summary>
    private static IEnumerable<CardView[]> MoverGroups(Ctx x, ActionOption action, GameCommand command)
    {
        var fixedMovers = (action.Movement?.FixedMoverIds ?? []).Select(x.Card).OfType<CardView>().ToArray();
        if (action.Movement is null || !action.Movement.TargetMoves) { yield return fixedMovers; yield break; }
        var targets = command.TargetId is { } chosen ? new[] { chosen } : action.TargetIds ?? [];
        foreach (var id in targets) if (x.Card(id) is { } target) yield return [.. fixedMovers, target];
    }

    /// <summary>The ability's own Move rule, when it moves only the bot's own cards (a move of an enemy Personality is a different thing).</summary>
    private static MoveSpec? OwnMove(ActionOption action, GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || ability.Move is not { } move) return null;
        var movesTarget = move.Movers is MoveSpec.Target or MoveSpec.SourceAndTarget or MoveSpec.PerformerAndTarget or MoveSpec.PerformingSlot;
        return movesTarget && ability.Target.StartsWith("enemy", StringComparison.Ordinal) ? null : move;
    }

    /// <summary>Moves the bot's own Personalities to the battle being fought: more Force where the Province still needs it.</summary>
    private static bool MovesToThisBattle(Ctx x, ActionOption action, GameCommand command) =>
        OwnMove(action, command) is { Destination: MoveSpec.CurrentBattlefield };

    /// <summary>
    /// Moves the bot's own Personalities to a different battlefield where their Force changes the outcome (Mendon's outcome ladder, <see cref="BattlePolicy.Odds.Tier"/>), without costing
    /// the battle being fought what it still needs.
    /// </summary>
    private static bool MovesToChangeAnotherBattle(Ctx x, ActionOption action, GameCommand command, BattlePolicy.Odds current)
    {
        if (OwnMove(action, command) is not { } move || move.Destination is not (MoveSpec.AdjacentUnresolved or MoveSpec.AnyUnresolved or MoveSpec.AnyOther or MoveSpec.AssignedThisTurn)) return false;
        var here = AttackedProvince(x);
        var destinations = (command.Province is int chosen ? new[] { chosen } : action.Provinces ?? []).Where(p => p != here).ToArray();
        foreach (var movers in MoverGroups(x, action, command))
        {
            if (movers.Length == 0 || movers.Any(m => m.OwnerId != x.Self.Id)) continue;
            var ids = movers.Select(m => m.Id).ToHashSet();
            var force = BattlePolicy.Force(x.Self.Army.Concat(x.Self.Home).Where(c => ids.Contains(c.Id) || c.AttachedTo is { } on && ids.Contains(on)));
            var leaving = BattlePolicy.Force(x.Self.Army.Where(c => c.ProvinceIndex == here && (ids.Contains(c.Id) || c.AttachedTo is { } on && ids.Contains(on))));
            if (leaving > 0 && !(current.Satisfied && leaving <= current.Margin)) continue; // pulling Force out of a battle that still needs it
            foreach (var destination in destinations)
            {
                var strength = x.Opponent.Provinces.FirstOrDefault(p => p.Index == destination && !p.Destroyed)?.Strength;
                var there = new BattlePolicy.Odds(true, BattlePolicy.Force(x.Self.Army.Where(c => c.ProvinceIndex == destination)), EnemyForceAt(x, destination), strength);
                if (there.Tier(force) > there.Tier()) return true;
            }
        }
        return false;
    }
}
