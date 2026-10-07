namespace L5R.Core;

// Primitive P24 (issue #690), movement. See EffectPrimitives.cs for the feature and docs/decisions/2026-10-06-690-p24-effect-primitives.md for the readings.
//
// SWITCH (core rules Glossary): "two units simultaneously swap locations; negating one side's movement negates the other's too." A switch existed for Sudden Movement and Spirit of the
// Shadows (ShadowsEmbraceBatch4.cs: SwitchPlan / SwitchBlocked), tied to the two slots of those abilities and asking a subset of the negations first. SwitchLocations is the general entry a
// card's effect calls with any two units. It asks, for each side in turn, every question an ordinary move to that destination asks (MoveHome's and TryMoveToBattlefield /
// MoveToBattlefield's, in their order); the first side that is negated spends the negation that stopped it and neither unit moves. Only when both pass do both move, at once: each
// destination was read before either left. A test pins the list of questions against the three move functions, so a negation added to one of them fails until it is added here.
public sealed partial class MatchEngine
{
    /// <summary>Switches the locations of the two cards' units (a card in a unit stands for its unit). True when both moved; false, with nothing moved, when either is not at a location, both
    /// stand at the same place, or either movement was negated. <paramref name="sourceName"/> names the effect in the log.</summary>
    internal bool SwitchLocations(CardInstance first, CardInstance second, string sourceName)
    {
        var a = UnitOf(first); var b = UnitOf(second);
        if (a is null || b is null || a.Id == b.Id || a.Zone is not (Zone.Home or Zone.Battlefield) || b.Zone is not (Zone.Home or Zone.Battlefield))
        {
            Emit("switchFailed", $"{sourceName}: a unit is no longer at a location, so neither moves.", first.OwnerId, first.Id);
            return false;
        }
        // Each destination is where the other stands now: a battlefield, or (null) its own controller's home.
        var toA = SwitchDestination(b); var toB = SwitchDestination(a);
        if (toA == toB) // the same battlefield, or both at home (two players' homes are two locations, but a unit only ever stands in its own)
        {
            Emit("switchFailed", $"{sourceName}: {Definition(a).Name} and {Definition(b).Name} have no other location to switch to, so neither moves.", a.OwnerId, a.Id);
            return false;
        }
        foreach (var (mover, to) in new[] { (a, toA), (b, toB) })
        {
            if (!SwitchSideNegated(mover, to)) continue;
            Emit("switchNegated", $"{sourceName}: {Definition(mover).Name}'s movement is negated, so the switch -- the other movement too -- is negated.", mover.OwnerId, mover.Id);
            return false;
        }
        SwitchRelocate(a, toA);
        SwitchRelocate(b, toB);
        Emit("locationsSwitched", $"{sourceName}: {Definition(a).Name} and {Definition(b).Name} switched locations.", a.OwnerId, a.Id);
        return true;
    }

    private CardInstance? UnitOf(CardInstance card) => InPlay(card) && _state.Cards.TryGetValue(UnitHostId(card), out var host) && InPlay(host) ? host : null;

    // Would this side's movement be negated? The questions of MoveHome (to == null) or of TryMoveToBattlefield and MoveToBattlefield, in their order. A question that negates spends
    // what it spends for an ordinary move (a one-shot shield, a watch, a "first time each turn" trait) and logs its own "moveNegated".
    private bool SwitchSideNegated(CardInstance card, int? to)
    {
        if (EnemyActionMovementAutoNegated(card)) return true;
        if (NegatesEffect(card, "move"))
        {
            Emit("moveNegated", $"{Definition(card).Name}'s movement was negated before it resolved.", card.OwnerId, card.Id);
            return true;
        }
        if (to is not int province)
            return MovementLockNegates(card, MovementKinds.Home) || P4ShieldNegatesMoveHome(card) || BattlefieldMovementNegated(card, StandingBattlefield(card), null) || TryConsumeWatch(WatchKind.Move, card, EffectSourcePlayer()) || SwitchSideDelayed(card, DelayedKinds.MoveHome);
        return SwitchSideDelayed(card, DelayedKinds.MoveToBattlefield) || TryConsumeWatch(WatchKind.Move, card, EffectSourcePlayer())
            || MovementLockNegates(card, MovementKinds.ToBattlefield) || MovementToBattlefieldNegated(card, province) || KitsuneNegatesMovement(card, province) || UguluNegatesMovement(card, province)
            || ShieldmanNegatesMove(card) || StarCrossedNegatesMove(card, province) || AsukaNegatesMove(card) || HoteiNegatesMove(card) || P4NegatesMoveToBattlefield(card, province);
    }

    // A delay armed on one side's movement (EmperorRulingR2.cs) cannot be honoured: a movement applied later would move one unit alone. The delayed side does not move now, so neither
    // does the other, and nothing is recorded for later.
    private bool SwitchSideDelayed(CardInstance card, string kind)
    {
        if (ActiveDelay(card, kind) is not { } scope || CannotBeDelayed(kind) || MovementNegationBarred(card, scope.ByPlayer)) return false;
        Emit("moveNegated", $"{Definition(card).Name}'s movement is delayed, so he does not switch locations.", card.OwnerId, card.Id);
        return true;
    }

    // The movement itself, with what MoveHome / MoveToBattlefield record for a unit they moved.
    private void SwitchRelocate(CardInstance card, int? to)
    {
        if (to is not int province)
        {
            var from = card.Zone == Zone.Battlefield ? card.ProvinceIndex : null;
            ReturnHome(card, false);
            Emit("unitMovedHome", $"{Definition(card).Name}'s unit moved home.", card.OwnerId, card.Id);
            RecordActionEffect(card.Id, MovedKind, fromBattlefield: from);
            return;
        }
        card.Zone = Zone.Battlefield; card.ProvinceIndex = province;
        foreach (var attachment in Attachments(card.Id)) attachment.ProvinceIndex = province;
        _state.PendingMoveReactionCardId = card.Id;
        FireConsecrationWatches(card, province);
        Emit("unitMovedToBattlefield", $"{Definition(card).Name}'s unit moved to {BattlefieldLabel(province)}.", card.OwnerId, card.Id);
        RecordActionEffect(card.Id, MovedKind, toBattlefield: province);
    }

    // ---- adjacent battlefields --------------------------------------------------------------------------------------------------------------------------------------------------------------
    // "Move ... to an adjacent battlefield": MoveSpec.AdjacentUnresolved already lists the unresolved battlefields next to the current one for an ability that declares its move as data
    // (issue #336). These two are the same rule for an effect that is not such an ability (a trait, a follow-up): adjacency is the Defender's province line, a battlefield without a
    // province has no adjacent battlefield (R-002), and only a battlefield still to be fought is a destination.

    /// <summary>The unresolved battlefields next to <paramref name="battlefield"/>.</summary>
    internal int[] AdjacentUnresolvedBattlefields(int battlefield) => AdjacentBattlefields(battlefield).Where(_state.UnresolvedBattlefields.Contains).ToArray();

    /// <summary>Moves the card's unit from the battlefield it stands at to an adjacent unresolved one. False, with nothing moved, when it stands at no battlefield, the destination is not
    /// one of <see cref="AdjacentUnresolvedBattlefields"/>, or the movement was negated.</summary>
    internal bool MoveToAdjacentBattlefield(CardInstance card, int destination)
    {
        if (UnitOf(card) is not { Zone: Zone.Battlefield, ProvinceIndex: int from } unit || !AdjacentUnresolvedBattlefields(from).Contains(destination)) return false;
        return TryMoveToBattlefield(unit, destination);
    }
}
