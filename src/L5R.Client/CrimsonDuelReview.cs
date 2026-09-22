using Godot;
using L5R.Core;
using System.Text.Json;
using System.Globalization;

namespace L5R.Client;

/// <summary>Native composition/input/motion review. Public fixture cards, no match commands.</summary>
public partial class CrimsonDuelReview : Control
{
    private CrimsonDuelStage _stage=null!;
    private TableSurface _table=null!;
    private Control _plane=null!,_hud=null!;
    private TextureRect _reference=null!;
    private CardTile? _reading;
    private readonly List<CardTile> _cards=[];
    private readonly List<double> _frameTimes=[];
    private bool _clean,_referenceVisible,_crowded,_revealed,_bowed,_other,_sidebar,_check,_help;
    private bool _benchmark;
    private float _zoom=1;
    private string? _capture,_film;
    private int _filmFrame,_filmFrames=300,_checkStep,_inspections;
    private double _elapsed,_settle=2,_duration=20;
    private long _lastFrameStamp;
    private double? _fixedTime;
    private int _phase=3;
    private bool _reducedMotion;
    private Rect2 _play;
    private Vector2 _offset;
    private float _scale;
    private const float Unit=CrimsonDuelStage.Unit;

    public override void _Ready()
    {
        var prefs=new MenuPreferences("default");prefs.Load();_reducedMotion=prefs.ReducedMotion;
        foreach(var a in OS.GetCmdlineUserArgs())
        {
            if(a.StartsWith("--capture="))_capture=a[10..];
            if(a.StartsWith("--film="))_film=a[7..];
            if(a.StartsWith("--frames="))_filmFrames=int.Parse(a[9..]);
            if(a.StartsWith("--settle="))_settle=double.Parse(a[9..],CultureInfo.InvariantCulture);
            if(a.StartsWith("--duration="))_duration=double.Parse(a[11..],CultureInfo.InvariantCulture);
            if(a.StartsWith("--time="))_fixedTime=double.Parse(a[7..],CultureInfo.InvariantCulture);
            if(a.StartsWith("--zoom="))_zoom=float.Parse(a[7..],CultureInfo.InvariantCulture);
            if(a.StartsWith("--phase="))_phase=int.Parse(a[8..]);
            _clean|=a=="--clean";_referenceVisible|=a=="--reference";
            _crowded|=a=="--crowded";_revealed|=a=="--revealed";_bowed|=a=="--bowed";
            _other|=a=="--other-seat";_sidebar|=a=="--sidebar";_check|=a=="--check";_benchmark|=a=="--benchmark";
            if(a=="--reduced-motion")_reducedMotion=true;
            if(a=="--motion")_reducedMotion=false;
        }
        Theme=Palette.MakeTheme();ClipContents=true;
        if(_capture is not null||_film is not null)GetWindow().Unresizable=true;
        _stage=new CrimsonDuelStage {ReducedMotion=_reducedMotion,Phase=_phase};AddChild(_stage);
        _table=new TableSurface {PlanePitch=CrimsonDuelStage.Pitch,PlaneDepth=CrimsonDuelStage.Depth};AddChild(_table);
        _plane=new Control();_table.Canvas.AddChild(_plane);
        _hud=new Control {MouseFilter=MouseFilterEnum.Ignore};AddChild(_hud);
        _reference=new TextureRect {
            Texture=ImageTexture.CreateFromImage(Image.LoadFromFile(ProjectSettings.GlobalizePath("res://../../assets/ui/duel-arena/crimson/approved-crimson-standoff.png"))),
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter=MouseFilterEnum.Ignore};AddChild(_reference);
        GetViewport().SizeChanged+=Layout;Layout();
        if(_film is not null)System.IO.Directory.CreateDirectory(_film);
        GD.Print("CRIMSON_DUEL: native hybrid presentation; original scans, public composition fixtures; H help.");
    }

