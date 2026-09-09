using Godot;
namespace L5R.Client;

/// <summary>Full catalogue face, with physical copy edges. Art is never distorted or replaced.</summary>
public partial class DeckFace : Button
{
    public CatalogueData.Card Card { get; }
    private readonly CatalogueScans _scans;
    public int Copies = 1;
    public bool Selected;
    public DeckFace(CatalogueData.Card card, CatalogueScans scans)
    {
        Card=card;_scans=scans;Flat=true;AccessibilityName=card.Name;MouseDefaultCursorShape=CursorShape.PointingHand;
        TextureFilter=TextureFilterEnum.LinearWithMipmaps;
        foreach(var s in new[]{"normal","hover","pressed","focus"})AddThemeStyleboxOverride(s,new StyleBoxEmpty());
        MouseEntered+=QueueRedraw;MouseExited+=QueueRedraw;FocusEntered+=QueueRedraw;FocusExited+=QueueRedraw;
        scans.Changed+=QueueRedraw;TreeExiting+=()=>scans.Changed-=QueueRedraw;
    }
    public override void _Draw()
    {
        var face=new Rect2(3,2,Size.X-6,Size.Y-10);
        for(var i=Math.Min(Copies-1,5);i>0;i--){DrawRect(new Rect2(face.Position+new Vector2(i*.5f,i*1.3f),face.Size),new Color("b7a47c"));DrawRect(new Rect2(face.Position+new Vector2(i*.5f,i*1.3f),face.Size),new Color("24201b"),false,1);}
        if(IsHovered()||HasFocus()||Selected)
        {
            var glow=new StyleBoxFlat{BgColor=Colors.Transparent,ShadowColor=new Color(.95f,.64f,.2f,.5f),ShadowSize=12,CornerRadiusTopLeft=4,CornerRadiusTopRight=4,CornerRadiusBottomLeft=4,CornerRadiusBottomRight=4};glow.Draw(GetCanvasItem(),face.Grow(3));
        }
        DrawRect(face.Grow(2),new Color("8a7045"));DrawRect(face,new Color("070b0e"));
        var tex=_scans.Get(Card.Printings.FirstOrDefault());
        if(tex is not null){var k=Math.Min(face.Size.X/tex.GetWidth(),face.Size.Y/tex.GetHeight());var size=tex.GetSize()*k;DrawTextureRect(tex,new Rect2(face.Position+(face.Size-size)/2,size),false);}
        else {DrawString(Palette.Body,new Vector2(8,face.Size.Y*.45f),Card.Name,HorizontalAlignment.Center,Size.X-16,16,CatalogueArt.Ivory);DrawString(Palette.Body,new Vector2(8,face.Size.Y*.55f),"Loading scan…",HorizontalAlignment.Center,Size.X-16,14,CatalogueArt.Muted);}
        DeckSkin.Frame().Draw(GetCanvasItem(),face.Grow(2));
        if(IsHovered()||HasFocus()||Selected)DrawRect(face.Grow(2),new Color("e4ba66"),false,1);
    }
}

public partial class DeckThumbnail : Control
{
    private readonly CatalogueData.Card _card;
    private readonly CatalogueScans _scans;
    public DeckThumbnail(CatalogueData.Card card,CatalogueScans scans)
    {_card=card;_scans=scans;MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.LinearWithMipmaps;scans.Changed+=QueueRedraw;TreeExiting+=()=>scans.Changed-=QueueRedraw;}
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("171b1d"));
        var tex=_scans.Get(_card.Printings.FirstOrDefault());
        if(tex is not null)DrawTextureRectRegion(tex,new Rect2(1,1,Size.X-2,Size.Y-2),new Rect2(tex.GetWidth()*.1f,tex.GetHeight()*.16f,tex.GetWidth()*.8f,tex.GetHeight()*.52f));
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("8a7045"),false,1);
    }
}

