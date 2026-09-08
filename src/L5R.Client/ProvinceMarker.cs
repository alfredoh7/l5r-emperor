using Godot;
namespace L5R.Client;

/// <summary>Stable public province number, kept legible while the table camera zooms.</summary>
public partial class ProvinceMarker : Label
{
    public Control ProvinceAnchor = null!;
    public Func<Vector2,Vector2> Projection = null!;
    public Func<bool> Allowed = () => true;
    public int ProvinceIndex;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        Text=(ProvinceIndex+1).ToString();
        HorizontalAlignment=HorizontalAlignment.Center;
        AddThemeFontOverride("font",Palette.Title);AddThemeFontSizeOverride("font_size",22);
        AddThemeColorOverride("font_color",Palette.Paper);
        AddThemeColorOverride("font_outline_color",new Color("080d12"));AddThemeConstantOverride("outline_size",7);
        Size=new Vector2(32,30);
    }
    public override void _Process(double delta)
    {
        if(!IsInstanceValid(ProvinceAnchor)||!ProvinceAnchor.IsInsideTree()){Hide();return;}
        Visible=Allowed()&&ProvinceAnchor.IsVisibleInTree(); if(!Visible)return;
        var card=ProvinceAnchor.GetChildren().OfType<CardTile>().FirstOrDefault();
        var point=card is null ? Projection(ProvinceAnchor.GetGlobalRect().GetCenter()) : card.FaceCornersOnViewport().OrderBy(p=>p.Y).Take(2).Aggregate(Vector2.Zero,(sum,p)=>sum+p)/2;
        Position=GetParent<Control>().GetGlobalTransform().AffineInverse()*point+new Vector2(-16,-30);
    }
}
