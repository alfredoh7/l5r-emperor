using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Physical presentation around the shared full card face; no new gameplay data.</summary>
public partial class DuelCardTile : CardTile
{
    public bool Upright {get;set;}
    public bool Grounded {get;set;}
    public bool FaceDown {get;set;}
    public DuelCardTile() { }
    public DuelCardTile(CardView card,Action<CardView> clicked):base(card,true,clicked,_=>{}) { }
    public override void _Draw()
    {
        if(Size.X<=1||Card is null)return;
        var inverse=GetGlobalTransform().AffineInverse();
        var quad=FaceCornersInCanvas().Select(p=>inverse*p).ToArray();
        var center=quad.Aggregate(Vector2.Zero,(a,b)=>a+b)/4;
        if(Grounded)
        {
            var foot=(quad[2]+quad[3])/2;
            // Small soft contact at the bottom edge anchors the upright card to cloth.
            for(var layer=5;layer>=0;layer--)
            {
                var radius=new Vector2((quad[2].X-quad[3].X)*.53f+layer*2,5+layer*2.2f);
                var ellipse=Enumerable.Range(0,40).Select(i=>foot+new Vector2(7,4)+Vector2.FromAngle(i*Mathf.Tau/40)*radius).ToArray();
                DrawColoredPolygon(ellipse,new Color(.006f,.005f,.009f,.038f));
            }
        }
        var offset=Upright?new Vector2(5,4):new Vector2(2,3);
        foreach(var spread in new[]{7f,4f,1.5f})
            DrawColoredPolygon(quad.Select(p=>p+(p-center).Normalized()*spread+offset).ToArray(),new Color(.005f,.004f,.007f,Upright?.10f:.075f));
        DrawColoredPolygon(quad.Select(p=>p+new Vector2(1.2f,1.8f)).ToArray(),new Color("1d1815"));
        if(FaceDown)
        {
            DrawColoredPolygon(quad,Colors.White,new Vector2[]{Vector2.Zero,Vector2.Right,Vector2.One,Vector2.Down},TablePile.FateBack);
            if(Highlighted||IsInspectionHover)
                DrawPolyline(quad.Append(quad[0]).ToArray(),Highlighted?SelectionColor:HoverColor,Highlighted?2.2f:1.2f,true);
        }
        else base._Draw();
        // Fine warm paper edge, never an overlay over the printed information.
        DrawLine(quad[3]+Vector2.Down*1.1f,quad[2]+Vector2.Down*1.1f,new Color(.60f,.49f,.34f,.62f),.8f,true);
        DrawLine(quad[0],quad[1],new Color(.93f,.76f,.49f,.32f),.65f,true);
    }
}
