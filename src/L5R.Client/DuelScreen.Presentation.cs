using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class DuelScreen
{
    private Control? _cardHover;
    private string? _hoverCardId;
    private double _hoverTime;
    private Vector2 _lastHoverPointer = new(-1000,-1000);
    internal string? HoverPreviewId => _cardHover?.HasMeta("card_preview")==true ? _cardHover.GetMeta("card_preview").AsString() : null;
    internal Rect2 HoverPreviewRect => _cardHover?.GetGlobalRect() ?? new Rect2();

    private string InspectionContext(CardView card)
    {
        if(_aftermathPlayed && Snapshot?.View.LastDuel is {} record &&
           (card.Id==record.Challenger.Id || card.Id==record.Challenged.Id))
        {
            var actual=Snapshot.View.Players.SelectMany(p=>p.Army.Concat(p.Home).Concat(p.Discard).Concat(p.RemovedFromGame)).FirstOrDefault(c=>c.Id==card.Id);
            return $"Duel participant · recorded for this result. Current location: {actual?.Zone.ToString() ?? "see aftermath"}.";
        }
        if(_resultArranged&&card.Zone==Zone.Focused)return "Revealed focus · retained duel evidence. This card has already left the focus pool; see Public piles for its current location.";
        return card.Zone==Zone.Focused&&!card.FaceUp ? "Your committed focus · privately inspectable."
            : card.Zone==Zone.Hand ? "In your hand. Select a card to focus."
            : "Public card · click to inspect.";
    }

    private void HideCardHover()
    {
        if(_cardHover is not null){RemoveChild(_cardHover);_cardHover.QueueFree();_cardHover=null;}
        _hoverCardId=null;_hoverTime=0;
    }

    private void ProcessCardHover(double delta)
    {
        if(Snapshot is null || InputLocked || PresentationBusy || _reading is not null || _dragging ||
           _controlsRect.HasPoint(_lastHoverPointer) || _guidanceRect.HasPoint(_lastHoverPointer))
        {HideCardHover();return;}
        var point=_table.ScreenToCanvas(_lastHoverPointer);
        var tile=UprightAt(_lastHoverPointer) ?? _plane.GetChildren().OfType<CardTile>().OrderByDescending(t=>t.ZIndex).FirstOrDefault(t=>t._HasPoint(point-t.Position));
        var pile=tile is null ? _piles.Values.FirstOrDefault(p=>p._HasPoint(point-p.Position)) : null;
        // Only identities already supplied by this seat's view can enter CardPreview.
        if(tile?.Card.Definition is null && pile is null){HideCardHover();return;}
        var identity=tile?.Card.Id ?? $"pile:{pile!.OwnerId}:{pile.PileZone}";
        if(_hoverCardId!=identity){HideCardHover();_hoverCardId=identity;}
        if(_cardHover is not null || (_hoverTime+=delta)<.5)return;
        _cardHover=tile is not null ? tile.CreateReadingPreview() : ReadingTooltip.Create(pile!,pile!.GetMeta("flat_tooltip").AsString());
        if(tile is not null)DuelUi.SkinInspection(_cardHover);
        AddChild(_cardHover);_cardHover.ZIndex=90;
        _cardHover.Size=_cardHover.CustomMinimumSize;
        // Keep the shared inspector intact; only choose the least obstructive position.
        _cardHover.Position=PlaceCardPreview(_cardHover.Size,tile);
        IgnoreHover(_cardHover);
    }
    private Vector2 PlaceCardPreview(Vector2 extent,CardTile? source)
    {
        const float margin=12;
        var max=(Size-extent-Vector2.One*margin).Max(Vector2.One*margin);
        var candidates=new List<Vector2>();
        foreach(var x in new[]{margin,max.X,(Size.X-extent.X)/2})
            foreach(var y in new[]{Math.Min(72*_scale,max.Y),max.Y,(Size.Y-extent.Y)/2})
                candidates.Add(new Vector2(x,y).Clamp(Vector2.One*margin,max));
        var protectedAreas=new List<(Rect2 Bounds,float Weight)> {(_controlsRect,10),(_guidanceRect,10),
            (new Rect2(_offset+new Vector2(606,260)*_scale,new Vector2(460,88)*_scale),5)};
        foreach(var card in _tiles.Values)
        {
            var corners=card.FaceCornersOnViewport();
            var low=corners.Aggregate(new Vector2(float.MaxValue,float.MaxValue),(a,b)=>a.Min(b));
            var high=corners.Aggregate(new Vector2(float.MinValue,float.MinValue),(a,b)=>a.Max(b));
            var weight=card==source?12:card.HasMeta("upright")?6:card.Card.Zone==Zone.Focused?3:1;
            protectedAreas.Add((new Rect2(low,high-low),weight));
        }
        float Score(Vector2 point)
        {
            var rect=new Rect2(point,extent);
            var score=protectedAreas.Sum(a=>{var area=rect.Intersection(a.Bounds).Size;return area.X*area.Y*a.Weight;});
            if(rect.Grow(18).HasPoint(_lastHoverPointer))score+=extent.X*extent.Y*20;
            // Break ties towards the outside edge, leaving the central confrontation open.
            score+=Math.Min(point.X,Size.X-rect.End.X)*extent.Y*.15f;
            return score;
        }
        return candidates.MinBy(Score);
    }
    private static void IgnoreHover(Node node)
    {
        if(node is Control control)control.MouseFilter=MouseFilterEnum.Ignore;
        foreach(var child in node.GetChildren())IgnoreHover(child);
    }

    private void RenderResultFooter(DuelRecord record)
    {
        _controlsRect=new Rect2(_offset+new Vector2(1320,780)*_scale,new Vector2(310,105)*_scale);
        var next=DuelUi.Button("Continue",()=>{if(!InputLocked&&!_submitted&&CanContinue)PresentationFinished?.Invoke();},true,_scale,50);
        next.Position=_controlsRect.Position;next.Size=_controlsRect.Size;_hud.AddChild(next);
        var details=DuelUi.Button("Duel details",()=>ShowConsequences(record),scale:_scale,fontSize:18);
        details.TooltipText="Read every source, consequence and revealed card in the complete account.";
        details.Position=_offset+new Vector2(755,883)*_scale;details.Size=new Vector2(162,39)*_scale;_hud.AddChild(details);
    }

    private VBoxContainer RecapPanel(out HBoxContainer actions)
    {
        HideCardHover();ClearReading();
        _hud.GetNodeOrNull<Control>("DuelResultFooter")?.Hide();
        var shade=new ColorRect{Color=new Color(0,0,0,.4f),Size=Size,MouseFilter=MouseFilterEnum.Stop};_hud.AddChild(shade);_reading=shade;
        ReadingChanged?.Invoke(true);RenderGuidance();
        var design=new Vector2(1240,350);
        var scale=Math.Min(1,Math.Min((Size.X-48)/design.X,(Size.Y-80)/design.Y));
        var shell=new Control{Size=design,Scale=Vector2.One*scale,Position=new Vector2((Size.X-design.X*scale)/2,Size.Y-design.Y*scale-24)};shade.AddChild(shell);
        // A readable ink interior; the transparent brush edges do not leak underlying controls.
        shell.AddChild(new ColorRect{Color=new Color(.025f,.028f,.028f,.97f),Position=new Vector2(18,14),Size=design-new Vector2(36,28),MouseFilter=MouseFilterEnum.Ignore});
        var surface=DuelUi.Surface(shell,28);surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var layout=new VBoxContainer();layout.AddThemeConstantOverride("separation",12);surface.AddChild(layout);
        var header=new HBoxContainer();layout.AddChild(header);
        var title=DuelUi.Text("Aftermath",38,Palette.Paper,true);title.AddThemeFontOverride("font",DuelUi.UiHeading);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(title);
        header.AddChild(DuelUi.Button("Back",ClearReading,fontSize:18));
        var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=SizeFlags.ExpandFill};layout.AddChild(scroll);
        var column=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};column.AddThemeConstantOverride("separation",10);scroll.AddChild(column);
        actions=new HBoxContainer();actions.AddThemeConstantOverride("separation",24);layout.AddChild(actions);
        return column;
    }
}