/// <summary>Virtualized collection rows and grouped draft tiles share the same bounded layout.</summary>
public partial class DeckSurface : ScrollContainer
{
    private sealed record Entry(CatalogueData.Card? Card,string Heading,Rect2 Rect,int Count=0);
    private readonly Control _sheet=new();
    private List<Entry> _entries=[];
    private readonly Dictionary<int,Control> _visible=[];
    public CatalogueData.Card[] Cards=[];
    public bool Rows, Setup;
    public float CardWidth=176;
    public string Group="Type";
    public Func<CatalogueData.Card,Control> MakeCard=null!;
    public int VisibleCount=>_visible.Count;
    public DeckSurface()
    {
        HorizontalScrollMode=ScrollMode.Disabled;VerticalScrollMode=ScrollMode.Auto;ClipContents=true;AddChild(_sheet);
        Resized+=Relayout;GetVScrollBar().ValueChanged+=_=>Refresh();
    }
    public void Reset(CatalogueData.Card[] cards,bool resetScroll=false)
    {Cards=cards;if(resetScroll)ScrollVertical=0;Relayout();}
    public void Relayout()
    {
        if(Size.X<40)return;
        foreach(var c in _visible.Values){_sheet.RemoveChild(c);c.QueueFree();}_visible.Clear();_entries=[];
        float y=4,w=Size.X-18;
        if(Rows)
        {
            foreach(var card in Cards){_entries.Add(new(card,"",new Rect2(2,y,w-4,58)));y+=60;}
        }
        else
        {
            var width=Setup?Math.Min(300,(w-60)/3):CardWidth;
            var columns=Setup?Math.Max(1,Math.Min(3,(int)(w/(width+22)))):Math.Max(1,(int)(w/(width+22)));var cell=w/columns;
            var height=width*1.4f+(Setup?90:104);
            string Key(CatalogueData.Card c)=>Setup?"":Group=="Gold"?"GOLD "+(c.Numeric("cost")==double.MaxValue?"—":c.Numeric("cost").ToString()):Group=="None"?"CARDS":string.Join(" / ",c.Types.Select(t=>t=="Personality"?"Personalities":t=="Strategy"?"Strategies":t+"s")).ToUpperInvariant();
            foreach(var group in Cards.GroupBy(Key))
            {
                if(group.Key.Length>0){_entries.Add(new(null,group.Key,new Rect2(8,y,w-16,30),group.Count()));y+=34;}
                var i=0;foreach(var card in group){_entries.Add(new(card,"",new Rect2(i%columns*cell+(cell-width)/2,y+i/columns*height,width,height-10)));i++;}
                y+=(float)Math.Ceiling((double)i/columns)*height+12;
            }
        }
        _sheet.CustomMinimumSize=new Vector2(0,Math.Max(Size.Y,y));_sheet.Size=new Vector2(w,Math.Max(Size.Y,y));Refresh();
    }
    private void Refresh()
    {
        if(!IsInsideTree())return;
        var indices=Enumerable.Range(0,_entries.Count).Where(i=>_entries[i].Rect.End.Y>=ScrollVertical-50&&_entries[i].Rect.Position.Y<ScrollVertical+Size.Y+50).ToHashSet();
        foreach(var i in _visible.Keys.Where(i=>!indices.Contains(i)).ToArray()){_sheet.RemoveChild(_visible[i]);_visible[i].QueueFree();_visible.Remove(i);}
        foreach(var i in indices)
        {
            var entry=_entries[i];if(!_visible.TryGetValue(i,out var item))
            {item=entry.Card is null?new DeckSectionTitle(entry.Heading,entry.Count):MakeCard(entry.Card);_sheet.AddChild(item);_visible[i]=item;}
            item.Position=entry.Rect.Position;item.Size=entry.Rect.Size;
        }
    }
}

public partial class DeckSectionTitle(string title,int count) : Control
{
    public override void _Draw()
    {
        var font=CatalogueArt.Heading(23);var width=font.GetStringSize(title,fontSize:23).X;
        DrawString(font,new Vector2(0,22),title,HorizontalAlignment.Left,-1,23,DeckSkin.Text);
        DrawString(Palette.Body,new Vector2(width+16,22),$"({count} shown)",HorizontalAlignment.Left,-1,17,new Color("c8bb99"));
        DrawLine(new Vector2(0,29),new Vector2(Size.X,29),new Color(.6f,.44f,.22f,.6f),1);
    }
}
