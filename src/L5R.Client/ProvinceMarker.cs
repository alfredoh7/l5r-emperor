using Godot;
namespace L5R.Client;

/// <summary>A ceremonial public identity under the province's resting card slot.</summary>
public partial class ProvinceMarker : Control
{
    public const float Footer = 96;
    public Control ProvinceAnchor = null!;
    public Func<Vector2,Vector2> Projection = null!;
    public Func<bool> Allowed = () => true;
    public int ProvinceIndex;
    public string Text => (ProvinceIndex + 1).ToString();
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        Hide(); // Never flash at HUD origin before the first projected placement.
    }
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(ProvinceAnchor) || !ProvinceAnchor.IsInsideTree()) { Hide(); return; }
        Visible = Allowed() && ProvinceAnchor.IsVisibleInTree(); if (!Visible) return;
        var size = ProvinceAnchor.Size;
        var anchor = ProvinceAnchor.GetGlobalTransform();
        var inverse = GetParent<Control>().GetGlobalTransform().AffineInverse();
        Vector2 Project(Vector2 p) => inverse * Projection(anchor * p);
        // Use the slot, never the hovered card's growing/rotating face. An empty
        // surviving province has exactly the same identity and ceremonial position.
        var center = Project(new Vector2(size.X / 2, size.Y + Footer / 2));
        var width = Project(new Vector2(size.X * .84f, size.Y)).DistanceTo(Project(new Vector2(size.X * .16f, size.Y)));
        var screenScale = Math.Max(.1f, GetParent<Control>().GetGlobalTransform().X.Length());
        var availableHeight = Project(new Vector2(size.X / 2, size.Y + Footer)).DistanceTo(Project(new Vector2(size.X / 2, size.Y)));
        var height = Math.Min(Math.Clamp(width * .45f, 19 / screenScale, 34 / screenScale), availableHeight * .92f);
        Size = new Vector2(height * MatEmblemArt.Seal.GetWidth() / MatEmblemArt.Seal.GetHeight(), height);
        Position = center - Size / 2;
        QueueRedraw();
    }
    public override void _Draw() => MatEmblemArt.DrawSeal(this, new Rect2(Vector2.Zero, Size), Text);
}
