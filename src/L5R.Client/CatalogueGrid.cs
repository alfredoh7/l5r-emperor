using Godot;
namespace L5R.Client;

/// <summary>Virtual rows keep 2,149 cards responsive without instantiating the entire collection.</summary>
public partial class CatalogueGrid : ScrollContainer
{
    private readonly Control _sheet = new();
    private readonly Dictionary<int, CatalogueCardTile> _tiles = [];
    private CatalogueData.Card[] _cards = [];
    private readonly CatalogueScans _scans;
    private int _columns = 6;
    private float _cellWidth = 240, _rowHeight = 360, _cardWidth = 222;
    private string? _selected;
    private int? _resizeAnchor;
    private float _resizeOffset;
    public void BeginCardResize()
    {
        var row = (int)(ScrollVertical / _rowHeight);
        _resizeAnchor = row * _columns; _resizeOffset = ScrollVertical - row * _rowHeight;
    }
    public void EndCardResize() => _resizeAnchor = null;
    public Action<CatalogueData.Card>? Selected;
    /// <summary>Optional full-size scan preview shown while a card face is hovered or focused.</summary>
    public CardScanHover? Hover { get; set; }
    public float CardWidth { get; set; } = 230;
    public int VisibleTileCount => _tiles.Count;
    public CatalogueData.Card[] Cards => _cards;
    public CatalogueGrid(CatalogueScans scans)
    {
        _scans = scans; FocusMode = FocusModeEnum.All; ClipContents = true; HorizontalScrollMode = ScrollMode.Disabled; VerticalScrollMode = ScrollMode.Auto;
        AddChild(_sheet); Resized += LayoutCards; GetVScrollBar().ValueChanged += _ => { Hover?.Dismiss(); Refresh(); };
        _scans.Changed += RedrawCards; MyCollection.Store.Changed += RedrawCards;
        TreeExiting += () => { _scans.Changed -= RedrawCards; MyCollection.Store.Changed -= RedrawCards; };
    }
    public void SetCards(CatalogueData.Card[] cards, bool reset = true)
    { _cards = cards; foreach (var tile in _tiles.Values) { _sheet.RemoveChild(tile); tile.QueueFree(); } _tiles.Clear(); if (reset) ScrollVertical = 0; LayoutCards(); }
    public void Select(string? id) { _selected = id; RedrawCards(); }
    public void LayoutCards()
    {
        if (Size.X < 10) return;
        var anchor = _resizeAnchor ?? (_columns > 0 ? (int)(ScrollVertical / _rowHeight) * _columns : 0);
        var offset = _resizeAnchor is not null ? _resizeOffset : ScrollVertical - (int)(ScrollVertical / _rowHeight) * _rowHeight;
        var desired = CardWidth + 16;
        _columns = Math.Max(1, (int)((Size.X - 12) / desired));
        _cellWidth = (Size.X - 18) / _columns;
        _cardWidth = Math.Min(_cellWidth - 16, CardWidth);
        _rowHeight = _cardWidth * 1.4f + 57;
        _sheet.CustomMinimumSize = new Vector2(0, (float)Math.Max(Size.Y, Math.Ceiling((double)_cards.Length / _columns) * _rowHeight));
        _sheet.Size = new Vector2(Size.X - 18, _sheet.CustomMinimumSize.Y);
        ScrollVertical = (int)(anchor / _columns * _rowHeight + offset); Refresh();
    }
    private void Refresh()
    {
        if (!IsInsideTree() || Size.Y < 10) return;
        var first = Math.Max(0, ((int)(ScrollVertical / _rowHeight) - 1) * _columns);
        var end = Math.Min(_cards.Length, ((int)((ScrollVertical + Size.Y) / _rowHeight) + 2) * _columns);
        foreach (var key in _tiles.Keys.Where(i => i < first || i >= end).ToArray()) { _sheet.RemoveChild(_tiles[key]); _tiles[key].QueueFree(); _tiles.Remove(key); }
        for (var i = first; i < end; i++)
        {
            if (!_tiles.TryGetValue(i, out var tile))
            {
                var card = _cards[i]; tile = new CatalogueCardTile(card, _scans); tile.Pressed += () => { Hover?.Dismiss(); Selected?.Invoke(card); }; Hover?.Track(tile, () => card); _sheet.AddChild(tile); _tiles[i] = tile;
            }
            tile.Position = new Vector2(i % _columns * _cellWidth + (_cellWidth - _cardWidth) / 2, i / _columns * _rowHeight + 7);
            tile.Size = new Vector2(_cardWidth, _rowHeight - 12); tile.Chosen = tile.Card.Id == _selected; tile.QueueRedraw();
        }
    }
    private void RedrawCards() { foreach (var tile in _tiles.Values) { tile.Chosen = tile.Card.Id == _selected; tile.QueueRedraw(); } }
    public void EnsureCard(string id)
    {
        var index = Array.FindIndex(_cards, c => c.Id == id); if (index < 0) return;
        var top = index / _columns * _rowHeight; var bottom = top + _rowHeight;
        if (top < ScrollVertical) ScrollVertical = (int)top;
        else if (bottom > ScrollVertical + Size.Y) ScrollVertical = (int)(bottom - Size.Y);
        Refresh();
    }
    public CatalogueCardTile? Tile(string id) => _tiles.Values.FirstOrDefault(t => t.Card.Id == id);
}
public partial class CatalogueCardTile : Button
{
    public CatalogueData.Card Card { get; }
    public bool Chosen { get; set; }
    private readonly CatalogueScans _scans;
    public CatalogueCardTile(CatalogueData.Card card, CatalogueScans scans)
    {
        Card = card; _scans = scans; AccessibilityName = card.Name; AccessibilityDescription = card.Playable ? "Playable game version. Select to inspect." : "Reference card, not yet playable. Select to inspect."; Flat = true; Text = "";;
        MouseDefaultCursorShape = CursorShape.PointingHand; TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var face = new Rect2(0, 0, Size.X, Size.X * 1.4f);
        var hot = IsHovered() || HasFocus() || Chosen;
        if (hot) for (var i = 8; i > 0; i--) DrawRect(face.Grow(i), new Color(.91f, .66f, .29f, (Chosen ? .10f : .065f) * (1 - i / 9f)), false, 2);
        DrawRect(face.Grow(2), new Color("7b6034")); DrawRect(face.Grow(1), new Color("12100c"));
        var tex = _scans.Get(Card.Printings.FirstOrDefault());
        if (tex is not null)
        {
            var scale = Math.Min(face.Size.X / tex.GetWidth(), face.Size.Y / tex.GetHeight()); var size = tex.GetSize() * scale;
            DrawTextureRect(tex, new Rect2(face.Position + (face.Size - size) / 2, size), false);
        }
        else
        {
            DrawStyleBox(CatalogueArt.Frame(12), face);
            DrawString(Palette.Body, new Vector2(12, face.Size.Y * .43f), Card.Name, HorizontalAlignment.Center, face.Size.X - 24, 18, CatalogueArt.Ivory);
            DrawString(Palette.Body, new Vector2(12, face.Size.Y * .54f), string.Join(" · ", Card.Types), HorizontalAlignment.Center, face.Size.X - 24, 16, CatalogueArt.Muted);
            DrawString(Palette.Body, new Vector2(8, face.Size.Y - 24), _scans.State(Card.Printings.FirstOrDefault()).Split(" · ")[0], HorizontalAlignment.Center, face.Size.X - 16, 13, CatalogueArt.Muted);
        }
        if (hot) DrawRect(face.Grow(2), CatalogueArt.Gold, false, 2);
        DrawString(Palette.Body, new Vector2(0, face.Size.Y + 19), $"Owned: {MyCollection.Store.Count(Card.Id)}", HorizontalAlignment.Center, Size.X, 16, CatalogueArt.Ivory);
        if (!Card.Playable) DrawString(Palette.Body, new Vector2(0, face.Size.Y + 40), "Not yet playable", HorizontalAlignment.Center, Size.X, 14, CatalogueArt.Muted);
    }
}
