using Godot;
using L5R.Core;
using System.Reflection;
namespace L5R.Client;

// Fixture-only injection is confined to the explicit native capture path, never gameplay/networking.
public sealed partial class MatchSession
{
    internal void StartEventProbe(MatchEngine engine,int seat)
    {Disconnect();_engine=engine;_localSeat=seat;_practiceNameSeat=seat;RefreshLocal();}
}
public partial class Main
{
    private bool _eventProbeStarted;
    private void ProcessEventCapture(double delta)
    {
        _captureClock+=delta;
        if(_eventProbeStarted||_captureClock<1.5)return;
        _eventProbeStarted=true;CaptureEventDecision();
    }
    private static MatchEngine EventFixture(bool strife,bool last=false)
    {
        var s=MatchEngine.CreatePublishedTestMatch(42).State;
        CardInstance Add(string def,int owner,Zone zone,int? province=null)
        {
            var c=new CardInstance {Id="event-native-"+s.Cards.Count,DefinitionId=def,OwnerId=owner,Zone=zone,FaceUp=true,ProvinceIndex=province};s.Cards[c.Id]=c;
            if(zone==Zone.Hand)s.Players[owner].Hand.Add(c.Id);return c;
        }
        // Create the pre-trigger state; the real engine must open the pending window below.
        GameCommand trigger;
        if(strife)
        {
            s.Phase=Phase.Battle;s.ActivePlayerId=1;s.PriorityPlayerId=0;s.CurrentBattleProvince=1;s.UnresolvedBattlefields=[1];
            Add("oracle-l5r-8494",1,Zone.EventInPlay);
            var ring=Add("training-ring-of-earth-stub",0,Zone.Home);
            trigger=new("useAbility",0,s.Version,CardId:ring.Id,AbilityId:"ring-of-earth-lower",TargetPlayerId:1,Province:1);
        }
        else
        {
            s.Phase=Phase.Dynasty;s.ActivePlayerId=1;s.PriorityPlayerId=1;
            var p=s.Players[0].Provinces[1];if(p.CardId is { } old)s.Cards[old].Zone=Zone.DynastyDiscard;
            var ev=Add("oracle-l5r-2978",0,Zone.Province,1);ev.FaceUp=false;p.CardId=ev.Id;
            if(last)foreach(var province in s.Players[0].Provinces.Where(p=>p.Index!=1))province.Destroyed=true;
            trigger=new("endDynasty",1,s.Version);
        }
        var from=typeof(MatchEngine).GetMethod("FromTestState",BindingFlags.NonPublic|BindingFlags.Static)!;
        var engine=(MatchEngine)from.Invoke(null,[s])!;
        var result=engine.Execute(trigger);if(!result.Accepted)throw new Exception("Event fixture trigger: "+result.Error);
        if(engine.GetView(0).PendingChoice?.Type!=(strife?"timesOfStrifeDiscard":"harshChoices"))throw new Exception("Engine did not open the native decision");
        return engine;
    }
    private async Task EventFrames(int count=5)
    {
        for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(false);RenderingServer.ForceSync();
    }
    private async Task EventClick(Control target)
    {
        var p=target.GetGlobalRect().GetCenter();
        if(target is CardTile tile)p=ScreenCenter(tile);
        // Root viewport coordinates, through the ordinary _Input and GUI path.
        // Avoid the OS-wide input queue: a physical mouse event can otherwise be
        // interleaved between the synthetic down/up on the developer's desktop.
        var viewport=GetViewport();
        viewport.PushInput(new InputEventMouseMotion {Position=p,GlobalPosition=p},true);
        viewport.PushInput(new InputEventMouseButton {Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,ButtonMask=MouseButtonMask.Left,Pressed=true},true);
        viewport.PushInput(new InputEventMouseButton {Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false},true);
        await EventFrames();
    }
    private async void CaptureEventDecision()
    {
        try
        {
            var strife=_captureMode.Contains("strife");var other=_captureMode.Contains("opponent");var last=_captureMode.Contains("last");
            _autoOpponent=false;_reducedMotion=!_captureMode.Contains("motion");_session.StartEventProbe(EventFixture(strife,last),other?1:0);
            _uiQueue.Clear();_presentationSnapshot=null;_battlePresentationStage=0;_motionView=null;_selected=null;_chosen=null;
            Render();ResetTableCamera();await EventFrames(12);
            for(var i=0;i<180&&_turnBloom.Playing;i++)await EventFrames(1);
            void Check(bool condition,string message){if(!condition)throw new Exception(message);}
            GD.Print($"EVENT_LAYOUT: sidebar={_eventSidebar.Size}; handbar={_eventHandBar.Size}; panelEnd={_eventSidebar.GetGlobalRect().End}; viewport={GetViewport().GetVisibleRect().Size}");
            if(_captureMode.Contains("review")){GetViewport().GetTexture().GetImage().SavePng(_capturePath!);GD.Print("EVENT_REVIEW_READY");return;}
            var before=View.Version;
            Check(_eventSidebar.Visible&&!_inspectorPanel.Visible,"Generic inspector covered Event decision");
            if(other)
            {
                Check(!_eventHandBar.Visible&&_eventSidebar.KeepButton is null,"Opponent received private decision controls");
                Check(!_tiles.Any(t=>t.DecisionSelected),"Private selection exposed to opponent");
            }
            else if(strife)
            {
                Check(_eventHandBar.Visible&&_eventHandBar.Commit.Disabled,"Discard must start unselected/disabled");
                GetViewport().GetTexture().GetImage().SavePng(_capturePath+".unselected.png");
                var tile=_tiles.First(t=>t.Card.Zone==Zone.Hand&&t.Card.OwnerId==0);var id=tile.Card.Id;
                await EventClick(tile);Check(SelectedEventDiscard?.Command?.TargetId==id&&!_eventHandBar.Commit.Disabled,"Native hand click failed to select");
                Check(View.Version==before,"Selecting sent a command");
                await EventClick(_eventHandBar.Clear);Check(_eventDiscardId is null&&_eventHandBar.Commit.Disabled,"Clear did not reset the draft");
                await EventClick(tile);Check(View.Version==before,"Reselect mutated engine");
                GetViewport().PushInput(new InputEventMouseMotion {Position=new(40,230),GlobalPosition=new(40,230)},true);await EventFrames(14);
                GetViewport().GetTexture().GetImage().SavePng(_capturePath!);
                var zoom=_cameraZoom;var p=_eventHandBar.GetGlobalRect().GetCenter();GetViewport().PushInput(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.WheelUp,Pressed=true},true);GetViewport().PushInput(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.WheelUp,Pressed=false},true);
                Check(_cameraZoom==zoom,"Wheel over decision bar reached table");
                await EventClick(_eventHandBar.Commit);Check(View.Version==before+1&&View.PendingChoice is null,"Named discard did not resolve");
                Check(Self.Discard.Any(c=>c.Id==id&&c.Zone==Zone.FateDiscard),"Wrong discard destination");
                Check(!_eventHandBar.Visible&&!_eventSidebar.Visible,"Decision surface remained after resolving");
                GetViewport().GetTexture().GetImage().SavePng(_capturePath+".resolved.png");
            }
            else
            {
                GetViewport().GetTexture().GetImage().SavePng(_capturePath!);
                var destroy=_captureMode.Contains("destroy")||last;
                await EventClick(destroy?_eventSidebar.DestroyButton!:_eventSidebar.KeepButton!);
                Check(View.Version==before+1&&View.PendingChoice is null,"Harsh choice did not commit exactly once");
                Check(Self.Provinces[1].Destroyed==destroy,"Wrong province changed");
                if(last)Check(View.WinnerId==1&&View.Phase==Phase.Finished,"Last-province defeat did not remain finished");
                Check(!_eventSidebar.Visible,"Decision did not close after resolution");
                GetViewport().GetTexture().GetImage().SavePng(_capturePath+".resolved.png");
            }
            if(other)GetViewport().GetTexture().GetImage().SavePng(_capturePath!);
            GD.Print("EVENT_DECISION_PASS: "+_captureMode+"; real trigger/query commands, native controls, draft isolation and authoritative outcome.");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());RenderingServer.ForceDraw(false);RenderingServer.ForceSync();GetViewport().GetTexture().GetImage().SavePng(_capturePath+".failure.png");GetTree().Quit(1);}
    }
}
