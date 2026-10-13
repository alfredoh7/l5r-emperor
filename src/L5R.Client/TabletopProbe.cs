using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Explicit visual fixture: count boundaries and supplied Clan emblems.</summary>
public partial class TabletopProbe : Control
{
    private string _output="";
    private readonly List<TablePile> _piles=[];
    private double _clock;
    public override void _Ready()
    {
        _output=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--output="))[9..];
        Theme=Palette.MakeTheme();
        AddChild(new ColorRect { Color=new Color("101c25"),Size=GetViewportRect().Size,MouseFilter=MouseFilterEnum.Ignore });
        var def=PublishedContent.Snapshot.Cards.First(d=>d.Type==CardType.Stronghold);
        var card=new CardView("fixture",def,0,Zone.Stronghold,true,false,null,null,0,0);
        var player=new PlayerView(0,"Fixture","Lion",12,1,card,[],[],[],[],0,[],false,false,[],0);
        foreach(var (count,col) in new[]{0,1,8,20,40}.Select((n,i)=>(n,i)))
        {
            var label=Palette.Text($"{count} cards",22); label.Position=new Vector2(90+col*260,26);AddChild(label);
            foreach(var row in new[]{0,1})
            {
                var p=new TablePile(player,row==0?Zone.DynastyDeck:Zone.FateDeck,()=>{})
                { Position=new Vector2(95+col*260,140+row*270),Size=new Vector2(150,210) };
                AddChild(p);_piles.Add(p);
            }
        }
        foreach(var (clan,i) in new[]{"Lion","Crane","Phoenix","Scorpion","Crab","Unicorn","Dragon"}.Select((c,i)=>(c,i)))
        {
            var p=new ClanPlaque(clan,i==0?-12:12,10,false,()=>{},i==1?"Your response":i==0?"Awaiting response":"",i==0?"OPPONENT'S TURN":"",i==1,i==0)
            { Position=new Vector2(30+(i%4)*325,710+(i/4)*140),Size=new Vector2(270,120),ReducedMotion=true }; AddChild(p);
        }
    }
    public override void _Process(double delta)
    {
        _clock+=delta;if(_clock<1.5)return;
        try
        {
            foreach(var p in _piles)
            {
                var before=p.TopFace;
                if(!p._HasPoint(before.Position+Vector2.One)||p._HasPoint(before.Position-new Vector2(10,10)))throw new Exception("Pile hit outline does not match raised face");
                p.SettleFrom(p.Count+1,false);p._Process(.325);
                if(p.ShownCount<=p.Count||p.ShownCount>=p.Count+1||p.TopFace.Position.Y>=before.Position.Y) throw new Exception("Pile height does not follow an in-flight draw");
                p.SettleFrom(p.Count,true);
                if(p.ShownCount!=p.Count||p.TopFace!=before)throw new Exception("Reduced-motion pile did not settle exactly");
            }
            RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng(_output);
            GD.Print("TABLETOP_PASS: both deck backs at 0/1/8/20/40 cards; draw interpolation including last-to-empty; exact reduced-motion settling.");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
