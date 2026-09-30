using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private enum AttackChoice { Unselected, NoTarget, Card, Province }
    private AttackChoice _attackChoice;
    // Every currently legal Attack offers NoTarget. Other province abilities/movement must keep their own UI.
    private static bool HasAttackTargets(ActionOption? action) => action is { Type: "useAbility", NoTarget: true, Movement: null, Command: not null };
    private bool AttackTargetActive => View.WinnerId is null && HasAttackTargets(_chosen) && _chosen!.Command!.PlayerId == View.ViewerId
        && _chosen.Command.ExpectedVersion == View.Version;
    private bool AttackProvinceTarget(int owner, int province) => AttackTargetActive && owner == 1 - View.ActivePlayerId && _chosen!.Provinces?.Contains(province) == true;
    private void ChooseAttackTarget(AttackChoice kind, string? card = null, int? province = null)
    {
        if (!AttackTargetActive || !DecisionCanSubmit) return;
        var action = _chosen!;
        if (kind == AttackChoice.NoTarget && !action.NoTarget || kind == AttackChoice.Card && (card is null || action.TargetIds?.Contains(card) != true)
            || kind == AttackChoice.Province && (province is null || action.Provinces?.Contains(province.Value) != true) || kind == AttackChoice.Unselected) return;
        _attackChoice = kind; _alternativeCostConfirmed = false;
        _chosen = action with { Command = action.Command! with { TargetId = kind == AttackChoice.Card ? card : null, Province = kind == AttackChoice.Province ? province : null } };
        ResuggestSources(); RenderInspector(); Highlight();
    }
    private void InspectAttackCard(CardView card)
    { var (_, body) = Dialog(card.Definition?.Name ?? "Card", 680, 720); body.AddChild(CardPreview.Face(card, 510)); body.AddChild(Palette.Wrapped(card.Definition?.Text ?? "", 17)); }
    private void FocusAttackTargets()
    {
        if (!AttackTargetActive) return;
        var regions = new List<Rect2>();
        if (View.CurrentBattleProvince is { } current && _battleCourts.TryGetValue(current, out var court))
        {
            regions.Add(court);
            if (_matTargets.FirstOrDefault(s => s.Id == $"province:{1 - View.ActivePlayerId}:{current}") is { } province) regions.Add(province.Bounds);
        }
        regions.AddRange(_matTargets.Where(s => (_chosen!.Provinces ?? []).Any(p => s.Id == $"province:{1 - View.ActivePlayerId}:{p}")).Select(s => s.Bounds));
        var ids = (_chosen!.TargetIds ?? []).Append(_chosen.Command!.PerformerId ?? _chosen.CardId ?? "");
        foreach (var id in ids)
            if (_tiles.FirstOrDefault(t => t.Card.Id == id) is { } tile)
                regions.Add(new Rect2(ScreenToWorld(ScreenCenter(tile)) - new Vector2(100, 120), new Vector2(200, 240)));
        if (regions.Count == 0) return;
        var region = regions.Aggregate((a, b) => a.Merge(b)).Grow(38);
        _cameraFitted = false; _cameraGlide = false; _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter(); ApplyTableCamera();
    }
    private bool RenderAttackTargets()
    {
        if (!AttackTargetActive || PresentationBusy) return false;
        var action = _chosen!; var command = action.Command!; _guide.Visible = false;
        var heading = Palette.Text("CHOOSE AN ATTACK TARGET", 19, title: true);
        heading.AutowrapMode = TextServer.AutowrapMode.WordSmart; heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; _sidebarHeader.AddChild(heading);
        var actionLabel = command.PerformerId is { } performerId ? action.Label.Replace($" ({performerId})", "", StringComparison.Ordinal) : action.Label;
        _inspector.AddChild(Palette.Wrapped(actionLabel, 18, Palette.Gold));
        if (FindCard(action.CardId ?? command.CardId ?? "") is { Definition: not null } source)
        {
            var sourceButton = SidebarSkin.Button("From: " + source.Definition.Name + " · inspect", () => InspectAttackCard(source));
            sourceButton.CustomMinimumSize = new(0, 44); sourceButton.AddThemeFontSizeOverride("font_size", 16); _inspector.AddChild(sourceButton);
        }
        if (command.PerformerId is { } performer && FindCard(performer) is { } performing)
            _inspector.AddChild(Palette.Wrapped("Performer: " + ShortName(performing), 16, Palette.PaperDim));
        void AddOption(string name, string label, AttackChoice kind, int? province = null)
        {
            var selected = _attackChoice == kind && (kind != AttackChoice.Province || command.Province == province);
            var button = SidebarSkin.Button((selected ? "✓  " : "○  ") + label, () => ChooseAttackTarget(kind, province: province));
            button.Name = name; button.Disabled = !DecisionCanSubmit; button.CustomMinimumSize = new(0, 82);
            if (selected) button.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: new Color(1.4f, 1.25f, .85f)));
            _inspector.AddChild(button);
        }
        if (action.NoTarget) AddOption("AttackNoTarget", "No target\nNothing will be hit. Costs still apply.", AttackChoice.NoTarget);
        foreach (var province in action.Provinces ?? [])
            AddOption("AttackProvince" + province, $"This battlefield’s province\nProvince {province + 1}", AttackChoice.Province, province);
        var targets = (action.TargetIds ?? []).Select(FindCard).OfType<CardView>().Where(c => c.Definition is not null).ToArray();
        if (targets.Length > 0)
        {
            _inspector.AddChild(Palette.Text("OR CHOOSE A CARD", 17, Palette.Gold));
            var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10); _inspector.AddChild(grid);
            foreach (var target in targets)
            {
                var price = action.TargetGold?.TryGetValue(target.Id, out var quote) == true ? $" · {quote.Cost} Gold" : null;
                var face = new AttackTargetFace(target, _attackChoice == AttackChoice.Card && command.TargetId == target.Id, price) { Disabled = !DecisionCanSubmit };
                face.Pressed += () => ChooseAttackTarget(AttackChoice.Card, target.Id); grid.AddChild(face);
            }
        }
        else _inspector.AddChild(Palette.Wrapped("No card targets are available.", 16, Palette.PaperDim));
        var description = _attackChoice switch
        {
            AttackChoice.NoTarget => "No target — nothing will be hit.",
            AttackChoice.Province => $"Province {command.Province + 1} — this battlefield’s province.",
            AttackChoice.Card => FindCard(command.TargetId!) is { } card ? ShortName(card) : "Card unavailable.",
            _ => "Choose an option before confirming."
        };
        _decisionActions.AddChild(Palette.Wrapped(description, 17, _attackChoice == AttackChoice.Unselected ? Palette.PaperDim : Palette.Gold));
        if (_attackChoice == AttackChoice.Card && FindCard(command.TargetId!) is { } selectedCard)
        {
            var inspect = SidebarSkin.Button("Inspect selected card", () => InspectAttackCard(selectedCard));
            inspect.CustomMinimumSize = new(0, 40); inspect.AddThemeFontSizeOverride("font_size", 16); _decisionActions.AddChild(inspect);
        }
        if (action.AlternativeCost is not null && !_alternativeCostConfirmed)
        {
            _decisionActions.AddChild(SidebarSkin.Button("Review cost choices", () => { _reviewingAlternativeCost = true; _decisionSelection = null; RenderInspector(); }, true));
            _decisionActions.AddChild(SidebarSkin.Button("Cancel attack", CancelChoice)); return true;
        }
        var (required, ready) = RenderGoldPanel(action);
        AddConfirm(_attackChoice == AttackChoice.NoTarget ? "Confirm attack · No target" : "Confirm attack", required, ready,
            () => command with { SourceIds = required > 0 ? _sources.ToArray() : [] }, "Cancel attack", action.Enabled && DecisionCanSubmit && _attackChoice != AttackChoice.Unselected);
        return true;
    }
}
