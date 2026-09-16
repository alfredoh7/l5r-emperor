using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Approved A/B pixels, with semantic text and quantities supplied at runtime.</summary>
public static class FallenArt
{
    // Canvas draw commands retain RIDs, not the managed resources. Keep the atlases
    // alive across redraws/GC, including the opponent's idle counter.
    private static Texture2D? _a, _b, _honorable;
    public static Texture2D A => _a ??= GD.Load<Texture2D>("res://Art/Fallen/approved-A.png");
    public static Texture2D B => _b ??= GD.Load<Texture2D>("res://Art/Fallen/approved-B.png");
    private static Texture2D Honorable => _honorable ??= GD.Load<Texture2D>("res://Art/Fallen/honorable-medallion-v1.png");
    public static readonly Rect2 CounterSource = new(75,343,454,103);
    public static readonly Rect2 GallerySource = new(567,94,1077,590);
    public static readonly Color Dishonor = new("e38b7d");
    public static bool IsFallen(CardView card) => card.Zone==Zone.Dead && card.FaceUp && card.Definition?.Type==CardType.Personality;
    public static CardView[] Cards(PlayerView player) => player.Discard.Where(IsFallen).ToArray();
    public static string DeathLabel(CardView card) => card.Dishonorable ? "Died dishonorably" : "Died honorably";
    public static bool TurnsOnTable(CardView card) => card.FaceUp && card.Definition?.Type==CardType.Personality
        && card.Dishonorable && card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached;
    public static void Frame(CanvasItem canvas, Rect2 target, bool counter=false)
        => Border(canvas,target,counter?A:B,counter?CounterSource:GallerySource,counter?13:22);
    public static void Border(CanvasItem canvas,Rect2 target,Texture2D art,Rect2 source,float edge,bool paper=false)
    {
        if(paper) canvas.DrawTextureRectRegion(art,target,new Rect2(600,171,12,26));
        else canvas.DrawTextureRect(CardChangeArt.Lacquer,target,false,new Color(.27f,.27f,.27f));
        float[] xs=[0,edge,source.Size.X-edge,source.Size.X],ys=[0,edge,source.Size.Y-edge,source.Size.Y];
        float[] dx=[target.Position.X,target.Position.X+edge,target.End.X-edge,target.End.X];
        float[] dy=[target.Position.Y,target.Position.Y+edge,target.End.Y-edge,target.End.Y];
        for(var y=0;y<3;y++)for(var x=0;x<3;x++)
        {
            if(x==1&&y==1)continue;
            if(source==CounterSource&&y==2&&x==1)
            {
                // Omit the tooltip pointer baked across the source's lower edge.
                canvas.DrawTextureRectRegion(art,new Rect2(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]),new Rect2(349,433,92,13));
                continue;
            }
            canvas.DrawTextureRectRegion(art,new Rect2(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]),
                new Rect2(source.Position+new Vector2(xs[x],ys[y]),new Vector2(xs[x+1]-xs[x],ys[y+1]-ys[y])));
        }
    }
    public static void Helmet(CanvasItem canvas,Rect2 target)
        => canvas.DrawTextureRectRegion(A,target,new Rect2(119,353,90,76));
    public static void Fan(CanvasItem canvas,Rect2 target,bool dishonorable)
        => canvas.DrawTextureRectRegion(A,target,dishonorable?new Rect2(951,568,57,44):new Rect2(665,568,52,44));
    public static void Medallion(CanvasItem canvas,Vector2 center,float diameter,bool dishonorable)
    {
        if(dishonorable)
        {
            var points=Enumerable.Range(0,80).Select(i=>Vector2.FromAngle(i*Mathf.Tau/80)).ToArray();
            canvas.DrawColoredPolygon(points.Select(p=>center+p*diameter/2).ToArray(),Colors.White,
                points.Select(p=>(new Vector2(1487,292)+p*76)/B.GetSize()).ToArray(),B);
        }
        else
        {
            // Dedicated imagegen companion derived from the approved B medallion.
            canvas.DrawTextureRectRegion(Honorable,new Rect2(center-Vector2.One*diameter/2,Vector2.One*diameter),new Rect2(50,48,1153,1140));
        }
    }
}

public partial class FallenMedallion : Control
{
    public FallenMedallion(){TextureFilter=TextureFilterEnum.LinearWithMipmaps;}
    public bool Dishonorable { get; set; }
    public bool Small { get; init; }
    public override void _Draw()
    {
        if(Small)FallenArt.Fan(this,new Rect2(Vector2.Zero,Size),Dishonorable);
        else FallenArt.Medallion(this,Size/2,Math.Min(Size.X,Size.Y),Dishonorable);
    }
}

public partial class FallenButton : Button
{
    public string Caption { get; set; }="";
    public bool Selected { get; set; }
    public bool Tab { get; init; }
    public int FontSize { get; init; }=20;
    public FallenButton()
    {
        Flat=true;MouseDefaultCursorShape=CursorShape.PointingHand;FocusMode=FocusModeEnum.All;
        foreach(var s in new[]{"normal","pressed","hover","focus"})AddThemeStyleboxOverride(s,new StyleBoxEmpty());
        MouseEntered+=QueueRedraw;MouseExited+=QueueRedraw;FocusEntered+=QueueRedraw;FocusExited+=QueueRedraw;
    }
    public override void _Draw()
    {
        var src=Tab?(Selected?new Rect2(593,160,179,51):new Rect2(781,160,179,48)):new Rect2(1390,620,215,43);
        FallenArt.Border(this,new Rect2(Vector2.Zero,Size),FallenArt.B,src,8,Tab&&Selected);
        if(IsHovered()||HasFocus())DrawRect(new Rect2(2,2,Size.X-4,Size.Y-4),Palette.Gold,false,1);
        var size=FontSize;
        while(size>12&&Palette.Body.GetStringSize(Caption,fontSize:size).X>Size.X-18)size--;
        DrawString(Palette.Body,new Vector2(8,(Size.Y+Palette.Body.GetAscent(size)-Palette.Body.GetDescent(size))/2),
            Caption,HorizontalAlignment.Center,Size.X-16,size,Tab&&Selected?new Color("17140d"):Palette.Paper);
    }
}
