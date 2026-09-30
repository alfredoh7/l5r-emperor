using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class PhaseTrack
{
    private PhaseHelpPanel _help = null!;
    private Label _helpTitle = null!, _helpBody = null!, _helpContext = null!;
    private string _turnContext = "";
    private bool _battleWithinAction;
    private int _helpIndex, _hoverIndex = -1;
    private float _hoverTime;
    public Button HelpButton { get; private set; } = null!;
    private static readonly Phase[] Phases = [Phase.Straighten, Phase.Events, Phase.Action, Phase.AttackDeclaration, Phase.Dynasty, Phase.End];
    private static string SegmentHelp(int index) => index == 3
        ? "Resolve the attack: attacker assignments, defender assignments, battle selection, Battle actions and resolution. The game presents each required decision in order."
        : RulesHelp.PhaseGuide(Phases[index]);
    private void CreateHelp()
    {
        HelpButton = Palette.Button("?", () => ShowHelp(Math.Max(0, _active)));
        HelpButton.AccessibilityName = "Current phase help";
        HelpButton.Text = ""; HelpButton.CustomMinimumSize = Vector2.Zero;
        var seal = new AtlasTexture { Atlas = GD.Load<Texture2D>("res://Art/six-phase-help-reference.png"), Region = new Rect2(1200, 513, 40, 41) };
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" }) HelpButton.AddThemeStyleboxOverride(state, new StyleBoxTexture { Texture = seal, ModulateColor = state == "disabled" ? new Color(1, 1, 1, .45f) : Colors.White });
        HelpButton.FocusEntered += () => ShowHelp(Math.Max(0, _active));
        HelpButton.FocusExited += HideHelp;
        HelpButton.GuiInput += e => HelpInput(e, -1, HelpButton);
        AddChild(HelpButton);
        _help = new PhaseHelpPanel { ZIndex = 100, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_help);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 5); _help.AddChild(box);
        _helpTitle = Palette.Text("", 22, Palette.Paper, true);
        _helpBody = Palette.Wrapped("", 17, Palette.Paper);
        _helpContext = Palette.Text("", 13, Palette.PaperDim);
        foreach (var label in new[] { _helpTitle, _helpBody, _helpContext }) { label.MouseFilter = MouseFilterEnum.Ignore; box.AddChild(label); }
        _help.Hide();
    }
    private void ShowHelp(int index)
    {
        _helpIndex = Math.Clamp(index, 0, 5);
        _helpTitle.Text = _names[_helpIndex];
        _helpBody.Text = _battleWithinAction && _helpIndex == 2
            ? "A card has started a battle during the Action Phase. Take the Battle actions offered by the game. The Action Phase resumes when this battle finishes."
            : SegmentHelp(_helpIndex);
        _helpContext.Text = _turnContext + " · Esc to close";
        var pixelScale = Math.Max(.01f, GetGlobalTransformWithCanvas().Scale.X);
        var textScale = Math.Max(1, 1 / pixelScale);
        _helpTitle.AddThemeFontSizeOverride("font_size", (int)(22 * textScale));
        _helpTitle.AddThemeFontOverride("font", Palette.HeadingLabel((int)(22 * textScale)));
        _helpBody.AddThemeFontSizeOverride("font_size", (int)(17 * textScale));
        _helpContext.AddThemeFontSizeOverride("font_size", (int)(13 * textScale));
        var width = Math.Min(_plate.Size.X, Math.Min(440 * textScale, GetViewportRect().Size.X / pixelScale - 20));
        _help.CustomMinimumSize = new Vector2(width, 0);
        _help.Size = new Vector2(width, 0);
        _help.Show(); PlaceHelp(); QueueRedraw();
    }
    private void HideHelp() { _help.Hide(); QueueRedraw(); }
    private void PlaceHelp()
    {
        if (_help is null || !_help.Visible) return;
        _help.Size = new Vector2(_help.Size.X, _help.GetCombinedMinimumSize().Y);
        var scale = GetGlobalTransformWithCanvas().Scale;
        var origin = GetGlobalTransformWithCanvas().Origin;
        var minX = Math.Max(_plate.Position.X, (8 - origin.X) / Math.Max(.01f, scale.X));
        var maxX = Math.Min(_plate.Position.X + _plate.Size.X - _help.Size.X, (GetViewportRect().Size.X - 8 - origin.X) / Math.Max(.01f, scale.X) - _help.Size.X);
        var desired = _plate.Position.X + _centers[_helpIndex] * _plate.Size.X - _help.Size.X / 2;
        _help.Position = new Vector2(Math.Clamp(desired, minX, Math.Max(minX, maxX)), _plate.Size.Y + 52);
        _help.ArrowX = Math.Clamp(_plate.Position.X + _centers[_helpIndex] * _plate.Size.X - _help.Position.X, 22, _help.Size.X - 22);
        _help.QueueRedraw();
    }
    private void HelpInput(InputEvent input, int index, Control owner)
    {
        if (input is not InputEventKey { Pressed: true } key) return;
        if (key.Keycode == Key.Escape) { HideHelp(); owner.ReleaseFocus(); _hoverIndex = -1; owner.AcceptEvent(); }
        if (index >= 0 && key.Keycode is Key.Left or Key.Right)
        { _areas[(index + (key.Keycode == Key.Right ? 1 : 5)) % 6].GrabFocus(); owner.AcceptEvent(); }
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (_help.Visible && input is InputEventKey { Pressed: true, Keycode: Key.Escape })
        { HideHelp(); _hoverIndex = -1; GetViewport().SetInputAsHandled(); }
    }
}

/// <summary>C's compact help frame, sampled as bitmap corners and edge runs.</summary>
public partial class PhaseHelpPanel : PanelContainer
{
    private Texture2D _source = null!;
    public float ArrowX { get; set; }
    public override void _Ready()
    {
        _source = GD.Load<Texture2D>("res://Art/six-phase-help-reference.png");
        var texture = new AtlasTexture { Atlas = _source, Region = new Rect2(537, 580, 418, 103) };
        var style = new StyleBoxTexture { Texture = texture, DrawCenter = false };
        foreach (var side in new[] { Side.Left, Side.Right, Side.Top, Side.Bottom })
        { style.SetTextureMargin(side, 14); style.SetContentMargin(side, 18); }
        AddThemeStyleboxOverride("panel", style);
    }
    public override void _Draw()
    {
        if (_source is not null)
        {
            DrawTextureRectRegion(_source, new Rect2(Vector2.One * 5, Size - Vector2.One * 10), new Rect2(560, 598, 36, 18));
            DrawColoredPolygon([new(ArrowX, -24), new(ArrowX + 18, 1), new(ArrowX - 18, 1)], Colors.White, [new Vector2(639, 555) / _source.GetSize(), new Vector2(657, 580) / _source.GetSize(), new Vector2(621, 580) / _source.GetSize()], _source);
        }
    }
}
