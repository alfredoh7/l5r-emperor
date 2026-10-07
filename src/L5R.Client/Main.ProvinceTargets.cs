using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // "Target a province" abilities (Shrine to Nimuro, Ring of Earth, Genji...): Core offers one option per owner of the province, each carrying the provinces that
    // are legal for that owner. The player picks the province themselves, from one list in two groups (the opponent's, then their own), each row naming the
    // province number and the card in it (or that it is face down). Core still validates the command; the client only presents the engine's legal provinces.
    private static bool IsProvinceTarget(ActionOption o) =>
        o is { Type: "useAbility", Provinces: not null, Movement: null, Attack: null, Command: { TargetPlayerId: not null, TargetId: null, CardIds: null, AbilityId: not null } };

    private bool ProvinceTargetActive => _chosen is not null && IsProvinceTarget(_chosen);

    private ActionOption[] ProvinceSiblings(ActionOption o) => Actions.Where(a => IsProvinceTarget(a) && a.CardId == o.CardId && a.Command!.AbilityId == o.Command!.AbilityId
        && a.Command.PerformerId == o.Command.PerformerId && a.Command.CostOption == o.Command.CostOption).ToArray();

    private bool ProvinceTargetLegal(int owner, int province) =>
        ProvinceTargetActive && ProvinceSiblings(_chosen!).Any(s => s.Enabled && s.Command!.TargetPlayerId == owner && s.Provinces!.Contains(province)) || MultiProvinceLegal(owner, province);

    private bool ProvinceTargetChosen(int owner, int province) =>
        ProvinceTargetActive && _chosen!.Command is { Province: { } chosen, TargetPlayerId: { } chosenOwner } && chosen == province && chosenOwner == owner || MultiProvinceChosen(owner, province);

    // The two owner phrases Core appends so that the per-owner options can be told apart; the grouped button and the picker say it better.
    private static string WithoutProvinceOwner(string label) =>
        BattleActionText.WithoutIds(label.Replace(" · your Province", "", StringComparison.Ordinal).Replace(" · an enemy Province", "", StringComparison.Ordinal));

    // One action button for all the per-owner options of the same ability and performer.
    private void AddProvinceTargetAction(VBoxContainer container, ActionOption option)
    {
        var key = $"{option.CardId}/{option.Command!.AbilityId}/{option.Command.PerformerId}/{option.Command.CostOption}";
        if (container.GetChildren().Any(c => c.HasMeta("province_target_group") && c.GetMeta("province_target_group").AsString() == key)) return;
        var representative = ProvinceSiblings(option).FirstOrDefault(a => a.Enabled) ?? option;
        var label = Placard(representative, WithoutProvinceOwner(representative.Label));
        var grouped = SidebarSkin.Button(label, () => Choose(representative));
        grouped.SetMeta("province_target_group", key); grouped.Disabled = !representative.Enabled || !DecisionCanSubmit;
        grouped.TooltipText = RulesHelp.Tooltip((representative.Reason is { } reason ? "Unavailable: " + reason + "\n\n" : label + "\n\n") + RulesHelp.ActionGuide(representative.Type));
        grouped.AutowrapMode = TextServer.AutowrapMode.WordSmart; grouped.ClipText = true;
        container.AddChild(grouped);
        if (!representative.Enabled) container.AddChild(Palette.Wrapped(representative.Reason ?? "No legal province.", 15, Palette.PaperDim));
    }

    // "Province 2 · Kyuden Otomo" / "Province 3 · Face down" / "Province 1 · Empty": the number is the player-facing one the table labels use.
    private string ProvinceLabel(int owner, int index)
    {
        var province = View.Players.Single(p => p.Id == owner).Provinces.Single(p => p.Index == index);
        var slots = ProvincePresentation.Slots(province).Select(s => s.Card is not { } card ? "Empty" : card.FaceUp && card.Definition is { } definition ? definition.Name : "Face down").ToArray();
        var cards = slots.Length == 0 ? "No Dynasty card" : string.Join(" / ", slots);
        return $"Province {index + 1} · {cards}" + (province.Region is { } region ? " · Region: " + (region.FaceUp && region.Definition is { } regionDefinition ? regionDefinition.Name : "face down") : "");
    }

    private void RenderProvincePicker(ActionOption action)
    {
        var siblings = ProvinceSiblings(action).Where(s => s.Enabled).ToArray();
        _inspector.AddChild(Palette.Text("CHOOSE A PROVINCE", 11, Palette.Gold));
        _inspector.AddChild(Palette.Wrapped("Click a highlighted province on the table or choose below. Hover a row to find its province on the table; nothing happens until you confirm.", 13, Palette.PaperDim));
        foreach (var (heading, owner) in new[] { ("OPPONENT'S PROVINCES", View.Players.First(p => p.Id != View.ViewerId).Id), ("YOUR PROVINCES", View.ViewerId) })
        {
            var rows = siblings.Where(s => s.Command!.TargetPlayerId == owner).SelectMany(s => s.Provinces!).Distinct().Order().ToArray();
            if (rows.Length == 0) continue;
            _inspector.AddChild(Palette.Text(heading, 11, Palette.Gold));
            foreach (var index in rows)
            {
                var (ownerId, provinceIndex) = (owner, index);
                var chosen = ProvinceTargetChosen(ownerId, provinceIndex);
                var label = ProvinceLabel(ownerId, provinceIndex);
                var row = SidebarSkin.Button((chosen ? "✓  " : "○  ") + label, () => ChooseProvinceTarget(ownerId, provinceIndex));
                row.Alignment = HorizontalAlignment.Left; row.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.ClipText = true;
                row.SetMeta("province_target_row", $"{ownerId}:{provinceIndex}");
                row.TooltipText = label;
                if (chosen) row.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: new Color(1.35f, 1.2f, .9f)));
                row.MouseEntered += () => HoverProvinceTarget(ownerId, provinceIndex, true);
                row.MouseExited += () => HoverProvinceTarget(ownerId, provinceIndex, false);
                row.FocusEntered += () => HoverProvinceTarget(ownerId, provinceIndex, true);
                row.FocusExited += () => HoverProvinceTarget(ownerId, provinceIndex, false);
                row.TreeExiting += () => HoverProvinceTarget(ownerId, provinceIndex, false);
                _inspector.AddChild(row);
            }
        }
        if (action.Command!.Province is null) _inspector.AddChild(Palette.Wrapped("Choose a province to continue.", 13, Palette.Error));
    }

    // The row under the pointer draws its province's cards as hovered and brightens the province on the table.
    private void HoverProvinceTarget(int owner, int index, bool on)
    {
        if (!IsInstanceValid(this) || View?.Players.FirstOrDefault(p => p.Id == owner)?.Provinces.FirstOrDefault(p => p.Index == index) is not { } province) return;
        var cards = ProvincePresentation.Cards(province).ToArray();
        foreach (var tile in _tiles)
            if (IsInstanceValid(tile) && cards.Any(c => ReferenceEquals(c, tile.Card) || c.Id.Length > 0 && c.Id == tile.Card.Id)) tile.ForcedHover = on;
        if (_provinceButtons.TryGetValue((owner, index), out var button) && IsInstanceValid(button))
            button.Modulate = on ? new Color(1.45f, 1.3f, .8f) : ProvinceTargetChosen(owner, index) ? new Color(1.4f, 1.25f, .75f) : ProvinceTargetLegal(owner, index) ? new Color(1.2f, 1.15f, .85f) : Colors.White;
    }

    private void ChooseProvinceTarget(int owner, int index)
    {
        if (!ProvinceTargetActive) return;
        var option = ProvinceSiblings(_chosen!).FirstOrDefault(s => s.Enabled && s.Command!.TargetPlayerId == owner && s.Provinces!.Contains(index));
        if (option is null) return;
        _chosen = option with { Command = option.Command! with { Province = index } };
        RenderInspector(); Highlight();
    }
}
