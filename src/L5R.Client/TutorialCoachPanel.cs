using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>The tutorial's note card (issue #935): says what the current step is about and holds the button that moves on. It presents the runner's text and decides nothing.</summary>
public partial class TutorialCoachPanel : PanelContainer
{
    private readonly VBoxContainer _body = new() { MouseFilter = MouseFilterEnum.Ignore };
    public Action? Continue { get; set; }
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
        StepId = coach.StepId;
        Palette.Clear(_body);
        _body.AddChild(Palette.Text(coach.Finished ? "Tutorial" : $"Lesson 1 · step {coach.Step} of {coach.Steps}", 13, Palette.Muted));
        _body.AddChild(Palette.Wrapped(coach.Title, 23, Palette.Gold));
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
        var text = Palette.Wrapped(coach.Body, 16, Palette.Paper); text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(text); _body.AddChild(scroll);
        if (coach.Button is { } label)
        {
            var button = SidebarSkin.Button(label, () => Continue?.Invoke(), true); button.CustomMinimumSize = new Vector2(0, 44); _body.AddChild(button);
            button.Name = "TutorialContinue";
        }
        else _body.AddChild(Palette.Wrapped("Your move: use the highlighted choice on the table.", 15, Palette.Crane));
    }
}
