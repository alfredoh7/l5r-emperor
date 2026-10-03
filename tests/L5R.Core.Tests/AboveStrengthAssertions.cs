using L5R.Core;
using static RegressionAssertions;

// Core rule: a target whose compared value is above an Attack's strength is still a LEGAL target;
// the Attack simply fails to destroy it. These helpers replace the old "Reject" pins for that case.
internal static class AboveStrengthAssertions
{
    // Accepts the command on a COPY of the engine's state (so the caller's engine is untouched and can go on to
    // exercise a further command) and proves each named card is left exactly where it was (not destroyed).
    public static void AcceptSurvive(MatchEngine engine,GameCommand command,params string[] survivors)
    {
        var copy=MatchEngine.FromTestState(engine.State);
        var zones=survivors.ToDictionary(id=>id,id=>copy.State.Cards[id].Zone);
        Accept(copy,command with {ExpectedVersion=copy.Version});
        foreach(var id in survivors)
            if(copy.State.Cards[id].Zone!=zones[id]) throw new Exception($"Above-strength target {id} was not left in play: {zones[id]} -> {copy.State.Cards[id].Zone}");
    }
}
