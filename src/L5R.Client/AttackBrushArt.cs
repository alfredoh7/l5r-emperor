using Godot;

namespace L5R.Client;

/// <summary>Approved red brush artwork. Dashes repeat along a straight intention;
/// the head keeps its painted proportions. This is never a movement or resolved hit.</summary>
public static class AttackBrushArt
{
    private static Texture2D? _atlas,_dash,_head;
    private static Texture2D Atlas=>_atlas??=GD.Load<Texture2D>("res://Art/AttackTargets/red-brush-atlas.png");
    private static Texture2D Dash=>_dash??=new AtlasTexture {Atlas=Atlas,Region=new Rect2(24,216,1488,120),FilterClip=true};
    private static Texture2D Head=>_head??=new AtlasTexture {Atlas=Atlas,Region=new Rect2(296,512,944,320),FilterClip=true};

    public static void Draw(CanvasItem canvas,Vector2 start,Vector2 end)
    {
        var length=start.DistanceTo(end);
        if(length<8)return;
        canvas.DrawSetTransform(start,(end-start).Angle());
        var headWidth=Math.Min(38,length*.48f);
        var headHeight=headWidth*Head.GetHeight()/Head.GetWidth();
        var stem=Math.Max(0,length-headWidth-6);
        var count=Math.Max(1,(int)Math.Ceiling(stem/42));
        var spacing=stem/count;
        for(var i=0;i<count&&stem>3;i++)
        {
            var width=Math.Min(33,spacing*.80f);
            // Only the plain shaft compresses; the individually painted tip never stretches.
            canvas.DrawTextureRect(Dash,new Rect2(i*spacing,-4,width,8),false);
        }
        canvas.DrawTextureRect(Head,new Rect2(length-headWidth,-headHeight/2,headWidth,headHeight),false);
        canvas.DrawSetTransform(Vector2.Zero);
    }
}
