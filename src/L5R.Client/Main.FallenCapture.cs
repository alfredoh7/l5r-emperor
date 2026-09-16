using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool _fallenProbeDone;
    private async void PrepareFallenCapture()
    {
        if(_capturePath is null||!_captureMode.StartsWith("published-fallen",StringComparison.Ordinal))return;
        try
        {
            _autoOpponent=false;_reducedMotion=true;_session.StartPractice(false,true,0);
            _uiQueue.Clear();Render();
            var original=_session.Snapshot!;
            var players=FallenProbe.FixturePlayers(original.View.Players).Select(p=>p with {
                Home=p.Home.Concat(Enumerable.Range(0,2).Select(i=>FallenProbe.Example("oracle-l5r-1645","table-fallen-"+p.Id+"-"+i,p.Id,
                    Zone.Home,dishonorable:i==1,bowed:p.Id==1) with { UnitForce=p.Id==1?0:3 })).ToArray()
            }).ToArray();
            var view=original.View with {Players=players,ViewerId=_captureMode.Contains("seat-1")?1:0,ActivePlayerId=0,
                PriorityPlayerId=0,Phase=Phase.Action,PendingChoice=null,CurrentBattleProvince=null,UnresolvedBattlefields=[]};
            _presentationSnapshot=original with {View=view,Actions=[]};
            _selected=null;_chosen=null;_selectedHomeReminder=null;_motionView=null;_tableLayoutSeen=false;
            RenderBoard();RenderInspector();RenderPhaseActions();RenderTimeline();ResetTableCamera();
            SetNotice("Fallen artwork review · illustrative public state");
            for(var i=0;i<5;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            while(_turnBloom.Playing)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);
            var counters=_clanHud.GetChildren().OfType<FallenCounter>().ToArray();
            if(counters.Length!=2||counters.Any(c=>c.Count!=view.Players.Single(p=>p.Id==c.Player.Id).DeadCount))
                throw new Exception("Player counters do not display authoritative DeadCount");
            foreach(var tile in _tiles.Where(t=>t.Card.Id.StartsWith("table-fallen-")))
                if(!Mathf.IsEqualApprox(tile.VisualFaceAngle,(tile.Card.Bowed?Mathf.Pi/2:0)+(tile.Card.Dishonorable?Mathf.Pi:0)))
                    throw new Exception("Full-table dishonor orientation incorrect");
            var counter=counters.Single(c=>c.Player.Id==view.ViewerId);
            var point=counter.GetGlobalTransform()*(counter.Size/2);
            GetWindow().GrabFocus();GetViewport().WarpMouse(point);
            GetViewport().PushInput(new InputEventMouseMotion {Position=point,GlobalPosition=point},true);
            GetViewport().PushInput(new InputEventMouseButton {Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true},true);
            GetViewport().PushInput(new InputEventMouseButton {Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false},true);
            for(var i=0;i<5;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(_fallenGallery?.OwnerId!=view.ViewerId||!IsInstanceValid(_fallenWindow)||!_fallenWindow!.Visible)
                throw new Exception("Native Fallen counter click did not open the correct player's modal");
            if(!CameraBlocked)throw new Exception("Fallen modal did not block table inputs");
            if(_fallenGallery.Rows.Any(r=>r.Card.Zone!=Zone.Dead))throw new Exception("Removed/discarded cards appeared in Fallen");
            _fallenGallery.SelectCard("fallen-konomi-"+view.ViewerId);
            for(var i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(_session.Snapshot!=original)throw new Exception("Read-only navigation modified session snapshot");
            // Idle draw lists must keep their texture resources alive too.
            GC.Collect();GC.WaitForPendingFinalizers();
            for(var i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(_captureMode.EndsWith("table",StringComparison.Ordinal))
            {
                var window=_fallenWindow??throw new Exception("Fallen window unexpectedly closed before Escape test");
                window.PushInput(new InputEventKey {Keycode=Key.Escape,Pressed=true},true);
                window.PushInput(new InputEventKey {Keycode=Key.Escape,Pressed=false},true);
                for(var i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(IsInstanceValid(_fallenWindow))throw new Exception("Escape did not close Fallen");
                GetWindow().GrabFocus();
            }
            GD.Print("FALLEN_TABLE_PASS: native counter click, correct owner, modal table blocking, isolated public piles, unchanged session, full-table orientations.");
            _fallenProbeDone=true;
        }
        catch(Exception e){GD.PushError(e.ToString());GetViewport().GetTexture().GetImage().SavePng(_capturePath+".failure.png");GetTree().Quit(1);}
    }
}
