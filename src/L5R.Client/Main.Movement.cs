using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private readonly Dictionary<string, Vector2> _movementPlacements = [];
    private bool MovementActive => View.WinnerId is null && _chosen is { Type: "useAbility", Movement: not null, Command: not null }
        && _chosen.Command.PlayerId == View.ViewerId && _chosen.Command.ExpectedVersion == View.Version;
    private bool MovementDestination(int owner, int province) => MovementActive && _chosen!.Movement!.DefenderId == owner && _chosen.Provinces?.Contains(province) == true;
    private string[] MovementMoverIds() => !MovementActive ? [] : _chosen!.Movement!.FixedMoverIds
        .Concat(_chosen.Movement.TargetMoves && _chosen.Command!.TargetId is { } target ? new[] { target } : []).Distinct().ToArray();
    private IEnumerable<CardView> MovementCourtUnits(PlayerView owner, int province) => !MovementActive || _chosen!.Command!.Province != province ? [] :
        MovementMoverIds().Select(FindCard).OfType<CardView>().Where(c => c.OwnerId == owner.Id && c.AttachedTo is null &&
            (c.Zone != Zone.Battlefield || c.ProvinceIndex != province));
    private bool MovementGhost(CardView card, int province) => MovementActive && _chosen!.Command!.Province == province &&
        MovementMoverIds().Contains(card.Id) && (card.Zone != Zone.Battlefield || card.ProvinceIndex != province);

    private void ChooseMovementProvince(int owner, int province)
    {
        if (!DecisionCanSubmit || !MovementDestination(owner, province)) return;
        _chosen = _chosen! with { Command = _chosen.Command! with { Province = province } };
        RenderBoard(); RenderInspector(); Highlight(); FocusMovement();
    }
    private void FocusMovement()
    {
        if (!MovementActive) return;
        var regions = (_chosen!.Provinces ?? []).Where(_battleCourts.ContainsKey).Select(p => _battleCourts[p]).ToList();
        regions.AddRange(_matTargets.Where(s => (_chosen.Provinces ?? []).Any(p => s.Id == $"province:{_chosen.Movement!.DefenderId}:{p}")).Select(s => s.Bounds));
        if (View.CurrentBattleProvince is { } current && _battleCourts.TryGetValue(current, out var court)) regions.Add(court);
        foreach (var id in MovementMoverIds())
            if (_tiles.FirstOrDefault(t => t.Card.Id == id) is { } tile)
                regions.Add(new Rect2(ScreenToWorld(ScreenCenter(tile)) - new Vector2(100, 120), new Vector2(200, 240)));
        if (regions.Count == 0) return;
        var region = regions.Aggregate((a, b) => a.Merge(b)).Grow(38);
        _cameraFitted = false; _cameraGlide = false; _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter(); ApplyTableCamera();
    }
    private IEnumerable<(CardView Unit, PlayerView Owner, Vector2 Origin)> ProposedUnitPlacements()
    {
        foreach (var id in _assignments.Keys)
            if (Self.Home.FirstOrDefault(c => c.Id == id) is { } unit && _battlePlacements.TryGetValue(id, out var origin)) yield return (unit, Self, origin);
        foreach (var (id, origin) in MovementActive ? _movementPlacements : [])
            if (FindCard(id) is { } unit) yield return (unit, View.Players.Single(p => p.Id == unit.OwnerId), origin);
    }
    private bool RenderMovement()
    {
        if (!MovementActive || PresentationBusy) return false;
        var action = _chosen!; var command = action.Command!; var movement = action.Movement!;
        _guide.Visible = false;
        _sidebarHeader.AddChild(Palette.Text("CHOOSE A DESTINATION", 23, title: true));
        var source = FindCard(action.CardId ?? command.PerformerId ?? "");
        if (source?.Definition is not null)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); _inspector.AddChild(row);
            var face = new DecisionSourceFace(source, 180); row.AddChild(face);
            face.Pressed += () => { var (_, body) = Dialog(source.Definition.Name, 680, 720); body.AddChild(CardPreview.Face(source, 510)); body.AddChild(Palette.Wrapped(source.Definition.Text, 17)); };
            var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; row.AddChild(words);
            words.AddChild(Palette.Wrapped(source.Definition.Name, 21, Palette.Gold));
            words.AddChild(Palette.Wrapped("Select a highlighted battlefield or choose below. The arrows show proposed movement.", 18));
        }
        _inspector.AddChild(Palette.Wrapped(action.Label, 17, Palette.PaperDim));
        if (action.TargetIds is { Length: > 0 } targets)
        {
            _inspector.AddChild(Palette.Text("TARGET", 17, Palette.Gold));
            var picker = new OptionButton { Name = "MovementTargetPicker", FitToLongestItem = false, ClipText = true, Disabled = !DecisionCanSubmit };
            foreach (var id in targets)
                picker.AddItem(FindCard(id) is { } card ? $"{View.Players[card.OwnerId].Clan} · {ShortName(card)}" : "Card unavailable");
            picker.Selected = Array.IndexOf(targets, command.TargetId); picker.ItemSelected += i => { if (DecisionCanSubmit) ChooseTarget(targets[(int)i]); };
            _inspector.AddChild(picker);
            if (command.TargetId is null) _inspector.AddChild(Palette.Wrapped("Choose a target to continue.", 15, Palette.Error));
        }
        var names = string.Join(" and ", MovementMoverIds().Select(id => FindCard(id)).OfType<CardView>().Select(ShortName));
        foreach (var province in action.Provinces ?? [])
        {
            var selected = command.Province == province;
            var button = SidebarSkin.Button((selected ? "✓  " : "○  ") + $"Province {province + 1}", () => ChooseMovementProvince(movement.DefenderId, province));
            button.Disabled = !DecisionCanSubmit; button.SetMeta("movement_province", province); button.TooltipText = $"Proposed: {names} → Province {province + 1}";
            if (selected) button.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: new Color(1.35f, 1.2f, .9f)));
            _inspector.AddChild(button);
        }
        _inspector.AddChild(new HSeparator());
        _inspector.AddChild(Palette.Text("PROPOSED MOVEMENT", 18, Palette.Gold));
        _inspector.AddChild(Palette.Wrapped(command.Province is { } destination ? $"{names} → Province {destination + 1}." : "Choose one destination before confirming.", 18));
        _inspector.AddChild(Palette.Wrapped("Nothing moves until you confirm. Reactions may prevent individual movements.", 15, Palette.PaperDim));
        if (action.AlternativeCost is not null && !_alternativeCostConfirmed)
        {
            _decisionActions.AddChild(SidebarSkin.Button("Review cost choices", () => { _reviewingAlternativeCost = true; _decisionSelection = null; RenderInspector(); }, true));
            _decisionActions.AddChild(SidebarSkin.Button("Cancel movement", CancelChoice)); return true;
        }
        var (required, ready) = RenderGoldPanel(action);
        AddConfirm("Confirm movement", required, ready, () => command with { SourceIds = required > 0 ? _sources.ToArray() : [] }, "Cancel movement",
            action.Enabled && DecisionCanSubmit && command.Province is { } chosen && action.Provinces?.Contains(chosen) == true
            && (action.TargetIds is not { Length: > 0 } || command.TargetId is not null)); // issue #581: no default target, so the player picks one first
        return true;
    }
}
