using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class CardTile
{
    public int? PreviousForce { get; set; }
    public int? PreviousChi { get; set; }
    public double StatChangeAge { get; set; } = 10;
    public bool ShowsStatInlays => Card.Definition is { Type: CardType.Personality, ScenarioOnly: false } && Card.FaceUp && Card.Zone is Zone.Home or Zone.Battlefield;
    public bool StatFeedbackActive => ShowsStatInlays && StatChangeAge < (ReducedMotion ? .7 : 1.5) && (PreviousForce is int f && f!=Card.Force || PreviousChi is int c && c!=Card.Chi);
    private static Texture2D? _inlayAtlas;
    // The approved concept uses upright, open numerals; brush headings remain elsewhere.
    private static readonly Font InlayNumerals = new FontVariation { BaseFont = Palette.Body, VariationEmbolden = .7f, OpentypeFeatures = new Godot.Collections.Dictionary { ["lnum"] = 1, ["tnum"] = 1 } };
    // Approved A: broad recessed corner panels seated within the printed header.
    // They cover the old numeral and posts, without encroaching on the name or artwork.
    private Rect2 InlayRect(int index)
    {
        var face=FaceSize();
        return new Rect2(face.X*(index==0?.027f:.823f),face.Y*.019f,face.X*.15f,face.Y*.107f);
    }
    public Vector2[] InlayCornersOnViewport(int index)
    {
        var r=InlayRect(index); var face=FaceSize();
        return new[]{r.Position,r.Position+new Vector2(r.Size.X,0),r.End,r.Position+new Vector2(0,r.Size.Y)}
            .Select(p=>GetGlobalTransform()*ProjectFacePoint(p,face)).Select(p=>ProjectToScreen?.Invoke(p)??p).ToArray();
    }
    private void AdvanceStatFeedback(double delta)
    {
        if(StatChangeAge>=2)return;
        StatChangeAge+=delta; if(ShowsStatInlays)QueueRedraw();
    }
    private void DrawStatInlays()
    {
        if(!ShowsStatInlays)return;
        _inlayAtlas ??= GD.Load<Texture2D>("res://Art/stat-inlays-v2.png");
        var face=FaceSize(); var origin=ProjectFacePoint(Vector2.Zero,face);
        DrawSetTransformMatrix(new Transform2D(ProjectFacePoint(Vector2.Right,face)-origin,ProjectFacePoint(Vector2.Down,face)-origin,origin));
        for(var i=0;i<2;i++)
        {
            var r=InlayRect(i);
            DrawTextureRectRegion(_inlayAtlas,r,new Rect2(i*_inlayAtlas.GetWidth()/2f,0,_inlayAtlas.GetWidth()/2f,_inlayAtlas.GetHeight()));
            var current=i==0?Card.Force:Card.Chi;
            var old=i==0?PreviousForce:PreviousChi;
            var change=old is int previous?(long)current-previous:0;
            var active=StatFeedbackActive && change!=0;
            var color=change>0?new Color("cbe0ae"):new Color("e5aaa0");
            var pulse=ReducedMotion?.32f:Mathf.Sin(Mathf.Clamp((float)StatChangeAge/1.5f,0,1)*Mathf.Pi)*.55f;
            if(active)
            {
                DrawRect(r,color with{A=pulse*.3f});
                for(var spread=3;spread>=1;spread--)DrawRect(r.Grow(spread*.4f),color with{A=pulse/(spread+1)},false,1,true);
            }
            var value=current.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var size=Math.Max(4,(int)(r.Size.Y*1.04f));
            while(size>2&&InlayNumerals.GetStringSize(value,fontSize:size).X>r.Size.X*.70f)size--;
            var baseline=r.GetCenter().Y+(InlayNumerals.GetAscent(size)-InlayNumerals.GetDescent(size))/2;
            DrawStringOutline(InlayNumerals,new Vector2(r.Position.X,baseline),value,HorizontalAlignment.Center,r.Size.X,size,Math.Max(1,size/18),new Color("211b17"));
            DrawString(InlayNumerals,new Vector2(r.Position.X,baseline),value,HorizontalAlignment.Center,r.Size.X,size,new Color("eef3ff"));
            if(active)
            {
                var amount=change.ToString("+0;-0;0",System.Globalization.CultureInfo.InvariantCulture);
                var small=Math.Max(5,(int)(face.Y*.06f));
                var offset=ReducedMotion?0:Mathf.Sin((float)StatChangeAge/1.5f*Mathf.Pi)*face.Y*.009f;
                var opacity=ReducedMotion?1:1-Mathf.SmoothStep(.9f,1.5f,(float)StatChangeAge);
                var point=new Vector2(r.Position.X-4,r.End.Y+small+face.Y*.012f-offset);
                DrawStringOutline(InlayNumerals,point,amount,HorizontalAlignment.Center,r.Size.X+8,small,2,new Color(0,0,0,opacity*.8f));
                DrawString(InlayNumerals,point,amount,HorizontalAlignment.Center,r.Size.X+8,small,color with{A=opacity});
            }
        }
        DrawSetTransformMatrix(Transform2D.Identity);
    }
}
