using Godot;

namespace L5R.Client;

/// <summary>Public turn, priority and resources in the two fixed player plaques.</summary>
public partial class ClanPlaque : Control
{
    public bool Acting { get; set; }
    public bool TurnOwner { get; }
    public bool ReducedMotion { get; set; }
    public string TurnText { get; }
    public string ActionText { get; }
    private float _time;
    public ClanPlaque(string clan, int honor, int gold, bool favor, Action inspect, string action = "", string turn = "", bool acting = false, bool turnOwner = false)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Acting = acting; TurnOwner = turnOwner; TurnText=turn; ActionText=action;
        var frame=new LacquerPanel { MouseFilter=MouseFilterEnum.Ignore };
        AddChild(frame); frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var icon=Emblem(clan,new Vector2(94,94)); icon.Position=new Vector2(8,11);
        AddChild(icon);
        var title=Palette.Text(clan.ToUpperInvariant(),23,Palette.Paper,true); title.Position=new Vector2(111,7); AddChild(title);
        var turnLabel=Palette.Text(turn,15,Palette.Gold,true); turnLabel.Position=new Vector2(111,36); AddChild(turnLabel);
        var cue=Palette.Text(action,14,Palette.PaperDim); cue.Position=new Vector2(111,57); cue.Size=new Vector2(148,23); cue.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis; cue.TooltipText=action; cue.MouseFilter=MouseFilterEnum.Stop; AddChild(cue);
        var fan=new HonorFan { Position=new Vector2(110,86),Size=new Vector2(26,26),MouseFilter=MouseFilterEnum.Stop,TooltipText="Family Honor" }; AddChild(fan);
        var honorLabel=Palette.Text(honor.ToString(),23,Palette.Paper); honorLabel.Position=new Vector2(141,83); honorLabel.MouseFilter=MouseFilterEnum.Stop; honorLabel.TooltipText="Family Honor"; AddChild(honorLabel);
        var coin=Decal("koku-reference.png",new Rect2(5,10,548,560),new Vector2(191,86),new Vector2(27,27),true);
        coin.MouseFilter=MouseFilterEnum.Stop; coin.TooltipText="Available Gold production · produced when paying, not a saved balance"; AddChild(coin);
        var goldLabel=Palette.Text(gold.ToString(),23,Palette.Paper); goldLabel.Position=new Vector2(223,83); goldLabel.TooltipText=coin.TooltipText; goldLabel.MouseFilter=MouseFilterEnum.Stop; AddChild(goldLabel);
        var hit=new TooltipButton { Flat=true,Text="",Position=new Vector2(5,6),Size=new Vector2(101,103),TooltipText=clan+" · inspect dead cards and lost provinces" };
        foreach(var state in new[]{"normal","pressed","hover","focus"}) hit.AddThemeStyleboxOverride(state,Palette.Box(Colors.Transparent,state is "hover" or "focus"?Palette.Gold:null,0,50));
        hit.Pressed+=inspect; AddChild(hit);
        if(favor) { var mark=Palette.Text("◆",14,Palette.Gold); mark.Position=new Vector2(247,7); mark.MouseFilter=MouseFilterEnum.Stop; mark.TooltipText="Imperial Favor"; AddChild(mark); }
        // Draw glow over the lacquer so it cannot be hidden behind its opaque bitmap.
        var glow = new PlaqueGlow(this) { MouseFilter=MouseFilterEnum.Ignore }; AddChild(glow);
    }
    public static TextureRect Emblem(string clan,Vector2 size)
    {
        var emblemRegion = clan switch {
            "Lion" => new Rect2(114,225,224,218), "Crane" => new Rect2(574,220,225,223),
            "Phoenix" => new Rect2(0,0,222,222), "Scorpion" => new Rect2(234,0,219,222),
            "Crab" => new Rect2(465,0,218,222), "Unicorn" => new Rect2(689,0,220,222),
            "Dragon" => new Rect2(340,220,224,223), _ => new Rect2(114,225,224,218)
        };
        var extra = clan is "Mantis" or "Spider" ? GD.Load<Texture2D>("res://Art/clan-"+clan.ToLowerInvariant()+".png") : null;
        return Decal(extra is null ? "clan-reference.png" : "clan-"+clan.ToLowerInvariant()+".png", extra is null ? emblemRegion : new Rect2(Vector2.Zero,extra.GetSize()),Vector2.Zero,size,false);
    }
    private static TextureRect Decal(string file,Rect2 region,Vector2 position,Vector2 size,bool coin)
    {
        var sprite=new TextureRect { Texture=new AtlasTexture { Atlas=GD.Load<Texture2D>("res://Art/"+file),Region=region },
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,
            Position=position,Size=size,MouseFilter=MouseFilterEnum.Ignore,TextureFilter=TextureFilterEnum.LinearWithMipmaps };
        var material=new ShaderMaterial { Shader=GD.Load<Shader>("res://Art/aged-emblem.gdshader") };
        material.SetShaderParameter("decal_size",size); material.SetShaderParameter("white_cutout",coin); material.SetShaderParameter("saturation",coin?.80f:.53f);
        sprite.Material=material; return sprite;
    }
    public override void _Process(double delta)
    {
        if (!TurnOwner || ReducedMotion) return;
        _time+=(float)delta; GetChildren().OfType<PlaqueGlow>().Single().QueueRedraw();
    }
    private partial class HonorFan : Control
    { public override void _Draw() => TableIconButton.DrawGlyph(this,TableGlyph.Honor,new Rect2(Vector2.Zero,Size),Palette.Gold); }
    private partial class PlaqueGlow(ClanPlaque plaque) : Control
    {
        public override void _Draw()
        {
            if (!plaque.TurnOwner) return;
            var pulse=1f; // Steady warm frame denotes turn ownership; phase light denotes action.
            var strength=1f;
            var r=new Rect2(1,1,268,118);
            foreach(var (spread,alpha,width) in new[]{(9f,.10f,8f),(5f,.18f,5f),(2f,.42f,3f),(0f,.94f,2f)})
            {
                var halo=Palette.Box(Colors.Transparent,Palette.Gold with { A=alpha*strength*pulse },0,7+(int)spread);
                halo.BorderWidthLeft=halo.BorderWidthRight=halo.BorderWidthTop=halo.BorderWidthBottom=(int)width;
                DrawStyleBox(halo,r.Grow(spread));
            }
            DrawLine(new Vector2(110,77),new Vector2(256,77),Palette.Gold with{ A=.2f*strength },5,true);
            DrawLine(new Vector2(112,77),new Vector2(254,77),Palette.Gold with{ A=.8f*strength },1,true);
        }
    }
}
