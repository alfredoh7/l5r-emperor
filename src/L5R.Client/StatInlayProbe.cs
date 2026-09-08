using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class StatInlayProbe : Control
{
    private readonly List<CardTile> _cards=[];
    private string _output="";
    private double _clock;
    private int _step, _clicks;
    private bool _reduced;
    public override void _Ready()
    {
        _output=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--output="))[9..];
        _reduced=OS.GetCmdlineUserArgs().Contains("--reduced");
        Theme=Palette.MakeTheme();
        AddChild(new TextureRect { Texture=GD.Load<Texture2D>("res://Art/navy-mat-v4.png"),Size=GetViewportRect().Size,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=MouseFilterEnum.Ignore });
        var title=Palette.Text("Inlaid Force & Chi",44,Palette.Gold,true);title.Position=new Vector2(48,25);AddChild(title);
        var subtitle=Palette.Text("Approved A · recessed gold / graphite · public values",22,Palette.PaperDim);subtitle.Position=new Vector2(48,90);AddChild(subtitle);
        var defs=PublishedContent.Snapshot.Cards.Where(d=>d.Type==CardType.Personality).ToArray();
        for(var i=0;i<4;i++)
        {
            var definition=defs.First(d=>d.Name==(i%2==0?"Akodo Kisho":"Asahina Konomi"));
            int[] counts=[0,1,3,6];
            var y=170+counts[i]*20;
            for(var j=counts[i];j>=1;j--)
                AddChild(new CardTile(new CardView("attachment-"+i+"-"+j,PublishedContent.Snapshot.Cards.First(d=>d.Name=="Native Guide"),0,Zone.Attached,true,false,null,"p"+i,5,0),true,_=>{},_=>{})
                {Position=new Vector2(28+i*410,y-j*20),Size=new Vector2(385,405),FaceHeight=365,ReducedMotion=true});
            var card=new CardView("p"+i,definition,0,Zone.Home,true,i==1,null,null,i==0?7:i==1?0:i==2?3:12,i==2?5:i==3?10:3);
            var tile=new CardTile(card,true,_=>_clicks++,_=>{}) {Position=new Vector2(28+i*410,y),Size=new Vector2(385,405),FaceHeight=365,
                ReducedMotion=_reduced,PreviousForce=i==0?3:i==1?2:card.Force,PreviousChi=i==2?3:i==3?12:card.Chi,StatChangeAge=0};
            AddChild(tile);_cards.Add(tile);
            var label=Palette.Wrapped(new[]{"Force +4","Bowed · Force −2","Chi +2","Chi −2 · double digits"}[i],22,Palette.PaperDim);label.Position=new Vector2(45+i*410,750);label.Size=new Vector2(330,55);AddChild(label);
        }
        var foot=Palette.Text(_reduced?"Reduced motion · static confirmation, no movement":"The final stat is shown immediately; only its confirmation cue animates.",23,Palette.Gold);foot.Position=new Vector2(48,850);AddChild(foot);
    }
    public override void _Process(double delta)
    {
        _clock+=delta;
        try
        {
            foreach(var c in _cards)
                for(var i=0;i<2;i++)if(c.InlayCornersOnViewport(i).Any(p=>!Geometry2D.IsPointInPolygon(p,c.FaceCornersOnViewport())))throw new Exception("Inlay leaves the card plane during bow/hover");
            if(_step==0&&_clock>.4)
            {
                if(_cards.Any(c=>!c.ShowsStatInlays||!c.StatFeedbackActive))throw new Exception("Increase/decrease feedback absent");
                RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng(_output+"-change.png");
                var p=_cards[1].InlayCornersOnViewport(0).Aggregate(Vector2.Zero,(a,b)=>a+b)/4;
                GetViewport().PushInput(new InputEventMouseMotion {Position=p,GlobalPosition=p},true);
                GetViewport().PushInput(new InputEventMouseButton {Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true},true);
                GetViewport().PushInput(new InputEventMouseButton {Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false},true);
                _step++;
            }
            if(_step==1&&_clock>2.1)
            {
                if(_clicks!=1||_cards.Any(c=>c.StatFeedbackActive))throw new Exception("Inlay native click or feedback expiry failed");
                if(_cards[0].Card.Force!=7||_cards[1].Card.Force!=0||_cards[2].Card.Chi!=5||_cards[3].Card.Chi!=10)throw new Exception("Animation changed authoritative displayed values");
                GetViewport().PushInput(new InputEventMouseMotion {Position=new Vector2(5,5),GlobalPosition=new Vector2(5,5)},true);
                RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng(_output+"-settled.png");
                GD.Print("STAT_INLAY_NATIVE_PASS: 0/1/3/6 attachments; Force gain/loss, Chi gain/loss, zero and double digits; bowed inlay native click; expiry; final values unchanged; reduced="+_reduced);GetTree().Quit();
            }
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
