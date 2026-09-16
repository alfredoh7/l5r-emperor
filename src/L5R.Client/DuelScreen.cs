using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Reusable player-view-only presentation. Host owns session, setup, bot and completion.</summary>
public partial class DuelScreen : Control
{
    public event Action<GameCommand>? CommandRequested;
    public event Action? PresentationFinished;
    public event Action<bool>? ReadingChanged;
    public bool InputLocked { get; set; }
    public bool ReducedMotion { get; set; }
    public bool PresentationBusy => _revealQueue.Count>0 || _revealClock>0 || _cleanupClock>0;
    public SessionSnapshot? Snapshot { get; private set; }
    public bool CanContinue => Snapshot?.View.LastDuel is {Complete:true} && !PresentationBusy;
    private CrimsonDuelStage _stage=null!;
    private TableSurface _table=null!;
    private Control _plane=null!,_hud=null!;
    private VBoxContainer _actions=null!;
    private Label _status=null!;
    private Control? _reading;
    private readonly Dictionary<string,CardTile> _tiles=[];
    private readonly Queue<DuelReveal> _revealQueue=[];
    private readonly HashSet<long> _seenReveals=[];
    private DuelReveal? _shownReveal;
    private string? _duelId,_selectedHand,_selectedFocus,_selectedInspection;
    private string[] _effectOrder=[];
    private bool _submitted,_aftermathPlayed,_animateEntry;
    internal Vector2 CardClickPoint(string id) => _table.ProjectCanvas(_tiles[id].Position + _tiles[id].Size / 2);
    private double _revealClock,_commandCooldown,_cleanupClock;
    private float _scale;
    private Vector2 _offset;
    private Rect2 _controlsRect;

