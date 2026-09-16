using Godot;

namespace L5R.Client;

/// <summary>Responsive duel layouts using the approved in-game lacquer menu assets.</summary>
internal static class DuelUi
{
    private static Font Body => GD.Load<FontFile>("res://Art/Fonts/Caudex-Regular.ttf");

    public static Theme Theme()
    {
        var theme=SidebarSkin.Theme();
        theme.DefaultFont=Body;
        theme.SetFont("bold_font","RichTextLabel",SidebarSkin.Bold);
        theme.SetFont("italics_font","RichTextLabel",SidebarSkin.Italic);
        theme.SetFont("bold_italics_font","RichTextLabel",GD.Load<FontFile>("res://Art/Fonts/Caudex-BoldItalic.ttf"));
        foreach(var state in new[]{"normal","hover","pressed","disabled","focus"})
            theme.SetStylebox(state,"OptionButton",ButtonStyle(state,false,1));
        theme.SetFont("font","OptionButton",SidebarSkin.Bold);
        theme.SetFontSize("font_size","OptionButton",18);
        theme.SetColor("font_color","PopupMenu",Palette.Paper);
        theme.SetStylebox("hover","PopupMenu",SidebarSkin.Inset());
        theme.SetConstant("v_separation","PopupMenu",12);
        return theme;
    }

    public static Label Text(string text,int size=16,Color? color=null,bool title=false)
    {
        var label=Palette.Text(text,size,color,title);
        if(!title)label.AddThemeFontOverride("font",Body);
        return label;
    }

    public static Label Wrapped(string text,int size=16,Color? color=null)
    {
        var label=Text(text,size,color);label.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;return label;
    }

    private static StyleBox ButtonStyle(string state,bool primary,float scale)
    {
        if(state=="focus")return Palette.Box(Colors.Transparent,Palette.Paper,0,0);
        var tint=state switch {"hover"=>new Color(1.2f,1.15f,1.05f),"pressed"=>new Color(.8f,.8f,.8f),"disabled"=>new Color(.55f,.55f,.55f),_=>Colors.White};
        var box=SidebarSkin.Surface(true,primary&&state!="disabled",tint);
        box.ContentMarginLeft=box.ContentMarginRight=14*scale;
        box.ContentMarginTop=box.ContentMarginBottom=7*scale;
        foreach(var side in new[]{Side.Left,Side.Top,Side.Right,Side.Bottom})box.SetTextureMargin(side,Math.Max(9,14*scale));
        return box;
    }

    public static Button Button(string text,Action action,bool primary=false,float scale=1,int fontSize=18)
    {
        var button=SidebarSkin.Button(text,action,primary);
        button.SetMeta("flat_tooltip",true);
        button.CustomMinimumSize=new Vector2(0,Math.Max(30,40*scale));
        button.AddThemeFontSizeOverride("font_size",Math.Max(13,(int)(fontSize*scale)));
        button.AddThemeFontOverride("font",SidebarSkin.Bold);
        foreach(var state in new[]{"normal","hover","pressed","disabled","focus"})
            button.AddThemeStyleboxOverride(state,ButtonStyle(state,primary,scale));
        return button;
    }

    public static PanelContainer Surface(Control parent,int padding=22,bool inset=false)
    {
        var panel=new PanelContainer();var style=inset?SidebarSkin.Inset():SidebarSkin.Surface();
        style.ContentMarginLeft=style.ContentMarginRight=padding;
        style.ContentMarginTop=style.ContentMarginBottom=padding;
        panel.AddThemeStyleboxOverride("panel",style);parent.AddChild(panel);return panel;
    }

    public static VBoxContainer Modal(Control parent,Vector2 viewport,Vector2 designSize,string title)
    {
        var scale=Math.Min(1,Math.Min((viewport.X-48)/designSize.X,(viewport.Y-48)/designSize.Y));
        var shell=new Control{Position=(viewport-designSize*scale)/2,Size=designSize,Scale=Vector2.One*scale};parent.AddChild(shell);
        var panel=Surface(shell);panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);
        var box=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};box.AddThemeConstantOverride("separation",12);scroll.AddChild(box);
        box.AddChild(Text(title,30,Palette.Gold,true));box.AddChild(new HSeparator());return box;
    }
}
