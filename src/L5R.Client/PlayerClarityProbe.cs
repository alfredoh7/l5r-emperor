using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Native UI fixture using public views; never enters the playable catalogue.</summary>
public partial class PlayerClarityProbe : Control
{
    private PhaseTrack _track=null!;
    private MatchEngine _engine=null!;
    private MatchView _view=null!;
    private string _output="";
    private double _clock;
    private int _step;
    private long _version;
    private readonly List<CardTile> _cards=[];
    public override void _Ready()
    {
        _output=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--output="))[9..];
        Theme=Palette.MakeTheme();
        AddChild(new ColorRect { Color=new Color("101c25"),Size=new Vector2(1672,941),MouseFilter=MouseFilterEnum.Ignore });
        _engine=MatchEngine.CreateStarterMatch(91); _view=_engine.GetView(_engine.State.PriorityPlayerId);
        _track=new PhaseTrack { Position=new Vector2(330,18),Size=new Vector2(1020,130),ReducedMotion=true }; AddChild(_track);
        var view=_view with { ActivePlayerId=0, PriorityPlayerId=1,ViewerId=1,Players=_view.Players.Select(p=>p with{ Clan="Crane",Name=p.Id==0?"Mendon":"Alfredo" }).ToArray() };
        _track.Present(view); _track.PresentActions(view,[new ActionOption("pass","Pass",true,null,new GameCommand("pass",1,view.Version))],true);
        foreach(var p in view.Players)
            AddChild(new ClanPlaque(p.Clan,p.Honor,4,false,()=>{},p.Name,p.Id==0?"OPPONENT'S TURN":"",p.Id==1,p.Id==0) { Position=new Vector2(24,18+p.Id*146),Size=new Vector2(270,120),ReducedMotion=true });
        var note=Palette.Wrapped("Same-clan example: Mendon owns the turn; Alfredo may act.\nSteady gold plaque · blue action rail",23,Palette.PaperDim); note.Position=new Vector2(350,164);note.Size=new Vector2(900,70);AddChild(note);
        var def=PublishedContent.Snapshot.Cards.First(d=>d.Name.Contains("Takakura"));
        for(var i=0;i<3;i++)
        {
            var card=new CardView("public-fixture-"+i,def,1,Zone.Home,true,i==1,null,null,3,3,GrantedKeywords:i==2?[new("Elite","Test attachment","Attachment"),new("Cavalry","Test effect","Turn"),new("Naval","Test effect","Turn")]:[new("Elite","Test effect","Turn")],Dishonorable:i!=2,UnitForce:i==1?0:3);
            if(i!=1)
            {
                var follower=PublishedContent.Snapshot.Cards.First(d=>d.Name=="Native Guide");
                for(var j=(i==0?1:3);j>=1;j--)
                    AddChild(new CardTile(new CardView("follower-"+i+"-"+j,follower,1,Zone.Attached,true,false,null,card.Id,5,0),false,_=>{},_=>{})
                    { Position=new Vector2(350+i*390,275-j*20),Size=new Vector2(355,360),FaceHeight=340,ReducedMotion=true });
            }
            var tile=new CardTile(card,false,_=>{},_=>{}) {Position=new Vector2(350+i*390,275),Size=new Vector2(355,360),FaceHeight=340,ReducedMotion=true}; AddChild(tile);_cards.Add(tile);
        }
        var legends=Palette.Text("Public status fixtures · one attachment / bowed / three attachments + granted keywords",23,Palette.PaperDim);legends.Position=new Vector2(350,663);AddChild(legends);
        foreach(var (clan,i) in new[]{"Lion","Crane","Mantis","Spider"}.Select((c,i)=>(c,i)))
            AddChild(new ClanPlaque(clan,12,4,false,()=>{},"", "",false,false) {Position=new Vector2(350+i*310,740),Size=new Vector2(270,120),ReducedMotion=true});
    }
    public override void _Process(double delta)
    {
        _clock+=delta; if(_clock<1.2)return;
        try
        {
            if(_step==0)
            {
                if(_cards[0].PublicStatusLabels.Length!=2 || _cards[1].PublicStatusLabels.Length!=2 || _cards[2].PublicStatusLabels.Length!=3)throw new Exception("Public status labels lost");
                var hidden=_cards[0].Card with{ Definition=null,FaceUp=false };
                var tile=new CardTile(hidden,true,_=>{},_=>{});
                if(tile.PublicStatusLabels.Length!=0)throw new Exception("Hidden card status leak"); tile.Free();
                if(_track.PassButton.Disabled || _track.PriorityLabel.Text!="You may act or pass")throw new Exception("Action cue missing");
                RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(_output);
                // Exercise native keyboard activation against a real enabled engine command.
                _view=_engine.GetView(_engine.State.PriorityPlayerId);var actions=_engine.QueryActions(_view.ViewerId);
                var pass=actions.First(a=>a.Enabled && a.Type=="pass");
                _track.Present(_view);_track.PresentActions(_view,actions,true);_version=_engine.Version;
                _track.PassRequested=()=>{var result=_engine.Execute(pass.Command!);if(!result.Accepted)throw new Exception(result.Error);};
                _track.PassButton.GrabFocus();
                GetViewport().PushInput(new InputEventKey {Keycode=Key.Enter,PhysicalKeycode=Key.Enter,Pressed=true},true);
                GetViewport().PushInput(new InputEventKey {Keycode=Key.Enter,PhysicalKeycode=Key.Enter,Pressed=false},true);
                _step++;_clock=0;return;
            }
            if(_engine.Version!=_version+1)throw new Exception("Keyboard Pass did not submit exactly once");
            _track.PresentActions(_view,[],true);if(!_track.PassButton.Disabled || _track.PriorityLabel.Text.Contains("or pass"))throw new Exception("Required choice offers illegal pass");
            _track.PresentActions(_view,_engine.QueryActions(_view.ViewerId),false);if(!_track.PassButton.Disabled)throw new Exception("Disconnected controls remain enabled");
            _track.PresentActions(_view with{ViewerId=1-_view.PriorityPlayerId},[],true);if(!_track.PriorityLabel.Text.Contains("Opponent"))throw new Exception("Opponent cue ambiguous");
            GD.Print("PLAYER_CLARITY_PASS: same-clan owner/priority split; native keyboard Pass executes one authoritative command; required-choice/disconnected Pass disabled; hidden status suppressed; bowed/stacked keyword labels.");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
