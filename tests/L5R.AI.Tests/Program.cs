using System.Text.Json;
using L5R.AI;
using L5R.Core;

var runner=new TestRunner(args);
if(!runner.Valid) return 2;
var tests=new List<(string Name,Action Test)>();
Run("waits without priority and never concedes automatically",()=>
{
    var game=MatchEngine.CreateStarterMatch();
    Check(TrainingOpponent.Choose(game.GetView(1),game.QueryActions(1)).Command is null,"AI acted out of turn");
    Check(TrainingOpponent.Choose(game.GetView(0),[new("concede","Concede",true,null,new("concede",0,0))]).Command is null,"AI conceded");
});
Run("consumes only supplied detached player views",()=>
{
    var game=MatchEngine.CreateEconomyScenario();
    var view=JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(game.GetView(0)))!;
    var actions=JsonSerializer.Deserialize<ActionOption[]>(JsonSerializer.Serialize(game.QueryActions(0)))!;
    var decision=TrainingOpponent.Choose(view,actions);
    Check(decision.Command is not null,"No decision");
    Check(game.Execute(decision.Command!).Accepted,"Detached inputs produced illegal command");
});
Run("resolves purchase and Proclaim with alternating reaction priority",()=>
{
    var game=MatchEngine.CreateEconomyScenario(); var id=game.GetView(0).Players[0].Provinces[0].Card!.Id;
    var quote=game.QuotePurchase(0,id);
    Check(game.Execute(new("purchase",0,0,id,quote.Payments[0].SourceIds)).Accepted,"Purchase failed");
    var move=TrainingOpponent.Choose(game.GetView(0),game.QueryActions(0)); Check(move.Command?.Type=="proclaim","Proclaim not chosen");
    Check(game.Execute(move.Command!).Accepted,"Proclaim failed");
    for(var i=0;i<2;i++)
    {
        var player=game.GetView(0).PriorityPlayerId;
        move=TrainingOpponent.Choose(game.GetView(player),game.QueryActions(player));
        Check(move.Command?.Type=="declineReaction","Reaction not passed"); Check(game.Execute(move.Command!).Accepted,"Reaction pass failed");
    }
    Check(game.GetView(0).PendingChoice is null,"Reaction window stuck");
});
Run("chooses an offered free Experienced Overlay",()=>
{
    var game=MatchEngine.CreateEconomyScenario();var view=game.GetView(0);
    var higher=view.Players[0].Provinces[0].Card!;
    var action=new ActionOption("overlay","Overlay",true,null,new("overlay",0,view.Version,higher.Id,TargetId:"lower"),higher.Id,["lower"]);
    var decision=TrainingOpponent.Choose(view,[action]);
    Check(decision.Command is {Type:"overlay",TargetId:"lower"},"AI did not choose the offered free Overlay");
});
foreach(var seed in new ulong[]{1,42,831,20260906,99999})
    Run($"seed {seed}: legal complete training match, bounded decisions and deterministic replay",()=>Play(seed));
foreach(var seed in new ulong[]{1,42,831,20260906,99999})
    Run($"seed {seed}: published abilities, complete match and replay",()=>Play(seed,true));
foreach(var pair in new[]{("Crane","Lion"),("Lion","Crane"),("Crane","Crane"),("Lion","Lion")})
    Run($"custom Practice {pair.Item1}/{pair.Item2}: complete match and replay",()=>Play(42,custom:pair));
runner.Run("ai",tests);
return runner.Finish();

void Run(string name,Action run) => tests.Add((name,run));

static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
static void Play(ulong seed,bool published=false,(string Your,string Opponent)? custom=null)
{
    var game=custom is {} pair?MatchEngine.CreateCustomMatch(seed,CustomPractice.Preset(pair.Your),CustomPractice.Preset(pair.Opponent)):published?MatchEngine.CreatePublishedMatch(seed):MatchEngine.CreateStarterMatch(seed); var commands=0;
    while(game.GetView(0).WinnerId is null && commands<1600)
    {
        var player=game.GetView(0).PriorityPlayerId;
        var view=game.GetView(player); var actions=game.QueryActions(player);
        // Also exercise assignment reconstruction from legal target/province metadata.
        if(commands%3==0) actions=actions.Select(a=>a.Type is "attack" or "assignDefense"?a with {Command=null}:a).ToArray();
        var decision=TrainingOpponent.Choose(view,actions);
        Check(decision.Command is not null,$"No decision at {view.Phase}, step {commands}");
        var result=game.Execute(decision.Command!);
        Check(result.Accepted,$"Illegal {decision.Command!.Type}: {result.Error}"); commands++;
    }
    Check(game.GetView(0).WinnerId is not null,$"Match exceeded 1600 decisions (turn {game.GetView(0).TurnNumber})");
    var replay=MatchEngine.Replay(game.ExportReplay());
    Check(JsonSerializer.Serialize(replay.GetView(0))==JsonSerializer.Serialize(game.GetView(0)),"Replay differs");
    Console.WriteLine($"  {commands} decisions; {game.GetView(0).VictoryReason}; turn {game.GetView(0).TurnNumber}");
}
