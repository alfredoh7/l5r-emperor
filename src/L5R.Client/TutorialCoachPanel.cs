using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>The tutorial's note card (issue #935): says what the current step is about and holds the button that moves on. It presents the runner's text and decides nothing.</summary>
public partial class TutorialCoachPanel : PanelContainer
{
    private readonly VBoxContainer _body = new() { MouseFilter = MouseFilterEnum.Ignore };
    private Button? _button;
    private Control? _buttons;
    public Action? Continue { get; set; }
    public Action? Back { get; set; }
    /// <summary>The last note of a lesson that has a lesson after it offers it beside the way back to the menu; this starts it by id.</summary>
    public Action<string>? NextTutorial { get; set; }
    /// <summary>The note's buttons are only shown while the table is at rest, so they can never be pressed in the middle of a card or battle presentation.</summary>
    public void SetReady(bool ready) { if (_buttons is not null && _buttons.Visible != ready) _buttons.Visible = ready; }
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
        StepId = coach.StepId; _button = null; _buttons = null;
        Palette.Clear(_body);
        _body.AddChild(Palette.Text(coach.Finished ? "Tutorial" : $"Tutorial · step {coach.Step} of {coach.Steps}", 13, Palette.Muted));
        _body.AddChild(Palette.Wrapped(coach.Title, 23, Palette.Gold));
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
        var text = Palette.Wrapped(coach.Body, 16, Palette.Paper); text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(text); _body.AddChild(scroll);
        if (coach.Button is null)
        {
            _body.AddChild(Palette.Text("TO CONTINUE", 13, Palette.Crane, true));
            _body.AddChild(Palette.Wrapped(coach.Instruction ?? "Make the move the lesson asks for.", 17, Palette.Paper));
            _body.AddChild(Palette.Wrapped("The next note appears when you have done this.", 14, Palette.Muted));
        }
        if (coach.Button is null && !coach.CanGoBack) return;
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; column.AddThemeConstantOverride("separation", 8); _buttons = column; _body.AddChild(column);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 8); column.AddChild(row);
        if (coach.CanGoBack)
        {
            var back = SidebarSkin.Button("Back", () => Back?.Invoke(), false); back.CustomMinimumSize = new Vector2(96, 44); back.Name = "TutorialBack";
            back.TooltipText = "Undo the last step and return to the scene before it."; row.AddChild(back);
        }
        if (coach.Button is { } label)
        {
            var next = coach.NextLessonId;
            var button = _button = SidebarSkin.Button(label, () => Continue?.Invoke(), next is null); button.CustomMinimumSize = new Vector2(0, 44); button.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(button);
            button.Name = "TutorialContinue";
            if (next is not null)
            {
                var follow = SidebarSkin.Button("Next Tutorial", () => NextTutorial?.Invoke(next), true); follow.CustomMinimumSize = new Vector2(0, 44); follow.Name = "TutorialNext";
                follow.TooltipText = coach.NextTitle is { } title ? "Continue with " + title + "." : "Continue with the next tutorial."; column.AddChild(follow);
            }
        }
    }
}
