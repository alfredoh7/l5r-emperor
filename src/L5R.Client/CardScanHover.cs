using Godot;
namespace L5R.Client;

/// <summary>Full-size local WebP scan shown beside a hovered catalogue card. Display only; it never changes a draft or reveals hidden cards.</summary>
public partial class CardScanHover : Control
{
    public const float MinimumEnlargement = 1.5f;
    private const float Margin = 12, Gap = 14, Delay = .3f;
    private readonly CatalogueScans _scans;
    private CatalogueData.Card? _card;
    private Rect2 _target;
    private int _ticket;
    public CatalogueData.Card? Card => _card;
    public Rect2 Face { get; private set; }
    public float DisplayedWidth { get; private set; }
    public CardScanHover(CatalogueScans scans)
    {
        _scans = scans; Name = "CardScanHover"; MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.LinearWithMipmaps; Visible = false; ZIndex = 100;
        scans.Changed += Refresh; TreeExiting += () => scans.Changed -= Refresh;
    }
    /// <summary>Size of the enlarged face: the scan's own pixel size, never under 1.5x the width of the card shown in the viewer, and never taller than the window.</summary>
    public static Vector2 FaceSize(Vector2 scan, float displayedWidth, Vector2 viewport)
    {
        var fit = Math.Min((viewport.Y - Margin * 2) / scan.Y, (viewport.X - Margin * 2) / scan.X);
        var width = Math.Max(displayedWidth * MinimumEnlargement, Math.Min(scan.X, scan.X * fit));
        width = Math.Min(width, scan.X * fit);
        return new Vector2(width, width * scan.Y / scan.X);
    }
    /// <summary>Wires hover/focus on a tile. Coordinates are global, so the owning screen's canvas scale is accounted for.</summary>
    public void Track(Control tile, Func<CatalogueData.Card> card)
    {
        tile.MouseEntered += () => _ = Arm(card(), tile); tile.MouseExited += Dismiss;
        tile.FocusEntered += () => _ = Arm(card(), tile); tile.FocusExited += Dismiss;
        tile.TreeExiting += Dismiss;
    }
    /// <param name="face">The image the viewer shows for this card when it is not the whole hovered control (for example a list row's thumbnail); its width sets the 1.5x minimum.</param>
    public async Task Arm(CatalogueData.Card card, Control tile, Control? face = null)
    {
        var ticket = ++_ticket;
        await ToSignal(GetTree().CreateTimer(Delay), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this) || !IsInsideTree() || ticket != _ticket || !IsInstanceValid(tile) || !tile.IsInsideTree() || !tile.IsVisibleInTree()) return;
        var transform = tile.GetGlobalTransform();
        ShowFor(card, new Rect2(transform.Origin, transform.BasisXform(tile.Size)), face is null ? null : face.GetGlobalTransform().BasisXform(face.Size).X);
    }
    /// <summary>Shows immediately; <paramref name="tile"/> is the hovered card face in global coordinates.</summary>
    public void ShowFor(CatalogueData.Card card, Rect2 tile, float? displayedWidth = null)
    {
        _card = card; _target = tile; DisplayedWidth = displayedWidth ?? tile.Size.X; Visible = true; Refresh();
    }
    public void Dismiss() { if (!IsInstanceValid(this)) return; _ticket++; _card = null; Visible = false; QueueRedraw(); }
    private void Refresh()
    {
        if (_card is null) return;
        var viewport = GetViewportRect().Size; var printing = _card.Printings.FirstOrDefault();
        var tex = _scans.Get(printing);
        if (tex is null) { Face = default; QueueRedraw(); return; }
        var size = FaceSize(tex.GetSize(), DisplayedWidth, viewport);
        // Beside the card on whichever side has room; the preview ignores the mouse so it can never cause flicker.
        var right = _target.End.X + Gap; var left = _target.Position.X - Gap - size.X;
        var x = right + size.X <= viewport.X - Margin ? right : left >= Margin ? left : Math.Clamp(_target.Position.X + (_target.Size.X - size.X) / 2, Margin, viewport.X - size.X - Margin);
        var y = Math.Clamp(_target.Position.Y + (_target.Size.Y - size.Y) / 2, Margin, Math.Max(Margin, viewport.Y - size.Y - Margin));
        Face = new Rect2(x, y, size.X, size.Y); QueueRedraw();
    }
    public override void _Draw()
    {
        if (_card is null || Face.Size.X <= 0) return;
        var tex = _scans.Get(_card.Printings.FirstOrDefault()); if (tex is null) return;
        var origin = GetGlobalTransform().Origin;
        var face = new Rect2(Face.Position - origin, Face.Size);
        for (var i = 10; i > 0; i--) DrawRect(face.Grow(i), new Color(0, 0, 0, .05f), true);
        DrawRect(face.Grow(3), new Color("8a7045")); DrawRect(face.Grow(1.5f), new Color("070b0e"));
        DrawTextureRect(tex, face, false);
    }
}
