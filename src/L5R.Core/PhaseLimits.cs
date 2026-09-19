namespace L5R.Core;

/// <summary>The six phases of a turn as the printed rules name them (Straighten, Events, Action, Attack, Dynasty, End).
/// The engine's Phase enum is finer than that: the whole Attack Phase (attack and defense assignment, both Maneuvers,
/// battle selection, and every battle) is several of its values, and Dynasty's province discard is another. A card that
/// says "each phase" means these six, so a limit must not reset in the middle of the Attack Phase.</summary>
internal static class PrintedPhases
{
    public const int Count = 6;
    public static int Of(Phase phase) => phase switch
    {
        Phase.Straighten => 0,
        Phase.Events => 1,
        Phase.Action => 2,
        Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense or Phase.BattleSelection or Phase.Battle => 3,
        Phase.Dynasty or Phase.DynastyDiscard => 4,
        Phase.End => 5,
        _ => Count
    };
    /// <summary>The number of one phase INSTANCE: this turn's Straighten Phase, this turn's Attack Phase, the next turn's, ... Two
    /// moments share a number exactly when they are in the same printed phase of the same turn.</summary>
    public static int Instance(int turnNumber, Phase phase) => turnNumber * (Count + 2) + Of(phase);
}

public sealed partial class MatchEngine
{
    // "Once per phase" (issue #333, Daigotsu Bofana; the general half of what a printed "the first time each phase" needs). The
    // engine's other limit, the AbilityUses stamp, is once per TURN: it holds the turn number, and a use counts while the stamp
    // equals it. A phase use is stamped in the same dictionary under a "phase:" key with the number of the phase INSTANCE -- the
    // turn and which of the six printed phases it is. The stamp is compared with the current phase whenever it is read, so a new
    // phase (including the same phase on the next turn) resets the limit with nothing to clear, and a stamp can never be
    // mistaken for a per-turn one because its key differs. It rides on the card's (or player's) existing AbilityUses, so it is
    // wiped when the card leaves play or returns to hand and is carried over by an Experienced overlay exactly like every other use.
    private const string PhaseUseKeyPrefix = "phase:";
    private int PhaseInstance() => PrintedPhases.Instance(_state.TurnNumber, _state.Phase);
    private bool PhaseUseSpent(Dictionary<string, int> uses, string key) =>
        uses.TryGetValue(PhaseUseKeyPrefix + key, out var stamp) && stamp == PhaseInstance();
    /// <summary>Spends the once-per-phase use of `key` on this card or player when it is still unspent in the current phase;
    /// false (and no change) when it was already spent this phase.</summary>
    private bool TrySpendPhaseUse(Dictionary<string, int> uses, string key)
    {
        if (PhaseUseSpent(uses, key)) return false;
        uses[PhaseUseKeyPrefix + key] = PhaseInstance();
        return true;
    }
}
