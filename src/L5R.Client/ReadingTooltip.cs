using Godot;

namespace L5R.Client;

/// <summary>Detached tooltips use the same readable density as the fullscreen table.</summary>
public static class ReadingTooltip
{
    internal static string? LastText;
    public static Control Create(Node owner, string text)
    {
        LastText=text;
        var lines=new List<string>();
        foreach(var paragraph in text.Split('\n'))
        {
            var line="";
            foreach(var word in paragraph.Split(' ',StringSplitOptions.RemoveEmptyEntries))
            {
                var next=line.Length==0?word:line+" "+word;
                if(line.Length>0 && Palette.Body.GetStringSize(next,HorizontalAlignment.Left,-1,18).X>410) { lines.Add(line); line=word; }
                else line=next;
            }
            lines.Add(line);
        }
        var design=new Vector2(444,Math.Max(56,lines.Count*25+32));
        var scale=owner.GetTree().Root.GetVisibleRect().Size.Y/1080f;
        var shell=new Control { CustomMinimumSize=design*scale,MouseFilter=Control.MouseFilterEnum.Ignore };
        var panel=new PanelContainer { Size=design,Scale=Vector2.One*scale,Theme=Palette.MakeTheme(),MouseFilter=Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel",Palette.Box(new Color("171a16"),Palette.Gold,16));
        shell.AddChild(panel); panel.AddChild(Palette.Text(string.Join("\n",lines),18,Palette.Paper));
        return shell;
    }
}

public partial class TooltipLabel : Label
{
    public override Control _MakeCustomTooltip(string forText) => string.IsNullOrWhiteSpace(forText) || HasMeta("flat_tooltip") ? null! : ReadingTooltip.Create(this,forText);
}
public partial class TooltipButton : Button
{
    public override Control _MakeCustomTooltip(string forText) => string.IsNullOrWhiteSpace(forText) || HasMeta("flat_tooltip") ? null! : ReadingTooltip.Create(this,forText);
}
public partial class TooltipRegion : Control
{
    public override Control _MakeCustomTooltip(string forText) => string.IsNullOrWhiteSpace(forText) || HasMeta("flat_tooltip") ? null! : ReadingTooltip.Create(this,forText);
}
