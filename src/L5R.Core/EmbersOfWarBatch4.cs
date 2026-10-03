namespace L5R.Core;

// Embers of War pass (issue #460), batch 4: primitives shared by more than one card of this batch, or too small to deserve their own file.
// Each is documented at its own use in CardAbilities.cs (the catalog entries) and FocusEffectChoice.cs (the Focus Effect choice); this file
// holds only the execution helper that needs its own method body.
public sealed partial class MatchEngine
{
    // Tactical Trap (oracle-l5r-7717): "Move him home. Bow him if he moved." -- MoveHome (unlike TryMoveToBattlefield) has no own bool return
    // for "did this actually move the card" (a negated move, or a target already at home, are both no-ops), so the zone is read before and
    // after the call, the same "did it actually happen" shape Shiba Mitsushen's own move-then-straighten-if-moved already uses the other way
    // around (issue #458). A target already home is not bowed at all -- Fear Me!'s "if he moved" reading (issue #336) applies here too.
    private void TacticalTrapMove(CardInstance target, int actingPlayerId)
    {
        var wasHome = target.Zone == Zone.Home;
        MoveHome(target, false);
        if (!wasHome && target.Zone == Zone.Home && !StalwartNegatesBow(target, actingPlayerId)) target.Bowed = true;
    }
}
