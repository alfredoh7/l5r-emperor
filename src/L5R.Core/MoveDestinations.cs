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
    private MoveDestinationView? MovementPreview(string abilityId, AbilityDefinition ability, CardInstance? source, string? performer)
    {
        // Hawado's target pays the removal cost; only Hawado moves. Her existing effect and validation are unchanged.
        if(abilityId=="hawado-move-battlefield")return new(1-_state.ActivePlayerId,source is null?[]:[source.Id],false);
        if(ability.Move is not {ChoosesBattlefield:true} move)return null;
        var fixedId=move.Movers is MoveSpec.Source or MoveSpec.SourceAndTarget?source?.Id:
            move.Movers is MoveSpec.Performer or MoveSpec.PerformerAndTarget?performer:null;
        return new(1-_state.ActivePlayerId,fixedId is null?[]:[fixedId],
            move.Movers is MoveSpec.Target or MoveSpec.SourceAndTarget or MoveSpec.PerformerAndTarget);
    }

    // The battlefields next to `current` (Mendon's ruling, issue #336): a battlefield is identified by the DEFENDER's province it is fought over, so
    // adjacency follows the DEFENDER's province line -- never the line of whichever player happens to own the card or take the action. Destroyed
    // provinces slide their neighbours together (AdjacentProvinces). The one place adjacency between battlefields is decided.
    private IEnumerable<int> AdjacentBattlefields(int current) => AdjacentProvinces(1 - _state.ActivePlayerId, current);

    // The battlefields a chosen destination may name right now (empty for a destination that needs no choice, and when no battle is
    // under way). A battlefield is identified by the DEFENDER's province it is fought over, so adjacency is the defender's province line
    // (destroyed provinces slide together, AdjacentProvinces), and "unresolved" is a battlefield still to be fought this Attack Phase.
    private int[] MoveDestinations(MoveSpec move)
    {
        if (!move.ChoosesBattlefield || _state.CurrentBattleProvince is not int current) return [];
        var unresolved = _state.UnresolvedBattlefields;
        return move.Destination == MoveSpec.AdjacentUnresolved
            ? AdjacentBattlefields(current).Where(unresolved.Contains).ToArray()
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
    // last (the arrival of the target is the one a "moved an enemy Personality to this battlefield" Reaction watches for). EACH movement is its
    // own effect and negatable on its own (Mendon's ruling, issue #336): a negation of one mover's movement (Tamori Tamoko, a Champion of Thunder's
    // "all") leaves the others' movement alone unless an effect negates theirs too, and a negation of the whole action stops all of them (the
    // deferred action then never reaches here). Returns whether the TARGET moved (false when its move was negated, or there was none).
    private bool ExecuteMove(GameCommand command, AbilityDefinition ability, CardInstance? source, CardInstance? target)
    {
        var move = ability.Move!;
        var battlefield = move.Destination == MoveSpec.CurrentBattlefield ? _state.CurrentBattleProvince : command.Province;
        if (move.Movers is MoveSpec.Source or MoveSpec.SourceAndTarget) MoveByRule(source!, move, battlefield);
        if (move.Movers is MoveSpec.Performer or MoveSpec.PerformerAndTarget) MoveByRule(Card(command.PerformerId), move, battlefield);
        return move.Movers is MoveSpec.Target or MoveSpec.SourceAndTarget or MoveSpec.PerformerAndTarget && MoveByRule(target!, move, battlefield);
    }

    private bool MoveByRule(CardInstance card, MoveSpec move, int? battlefield)
    {
        if (move.Destination == MoveSpec.Home)
        {
            var before = card.Zone;
            MoveHome(card, false);
            return before != card.Zone;
        }
        return TryMoveToBattlefield(card, battlefield!.Value);
    }
}
