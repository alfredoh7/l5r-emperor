using Godot;
using L5R.Core;
using System.Text.Json;
namespace L5R.Client;

public partial class Main
{
    private int _followupStep;
    private long _followupVersion;
    private string? _followupTarget;
    private readonly List<string> _followupChecks=[];
    private double _followupTotal;
    private bool RunDuelFollowupProbe(double delta)
    {
        _followupTotal+=delta;_followupClock+=delta;
        if(_followupTotal>120)throw new InvalidOperationException("Duel follow-up probe timed out at "+_followupStep);
        if(_matchDuelPreparing||_matchDuelReturning||_matchDuel?.Screen.PresentationBusy==true||_followupClock<.55)return true;
        _followupClock=0;
        var screen=_matchDuel?.Screen;var view=_session.Snapshot!.View;
        if(_matchProbeScenario=="concede")
        {
            switch(_followupStep++)
            {
                case 0:_followupVersion=view.Version;Press("Menu");break;
                case 1:Press("Concede match…");break;
                case 2:
                    Check(GetViewport().GuiGetFocusOwner() is Button{Text:"Keep playing"},"confirmation defaults to Keep playing");
                    Shot("confirmation");KeyPress(Key.Escape);break;
                case 3:Check(view.Version==_followupVersion&&view.WinnerId is null,"Escape cancels without a command");Press("Menu");break;
                case 4:Press("Concede match…");break;
                case 5:Press("Keep playing");break;
                case 6:Check(view.Version==_followupVersion&&view.WinnerId is null,"Keep playing preserves the duel");Press("Menu");break;
                case 7:Press("Concede match…");break;
                case 8:Press("Concede match");break;
                case 9:Check(view.WinnerId==1-view.ViewerId&&view.Version==_followupVersion+1,"actual menu concedes exactly once for either seat");Shot("conceded");Press("Return to table");break;
                case 10:Check(!MatchDuelOpen,"concession returns to the original table");Finish();break;
            }
            return true;
        }
        switch(_followupStep)
        {
            case 0:
                Check(view.PendingChoice?.Type=="focusEffectChoice"&&view.LastDuel?.Result is null,"decision pauses final verdict");
                Shot(view.ViewerId==view.PriorityPlayerId?"choice":"waiting");
                if(view.ViewerId!=view.PriorityPlayerId)
                {Check(!Descendants(screen!).OfType<Button>().Any(b=>b.Text is "Return to hand" or "Take no card"),"waiting seat has no choice controls");Press("Menu");_followupStep=100;return true;}
                _followupVersion=view.Version;Press("Inspect source");_followupStep=1;break;
            case 100:Press("Switch seat");_followupStep=101;break;
            case 101:Check(!screen!.IsVisibleInTree(),"seat switch uses opaque handoff");Press("Reveal this seat");_followupStep=0;break;
            case 1:Check(screen!.InspectedCard?.Id==view.PendingChoice!.CardId,"source opens original card inspector");Shot("source");Press("Close inspection");_followupStep=2;break;
            case 2:
                var options=_session.Snapshot.Actions.Where(a=>a.Enabled&&a.Type=="chooseFocusEffect").ToArray();
                var decline=options.FirstOrDefault(a=>a.Command!.Choice=="decline");
                Check(Descendants(screen!).OfType<Button>().Any(b=>b.Text is "Take no card" or "Decline effect")== (decline is not null),"decline is shown only when the engine offers it");
                if(OS.GetCmdlineUserArgs().Contains("--duel-choice-decline"))
                {Press(_matchProbeScenario is "choice-discard" or "choice-multiple" or "choice-crowded"?"Take no card":"Decline effect");_followupStep=4;break;}
                var option=OS.GetCmdlineUserArgs().Contains("--duel-choice-draw")?options.Single(a=>a.Command!.TargetId=="draw-card"):_matchProbeScenario=="choice-crowded"?options.Last(a=>a.Command!.Choice!="decline"):options.First(a=>a.Command!.Choice!="decline");_followupTarget=option.Command!.TargetId;
                var face=Descendants(screen!).OfType<CardTile>().FirstOrDefault(c=>c.Name=="Choice "+_followupTarget);
                if(face is null){Press(option.Label);_followupStep=4;break;}
                if(_matchProbeScenario=="choice-crowded"){_followupStep=20;break;}
                Click(face.GetGlobalRect().GetCenter());_followupStep=3;break;
            case 20:
                var trayScroll=Descendants(screen!).OfType<ScrollContainer>().Single(c=>c.Name=="Focus choices");
                var lastFace=Descendants(screen!).OfType<CardTile>().Single(c=>c.Name=="Choice "+_followupTarget);
                if(trayScroll.GetGlobalRect().HasPoint(lastFace.GetGlobalRect().GetCenter()))
                {Check(trayScroll.ScrollHorizontal>0,"crowded tray scrolls with native input to the last legal card");Shot("scrolled");Click(lastFace.GetGlobalRect().GetCenter());_followupStep=3;}
                else {var bar=trayScroll.GetHScrollBar().GetGlobalRect();Click(new Vector2(bar.End.X-20,bar.GetCenter().Y));}
                break;
            case 3:
                Check(screen!.SelectedFocusChoiceTarget==_followupTarget&&view.Version==_followupVersion,"selecting a target never submits it");Shot("selected");
                var confirm=Descendants(screen).OfType<Button>().Single(b=>b.Name=="Resolve Focus choice");Click(confirm.GetGlobalRect().GetCenter());_followupStep=4;break;
            case 4:
                Check(view.Version==_followupVersion+1,"choice submits exactly one engine command");
                Check(view.LastDuel!.Beats!.Any(b=>b.Kind=="FocusEffect"&&b.Source?.Definition?.Id==(_matchProbeScenario=="choice-effect"?"oracle-l5r-10123":_matchProbeScenario=="choice-straighten"?"oracle-l5r-10139":_matchProbeScenario=="choice-attach"?"oracle-l5r-6702":_matchProbeScenario=="choice-destroy"?"oracle-l5r-8002":"oracle-l5r-2031")),"choice outcome retained as a source-linked beat");
                if(view.PendingChoice?.Type=="focusEffectChoice"){_followupStep=0;break;}
                Check(screen!.CanContinue,"complete verdict waits for Continue");Shot("verdict");Press("Duel details");_followupStep=5;break;
            case 5:
                Check(Descendants(screen!).OfType<Label>().Any(l=>l.Text.Contains("Disreputable Deal")||l.Text.Contains("Sandalwood Incense")||l.Text.Contains("Ceaseless Vigil")||l.Text.Contains("Shamsir")||l.Text.Contains("The Crimson Mark")),"recap includes the Focus Effect source");Shot("recap");Press("Continue");_followupStep=6;break;
            case 6:
                Check(!MatchDuelOpen&&view.PendingDuel is null,"returns to the same match after the decision");Shot("table");Finish();break;
        }
        return true;
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_followupChecks.Add(message);}
        void Shot(string name){System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_capturePath!)!);GetViewport().GetTexture().GetImage().SavePng(_capturePath+"-"+name+".png");}
        void Press(string text){var button=Descendants(_matchDuel!).OfType<Button>().Last(b=>b.IsVisibleInTree()&&!b.Disabled&&b.Text==text);Click(button.GetGlobalRect().GetCenter());}
        void Click(Vector2 p){GetViewport().WarpMouse(p);Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p});foreach(var down in new[]{true,false})Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=down});Input.FlushBufferedEvents();}
        void KeyPress(Key key){Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=true});Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=false});Input.FlushBufferedEvents();}
        void Finish(){System.IO.File.WriteAllText(_capturePath+".json",JsonSerializer.Serialize(new{Passed=true,Scenario=_matchProbeScenario,ReducedMotion=_reducedMotion,Checks=_followupChecks}));GD.Print("DUEL_FOLLOWUP_CHECK: completed");GetTree().Quit();}
    }
}
