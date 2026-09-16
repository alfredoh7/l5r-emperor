using Godot;

namespace L5R.Client;

/// <summary>Duel-only ink surfaces and original Musashi headings; card faces stay untouched.</summary>
internal static class DuelUi
{
    private static Font Body => GD.Load<FontFile>("res://Art/Fonts/Caudex-Regular.ttf");
    internal static Font Heading => GD.Load<FontFile>("res://Art/Fonts/MusashiBrush.otf");

    public static Theme Theme()
    {
        var theme=SidebarSkin.Theme();
        theme.DefaultFont=Body;
        foreach(var state in new[]{"normal","hover","pressed","disabled"})theme.SetStylebox(state,"CheckBox",new StyleBoxEmpty{ContentMarginLeft=2,ContentMarginRight=2,ContentMarginTop=2,ContentMarginBottom=2});
        theme.SetStylebox("focus","CheckBox",Palette.Box(Colors.Transparent,Palette.Paper,1,0));
        theme.SetFont("font","CheckBox",Body);
        theme.SetColor("font_color","CheckBox",Palette.Paper);
        theme.SetIcon("unchecked","CheckBox",CheckIcon(false));
        theme.SetIcon("checked","CheckBox",CheckIcon(true));
        theme.SetStylebox("panel","PopupMenu",Plaque());
        theme.SetFont("bold_font","RichTextLabel",SidebarSkin.Bold);
        theme.SetFont("italics_font","RichTextLabel",SidebarSkin.Italic);
        theme.SetFont("bold_italics_font","RichTextLabel",GD.Load<FontFile>("res://Art/Fonts/Caudex-BoldItalic.ttf"));
        foreach(var state in new[]{"normal","hover","pressed","disabled","focus"})
            theme.SetStylebox(state,"OptionButton",ButtonStyle(state,1));
        theme.SetFont("font","OptionButton",SidebarSkin.Bold);
        theme.SetFontSize("font_size","OptionButton",18);
        theme.SetColor("font_color","PopupMenu",Palette.Paper);
        theme.SetStylebox("hover","PopupMenu",Palette.Box(new Color(.16f,.14f,.13f),Colors.Transparent,0,0));
        theme.SetConstant("v_separation","PopupMenu",12);
        return theme;
    }
    private static Texture2D CheckIcon(bool selected)
    {
        // A crisp functional checkbox, not decorative artwork; independent of the
        // inherited dark check icons so the unchecked state remains visible on ink.
        var image=Image.CreateEmpty(18,18,false,Image.Format.Rgba8);image.Fill(Colors.Transparent);
        var ivory=new Color(.91f,.88f,.78f);
        for(var i=1;i<17;i++){image.SetPixel(i,1,ivory);image.SetPixel(i,16,ivory);image.SetPixel(1,i,ivory);image.SetPixel(16,i,ivory);}
        if(selected)for(var y=5;y<13;y++)for(var x=5;x<13;x++)image.SetPixel(x,y,ivory);
        return ImageTexture.CreateFromImage(image);
    }
    public static Label Text(string text,int size=16,Color? color=null,bool title=false)
    {
        var label=Palette.Text(text,size,color,title);label.AddThemeFontOverride("font",title?Heading:Body);return label;
    }
    public static Label Wrapped(string text,int size=16,Color? color=null)
    {
        var label=Text(text,size,color);label.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;return label;
    }
    private static StyleBox ButtonStyle(string state,float scale)
    {
        // Keyboard focus remains a functional outline, separate from decorative art.
        if(state=="focus")return Palette.Box(Colors.Transparent,Palette.Paper,1,0);
        var tint=state switch {"hover"=>new Color(1.35f,1.3f,1.25f),"pressed"=>new Color(.75f,.75f,.75f),"disabled"=>new Color(.7f,.7f,.7f,.45f),_=>Colors.White};
        var box=Plaque(scale,tint);box.ContentMarginLeft=box.ContentMarginRight=18*scale;
        box.ContentMarginTop=box.ContentMarginBottom=9*scale;return box;
    }
    private static Texture2D? _wash;
    // Imagegen-derived transparent ink from the approved concept, with only the plain
    // center stretched. Names retained for the existing contextual result labels.
    public static StyleBoxTexture Plaque(float scale=1,Color? tint=null)
    {
        if(_wash is null)
        {
            var pixels=GD.Load<Texture2D>("res://Art/DuelUi/ink-wash.png").GetImage();
            var used=pixels.GetUsedRect();pixels=pixels.GetRegion(used);
            pixels.Resize(600,160,Image.Interpolation.Lanczos);pixels.GenerateMipmaps();
            _wash=ImageTexture.CreateFromImage(pixels);
        }
        var box=new StyleBoxTexture {Texture=_wash,ModulateColor=tint??Colors.White};
        box.TextureMarginLeft=box.TextureMarginRight=42*scale;
        box.TextureMarginTop=box.TextureMarginBottom=18*scale;
        box.ContentMarginLeft=box.ContentMarginRight=24*scale;
        box.ContentMarginTop=box.ContentMarginBottom=14*scale;
        return box;
    }
    public static Button Button(string text,Action action,bool primary=false,float scale=1,int fontSize=18)
    {
        var button=new Button{Text=text,MouseDefaultCursorShape=Control.CursorShape.PointingHand,FocusMode=Control.FocusModeEnum.All};
        button.Pressed+=action;button.SetMeta("flat_tooltip",true);
        button.CustomMinimumSize=new Vector2(0,Math.Max(30,40*scale));
        button.AddThemeFontSizeOverride("font_size",Math.Max(primary?24:14,(int)(fontSize*scale)));
        button.AddThemeFontOverride("font",primary||text=="Menu"?Heading:Body);
        foreach(var state in new[]{"font_color","font_hover_color","font_pressed_color","font_focus_color"})button.AddThemeColorOverride(state,Palette.Paper);
        button.AddThemeColorOverride("font_disabled_color",Palette.PaperDim);
        foreach(var state in new[]{"normal","hover","pressed","disabled","focus"})button.AddThemeStyleboxOverride(state,ButtonStyle(state,scale));
        if(primary)
        {
            var slash=new TextureRect{Texture=GD.Load<Texture2D>("res://Art/DuelUi/red-slash.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};
            button.AddChild(slash);
            void Place(){slash.Position=new Vector2(button.Size.X*.73f,button.Size.Y*.2f);slash.Size=new Vector2(button.Size.X*.3f,button.Size.Y*.75f);slash.PivotOffset=slash.Size/2;slash.Rotation=-.25f;slash.Modulate=new Color(1,1,1,button.Disabled?.18f:.85f);}
            button.Resized+=Place;button.Draw+=Place;
        }
        return button;
    }
    public static PanelContainer Surface(Control parent,int padding=22,bool inset=false)
    {
        var panel=new PanelContainer();var style=Plaque();
        style.ContentMarginLeft=style.ContentMarginRight=padding;
        style.ContentMarginTop=style.ContentMarginBottom=padding;
        panel.AddThemeStyleboxOverride("panel",style);parent.AddChild(panel);return panel;
    }
    public static VBoxContainer Modal(Control parent,Vector2 viewport,Vector2 designSize,string title)
    {
        var scale=Math.Min(1,Math.Min((viewport.X-48)/designSize.X,(viewport.Y-48)/designSize.Y));
        var shell=new Control{Position=(viewport-designSize*scale)/2,Size=designSize,Scale=Vector2.One*scale};parent.AddChild(shell);
        var panel=Surface(shell,32);panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);
        var box=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};box.AddThemeConstantOverride("separation",12);scroll.AddChild(box);
        box.AddChild(Text(title,34,Palette.Paper,true));return box;
    }
}
