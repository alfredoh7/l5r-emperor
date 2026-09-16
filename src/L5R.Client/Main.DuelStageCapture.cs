using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private readonly string[] _stageScenarios=["refusal","reaction","duelist","effects","discard","aftermath","force","honor","open","duelist-tie","four","empty","tie"];
    private int _scenarioProbe,_scenarioPhase,_scenarioMoves,_pendingStep;
    private GameCommand? _pendingDemoCommand;
    private void ProcessDuelStageCapture(double delta)
    {
        _captureClock+=delta;if(_captureClock<(_scenarioPhase<=0?1.0:.45))return;_captureClock=0;
        if(++_scenarioMoves>650)throw new Exception("Duel stage probe stalled.");
        IEnumerable<Node> Descendants(Node n){foreach(var c in n.GetChildren()){yield return c;foreach(var d in Descendants(c))yield return d;}}
        var host=_duelDemo;
        Button? Find(string text)=>host is null?null:Descendants(host).OfType<Button>().FirstOrDefault(b=>(b.Text==text||b.Name==text)&&b.IsVisibleInTree());
        void Native(Vector2 p){Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p});foreach(var pressed in new[]{true,false})Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=pressed});}
        void Press(string text){
            if(text=="Switch seat"&&Find(text) is null){Native(Find("Menu")!.GetGlobalRect().GetCenter());return;}
            if(text=="Focus from deck"&&Find(text) is null){Native(host!.Screen.PileClickPoint(host.Session.Snapshot!.View.ViewerId,Zone.FateDeck));return;}
            if(text is "Focus selected card" or "Focus from deck"){Native(host!.Screen.LandingClickPoint);return;}
            var b=Find(text)??throw new Exception($"Missing button {text} in {_stageScenarios[_scenarioProbe]}");if(b.Disabled)throw new Exception($"Disabled button {text}");Native(b.GetGlobalRect().GetCenter());}
        void Capture(string suffix){GetViewport().GetTexture().GetImage().SavePng(_capturePath+"-"+_stageScenarios[_scenarioProbe]+"-"+suffix+".png");}
        if(_scenarioPhase==0)
        {
            _preferences.ReducedMotion=true;_preferences.DuelGuidance=false;ApplyPreferences(false);_titleMenu!.ShowPlay();_scenarioPhase=-1;return;
        }
        if(_scenarioPhase==-1)
        {
            var entry=_titleMenu!.Entries.GetChildren().OfType<Button>().FirstOrDefault(b=>b.Text=="DUEL DEMO") ?? throw new Exception($"Duel entry missing from {_titleMenu.Page}: {string.Join(",",_titleMenu.Entries.GetChildren().OfType<Button>().Select(b=>b.Text))}");
            Native(entry.GetGlobalRect().GetCenter());_scenarioPhase=1;return;
        }
        if(_scenarioPhase==1)
        {
            var picker=Descendants(host!).OfType<OptionButton>().First();var index=DuelDemoScenarios.All.ToList().FindIndex(s=>s.Id==_stageScenarios[_scenarioProbe]);
            picker.Selected=index;picker.EmitSignal(OptionButton.SignalName.ItemSelected,index);_scenarioPhase=2;return;
        }
        if(_scenarioPhase==2)
        {
            var check=Find("Manual · control both seats")!;if(!check.ButtonPressed)Native(check.GetGlobalRect().GetCenter());_scenarioPhase=3;return;
        }
        if(_scenarioPhase==3){Press("Begin Duel");_scenarioPhase=4;return;}
        if(host!.Screen.PresentationBusy)return;
        if(Find("Reveal this seat") is not null){Press("Reveal this seat");return;}
        var v=host.Session.Snapshot!.View;
        if(host.Screen.CoachingVisible)throw new Exception("Suppressed coaching reappeared in a required stage.");
        if(v.PendingDuel is not null&&string.IsNullOrEmpty(host.Screen.EssentialStatus))throw new Exception("Required stage lost its essential status.");
        if(v.LastDuel!.Complete)
        {
            Capture("result");GD.Print($"DUEL_STAGE_CHECK: {_stageScenarios[_scenarioProbe]} complete through native controls, {v.Version} commands");
            Press("Continue");_scenarioProbe++;_scenarioPhase=1;
            if(_scenarioProbe==_stageScenarios.Length){System.IO.File.WriteAllText(_capturePath+".json","{\"passed\":true,\"scenarios\":13}");GetTree().Quit();}return;
        }
        if(v.PriorityPlayerId!=v.ViewerId){Press("Switch seat");return;}
        if(_pendingDemoCommand is {} pending)
        {
            if(pending.Choice=="discard") { Press(host.Session.Snapshot.Actions.Single(a=>a.Command==pending).Label);_pendingDemoCommand=null;return; }
            if(pending.Type=="duelistRefocus"&&_pendingStep++==0){Native(host.Screen.CardClickPoint(pending.TargetId!));return;}
            if(pending.Type=="orderDuelFocusEffects"&&_pendingStep++==0)
            {
                Capture("order");var second=Descendants(host).OfType<Button>().First(b=>b.Text.StartsWith("2.")&&b.IsVisibleInTree());Native(second.GetGlobalRect().GetCenter());return;
            }
            Press(pending.Type switch{"focus"=>"Focus selected card","duelistRefocus"=>"Replace from selected hand card",_=>"Resolve in this order"});_pendingDemoCommand=null;return;
        }
        var actions=host.Session.Snapshot.Actions.Where(a=>a.Enabled&&a.Command is not null).ToArray();
        switch(v.PendingDuel?.Stage)
        {
            case "AwaitingAccept":Press("Refuse challenge");return;
            case "Focusing":
                var cap=_stageScenarios[_scenarioProbe]=="four"?4:2;
                if(v.Players[v.ViewerId].FocusCount>=cap||!actions.Any(a=>a.Type=="focus")){Capture("before-strike");Press("Strike");return;}
                if(actions.FirstOrDefault(a=>a.Command!.Choice=="discard") is {} discard)
                {Press("Focus from discard…");_pendingDemoCommand=discard.Command;return;}
                var courtesy=v.Players[v.ViewerId].Hand.FirstOrDefault(c=>c.Definition!.Id=="oracle-l5r-7996");
                var hand=actions.FirstOrDefault(a=>a.Type=="focus"&&a.Command!.CardId==courtesy?.Id&&a.Command.CardId is not null);
                if(hand is not null){Native(host.Screen.CardClickPoint(hand.Command!.CardId!));_pendingDemoCommand=hand.Command;_pendingStep=0;return;}
                Press("Focus from deck");return;
            case "DuelistRefocus":
                var replacement=actions.FirstOrDefault(a=>a.Command!.TargetId is not null);
                if(replacement is null){Press("Keep focused cards");return;}
                Capture("replacement");Native(host.Screen.CardClickPoint(replacement.Command!.CardId!));_pendingDemoCommand=replacement.Command;_pendingStep=0;return;
            case "OrderingEffects":
                Press("Arrange Focus Effects…");_pendingDemoCommand=actions.Single(a=>a.Type=="orderDuelFocusEffects").Command;_pendingStep=0;return;
            default:
                var reaction=actions.FirstOrDefault(a=>a.Type=="useAbility")??actions.FirstOrDefault(a=>a.Command!.Choice=="draw")??actions.First(a=>a.Type=="declineReaction");
                Capture("reaction");Press(reaction.Label);return;
        }
    }
}
