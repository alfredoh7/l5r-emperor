using Godot;
namespace L5R.Client;

public enum TableGlyph { Menu, Rules, Fit, Battle, Honor }

/// <summary>Small engraved table tools, with native buttons and readable tooltips.</summary>
public partial class TableIconButton : TooltipButton
{
    public TableGlyph Glyph { get; }
    public TableIconButton(TableGlyph glyph, Action pressed, string help)
    {
        Glyph = glyph; Text = ""; TooltipText = help;
        if(glyph is TableGlyph.Fit or TableGlyph.Battle) FocusMode=FocusModeEnum.None;
        CustomMinimumSize = new Vector2(46, 44);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        Pressed += pressed; MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var hot = IsHovered() || HasFocus();
        var r = new Rect2(2, 2, Size.X - 4, Size.Y - 4);
        DrawStyleBox(Palette.Box(new Color("111718"), new Color(hot ? "c5a66e" : "665337"), 0, 5), r);
        DrawStyleBox(Palette.Box(Colors.Transparent, new Color("342f24"), 0, 3), r.Grow(-3));
        if (hot) DrawStyleBox(Palette.Box(Colors.Transparent, new Color(.9f,.67f,.31f,.13f), 0, 6),r.Grow(2));
        // The motifs are engraved into the lacquer, with a dark recess and warm lip.
        DrawGlyph(this, Glyph, new Rect2((Size.X-28)/2, (Size.Y-28)/2+1, 28, 28), new Color("070908"));
        DrawGlyph(this, Glyph, new Rect2((Size.X-28)/2, (Size.Y-28)/2, 28, 28), hot ? Palette.Paper : Palette.Gold);
    }
    public static void DrawGlyph(CanvasItem canvas, TableGlyph glyph, Rect2 box, Color color)
    {
        Vector2 P(float x, float y) => box.Position + new Vector2(x*box.Size.X,y*box.Size.Y);
        void Line(float x,float y,float xx,float yy,float width=1.6f) => canvas.DrawLine(P(x,y),P(xx,yy),color,width,true);
        switch(glyph)
        {
            case TableGlyph.Menu:
                for(var i=0;i<3;i++) { var y=.24f+i*.26f; Line(.09f,y,.91f,y,2.2f); Line(.09f,y+.055f,.79f,y+.055f,.6f); }
                break;
            case TableGlyph.Rules:
                canvas.DrawStyleBox(Palette.Box(new Color(.5f,.4f,.23f,.12f),color,0,2),new Rect2(P(.2f,.12f),box.Size*new Vector2(.60f,.76f)));
                Line(.12f,.10f,.88f,.10f,2.5f); Line(.12f,.90f,.88f,.90f,2.5f);
                for(var i=0;i<3;i++) Line(.34f,.34f+i*.16f,i==2?.57f:.68f,.34f+i*.16f,1.2f);
                break;
            case TableGlyph.Fit:
                foreach(var (x,y,sx,sy) in new[]{(.1f,.1f,1f,1f),(.9f,.1f,-1f,1f),(.1f,.9f,1f,-1f),(.9f,.9f,-1f,-1f)})
                { Line(x,y,x+sx*.23f,y,2); Line(x,y,x,y+sy*.23f,2); }
                canvas.DrawRect(new Rect2(P(.37f,.31f),box.Size*new Vector2(.26f,.38f)),color,false,1,true);
                break;
            case TableGlyph.Battle:
                Line(.18f,.12f,.80f,.87f,2); Line(.82f,.12f,.20f,.87f,2);
                Line(.07f,.67f,.40f,.90f,2); Line(.60f,.90f,.93f,.67f,2);
                break;
            case TableGlyph.Honor:
                var pivot=P(.5f,.94f); var radius=box.Size.X*.69f;
                var points=new List<Vector2>{pivot};
                for(var i=0;i<=24;i++) points.Add(pivot+Vector2.FromAngle(Mathf.DegToRad(224+i*(92f/24)))*radius);
                canvas.DrawColoredPolygon(points.ToArray(),new Color("bda67e"));
                canvas.DrawPolyline(points.Append(points[0]).ToArray(),new Color("594227"),1.3f,true);
                for(var i=0;i<=8;i++) canvas.DrawLine(pivot,pivot+Vector2.FromAngle(Mathf.DegToRad(224+i*11.5f))*radius,new Color("715d3d"),.9f,true);
                canvas.DrawArc(pivot,radius*.89f,Mathf.DegToRad(224),Mathf.DegToRad(316),30,color,1,true);
                canvas.DrawCircle(pivot,1.7f,new Color("d4b879"));
                break;
        }
    }
}
