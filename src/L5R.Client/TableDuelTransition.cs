using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Presentation-only bridge. Consumes public card views and screen quads, never commands/state.</summary>
public partial class TableDuelTransition : Control
{
    public sealed record Card(CardView View,Vector2[] Origin,Vector2[] Destination);
    private sealed record Face(Card Data,Texture2D Texture,Vector2[] Close);
    public event Action? Finished;
    public event Action<float>? CameraProgress;
    public event Action? ControlsRevealed;
    public bool ReducedMotion {get;init;}
    public double Elapsed {get;private set;}
    public bool Running {get;private set;}
    public const double Duration=4.5;
    private readonly List<Face> _faces=[];
    private ShaderMaterial _reveal=null!;
    private TextureRect _room=null!,_hud=null!,_wordmark=null!;
    private Control _cardLayer=null!;
    private Label _challenge=null!;
    private PanelContainer _challengePanel=null!;
    private Button _skip=null!;
    private bool _controlsRevealed;
    private Vector2 _design;
    private ImageTexture? _fullTable,_destination;
    public void Build(Texture2D cleanRoom,Image fullTable,Image destination,IReadOnlyList<Card> cards,Vector2 size,string challenger,string challenged)
    {
        Size=size;_design=size;MouseFilter=MouseFilterEnum.Stop;
        _fullTable=ImageTexture.CreateFromImage(fullTable);_destination=ImageTexture.CreateFromImage(destination);
        _reveal=new ShaderMaterial{Shader=GD.Load<Shader>("res://Art/DuelTransition/ink-reveal.gdshader")};
        _reveal.SetShaderParameter("destination",_destination);
        _reveal.SetShaderParameter("ink_fiber",GD.Load<Texture2D>("res://Art/DuelUi/red-slash.png"));
        _reveal.SetShaderParameter("pixel",Vector2.One/size);
        _room=ImageRect(cleanRoom);_room.Material=_reveal;AddChild(_room);
        _hud=ImageRect(_fullTable);AddChild(_hud);
        _cardLayer=new TransitionFaces{OwnerTransition=this,MouseFilter=MouseFilterEnum.Ignore};AddChild(_cardLayer);
        _wordmark=ImageRect(GD.Load<Texture2D>("res://Art/DuelUi/duel-wordmark.png"));AddChild(_wordmark);
        _wordmark.Position=new Vector2(size.X*.36f,size.Y*.035f);_wordmark.Size=new Vector2(size.X*.28f,size.Y*.18f);
        _wordmark.StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered;
        _challenge=DuelUi.Text($"{challenger}  challenges  {challenged}",Math.Max(22,(int)(size.Y*.027f)),Palette.Paper);
        _challenge.HorizontalAlignment=HorizontalAlignment.Center;
        _challengePanel=new PanelContainer{Position=new Vector2(size.X*.2f,size.Y*.14f),Size=new Vector2(size.X*.6f,size.Y*.058f),MouseFilter=MouseFilterEnum.Ignore};
        _challengePanel.AddThemeStyleboxOverride("panel",SidebarSkin.Surface());AddChild(_challengePanel);
        _challenge.AddThemeColorOverride("font_shadow_color",Colors.Black);_challenge.AddThemeConstantOverride("shadow_offset_y",2);_challengePanel.AddChild(_challenge);
        _skip=DuelUi.Button("Skip",Complete,fontSize:18);_skip.Position=new Vector2(size.X-110,20);_skip.Size=new Vector2(86,38);AddChild(_skip);
        foreach(var card in cards)
        {
            var portrait=new SubViewport{Size=new Vector2I(560,560),TransparentBg=true,Disable3D=true,HandleInputLocally=false,RenderTargetUpdateMode=SubViewport.UpdateMode.Once};AddChild(portrait);
            var tile=new CardTile(card.View with {Bowed=false},true,_=>{},_=>{}){Size=Vector2.One*560,FaceHeight=548,ReducedMotion=true,UprightPreview=true,ShowTokenRail=false,Material=CardPaper.Reading,MouseFilter=MouseFilterEnum.Ignore,FocusMode=FocusModeEnum.None};portrait.AddChild(tile);
            var rect=tile.FaceCornersInCanvas();
            var atlas=new AtlasTexture{Atlas=portrait.GetTexture(),Region=new Rect2(rect[0],rect[2]-rect[0])};

            // The intermediate close-up shares the destination's unit spacing and attachment ordering.
            var destCenter=card.Destination.Aggregate(Vector2.Zero,(a,b)=>a+b)/4;
            var side=destCenter.X<size.X/2?-1:1;
            var middle=new Vector2(size.X*(side<0?.285f:.715f),size.Y*.53f);
            var targetCenter=new Vector2(size.X*(side<0?.308f:.715f),size.Y*.347f);
            var close=card.Destination.Select(p=>middle+((p-targetCenter)*1.40f).Rotated(side*-.026f)).ToArray();
            _faces.Add(new(card,atlas,close));
        }
        Present(0);
    }
    private TextureRect ImageRect(Texture2D texture)=>new(){Texture=texture,Size=_design,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=MouseFilterEnum.Ignore,TextureFilter=TextureFilterEnum.LinearWithMipmaps};
    public void Start(){Running=true;Elapsed=0;Present(0);}
    public override void _Process(double delta)
    {
        if(!Running)return;
        if(GetViewportRect().Size!=_design){Complete();return;}
        Elapsed+=delta;Present(Elapsed);
        if(Elapsed>=(ReducedMotion?.3:Duration))Complete();
    }
    private static float Ease(double x){var t=Math.Clamp((float)x,0,1);return t*t*t*(t*(t*6-15)+10);}
    private static float Beat(double t,double start,double end)=>Ease((t-start)/(end-start));
    public void Present(double t)
    {
        Elapsed=t;
        if(ReducedMotion)
        {
            _reveal.SetShaderParameter("progress",1f);_reveal.SetShaderParameter("fade",1-Beat(t,0,.3));
            _hud.Modulate=new Color(1,1,1,1-Beat(t,0,.3));_wordmark.Hide();_challengePanel.Hide();_cardLayer.Hide();
            RevealControls();return;
        }
        _hud.Modulate=new Color(1,1,1,1-Beat(t,.3,.85));
        CameraProgress?.Invoke(Beat(t,.55,1.9));
        _reveal.SetShaderParameter("push",0f);
        _reveal.SetShaderParameter("softness",Beat(t,.7,1.85)*.6f);
        _reveal.SetShaderParameter("progress",Beat(t,1.95,3.55));
        _reveal.SetShaderParameter("fade",1-Beat(t,3.8,4.45));
        _wordmark.Modulate=new Color(1,1,1,Beat(t,2.0,2.35)*(1-Beat(t,3.2,3.95)));
        _challengePanel.Modulate=new Color(1,1,1,Beat(t,.05,.24)*(1-Beat(t,.8,1.1)));
        if(t>=3.8)RevealControls();
        _cardLayer.QueueRedraw();
    }
    private void RevealControls(){if(_controlsRevealed)return;_controlsRevealed=true;ControlsRevealed?.Invoke();}
    public void Complete()
    {
        if(!Running)return;Running=false;RevealControls();Finished?.Invoke();QueueFree();
    }
    public override void _Input(InputEvent e)
    {
        if(Running&&e is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Space or Key.Escape}){Complete();GetViewport().SetInputAsHandled();}
    }
    private void Paint(Control canvas)
    {
        var t=Elapsed;var lift=Beat(t,.8,1.9);var settle=Beat(t,2.65,3.95);var s=_design.Y/1080;
        foreach(var face in _faces)
        {
            var quad=face.Data.Origin.Select((p,i)=>p.Lerp(face.Close[i],lift).Lerp(face.Data.Destination[i],settle)).ToArray();
            // The last fraction settles without overshoot so live controls occupy the identical pixels.
            var floatHeight=Mathf.Sin(lift*Mathf.Pi)*(1-settle)*_design.Y*.035f;
            quad=quad.Select(p=>p-Vector2.Down*floatHeight).ToArray();
            var center=quad.Aggregate(Vector2.Zero,(a,b)=>a+b)/4;
            var foot=(quad[2]+quad[3])/2;
            var alpha=Beat(t,.29,.82);
            if(t<1.05)
            {
                var gold=new Color(.94f,.76f,.36f,Beat(t,.05,.3)*(1-Beat(t,.65,1.05)));
                foreach(var width in new[]{12f,6f,2f})canvas.DrawPolyline(quad.Append(quad[0]).ToArray(),gold with {A=gold.A*(width==2?.7f:.12f)},width*s,true);
            }
            for(var spread=6;spread>=1;spread--)
            {
                var radius=new Vector2((quad[2]-quad[3]).Length()*.52f+spread*3*s,5*s+spread*2*s);
                var shadow=Enumerable.Range(0,40).Select(i=>foot+new Vector2(10,10)*s+Vector2.FromAngle(i*Mathf.Tau/40)*radius).ToArray();
                canvas.DrawColoredPolygon(shadow,new Color(0,0,0,.037f*alpha));
            }
            foreach(var spread in new[]{6f,3f,1f})canvas.DrawColoredPolygon(quad.Select(p=>p+(p-center).Normalized()*spread*s+Vector2.One*4*s).ToArray(),new Color(0,0,0,.12f*alpha));
            canvas.DrawColoredPolygon(quad.Select(p=>p+Vector2.One*1.5f*s).ToArray(),new Color(.19f,.14f,.09f,alpha));
            canvas.DrawColoredPolygon(quad,new Color(1,1,1,alpha),new Vector2[]{Vector2.Zero,Vector2.Right,Vector2.One,Vector2.Down},face.Texture);
            canvas.DrawLine(quad[3],quad[2],new Color(.72f,.56f,.34f,alpha*.6f),s,true);
        }
    }
    private partial class TransitionFaces:Control
    {
        public TableDuelTransition OwnerTransition=null!;
        public override void _Draw()=>OwnerTransition.Paint(this);
    }
}
