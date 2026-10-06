using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private PanelContainer? _candidateTray;
    private void FitCandidateChoices()
    {
        if (_candidateTray is null) return;
        _candidateTray.Position = new(_hand.Position.X, _hand.Position.Y - 366);
        _candidateTray.Size = new(_hand.Size.X, 352);
    }
    private bool RenderCandidateChoices(ActionOption[] options)
    {
        var candidates = options.Select(option => (Option: option, Cards: (option.DecisionCandidateIds ??
            (option.Command?.TargetId is { } target ? [target] : [])).Select(FindCard).Where(c => c?.Definition is not null).Cast<CardView>().ToArray())).ToArray();
        if (!candidates.Any(c => c.Cards.Length > 0)) return false;
        _candidateTray ??= new PanelContainer { Theme = SidebarSkin.Theme() };
        if (_candidateTray.GetParent() is null) _root.AddChild(_candidateTray);
        _candidateTray.Visible = true; Palette.Clear(_candidateTray);
        _candidateTray.AddThemeStyleboxOverride("panel", SidebarSkin.Surface());
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 8); _candidateTray.AddChild(body);
        body.AddChild(Palette.Text("CHOOSE ONE · ONLY YOUR AVAILABLE OPTIONS", 19, Palette.Gold, title: true));
        var scroll = new ScrollContainer
        {
            FollowFocus = true,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        body.AddChild(scroll);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); scroll.AddChild(row);
        foreach (var candidate in candidates)
        {
            var option = candidate.Option;
            var box = new VBoxContainer { CustomMinimumSize = new(220, 0) };
            row.AddChild(box);
            if (candidate.Cards.FirstOrDefault() is { } card)
            {
                var face = new DecisionSourceFace(card, 172, context: "Available choice", interactionHint: "Click to select this option. Confirm choice commits the decision.");
                face.Pressed += () => SelectCandidateOption(option);
                face.Disabled = !option.Enabled || option.Command is null || !DecisionCanSubmit;
                var outline = new PanelContainer();
                var border = SidebarSkin.Inset(); border.ModulateColor = ReferenceEquals(_decisionSelection, option) ? new Color(1.12f, 1.06f, .94f) : Colors.White;
                outline.AddThemeStyleboxOverride("panel", border); outline.AddChild(face); box.AddChild(outline);
            }
            var button = SidebarSkin.Button((ReferenceEquals(_decisionSelection, option) ? "✓ " : "") + Placard(option, option.DecisionLabel ?? option.Label),
                () => SelectCandidateOption(option));
            button.SetMeta("candidate_key", option.Command?.Choice ?? option.Command?.TargetId ?? option.Label);
            button.CustomMinimumSize = new(220, 88); button.Disabled = !option.Enabled || option.Command is null || !DecisionCanSubmit;
            button.AddThemeFontSizeOverride("font_size", 15); box.AddChild(button);
            if (!option.Enabled) box.AddChild(Palette.Wrapped(option.Reason ?? "Unavailable", 14, Palette.PaperDim));
        }
        FitCandidateChoices();
        _inspector.AddChild(Palette.Wrapped(_decisionSelection is { } picked ? Placard(picked) : "Choose a card or option in the tray. Nothing is committed yet.", 20, Palette.Paper));
        var confirm = SidebarSkin.Button("Confirm choice", CommitDecision, true);
        confirm.Disabled = _decisionSelection is null || !DecisionCanSubmit;
        if (_decisionSelection is { } payment && QuoteFor(payment) is not null)
        { var (_, ready) = RenderGoldPanel(payment); confirm.Disabled |= !ready; }
        _decisionActions.AddChild(confirm);
        if (_decisionSelection is not null) _decisionActions.AddChild(SidebarSkin.Button("Clear selection", () => { _decisionSelection = null; _chosen = null; _sources.Clear(); RenderInspector(); Highlight(); }));
        return true;
    }
    private bool SelectPendingCandidate(CardView card)
    {
        if (!GenericDecisionActive) return false;
        if (SelectBudgetCandidate(card)) return true; // Sleep (issue #667)
        var matches = Actions.Where(a => a.Enabled && (a.DecisionCandidateIds?.Contains(card.Id) == true || a.Command?.TargetId == card.Id)).ToArray();
        if (matches.Length == 1) SelectCandidateOption(matches[0]);
        else if (matches.Length > 1) SetNotice("This card has more than one option. Choose its method in the tray.");
        return true;
    }
    private void SelectCandidateOption(ActionOption option)
    {
        if (!DecisionCanSubmit || !option.Enabled || option.Command is null || !Actions.Contains(option)) return;
        _decisionSelection = option; _chosen = null; _sources.Clear();
        if (QuoteFor(option) is not null) { _chosen = option; ResuggestSources(); }
        RenderInspector(); Highlight();
        _candidateTray?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(b => b.GetMeta("candidate_key", "").AsString() == (option.Command?.Choice ?? option.Command?.TargetId ?? option.Label))?.CallDeferred(Control.MethodName.GrabFocus);
    }
}
