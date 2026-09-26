namespace L5R.Core;

/// <summary>Kinds of movement a lock refuses.</summary>
[Flags]
public enum MovementKinds
{
    None = 0, Home = 1, ToBattlefield = 2, Assign = 4,
    // Emperor Edition Stronghold pass (issue #486), batch 3 (The Khan's Estate: "He may not be assigned to attack this turn"): assignment to an ATTACKING army only -- he may still be assigned to defend.
    AssignAttack = 8
}

/// <summary>"He may not be assigned, and will not move, to battlefields this turn" (A Quiet Transaction) and "negate his (future) movement" (Strength of the Tsunami): the
/// kinds of movement a card's effects and assignment may not perform until the end of the turn numbered <see cref="Turn"/> (issue #415).</summary>
public sealed record MovementLock(string Source, int Turn, MovementKinds Kinds,
    // Moksha Patam (issue #453 batch 5): "While this card is at the current battlefield, negate his movement to it" -- the lock holds only while the card
    // WhileCardAtBattlefield stands at the battlefield Battlefield, which must also be the current one (MatchEngine.MovementLockHolds). Null: unconditional.
    string? WhileCardAtBattlefield = null, int? Battlefield = null,
    // Shinjo Junpei (Forgotten Legacy pass, issue #467 batch 2): "negate his NEXT movement" -- a one-shot lock, spent the first time it negates a movement
    // (MovementLockNegates), and otherwise lapsing at the end of the turn like every other lock.
    bool NextOnly = false);

// Movement locks (issue #415, Mendon's ruling on Strength of the Tsunami: "negate his (future) movement" means the movement AFTER the one the card caused, until the end of the
// turn). Every movement an effect makes goes through MoveHome or MoveToBattlefield, and the assignment of an army through Assign; each asks here first. A battle ending sends its
// units home by the rules, not by an effect, so it is not a movement in this sense (the convention Tamori Tamoko's negation already follows).
public sealed partial class MatchEngine
{
    private void LockMovement(CardInstance card, string source, MovementKinds kinds)
    {
        if (WartimePreparationsNegatesAssignLock(card, source, kinds)) return; // Wartime Preparations (The Shadow's Embrace pass, issue #476 batch 6).
        card.MovementLocks.Add(new MovementLock(source, _state.TurnNumber, kinds));
        Emit("movementLocked", $"{Definition(card).Name}'s {DescribeKinds(kinds)} is negated for the rest of the turn.", card.OwnerId, card.Id);
    }

    private static string DescribeKinds(MovementKinds kinds) =>
        kinds == MovementKinds.AssignAttack ? "assignment to attack" : kinds == MovementKinds.Assign ? "assignment to battlefields" : kinds.HasFlag(MovementKinds.Assign) ? "assignment and movement to battlefields" : kinds.HasFlag(MovementKinds.Home) ? "movement" : "movement to battlefields";

    private bool MovementLocked(CardInstance card, MovementKinds kind) =>
        card.MovementLocks.Any(l => l.Turn == _state.TurnNumber && (l.Kinds & kind) != 0 && MovementLockHolds(l)) || PerfectMomentLocks(card) || P4GlobalMovementLock(kind) || SuikihimeMovementLocked(card, kind); // Too Close to Home (issue #503)

    // A card whose movement of this kind is locked stays where it is; the effect that moved it goes on with whatever else it does.
    private bool MovementLockNegates(CardInstance card, MovementKinds kind)
    {
        if (!MovementLocked(card, kind) || HiromiUnnegatable()) return false; // Yoritomo Hiromi (issue #477): the movement from her Battle actions will not be negated.
        Emit("moveNegated", $"{Definition(card).Name}'s movement was negated: it may not move this turn.", card.OwnerId, card.Id);
        // A "next movement" lock is spent by the movement it just negated (Shinjo Junpei); a turn-long lock negating the same movement is not.
        card.MovementLocks.RemoveAll(l => l.NextOnly && l.Turn == _state.TurnNumber && (l.Kinds & kind) != 0 && MovementLockHolds(l));
        return true;
    }
}
