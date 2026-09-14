using Godot;
namespace L5R.Client;

/// <summary>The approved bonsai composition, with live native controls and the original game card backs.</summary>
public partial class DeckLibraryLanding : Control
{
    private readonly Control _artboard=new();
    private readonly Action _newDeck,_import,_folder,_back;
    public DeckLibraryLanding(Action newDeck,Action import,Action folder,Action back)
    {_newDeck=newDeck;_import=import;_folder=folder;_back=back;}
    public override void _Ready()
    {
        AddChild(_artboard);_artboard.Size=new Vector2(1586,992);
        var texture=GD.Load<Texture2D>("res://Art/DeckBuilder/bonsai-library.png");
        var art=new TextureRect{Texture=texture,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore,Size=_artboard.Size};_artboard.AddChild(art);
        var backs=new DeckLibraryBacks{MouseFilter=MouseFilterEnum.Ignore};_artboard.AddChild(backs);
        Button ActionButton(string name,string subtitle,float y,Action action)
        {
            var button=new DeckLibraryAction(texture,new Rect2(751,y,747,143),subtitle){Text=name,AccessibilityName=name,Position=new Vector2(751,y),Size=new Vector2(747,143)};
            _artboard.AddChild(button);button.Pressed+=action;return button;
        }
        ActionButton("New deck","Choose a clan and stronghold.",275,_newDeck);
        ActionButton("Import deck","Open a shared deck file.",440,_import);
        ActionButton("Open deck folder","",600,_folder);
        var back=new Button{Text="Back",AccessibilityName="Back to menu",Position=new Vector2(29,10),Size=new Vector2(123,38)};
        foreach(var state in new[]{"normal","hover","pressed"})back.AddThemeStyleboxOverride(state,new StyleBoxEmpty());back.AddThemeFontSizeOverride("font_size",20);_artboard.AddChild(back);back.Pressed+=_back;
        AddLabel("My decks",new Rect2(179,9,520,43),39,true);
        var brand=AddLabel("LEGEND OF THE FIVE RINGS\nEMPEROR EDITION",new Rect2(1327,10,242,41),12);brand.HorizontalAlignment=HorizontalAlignment.Right;brand.AddThemeColorOverride("font_color",DeckSkin.Gold);
        var draft=AddLabel("Unfinished decks can be saved as drafts.",new Rect2(752,786,746,32),20);draft.HorizontalAlignment=HorizontalAlignment.Center;
        var local=AddLabel("Saved on this device",new Rect2(1250,953,273,25),14);local.HorizontalAlignment=HorizontalAlignment.Right;
        Resized+=Fit;Fit();
    }
    private Label AddLabel(string text,Rect2 rect,int size,bool heading=false)
    {var label=CatalogueArt.Label(text,size,heading);label.Position=rect.Position;label.Size=rect.Size;_artboard.AddChild(label);return label;}
    private void Fit()=>_artboard.Scale=new Vector2(Size.X/1586f,Size.Y/992f);
}

public partial class DeckLibraryAction : Button
{
    private readonly Texture2D _art;
    private readonly Rect2 _region;
    private readonly string _subtitle;
    private static readonly Font TitleFace=new FontVariation{BaseFont=Palette.Body,VariationEmbolden=.55f};
    public DeckLibraryAction(Texture2D art,Rect2 region,string subtitle)
    {
        _art=art;_region=region;_subtitle=subtitle;MouseDefaultCursorShape=CursorShape.PointingHand;
        foreach(var state in new[]{"normal","hover","pressed","focus","disabled"})AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        foreach(var state in new[]{"font_color","font_hover_color","font_pressed_color","font_focus_color"})AddThemeColorOverride(state,Colors.Transparent);
        AddThemeFontSizeOverride("font_size",1);MouseEntered+=QueueRedraw;MouseExited+=QueueRedraw;FocusEntered+=QueueRedraw;FocusExited+=QueueRedraw;
    }
    public override void _Draw()
    {
        var tint=IsHovered()||HasFocus()?new Color(1.13f,1.1f,1.05f):Colors.White;
        DrawTextureRectRegion(_art,new Rect2(Vector2.Zero,Size),new Rect2(_region.Position*_art.GetSize()/new Vector2(1586,992),_region.Size*_art.GetSize()/new Vector2(1586,992)),tint);
        if(IsHovered()||HasFocus())DrawRect(new Rect2(2,2,Size.X-4,Size.Y-4),new Color("ffe5a5"),false,2);
        var ink=Text=="New deck"?new Color("161009"):DeckSkin.Text;
        DrawString(TitleFace,new Vector2(188,_subtitle.Length>0?72:84),Text.ToUpperInvariant(),HorizontalAlignment.Left,-1,Text=="Open deck folder"?30:36,ink);
        if(_subtitle.Length>0)DrawString(Palette.Body,new Vector2(188,103),_subtitle,HorizontalAlignment.Left,-1,25,ink);
    }
}

public partial class DeckLibraryBacks : Control
{
    public override void _Draw()
    {
        // Preserve source artwork for backs; the case is decorative, never a replacement for card identity.
        DrawBack("original-dynasty-back.png",[new(149,499),new(302,459),new(422,590),new(244,634)]);
        DrawBack("original-fate-back.png",[new(378,437),new(523,407),new(633,528),new(452,571)]);
    }
    private void DrawBack(string file,Vector2[] quad)
    {
        var texture=GD.Load<Texture2D>("res://Art/"+file);
        DrawPolygon(quad,new Color[]{new(.76f,.72f,.61f,1)},new Vector2[]{Vector2.Zero,Vector2.Right,Vector2.One,Vector2.Down},texture);
    }
}
