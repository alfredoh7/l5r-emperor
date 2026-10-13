using System.Text.Json;
using L5R.Core;

internal static class RegressionAssertions
{
    public static void Accept(MatchEngine engine,GameCommand command)
    {
        var duplicated=ReactionProbe.Before(engine); // issue #898: L5R_REACTION_PROBE=1 checks every reaction window a suite reaches
        var result=engine.Execute(command);
        Require(result.Accepted,$"{command}: {result.Error ?? "Rejected"}");
        ReactionProbe.Observe(engine);
        ReactionProbe.After(duplicated,command,engine);
    }

    public static void Reject(MatchEngine engine,GameCommand command)
    {
        var before=JsonSerializer.Serialize(engine.State);
        Require(!engine.Execute(command).Accepted,$"Expected rejection: {command}");
        Require(JsonSerializer.Serialize(engine.State)==before,$"Rejection mutated state: {command}");
    }

    public static void HiddenIdsAbsent(MatchView view,IEnumerable<string> ids)
    {
        var json=JsonSerializer.Serialize(view);
        foreach(var id in ids) Require(!json.Contains(JsonSerializer.Serialize(id)), $"Hidden card id leaked: {id}");
    }

    public static void OpeningCounts(MatchState state)
    {
        foreach(var player in state.Players)
        {
            Require(player.Provinces.Count==4,$"Player {player.Id}: expected four provinces");
            Require(player.Hand.Count==6,$"Player {player.Id}: expected six cards in hand");
            Require(player.DynastyDeck.Count==36,$"Player {player.Id}: expected 36 Dynasty cards");
            Require(player.FateDeck.Count==34,$"Player {player.Id}: expected 34 Fate cards");
        }
    }

    // Each command gets a fresh engine. The caller chooses replay (training) or state (live cards).
    public static void OfferedCommandsValidate(MatchEngine source,Func<MatchEngine> fresh,bool alternateTargets=false)
    {
        var options=source.QueryActions(source.State.PriorityPlayerId).Where(a=>a.Enabled && a.Command is not null).ToArray();
        Require(options.Length>0,"Fixture offered no enabled exact commands");
        foreach(var option in options)
        {
            Accept(fresh(),option.Command!);
            if(alternateTargets && option.Type=="useAbility" && option.Command!.CardIds is null)
                foreach(var target in option.TargetIds??[]) Accept(fresh(),option.Command! with {TargetId=target});
        }
    }

    private static void Require(bool condition,string message)
    {
        if(!condition) throw new Exception(message);
    }
}
