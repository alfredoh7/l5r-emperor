using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private readonly Dictionary<string, List<string>> _multiCards = [];
    private readonly Dictionary<string, int> _multiPlayers = [];
    private readonly HashSet<string> _multiExpanded = [];
    private string? _multiDraftKey, _multiActiveSlot, _multiMatch, _reactionCameraKey;
    private Window? _multiInspection;
    private static bool HasMultiTargets(ActionOption? action) => action is { Slots.Length: > 0, Command: not null };
    private bool MultiTargetActive => View.WinnerId is null && HasMultiTargets(_chosen) &&
        _chosen!.Command!.PlayerId == View.ViewerId && View.PriorityPlayerId == View.ViewerId && _chosen.Command.ExpectedVersion == View.Version;
    private IEnumerable<TargetSlotOption> MultiUpFront => _chosen?.Slots?.Where(s => s.Timing == TargetSlotTimings.UpFront) ?? [];
    private string MultiKey => $"{_session.MatchKey}/{View.ViewerId}/{View.Version}/{_chosen?.CardId}/{_chosen?.Command?.AbilityId}/{_chosen?.Command?.PerformerId}/{DecisionCanSubmit}";
    private void ClearMultiTargets()
    {
        if (_multiInspection is not null && IsInstanceValid(_multiInspection)) { _multiInspection.Hide(); _multiInspection.QueueFree(); }
        _multiInspection = null; _multiCards.Clear(); _multiPlayers.Clear(); _multiExpanded.Clear(); _multiDraftKey = _multiActiveSlot = _multiMatch = null;
    }
    private void BeginMultiTargets()
    {
        ClearMultiTargets(); if (!MultiTargetActive) return;
        _multiDraftKey = MultiKey; _multiMatch = _session.MatchKey;
        foreach (var slot in MultiUpFront.Where(s => s.Kind == TargetSlotKinds.Card)) _multiCards[slot.Slot] = [];
        _multiActiveSlot = MultiUpFront.FirstOrDefault()?.Slot;
        if (_multiActiveSlot is not null) _multiExpanded.Add(_multiActiveSlot);
        _chosen = _chosen! with { Command = _chosen!.Command! with { Selections = [], TargetId = null } };
    }
    private int MultiCount(TargetSlotOption slot) => slot.Kind == TargetSlotKinds.Card ? _multiCards.GetValueOrDefault(slot.Slot)?.Count ?? 0 : _multiPlayers.ContainsKey(slot.Slot) ? 1 : 0;
    private bool MultiReady => MultiUpFront.All(s => MultiCount(s) >= s.Min && MultiCount(s) <= s.Max);
    private IEnumerable<string> MultiSelectedIds => MultiUpFront.SelectMany(s => _multiCards.GetValueOrDefault(s.Slot) ?? []);
    private GameCommand MultiCommand()
    {
        var slots = MultiUpFront.Select(s => s.Kind == TargetSlotKinds.Card ? new SlotSelection(s.Slot, _multiCards[s.Slot].ToArray()) :
            new SlotSelection(s.Slot, PlayerId: _multiPlayers.TryGetValue(s.Slot, out var player) ? player : null)).ToArray();
        return _chosen!.Command! with { Selections = slots, TargetId = slots.SelectMany(s => s.CardIds ?? []).FirstOrDefault() };
    }
    private void RefreshMultiTargets(string? focus = null)
    {
        var oldTarget = _chosen!.Command!.TargetId;
        _chosen = _chosen with { Command = MultiCommand() }; _alternativeCostConfirmed = false;
        if (_chosen.Command.TargetId is {} target && oldTarget != target && _chosen.TargetGold?.ContainsKey(target) == true) ResuggestSources();
        RenderInspector(); Highlight();
        if (focus is not null && _inspector.FindChildren("*", "Button", true, false).OfType<MultiTargetFace>().FirstOrDefault(b => b.GetMeta("focus_key").AsString() == focus) is {} button && !button.Disabled)
            button.CallDeferred(Control.MethodName.GrabFocus);
    }
    private void ToggleMultiCard(string slotId, string id)
    {
        if (!MultiTargetActive || !DecisionCanSubmit || _multiDraftKey != MultiKey) return;
        var slot = MultiUpFront.FirstOrDefault(s => s.Slot == slotId && s.Kind == TargetSlotKinds.Card);
        if (slot?.CardIds.Contains(id) != true) return;
        var selected = _multiCards[slotId];
        if (!selected.Remove(id))
        {
            if (selected.Count >= slot.Max) { SetNotice($"This group allows at most {slot.Max}. Remove a selection first."); return; }
            selected.Add(id);
        }
        _multiActiveSlot = slotId; _multiExpanded.Add(slotId); RefreshMultiTargets(slotId + "/" + id);
    }
    private bool SelectMultiCard(CardView card)
    {
        if (!MultiTargetActive) return false;
        var slot = MultiUpFront.FirstOrDefault(s => s.Slot == _multiActiveSlot && s.Kind == TargetSlotKinds.Card);
        if (slot?.CardIds.Contains(card.Id) == true) { ToggleMultiCard(slot.Slot, card.Id); return true; }
        // A Gold source can still be chosen through the existing payment panel. A board click during target
        // selection always belongs to the active group, including when the candidate is itself a Holding.
        SetNotice("Choose a gold-outlined card for the active group, or open another group in the sidebar."); return true;
    }
    private void MoveMultiCard(string slotId, int index, int direction)
    {
        if (!MultiTargetActive || !DecisionCanSubmit || _multiDraftKey != MultiKey) return;
        var list = _multiCards[slotId]; var next = index + direction;
        if (next < 0 || next >= list.Count) return;
        var focused = list[index]; (list[index], list[next]) = (list[next], list[index]); RefreshMultiTargets(slotId + "/" + focused);
    }
    private bool RenderMultiTargets()
    {
        if (!MultiTargetActive) { ClearMultiTargets(); return false; }
        if (_multiDraftKey != MultiKey) BeginMultiTargets();
        if (PresentationBusy || _reviewingAlternativeCost) return false;
        var action = _chosen!; var key = _multiDraftKey; _guide.Visible = false;
        var source = FindCard(action.CardId ?? action.Command!.CardId ?? "");
        var title = Palette.Wrapped(source?.Definition?.Name.ToUpperInvariant() ?? "CHOOSE TARGETS", 26);
        title.AddThemeFontOverride("font", Palette.HeadingFace); title.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(title);
        _sidebarHeader.AddChild(new HSeparator());
        var context = new HBoxContainer(); context.AddThemeConstantOverride("separation", 12); _inspector.AddChild(context);
        if (source is { Definition: not null })
        {
            var face = new DecisionSourceFace(source, 140); context.AddChild(face); face.Pressed += () => InspectMultiCard(source);
        }
        var explanation = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; explanation.AddThemeConstantOverride("separation", 8); context.AddChild(explanation);
        var label = action.Command!.PerformerId is {} performer ? action.Label.Replace($" ({performer})", "", StringComparison.Ordinal) : action.Label;
        explanation.AddChild(Palette.Wrapped(label, 18));
        if (action.Command.PerformerId is {} id && FindCard(id) is {} performing)
            explanation.AddChild(Palette.Wrapped("Performer: " + ShortName(performing), 16, Palette.PaperDim));
        _inspector.AddChild(Palette.Wrapped("Choose on the board or below. Nothing resolves until you confirm.", 16, Palette.PaperDim));
        foreach (var slot in MultiUpFront)
        {
            var count = MultiCount(slot); var open = _multiExpanded.Contains(slot.Slot);
            var header = SidebarSkin.Button((open ? "▾ " : "▸ ") + slot.Label + $"\n{count} selected · Choose {(slot.Min == slot.Max ? slot.Min.ToString() : $"{slot.Min}–{slot.Max}")}", () =>
            {
                if (!DecisionCanSubmit || key != _multiDraftKey) return;
                _multiActiveSlot = slot.Slot;
                if (!_multiExpanded.Remove(slot.Slot)) _multiExpanded.Add(slot.Slot);
                RenderInspector(); Highlight();
            });
            header.Name = "MultiGroup_" + slot.Slot; header.CustomMinimumSize = new(0, 78); header.AddThemeFontSizeOverride("font_size", 18);
            if (_multiActiveSlot == slot.Slot) header.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: new Color(1.35f, 1.22f, .9f)));
            header.Disabled = !DecisionCanSubmit; _inspector.AddChild(header);
            if (!open) continue;
            if (slot.Kind == TargetSlotKinds.Player)
            {
                foreach (var playerId in slot.PlayerIds)
                {
                    var player = View.Players.Single(p => p.Id == playerId); var selected = _multiPlayers.GetValueOrDefault(slot.Slot, -1) == playerId;
                    var button = SidebarSkin.Button((selected ? "✓ " : "") + (playerId == View.ViewerId ? "You" : "Your opponent") + " · " + player.Clan, () =>
                    {
                        if (!DecisionCanSubmit || key != _multiDraftKey) return;
                        if (!_multiPlayers.Remove(slot.Slot, out var old) || old != playerId) _multiPlayers[slot.Slot] = playerId;
                        RefreshMultiTargets();
                    });
                    button.SetMeta("slot", slot.Slot); button.SetMeta("player", playerId); button.Disabled = !DecisionCanSubmit; _inspector.AddChild(button);
                }
            }
            else
            {
                var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10); _inspector.AddChild(grid);
                foreach (var targetId in slot.CardIds.OrderBy(id => _multiCards[slot.Slot].IndexOf(id) is var order && order >= 0 ? order : int.MaxValue))
                {
                    if (FindCard(targetId) is not { Definition: not null } target) continue;
                    var selected = _multiCards[slot.Slot].IndexOf(targetId);
                    var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 4); grid.AddChild(column);
                    var ordinal = MultiUpFront.TakeWhile(s => s.Slot != slot.Slot).Sum(s => _multiCards.GetValueOrDefault(s.Slot)?.Count ?? 0) + selected + 1;
                    var face = new MultiTargetFace(target, selected >= 0 ? ordinal : null);
                    face.SetMeta("slot", slot.Slot); face.SetMeta("card", targetId); face.SetMeta("focus_key", slot.Slot + "/" + targetId);
                    face.Disabled = !DecisionCanSubmit || selected < 0 && count >= slot.Max;
                    face.Pressed += () => { if (key == _multiDraftKey) ToggleMultiCard(slot.Slot, targetId); }; column.AddChild(face);
                    var inspect = SidebarSkin.Button("Inspect", () => InspectMultiCard(target)); inspect.CustomMinimumSize = new(164, 32);
                    inspect.AddThemeFontSizeOverride("font_size", 15); inspect.AutowrapMode = TextServer.AutowrapMode.Off;
                    var inspectStyle = SidebarSkin.Surface(true); inspectStyle.ContentMarginTop = inspectStyle.ContentMarginBottom = 4;
                    inspect.AddThemeStyleboxOverride("normal", inspectStyle); inspect.SetMeta("inspect_card", targetId); column.AddChild(inspect);
                    if (selected < 0) continue;
                    var order = new HBoxContainer(); order.AddThemeConstantOverride("separation", 4); column.AddChild(order);
                    Button Small(string text, Action callback)
                    {
                        var b = SidebarSkin.Button(text, callback); b.CustomMinimumSize = new(48, 32); b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                        b.AutowrapMode = TextServer.AutowrapMode.Off; b.AddThemeFontSizeOverride("font_size", 18);
                        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) b.AddThemeStyleboxOverride(state, Palette.Box(new Color("171a1b"), Palette.Gold, 3, 3));
                        return b;
                    }
                    var left = Small("‹", () => MoveMultiCard(slot.Slot, selected, -1)); left.Disabled = !DecisionCanSubmit || selected == 0;
                    left.AccessibilityName = "Move " + ShortName(target) + " earlier"; left.SetMeta("move", -1); left.SetMeta("card", targetId);
                    var remove = Small("×", () => ToggleMultiCard(slot.Slot, targetId)); remove.Disabled = !DecisionCanSubmit; remove.AccessibilityName = "Remove " + ShortName(target);
                    var right = Small("›", () => MoveMultiCard(slot.Slot, selected, 1)); right.Disabled = !DecisionCanSubmit || selected == count - 1;
                    right.AccessibilityName = "Move " + ShortName(target) + " later"; right.SetMeta("move", 1); right.SetMeta("card", targetId);
                    order.AddChild(left); order.AddChild(remove); order.AddChild(right);
                }
            }
        }
        foreach (var followup in action.Slots!.Where(s => s.Timing == TargetSlotTimings.AtResolution))
        {
            _inspector.AddChild(new HSeparator());
            var note = Palette.Wrapped(followup.Label, 17, Palette.Gold);
            note.TooltipText = string.Join("\n", followup.OptionLabels ?? []); _inspector.AddChild(note);
            _inspector.AddChild(Palette.Wrapped("The game will ask this only if the condition is met.", 15, Palette.PaperDim));
        }
        var total = MultiUpFront.Sum(MultiCount);
        _decisionActions.AddChild(Palette.Wrapped($"{total} selected" + (MultiReady ? " · Review the order before confirming." : " · Complete each required group."), 17, MultiReady ? Palette.Gold : Palette.PaperDim));
        if (action.AlternativeCost is not null && !_alternativeCostConfirmed)
        {
            var costs = SidebarSkin.Button("Review cost choices", () => { _reviewingAlternativeCost = true; _decisionSelection = null; RenderInspector(); }, true);
            costs.Disabled = !MultiReady || !DecisionCanSubmit; _decisionActions.AddChild(costs);
        }
        else
        {
            var (required, ready) = RenderGoldPanel(action);
            _paymentConfirm = SidebarSkin.Button((total == 0 ? "Confirm without targets" : $"Confirm {total} target{(total == 1 ? "" : "s")}") + (required > 0 ? $" · {required} Gold" : ""), () =>
            {
                if (!DecisionCanSubmit || !MultiTargetActive || key != _multiDraftKey || !MultiReady || !ready) return;
                Submit(MultiCommand() with { SourceIds = required > 0 ? _sources.ToArray() : [] });
            }, true);
            _paymentConfirm.Disabled = !DecisionCanSubmit || !MultiReady || !ready; _decisionActions.AddChild(_paymentConfirm);
        }
        _decisionActions.AddChild(SidebarSkin.Button("Cancel action", CancelChoice)); return true;
    }
    private void InspectMultiCard(CardView card)
    {
        if (!MultiTargetActive || _multiDraftKey != MultiKey) return;
        if (_multiInspection is not null && IsInstanceValid(_multiInspection)) { _multiInspection.Hide(); _multiInspection.QueueFree(); }
        var (window, body) = Dialog(card.Definition?.Name ?? "Card", 650, 780); _multiInspection = window; body.AddChild(CardPreview.Face(card, 610));
    }
    private void FrameReactionTarget()
    {
        var key = View.PendingChoice?.Type == "beforeActionReaction" ? $"{_session.MatchKey}/{View.ViewerId}/{View.PendingChoice.CardId}" : null;
        if (_reactionCameraKey == key) return;
        if (PresentationBusy) return;
        _reactionCameraKey = key;
        if (key is null) return;
        if (BattleActive) FocusBattle();
        else if (_tiles.FirstOrDefault(t => t.Card.Id == View.PendingChoice!.CardId && _world.IsAncestorOf(t)) is {} tile)
        {
            var region = new Rect2(ScreenToWorld(ScreenCenter(tile)) - new Vector2(240, 260), new Vector2(480, 520));
            _cameraFitted = false; _cameraGlide = false; _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter(); ApplyTableCamera();
        }
    }
    private void RenderReactionTarget()
    {
        if (View.PendingChoice?.Type != "beforeActionReaction" || FindCard(View.PendingChoice.CardId ?? "") is not { Definition: not null } target) return;
        _inspector.AddChild(Palette.Wrapped("Reaction for " + ShortName(target), 20, Palette.Gold));
        var face = new DecisionSourceFace(target, 190); face.Pressed += () => InspectAttackCard(target); _inspector.AddChild(face);
    }
    private bool MultiSelectionsAllowed(ActionOption option) => option.Slots is not null && option.Slots.Where(s => s.Timing == TargetSlotTimings.UpFront).All(s =>
        s.Kind == TargetSlotKinds.Card ? _multiCards.TryGetValue(s.Slot, out var selected) && selected.Count >= s.Min && selected.Count <= s.Max && selected.All(s.CardIds.Contains) :
        _multiPlayers.TryGetValue(s.Slot, out var player) ? s.PlayerIds.Contains(player) : s.Min == 0);
    private void FocusMultiTargets()
    {
        if (!MultiTargetActive) return;
        var candidates = MultiUpFront.SelectMany(s => s.CardIds).Append(_chosen!.Command!.PerformerId ?? _chosen.CardId ?? "").ToHashSet();
        var bounds = _tiles.Where(t => candidates.Contains(t.Card.Id) && _world.IsAncestorOf(t)).Select(t =>
            new Rect2(ScreenToWorld(ScreenCenter(t)) - new Vector2(90, 110), new Vector2(180, 220))).ToArray();
        if (bounds.Length == 0) return;
        var region = bounds.Aggregate((a, b) => a.Merge(b)).Grow(60); _cameraFitted = false; _cameraGlide = false; _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter(); ApplyTableCamera();
    }
    private Vector2 MultiTargetPoint(CardTile tile)
    {
        if (tile.Card.AttachedTo is not null && _world.IsAncestorOf(tile))
            return _tableSurface.ProjectCanvas(tile.GetGlobalRect().Position + new Vector2(tile.Size.X / 2, Math.Min(10, tile.Size.Y / 2)));
        return ScreenCenter(tile);
    }
}
