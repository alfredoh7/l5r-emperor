using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class DuelScreen
{
    private readonly Dictionary<(int,Zone),DuelFatePile> _piles=[];
    private readonly List<(Vector2 Point,string Text,float Width,Action? Open)> _tableLabels=[];
    private Button? _landing;
    private bool _selectedDeck;
    private Dictionary<string,Zone> _previousZones=[];
    private string? _dragCard;
    private Vector2 _dragStart;
    private bool _dragging;
    private CardTile? _dragGhost;
    private static Vector2 FocusPoint(int side,int index)=>new((side==0?332:1042)+(index switch {0=>1,1=>2,2=>0,_=>3})*106,564);
    private static Vector2 PilePoint(int side,Zone zone)=>new(side==0?155:1517,zone==Zone.FateDeck?488:660);
    private Vector2 PlanePoint(Vector2 design)=>_table.CanvasSize/2+ToPlane(design);
    internal Vector2 PileClickPoint(int owner,Zone zone)=>_table.ProjectCanvas(_piles[(owner,zone)].Position+_piles[(owner,zone)].TopFace.GetCenter());
    internal Vector2 FallenClickPoint(int owner)=>_table.ProjectCanvas(PlanePoint(new Vector2(owner==Snapshot!.View.ViewerId?515:1195,442)));
    internal Vector2 LandingClickPoint=>_landing is null?Vector2.Zero:_table.ProjectCanvas(_landing.Position+_landing.Size/2);
    internal int VisibleFocusedCount=>_tiles.Values.Count(t=>t.Card.Zone==Zone.Focused);
    internal bool HasPrivateInspection=>_reading is not null;
    internal string? SelectedInspectionId=>_selectedInspection;
    internal Vector2 FocusedClickPoint(int owner)=>CardClickPoint(_tiles.Values.First(t=>t.Card.Zone==Zone.Focused&&t.Card.OwnerId==owner).Card.Id);

    private CardTile? UprightAt(Vector2 point)=>_tiles.Values.Where(t=>t.HasMeta("upright"))
        .Reverse().FirstOrDefault(t=>t._HasPoint(t.GetGlobalTransform().AffineInverse()*point));
    internal int UprightCount=>_tiles.Values.Count(t=>t.HasMeta("upright")&&t.Card.Definition?.Type==CardType.Personality);
    internal int VisibleDiscardPiles=>_piles.Values.Count(p=>p.PileZone==Zone.FateDiscard);
    private void RenderCards(bool animate=true)
    {
        if(_plane is null)return;
        var old=_tiles.ToDictionary(p=>p.Key,p=>(p.Value.Card,Point:CardClickPoint(p.Key),Height:p.Value.HasMeta("upright")?p.Value.FaceHeight*_scale:p.Value.FaceHeight*_scale*.7f));
        var counts=_piles.ToDictionary(p=>p.Key,p=>p.Value.ShownCount);
        foreach(var parent in new[]{_plane,_uprights})foreach(var c in parent.GetChildren()){parent.RemoveChild(c);c.QueueFree();}
        _tiles.Clear();_piles.Clear();_tableLabels.Clear();_landing=null;_dragGhost=null;
        if(Snapshot is null)return;
        var view=Snapshot.View;var record=view.LastDuel;
        var participants=record is null?view.Players.SelectMany(p=>p.Army.Concat(p.Home)).Where(c=>c.Definition?.Type==CardType.Personality&&c.Definition.Id!="oracle-l5r-5095").Take(2).ToArray():[record.Challenger,record.Challenged];
        var attachments=record?.Attachments??view.Players.SelectMany(p=>p.Army.Concat(p.Home)).Where(c=>c.Zone==Zone.Attached).ToArray();
        var discarded=view.Players.SelectMany(p=>p.Discard.Concat(p.RemovedFromGame)).ToDictionary(c=>c.Id);
        foreach(var player in view.Players)
        {
            var side=player.Id==view.ViewerId?0:1;
            var deck=new DuelFatePile(player,()=>SelectPile(player.Id,Zone.FateDeck)){Size=new Vector2(190,222),Material=CardPaper.FixedTable,Selected=_selectedDeck&&side==0};
            deck.Position=PlanePoint(PilePoint(side,Zone.FateDeck))-new Vector2(deck.Size.X/2,deck.Size.Y/2+17);
            deck.SetMeta("flat_tooltip",deck.TooltipText);deck.TooltipText="";_plane.AddChild(deck);_piles[(player.Id,Zone.FateDeck)]=deck;
            if(counts.TryGetValue((player.Id,Zone.FateDeck),out var before))deck.SettleFrom(before,ReducedMotion||!animate);
            var participant=participants.FirstOrDefault(c=>c.OwnerId==player.Id);
            if(participant is not null)
            {
                var x=side==0?515:1195;var foot=_aftermathPlayed?567:502;
                var attached=attachments.Where(c=>c.AttachedTo==participant.Id&&(!_aftermathPlayed||!discarded.ContainsKey(c.Id))).ToArray();
                if(!_aftermathPlayed||!discarded.ContainsKey(participant.Id))
                {
                    // Depth staircase: complete original header and artwork strips remain exposed.
                    var step=Math.Min(78,145f/Math.Max(1,attached.Length));
                    for(var i=attached.Length-1;i>=0;i--)AddUpright(attached[i],new Vector2(x-12*(i+1),foot-step*(i+1)),340);
                    AddUpright(participant,new Vector2(x,foot),350);
                }
                else if(discarded[participant.Id].Zone is Zone.Dead or Zone.FateDiscard or Zone.DynastyDiscard)
                    AddFlat(discarded[participant.Id] with {Bowed=false},new Vector2(x,442),280,()=>ShowPile(player.Id,discarded[participant.Id].Zone));
            }
            CardView[] known;int count;
            if(_aftermathPlayed){known=[];count=0;}
            else if(_shownReveal is not null){known=_shownReveal.Cards.Where(c=>c.OwnerId==player.Id).ToArray();count=known.Length;}
            else
            {
                known=player.Focused??[];count=player.FocusCount;
                if(PresentationBusy&&record?.Reveals.LastOrDefault() is {} pending){count=pending.Cards.Count(c=>c.OwnerId==player.Id);known=[];}
            }
            for(var i=0;i<count;i++)
            {
                var card=i<known.Length?known[i]:new CardView($"anonymous-{player.Id}-{i}",null,player.Id,Zone.Focused,false,false,null,null,0);
                AddFlat(card,FocusPoint(side,i%4)+Vector2.Down*(i/4)*82,138);
            }
        }
        var hand=view.LastDuel?.Complete==true?[]:view.Players[view.ViewerId].Hand;var spacing=Math.Min(180,870f/Math.Max(1,hand.Length));
        for(var i=0;i<hand.Length;i++)AddFlat(hand[i],new Vector2(835+(i-(hand.Length-1)/2f)*spacing,778),225);
        _animateEntry=false;RenderLanding();
        if(animate&&!ReducedMotion&&_aftermathPlayed&&_cleanupClock>0)
            foreach(var (id,previous) in old.Where(p=>discarded.ContainsKey(p.Key)&&(p.Value.Card.Zone==Zone.Focused||p.Value.Card.Zone==Zone.Attached||participants.Any(c=>c.Id==p.Key))))
            {
                var destination=discarded[id];var side=destination.OwnerId==view.ViewerId?0:1;
                var target=_offset+new Vector2(side==0?515:1195,442)*_scale;
                var remains=destination.Zone==Zone.Dead||participants.Any(c=>c.Id==id);
                if(!remains)target=_offset+new Vector2(side==0?-80:1752,665)*_scale;
                var flight=new CardTile(previous.Card with {Bowed=false},true,_=>{},_=>{}){Size=Vector2.One*(previous.Height+12),FaceHeight=previous.Height,
                    Material=CardPaper.Reading,ShowTokenRail=false,ReducedMotion=true,MouseFilter=MouseFilterEnum.Ignore};
                flight.Position=_scene.GetGlobalTransform().AffineInverse()*previous.Point-flight.Size/2;flight.PivotOffset=flight.Size/2;_flights.AddChild(flight);
                var tween=flight.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(flight,"position",target-flight.Size/2,.8);tween.TweenProperty(flight,"scale",new Vector2(.72f,remains?.4f:.72f),.8);
                tween.TweenProperty(flight,"modulate",new Color(1,1,1,0),.8);tween.Chain().TweenCallback(Callable.From(flight.QueueFree));
            }
        void Configure(CardTile tile)
        {
            tile.ReducedMotion=ReducedMotion;tile.ShowTokenRail=false;tile.Material=CardPaper.Reading;
            tile.ContextHelp=InspectionContext(tile.Card);tile.SetMeta("flat_tooltip",true);tile.TooltipText="";
            tile.Highlighted=tile.Card.Id==_selectedHand||tile.Card.Id==_selectedFocus;_tiles[tile.Card.Id]=tile;
        }
        void AddUpright(CardView card,Vector2 foot,float height)
        {
            var tile=new CardTile(card with {Bowed=false},true,Select,_=>{}){Size=Vector2.One*(height+12),FaceHeight=height,MouseFilter=MouseFilterEnum.Stop};
            Configure(tile);tile.SetMeta("upright",true);tile.ReducedMotion=true;tile.Position=foot-new Vector2(tile.Size.X/2,height+6);
            // CardTile supplies viewport coordinates already for native upright controls.
            tile.ProjectToScreen=point=>point;_uprights.AddChild(tile);
        }
        void AddFlat(CardView card,Vector2 design,float height,Action? click=null)
        {
            Action<CardView> select=click is null?Select:_=>click();
            CardTile tile=card.Definition is null||card.Zone==Zone.Focused&&!card.FaceUp?new CrimsonFocusBack(card,select):new CardTile(card,true,select,_=>{});
            tile.Size=Vector2.One*(height+12);tile.FaceHeight=height;var target=PlanePoint(design)-tile.Size/2;
            Configure(tile);tile.ProjectToScreen=_table.ProjectCanvas;tile.Position=target;_plane.AddChild(tile);
            if(CinematicBusy&&_cinema==Cinema.Opening&&card.Zone==Zone.Hand)tile.Modulate=new Color(1,1,1,0);
            if(animate&&!ReducedMotion&&card.Zone==Zone.Focused)
            {
                if(_revealClock>0){tile.PivotOffset=tile.Size/2;tile.Scale=new Vector2(.05f,1);tile.CreateTween().TweenProperty(tile,"scale",Vector2.One,.3).SetTrans(Tween.TransitionType.Sine);}
                else if(!old.ContainsKey(card.Id))
                {
                    tile.Position=target+new Vector2(0,card.OwnerId==view.ViewerId?80:-50);
                    tile.CreateTween().TweenProperty(tile,"position",target,.32).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                }
            }
        }
    }

    private void RenderTableLabels()
    {
        foreach(var (point,text,width,open) in _tableLabels)
        {
            Control label;
            if(open is not null)label=DuelUi.Button(text,()=>{if(!InputLocked&&!PresentationBusy)open();},scale:_scale,fontSize:16);
            else
            {
                var panel=new PanelContainer{MouseFilter=MouseFilterEnum.Ignore};panel.AddThemeStyleboxOverride("panel",DuelUi.Plaque(_scale));
                var words=DuelUi.Text(text,Math.Max(12,(int)(18*_scale)));words.HorizontalAlignment=HorizontalAlignment.Center;panel.AddChild(words);label=panel;
            }
            label.Position=_offset+(point-new Vector2(width/2,16))*_scale;label.Size=new Vector2(width,32)*_scale;_hud.AddChild(label);
        }
    }
    private GameCommand? SelectedFocusCommand()=>Legal.FirstOrDefault(a=>a.Type=="focus"&&(_selectedDeck?a.Command!.Choice=="deck":a.Command!.CardId==_selectedHand&&a.Command.Choice is null&&_selectedHand is not null))?.Command;
    private void RenderLanding()
    {
        if(_landing is not null&&GodotObject.IsInstanceValid(_landing)){_plane.RemoveChild(_landing);_landing.QueueFree();}_landing=null;
        if(Snapshot is null||InputLocked||PresentationBusy||SelectedFocusCommand() is not {} command)return;
        var count=Snapshot.View.Players[Snapshot.View.ViewerId].FocusCount;
        _landing=new Button{Name=_selectedDeck?"Focus from deck":"Focus selected card",Text="PLACE\nFOCUS",TooltipText=_selectedDeck?"Commit the top Fate card without looking. Enter also commits.":"Commit the selected hand card. Enter also commits.",Size=new Vector2(112,156),MouseDefaultCursorShape=CursorShape.PointingHand};
        _landing.Position=PlanePoint(FocusPoint(0,count%4)+Vector2.Down*(count/4)*76)-_landing.Size/2;
        foreach(var state in new[]{"normal","hover","pressed","disabled"})_landing.AddThemeStyleboxOverride(state,DuelUi.FocusFrame(state=="hover"));
        _landing.AddThemeStyleboxOverride("focus",Palette.Box(Colors.Transparent,Palette.Paper,1,0));
        _landing.AddThemeFontOverride("font",DuelUi.Prompt);
        _landing.AddThemeColorOverride("font_color",Palette.Paper);_landing.AddThemeFontSizeOverride("font_size",27);
        _landing.Disabled=_submitted||_commandCooldown>0;_landing.Pressed+=()=>Submit(command);_plane.AddChild(_landing);
    }
    private void SelectPile(int owner,Zone zone)
    {
        if(InputLocked||PresentationBusy||_commandCooldown>0)return;
        if(zone==Zone.FateDeck)
        {
            if(owner!=Snapshot!.View.ViewerId)return;
            if(!Legal.Any(a=>a.Type=="focus"&&a.Command!.Choice=="deck"))return;
            _coachingDismissed.Add(Snapshot!.View.ViewerId);_dontShowAgain=false;
            _selectedDeck=!_selectedDeck;_selectedHand=null;_selectedInspection=null;
            RefreshSelection();RenderLanding();RenderActions();
        }
        else ShowPile(owner,zone);
    }
    private void ShowPile(int owner,Zone zone)
    {
        var player=Snapshot!.View.Players[owner];var cards=(zone==Zone.RemovedFromGame?player.RemovedFromGame:player.Discard.Where(c=>c.Zone==zone)).ToArray();
        var name=zone switch {Zone.FateDiscard=>"Fate discard",Zone.Dead=>"Fallen personalities",Zone.DynastyDiscard=>"Dynasty discard",_=>"Removed from game"};
        ShowCards($"{player.Name} · {name}",cards,"Public cards · click a face to inspect");
    }
    public void OpenPublicPiles()
    {
        if(InputLocked||PresentationBusy||Snapshot is null)return;
        var box=Panel("Public piles",520);
        foreach(var player in Snapshot.View.Players)
            foreach(var zone in new[]{Zone.FateDiscard,Zone.Dead,Zone.DynastyDiscard,Zone.RemovedFromGame})
            {
                var owner=player.Id;var z=zone;var seat=owner==Snapshot.View.ViewerId?"Your":"Opponent";
                var label=zone switch{Zone.FateDiscard=>"Fate discard",Zone.Dead=>"fallen personalities",Zone.DynastyDiscard=>"Dynasty discard",_=>"removed cards"};
                Button($"{seat} {label}",()=>ShowPile(owner,z),box);
            }
        Button("Back",ClearReading,box);
    }
    private void ShowOtherPiles(int owner)
    {
        var box=Panel($"{Snapshot!.View.Players[owner].Name} · other piles",370);
        foreach(var zone in new[]{Zone.DynastyDiscard,Zone.Dead,Zone.RemovedFromGame})
        {var z=zone;Button(zone switch{Zone.DynastyDiscard=>"Dynasty discard",Zone.Dead=>"Fallen personalities",_=>"Removed from game"},()=>ShowPile(owner,z),box);}
        Button("Back",ClearReading,box);
    }
    private void ShowCards(string title,CardView[] cards,string hint)
    {
        var box=Panel(title,620);box.AddChild(DuelUi.Wrapped(cards.Length==0?"This pile is empty.":hint,16,Palette.PaperDim));
        var scroll=new ScrollContainer{CustomMinimumSize=new Vector2(0,410),SizeFlagsVertical=SizeFlags.ExpandFill};box.AddChild(scroll);
        var grid=new GridContainer{Columns=3,SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(grid);
        foreach(var card in cards.Where(c=>c.Definition is not null))
        {
            var tile=new CardTile(card with {Bowed=false},true,Inspect,_=>{}){FaceHeight=180,CustomMinimumSize=new Vector2(195,195),ShowTokenRail=false,ReducedMotion=true,Material=CardPaper.Reading};grid.AddChild(tile);
        }
        Button("Back",ClearReading,box);
    }
    private bool HandlePlacement(InputEventMouse mouse)
    {
        if(PresentationBusy)return false;
        if(mouse is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:true})
        {
            _dragStart=mouse.Position;_dragging=false;
            var point=_table.ScreenToCanvas(mouse.Position);
            _dragCard=_tiles.Values.LastOrDefault(t=>t.Card.Zone==Zone.Hand&&t.Card.OwnerId==Snapshot!.View.ViewerId&&t._HasPoint(point-t.Position))?.Card.Id;
        }
        else if(mouse is InputEventMouseMotion && _dragCard is {} id && mouse.Position.DistanceTo(_dragStart)>10)
        {
            _dragging=true;_selectedHand=id;_selectedDeck=false;
            if(_landing is null)RenderLanding();
            if(_dragGhost is null)
            {
                var source=_tiles[id];
                _dragGhost=new CardTile(source.Card,true,_=>{},_=>{}){Size=source.Size,FaceHeight=source.FaceHeight,ShowTokenRail=false,ReducedMotion=true,Material=CardPaper.FixedTable,MouseFilter=MouseFilterEnum.Ignore,Modulate=new Color(1,1,1,.82f)};
                _plane.AddChild(_dragGhost);
            }
            _dragGhost.Position=_table.ScreenToCanvas(mouse.Position)-_dragGhost.Size/2;
        }
        else if(mouse is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:false})
        {
            var dragging=_dragging;_dragging=false;_dragCard=null;
            if(_dragGhost is not null){_plane.RemoveChild(_dragGhost);_dragGhost.QueueFree();_dragGhost=null;}
            if(dragging&&_landing is not null&&_landing.GetRect().HasPoint(_table.ScreenToCanvas(mouse.Position))&&SelectedFocusCommand() is {} command)
            {Submit(command);GetViewport().SetInputAsHandled();return true;}
        }
        return false;
    }
}
