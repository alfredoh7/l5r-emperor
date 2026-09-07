using Godot;

namespace L5R.Client;

/// <summary>Shared material and brass joinery for the HUD; children remain native controls.</summary>
public partial class LacquerPanel : PanelContainer
{
    private static Texture2D? _wood;
    public override void _Ready() { _wood ??= GD.Load<Texture2D>("res://Art/lacquer-ui.png"); Resized += QueueRedraw; }
    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        if (_wood is not null) DrawTextureRect(_wood, r, false, new Color(.72f, .72f, .68f));
        DrawStyleBox(Palette.Box(new Color(.025f, .03f, .022f, .3f), new Color("78623e"), 0, 1), r);
        DrawRect(r.Grow(-4), new Color(.53f, .43f, .26f, .24f), false, 1);
        foreach (var p in new[] { new Vector2(0, 0), new Vector2(Size.X, 0), new Vector2(0, Size.Y), Size })
        {
            var dx = p.X == 0 ? 1 : -1; var dy = p.Y == 0 ? 1 : -1;
            var corner = p + new Vector2(dx * 7, dy * 7);
            DrawLine(corner, corner + new Vector2(dx * 14, 0), Palette.Gold with { A = .55f }, 1, true);
            DrawLine(corner, corner + new Vector2(0, dy * 14), Palette.Gold with { A = .55f }, 1, true);
        }
    }
}
