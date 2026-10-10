using System.Reflection;
using System.Text.Json;
using L5R.Client;
using L5R.Core;

internal static class PreparedReviewTests
{
    public static async Task Run()
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        using var session = new MatchSession();
        session.StartPractice(false, true, 0);
        var ordinaryGeneration = session.Generation; var ordinaryKey = session.MatchKey;

        // This position deliberately differs from the seed's normal initial state;
        // engine commands can play it, but ExportReplay cannot reconstruct it.
        var state = MatchEngine.CreatePublishedTestMatch(123).State;
        state.Players[0].Honor += 7;
        var prepared = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        session.StartPreparedReview(prepared, 0);
        Check(session.Generation > ordinaryGeneration && session.MatchKey != ordinaryKey, "A prepared review did not replace the presentation lifecycle");
        Check(!session.CanExportPracticeReplay && session.ExportPracticeReplay() is null, "A prepared custom position exported an unreconstructible replay");
        Check(session.Snapshot!.View.Players[0].Honor == state.Players[0].Honor, "Prepared position was replaced with an ordinary seed state");
        var generation = session.Generation; var key = session.MatchKey; var version = session.Snapshot.View.Version;
        session.SwitchPracticeSeat();
        Check(session.Snapshot!.View.ViewerId == 1 && session.Generation == generation && session.MatchKey == key,
            "Review hotseat switch reset the match lifecycle instead of changing only its viewer");
        Check(!session.CanExportPracticeReplay && session.ExportPracticeReplay() is null, "Changing review seats removed the replay restriction");
        session.Disconnect();
        Check(!session.CanExportPracticeReplay && session.ExportPracticeReplay() is null, "Disconnecting without replacing the prepared engine enabled invalid export");

        var concede = session.Snapshot.Actions.Single(a => a.Enabled && a.Type == "concede").Command!;
        await session.Submit(concede);
        Check(session.Snapshot!.View.Version == version + 1 && session.Snapshot.View.WinnerId == 0, "The prepared review could not execute an actual offered command");
        Check(!session.CanExportPracticeReplay && session.ExportPracticeReplay() is null, "Finishing a prepared review enabled invalid replay export");

        session.StartPractice(false, true, 0);
        Check(session.CanExportPracticeReplay && !session.BattleValuesReview && session.ExportPracticeReplay() is not null,
            "Starting ordinary practice retained prepared-position restrictions");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var replay = JsonSerializer.Deserialize<ReplayRecord>(session.ExportPracticeReplay()!, json)!;
        var replayed = MatchEngine.Replay(replay);
        Check(JsonSerializer.Serialize(replayed.GetView(0), json) == JsonSerializer.Serialize(session.Snapshot!.View, json),
            "Ordinary practice export did not reproduce its actual initial position");
        session.SwitchPracticeSeat();
        Check(session.CanExportPracticeReplay && JsonSerializer.Serialize(replayed.GetView(1), json) == JsonSerializer.Serialize(session.Snapshot!.View, json),
            "Ordinary practice seat switch lost valid replay export or changed replayable state");
    }
}
