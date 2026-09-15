using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private Control? _statTokenLayer;
    private IEnumerable<StatTokens> UnitBadges => _statTokenLayer?.GetChildren().OfType<StatTokens>() ?? [];
    private IEnumerable<StatTokens> TokenPairs => UnitBadges.Where(t => t is not UnitStandard);
    private bool OverStatToken(Vector2 point) => _tableViewport.GetGlobalRect().HasPoint(point) && UnitBadges.Any(t => t.Visible && t._HasPoint(t.GetGlobalTransform().AffineInverse() * point));
    private bool BattleActive => View.Phase is Phase.AttackDeclaration or Phase.DefenseAssignment or Phase.BattleSelection or Phase.Battle;

    private void RenderBoard()
    {
        ObserveStatChanges();
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
        _matSections.Emphasize(null, hover:true);
        MeasureTableLayout();
        SyncMatSections();
        var enemy = View.Players.Single(p => p.Id != View.ViewerId);
        AddHoldings(enemy); AddHome(enemy); AddProvinces(enemy); AddBattlefields(); AddProvinces(Self); AddHome(Self); AddHoldings(Self);
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
    private float ProvinceRowY(PlayerView player) => player.Id == View.ViewerId ? _selfProvinceY : _enemyProvinceY;
    private float SlotX(PlayerView player, int slot) =>
        _playLeft + (player.Id == View.ViewerId ? _slotCenters[slot] : _playWidth - _slotCenters[slot]) - CardHeight / 2;
    private CardView[] HomeCards(PlayerView player) => player.Home
        .Where(c => c.AttachedTo is null && c.Definition?.Type != CardType.Holding).ToArray();
    private CardView[] HoldingCards(PlayerView player) => new[] { player.Stronghold }
        .Concat(player.Home.Where(c => c.AttachedTo is null && c.Definition?.Type == CardType.Holding)).ToArray();
    private static int AttachmentCount(CardView card, PlayerView owner) => owner.Home.Concat(owner.Army).Count(c => c.AttachedTo == card.Id);
    private static int TokenAttachmentCount(CardView card, PlayerView owner) => owner.Home.Concat(owner.Army).Count(c => c.AttachedTo == card.Id && CardTokenRail.HasTokens(c));
    private float UnitWidth(CardView card, PlayerView owner) => Math.Max(CardFootprint(card, CardHeight), owner.Home.Concat(owner.Army)
        .Where(c => c.AttachedTo == card.Id).Select(c => CardFootprint(c, CardHeight)).DefaultIfEmpty(0).Max()) + (card.UnitForce.HasValue ? UnitStandard.ReservedWidth : 0) + TokenAttachmentCount(card, owner) * CardTokenRail.Width;
    private float TokenBottomPadding(CardView card, PlayerView owner)
    {
        var attachments=owner.Home.Concat(owner.Army).Where(c=>c.AttachedTo==card.Id).ToArray();
        var padding=card.Bowed&&CardTokenRail.HasTokens(card)?CardTokenRail.Width:0f;
        for(var i=0;i<attachments.Length;i++)
            if(attachments[i].Bowed&&CardTokenRail.HasTokens(attachments[i]))
                padding=Math.Max(padding,(2+attachments.Take(i).Count(CardTokenRail.HasTokens))*CardTokenRail.Width*1.02f);
        return padding;
    }
    private float StackHeight(CardView card, PlayerView owner) => PlaneHeight(CardHeight) + AttachmentCount(card, owner) * 24 + (AttachmentCount(card, owner) > 0 ? 24 : 0) + TokenBottomPadding(card,owner);
    private int ArmyColumns(int province) => Math.Clamp((int)Math.Ceiling(Math.Sqrt(View.Players.Max(p => CourtUnits(p, province).Count()))), 1, 4);
    private static float CardFootprint(CardView card, float height)
    {
        var rail = CardTokenRail.HasTokens(card) ? CardTokenRail.Width * height / CardHeight : 0;
        if (card.Bowed) return height + rail;
        var art = card.Definition is { ScenarioOnly: false } definition ? CardTile.Artwork(definition) : null;
        var ratio = art is null ? .7142857f : art.GetWidth() / (float)art.GetHeight();
        return (height - 12) * ratio + 16 + rail;
    }

    private void AddHome(PlayerView player) => AddMatCards(player, _homeRows[player.Id], _homeBounds[player.Id], false);
    private void AddHoldings(PlayerView player) => AddMatCards(player, _holdingRows[player.Id], _holdingBounds[player.Id], true);
    private void AddMatCards(PlayerView player, CardView[][] rows, Rect2 bounds, bool holdings)
    {
        var group = Place(_board, new Control { MouseFilter = Control.MouseFilterEnum.Ignore },
            bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
        group.SetMeta("home_owner", player.Id);
        group.SetMeta("mat_section", $"{(holdings ? "holdings" : "home")}:{player.Id}");
        var y = MatHeader;
        foreach (var row in rows)
        {
            var rowHeight = row.Max(c => StackHeight(c, player));
            var x = MatPadding;
            foreach (var card in row)
            {
                var width = UnitWidth(card, player);
                var control = card.Definition?.Type == CardType.Personality ? UnitStack(card, player, CardHeight)
                    : Tile(card, true, width, CardHeight);
                Place(group, control, x, y, width, control is CardTile ? PlaneHeight(CardHeight) : StackHeight(card, player));
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
            var panel = Place(_unitDetail, new PanelContainer { Theme = SidebarSkin.Theme() }, 238, 342, BoardWidth, 320);
            panel.AddThemeStyleboxOverride("panel", SidebarSkin.Inset());
            panel.SetMeta("battle_province", personality.ProvinceIndex ?? -1);
            panel.SetMeta("home_owner", personality.OwnerId);
            var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 6); panel.AddChild(content);
            var heading = new HBoxContainer(); content.AddChild(heading);
            var label = Palette.Text(ShortName(personality) + $"   ·   {attachments.Length} attachment(s)", 20, Palette.Paper, title: true);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; heading.AddChild(label);
            var close = Palette.Button("Fold unit  ×", () => { _expandedUnits.Clear(); RenderBoard(); });
            close.AddThemeFontSizeOverride("font_size", 12); heading.AddChild(close);
            var detailSide = Math.Min(210, (BoardWidth - 60) / (2.4f + Math.Max(0, attachments.Length - 1) * .8f));
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
        var faceWidth = Math.Max(CardFootprint(personality, side), attachments.Select(c => CardFootprint(c, side)).DefaultIfEmpty(0).Max()) + (expanded ? 0 : TokenAttachmentCount(personality, owner) * CardTokenRail.Width * side / CardHeight);
        var bannerWidth = personality.UnitForce.HasValue ? UnitStandard.ReservedWidth * side / CardHeight : 0;
        var width = (expanded && attachments.Length > 0 ? faceWidth * 2 + Math.Max(0, attachments.Length - 1) * step : faceWidth) + bannerWidth;
        var tileHeight = detail ? side : PlaneHeight(side);
        var height = tileHeight + (expanded ? 0 : attachments.Length * step) + TokenBottomPadding(personality,owner)*side/CardHeight;
        var group = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
        group.AddThemeConstantOverride("separation", 0);
        var stack = new Control { CustomMinimumSize = new Vector2(width, height + 4), MouseFilter = Control.MouseFilterEnum.Pass };
        group.AddChild(stack); stack.SetMeta("unit_id", personality.Id);
        for (var i = attachments.Length - 1; i >= 0; i--)
        {
            var child = Tile(attachments[i], true, faceWidth, side);
            child.TokenRailOffset = expanded ? 0 : (1 + attachments.Take(i).Count(CardTokenRail.HasTokens)) * CardTokenRail.Width * (side-12)/198f;
            if (detail) { child.ProjectToScreen = null; child.CustomMinimumSize = new Vector2(faceWidth, side); }
            Place(stack, child, expanded ? faceWidth + bannerWidth + i * step : 0, expanded ? 4 : (attachments.Length - 1 - i) * step, faceWidth, tileHeight);
        }
        var front = Tile(personality, true, faceWidth, side);
        if (detail) { front.ProjectToScreen = null; front.CustomMinimumSize = new Vector2(faceWidth, side); }
        Place(stack, front, 0, expanded ? 0 : attachments.Length * step, faceWidth, tileHeight);
        var railPeers=stack.GetChildren().OfType<CardTile>().ToArray();
        foreach(var tile in railPeers)
            tile.TokenRailCompanions=railPeers;
        if (personality.UnitForce.HasValue)
        {
            var standard = new UnitStandard
            {
                CardAnchor = front, Card = personality,
                Companions = detail ? [] : stack.GetChildren().OfType<CardTile>().Where(t => t != front).ToArray(),
                Projection = detail ? null : _tableSurface.ProjectCanvas,
                Inline = detail, Explanation = UnitStandard.ExplainUnit(personality),
                Allowed = () => detail || (_expandedUnits.Count == 0 && _tableViewport.GetGlobalRect().HasPoint(ScreenCenter(front)))
            };
            if (!detail) foreach (var tile in stack.GetChildren().OfType<CardTile>()) tile.AlignUnitRightEdge = true;
            standard.Pressed += () => SelectCard(personality);
            standard.SetMeta("flat_tooltip", standard.Explanation);
            stack.AddChild(standard);
        }
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; footer.AddThemeConstantOverride("separation", 4); group.AddChild(footer);
        if (attachments.Length > 0)
        {
            var expand = new Button { Text = $"{(expanded ? "−" : "+")} {attachments.Length}", Flat = true, CustomMinimumSize = new Vector2(32, 19), TooltipText = $"{(expanded ? "Fold" : "Spread")} {attachments.Length} attachment(s) belonging to {ShortName(personality)}. Each card keeps its own bowed state." };
            CompactButton(expand); expand.AddThemeFontSizeOverride("font_size", 12);
            expand.Pressed += () => { if (PresentationBusy) return; var open = !_expandedUnits.Contains(personality.Id); _expandedUnits.Clear(); if (open) _expandedUnits.Add(personality.Id); RenderBoard(); };
            footer.AddChild(expand);
        }
        return group;
    }

    private void AddProvinces(PlayerView owner)
    {
        foreach (var province in owner.Provinces)
        {
            if (province.Destroyed)
            {
                var marker = Place(_board, new Control { MouseFilter = Control.MouseFilterEnum.Pass,
                    TooltipText = $"{owner.Clan} · Province {province.Index + 1} has fallen. This location is no longer available for assignments." },
                    SlotX(owner, 2 + province.Index), ProvinceRowY(owner) + PlaneHeight(CardHeight) + MatPadding, CardHeight, 64);
                marker.SetMeta("fallen_marker", true);
                continue;
            }
            // Province positions remain stable after destruction: an absent province
            // must never renumber or shift another battlefield's physical destination.
            var area = Place(_board, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, SlotX(owner, 2 + province.Index), ProvinceRowY(owner), CardHeight, PlaneHeight(CardHeight));
            var button = new Button { Text = "", Flat = true };
            CompactButton(button);
            button.Pressed += () => SelectProvince(owner.Id, province.Index);
            Place(area, button, 28, 0, CardHeight - 56, PlaneHeight(CardHeight)); _provinceButtons[(owner.Id, province.Index)] = button;
            if (province.Card is { } card)
            {
                var tile = Tile(card, true, CardHeight, CardHeight);
                Place(area, tile, 0, 0, CardHeight, PlaneHeight(CardHeight));
            }

        }
    }

    private void AddBattlefields()
    {
        if (!BattleActive) return;
        var defender = View.Players.Single(p => p.Id != View.ActivePlayerId);
        foreach (var (index, bounds) in _battleCourts)
        {
            var court = Place(_board, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
            court.SetMeta("battle_province", index);
            var target = new Button { Text = "", Flat = true, TooltipText = $"Province {index + 1} battlefield" };
            CompactButton(target); target.Pressed += () => SelectProvince(defender.Id, index);
            Place(court, target, 0, 0, bounds.Size.X, MatHeader); _laneSummaries[index] = target;
            foreach (var player in View.Players)
            foreach (var unit in player.Army.Where(c => c.AttachedTo is null && c.ProvinceIndex == index))
            {
                var position = _battlePlacements[unit.Id] - bounds.Position;
                Place(court, UnitStack(unit, player, CardHeight), position.X, position.Y, UnitWidth(unit, player), StackHeight(unit, player));
            }
        }
    }

    private void SelectProvince(int owner, int province)
    {
        if (PresentationBusy) return;
        if (_chosen is { Type: "attack" or "assignDefense" } action)
        {
            if (owner != 1 - View.ActivePlayerId || action.Provinces?.Contains(province) != true || _assignmentUnit is null) return;
            _assignments[_assignmentUnit] = province;
            SetNotice($"{ShortName(FindCard(_assignmentUnit)!)} → Province {province + 1}. Confirm assignments when ready.");
            RenderBoard(); RenderInspector(); Highlight(); return;
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
                yield return new(ScreenCenter(tile), WorldToScreen(_battlePlacements.GetValueOrDefault(id, ScreenToWorld(ScreenCenter(target))) + new Vector2(70, 100)), Palette.Gold, true, clip);
        }
        // Cavalry Maneuvers adds units on top of already-committed Infantry attackers/defenders;
        // without a persistent reminder of who is already assigned where, the new declaration has
        // to be made blind. _assignments above only covers the still-being-drafted unit; this covers
        // every unit already locked into a battlefield (both sides), straight from the committed View.
        if (View.Phase is Phase.CavalryDeclaration or Phase.CavalryDefense)
        {
            foreach (var unit in View.Players.SelectMany(p => p.Army))
            {
                if (unit.ProvinceIndex is not int province) continue;
                var tile = _tiles.FirstOrDefault(t => t.Card.Id == unit.Id);
                if (tile is not null && _provinceButtons.TryGetValue((1 - View.ActivePlayerId, province), out var target) && TileVisible(tile))
                    yield return new(ScreenCenter(tile), ScreenCenter(target), Palette.Crane, true, clip);
            }
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
