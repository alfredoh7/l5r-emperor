using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>The tutorial's note card (issue #935): says what the current step is about and holds the button that moves on. It presents the runner's text and decides nothing.</summary>
public partial class TutorialCoachPanel : PanelContainer
{
    private readonly VBoxContainer _body = new() { MouseFilter = MouseFilterEnum.Ignore };
    private Button? _button;
    public Action? Continue { get; set; }
    /// <summary>The note's button is only shown while the table is at rest, so it can never be pressed in the middle of a card or battle presentation.</summary>
    public void SetReady(bool ready) { if (_button is not null && _button.Visible != ready) _button.Visible = ready; }
    public string StepId { get; private set; } = "";

    public override void _Ready()
    {
        Name = "TutorialCoach"; Theme = SidebarSkin.Theme(); MouseFilter = MouseFilterEnum.Stop;
        var skin = SidebarSkin.Surface(false, true); skin.ContentMarginLeft = skin.ContentMarginRight = 20; skin.ContentMarginTop = 16; skin.ContentMarginBottom = 16;
        AddThemeStyleboxOverride("panel", skin);
        _body.AddThemeConstantOverride("separation", 9); AddChild(_body);
    }

    public void Present(TutorialCoach coach)
    {
        StepId = coach.StepId; _button = null;
        Palette.Clear(_body);
        _body.AddChild(Palette.Text(coach.Finished ? "Tutorial" : $"Lesson 1 · step {coach.Step} of {coach.Steps}", 13, Palette.Muted));
        _body.AddChild(Palette.Wrapped(coach.Title, 23, Palette.Gold));
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
        var text = Palette.Wrapped(coach.Body, 16, Palette.Paper); text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(text); _body.AddChild(scroll);
        if (coach.Button is { } label)
        {
            var button = _button = SidebarSkin.Button(label, () => Continue?.Invoke(), true); button.CustomMinimumSize = new Vector2(0, 44); _body.AddChild(button);
            button.Name = "TutorialContinue";
        }
        else
        {
            _button = null;
            _body.AddChild(Palette.Text("TO CONTINUE", 13, Palette.Crane, true));
            _body.AddChild(Palette.Wrapped(coach.Instruction ?? "Make the move the lesson asks for.", 17, Palette.Paper));
            _body.AddChild(Palette.Wrapped("The next note appears when you have done this.", 14, Palette.Muted));
        }
    }
}
