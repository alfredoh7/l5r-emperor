using Godot;

namespace L5R.Client;

/// <summary>Public resources in the two left-hand clan plaques.</summary>
public partial class ClanPlaque : Control
{
    private Label _clan=null!, _honor=null!, _gold=null!;
    public bool Acting { get; set; }
    public bool ReducedMotion { get; set; }
    private float _time;
    private Color _accent;
    public ClanPlaque(string clan, int honor, int gold, bool favor, Action inspect, string action = "")
    {
        MouseFilter=MouseFilterEnum.Ignore;
        Acting = action.Length > 0;
        _accent = clan == "Lion" ? new Color("f1c976") : new Color("9bd4e4");
        var frame=new LacquerPanel { MouseFilter=MouseFilterEnum.Ignore };
        AddChild(frame); frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var sheet=GD.Load<Texture2D>("res://Art/clan-medallions.png");
        var icon=new TextureRect { Texture=new AtlasTexture { Atlas=sheet, Region=new Rect2(clan=="Lion"?24:791,122,724,732) }, ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize, StretchMode=TextureRect.StretchModeEnum.Scale, Position=new Vector2(8,10),Size=new Vector2(98,98),MouseFilter=MouseFilterEnum.Ignore };
        // Circular native clipping keeps the sprite sheet's background outside the emblem.
        icon.Material=new ShaderMaterial { Shader=new Shader { Code="shader_type canvas_item; varying vec2 local; void vertex(){local=VERTEX/98.0;} void fragment(){COLOR.a*=1.0-smoothstep(0.488,0.498,length(local-vec2(0.5)));}" } };
        icon.TextureFilter=TextureFilterEnum.LinearWithMipmaps;
        AddChild(icon);
        _clan=Palette.Text(clan.ToUpperInvariant(),23,Palette.Paper,true); _clan.Position=new Vector2(115,21); AddChild(_clan);
        _honor=Palette.Text("✺  "+honor,25,Palette.Gold); _honor.Position=new Vector2(115,62); AddChild(_honor);
        _gold=Palette.Text("◈  "+gold,25,Palette.Gold); _gold.Position=new Vector2(185,62); AddChild(_gold);
        foreach(var (label,help) in new[]{(_honor,"Family Honor"),(_gold,"Available Gold production · produced when paying, not a saved balance")}) { label.MouseFilter=MouseFilterEnum.Stop; label.TooltipText=help; }
        var hit=new TooltipButton { Flat=true,Text="",Position=new Vector2(5,6),Size=new Vector2(101,103),TooltipText=clan+" · inspect dead cards and lost provinces" };
        hit.AddThemeStyleboxOverride("normal",Palette.Box(Colors.Transparent,null,0));
        hit.AddThemeStyleboxOverride("hover",Palette.Box(new Color(.8f,.62f,.3f,.08f),Palette.Gold,0,50));
        hit.AddThemeStyleboxOverride("pressed",Palette.Box(new Color(.8f,.62f,.3f,.14f),Palette.Gold,0,50));
        hit.AddThemeStyleboxOverride("focus",Palette.Box(Colors.Transparent,Palette.Gold,0,50));
        hit.Pressed+=inspect; AddChild(hit);
        if (Acting)
        {
            var cue = Palette.Text(action, 13, _accent); cue.Position = new Vector2(115, 96);
            cue.MouseFilter = MouseFilterEnum.Ignore; AddChild(cue);
        }
        if(favor) { var mark=Palette.Text("◆",14,Palette.Gold); mark.Position=new Vector2(239,10); mark.MouseFilter=MouseFilterEnum.Stop; mark.TooltipText="Imperial Favor"; AddChild(mark); }
    }

    public override void _Process(double delta)
    {
        if (!Acting || ReducedMotion) return;
        _time += (float)delta; QueueRedraw();
    }
    public override void _Draw()
    {
        if (!Acting) return;
        var pulse = ReducedMotion ? 1 : 1 + .06f * Mathf.Sin(_time * 2);
        foreach (var (spread, alpha) in new[] { (7f, .06f), (4f, .15f), (1f, .85f) })
            DrawRect(new Rect2(Vector2.Zero, Size).Grow(spread), _accent with { A = alpha * pulse }, false, spread > 1 ? 3 : 2, true);
    }
}
