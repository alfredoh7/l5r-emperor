using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Reusable player-view-only presentation. Host owns session, setup, bot and completion.</summary>
public partial class DuelScreen : Control
{
    public event Action<GameCommand>? CommandRequested;
    public event Action? PresentationFinished;
    public bool InputLocked { get; set; }
    public bool ReducedMotion { get; set; }
    public bool PresentationBusy => _revealQueue.Count>0 || _revealClock>0;
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
    private double _revealClock,_commandCooldown;
    private float _scale;
    private Vector2 _offset;
    private Rect2 _controlsRect;

    public override void _Ready()
    {
        Theme=Palette.MakeTheme();SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);MouseFilter=MouseFilterEnum.Ignore;
        _stage=new CrimsonDuelStage{ReducedMotion=ReducedMotion};AddChild(_stage);
        _table=new TableSurface{PlanePitch=CrimsonDuelStage.Pitch,PlaneDepth=CrimsonDuelStage.Depth};AddChild(_table);
        _plane=new Control();_table.Canvas.AddChild(_plane);
        _hud=new Control{MouseFilter=MouseFilterEnum.Ignore};AddChild(_hud);
        Resized+=Layout;Layout();
    }
    public void Present(SessionSnapshot snapshot)
    {
        if(Snapshot?.View is {} previous && previous.Version==snapshot.View.Version && previous.ViewerId==snapshot.View.ViewerId && previous.Scenario==snapshot.View.Scenario)return;
        var changedSeat=Snapshot?.View.ViewerId!=snapshot.View.ViewerId;
        Snapshot=snapshot;_submitted=false;ClearReading();
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
        _selectedHand=null;_selectedFocus=null;_selectedInspection=null;_effectOrder=[];_commandCooldown=0;_animateEntry=false;ClearReading();
    }
    private void Layout()
    {
        if(_stage is null)return;
        var size=GetViewportRect().Size;
        _stage.Resize(size,1);_scale=_stage.ContentScale;_offset=_stage.ContentOffset;
        var density=_scale*CrimsonDuelStage.Density;
        _table.Position=_offset;_table.Scale=Vector2.One*density;
        _table.ResizePlane((Vector2)_stage.World.Size/density,Math.Max(.8f,density*1.2f));
        _plane.Size=_table.CanvasSize;_hud.Size=size;
        RenderCards(false);RenderActions();
    }
    private static Vector2 ToPlane(Vector2 point)=>TableSurface.UnprojectOffset((point-CrimsonDuelStage.StageCanvas/2)/CrimsonDuelStage.Density,CrimsonDuelStage.Pitch,CrimsonDuelStage.Depth);
    private void RenderCards(bool animate=true)
    {
        if(_plane is null)return;
        var positions=_tiles.ToDictionary(p=>p.Key,p=>p.Value.Position);
        foreach(var c in _plane.GetChildren()){_plane.RemoveChild(c);c.QueueFree();}_tiles.Clear();
        if(Snapshot is null)return;
        var view=Snapshot.View;var record=view.LastDuel;
        var participants=record is null?view.Players.SelectMany(p=>p.Army.Concat(p.Home)).Where(c=>c.Definition?.Type==CardType.Personality&&c.Definition.Id!="oracle-l5r-5095").Take(2).ToArray():[record.Challenger,record.Challenged];
        var attachments=record?.Attachments??view.Players.SelectMany(p=>p.Army.Concat(p.Home)).Where(c=>c.Zone==Zone.Attached).ToArray();
        var stage=view.PendingDuel?.Stage;
        foreach(var participant in participants)
        {
            var side=participant.OwnerId==view.ViewerId?0:1;
            var center=new Vector2(side==0?562:1110,555);
            var attached=attachments.Where(c=>c.AttachedTo==participant.Id).ToArray();
            for(var i=attached.Length-1;i>=0;i--)Add(attached[i],center+new Vector2((side==0?-1:1)*(70+i*65),10+i*12),340);
            Add(participant,center,460);
            var owner=view.Players[participant.OwnerId];
            CardView[] known;int count;
            if(_shownReveal is not null){known=_shownReveal.Cards.Where(c=>c.OwnerId==owner.Id).ToArray();count=known.Length;}
            else {known=owner.Focused??[];count=owner.FocusCount;if(PresentationBusy && record?.Reveals.LastOrDefault() is {} pending){count=pending.Cards.Count(c=>c.OwnerId==owner.Id);known=[];}}
            for(var i=0;i<count;i++)
            {
                var c=i<known.Length?known[i]:new CardView($"anonymous-{owner.Id}-{i}",null,owner.Id,Zone.Focused,false,false,null,null,0);
                Add(c,new Vector2((side==0?282:965)+i*144,725),200);
            }
        }
        var hand=view.Players[view.ViewerId].Hand;
        var step=Math.Min(100,620f/Math.Max(1,hand.Length));
        for(var i=0;i<hand.Length;i++)Add(hand[i],new Vector2(730+(i-(hand.Length-1)/2f)*step,855),196);
        _animateEntry=false;
        void Add(CardView card,Vector2 design,float height)
        {
            CardTile tile=card.Definition is null || card.Zone==Zone.Focused&&!card.FaceUp?new CrimsonFocusBack(card,Select):new CardTile(card,true,Select,_=>{});
            tile.Size=Vector2.One*(height+12);tile.FaceHeight=height;
            var target=_table.CanvasSize/2+ToPlane(design)-tile.Size/2;
            tile.Position=target;tile.ReducedMotion=ReducedMotion;tile.ShowTokenRail=false;tile.Material=CardPaper.FixedTable;
            tile.ProjectToScreen=_table.ProjectCanvas;tile.SetMeta("flat_tooltip",true);
            tile.Highlighted=card.Id==_selectedHand||card.Id==_selectedFocus;
            _plane.AddChild(tile);_tiles[card.Id]=tile;
            if(_aftermathPlayed&&view.Players.SelectMany(p=>p.Discard).Any(c=>c.Id==card.Id&&c.Zone==Zone.Dead))tile.Modulate=new Color(.55f,.55f,.55f,.6f);
            if(animate&&!ReducedMotion)
            {
                if(PresentationBusy && card.Zone==Zone.Focused) { tile.PivotOffset=tile.Size/2;tile.Scale=new Vector2(.05f,1);tile.CreateTween().TweenProperty(tile,"scale",Vector2.One,.3).SetTrans(Tween.TransitionType.Sine); }
                if(_animateEntry && (participants.Any(p=>p.Id==card.Id)||card.Zone==Zone.Attached))tile.Position=target+new Vector2(card.OwnerId==view.ViewerId?-480:480,120);
                else if(positions.TryGetValue(card.Id,out var old)&&old.DistanceTo(target)>2)tile.Position=old;
                else if(!positions.ContainsKey(card.Id)){tile.Modulate=new Color(1,1,1,0);tile.Position=target+new Vector2(0,45);}
                var tween=tile.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tween.TweenProperty(tile,"position",target,.38);tween.TweenProperty(tile,"modulate",Colors.White,.38);
            }
        }
    }
    private void Select(CardView card)
    {
        if(card.Definition is null||PresentationBusy)return;
        _selectedInspection=card.Id;
        if(card.Zone==Zone.Hand && card.OwnerId==Snapshot!.View.ViewerId)_selectedHand=card.Id;
        else if(card.Zone==Zone.Focused && card.OwnerId==Snapshot!.View.ViewerId)_selectedFocus=card.Id;
        else {Inspect(card);return;}
        foreach(var tile in _tiles.Values){tile.Highlighted=tile.Card.Id==_selectedHand||tile.Card.Id==_selectedFocus;tile.QueueRedraw();}
        RenderActions();
    }
    private IEnumerable<ActionOption> Legal=>Snapshot?.Actions.Where(a=>a.Enabled&&a.Command is not null)??[];
    private void RenderActions()
    {
        if(_hud is null)return;
        foreach(var c in _hud.GetChildren()){_hud.RemoveChild(c);c.QueueFree();}_reading=null;
        if(Snapshot is null)return;
        var view=Snapshot.View;var stage=view.PendingDuel?.Stage;
        _status=Palette.Text(Status(),20,Palette.Paper);_status.Position=_offset+new Vector2(335,370)*_scale;
        _status.Size=new Vector2(1020,35)*_scale;_status.HorizontalAlignment=HorizontalAlignment.Center;
        _status.AddThemeColorOverride("font_shadow_color",Colors.Black);_status.AddThemeConstantOverride("shadow_offset_x",1);_status.AddThemeConstantOverride("shadow_offset_y",2);
        _status.AddThemeFontSizeOverride("font_size",Math.Max(14,(int)(20*_scale)));_hud.AddChild(_status);
        _controlsRect=new Rect2(_offset+new Vector2(1080,785)*_scale,new Vector2(340,155)*_scale);
        Control? inspectionParent=null;
        _actions=new VBoxContainer{Position=_controlsRect.Position,Size=_controlsRect.Size};_actions.AddThemeConstantOverride("separation",4);_hud.AddChild(_actions);
        if(PresentationBusy){Text(_shownReveal?.Kind=="Replacement"?"Duelist replacement":"Revealing focused cards…");return;}
        if(view.LastDuel?.Result is {} result)
        {
            var record=view.LastDuel;
            var outcome=result.Kind=="Refused"?"Challenge refused":result.WinnerId is null?"Equal totals · both lose":$"{(result.WinnerId==record.Challenger.Id?record.Challenger:record.Challenged).Definition!.Name} wins";
            var leftTotal=record.Challenger.OwnerId==view.ViewerId?result.ChallengerTotal:result.ChallengedTotal;
            var rightTotal=record.Challenger.OwnerId==view.ViewerId?result.ChallengedTotal:result.ChallengerTotal;
            var statName=record.Stat=="PersonalHonor"?"Personal Honor":record.Stat;
            _status.Text=outcome+(result.Kind=="Refused"?"":$"  ·  {statName}: {leftTotal} – {rightTotal}");
            if(CanContinue)
            {
                Button("View consequences",()=>ShowConsequences(record));Button("Continue",()=>PresentationFinished?.Invoke());return;
            }
        }
        if(stage=="Focusing")
        {
            var hand=Legal.FirstOrDefault(a=>a.Type=="focus"&&a.Command!.CardId==_selectedHand&&a.Command.Choice is null);
            SubmitButton("Focus selected card",hand?.Command);
            SubmitButton("Focus from deck",Legal.FirstOrDefault(a=>a.Type=="focus"&&a.Command!.Choice=="deck")?.Command);
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
        if(_selectedInspection is {} selected && _tiles.TryGetValue(selected,out var tile))Button("Inspect selected card",()=>Inspect(tile.Card),inspectionParent);
        if(view.PriorityPlayerId!=view.ViewerId)Text($"Waiting for {view.Players[view.PriorityPlayerId].Name}");
    }
    private string Status()
    {
        var v=Snapshot!.View;
        return v.PendingChoice?.Prompt ?? v.PendingDuel?.Stage switch {
            "AwaitingAccept"=>"The challenge awaits an answer", "Focusing"=>$"{v.Players[v.PriorityPlayerId].Name} · Focus or Strike",
            "DuelistRefocus"=>"Duelist · select a focus card, then its replacement; or keep your focus", "OrderingEffects"=>"Choose the order of Focus Effects", _=>"" };
    }
    private void Text(string text){var label=Palette.Wrapped(text,Math.Max(13,(int)(16*_scale)),Palette.Paper);_actions.AddChild(label);}
    private Button Button(string text,Action pressed,Control? parent=null)
    {
        var b=new Button{Text=text,CustomMinimumSize=new Vector2(0,Math.Max(26,32*_scale)),TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis,TooltipText=text};
        b.AddThemeFontSizeOverride("font_size",Math.Max(13,(int)(17*_scale)));foreach(var state in new[]{"normal","hover","pressed","disabled","focus"})
            b.AddThemeStyleboxOverride(state,Palette.Box(new Color(state=="disabled"?"252326":"17120f"),new Color(state=="disabled"?"5c5650":"ba9658"),4));
        (parent??_actions).AddChild(b);b.Pressed+=()=>{if(!_submitted&&_commandCooldown<=0)pressed();};return b;
    }
    private void SubmitButton(string text,GameCommand? command,Control? parent=null)
    {var b=Button(text,()=>Submit(command!),parent);b.Disabled=command is null||_submitted||_commandCooldown>0;}
    private void Submit(GameCommand command)
    {
        if(_submitted||_commandCooldown>0||PresentationBusy||Snapshot?.View.Version!=command.ExpectedVersion)return;
        _submitted=true;_commandCooldown=.18;foreach(var button in _actions.GetChildren().OfType<Button>())button.Disabled=true;
        CommandRequested?.Invoke(command);
    }
    public void SubmissionFinished(){_submitted=false;RenderActions();}
    private VBoxContainer Panel(string title)
    {
        ClearReading();var shade=new ColorRect{Color=new Color(0,0,0,.68f),Size=Size,MouseFilter=MouseFilterEnum.Stop};_hud.AddChild(shade);_reading=shade;
        var panel=new PanelContainer{Position=Size/2-new Vector2(300,280),Size=new Vector2(600,560)};shade.AddChild(panel);
        panel.AddThemeStyleboxOverride("panel",Palette.Box(new Color("16151b"),Palette.Gold,16));
        var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);
        var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(box);box.AddChild(Palette.Text(title,25,Palette.Gold));return box;
    }
    private void ChoicePanel(string title,ActionOption[] choices)
    {
        var box=Panel(title);
        foreach(var a in choices)
        {
            var row=new HBoxContainer();box.AddChild(row);SubmitButton(a.Label,a.Command,row);
            var card=Snapshot!.View.Players.SelectMany(p=>p.Discard).FirstOrDefault(c=>c.Id==a.CardId);
            if(card is not null)Button("Inspect",()=>Inspect(card),row);
        }
        Button("Back",ClearReading,box);
    }
    private void ShowOrder()
    {
        var box=Panel("Focus Effect order");
        var cards=Snapshot!.View.LastDuel!.Reveals.Last().Cards.ToDictionary(c=>c.Id);
        for(var i=0;i<_effectOrder.Length;i++)
        {var index=i;var id=_effectOrder[i];Button($"{i+1}. {Snapshot.View.Players[cards[id].OwnerId].Name} · {cards[id].Definition!.Name} · move earlier",()=>{if(index>0)(_effectOrder[index-1],_effectOrder[index])=(_effectOrder[index],_effectOrder[index-1]);ShowOrder();},box);}
        var action=Legal.Single(a=>a.Type=="orderDuelFocusEffects").Command!;
        SubmitButton("Resolve in this order",action with{CardIds=_effectOrder.ToArray()},box);Button("Back",ClearReading,box);
    }
    private void ShowConsequences(DuelRecord record)
    {
        var box=Panel("Duel outcome");var result=record.Result!;
        if(result.Kind=="Resolved")box.AddChild(Palette.Wrapped($"{record.Challenger.Definition!.Name}: {result.ChallengerStat} {record.Stat} → {result.ChallengerTotal}\n{record.Challenged.Definition!.Name}: {result.ChallengedStat} {record.Stat} → {result.ChallengedTotal}",19));
        foreach(var e in result.Consequences)box.AddChild(Palette.Wrapped(e.Message,17));
        foreach(var p in result.Provinces.Where(p=>p.After!=p.Before))box.AddChild(Palette.Wrapped($"{Snapshot!.View.Players[p.PlayerId].Name} · province {p.Index+1}: {p.Before} → {p.After} strength",17));
        foreach(var id in result.EffectOrder){var card=record.Reveals.Last().Cards.First(c=>c.Id==id);box.AddChild(Palette.Wrapped($"Focus Effect: {card.Definition!.Name}",17));}
        box.AddChild(Palette.Wrapped($"Returns to {result.ReturnPhase} · {Snapshot!.View.Players[result.ReturnPriority].Name} has priority.",17));Button("Back",ClearReading,box);
    }
    private void Inspect(CardView card)
    {
        if(card.Definition is null)return;
        ClearReading();var shade=new ColorRect{Color=new Color(0,0,0,.65f),Size=Size};_hud.AddChild(shade);_reading=shade;
        var h=Math.Min(700,Size.Y*.78f);var tile=new CardTile(card with{Bowed=false},true,_=>ClearReading(),_=>{})
        {FaceHeight=h,Size=Vector2.One*(h+12),Position=Size/2-Vector2.One*(h+12)/2,ReducedMotion=true,ShowTokenRail=false,Material=CardPaper.Reading};shade.AddChild(tile);
        var close=Button("Close inspection",ClearReading,shade);close.Position=new Vector2(Size.X/2-100,Size.Y*.92f);close.Size=new Vector2(200,35);
    }
    public void ClearReading(){if(_reading is null)return;_hud.RemoveChild(_reading);_reading.QueueFree();_reading=null;}
    public override void _Input(InputEvent e)
    {
        if(!IsVisibleInTree()||InputLocked)return;
        if(e is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Escape}&&_reading is not null){ClearReading();GetViewport().SetInputAsHandled();return;}
        if(e is InputEventMouse m)
        {
            if(_reading is null&&m.Position.Y>_offset.Y+400*_scale&&!_controlsRect.HasPoint(m.Position))_table.ForwardMouse(m);
            else _table.ExitMouse();
        }
    }
    public override void _Process(double delta)
    {
        if(!IsVisibleInTree())return;
        if(_commandCooldown>0){_commandCooldown-=delta;if(_commandCooldown<=0)RenderActions();}
        _stage.ReducedMotion=ReducedMotion;_stage.Advance(delta);
        if(_revealClock>0){_revealClock-=delta;if(_revealClock<=0){RenderCards();RenderActions();}}
        if(!PresentationBusy && !_aftermathPlayed && Snapshot?.View.LastDuel?.Result is {})
        {
            _aftermathPlayed=true;
            foreach(var card in Snapshot.View.Players.SelectMany(p=>p.Discard).Where(c=>c.Zone==Zone.Dead))
                if(_tiles.TryGetValue(card.Id,out var tile))
                {
                    if(ReducedMotion)tile.Modulate=new Color(.55f,.55f,.55f,.6f);
                    else tile.CreateTween().TweenProperty(tile,"modulate",new Color(.55f,.55f,.55f,.6f),.5);
                }
        }
        if(_revealClock<=0&&_revealQueue.TryDequeue(out var reveal))
        {_shownReveal=reveal;_revealClock=ReducedMotion?0:.8;RenderCards();RenderActions();}
    }
}
