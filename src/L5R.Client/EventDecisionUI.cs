using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Approved A1/B2 surfaces. All commands come from the session's legal action set.</summary>
public partial class EventDecisionSidebar : Control
{
    public EventDecisionSidebar() { MouseFilter = MouseFilterEnum.Stop; Resized += QueueRedraw; }
    public override void _Draw() => EventDecisionArt.Frame(this, new(Vector2.Zero, Size));
    private Control Content()
    {
        var margin = new MarginContainer(); AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.OffsetLeft = 26; margin.OffsetRight = -26; margin.OffsetTop = 26; margin.OffsetBottom = -26; return margin;
    }
    public Button? KeepButton { get; private set; }
    public Button? DestroyButton { get; private set; }
    private VBoxContainer _column = null!;
    public void ContextAction(string caption, Action action)
    {
        var button = new EventArtButton(caption, action) { CustomMinimumSize = new(0, 48) };
        _column.AddChild(button);
    }
    public void PauseForConnection(bool reconnect, Action action)
    {
        foreach (var button in _column.GetChildren().OfType<Button>()) button.Hide();
        _column.AddChild(Centered(reconnect ? "Connection lost. Your choice is saved." : "Waiting for your opponent to reconnect.", 18, Palette.PaperDim));
        if (reconnect) ContextAction("Reconnect", action);
    }
    public void Present(MatchView view, ActionOption[] actions, CardView? source, bool canCommit, Action<ActionOption> submit)
    {
        Palette.Clear(this); KeepButton = DestroyButton = null;
        Theme = SidebarSkin.Theme();
        var pending = view.PendingChoice!; var own = pending.PlayerId == view.ViewerId;
        var harsh = pending.Type == "harshChoices"; var strife = pending.Type == "forcedDiscard";
        var column = _column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 7); Content().AddChild(column);
        if (harsh) column.AddChild(Centered(own ? "EVENT · YOUR CHOICE" : "EVENT · OPPONENT’S CHOICE", 17, Palette.PaperDim));
        column.AddChild(Centered(harsh ? "HARSH CHOICES" : strife ? "TIMES OF STRIFE" : pending.Type == "brashDraw" ? "BRASH" : "OVERCONFIDENT", 30, Palette.Paper, true));
        column.AddChild(new DecisionRule());
        if (harsh && source?.ProvinceIndex is int province)
            column.AddChild(Centered($"{(own ? "Your" : "Opponent’s")} Province {province + 1}", 19, Palette.Paper));
        if (source is not null) column.AddChild(CardPreview.Face(source, harsh ? 420 : 458));
        if (strife)
        {
            column.AddChild(Centered(own ? "Opponent’s Event" : "Your Event", 22, Palette.Paper));
            column.AddChild(new DecisionRule());
            column.AddChild(Centered(own ? "You announced a Battle action with no units at this battlefield." : "Your opponent is choosing a card to discard from their hand.", 23, Palette.Paper));
        }
        else if (harsh)
        {
            var h = view.PendingHarshChoices!; // the engine's own consequence (issue #581); the client counts nothing
            column.AddChild(Centered(own ? (h.EndsMatch ? "Destroy your last province?" : $"Destroy this province to draw {h.CardsDrawn} cards?") : "Your opponent is deciding whether to destroy this province.", 22, Palette.Paper));
            column.AddChild(new DecisionRule());
            var consequence = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; consequence.AddThemeConstantOverride("separation", 18); column.AddChild(consequence);
            consequence.AddChild(new DecisionImpact { Province = true, CustomMinimumSize = new(38, 52) });
            consequence.AddChild(Centered($"Provinces\n{h.ProvincesBefore} → {h.ProvincesAfter}", 19, Palette.Paper));
            consequence.AddChild(new VSeparator());
            if (!h.EndsMatch) consequence.AddChild(new DecisionImpact { CustomMinimumSize = new(38, 52) });
            consequence.AddChild(Centered(h.EndsMatch ? "Defeat\nMatch ends" : $"Draw\n{h.CardsDrawn} cards", 19, h.EndsMatch ? new Color("efb09a") : Palette.Paper));
            if (own)
            {
                foreach (var option in actions.Where(a => a.Type == "harshChoices"))
                {
                    var destroy = option.Command?.Choice == "destroy";
                    var button = new EventArtButton(option.Label, () => submit(option)) { Danger = destroy };
                    button.CustomMinimumSize = new(0, 66); button.Disabled = !canCommit || !option.Enabled;
                    column.AddChild(button); if (destroy) DestroyButton = button; else KeepButton = button;
                }
            }
            var warning = Centered(h.EndsMatch ? "Your last province. Destroying it ends the match in defeat." : "The province is permanently lost.", 18, h.EndsMatch ? new Color("efb09a") : Palette.PaperDim);
            warning.AddThemeFontOverride("font", SidebarSkin.Italic); column.AddChild(warning);
        }
        else
        {
            column.AddChild(Centered(own ? "You may draw 1 card." : "Your opponent is deciding whether to draw a card.", 24, Palette.Paper));
            if (own) foreach (var option in actions.Where(a => a.Type == pending.Type))
            {
                var b = SidebarSkin.Button(option.Label, () => submit(option), option.Command?.Choice == "draw"); b.Disabled = !canCommit || !option.Enabled; column.AddChild(b);
            }
        }
    }
    internal static Label Centered(string text, int size, Color color, bool heading = false)
    {
        var l = Palette.Wrapped(text, size, color); l.HorizontalAlignment = HorizontalAlignment.Center;
        if (heading) { l.AddThemeFontOverride("font", Palette.HeadingLabel(size)); l.Text = text.ToUpperInvariant(); }
        return l;
    }
}

