using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool _grantProbeDone;
    private Vector2? _grantPointer;
    private void MaintainGrantPointer()
    {
        if(_grantPointer is not { } p)return;
        // The capture runs beside other native Godot windows. Keep its real OS
        // focus rather than bypassing the game's normal unfocused-input guard.
        if(!GetWindow().HasFocus())GetWindow().GrabFocus();
        GetViewport().PushInput(new InputEventMouseMotion { Position=p,GlobalPosition=p },true);
    }
    // Explicit public-snapshot artwork fixture, following the Home/Favor review harness.
    // It never registers Spirit of the Berserker as an executable card or edits engine state.
    private async void PreparePermanentGrantCapture()
    {
        if(_capturePath is null || !_captureMode.StartsWith("published-grants",StringComparison.Ordinal)) return;
        try
        {
            _autoOpponent=false;_reducedMotion=true;
            _session.StartPractice(false,true,0);
            var snapshot=_session.Snapshot!;
            var def=CardCatalog.Get("oracle-l5r-1645");
            var hero=new CardView("grant-review-hero",def,0,Zone.Home,true,false,null,null,5,3,UnitForce:5,
                GrantedKeywords:[new("Berserker","grant-review-source","Permanent")],
                GrantedEffects:[new("+2 Force","","grant-review-source","Permanent","Stat"),
                    new("Granted Reaction","After another player's action targets this Personality: Delay his destruction from the action's effects until before this turn ends.","grant-review-source","Permanent")]);
            var source=new CardDefinition("grant-review-source-definition","Spirit of the Berserker",CardType.Strategy,
                Text:"Reference source for this staged artwork review. This card is not implemented.",ScenarioOnly:false);
            var publicSource=new CardView("grant-review-source",source,0,Zone.FateDiscard,true,false,null,null,source.Force,source.Chi);
            var players=snapshot.View.Players.Select(p=>p with {
                Home=p.Home.Concat(new[]{hero with { Id=p.Id==0?hero.Id:"grant-review-opponent",OwnerId=p.Id,
                    Bowed=p.Id==1,GrantedEffects=p.Id==1?null:hero.GrantedEffects }}).ToArray(),
                Discard=p.Id==0?p.Discard.Append(publicSource).ToArray():p.Discard
            }).ToArray();
            var view=snapshot.View with { Players=players,ActivePlayerId=0,PriorityPlayerId=0,ViewerId=0,
                Phase=Phase.Action,PendingChoice=null,CurrentBattleProvince=null,UnresolvedBattlefields=[] };
            typeof(MatchSession).GetProperty(nameof(MatchSession.Snapshot))!.SetValue(_session,
                snapshot with { View=view,Actions=[],Mode="Native artwork review" });
            _uiQueue.Clear();_selected=null;_chosen=null;_selectedHomeReminder=null;
            _tableLayoutSeen=false;Render();ResetTableCamera();
            SetNotice("Artwork review · staged public grants; no gameplay commands");
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree().CreateTimer(1.5),SceneTreeTimer.SignalName.Timeout);
            while(_turnBloom.Playing)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var tile=_tiles.Single(t=>t.Card.Id==hero.Id);
            var seal=tile.TokenRail?.GrantSeal??throw new Exception("Full-game card has no permanent seal");
            var point=tile.ProjectToScreen?.Invoke(seal.GetGlobalTransform()*(seal.Size/2))??seal.GetGlobalTransform()*(seal.Size/2);
            var version=View.Version;
            GetWindow().GrabFocus();GetViewport().NotifyMouseEntered();GetViewport().WarpMouse(point);
            // Native window/layout callbacks can land after the first fitted frame.
            // Wait for the visible cap's current center to be the actual hit target.
            for(var frame=0;frame<12;frame++)
            {
                point=tile.ProjectToScreen?.Invoke(seal.GetGlobalTransform()*(seal.Size/2))??seal.GetGlobalTransform()*(seal.Size/2);
                _grantPointer=point;
                GetViewport().PushInput(new InputEventMouseMotion { Position=point,GlobalPosition=point },true);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            if(TableHovered is not PermanentGrantSeal)throw new Exception($"Pre-click projected hit missed seal: {TableHovered?.GetPath()} at {point}; local={seal.GetGlobalTransform()*(seal.Size/2)}; last={_lastTableMouse}; blocked={CameraBlocked}");
            GetViewport().PushInput(new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true },true);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GetViewport().PushInput(new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false },true);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _grantPointer=null;
            if(_selected?.Id!=hero.Id || View.Version!=version)throw new Exception($"Seal did not inspect its card without submitting a command: selected={_selected?.Id}, hover={TableHovered?.GetType().Name}, version={View.Version}/{version}");
            if(CardChanges.SourceName("grant-review-source",id=>FindCard(id)?.Definition?.Name)!="Spirit of the Berserker")
                throw new Exception("Public discard source was not resolved");
            // Source inspection must remain a read-only navigation action.
            var sourceButton=_inspector.FindChildren("*","LinkButton",true,false).OfType<LinkButton>().First(b=>b.Text.Contains("Spirit of the Berserker"));
            sourceButton.EmitSignal(BaseButton.SignalName.Pressed);
            if(_selected?.Id!=publicSource.Id||View.Version!=version)throw new Exception("Source inspection changed the match or selected the wrong card");
            SelectCard(hero);ReleaseTableFocus();
            if(_captureMode.EndsWith("detail",StringComparison.Ordinal))
            {
                while(_turnBloom.Playing)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                point=tile.ProjectToScreen?.Invoke(seal.GetGlobalTransform()*(seal.Size/2))??seal.GetGlobalTransform()*(seal.Size/2);
                _grantPointer=point;GetWindow().GrabFocus();
                GetViewport().WarpMouse(point);
                GetViewport().PushInput(new InputEventMouseMotion { Position=point,GlobalPosition=point,Relative=Vector2.One },true);
                for(var attempt=0;attempt<50&&(_tableHover is not PermanentGrantSeal||_tableTooltip is null);attempt++)
                    await ToSignal(GetTree().CreateTimer(.1),SceneTreeTimer.SignalName.Timeout);
                if(_tableHover is not PermanentGrantSeal || _tableTooltip is null || CardChangePanel.LastCardId!=hero.Id)
                    throw new Exception($"Projected seal tooltip routing failed: {_tableHover?.GetType().Name}, native={TableHovered?.GetType().Name}, point={point}, last={_lastTableMouse}, blocked={CameraBlocked}, popup={_tableTooltip is not null}, time={_tableHoverTime}, card={CardChangePanel.LastCardId}");
                IEnumerable<Node> Descendants(Node parent)=>parent.GetChildren(true).SelectMany(n=>new[]{n}.Concat(Descendants(n)));
                var panels=Descendants(GetTree().Root).OfType<CardChangePanel>().Where(p=>p.IsVisibleInTree()&&!p.IsQueuedForDeletion()).ToArray();
                if(panels.Length!=2)throw new Exception($"Expected one hover panel and one inspector panel, got {panels.Length}: {string.Join(", ",panels.Select(p=>p.GetPath()))}");
            }
            else Input.ParseInputEvent(new InputEventMouseMotion { Position=new Vector2(5,5) });
            // Let nested wrapped inspector controls settle before capturing.
            for(var i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print("GRANT_TABLE_PASS: native seal click selected correct owner without a command; public discard source inspected without a command; projected hover checked in detail mode; approved full-game skin. Staged view, not card-mechanics acceptance.");
            _grantProbeDone=true;
        }
        catch(Exception e){GD.PushError(e.ToString());GetViewport().GetTexture().GetImage().SavePng(_capturePath+".failure.png");GetTree().Quit(1);}
    }
}
