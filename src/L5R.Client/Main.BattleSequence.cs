using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private BattleProgressStrip? _battleProgressStrip;
    private bool _battleSequenceOpen;
    private string? _battleSequenceKey;
    private const float BattleStripWidth = 108;
    private bool HasBattleProgress => _session.Snapshot is not null && View.WinnerId is null &&
        (View.BattleStep is { Segment: not BattleSteps.None } || _resolutionMessage is not null || PresentationBusy && !_actionOutcome);
    private float BattleStripReserve => HasBattleProgress ? BattleStripWidth + 8 : 0;
    private string ProgressSegment => _resolutionMessage is not null || PresentationBusy && !_actionOutcome ? BattleSteps.Resolution : View.BattleStep?.Segment ?? BattleSteps.None;
    private string ProgressKey => $"{_session.MatchKey}/{View.ViewerId}/{View.Version}";
    private void BuildBattleSequence()
    {
        _battleProgressStrip = new(); _battleProgressStrip.ToggleSequence += () =>
        { _battleSequenceOpen = !_battleSequenceOpen; _battleSequenceKey = ProgressKey; RenderInspector(); RenderPhaseActions(); _battleProgressStrip.Present(ProgressSegment, _battleSequenceOpen); };
        _root.AddChild(_battleProgressStrip); FitBattleSequence();
    }
    private void FitBattleSequence()
    {
        if (_battleProgressStrip is null) return;
        _battleProgressStrip.Visible = HasBattleProgress;
        if (!HasBattleProgress) { _battleSequenceOpen = false; return; }
        _battleProgressStrip.Position = new(_layoutWidth - SidebarWidth - 22 - BattleStripReserve, 94);
        _battleProgressStrip.Size = new(BattleStripWidth, 892);
        _battleProgressStrip.Present(ProgressSegment, _battleSequenceOpen);
    }
    private static string BattleStepTitle(BattleStepView? step) => step?.Step switch
    {
        BattleSteps.DeclareOrDecline => "Declare an attack", BattleSteps.AttackerAssigns => step.Segment == BattleSteps.CavalryManeuvers ? "Cavalry · attacker" : "Infantry · attacker",
        BattleSteps.DefenderAssigns => step.Segment == BattleSteps.CavalryManeuvers ? "Cavalry · defender" : "Infantry · defender",
        BattleSteps.ChooseBattle => "Choose battlefield", BattleSteps.Engage => "Engage", BattleSteps.BattleActions => "Battle actions",
        BattleSteps.BeforeResolution => "Before resolution", BattleSteps.Aftermath => "Battle aftermath", _ => "Battle progress"
    };
    private string BattlePerson(int? id) => id is not {} player ? "No pending decision" : (player == View.ViewerId ? "You" : "Your opponent") + " · " + View.Players.Single(p => p.Id == player).Clan;
    private string FirstActionReason(string? reason) => reason switch
    {
        BattleSteps.ReasonDefault => "Defender’s normal first opportunity", BattleSteps.ReasonNaval => "Naval Invasion",
        BattleSteps.ReasonEngageGrant => "Engage effect", BattleSteps.ReasonDefenderOverride => "Defender’s overriding effect", _ => reason ?? "Not yet determined"
    };
    private void RenderBattleFacts(bool showDecision = false)
    {
        if (View.BattleStep is not {} step || step.Segment == BattleSteps.None || PresentationBusy) return;
        if (showDecision) FlowText(BattlePerson(step.DecisionPlayerId), 19, Palette.Gold);
        if (step.Step is BattleSteps.AttackerAssigns or BattleSteps.DefenderAssigns)
            FlowText("Attacker assigns first, then defender. You may leave units unassigned.", 16, Palette.PaperDim);
        if (step.FirstActionPlayerId is not null)
        {
            FlowText((step.FirstActionResolved ? "First action: " : "Provisional first action: ") + BattlePerson(step.FirstActionPlayerId), 17, Palette.Paper);
            FlowText(FirstActionReason(step.FirstActionReason) + (step.FirstActionResolved ? "." : " · may change during Engage."), 16, Palette.PaperDim);
        }
        if (step.FirstActionLimit == BattleSteps.LimitMustPass) FlowText("The first opportunity must be passed.", 17, Palette.Gold);
        if (step.FirstActionLimit == BattleSteps.LimitPerformerOnly)
            FlowText("First Battle action must be performed by " + (FindCard(step.FirstActionPerformerId ?? "")?.Definition?.Name ?? "the named performer") + ".", 17, Palette.Gold);
        if (step.Step == BattleSteps.BattleActions)
        {
            FlowText($"{step.ConsecutivePasses} consecutive pass{(step.ConsecutivePasses == 1 ? "" : "es")}", 19, Palette.Gold);
            FlowText(step.ConsecutivePasses == 1 ? "The next Battle-action pass proceeds to resolution." : "Two Battle-action passes in a row end this segment.", 16, Palette.PaperDim);
        }
        if (step.EngageClaims.Length > 0)
            foreach (var claim in step.EngageClaims)
                FlowText("Claim · " + BattlePerson(claim.PlayerId) + " · " + (FindCard(claim.SourceId ?? "")?.Definition?.Name ?? (claim.Kind == "Naval" ? "Naval Invasion" : "Engage effect")), 15, Palette.PaperDim);
        if (step.BeforeResolutionEffects is { Length: > 0 } effects)
        {
            FlowText("Before resolution", 19, Palette.Gold);
            foreach (var effect in effects)
                FlowText((effect.Id == step.ActiveBeforeResolutionSourceId ? "Now · " : "Waiting · ") + effect.Label + " · " + View.Players.Single(p => p.Id == effect.PlayerId).Clan, 16, Palette.PaperDim);
        }
        _inspector.AddChild(new HSeparator());
    }
    private bool RenderBattleSequence()
    {
        if (!HasBattleProgress || _battleSequenceKey != ProgressKey)
        { _battleSequenceOpen = false; _battleProgressStrip?.Present(ProgressSegment, false); }
        if (!_battleSequenceOpen) return false;
        _guide.Visible = false;
        var title = Palette.Text("BATTLE SEQUENCE", 26, Palette.Paper, true); title.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(title);
        _sidebarHeader.AddChild(Palette.Wrapped("Current: " + (_resolutionMessage is not null ? "Battle result" : BattleStepTitle(View.BattleStep)), 18, Palette.Gold));
        _sidebarHeader.AddChild(new HSeparator());
        var stages = new[] { "Declaration", "Infantry Maneuvers", "Cavalry Maneuvers", "Fight Battles", "Resolution" };
        for (var i = 0; i < stages.Length; i++)
        {
            var active = BattleProgressStrip.Segments[i] == ProgressSegment;
            FlowText((active ? "● " : "○ ") + stages[i], 21, active ? Palette.Gold : Palette.PaperDim);
            if (i == 0) FlowText("Declare an attack, or decline to Dynasty.", 15, Palette.PaperDim);
            if (i is 1 or 2) FlowText("Attacker assigns → defender assigns", 15, Palette.PaperDim);
            if (i == 3)
                foreach (var (key, text) in new[] { (BattleSteps.ChooseBattle, "Choose battlefield"), (BattleSteps.Engage, "Engage · when offered"), (BattleSteps.BattleActions, "First opportunity, then alternating actions") })
                    FlowText("    " + (active && View.BattleStep?.Step == key ? "● " : "· ") + text, 16, active && View.BattleStep?.Step == key ? Palette.Gold : Palette.PaperDim);
            if (i == 4) FlowText("Before-resolution effects → Force comparison → aftermath", 15, Palette.PaperDim);
            _inspector.AddChild(new HSeparator());
        }
        RenderBattleFacts(true);
        FlowText("The game presents only the decisions that apply. This sequence does not skip or submit actions.", 15, Palette.PaperDim);
        _decisionActions.AddChild(SidebarSkin.Button(_chosen is null ? "Back to current decision" : "Back to selected action", () => { _battleSequenceOpen = false; RenderInspector(); _battleProgressStrip!.Present(ProgressSegment, false); }));
        return true;
    }
}
