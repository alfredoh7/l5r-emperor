using Godot;

namespace L5R.Client;

/// <summary>Fits complete cards/units to the shelf. Very crowded shelves page by
/// whole units instead of clipping a scan halfway through at a scroll boundary.</summary>
public partial class CardShelf : Control
{
    public Control Items { get; } = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Button _previous = new TooltipButton() { Name = "PreviousPage", Text = "‹", Flat = true };
    private readonly Button _next = new TooltipButton() { Name = "NextPage", Text = "›", Flat = true };
    private readonly Label _pageLabel = Palette.Text("", 12, Palette.Gold);
    private int _page;

    public CardShelf()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(Items); AddChild(_previous); AddChild(_pageLabel); AddChild(_next);
        _previous.TooltipText = "Previous cards in this area";
        _next.TooltipText = "More cards in this area · units stay together";
        foreach (var button in new[] { _previous, _next })
        {
            button.AddThemeStyleboxOverride("normal", Palette.Box(Colors.Transparent, null, 0));
            button.AddThemeStyleboxOverride("hover", Palette.Box(new Color(.3f, .24f, .12f, .5f), Palette.Gold, 0));
            button.AddThemeStyleboxOverride("pressed", Palette.Box(Colors.Transparent, Palette.Crane, 0));
            button.AddThemeStyleboxOverride("focus", Palette.Box(Colors.Transparent, Palette.Crane, 0));
            button.AddThemeFontSizeOverride("font_size", 20);
        }
        _pageLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _previous.Pressed += () => { _page = Math.Max(0, _page - 1); LayoutCards(); };
        _next.Pressed += () => { _page++; LayoutCards(); };
    }

    public override void _Process(double delta) => LayoutCards();

    private void LayoutCards()
    {
        if (Size.X < 1) return;
        Items.Size = Size;
        var cards = Items.GetChildren().OfType<Control>().ToArray();
        var total = cards.Sum(c => c.GetCombinedMinimumSize().X + 6) - 6;
        var fit = total <= 0 ? 1 : Math.Min(1, Size.X / total);
        var pages = new List<List<Control>> { new() };
        if (fit >= 1f) pages[0].AddRange(cards);
        else
        {
            var occupied = 0f;
            foreach (var card in cards)
            {
                var width = Math.Min(Size.X, card.GetCombinedMinimumSize().X);
                if (occupied > 0 && occupied + width > Size.X) { pages.Add(new()); occupied = 0; }
                pages[^1].Add(card); occupied += width + 6;
            }
        }
        _page = Math.Clamp(_page, 0, pages.Count - 1);
        var paged = pages.Count > 1;
        _previous.Visible = _next.Visible = paged;
        _pageLabel.Visible = false;
        _previous.Disabled = _page == 0; _next.Disabled = _page == pages.Count - 1;
        _pageLabel.Text = "";
        _previous.TooltipText = $"Previous cards · page {_page + 1} of {pages.Count}";
        _next.TooltipText = $"More cards · page {_page + 1} of {pages.Count} · units stay together";
        _previous.Position = new Vector2(Size.X - 54, Size.Y - 21); _previous.Size = new Vector2(24, 18);
        _pageLabel.Position = new Vector2(Size.X - 65, Size.Y - 19); _pageLabel.Size = new Vector2(38, 16);
        _next.Position = new Vector2(Size.X - 24, Size.Y - 21); _next.Size = new Vector2(24, 18);
        var visible = pages[_page];
        var widthSum = visible.Sum(c => c.GetCombinedMinimumSize().X + 6) - 6;
        var scale = Math.Min(1, Size.X / Math.Max(1, widthSum));
        var maxHeight = visible.Count == 0 ? 1 : visible.Max(c => c.GetCombinedMinimumSize().Y);
        scale = Math.Min(scale, Size.Y / Math.Max(1, maxHeight));
        var x = 2f;
        foreach (var card in cards)
        {
            card.Visible = visible.Contains(card);
            if (!card.Visible) continue;
            card.Scale = Vector2.One * scale;
            card.Size = card.GetCombinedMinimumSize();
            card.Position = new Vector2(x, 0);
            x += (card.Size.X + 6) * scale;
        }
    }
}