public partial class EventDecisionHandBar : Control
{
    public EventDecisionHandBar() { MouseFilter = MouseFilterEnum.Stop; Resized += QueueRedraw; }
    public override void _Draw() => EventDecisionArt.Frame(this, new(Vector2.Zero, Size), true);
    public Button Commit { get; private set; } = null!;
    public Button Clear { get; private set; } = null!;
    public void Present(ActionOption? selected, bool canCommit, Action commit, Action clear)
    {
        Palette.Clear(this); Theme = SidebarSkin.Theme();
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 24); AddChild(row); row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); row.OffsetLeft = 46; row.OffsetRight = -28; row.OffsetTop = 16; row.OffsetBottom = -14;
        var instructions = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter }; row.AddChild(instructions);
        instructions.AddChild(Palette.Text("DISCARD 1 CARD", 29, Palette.Paper, true));
        instructions.AddChild(Palette.Wrapped("Choose from your hand.", 23, Palette.Paper));
        var choices = new VBoxContainer { CustomMinimumSize = new(330, 0) }; choices.AddThemeConstantOverride("separation", 3); row.AddChild(choices);
        var status = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; status.AddThemeConstantOverride("separation", 26); choices.AddChild(status);
        status.AddChild(Palette.Text(selected is null ? "0 of 1 selected" : "1 of 1 selected", 17, Palette.PaperDim));
        Clear = Palette.Button("Clear", clear); Clear.Flat = true; Clear.Disabled = selected is null;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) Clear.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        Clear.AddThemeFontSizeOverride("font_size", 17); Clear.CustomMinimumSize = new(50, 23); status.AddChild(Clear);
        Commit = new EventArtButton(selected?.Label ?? "Select a card to discard", commit) { Gold = true }; Commit.CustomMinimumSize = new(330, 54);
        Commit.AddThemeFontSizeOverride("font_size", 20); Commit.Disabled = selected is null || !canCommit; choices.AddChild(Commit);
    }
}

public partial class DecisionRule : Control
{
    public DecisionRule() { CustomMinimumSize = new(0, 14); MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        var c = Size / 2; var gold = new Color("ad8746");
        DrawLine(new(4, c.Y), new(c.X - 13, c.Y), gold with { A = .6f }, 1, true); DrawLine(new(c.X + 13, c.Y), new(Size.X - 4, c.Y), gold with { A = .6f }, 1, true);
        DrawPolyline([c + new Vector2(0, -5), c + new Vector2(5, 0), c + new Vector2(0, 5), c + new Vector2(-5, 0), c + new Vector2(0, -5)], gold, 1.2f, true);
    }
}
public partial class DecisionImpact : Control
{
    public bool Province { get; set; }
    public override void _Draw()
    {
        var g = Palette.Gold;
        if (Province)
        {
            DrawPolyline([new(2, 14), new(10, 18), new(27, 18), new(36, 14)], g, 3, true);
            DrawLine(new(7, 26), new(32, 26), g, 2, true); DrawLine(new(12, 18), new(9, 46), g, 3, true); DrawLine(new(27, 18), new(30, 46), g, 3, true);
        }
        else { for (int i = 0; i < 3; i++) DrawRect(new Rect2(5 + i * 5, 16 - i * 3, 20, 27), g, false, 1.5f, true); }
    }
}
