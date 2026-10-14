namespace L5R.Core;

// Destruction an action inflicts on its chosen card, and the reach of the one that trades a Personality for it.
public sealed partial class MatchEngine
{
    // "Destroy it" for a card an action targeted: a lone Follower, Item or Spell is destroyed as an attachment (it goes to the Fate discard pile and its host lives), a
    // Personality with his unit. Not a combat destruction, and always the acting player's own action (Lady Matsu's Rage, the Dishonorable-death penalty and Expendable apply).
    private void DestroyTargetCard(CardInstance target, int actingPlayerId)
    {
        if (target.Zone == Zone.Attached) DestroyAttachment(target); else DestroyUnits([target], false, actingPlayerId);
    }

    // Great Sacrifice (R-084, 2026-10-13): the enemy unit's TOTAL Force (UnitTotalForce, bowed cards included, the helper Fortitude uses) may be at most this reach -- the performer's Force plus Personal Honor, plus one if he is defending.
    private int SacrificeReach(CardInstance performer) => CardForce(performer) + CardPersonalHonor(performer) + (IsDefendingUnit(performer) ? 1 : 0);
}
