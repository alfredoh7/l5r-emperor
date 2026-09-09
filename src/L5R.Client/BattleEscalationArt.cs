using Godot;

namespace L5R.Client;

/// <summary>Selected source pixels. Only plain runs stretch; corners retain their proportions.</summary>
public static class BattleEscalationArt
{
    private static readonly Dictionary<string, Texture2D> Cache = [];
    public static Texture2D Daisho => Crop("res://Art/daisho.png", new Rect2I(54,54,1146,1146));
    public static Texture2D Arrow => BattleInkArt.Extract("res://Art/battle-detail-atlas.png", new Rect2I(106,130,622,293));
    public static Texture2D Step => BattleInkArt.Extract("res://Art/battle-detail-atlas.png", new Rect2I(950,180,464,182));
    public static Texture2D Journal => Crop("res://Art/war-journal.png", new Rect2I(116,65,789,1370));
    public static Texture2D Announcement => Crop("res://Art/battle-detail-atlas.png", new Rect2I(40,548,1452,314), true);

    public static Texture2D Crop(string path, Rect2I rect, bool darkInk = false)
    {
        var key = path + rect + darkInk;
        if (Cache.TryGetValue(key, out var texture)) return texture;
        using var source = GD.Load<Texture2D>(path).GetImage();
        if (source.IsCompressed()) source.Decompress();
        source.Convert(Image.Format.Rgba8);
        using var region = source.GetRegion(rect);
        if (darkInk)
        {
            // The atlas has a uniform navy backdrop, not painted transparency.
            // Keep both black brush fibers and red pigment; discard only navy.
            var bytes = region.GetData();
            for (var i=0; i<bytes.Length; i+=4)
            {
                var navy = Math.Clamp((bytes[i+2]-bytes[i]-3)/18f, 0, 1);
                bytes[i+3] = (byte)(255*(1-navy));
            }
            using var keyed = Image.CreateFromData(rect.Size.X,rect.Size.Y,false,Image.Format.Rgba8,bytes);
            keyed.GenerateMipmaps(); return Cache[key] = ImageTexture.CreateFromImage(keyed);
        }
        region.GenerateMipmaps(); return Cache[key] = ImageTexture.CreateFromImage(region);
    }

    public static void Frayed(Control c, Rect2 bounds, float opacity)
    {
        var source = BattleInkArt.Extract("res://Art/frayed-province-reference.png",new Rect2I(432,145,677,620),borderPigment:true);
        // Retain the actual torn fibers; leave the broken numeral to the live seal.
        const float corner=36, line=14;
        void P(Rect2 d, Rect2 s) => c.DrawTextureRectRegion(source,new Rect2(d.Position+bounds.Position,d.Size),s,new Color(1,.94f,.85f,opacity));
        var w=bounds.Size.X; var h=bounds.Size.Y;
        P(new Rect2(0,0,corner,corner),new Rect2(0,0,130,130));
        P(new Rect2(w-corner,0,corner,corner),new Rect2(547,0,130,130));
        P(new Rect2(0,h-corner,corner,corner),new Rect2(0,470,130,130));
        P(new Rect2(w-corner,h-corner,corner,corner),new Rect2(547,470,130,130));
        P(new Rect2(corner,0,w-2*corner,line),new Rect2(135,0,400,65));
        P(new Rect2(0,corner,line,h-2*corner),new Rect2(0,140,65,320));
        P(new Rect2(w-line,corner,line,h-2*corner),new Rect2(612,140,65,320));
        // Split bottom run keeps the live extinguished province seal readable.
        P(new Rect2(corner,h-line,w/2-corner-15,line),new Rect2(135,585,120,32));
        P(new Rect2(w/2+15,h-line,w/2-corner-15,line),new Rect2(440,585,95,32));
    }

    public static void Crimson(Control c, Vector2 size, bool above)
    {
        // 3A's actual rest close-up, isolated by warm pigment. Its blood and
        // gold detail are retained, rather than redrawing a geometric border.
        var corner=BattleInkArt.Extract("res://Art/crimson-reference.png",new Rect2I(1300,285,158,132),borderPigment:true);
        var run=BattleInkArt.Extract("res://Art/crimson-reference.png",new Rect2I(1145,328,152,61),borderPigment:true);
        void Corner(Vector2 p,float angle)
        { c.DrawSetTransform(p,angle); c.DrawTextureRect(corner,new Rect2(-40.2f,-43.2f,94.8f,79.2f),false); }
        Corner(Vector2.Zero,Mathf.Pi); Corner(new(size.X,0),-Mathf.Pi/2);
        Corner(size,0); Corner(new(0,size.Y),Mathf.Pi/2);
        void Line(Vector2 p,float angle,float length)
        { if(length<=0) return; c.DrawSetTransform(p,angle); c.DrawTextureRect(run,new Rect2(0,-17.4f,length,36.6f),false); }
        const float inset=32;
        // Gaps beneath the daisho and at the neck preserve the approved join.
        foreach(var y in new[]{0f,size.Y})
        {
            var gap = (y==0)==above ? 37f : 23f;
            Line(new(inset,y),0,size.X/2-gap-inset);
            Line(new(size.X/2+gap,y),0,size.X/2-gap-inset);
        }
        Line(new(0,inset),Mathf.Pi/2,size.Y-2*inset);
        Line(new(size.X,inset),Mathf.Pi/2,size.Y-2*inset);
        c.DrawSetTransform(Vector2.Zero);
        BattleInkArt.Neck(c,new Vector2(size.X/2,above?0:size.Y),above,new Color(.70f,.22f,.18f));
        // The seal itself stays brass, legible and physically mounted.
        c.DrawTextureRect(Daisho,new Rect2(size.X/2-27,above?size.Y-27:-27,54,54),false);
    }
}

public partial class CrimsonCourt : Control
{
    public bool Above { get; set; }
    private readonly ShaderMaterial _blood = new() { Shader = GD.Load<Shader>("res://Art/blood-soak.gdshader") };
    public override void _Ready()
    { MouseFilter=MouseFilterEnum.Ignore; TextureFilter=TextureFilterEnum.LinearWithMipmaps; Material=_blood; }
    public void Present(Rect2 rect,bool above,double age,bool reduced,bool current)
    {
        Position=rect.Position; Size=rect.Size; Above=above;
        _blood.SetShaderParameter("age",(float)(reduced?8:age));
        _blood.SetShaderParameter("still",reduced);
        _blood.SetShaderParameter("emphasis",current?1f:.82f);
        QueueRedraw();
    }
    public override void _Draw() => BattleEscalationArt.Crimson(this,Size,Above);
}
