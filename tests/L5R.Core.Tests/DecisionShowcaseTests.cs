using L5R.Core;
using System.Text.Json;
using static RegressionAssertions;

internal static class DecisionShowcaseTests
{
    public static void Run(TestRunner runner) => runner.Run("decision-showcase",[
        ("Yumaru: exactly one of three complete choices resolves and a stale answer is rejected",Yumaru),
        ("Fukui: disabled alternative retains public grouping metadata and transport preserves it",Fukui),
        ("Every prepared generic decision offers complete engine answers that execute",Generic)
    ]);
    private static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    private static void Yumaru()
    {
        foreach(var choice in new[]{"self","target","both"})
        {
            var engine=MatchEngine.CreateDecisionShowcase("yumaru");
            var pending=engine.State.PendingEffectChoice!;
            var answers=engine.QueryActions(0).Where(a=>a.Type=="chooseEffect").ToArray();
            Check(answers.Length==3,"Three choices required");
            Check(engine.QueryActions(1).All(a=>a.Type is "wait" or "concede"),"Opponent received decisions");
            var command=answers.Single(a=>a.Command!.Choice==choice).Command!;
            Accept(engine,command);
            Check((engine.State.Cards[pending.Command.CardId!].Zone==Zone.Home)==(choice is "self" or "both"),"Incorrect source movement");
            Check((engine.State.Cards[pending.Command.TargetId!].Zone==Zone.Home)==(choice is "target" or "both"),"Incorrect target movement");
            Reject(engine,command);
        }
    }
    private static void Generic()
    {
        foreach(var (kind,type) in new[]{("preserving","preservingHonor"),("rin","rinShame"),("conqueror","conqueror"),("straighten","declineStraighten"),("discard","forcedDiscard")})
        {
            var engine=MatchEngine.CreateDecisionShowcase(kind);
            Check(engine.State.PendingChoice?.Type==type,"Wrong pending choice: "+kind);
            var choices=engine.QueryActions(engine.State.PriorityPlayerId).Where(a=>a.Type!="concede").ToArray();
            Check(choices.Length>=2,"No meaningful choice: "+kind);
            foreach(var option in choices)Accept(MatchEngine.FromTestState(engine.State),option.Command!);
        }
    }
    private static void Fukui()
    {
        foreach(var kind in new[]{"fukui","fukui-bowed"})
        {
            var engine=MatchEngine.CreateDecisionShowcase(kind);
            var choices=engine.QueryActions(0).Where(a=>a.AlternativeCost?.AbilityId=="fukui-bow").ToArray();
            Check(choices.Length==2,"Both costs must be discoverable");
            Check(choices.Select(a=>a.AlternativeCost!.PerformerId).Distinct().Count()==1,"Grouping lost performer");
            var roundtrip=JsonSerializer.Deserialize<ActionOption[]>(JsonSerializer.Serialize(choices))!;
            Check(roundtrip.Zip(choices).All(pair=>pair.First.AlternativeCost==pair.Second.AlternativeCost),"Metadata lost on wire");
            var bow=choices.Single(a=>a.AlternativeCost!.Id=="bow");
            Check(bow.Enabled==(kind=="fukui"),"Bow availability incorrect");
            if(kind=="fukui-bowed")Check(bow.Command is null && !string.IsNullOrWhiteSpace(bow.Reason),"Disabled cost must retain reason without invented command");
            var destroy=choices.Single(a=>a.AlternativeCost!.Id=="destroy");
            Accept(engine,destroy.Command!);
            Check(engine.State.Cards[destroy.AlternativeCost!.PerformerId!].Zone==Zone.RemovedFromGame,"Created Ancestor not removed after destruction");
            Check(engine.State.Cards[destroy.Command!.TargetId!].Bowed,"Enemy not bowed");
        }
    }
}
