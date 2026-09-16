using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class DuelScreen
{
    private readonly Dictionary<(int,Zone),TablePile> _piles=[];
    private readonly List<(Vector2 Point,string Text,float Width,Action? Open)> _tableLabels=[];
    private Button? _landing;
    private bool _selectedDeck;
    private Dictionary<string,Zone> _previousZones=[];
    private string? _dragCard;
    private Vector2 _dragStart;
    private bool _dragging;
    private CardTile? _dragGhost;
    private static Vector2 FocusPoint(int side,int index)=>new((side==0?430:910)+(index switch {0=>1,1=>2,2=>0,_=>3})*110,602);
    private static Vector2 PilePoint(int side,Zone zone)=>new(side==0?300:1372,zone==Zone.FateDeck?453:610);
    private Vector2 PlanePoint(Vector2 design)=>_table.CanvasSize/2+ToPlane(design);
    internal Vector2 PileClickPoint(int owner,Zone zone)=>_table.ProjectCanvas(_piles[(owner,zone)].Position+_piles[(owner,zone)].TopFace.GetCenter());
    internal Vector2 FallenClickPoint(int owner)=>_table.ProjectCanvas(PlanePoint(new Vector2(owner==Snapshot!.View.ViewerId?290:1382,678)));
    internal Vector2 LandingClickPoint=>_landing is null?Vector2.Zero:_table.ProjectCanvas(_landing.Position+_landing.Size/2);
    internal int VisibleFocusedCount=>_tiles.Values.Count(t=>t.Card.Zone==Zone.Focused);
    internal bool HasPrivateInspection=>_reading is not null;
    internal string? SelectedInspectionId=>_selectedInspection;
    internal Vector2 FocusedClickPoint(int owner)=>CardClickPoint(_tiles.Values.First(t=>t.Card.Zone==Zone.Focused&&t.Card.OwnerId==owner).Card.Id);

    private void RenderCards(bool animate=true)
    {
        if(_plane is null)return;
        var old=_tiles.ToDictionary(p=>p.Key,p=>(p.Value.Card,p.Value.Position,p.Value.Size,p.Value.FaceHeight));
        var counts=_piles.ToDictionary(p=>p.Key,p=>p.Value.ShownCount);
        foreach(var c in _plane.GetChildren()){_plane.RemoveChild(c);c.QueueFree();}
        _tiles.Clear();_piles.Clear();_tableLabels.Clear();_landing=null;_dragGhost=null;
        if(Snapshot is null)return;
        var view=Snapshot.View;var record=view.LastDuel;
        var participants=record is null?view.Players.SelectMany(p=>p.Army.Concat(p.Home)).Where(c=>c.Definition?.Type==CardType.Personality&&c.Definition.Id!="oracle-l5r-5095").Take(2).ToArray():[record.Challenger,record.Challenged];
        var attachments=record?.Attachments??view.Players.SelectMany(p=>p.Army.Concat(p.Home)).Where(c=>c.Zone==Zone.Attached).ToArray();
        var discarded=view.Players.SelectMany(p=>p.Discard.Concat(p.RemovedFromGame)).ToDictionary(c=>c.Id);
        foreach(var player in view.Players)
        {
            var side=player.Id==view.ViewerId?0:1;
            foreach(var zone in new[]{Zone.FateDeck,Zone.FateDiscard})
            {
                // Core's public view supplies counts and identities. Discard ordering is
                // not in that contract; the visible face is a representative public card.
                var visiblePlayer=player;
                if(!_aftermathPlayed && record?.Result is not null)
                {
                    var moving=(record.Reveals.LastOrDefault()?.Cards??[]).Concat(attachments).Select(c=>c.Id).ToHashSet();
                    visiblePlayer=player with {Discard=player.Discard.Where(c=>!moving.Contains(c.Id)).ToArray()};
                }
                var recent=_aftermathPlayed&&zone==Zone.FateDiscard?record?.Reveals.LastOrDefault()?.Cards.LastOrDefault(c=>c.OwnerId==player.Id&&player.Discard.Any(d=>d.Id==c.Id&&d.Zone==zone))?.Definition:null;
                var pile=new TablePile(visiblePlayer,zone,()=>SelectPile(player.Id,zone),recent){Size=new Vector2(220,zone==Zone.FateDeck?195:182),Material=CardPaper.FixedTable};
                pile.Position=PlanePoint(PilePoint(side,zone))-new Vector2(pile.Size.X/2,pile.Size.Y/2+17);
                pile.SetMeta("flat_tooltip",pile.TooltipText);pile.TooltipText="";
                _plane.AddChild(pile);_piles[(player.Id,zone)]=pile;
                if(counts.TryGetValue((player.Id,zone),out var before))pile.SettleFrom(before,ReducedMotion||!animate);
            }
            var participant=participants.FirstOrDefault(c=>c.OwnerId==player.Id);
            if(participant is not null)
            {
                var center=new Vector2(side==0?548:1124,453);
                // Retained public participant portrait; this never changes its gameplay location.
                var attached=attachments.Where(c=>c.AttachedTo==participant.Id&&(!_aftermathPlayed||!discarded.ContainsKey(c.Id))).ToArray();
                for(var i=attached.Length-1;i>=0;i--)Add(attached[i],center+new Vector2((side==0?-1:1)*(50+i*27),8+i*8),255);
                Add(participant,center,340);
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
                // The normal four cards never move as commitments accumulate. Larger
                // engine-supported sets get a second row rather than dropping a card.
                Add(card,FocusPoint(side,i%4)+Vector2.Down*(i/4)*76,132);
            }
            var dead=player.Discard.Where(c=>c.Zone==Zone.Dead).ToArray();
            if(_aftermathPlayed&&dead.Length>0)
            {
                var fallen=new CardTile(dead[0] with {Bowed=true},true,_=>ShowPile(player.Id,Zone.Dead),_=>{})
                {Size=new Vector2(132,132),FaceHeight=120,ReducedMotion=true,ShowTokenRail=false,Material=CardPaper.FixedTable};
                fallen.Position=PlanePoint(new Vector2(side==0?290:1382,678))-fallen.Size/2;
                fallen.ProjectToScreen=_table.ProjectCanvas;fallen.SetMeta("flat_tooltip",true);fallen.TooltipText="";fallen.ContextHelp="Fallen personality · click to inspect the pile.";_plane.AddChild(fallen);
            }
            if(player.Discard.Any(c=>c.Zone==Zone.DynastyDiscard)||player.RemovedFromGame.Length>0)
                _tableLabels.Add((new Vector2(side==0?290:1382,748),"Other piles…",170,()=>ShowOtherPiles(player.Id)));
        }
        var hand=CanContinue?[]:view.Players[view.ViewerId].Hand;var step=Math.Min(104,640f/Math.Max(1,hand.Length));
        for(var i=0;i<hand.Length;i++)Add(hand[i],new Vector2(820+(i-(hand.Length-1)/2f)*step,680),145);
        _animateEntry=false;RenderLanding();
        if(animate&&!ReducedMotion&&_aftermathPlayed&&_cleanupClock>0)
        {
            foreach(var (id,previous) in old.Where(p=>!_tiles.ContainsKey(p.Key)&&discarded.ContainsKey(p.Key)))
            {
                var destination=discarded[id];var side=destination.OwnerId==view.ViewerId?0:1;
                var point=destination.Zone==Zone.FateDiscard?PilePoint(side,Zone.FateDiscard):new Vector2(side==0?290:1382,destination.Zone==Zone.Dead?678:748);
                var flight=new CardTile(previous.Card,true,_=>{},_=>{}){Size=previous.Size,FaceHeight=previous.FaceHeight,Position=previous.Position,Material=CardPaper.FixedTable,ShowTokenRail=false,MouseFilter=MouseFilterEnum.Ignore,ReducedMotion=true,PivotOffset=previous.Size/2};
                _plane.AddChild(flight);var tween=flight.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(flight,"position",PlanePoint(point)-previous.Size/2,.6);
                tween.TweenProperty(flight,"scale",Vector2.One*.5f,.6);
                tween.TweenProperty(flight,"modulate",new Color(1,1,1,0),.6);
            }
        }
        void Add(CardView card,Vector2 design,float height)
        {
            CardTile tile=card.Definition is null||card.Zone==Zone.Focused&&!card.FaceUp?new CrimsonFocusBack(card,Select):new CardTile(card,true,Select,_=>{});
            tile.Size=Vector2.One*(height+12);tile.FaceHeight=height;var target=PlanePoint(design)-tile.Size/2;
            tile.Position=target;tile.ReducedMotion=ReducedMotion;tile.ShowTokenRail=false;tile.Material=CardPaper.FixedTable;
            tile.ContextHelp=InspectionContext(card);tile.ProjectToScreen=_table.ProjectCanvas;tile.SetMeta("flat_tooltip",true);tile.TooltipText="";
            tile.Highlighted=card.Id==_selectedHand||card.Id==_selectedFocus;
            _plane.AddChild(tile);_tiles[card.Id]=tile;
            if(animate&&!ReducedMotion)
            {
                if(_revealClock>0&&card.Zone==Zone.Focused){tile.PivotOffset=tile.Size/2;tile.Scale=new Vector2(.05f,1);tile.CreateTween().TweenProperty(tile,"scale",Vector2.One,.3).SetTrans(Tween.TransitionType.Sine);}
                if(_animateEntry&&(participants.Any(p=>p.Id==card.Id)||card.Zone==Zone.Attached))tile.Position=target+new Vector2(card.OwnerId==view.ViewerId?-480:480,120);
                else if(old.TryGetValue(card.Id,out var previous)&&previous.Position.DistanceTo(target)>2)tile.Position=previous.Position;
                else if(card.Zone==Zone.Focused&&!old.ContainsKey(card.Id))
                {
                    var side=card.OwnerId==view.ViewerId?0:1;
                    var source=_previousZones.GetValueOrDefault(card.Id);
                    if(source is Zone.FateDiscard or Zone.DynastyDiscard)tile.Position=PlanePoint(PilePoint(side,Zone.FateDiscard))-tile.Size/2;
                    else if(side==0)tile.Position=PlanePoint(PilePoint(side,Zone.FateDeck))-tile.Size/2;
                    else tile.Position=target+Vector2.Up*50; // The public view does not identify a hidden source.
                }
                var tween=tile.CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tween.TweenProperty(tile,"position",target,.38);
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
        _landing=new Button{Name=_selectedDeck?"Focus from deck":"Focus selected card",Text="Place\nfocus",TooltipText=_selectedDeck?"Commit the top Fate card without looking. Enter also commits.":"Commit the selected hand card. Enter also commits.",Size=new Vector2(102,132),MouseDefaultCursorShape=CursorShape.PointingHand};
        _landing.Position=PlanePoint(FocusPoint(0,count%4)+Vector2.Down*(count/4)*76)-_landing.Size/2;
        _landing.AddThemeStyleboxOverride("normal",Palette.Box(new Color(.07f,.09f,.12f,.35f),new Color(.8f,.7f,.45f,.65f),1,3));
        _landing.AddThemeStyleboxOverride("hover",Palette.Box(new Color(.15f,.17f,.18f,.55f),Palette.Gold,2,3));
        _landing.AddThemeColorOverride("font_color",Palette.Paper);_landing.AddThemeFontSizeOverride("font_size",21);
        _landing.Disabled=_submitted||_commandCooldown>0;_landing.Pressed+=()=>Submit(command);_plane.AddChild(_landing);
    }
    private void SelectPile(int owner,Zone zone)
    {
        if(InputLocked||PresentationBusy)return;
        if(zone==Zone.FateDeck)
        {
            if(owner!=Snapshot!.View.ViewerId)return;
            if(!Legal.Any(a=>a.Type=="focus"&&a.Command!.Choice=="deck"))return;
            _coachingDismissed.Add(Snapshot!.View.ViewerId);_dontShowAgain=false;
            _selectedDeck=true;_selectedHand=null;_selectedInspection=null;RenderLanding();RenderActions();
        }
        else ShowPile(owner,zone);
    }
    private void ShowPile(int owner,Zone zone)
    {
        var player=Snapshot!.View.Players[owner];var cards=(zone==Zone.RemovedFromGame?player.RemovedFromGame:player.Discard.Where(c=>c.Zone==zone)).ToArray();
        var name=zone switch {Zone.FateDiscard=>"Fate discard",Zone.Dead=>"Fallen personalities",Zone.DynastyDiscard=>"Dynasty discard",_=>"Removed from game"};
        ShowCards($"{player.Name} · {name}",cards,"Public cards · click a face to inspect");
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
