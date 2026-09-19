namespace L5R.Core;

/// <summary>A move whose destination is a rule of the ability, not a per-card special case (issue #336): WHO moves and WHERE to.
/// Movers is "target", "source", "source-and-target" (Sea Troll Predator moves the Personality it targets and itself, together), "performer" or
/// "performer-and-target" (the card an ability names as its performer, for "choose your performing X ... move them both").
/// Destination is "home", "current-battlefield" (both need no choice), or one of the two rules a player chooses between:
/// "adjacent-unresolved-battlefield" (an unresolved battlefield next to the current one) and "unresolved-battlefield" (any other
/// unresolved battlefield). For a chosen destination the engine lists the legal battlefields (ActionOption.Provinces) and the command
/// names one in GameCommand.Province -- the same province indices the attack-assignment flow already draws with arrows.</summary>
public sealed record MoveSpec(string Movers, string Destination)
{
    public const string Target = "target", Source = "source", SourceAndTarget = "source-and-target", Performer = "performer", PerformerAndTarget = "performer-and-target";
    public const string Home = "home", CurrentBattlefield = "current-battlefield", AdjacentUnresolved = "adjacent-unresolved-battlefield", AnyUnresolved = "unresolved-battlefield";
    public bool ChoosesBattlefield => Destination is AdjacentUnresolved or AnyUnresolved;
}

public sealed partial class MatchEngine
{
    // The battlefields a chosen destination may name right now (empty for a destination that needs no choice, and when no battle is
    // under way). A battlefield is identified by the DEFENDER's province it is fought over, so adjacency is the defender's province line
    // (destroyed provinces slide together, AdjacentProvinces), and "unresolved" is a battlefield still to be fought this Attack Phase.
    private int[] MoveDestinations(MoveSpec move)
    {
        if (!move.ChoosesBattlefield || _state.CurrentBattleProvince is not int current) return [];
        var unresolved = _state.UnresolvedBattlefields;
        return move.Destination == MoveSpec.AdjacentUnresolved
            ? AdjacentProvinces(1 - _state.ActivePlayerId, current).Where(unresolved.Contains).ToArray()
            : unresolved.Where(p => p != current).ToArray();
    }

    // ValidateAbility's check: a chosen destination must be one of the listed battlefields, and every other kind names none.
    private void ValidateMove(GameCommand command, AbilityDefinition ability)
    {
        if (ability.Move is not { } move) return;
        if (move.ChoosesBattlefield)
            Require(command.Province is int province && MoveDestinations(move).Contains(province), move.Destination == MoveSpec.AdjacentUnresolved
                ? "Choose an adjacent unresolved battlefield." : "Choose another unresolved battlefield.");
        else Require(command.Province is null, "This move names no battlefield.");
    }

    // The effect of a move: every mover goes to the destination, the ability's own source (or its named performer) first and its target
    // last (the arrival of the target is the one a "moved an enemy Personality to this battlefield" Reaction watches for). The source and
    // the performer move themselves -- nothing a reaction negates -- while the target's movement is an effect on it, so a negation of its
    // movement (Tamori Tamoko) applies to it alone and the others still move. Returns whether the TARGET moved (false when its move was
    // negated, or there was none).
    private bool ExecuteMove(GameCommand command, AbilityDefinition ability, CardInstance? source, CardInstance? target)
    {
        var move = ability.Move!;
        var battlefield = move.Destination == MoveSpec.CurrentBattlefield ? _state.CurrentBattleProvince : command.Province;
        if (move.Movers is MoveSpec.Source or MoveSpec.SourceAndTarget) MoveByRule(source!, move, battlefield, negatable: false);
        if (move.Movers is MoveSpec.Performer or MoveSpec.PerformerAndTarget) MoveByRule(Card(command.PerformerId), move, battlefield, negatable: false);
        return move.Movers is MoveSpec.Target or MoveSpec.SourceAndTarget or MoveSpec.PerformerAndTarget && MoveByRule(target!, move, battlefield, negatable: true);
    }

    private bool MoveByRule(CardInstance card, MoveSpec move, int? battlefield, bool negatable)
    {
        if (move.Destination == MoveSpec.Home)
        {
            var before = card.Zone;
            MoveHome(card, false);
            return before != card.Zone;
        }
        if (negatable) return TryMoveToBattlefield(card, battlefield!.Value);
        MoveToBattlefield(card, battlefield!.Value);
        Emit("unitMovedToBattlefield", $"{Definition(card).Name}'s unit moved to the battlefield at province {battlefield.Value + 1}.", card.OwnerId, card.Id);
        return true;
    }
}
