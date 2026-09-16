using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class DuelScreen
{
    private Control? _cardHover;
    private string? _hoverCardId;
    private double _hoverTime;
    private Vector2 _lastHoverPointer = new(-1000,-1000);
    private Label? DuelValueAt(Vector2 point)=>_hud.GetChildren().OfType<Label>().FirstOrDefault(l=>l.Name.ToString().StartsWith("DuelValue")&&l.GetGlobalRect().HasPoint(point));
    internal Vector2 DuelValuePoint(int side)=>_hud.GetNode<Label>($"DuelValue{side}").GetGlobalRect().GetCenter();
    internal string[] VisibleDuelValues => _hud.GetChildren().OfType<Label>().Where(l=>l.Name.ToString().StartsWith("DuelValue")).OrderBy(l=>l.Name.ToString()).Select(l=>l.Text).ToArray();
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
           _controlsRect.HasPoint(_lastHoverPointer) || _guidanceRect.HasPoint(_lastHoverPointer) || DuelValueAt(_lastHoverPointer) is not null)
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
        // Same bounded, pointer-adjacent full-card preview as the main table.
        _cardHover.Position=(_lastHoverPointer+new Vector2(18,22)).Clamp(Vector2.One*8,Size-_cardHover.Size-Vector2.One*8);
        IgnoreHover(_cardHover);
    }
    private static void IgnoreHover(Node node)
    {
        if(node is Control control)control.MouseFilter=MouseFilterEnum.Ignore;
        foreach(var child in node.GetChildren())IgnoreHover(child);
    }

    private void RenderDuelValues()
    {
        if(Snapshot?.View.LastDuel is not {} record)return;
        var result=(_aftermathPlayed||_cinema==Cinema.Ending) ? record.Result : null;
        if(result?.Kind=="Refused")return;
        foreach(var card in new[]{record.Challenger,record.Challenged})
        {
            var side=card.OwnerId==Snapshot.View.ViewerId?0:1;
            var challenger=card.Id==record.Challenger.Id;
            var current=Snapshot.View.Players.SelectMany(p=>p.Army.Concat(p.Home)).FirstOrDefault(c=>c.Id==card.Id) ?? card;
            var value=result is {Kind:"Resolved"} ? (challenger?result.ChallengerTotal:result.ChallengedTotal)
                : record.Stat switch {"Force"=>current.Force,"PersonalHonor"=>current.Definition!.PersonalHonor,_=>current.Chi};
            var label=DuelUi.Text(value.ToString(System.Globalization.CultureInfo.InvariantCulture),Math.Max(28,(int)((result is null?46:70)*_scale)),Palette.Paper,true);
            label.MouseFilter=MouseFilterEnum.Stop;label.Name=$"DuelValue{side}";label.HorizontalAlignment=HorizontalAlignment.Center;
            label.VerticalAlignment=VerticalAlignment.Center;
            label.Position=_offset+new Vector2((side==0?465:1205)-90,612)*_scale;
            label.Size=new Vector2(180,65)*_scale;
            label.AddThemeColorOverride("font_shadow_color",Colors.Black);
            label.AddThemeConstantOverride("shadow_offset_y",2);
            label.TooltipText=result is {Kind:"Resolved"}
                ? $"{card.Definition!.Name}\nFinal duel total: {value}\n{record.Stat} contribution: {(challenger?result.ChallengerStat:result.ChallengedStat)}\nFocus cards and effects are recorded in Duel details."
                : $"{card.Definition!.Name} · {record.Stat}: {value}";
            _hud.AddChild(label);
            if(result?.WinnerId==card.Id)
            {
                var slash=new TextureRect{Texture=GD.Load<Texture2D>("res://Art/DuelUi/red-slash.png"),
                    ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
                    Position=_offset+new Vector2((side==0?465:1205)-47,675)*_scale,Size=new Vector2(94,20)*_scale,MouseFilter=MouseFilterEnum.Ignore};
                _hud.AddChild(slash);
            }
        }
    }

    private void RenderResultFooter(DuelRecord record)
    {
        _controlsRect=new Rect2(_offset+new Vector2(1320,780)*_scale,new Vector2(310,105)*_scale);
        var next=DuelUi.Button("Continue",()=>{if(!InputLocked&&!_submitted&&CanContinue)PresentationFinished?.Invoke();},true,_scale,50);
        next.Position=_controlsRect.Position;next.Size=_controlsRect.Size;_hud.AddChild(next);
        var details=DuelUi.Button("Duel details",()=>ShowConsequences(record),scale:_scale,fontSize:18);
        details.Position=new Vector2(Math.Max(160,_offset.X+155*_scale),16);details.Size=new Vector2(150,40)*_scale;_hud.AddChild(details);
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
