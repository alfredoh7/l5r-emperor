using Godot;
namespace L5R.Client;

public static class PracticeArt
{
    public static readonly Texture2D A = GD.Load<Texture2D>("res://Art/Practice/approved-A.png");
    public static readonly Texture2D B = GD.Load<Texture2D>("res://Art/Practice/approved-B.png");
    public static readonly Texture2D ButtonPlate = GD.Load<Texture2D>("res://Art/Practice/button-plate.png");
    public static readonly Texture2D Background = GD.Load<Texture2D>("res://Art/Practice/backdrop.png");
    public static readonly Font Bold = GD.Load<FontFile>("res://Art/Fonts/Caudex-Bold.ttf");
    public static readonly Color Ready = new("9bdcd3"), Amber = new("e5b460");
    public static void Frame(CanvasItem canvas, Rect2 rect, bool browser=false)
    {
        var source = browser?new Rect2(29,93,1030,790):new Rect2(155,120,666,686); var texture=browser?B:A;const int edge = 20;
        canvas.DrawTextureRect(CardChangeArt.Lacquer,rect,false,new Color(.27f,.27f,.27f));
        float[] x=[0,edge,source.Size.X-edge,source.Size.X], y=[0,edge,source.Size.Y-edge,source.Size.Y];
        float[] dx=[rect.Position.X,rect.Position.X+edge,rect.End.X-edge,rect.End.X],dy=[rect.Position.Y,rect.Position.Y+edge,rect.End.Y-edge,rect.End.Y];
        for(var j=0;j<3;j++)for(var i=0;i<3;i++)
        {
            if(i==1&&j==1)continue;
            // Clean frame run avoids the overlapping source clan crest.
            var region=!browser&&i==1&&j==0?new Rect2(380,120,400,15):new Rect2(source.Position+new Vector2(x[i],y[j]),new Vector2(x[i+1]-x[i],y[j+1]-y[j]));
            canvas.DrawTextureRectRegion(texture,new Rect2(dx[i],dy[j],dx[i+1]-dx[i],!browser&&i==1&&j==0?15:dy[j+1]-dy[j]),region);
        }
    }
    public static void SelectionFrame(CanvasItem canvas,Rect2 rect)
    {
        var source=new Rect2(1094,186,522,204);const int e=8;
        float[] x=[0,e,source.Size.X-e,source.Size.X],y=[0,e,source.Size.Y-e,source.Size.Y];
        float[] dx=[0,e,rect.Size.X-e,rect.Size.X],dy=[0,e,rect.Size.Y-e,rect.Size.Y];
        for(var j=0;j<3;j++)for(var i=0;i<3;i++)if(i!=1||j!=1)
            canvas.DrawTextureRectRegion(B,new Rect2(rect.Position+new Vector2(dx[i],dy[j]),new Vector2(dx[i+1]-dx[i],dy[j+1]-dy[j])),new Rect2(source.Position+new Vector2(x[i],y[j]),new Vector2(x[i+1]-x[i],y[j+1]-y[j])));
    }
    public static void Rule(CanvasItem canvas,float x,float y,float width)=>canvas.DrawLine(new(x,y),new(x+width,y),new Color("91713c"),1);
}

public partial class PracticePanel : Control
{
    public bool Browser;
    public PracticePanel(){MouseFilter=MouseFilterEnum.Ignore;}
    public override void _Draw()=>PracticeArt.Frame(this,new Rect2(Vector2.Zero,Size),Browser);
}

