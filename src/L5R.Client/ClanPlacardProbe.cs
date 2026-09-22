using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Native source comparison and public-stat input fixture; not playable card content.</summary>
public partial class ClanPlacardProbe : Control
{
    private double _clock;
    private bool _clicked;
    private int _step;
    private ClanPlaque _actual = null!;
    public override void _Ready()
    {
        Theme = Palette.MakeTheme();
        AddChild(new ColorRect { Color = new Color("090e12"), Size = new Vector2(1672, 941), MouseFilter = MouseFilterEnum.Ignore });
        Label Caption(string text, Vector2 position, int size = 20)
        {
            var label = Palette.Text(text, size, Palette.Paper); label.Position = position;
            label.AddThemeFontOverride("font", ClanPlaque.BodyFace); AddChild(label); return label;
        }
        Caption("APPROVED SOURCE", new Vector2(35, 30), 25);
        Caption("NATIVE GODOT · LIVE VALUES", new Vector2(864, 30), 25);
        var path = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--reference="))[12..];
        var image = Image.LoadFromFile(path);
        var source = new TextureRect { Texture = new AtlasTexture { Atlas = ImageTexture.CreateFromImage(image),
            Region = new Rect2(38, 132, 1097, 385) }, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Position = new Vector2(28, 97), Size = new Vector2(775, 270), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(source);
        _actual = new ClanPlaque("Crane", 11, 2, false, () => _clicked = true, "Crane", "YOUR TURN", true, true, 5, new(8,8,[]))
            { Position = new Vector2(854,97), Size = new Vector2(775,270), ReducedMotion = true };
        AddChild(_actual);
        Caption("Actual HUD size · modified, over-limit and unknown snapshot states", new Vector2(35,420));
        var samples = new[] {
            new ClanPlaque("Crane",11,2,false,()=>{},"Alfredo","YOUR TURN",true,true,5,new(8,8,[])),
            new ClanPlaque("Lion",-12,10,false,()=>{},"Mendon","OPPONENT'S TURN",false,true,6,new(8,9,[new("public-source","Asahina Tsugio",1)])),
            new ClanPlaque("Dragon",120,125,true,()=>{},"A long player name for clipping review","",false,false,12,new(8,10,[new("a","Asahina Tsugio",1),new("b","Asako Kijo",1)])),
            new ClanPlaque("Crab",8,0,false,()=>{},"Older server snapshot","",false,false,4,null)
        };
        for (var i=0;i<samples.Length;i++) { samples[i].Position=new Vector2(35+i*407,470); AddChild(samples[i]); }
        var tooltip = ClanPlaque.MakeStatTooltip("Hand limit", samples[1].HandHelp);
        tooltip.Position = new Vector2(35,630); AddChild(tooltip);
        var explanation = Caption("Counts are public; card identities stay hidden.\nA modified limit uses the engine's current value.\nBeing over the limit does not block drawing.\nThe End Phase remains the enforcement point.", new Vector2(500,655), 22);
        explanation.Size = new Vector2(690,160);
        if(samples[3].HandText!="4 / —") throw new InvalidOperationException("Missing limit must not default to eight.");
        if(!samples[1].HandHelp.Contains("Asahina Tsugio"))throw new InvalidOperationException("Modifier source missing.");
    }
    public override void _Process(double delta)
    {
        _clock+=delta;if(_clock<1)return;
        try
        {
            if(_step==0)
            {
                var p=_actual.GlobalPosition+new Vector2(50,52)*2.5f;
                GetViewport().PushInput(new InputEventMouseButton {Position=p,ButtonIndex=MouseButton.Left,Pressed=true},true);
                GetViewport().PushInput(new InputEventMouseButton {Position=p,ButtonIndex=MouseButton.Left,Pressed=false},true);
                _actual.HandRegion.GrabFocus(); _step++; _clock=0;return;
            }
            if(!_clicked)throw new InvalidOperationException("Clan inspection no longer responds to native input.");
            if(_step==1)
            {
                if(!_actual.HandRegion.HasFocus())throw new InvalidOperationException("Hand inspection cannot receive keyboard focus.");
                _actual.HandRegion.ReleaseFocus(); _clock=0; _step++; return;
            }
            if(_actual.HandText!="5 / 8")throw new InvalidOperationException("Live count text mismatch.");
            RenderingServer.ForceDraw();
            GetViewport().GetTexture().GetImage().SavePng(OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--output="))[9..]);
            GD.Print("CLAN_PLACARD_PASS: source/native capture; known/modified/over-limit/unknown states; clan click and hand focus.");
            GetTree().Quit();
        }
        catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
