using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Read-only battle segment navigation. Clicking opens the sequence; it never advances the game.</summary>
public sealed partial class BattleProgressStrip : PanelContainer
{
    public static readonly string[] Segments = [BattleSteps.Declaration, BattleSteps.InfantryManeuvers, BattleSteps.CavalryManeuvers, BattleSteps.FightBattles, BattleSteps.Resolution];
    public static readonly string[] Names = ["Declare", "Infantry", "Cavalry", "Fight", "Resolve"];
    private BattleProgressIcon[] _buttons = [];
    public event Action? ToggleSequence;
    public string ActiveSegment { get; private set; } = BattleSteps.None;
    public Button StepButton(int index) => _buttons[index];
    public BattleProgressStrip()
    {
        Theme = SidebarSkin.Theme(); MouseFilter = MouseFilterEnum.Stop; ZIndex = 24;
        var surface = SidebarSkin.Surface(); surface.ContentMarginLeft = surface.ContentMarginRight = 12;
        surface.ContentMarginTop = surface.ContentMarginBottom = 20; AddThemeStyleboxOverride("panel", surface);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 0); AddChild(column);
        _buttons = Enumerable.Range(0, 5).Select(i => new BattleProgressIcon(i)).ToArray();
        foreach (var b in _buttons) { b.SizeFlagsVertical = SizeFlags.ExpandFill; b.Pressed += () => ToggleSequence?.Invoke(); column.AddChild(b); }
    }
    public void Present(string segment, bool expanded)
    {
        ActiveSegment = segment;
        for (var i = 0; i < _buttons.Length; i++)
        {
            _buttons[i].Current = Segments[i] == segment; _buttons[i].QueueRedraw();
            _buttons[i].TooltipText = Names[i] + (_buttons[i].Current ? " · current segment" : "") + "\n" + (expanded ? "Close" : "Open") + " the full battle sequence. This does not change the game.";
            _buttons[i].AccessibilityName = _buttons[i].TooltipText;
        }
    }
}

public sealed partial class BattleProgressIcon : Button
{
    private readonly int _index;
    private static Texture2D? _reference, _helmet;
    // Actual approved concept pixels, sampled as an atlas at draw time. The generated illustration's game cards/text are never used.
    private static readonly Rect2[] Glyphs = [new(1280, 149, 32, 42), default, new(1275, 392, 43, 33), new(1278, 511, 36, 38), new(1276, 634, 40, 39)];
    public bool Current { get; set; }
    public BattleProgressIcon(int index)
    {
        _index = index; TextureFilter = TextureFilterEnum.LinearWithMipmaps; CustomMinimumSize = new(78, 112); FocusMode = FocusModeEnum.All;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        AddThemeStyleboxOverride("focus", Palette.Box(Colors.Transparent, Palette.Gold, 5, 2));
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var center = new Vector2(Size.X / 2, Size.Y / 2 - 14); var tone = Current ? Palette.Gold : Palette.PaperDim;
        DrawCircle(center, 34, new Color("10100e"));
        if (Current) { DrawArc(center, 36, 0, Mathf.Tau, 64, Palette.Gold with { A = .18f }, 6, true); DrawArc(center, 34, 0, Mathf.Tau, 64, Palette.Gold, 2, true); }
        else DrawArc(center, 34, 0, Mathf.Tau, 64, new Color("796344"), 1.4f, true);
        if (_index == 1) DrawTextureRect(_helmet ??= GD.Load<Texture2D>("res://Art/BattleProgress/infantry-helmet.png"), new Rect2(center - new Vector2(29, 29), new Vector2(58, 58)), false, Colors.White with { A = Current ? 1 : .85f });
        else
        {
            var source = Glyphs[_index]; var size = source.Size * (44 / Math.Max(source.Size.X, source.Size.Y));
            DrawTextureRectRegion(_reference ??= GD.Load<Texture2D>("res://Art/BattleProgress/approved-strip-c.png"), new Rect2(center - size / 2, size), source, Colors.White with { A = Current ? 1 : .85f });
        }
        DrawString(Current ? SidebarSkin.Bold : Palette.Body, new Vector2(0, center.Y + 60), BattleProgressStrip.Names[_index], HorizontalAlignment.Center, Size.X, 16, tone);
    }
    public override Control _MakeCustomTooltip(string text) => ReadingTooltip.Create(this, text);
}