public partial class PracticeButton : Button
{
    public bool Primary, Selected, Quiet, Bare, Outline, RichOutline;
    public Color OutlineColor=PracticeArt.Amber;
    public float DividerAt;
    public int PointSize=24;
    public PracticeButton(string caption,Action action)
    {
        Text=caption;AccessibilityName=caption;MouseDefaultCursorShape=CursorShape.PointingHand;Flat=true;
        foreach(var state in new[]{"normal","hover","pressed","disabled","focus"})AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        // Draw native text ourselves over clean artwork slices, never over baked labels.
        AddThemeColorOverride("font_color",Colors.Transparent);AddThemeColorOverride("font_hover_color",Colors.Transparent);AddThemeColorOverride("font_pressed_color",Colors.Transparent);AddThemeColorOverride("font_focus_color",Colors.Transparent);AddThemeColorOverride("font_disabled_color",Colors.Transparent);
        Pressed+=action;MouseEntered+=QueueRedraw;MouseExited+=QueueRedraw;FocusEntered+=QueueRedraw;FocusExited+=QueueRedraw;
    }
    public override void _Draw()
    {
        var gold=(Primary||Selected)&&!Disabled;
        var source=gold?new Rect2(1250,822,385,72):new Rect2(240,730,235,52);
        var rect=new Rect2(Vector2.Zero,Size);
        if(gold)DrawTextureRectRegion(PracticeArt.ButtonPlate,rect,new Rect2(1250,822,385,72));
        else if(!Bare)DrawTextureRect(CardChangeArt.Lacquer,rect,false,new Color(.27f,.27f,.27f));
        const int edge=9;
        float[] x=[0,edge,source.Size.X-edge,source.Size.X], y=[0,edge,source.Size.Y-edge,source.Size.Y];
        float[] dx=[0,edge,Size.X-edge,Size.X],dy=[0,edge,Size.Y-edge,Size.Y];
        for(var j=0;j<3;j++)for(var i=0;i<3;i++)if((i!=1||j!=1)&&!Quiet&&!Bare)
            DrawTextureRectRegion(PracticeArt.A,new Rect2(dx[i],dy[j],dx[i+1]-dx[i],dy[j+1]-dy[j]),new Rect2(source.Position+new Vector2(x[i],y[j]),new Vector2(x[i+1]-x[i],y[j+1]-y[j])));
        if(RichOutline)PracticeArt.SelectionFrame(this,rect);
        else if(Outline)DrawRect(rect.Grow(-2),OutlineColor,false,Selected?2:1);
        if(DividerAt>0)DrawLine(new(DividerAt,25),new(DividerAt,Size.Y-20),new Color("715931"),1);
        if(!Disabled&&(IsHovered()||HasFocus()))DrawRect(rect.Grow(-3),Palette.Paper,false,1);
        var font=Primary?Palette.HeadingLabel(PointSize):Palette.Body;var size=PointSize;
        while(size>14&&font.GetStringSize(Text,fontSize:size).X>Size.X-30)size--;
        DrawString(font,new Vector2(10,(Size.Y+font.GetAscent(size)-font.GetDescent(size))/2),Text,HorizontalAlignment.Center,Size.X-20,size,Disabled?new Color("777064"):gold?new Color("201507"):Palette.Paper);
    }
}

public partial class PracticeReadyIcon : Control
{
    public PracticeReadyIcon(){MouseFilter=MouseFilterEnum.Ignore;}
    public override void _Draw()
    {
        var center=Size/2;var r=Math.Min(Size.X,Size.Y)/2-4;
        DrawArc(center,r+2,0,Mathf.Tau,48,new Color(.4f,.9f,.85f,.16f),4,true);
        DrawArc(center,r,0,Mathf.Tau,48,PracticeArt.Ready,2,true);
        DrawPolyline([center+new Vector2(-r*.55f,0),center+new Vector2(-r*.13f,r*.39f),center+new Vector2(r*.62f,-r*.52f)],PracticeArt.Ready,2.5f,true);
    }
}

/// <summary>Artwork crop excludes the Stronghold's printed stat rail; inspection shows its entire face.</summary>
public partial class PracticeStrongholdArt : Control
{
    private readonly CatalogueData.Card _card;
    private readonly CatalogueScans _scans;
    public PracticeStrongholdArt(CatalogueData.Card card,CatalogueScans scans)
    {_card=card;_scans=scans;MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.LinearWithMipmaps;scans.Changed+=QueueRedraw;TreeExiting+=()=>scans.Changed-=QueueRedraw;}
    public override void _Draw()
    {
        var tex=_scans.Get(_card.Printings.FirstOrDefault());
        if(tex is not null)
        {
            var crop=new Rect2(tex.GetWidth()*.075f,tex.GetHeight()*.16f,tex.GetWidth()*.68f,tex.GetHeight()*.37f);
            var fitted=crop.Size;var ratio=Size.X/Size.Y;
            if(fitted.X/fitted.Y>ratio)fitted.X=fitted.Y*ratio;else fitted.Y=fitted.X/ratio;
            DrawTextureRectRegion(tex,new Rect2(Vector2.Zero,Size),new Rect2(crop.Position+(crop.Size-fitted)/2,fitted));
        }
        DrawRect(new Rect2(Vector2.Zero,Size).Grow(-1),Palette.Gold,false,2);
    }
}

public partial class PracticeDeckTotals(int dynasty,int fate) : Control
{
    public override void _Draw()
    {
        DrawString(Palette.Body,new Vector2(0,24),"Dynasty",HorizontalAlignment.Center,Size.X/2,24,Palette.Paper);
        DrawString(Palette.Body,new Vector2(Size.X/2,24),"Fate",HorizontalAlignment.Center,Size.X/2,24,Palette.Paper);
        DrawString(Palette.Body,new Vector2(0,61),dynasty.ToString(),HorizontalAlignment.Center,Size.X/2,34,Palette.Paper);
        DrawString(Palette.Body,new Vector2(Size.X/2,61),fate.ToString(),HorizontalAlignment.Center,Size.X/2,34,Palette.Paper);
        DrawLine(new(Size.X/2,2),new(Size.X/2,64),Palette.Gold,1);
    }
}
