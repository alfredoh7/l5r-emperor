using Godot;

namespace L5R.Client;

/// <summary>Public resources in the two left-hand clan plaques.</summary>
public partial class ClanPlaque : Control
{
    private Label _clan=null!, _honor=null!, _gold=null!;
    public ClanPlaque(string clan, int honor, int gold, bool favor, Action inspect)
    {
        MouseFilter=MouseFilterEnum.Ignore;
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
        if(favor) { var mark=Palette.Text("◆",14,Palette.Gold); mark.Position=new Vector2(239,10); mark.MouseFilter=MouseFilterEnum.Stop; mark.TooltipText="Imperial Favor"; AddChild(mark); }
    }
}