    public override void _Ready()
    {
        Theme=DuelUi.Theme();SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);MouseFilter=MouseFilterEnum.Ignore;
        _stage=new CrimsonDuelStage{ReducedMotion=ReducedMotion,MatLift=110};AddChild(_stage);
        _table=new TableSurface{PlanePitch=CrimsonDuelStage.Pitch,PlaneDepth=CrimsonDuelStage.Depth};AddChild(_table);
        _plane=new Control();_table.Canvas.AddChild(_plane);
        _hud=new Control{MouseFilter=MouseFilterEnum.Ignore};AddChild(_hud);
        Resized+=Layout;Layout();
    }
    public void Present(SessionSnapshot snapshot)
    {
        if(Snapshot?.View is {} previous && previous.Version==snapshot.View.Version && previous.ViewerId==snapshot.View.ViewerId && previous.Scenario==snapshot.View.Scenario)return;
        var changedSeat=Snapshot?.View.ViewerId!=snapshot.View.ViewerId;
        _previousZones=(Snapshot?.View.Players.SelectMany(p=>p.Hand.Concat(p.Focused??[]).Concat(p.Discard))??[]).ToDictionary(c=>c.Id,c=>c.Zone);
        Snapshot=snapshot;_submitted=false;_selectedDeck=false;_dragCard=null;ClearReading();
        if(changedSeat){_selectedHand=null;_selectedFocus=null;_selectedInspection=null;}
        var record=snapshot.View.LastDuel;
        if(record?.Id!=_duelId){_animateEntry=record is not null;_duelId=record?.Id;_seenReveals.Clear();_revealQueue.Clear();_shownReveal=null;_revealClock=0;_aftermathPlayed=false;}
        foreach(var reveal in record?.Reveals??[])
            if(_seenReveals.Add(reveal.Sequence))_revealQueue.Enqueue(reveal);
        if(ReducedMotion)while(_revealQueue.TryDequeue(out var frame))_shownReveal=frame;
        if(_selectedHand is {} hand && !snapshot.View.Players[snapshot.View.ViewerId].Hand.Any(c=>c.Id==hand))_selectedHand=null;
        if(_selectedFocus is {} focus && !(snapshot.View.Players[snapshot.View.ViewerId].Focused??[]).Any(c=>c.Id==focus))_selectedFocus=null;
        var order=snapshot.Actions.FirstOrDefault(a=>a.Type=="orderDuelFocusEffects")?.TargetIds??[];
        if(!_effectOrder.Order().SequenceEqual(order.Order()))_effectOrder=order.ToArray();
        RenderCards();RenderActions();
    }
    public void ResetPresentation()
    {
        Snapshot=null;_duelId=null;_seenReveals.Clear();_revealQueue.Clear();_shownReveal=null;_revealClock=0;_aftermathPlayed=false;
        _selectedHand=null;_selectedFocus=null;_selectedInspection=null;_selectedDeck=false;_dragCard=null;_cleanupClock=0;_effectOrder=[];_commandCooldown=0;_animateEntry=false;ClearReading();
    }
    private void Layout()
    {
        if(_stage is null)return;
        var size=GetViewportRect().Size;
        _stage.Resize(size,1);_scale=_stage.ContentScale;_offset=_stage.ContentOffset;
        var density=_scale*CrimsonDuelStage.Density;
        _table.Position=_stage.TableOffset;_table.Scale=Vector2.One*density;
        _table.ResizePlane((Vector2)_stage.World.Size/density,Math.Max(.8f,density*1.2f));
        _plane.Size=_table.CanvasSize;_hud.Size=size;
        RenderCards(false);RenderActions();
    }
    private static Vector2 ToPlane(Vector2 point)=>TableSurface.UnprojectOffset((point+new Vector2(0,110)-CrimsonDuelStage.StageCanvas/2)/CrimsonDuelStage.Density,CrimsonDuelStage.Pitch,CrimsonDuelStage.Depth);
    private void Select(CardView card)
    {
        if(card.Definition is null||PresentationBusy||InputLocked)return;
        _selectedDeck=false;
        _selectedInspection=card.Id;
        if(card.Zone==Zone.Hand && card.OwnerId==Snapshot!.View.ViewerId)_selectedHand=card.Id;
        else if(card.Zone==Zone.Focused && card.OwnerId==Snapshot!.View.ViewerId)_selectedFocus=card.Id;
        else {Inspect(card);return;}
        foreach(var tile in _tiles.Values){tile.Highlighted=tile.Card.Id==_selectedHand||tile.Card.Id==_selectedFocus;tile.QueueRedraw();}
        RenderLanding();RenderActions();
    }
    private IEnumerable<ActionOption> Legal=>Snapshot?.Actions.Where(a=>a.Enabled&&a.Command is not null)??[];
    private void RenderActions()
    {
        if(_hud is null)return;
        ClearReading();
        foreach(var c in _hud.GetChildren()){_hud.RemoveChild(c);c.QueueFree();}_reading=null;
        if(Snapshot is null)return;
        var view=Snapshot.View;var stage=view.PendingDuel?.Stage;
        var ribbon=new Control{Position=_offset+new Vector2(370,275)*_scale,Size=new Vector2(932,42)*_scale,MouseFilter=MouseFilterEnum.Ignore};
        _hud.AddChild(ribbon);ribbon.Visible=view.LastDuel is not null;
        _status=DuelUi.Text(Status(),19,Palette.Paper);_status.HorizontalAlignment=HorizontalAlignment.Center;
        _status.VerticalAlignment=VerticalAlignment.Center;_status.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);ribbon.AddChild(_status);
        _status.AddThemeColorOverride("font_shadow_color",Colors.Black);_status.AddThemeConstantOverride("shadow_offset_x",1);_status.AddThemeConstantOverride("shadow_offset_y",2);
        _status.AddThemeFontSizeOverride("font_size",Math.Max(13,(int)(19*_scale)));
        _controlsRect=new Rect2(_offset+new Vector2(1190,795)*_scale,new Vector2(215,130)*_scale);
        Control? inspectionParent=null;
        RenderTableLabels();
        _actions=new VBoxContainer{Position=_controlsRect.Position,Size=_controlsRect.Size};_actions.AddThemeConstantOverride("separation",Math.Max(2,(int)(4*_scale)));_hud.AddChild(_actions);
        if(PresentationBusy){Text(_shownReveal?.Kind=="Replacement"?"Duelist replacement":"Revealing focused cards…");return;}
        if(view.LastDuel?.Result is {} result)
        {
            var record=view.LastDuel;
            var outcome=result.Kind=="Refused"?"Challenge refused":result.WinnerId is null?"Equal totals · both lose":$"{(result.WinnerId==record.Challenger.Id?record.Challenger:record.Challenged).Definition!.Name} wins";
            var leftTotal=record.Challenger.OwnerId==view.ViewerId?result.ChallengerTotal:result.ChallengedTotal;
            var rightTotal=record.Challenger.OwnerId==view.ViewerId?result.ChallengedTotal:result.ChallengerTotal;
            var statName=record.Stat=="PersonalHonor"?"Personal Honor":record.Stat;
            _status.Text=outcome+(result.Kind=="Refused"?"":$"  ·  {statName}: {leftTotal} – {rightTotal}");
            if(!record.Complete)_status.Text+=$" · {view.Players[view.PriorityPlayerId].Name}’s reaction";
            if(CanContinue)
            {
                Button("View consequences",()=>ShowConsequences(record));
                if(record.Reveals.LastOrDefault() is {} reveal)Button("Review revealed cards",()=>ShowCards("Revealed cards · history",reveal.Cards,"Duel history · these cards have already moved to their final destinations."));
                Button("Continue",()=>PresentationFinished?.Invoke());return;
            }
        }
        if(stage=="Focusing")
        {
            SubmitButton("Strike",Legal.FirstOrDefault(a=>a.Type=="strike")?.Command);
            var discard=Legal.Where(a=>a.Command!.Choice=="discard").ToArray();
            if(discard.Length>0)
            {
                var row=new HBoxContainer();_actions.AddChild(row);inspectionParent=row;
                Button("Focus from discard…",()=>ChoicePanel("Choose a public discard",discard),row).SizeFlagsHorizontal=SizeFlags.ExpandFill;
            }
        }
        else if(stage=="DuelistRefocus" && view.PriorityPlayerId==view.ViewerId)
        {
            SubmitButton("Replace from selected hand card",Legal.FirstOrDefault(a=>a.Command!.CardId==_selectedFocus&&a.Command.TargetId==_selectedHand&&_selectedFocus is not null&&_selectedHand is not null)?.Command);
            SubmitButton("Replace from deck",Legal.FirstOrDefault(a=>a.Command!.CardId==_selectedFocus&&a.Command.Choice=="deck"&&_selectedFocus is not null)?.Command);
            SubmitButton("Keep focused cards",Legal.FirstOrDefault(a=>a.Command!.CardId is null)?.Command);
        }
        else if(stage=="OrderingEffects" && view.PriorityPlayerId==view.ViewerId)
        {
            Button("Arrange Focus Effects…",ShowOrder);
        }
        else foreach(var option in Legal.Where(a=>a.Type is "acceptDuel" or "declineDuel" or "declineReaction" or "useAbility" or "overconfidentDraw"))
            if(view.LastDuel is not null)SubmitButton(option.Label,option.Command);
        if(_selectedInspection is {} selected && _tiles.TryGetValue(selected,out var tile))Button(inspectionParent is null?"Inspect selected card":"Inspect",()=>Inspect(tile.Card),inspectionParent);
    }
    private string Status()
    {
        var v=Snapshot!.View;
        return v.PendingChoice is {} pending ? $"{v.Players[v.PriorityPlayerId].Name} · {pending.Prompt}" : v.PendingDuel?.Stage switch {
            "AwaitingAccept"=>$"{v.Players[v.PriorityPlayerId].Name} · accept or refuse the challenge", "Focusing"=>v.PriorityPlayerId==v.ViewerId?(Legal.Any(a=>a.Type=="focus")?"Your decision · commit a card or Strike":"Your decision · Strike"):$"{v.Players[v.PriorityPlayerId].Name} is deciding",
            "DuelistRefocus"=>$"{v.Players[v.PriorityPlayerId].Name} · Duelist: select a focus card and its replacement, or keep your focus", "OrderingEffects"=>$"{v.Players[v.PriorityPlayerId].Name} · choose the order of Focus Effects", _=>"" };
    }
    private void Text(string text){var label=DuelUi.Wrapped(text,Math.Max(13,(int)(16*_scale)),Palette.Paper);_actions.AddChild(label);}
    private Button Button(string text,Action pressed,Control? parent=null)
    {
        var primary=text is "Strike" or "Continue" or "Resolve in this order";
        var buttonScale=_reading is null?_scale:1;
        var b=DuelUi.Button(text,()=>{if(!_submitted&&_commandCooldown<=0)pressed();},primary,buttonScale,primary?19:17);
        b.CustomMinimumSize=new Vector2(0,Math.Max(25,(primary?48:34)*buttonScale));
        foreach(var state in new[]{"normal","hover","pressed","disabled"})
        {
            var style=(StyleBoxTexture)b.GetThemeStylebox(state);style.ContentMarginTop=style.ContentMarginBottom=4*buttonScale;
        }
        b.TooltipText=text;(parent??_actions).AddChild(b);return b;
    }
    private Button SubmitButton(string text,GameCommand? command,Control? parent=null)
    {var b=Button(text,()=>Submit(command!),parent);b.Disabled=command is null||_submitted||_commandCooldown>0;return b;}
    private void Submit(GameCommand command)
    {
        if(InputLocked||_submitted||_commandCooldown>0||PresentationBusy||Snapshot?.View.Version!=command.ExpectedVersion)return;
        _submitted=true;_commandCooldown=.18;foreach(var button in _actions.GetChildren().OfType<Button>())button.Disabled=true;
        CommandRequested?.Invoke(command);
    }
    public void SubmissionFinished(){_submitted=false;RenderActions();}
    private VBoxContainer Panel(string title,float height=620)
    {
        ClearReading();var shade=new ColorRect{Color=new Color(0,0,0,.68f),Size=Size,MouseFilter=MouseFilterEnum.Stop};_hud.AddChild(shade);_reading=shade;ReadingChanged?.Invoke(true);
        return DuelUi.Modal(shade,Size,new Vector2(690,height),title);
    }
    private void ChoicePanel(string title,ActionOption[] choices)
    {
        var box=Panel(title,Math.Clamp(160+choices.Length*70,280,620));
        foreach(var a in choices)
        {
            var row=new HBoxContainer();box.AddChild(row);SubmitButton(a.Label,a.Command,row).SizeFlagsHorizontal=SizeFlags.ExpandFill;
            var card=Snapshot!.View.Players.SelectMany(p=>p.Discard).FirstOrDefault(c=>c.Id==a.CardId);
            if(card is not null)Button("Inspect",()=>Inspect(card),row).AutowrapMode=TextServer.AutowrapMode.Off;
        }
        Button("Back",ClearReading,box);
    }
    private void ShowOrder()
    {
        var box=Panel("Focus Effect order",Math.Min(620,230+_effectOrder.Length*54));
        var cards=Snapshot!.View.LastDuel!.Reveals.Last().Cards.ToDictionary(c=>c.Id);
        for(var i=0;i<_effectOrder.Length;i++)
        {var index=i;var id=_effectOrder[i];Button($"{i+1}. {Snapshot.View.Players[cards[id].OwnerId].Name} · {cards[id].Definition!.Name} · move earlier",()=>{if(index>0)(_effectOrder[index-1],_effectOrder[index])=(_effectOrder[index],_effectOrder[index-1]);ShowOrder();},box);}
        var action=Legal.Single(a=>a.Type=="orderDuelFocusEffects").Command!;
        SubmitButton("Resolve in this order",action with{CardIds=_effectOrder.ToArray()},box);Button("Back",ClearReading,box);
    }
    private void ShowConsequences(DuelRecord record)
    {
        var box=Panel("Duel outcome");var result=record.Result!;
        if(result.Kind=="Resolved")box.AddChild(DuelUi.Wrapped($"{record.Challenger.Definition!.Name}: {result.ChallengerStat} {record.Stat} → {result.ChallengerTotal}\n{record.Challenged.Definition!.Name}: {result.ChallengedStat} {record.Stat} → {result.ChallengedTotal}",19));
        foreach(var e in result.Consequences)box.AddChild(DuelUi.Wrapped(e.Message,17));
        foreach(var p in result.Provinces.Where(p=>p.After!=p.Before))box.AddChild(DuelUi.Wrapped($"{Snapshot!.View.Players[p.PlayerId].Name} · province {p.Index+1}: {p.Before} → {p.After} strength",17));
        foreach(var id in result.EffectOrder){var card=record.Reveals.Last().Cards.First(c=>c.Id==id);box.AddChild(DuelUi.Wrapped($"Focus Effect: {card.Definition!.Name}",17));}
        box.AddChild(DuelUi.Wrapped($"Returns to {result.ReturnPhase} · {Snapshot!.View.Players[result.ReturnPriority].Name} has priority.",17));Button("Back",ClearReading,box);
    }
    private void Inspect(CardView card)
    {
        if(card.Definition is null)return;
        ClearReading();var shade=new ColorRect{Color=new Color(0,0,0,.65f),Size=Size};_hud.AddChild(shade);_reading=shade;ReadingChanged?.Invoke(true);
        var h=Math.Min(700,Size.Y*.78f);var tile=new CardTile(card with{Bowed=false},true,_=>ClearReading(),_=>{})
        {FaceHeight=h,Size=Vector2.One*(h+12),Position=Size/2-Vector2.One*(h+12)/2,ReducedMotion=true,ShowTokenRail=false,Material=CardPaper.Reading};shade.AddChild(tile);
        var close=Button("Close inspection",ClearReading,shade);close.Position=new Vector2(Size.X/2-100,Size.Y*.92f);close.Size=new Vector2(200,35);
    }
    public void ClearReading(){if(_reading is null)return;_hud.RemoveChild(_reading);_reading.QueueFree();_reading=null;ReadingChanged?.Invoke(false);}
    public override void _Input(InputEvent e)
    {
        if(!IsVisibleInTree()||InputLocked)return;
        if(e is InputEventKey{Pressed:true,Echo:false} key)
        {
            if(key.Keycode==Key.Escape && _reading is not null){ClearReading();GetViewport().SetInputAsHandled();return;}
            if(_reading is not null)return;
            if(key.Keycode is Key.Left or Key.Right && Snapshot is not null && !PresentationBusy)
            {
                var hand=Snapshot.View.Players[Snapshot.View.ViewerId].Hand;
                if(hand.Length>0){var index=Array.FindIndex(hand,c=>c.Id==_selectedHand);Select(hand[(index+(key.Keycode==Key.Right?1:hand.Length-1)+hand.Length)%hand.Length]);GetViewport().SetInputAsHandled();}
            }
            if(key.Keycode==Key.D){SelectPile(Snapshot!.View.ViewerId,Zone.FateDeck);GetViewport().SetInputAsHandled();}
            if(key.Keycode==Key.Enter && SelectedFocusCommand() is {} commit){Submit(commit);GetViewport().SetInputAsHandled();}
            if(key.Keycode==Key.I && _selectedInspection is {} id && _tiles.TryGetValue(id,out var tile)){Inspect(tile.Card);GetViewport().SetInputAsHandled();}
        }
        if(e is InputEventMouse m)
        {
            if(_reading is null&&HandlePlacement(m))return;
            if(_reading is null&&!_controlsRect.HasPoint(m.Position)&&m.Position.Y>_offset.Y+310*_scale)
            {
                _table.ForwardMouse(m);
            }
            else _table.ExitMouse();
        }
    }
    public override void _Process(double delta)
    {
        if(!IsVisibleInTree())return;
        if(_commandCooldown>0){_commandCooldown-=delta;if(_commandCooldown<=0){RenderLanding();RenderActions();}}
        _stage.ReducedMotion=ReducedMotion;_stage.Advance(delta);
        if(_cleanupClock>0){_cleanupClock-=delta;if(_cleanupClock<=0){RenderCards(false);RenderActions();}}
        if(_revealClock>0){_revealClock-=delta;if(_revealClock<=0){RenderCards();RenderActions();}}
        if(_revealClock<=0&&_revealQueue.TryDequeue(out var reveal))
        {_shownReveal=reveal;_revealClock=ReducedMotion?0:1.1;RenderCards();RenderActions();return;}
        if(!PresentationBusy && !_aftermathPlayed && Snapshot?.View.LastDuel?.Result is {})
        {
            _aftermathPlayed=true;_cleanupClock=ReducedMotion?0:.65;
            RenderCards(!ReducedMotion);RenderActions();
        }
    }
}
