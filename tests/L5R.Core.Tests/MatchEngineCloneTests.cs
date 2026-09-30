using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #550: RoomManager.SubmitAsync validated every submitted command against a trial built by
// MatchEngine.Replay(ExportReplay()) -- rebuilding the engine from scratch and re-executing every prior
// accepted command first, an O(n) cost per submission (O(n^2) over a match). MatchEngine.Clone() builds the
// trial directly from the live state instead. This suite proves Clone() is an exact substitute for
// Replay(ExportReplay()): same view for both players, same RNG state (so the NEXT command, executed on both
// independently, is accepted/rejected identically and ends in the same state), and _customDecks survives.
internal static class MatchEngineCloneTests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);

    public static void Run(TestRunner runner) => runner.Run("match-engine-clone", [
        ("Clone() matches Replay(ExportReplay()) after many accepted commands: same views for both players, same version, and the next command lands identically on both (proves RNG state travels with Clone() exactly as it does with a fresh replay)", CloneMatchesReplayAfterManyCommands),
        ("Clone() carries a Custom Practice match's _customDecks (ExportReplay().CustomDecks round-trips) and its view, from the moment of creation with no commands yet played", CloneCarriesCustomDecks),
    ]);

    private static GameCommand? NextCommand(MatchEngine e)
    {
        var playerId = e.State.PriorityPlayerId;
        var options = e.QueryActions(playerId).Where(o => o.Enabled && o.Command is not null && o.Type != "concede").ToArray();
        var chosen = options.FirstOrDefault(o => o.Type != "pass") ?? options.FirstOrDefault();
        return chosen?.Command;
    }

    private static void CloneMatchesReplayAfterManyCommands()
    {
        var e = MatchEngine.CreateStarterMatch();
        for (var i = 0; i < 30 && e.State.WinnerId is null; i++)
        {
            var command = NextCommand(e);
            if (command is null) break;
            Accept(e, command);
        }
        Check(e.ExportReplay().Commands.Length > 5, "the sequence actually accepted several commands, or this test proves nothing");

        var viaClone = e.Clone();
        var viaReplay = MatchEngine.Replay(e.ExportReplay());
        Eq(viaClone.Version, viaReplay.Version, "Clone() and Replay(ExportReplay()) agree on the version counter");
        Eq(JsonSerializer.Serialize(viaClone.GetView(0)), JsonSerializer.Serialize(viaReplay.GetView(0)), "...and on player 0's view");
        Eq(JsonSerializer.Serialize(viaClone.GetView(1)), JsonSerializer.Serialize(viaReplay.GetView(1)), "...and on player 1's view");

        if (e.State.WinnerId is null && NextCommand(e) is { } next)
        {
            var viaCloneResult = viaClone.Execute(next);
            var viaReplayResult = viaReplay.Execute(next);
            Eq(viaCloneResult.Accepted, viaReplayResult.Accepted, "Both trials accept or reject the same next command identically");
            Eq(JsonSerializer.Serialize(viaClone.GetView(0)), JsonSerializer.Serialize(viaReplay.GetView(0)), "...and finish with the same player-0 view (RNG draws, if any, matched exactly)");
            Eq(JsonSerializer.Serialize(viaClone.GetView(1)), JsonSerializer.Serialize(viaReplay.GetView(1)), "...and the same player-1 view");
        }
    }

    private static void CloneCarriesCustomDecks()
    {
        var e = MatchEngine.CreateCustomMatch(42, CustomPractice.Preset("Crane"), CustomPractice.Preset("Lion"));
        var clone = e.Clone();
        Eq(JsonSerializer.Serialize(clone.ExportReplay().CustomDecks), JsonSerializer.Serialize(e.ExportReplay().CustomDecks), "The clone's exported custom decks match the original's");
        Eq(JsonSerializer.Serialize(clone.GetView(0)), JsonSerializer.Serialize(e.GetView(0)), "The clone's view matches the original's immediately after creation");
        // A command executed on the clone must not mutate the original (proves Clone() truly detaches _state, not just its top-level reference).
        var command = NextCommand(clone);
        if (command is not null)
        {
            var before = JsonSerializer.Serialize(e.GetView(0));
            Accept(clone, command);
            Eq(JsonSerializer.Serialize(e.GetView(0)), before, "Executing a command on the clone left the original engine's view unchanged");
        }
    }
}