    private void Layout()
    {
        foreach(var c in _plane.GetChildren()){_plane.RemoveChild(c);c.QueueFree();}
        foreach(var c in _hud.GetChildren()){_hud.RemoveChild(c);c.QueueFree();}
        _cards.Clear();_reading=null;
        var screen=GetViewportRect().Size;
        var side=_sidebar?Math.Min(260,screen.X*.19f):0;
        _play=new Rect2(Vector2.Zero,new Vector2(screen.X-side,screen.Y));
        _stage.Resize(_play.Size,_zoom);_scale=_stage.ContentScale;_offset=_stage.ContentOffset;
        var density=_scale*CrimsonDuelStage.Density*_zoom;
        _table.Position=_offset;_table.Scale=Vector2.One*density;
        _table.ResizePlane((Vector2)_stage.World.Size/density,Math.Max(.8f,density*1.2f));
        _plane.Size=_table.CanvasSize;
        _table.Visible=!_clean&&!_referenceVisible;
        _hud.Visible=!_clean&&!_referenceVisible;
        _reference.Visible=_referenceVisible;_reference.Size=screen;
        _stage.Visible=!_referenceVisible;
        CardDefinition Named(string name)=>CardCatalog.Cards.Values.First(d=>d.Name==name);
        var defs=PublishedContent.Snapshot.Cards;
        var attachments=defs.Where(d=>d.Type is CardType.Item or CardType.Follower).ToArray();
        var strategies=defs.Where(d=>d.Type==CardType.Strategy).ToArray();
        for(var sideIndex=0;sideIndex<2;sideIndex++)
        {
            var owner=_other?1-sideIndex:sideIndex;
            var definition=Named(owner==0?"Doji Tatsuki":"Matsu Hachiro");
            var center=DesignToPlane(new Vector2(sideIndex==0?562:1110,555));
            var count=_crowded?6:2;
            for(var i=count-1;i>=0;i--)
            {
                var direction=sideIndex==0?-1:1;
                var off=new Vector2(direction*(70+i*(_crowded?34:90)),12+i*14);
                AddCard(Sample($"attachment-{owner}-{i}",attachments[i%attachments.Length],owner,Zone.Attached,_bowed&&i%2==0),center+off,_crowded?335:365);
            }
            AddCard(Sample("unit-"+owner,definition,owner,Zone.Battlefield,_bowed&&sideIndex==1),center,460);
            for(var i=0;i<4;i++)
            {
                var x=(sideIndex==0?282:965)+i*144;
                var view=_revealed?Sample($"focus-{owner}-{i}",strategies[i%strategies.Length],owner,Zone.Focused):
                    new CardView($"anonymous-focus-{owner}-{i}",null,owner,Zone.Focused,false,false,null,null,0);
                AddCard(view,DesignToPlane(new Vector2(x,725)),200);
            }
        }
        for(var i=0;i<5;i++)
            AddCard(Sample("hand-"+i,strategies[i%strategies.Length],_other?1:0,Zone.Hand),
                DesignToPlane(new Vector2(546+i*94,857)),196);
        var controls=new VBoxContainer {Position=_offset+new Vector2(1086,797)*_scale,Size=new Vector2(250,123)*_scale};
        controls.AddThemeConstantOverride("separation",Math.Max(2,(int)(7*_scale)));_hud.AddChild(controls);
        foreach(var text in new[]{"Focus from hand","Focus from deck","Strike"})
        {
            var button=new Button {Text=text,Disabled=true,CustomMinimumSize=new Vector2(250,34)*_scale,
                TooltipText="Presentation fixture. Live choices will come from the duel engine."};
            button.AddThemeStyleboxOverride("disabled",Palette.Box(new Color("17120f"),new Color("ba9658"),(int)(4*_scale)));
            button.AddThemeColorOverride("font_disabled_color",new Color("dbc99e"));
            button.AddThemeFontSizeOverride("font_size",Math.Max(12,(int)(18*_scale)));controls.AddChild(button);
        }
        if(_sidebar)
        {
            _hud.AddChild(new ColorRect{Position=new Vector2(_play.Size.X,0),Size=new Vector2(side,screen.Y),Color=new Color("14141a")});
            var label=Palette.Text("Review sidebar",18,Palette.Paper);label.Position=new Vector2(_play.Size.X+18,35);_hud.AddChild(label);
        }
        if(_help)
        {
            var label=Palette.Text("PUBLIC CARD FIXTURE · Tab concept · V scenery · C crowded · R reveal · B bow · P sidebar · O other seat · N motion · wheel zoom · F fit · H help · Esc close",14,Palette.Paper);
            label.Position=new Vector2(20,20);_hud.AddChild(label);
        }
    }
    private static Vector2 DesignToPlane(Vector2 point)=>TableSurface.UnprojectOffset(
        (point-CrimsonDuelStage.StageCanvas/2)/CrimsonDuelStage.Density,CrimsonDuelStage.Pitch,CrimsonDuelStage.Depth);
    private static CardView Sample(string id,CardDefinition d,int owner,Zone zone,bool bowed=false)=>
        new(id,d,owner,zone,true,bowed,null,null,d.Force,d.Chi);
    private void AddCard(CardView card,Vector2 center,float height)
    {
        CardTile tile=card.Definition is null?new CrimsonFocusBack(card):new CardTile(card,true,Inspect,_=>{});
        tile.Size=Vector2.One*(height+12);tile.FaceHeight=height;
        tile.Position=_table.CanvasSize/2+center-tile.Size/2;
        tile.ReducedMotion=true;tile.ShowTokenRail=false;tile.Material=CardPaper.FixedTable;
        tile.ProjectToScreen=_table.ProjectCanvas;tile.SetMeta("flat_tooltip",true);
        _plane.AddChild(tile);_cards.Add(tile);
    }
    private void Inspect(CardView card)
    {
        _inspections++;ClearReading();if(card.Definition is null)return;
        var h=Math.Min(650,_play.Size.Y*.76f);
        _reading=new CardTile(card with {Bowed=false,Zone=Zone.Home},true,_=>ClearReading(),_=>{})
        {FaceHeight=h,Size=Vector2.One*(h+12),Position=_play.Size/2-Vector2.One*(h+12)/2,
            ReducedMotion=true,ShowTokenRail=false,Material=CardPaper.Reading};
        _hud.AddChild(_reading);
    }
    private void ClearReading(){if(_reading is null)return;_hud.RemoveChild(_reading);_reading.QueueFree();_reading=null;}
    public override void _Input(InputEvent e)
    {
        // Native capture/benchmark windows must not react to desktop scrolling.
        // Only the explicit input probe forwards mouse events during a still capture.
        if((_film is not null||(_capture is not null&&!_check))&&e is InputEventMouse)return;
        if(_capture is not null&&e is InputEventMouseButton wb&&wb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)return;
        if(e is InputEventMouseButton {Pressed:true} w&&w.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {_zoom=Math.Clamp(_zoom+(w.ButtonIndex==MouseButton.WheelUp?.04f:-.04f),.85f,1.18f);Layout();return;}
        if(e is InputEventMouse m&&!_clean&&!_referenceVisible&&_reading is null&&_play.HasPoint(m.Position))
        {
            var onControls=new Rect2(_offset+new Vector2(1080,787)*_scale,new Vector2(275,150)*_scale).HasPoint(m.Position);
            if(!onControls)_table.ForwardMouse(m);else _table.ExitMouse();
        }
        if(e is not InputEventKey {Pressed:true,Echo:false} k)return;
        if(k.Keycode==Key.Escape){if(_reading is not null)ClearReading();else GetTree().Quit();return;}
        if(_capture is not null||_film is not null)return;
        switch(k.Keycode)
        {
            case Key.Tab:_referenceVisible=!_referenceVisible;break;
            case Key.V:_clean=!_clean;break;
            case Key.C:_crowded=!_crowded;break;
            case Key.R:_revealed=!_revealed;break;
            case Key.B:_bowed=!_bowed;break;
            case Key.O:_other=!_other;break;
            case Key.P:_sidebar=!_sidebar;break;
            case Key.N:_stage.ReducedMotion=!_stage.ReducedMotion;break;
            case Key.F:_zoom=1;break;
            case Key.H:_help=!_help;break;
            default:return;
        }
        Layout();
    }
    public override void _Process(double delta)
    {
        var stamp=System.Diagnostics.Stopwatch.GetTimestamp();
        var wallMilliseconds=_lastFrameStamp==0?0:(stamp-_lastFrameStamp)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        _lastFrameStamp=stamp;
        _elapsed+=delta;
        if(_fixedTime is double t)_stage.SetTime(t);
        else if(_film is not null)_stage.SetTime(_filmFrame/30.0);
        else _stage.Advance(Math.Min(delta,.10));
        if(_elapsed>3&&_benchmark&&wallMilliseconds>0)_frameTimes.Add(wallMilliseconds);
        if(_film is not null&&_elapsed>1.5)
        {
            RenderingServer.ForceDraw(false);RenderingServer.ForceSync();
            var result=GetViewport().GetTexture().GetImage().SaveJpg(System.IO.Path.Combine(_film,$"{_filmFrame:0000}.jpg"),.97f);
            if(result!=Error.Ok){GD.PushError("Motion frame capture failed: "+result);GetTree().Quit(1);return;}
            _filmFrame++;
            if(_filmFrame>=_filmFrames){SaveReport(System.IO.Path.Combine(_film,"capture.json"),Validate());GetTree().Quit();}
            return;
        }
        if(_capture is null||_elapsed<(_benchmark?_duration:_settle))return;
        try
        {
            var error=Validate();
            if(_check&&_checkStep<2)
            {
                var tile=_cards.First(c=>c.Card.Id==(_checkStep==0?"unit-"+(_other?1:0):"hand-2"));
                var point=tile.FaceCornersOnViewport().Aggregate(Vector2.Zero,(s,p)=>s+p)/4;
                Input.ParseInputEvent(new InputEventMouseMotion{Position=point});
                Input.ParseInputEvent(new InputEventMouseButton{Position=point,ButtonIndex=MouseButton.Left,Pressed=true});
                Input.ParseInputEvent(new InputEventMouseButton{Position=point,ButtonIndex=MouseButton.Left,Pressed=false});
                _checkStep+=10;_elapsed=0;return;
            }
            if(_check&&_checkStep>=10)
            {
                var expected=_checkStep==10?"unit-"+(_other?1:0):"hand-2";
                if(_reading?.Card.Id!=expected||_inspections!=_checkStep-9)throw new Exception("Projected native click selected the wrong card: "+expected);
                ClearReading();foreach(var card in _cards)card.ReleaseFocus();_table.ExitMouse();_checkStep-=9;
                Input.ParseInputEvent(new InputEventMouseMotion{Position=new Vector2(10,100)});_elapsed=0;return;
            }
            RenderingServer.ForceDraw(false);RenderingServer.ForceSync();
            if(GetViewport().GetTexture().GetImage().SavePng(_capture)!=Error.Ok)throw new Exception("Failed native capture");
            SaveReport(_capture+".json",error);
            GD.Print($"CRIMSON_PASS: camera {error:F5}px, cards {_cards.Count}, clicks {_inspections}, grass {_stage.GrassInstances}");
            GetTree().Quit();
        }
        catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}
    }
    private float Validate()
    {
        var error=new[]{Vector2.Zero,new Vector2(-500,-400),new Vector2(500,400)}.Max(p=>
            _table.ProjectCanvas(_table.CanvasSize/2+p).DistanceTo(_stage.ProjectWorld(new Vector3(p.X/Unit,0,p.Y/Unit))));
        if(error>.5f)throw new Exception("3D/card projection mismatch: "+error);
        if(!_clean&&!_referenceVisible)
            foreach(var c in _cards)
            {
                if(c.Card.Definition is {} d&&CardTile.Artwork(d)is null)throw new Exception("Missing original scan: "+d.Name);
                if(Mathf.IsEqualApprox(_zoom,1)&&c.FaceCornersOnViewport().Any(p=>!_play.HasPoint(p)))throw new Exception("Card clipped: "+c.Card.Id);
                if(c.FaceCornersInCanvas().Any(p=>!new Rect2(-788,-480,1576,970).HasPoint(p-_table.CanvasSize/2)))throw new Exception("Card outside mat: "+c.Card.Id);
            }
        return error;
    }
    private void SaveReport(string path,float error)
    {
        var ordered=_frameTimes.Order().ToArray();
        double Percentile(double p)=>ordered.Length==0?0:ordered[Math.Min(ordered.Length-1,(int)(p*(ordered.Length-1)))];
        System.IO.File.WriteAllText(path,JsonSerializer.Serialize(new{
            Stage="Crimson Standoff",Viewport=GetViewportRect().Size.ToString(),Phase=_phase,Zoom=_zoom,
            ReducedMotion=_stage.ReducedMotion,AtmosphereTime=_stage.AtmosphereTime,
            GrassInstances=_stage.GrassInstances,RenderedPrimitives=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame),
            WorldPrimitives=_stage.World.GetRenderInfo(Viewport.RenderInfoType.Visible,Viewport.RenderInfo.PrimitivesInFrame),
            WorldDrawCalls=_stage.World.GetRenderInfo(Viewport.RenderInfoType.Visible,Viewport.RenderInfo.DrawCallsInFrame),
            MaxCameraErrorPixels=error,OriginalCardFaces=_cards.Count(c=>c.Card.Definition is not null),HiddenBacks=_cards.Count(c=>c.Card.Definition is null),
            NativeCardClicks=_inspections,Crowded=_crowded,Bowed=_bowed,OtherSeat=_other,Revealed=_revealed,Sidebar=_sidebar,
            BenchmarkSamples=ordered.Length,FrameTimeClock="monotonic wall clock between native process frames",
            FrameMillisecondsP50=Percentile(.5),FrameMillisecondsP95=Percentile(.95),FrameMillisecondsP99=Percentile(.99),
            MotionFrames=_filmFrame,MotionFramesPerSecond=30,GameplayTested=false,HumanArtAccepted=false},new JsonSerializerOptions{WriteIndented=true}));
    }
}

/// <summary>The square source is a texture atlas face intended for a portrait card.
/// Keep the rendered footprint identical to CardTile's hidden-face hit polygon.</summary>
public partial class CrimsonFocusBack : CardTile
{
    public CrimsonFocusBack() { }
    public CrimsonFocusBack(CardView card,Action<CardView>? clicked=null):base(card,true,clicked??(_=>{}),_=>{})
    { SetMeta("flat_tooltip",true);TooltipText="Focused Fate card · face down"; }
    public override void _Draw()
    {
        var face=new Vector2(FaceHeight*.7142857f,FaceHeight);
        var rect=new Rect2((Size-face)/2,face);
        DrawRect(new Rect2(rect.Position+new Vector2(2,3),face),new Color(0,0,0,.45f));
        DrawTextureRect(GD.Load<Texture2D>("res://Art/original-fate-back.png"),rect,false);
        if(Highlighted)DrawRect(rect.Grow(3),Palette.Gold,false,3,true);
    }
}
