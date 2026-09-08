using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private Control? _statTokenLayer;
    private IEnumerable<StatTokens> TokenPairs => _statTokenLayer?.GetChildren().OfType<StatTokens>() ?? [];
    private bool OverStatToken(Vector2 point) => _tableViewport.GetGlobalRect().HasPoint(point) && TokenPairs.Any(t => t.Visible && t._HasPoint(t.GetGlobalTransform().AffineInverse() * point));
    private bool BattleActive => View.Phase is Phase.AttackDeclaration or Phase.DefenseAssignment or Phase.BattleSelection or Phase.Battle;

    private void RenderBoard()
    {
        var motion = BeginTableMotion();
        if (_statTokenLayer is null) { _statTokenLayer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; _tableViewport.AddChild(_statTokenLayer); }
        Palette.Clear(_statTokenLayer);
        if (_bowVersion != View.Version)
        {
            _previousBows.Clear(); foreach (var pair in _currentBows) _previousBows[pair.Key] = pair.Value;
            _bowVersion = View.Version; _bowChangedAt = Time.GetTicksMsec();
        }
        Palette.Clear(_board); Palette.Clear(_hand); Palette.Clear(_unitDetail); _tiles.Clear(); _provinceButtons.Clear(); _laneSummaries.Clear();
        // A removed unit must not leave its inspection tray holding camera input.
        _expandedUnits.RemoveWhere(id => !View.Players.SelectMany(p => p.Home.Concat(p.Army)).Any(c => c.Id == id && c.AttachedTo is null));
        MeasureTableLayout();
        var enemy = View.Players.Single(p => p.Id != View.ViewerId);
        AddHome(enemy); AddProvinces(enemy); AddBattlefields(); AddProvinces(Self); AddHome(Self);
        // A shallow physical fan, with complete scans and space for a modest hover lift.
        var step = Math.Min(204, Math.Max(22, (_hand.Size.X - HandCardSide - 84) / Math.Max(1, Self.Hand.Length - 1)));
        var start = (_hand.Size.X - (Math.Max(0, Self.Hand.Length - 1) * step + HandCardSide)) / 2;
        for (var i = 0; i < Self.Hand.Length; i++)
        {
            var offset = i - (Self.Hand.Length - 1) / 2f;
            var tile = Tile(Self.Hand[i], true, HandCardSide, HandCardSide);
            tile.Material = CardPaper.FixedTable;
            tile.RestAngle = Math.Clamp(offset * .028f, -.13f, .13f);
            Place(_hand, tile, start + i * step, Math.Abs(offset) * 1.4f - 1, HandCardSide, HandCardSide);
        }
        foreach (var tile in _tiles) _currentBows[tile.Card.Id] = tile.Card.Bowed;
        RenderClanHud(); RenderPiles(); ApplyTableCamera();
        EndTableMotion(motion);
        Highlight();
    }

    private static void CompactButton(Button button)
    {
        button.AddThemeStyleboxOverride("normal", Palette.Box(Colors.Transparent, null, 1));
        button.AddThemeStyleboxOverride("hover", Palette.Box(new Color(.35f, .29f, .15f, .22f), null, 1));
        button.AddThemeStyleboxOverride("pressed", Palette.Box(new Color(.35f, .29f, .15f, .35f), Palette.Gold, 1));
        button.AddThemeStyleboxOverride("focus", Palette.Box(Colors.Transparent, Palette.Crane, 1));
    }

    private static Control HorizontalShelf(Node parent, float height)
    {
        var shelf = new CardShelf { CustomMinimumSize = new Vector2(0, height) };
        parent.AddChild(shelf);
        return shelf.Items;
    }

    private const float CardHeight = 210, RowGap = 8, CardGap = 10;
    private float WorldWidth = 1360;
    private readonly float[] _laneWidths = [170, 170, 170, 170];
    private readonly float[] _slotCenters = new float[8];
    private float _enemyHomeY, _battleY, _battleHeight, _selfHomeY, _selfProvinceY;
    private float _worldHeight = 820;
    private readonly Dictionary<int, float> _homeHeights = [];
    private readonly Dictionary<int, float> _armyHeights = [];
    private readonly Dictionary<int, CardView[][]> _homeRows = [];
    private float PlaneHeight(float side) => side - 4;
    private float ProvinceRowY(PlayerView player) => player.Id == View.ViewerId ? _selfProvinceY : 12;
    private float SlotX(PlayerView player, int slot) =>
        (player.Id == View.ViewerId ? _slotCenters[slot] : WorldWidth - _slotCenters[slot]) - CardHeight / 2;
    private CardView[] HomeCards(PlayerView player) => new[] { player.Stronghold }
        .Concat(player.Home.Where(c => c.AttachedTo is null && c.Definition?.Type != CardType.Personality))
        .Concat(player.Home.Where(c => c.AttachedTo is null && c.Definition?.Type == CardType.Personality)).ToArray();
    private static int AttachmentCount(CardView card, PlayerView owner) => owner.Home.Concat(owner.Army).Count(c => c.AttachedTo == card.Id);
    private float UnitWidth(CardView card, PlayerView owner) => Math.Max(CardFootprint(card, CardHeight), owner.Home.Concat(owner.Army)
        .Where(c => c.AttachedTo == card.Id).Select(c => CardFootprint(c, CardHeight)).DefaultIfEmpty(0).Max());
    private float StackHeight(CardView card, PlayerView owner) => (card.Definition?.Type == CardType.Personality ? StatTokens.ReservedHeight : 0) + PlaneHeight(CardHeight) + AttachmentCount(card, owner) * 24 + (AttachmentCount(card, owner) > 0 ? 24 : 0);
    private int ArmyColumns(int province) => View.Players.Max(p => p.Army.Count(c => c.AttachedTo is null && c.ProvinceIndex == province)) > 2 ? 2 : 1;
    private void MeasureTableLayout()
    {
        for (var i = 0; i < 4; i++)
        {
            var width = View.Players.SelectMany(p => p.Army.Where(c => c.AttachedTo is null && c.ProvinceIndex == i).Select(c => UnitWidth(c, p))).DefaultIfEmpty(154).Max();
            _laneWidths[i] = Math.Max(170, ArmyColumns(i) * (width + CardGap));
        }
        var x = 20f;
        _slotCenters[0] = x + 75; x += 160;
        _slotCenters[1] = x + 75; x += 160;
        for (var i = 0; i < 4; i++) { _slotCenters[2 + i] = x + _laneWidths[i] / 2; x += _laneWidths[i]; }
        _slotCenters[6] = x + 85; x += 160;
        _slotCenters[7] = x + 85; x += 160;
        // A developed economy expands sideways before it needs another row.
        var homeWidth = View.Players.Max(p => HomeCards(p).Take(12).Sum(c => UnitWidth(c, p) + CardGap)) + 40;
        WorldWidth = Math.Max(x + 20, homeWidth);
        for (var i = 0; i < _slotCenters.Length; i++) _slotCenters[i] += (WorldWidth - x - 20) / 2;
        foreach (var player in View.Players)
        {
            var rows = new List<CardView[]>(); var row = new List<CardView>(); var used = 0f;
            foreach (var card in HomeCards(player))
            {
                var width = UnitWidth(card, player);
                if (row.Count > 0 && used + width > WorldWidth - 40) { rows.Add(row.ToArray()); row.Clear(); used = 0; }
                row.Add(card); used += width + CardGap;
            }
            if (row.Count > 0) rows.Add(row.ToArray());
            _homeRows[player.Id] = rows.ToArray();
            _homeHeights[player.Id] = rows.Sum(r => r.Max(c => StackHeight(c, player)) + RowGap);
            _armyHeights[player.Id] = Math.Max(0, player.Army.Where(c => c.AttachedTo is null).GroupBy(c => c.ProvinceIndex)
                .Select(lane => lane.Chunk(ArmyColumns(lane.Key ?? 0)).Sum(r => r.Max(c => StackHeight(c, player)) + RowGap)).DefaultIfEmpty(0).Max());
        }
        var enemy = View.Players.Single(p => p.Id != View.ViewerId);
        _enemyHomeY = 12 + PlaneHeight(CardHeight) + RowGap;
        _battleY = _enemyHomeY + _homeHeights[enemy.Id] + 8;
        _battleHeight = BattleActive && View.Players.Any(p => p.Army.Length > 0) ? _armyHeights.Values.Sum() + 32 : 24;
        _selfHomeY = _battleY + _battleHeight + 8;
        _selfProvinceY = _selfHomeY + _homeHeights[Self.Id];
        var previousHeight = _worldHeight;
        _worldHeight = _selfProvinceY + PlaneHeight(CardHeight) + 12;
        if (_tableLayoutSeen && _cameraFitted && _worldHeight > previousHeight + 1) _cameraFitted = false;
        _tableLayoutSeen = true;
        _board.Size = new Vector2(WorldWidth, _worldHeight); _world.Size = _board.Size; _pileHud.Size = _board.Size;
    }

    private static float CardFootprint(CardView card, float height)
    {
        if (card.Bowed) return height;
        var art = card.Definition is { ScenarioOnly: false } definition ? CardTile.Artwork(definition) : null;
        var ratio = art is null ? .7142857f : art.GetWidth() / (float)art.GetHeight();
        return (height - 12) * ratio + 16;
    }

    private void AddHome(PlayerView player)
    {
        var group = Place(_board, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, 0,
            player.Id == View.ViewerId ? _selfHomeY : _enemyHomeY, WorldWidth, _homeHeights[player.Id]);
        group.SetMeta("home_owner", player.Id);
        var y = 0f;
        foreach (var row in _homeRows[player.Id])
        {
            var rowHeight = row.Max(c => StackHeight(c, player));
            var x = 20f;
            foreach (var card in row)
            {
                var width = UnitWidth(card, player);
                var control = card.Definition?.Type == CardType.Personality ? UnitStack(card, player, CardHeight)
                    : Tile(card, true, width, CardHeight);
                Place(group, control, player.Id == Self.Id ? x : WorldWidth - x - width, y, width, control is CardTile ? PlaneHeight(CardHeight) : StackHeight(card, player));
                x += width + CardGap;
            }
            y += rowHeight + RowGap;
        }
    }

    private Control UnitStack(CardView personality, PlayerView owner, float side = 88, bool detail = false)
    {
        var attachments = owner.Home.Concat(owner.Army).Where(c => c.AttachedTo == personality.Id).ToArray();
        var expanded = _expandedUnits.Contains(personality.Id);
        if (expanded && !detail)
        {
            // Inspect the whole unit at useful size, instead of forcing six cards into
            // one narrow battlefield. These are the actual live tiles, not extra copies.
            var panel = Place(_unitDetail, new LacquerPanel(), 238, 342, BoardWidth, 320);
            panel.AddThemeStyleboxOverride("panel", Palette.Box(new Color(.04f, .037f, .025f, .98f), Palette.Gold, 16, 3));
            panel.SetMeta("battle_province", personality.ProvinceIndex ?? -1);
            panel.SetMeta("home_owner", personality.OwnerId);
            var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 6); panel.AddChild(content);
            var heading = new HBoxContainer(); content.AddChild(heading);
            var label = Palette.Text(ShortName(personality) + $"   ·   {attachments.Length} attachment(s)", 20, Palette.Paper, title: true);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; heading.AddChild(label);
            var close = Palette.Button("Fold unit  ×", () => { _expandedUnits.Clear(); RenderBoard(); });
            close.AddThemeFontSizeOverride("font_size", 12); heading.AddChild(close);
            var detailSide = Math.Min(210, (BoardWidth - 60) / (1 + attachments.Length * .8f));
            content.AddChild(UnitStack(personality, owner, detailSide, true));
            var location = personality.Zone == Zone.Battlefield ? $"{owner.Clan} · battlefield at Province {personality.ProvinceIndex + 1}" : $"{owner.Clan} · at home";
            content.AddChild(Palette.Text(location + "   ·   Hover any card to read it. Each card retains its own bow state.", 12, Palette.PaperDim));
            var placeholder = new Button { Text = ShortName(personality) + "\nUnit open below", CustomMinimumSize = new Vector2(UnitWidth(personality, owner), PlaneHeight(side)), Flat = true };
            placeholder.AddThemeFontSizeOverride("font_size", 10); CompactButton(placeholder);
            placeholder.Pressed += () => { _expandedUnits.Clear(); RenderBoard(); };
            return placeholder;
        }
        // Expose a usable strip of each attachment. A spread opens the entire card.
        var step = expanded ? side * .80f : 24f;
        var faceWidth = Math.Max(CardFootprint(personality, side), attachments.Select(c => CardFootprint(c, side)).DefaultIfEmpty(0).Max());
        var width = expanded ? faceWidth + attachments.Length * step : faceWidth;
        var tileHeight = detail ? side : PlaneHeight(side);
        var height = tileHeight + (expanded ? 0 : attachments.Length * step);
        var group = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
        group.AddThemeConstantOverride("separation", 0);
        var stack = new Control { CustomMinimumSize = new Vector2(width, height + 4), MouseFilter = Control.MouseFilterEnum.Pass };
        group.AddChild(stack); stack.SetMeta("unit_id", personality.Id);
        for (var i = attachments.Length - 1; i >= 0; i--)
        {
            var child = Tile(attachments[i], true, faceWidth, side);
            if (detail) { child.ProjectToScreen = null; child.CustomMinimumSize = new Vector2(faceWidth, side); }
            Place(stack, child, expanded ? (i + 1) * step : 0, expanded ? 4 : (attachments.Length - 1 - i) * step, faceWidth, tileHeight);
        }
        var front = Tile(personality, true, faceWidth, side);
        if (detail) { front.ProjectToScreen = null; front.CustomMinimumSize = new Vector2(faceWidth, side); }
        Place(stack, front, 0, expanded ? 0 : attachments.Length * step, faceWidth, tileHeight);
        var tokenCards = View.Players.SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Append(p.Stronghold)).Concat(Self.Hand).ToArray();
        var tokens = new StatTokens
        {
            CardAnchor = front,
            Companions = detail ? [] : stack.GetChildren().OfType<CardTile>().Where(t => t != front).ToArray(),
            Card = personality,
            Projection = detail ? null : _tableSurface.ProjectCanvas,
            Explanation = StatTokens.Explain(personality, tokenCards),
            Allowed = () => detail || (_expandedUnits.Count == 0 &&
                _tableViewport.GetGlobalRect().HasPoint(ScreenCenter(front)))
        };
        tokens.Pressed += () => SelectCard(personality);
        if (detail) { tokens.SetInlineSize(54); group.AddChild(tokens); }
        else
        {
            group.AddChild(new Control { CustomMinimumSize = new Vector2(0, StatTokens.ReservedHeight), MouseFilter = Control.MouseFilterEnum.Ignore });
            _statTokenLayer!.AddChild(tokens);
        }
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; footer.AddThemeConstantOverride("separation", 4); group.AddChild(footer);
        if (attachments.Length > 0)
        {
            var expand = new Button { Text = $"{(expanded ? "−" : "+")} {attachments.Length}", Flat = true, CustomMinimumSize = new Vector2(32, 19), TooltipText = $"{(expanded ? "Fold" : "Spread")} {attachments.Length} attachment(s) belonging to {ShortName(personality)}. Each card keeps its own bowed state." };
            CompactButton(expand); expand.AddThemeFontSizeOverride("font_size", 12);
            expand.Pressed += () => { var open = !_expandedUnits.Contains(personality.Id); _expandedUnits.Clear(); if (open) _expandedUnits.Add(personality.Id); RenderBoard(); };
            footer.AddChild(expand);
        }
        return group;
    }

    private void AddProvinces(PlayerView owner)
    {
        foreach (var province in owner.Provinces)
        {
            if (province.Destroyed) continue;
            // Province positions remain stable after destruction: an absent province
            // must never renumber or shift another battlefield's physical destination.
            var area = Place(_board, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, SlotX(owner, 2 + province.Index), ProvinceRowY(owner), CardHeight, PlaneHeight(CardHeight));
            var button = new Button { Text = "", Flat = true };
            CompactButton(button);
            button.Pressed += () => SelectProvince(owner.Id, province.Index);
            Place(area, button, 28, 0, CardHeight - 56, PlaneHeight(CardHeight)); _provinceButtons[(owner.Id, province.Index)] = button;
            if (province.Card is { } card) Place(area, Tile(card, true, CardHeight, CardHeight), 0, 0, CardHeight, PlaneHeight(CardHeight));
        }
    }

    private void AddBattlefields()
    {
        if (!BattleActive) return;
        var defender = View.Players.Single(p => p.Id != View.ActivePlayerId);
        var enemy = View.Players.Single(p => p.Id != View.ViewerId);
        foreach (var province in defender.Provinces)
        {
            if (province.Destroyed) continue;
            var laneWidth = _laneWidths[province.Index];
            var lane = Place(_board, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, SlotX(defender, 2 + province.Index) - (laneWidth - CardHeight) / 2, _battleY, laneWidth, _battleHeight);
            lane.SetMeta("battle_province", province.Index);
            if (View.CurrentBattleProvince == province.Index)
                Place(lane, new ColorRect { Color = new Color(.7f, .53f, .24f, .055f), MouseFilter = Control.MouseFilterEnum.Ignore }, 0, 0, laneWidth - 8, _battleHeight);
            var target = new Button { Text = "", Flat = true, TooltipText = $"Province {province.Index + 1} battlefield" };
            CompactButton(target); target.Pressed += () => SelectProvince(defender.Id, province.Index);
            Place(lane, target, 0, 0, laneWidth - 8, 24); _laneSummaries[province.Index] = target;
            foreach (var player in new[] { enemy, Self })
            {
                var y = player.Id == enemy.Id ? 12 : 12 + _armyHeights[enemy.Id] + 8;
                foreach (var row in player.Army.Where(c => c.AttachedTo is null && c.ProvinceIndex == province.Index).Chunk(ArmyColumns(province.Index)))
                {
                    var rowHeight = row.Max(c => StackHeight(c, player));
                    var x = 5f;
                    foreach (var unit in row)
                    {
                        var width = UnitWidth(unit, player);
                        Place(lane, UnitStack(unit, player, CardHeight), x, y, width, StackHeight(unit, player));
                        x += width + CardGap;
                    }
                    y += rowHeight + RowGap;
                }
            }
        }
    }

    private void SelectProvince(int owner, int province)
    {
        if (_chosen is { Type: "attack" or "assignDefense" } action)
        {
            if (owner != 1 - View.ActivePlayerId || action.Provinces?.Contains(province) != true || _assignmentUnit is null) return;
            _assignments[_assignmentUnit] = province;
            SetNotice($"{ShortName(FindCard(_assignmentUnit)!)} → Province {province + 1}. Confirm assignments when ready.");
            RenderInspector(); Highlight(); return;
        }
        if (_chosen is not null) return;
        var battle = Actions.FirstOrDefault(a => a.Enabled && a.Type == "selectBattle" && a.Command?.Province == province);
        if (owner == 1 - View.ActivePlayerId && battle is not null) { Submit(battle.Command!); return; }
        if (View.Players.Single(p => p.Id == owner).Provinces.Single(p => p.Index == province).Card is { } card) SelectCard(card);
    }

    private IEnumerable<IntentArrow> IntentArrows()
    {
        if (_session.Snapshot is null || !IsInstanceValid(_board)) yield break;
        var clip = _tableViewport.GetGlobalRect();
        foreach (var (id, province) in _assignments)
        {
            var tile = _tiles.FirstOrDefault(t => t.Card.Id == id);
            if (tile is not null && _provinceButtons.TryGetValue((1 - View.ActivePlayerId, province), out var target) && TileVisible(tile))
                yield return new(ScreenCenter(tile), ScreenCenter(target), Palette.Crane, true, clip);
        }
        if (_chosen is { Type: "equip" or "useAbility" or "playStrategy", Command: { TargetId: string targetId } command })
        {
            var from = _tiles.FirstOrDefault(t => t.Card.Id == (command.PerformerId ?? command.CardId));
            var to = _tiles.FirstOrDefault(t => t.Card.Id == targetId);
            if (from is not null && to is not null && from != to && TileVisible(from) && TileVisible(to))
                yield return new(ScreenCenter(from), ScreenCenter(to), Palette.Gold, false, from.Card.Zone == Zone.Hand ? clip.Merge(_hand.GetGlobalRect()) : clip);
        }
    }

    private static bool TileVisible(CardTile tile)
    {
        var center = tile.GetGlobalRect().GetCenter();
        for (Node? parent = tile.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll && !scroll.GetGlobalRect().HasPoint(center)) return false;
        return tile.IsVisibleInTree();
    }
}
