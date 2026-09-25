namespace L5R.Core;

// "Delay his destruction from the action's effects until before this turn ends" (Spirit of the Berserker, Second City). Since the shared delay mechanism (EmperorRulingR2.cs, issue #492) this is
// one use of it: the reaction arms a capture, limited to the DESTRUCTION kind, on the targeted card for the held action; where the action would destroy the card (DestroyUnits,
// DestroyAttachment) the destruction is recorded as a delayed effect that waits for DelayUntil.EndOfTurn, and is applied at the top of EndTurn if the card is still in play. Only the destruction
// is delayed; every other effect of the action applies as usual. A Personality is destroyed with his unit, as any destruction.
public sealed partial class MatchEngine
{
    // The reaction's effect: from now until the held action has resolved, the destruction it would inflict on this card is put off until before the turn ends.
    private void DelayDestructionFromAction(CardInstance card) => ArmDelayForHeldAction(card, DelayUntil.EndOfTurn, "Spirit of the Berserker", DelayedKinds.Destroy);
}
