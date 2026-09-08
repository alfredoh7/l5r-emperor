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
        AddThemeFontOverride("font",Palette.Title);AddThemeFontSizeOverride("font_size",14);
        AddThemeColorOverride("font_color",Palette.Paper);
        AddThemeColorOverride("font_outline_color",new Color("080d12"));AddThemeConstantOverride("outline_size",3);
        Size=new Vector2(18,20);
        Hide(); // First placement occurs after the new card controls have laid out.
    }
    public override void _Process(double delta)
    {
        if(!IsInstanceValid(ProvinceAnchor)||!ProvinceAnchor.IsInsideTree()){Hide();return;}
        Visible=Allowed()&&ProvinceAnchor.IsVisibleInTree(); if(!Visible)return;
        var card=ProvinceAnchor.GetChildren().OfType<CardTile>().FirstOrDefault();
        var size = ProvinceAnchor.Size;
        var corners = card?.FaceCornersOnViewport() ?? new[]
        {
            new Vector2(size.X * .16f, 0), new Vector2(size.X * .84f, 0),
            new Vector2(size.X * .84f, size.Y), new Vector2(size.X * .16f, size.Y)
        }.Select(p => Projection(ProvinceAnchor.GetGlobalTransform() * p)).ToArray();
        var root = GetParent<Control>().GetGlobalTransform().AffineInverse();
        var edge = root * ((corners[1] + corners[2]) / 2);
        var faceHeight = (root * corners[0]).DistanceTo(root * corners[3]);
        Scale = Vector2.One * Math.Clamp(faceHeight / 90f, .85f, 1);
        // A narrow side gutter keeps numbers apart from the section heading.
        Position = edge + new Vector2(2, -Size.Y * Scale.Y / 2);
    }
}
