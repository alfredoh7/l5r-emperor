using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private const float MatPadding = 22, MatHeader = InkMatSections.Header, MatGap = 24;
    private InkMatSections _matSections = null!;
    private float _playLeft, _playWidth;
    private int _matMatchEpoch;
    private float _enemyProvinceY, _reservedBattleHeight, _reservedEnemyBand, _reservedHoldingWidth;
    private readonly float[] _reservedLaneWidths = new float[4];
    private readonly Dictionary<int, Rect2> _battleCourts = [];
    private readonly Dictionary<string, Vector2> _battlePlacements = [];
    private IEnumerable<CardView> CourtUnits(PlayerView player, int province) => player.Army
        .Where(c => c.AttachedTo is null && c.ProvinceIndex == province)
        .Concat(player.Home.Where(c => c.AttachedTo is null && _assignments.GetValueOrDefault(c.Id, -1) == province));
    private readonly Dictionary<int, CardView[][]> _holdingRows = [];
    private readonly Dictionary<int, Rect2> _holdingBounds = [], _homeBounds = [];
    private readonly List<InkMatSection> _matTargets = [];

    private CardView[][] PackMatRows(CardView[] cards, PlayerView player, float available, int maxColumns = int.MaxValue)
    {
        var rows = new List<CardView[]>(); var row = new List<CardView>(); var width = 0f;
        foreach (var card in cards)
        {
            var next = UnitWidth(card, player);
            if (row.Count > 0 && (width + next > available || row.Count >= maxColumns))
            { rows.Add(row.ToArray()); row.Clear(); width = 0; }
            row.Add(card); width += next + CardGap;
        }
        if (row.Count > 0) rows.Add(row.ToArray());
        return rows.ToArray();
    }

    private float MatRowsWidth(CardView[][] rows, PlayerView player) => rows
        .Select(r => r.Sum(c => UnitWidth(c, player)) + Math.Max(0, r.Length - 1) * CardGap).DefaultIfEmpty(220).Max();
    private float MatRowsHeight(CardView[][] rows, PlayerView player) => rows
        .Sum(r => r.Max(c => StackHeight(c, player)) + RowGap) + MatHeader + MatPadding;

    private void MeasureTableLayout()
    {
        var oldSize = new Vector2(WorldWidth, _worldHeight);
        if (!_tableLayoutSeen)
        {
            _matMatchEpoch++; _reservedBattleHeight = 0; _reservedEnemyBand = 0; _reservedHoldingWidth = 0; Array.Clear(_reservedLaneWidths);
            _fallTimes.Clear(); _framedBattle = null;
        }
        _matTargets.Clear(); _battleCourts.Clear(); _battlePlacements.Clear();
        for (var i = 0; i < 4; i++)
        {
            var unitWidth = View.Players.SelectMany(p => CourtUnits(p, i)
                .Select(c => UnitWidth(c, p))).DefaultIfEmpty(154).Max();
            _laneWidths[i] = _reservedLaneWidths[i] = Math.Max(_reservedLaneWidths[i],
                Math.Max(BattleActive ? 440 : 210, ArmyColumns(i) * (unitWidth + CardGap) + MatPadding * 2 + 10));
        }
        // This inner play area mirrors by seat. Holdings deliberately remain on
        // screen-left for both owners and never enter this mirrored slot system.
        var x = 0f;
        _slotCenters[0] = x + 80; x += 160;
        _slotCenters[1] = x + 80; x += 160;
        for (var i = 0; i < 4; i++) { _slotCenters[2 + i] = x + _laneWidths[i] / 2; x += _laneWidths[i]; }
        _slotCenters[6] = x + 80; x += 160;
        _slotCenters[7] = x + 80; x += 160;
        _playWidth = x;

        var holdingWidth = 0f;
        foreach (var player in View.Players)
        {
            _holdingRows[player.Id] = PackMatRows(HoldingCards(player), player, 680, HoldingCards(player).Length > 4 ? 3 : 2);
            holdingWidth = Math.Max(holdingWidth, MatRowsWidth(_holdingRows[player.Id], player) + 2 * MatPadding);
            _homeRows[player.Id] = PackMatRows(HomeCards(player), player, _playWidth - 2 * MatPadding);
            _homeHeights[player.Id] = Math.Max(118, MatRowsHeight(_homeRows[player.Id], player));
            _armyHeights[player.Id] = Math.Max(0, Enumerable.Range(0, 4).Select(province => CourtUnits(player, province).Chunk(ArmyColumns(province)).Sum(r => r.Max(c => StackHeight(c, player)) + RowGap)).DefaultIfEmpty(0).Max());
        }
        holdingWidth = _reservedHoldingWidth = Math.Max(holdingWidth, _reservedHoldingWidth);
        _playLeft = MatPadding + holdingWidth + MatGap;
        WorldWidth = _playLeft + _playWidth + MatPadding;
        var enemy = View.Players.Single(p => p.Id != View.ViewerId);
        var provinceHeight = MatHeader + PlaneHeight(CardHeight) + ProvinceThresholdArt.Footer + MatPadding;
        // Home remains behind the province row: the court-to-province neck
        // never crosses resting cards, Holdings or another player's home.
        _enemyHomeY = MatPadding;
        var returning = enemy.Home.Concat(enemy.Army).Where(c => c.AttachedTo is null && c.Definition?.Type != CardType.Holding)
            .Select(c => c with { Bowed = true }).ToArray();
        var returnRows = PackMatRows(returning, enemy, _playWidth - 2 * MatPadding);
        _reservedEnemyBand = Math.Max(_reservedEnemyBand, Math.Max(_homeHeights[enemy.Id], MatRowsHeight(returnRows, enemy)));
        _enemyProvinceY = _enemyHomeY + _reservedEnemyBand + MatGap + MatHeader;
        var enemyBandBottom = Math.Max(_enemyProvinceY - MatHeader + provinceHeight,
            MatPadding + MatRowsHeight(_holdingRows[enemy.Id], enemy));
        _battleY = enemyBandBottom + 44;
        _battleHeight = _reservedBattleHeight = Math.Max(_reservedBattleHeight, BattleActive ? Math.Max(340, _armyHeights.Values.Sum() + MatHeader + MatPadding + 38) : 0);
        _selfProvinceY = _battleY + _battleHeight + (_battleHeight > 0 ? 44 : 0) + MatHeader;
        _selfHomeY = _selfProvinceY + PlaneHeight(CardHeight) + ProvinceThresholdArt.Footer + MatPadding + MatGap;
        _worldHeight = Math.Max(_selfHomeY + _homeHeights[Self.Id],
            _selfHomeY + MatRowsHeight(_holdingRows[Self.Id], Self)) + MatPadding;

        foreach (var player in View.Players)
        {
            var homeY = player.Id == Self.Id ? _selfHomeY : _enemyHomeY;
            var width = Math.Min(_playWidth, MatRowsWidth(_homeRows[player.Id], player) + 2 * MatPadding);
            _homeBounds[player.Id] = new Rect2(_playLeft + (_playWidth - width) / 2, homeY, width, _homeHeights[player.Id]);
            _holdingBounds[player.Id] = new Rect2(MatPadding, player.Id == Self.Id ? _selfHomeY : MatPadding,
                holdingWidth, MatRowsHeight(_holdingRows[player.Id], player));
            _matTargets.Add(new($"holdings:{player.Id}", "Holdings", _holdingBounds[player.Id]));
            _matTargets.Add(new($"home:{player.Id}", "Home", _homeBounds[player.Id]));
            // Each surviving province has its own fixed identity and C threshold.
            // Destroying one never shifts or renumbers its surviving neighbours.
            foreach (var province in player.Provinces.Where(p => !p.Destroyed))
            {
                var bayWidth = Math.Min(CardHeight + 8, _laneWidths[province.Index] - 12);
                _matTargets.Add(new($"province:{player.Id}:{province.Index}", $"Province {province.Index + 1}",
                    new Rect2(SlotX(player, province.Index + 2) + (CardHeight - bayWidth) / 2,
                        ProvinceRowY(player) - MatHeader, bayWidth, provinceHeight),
                    ProvinceNumber: province.Index + 1));
            }
        }
        if (BattleActive)
        {
            var defender = View.Players.Single(p => p.Id != View.ActivePlayerId);
            foreach (var province in defender.Provinces.Where(p => !p.Destroyed))
            {
                var index = province.Index;
                if (!View.Players.Any(p => CourtUnits(p, index).Any()) && View.CurrentBattleProvince != index) continue;
                var widest = View.Players.SelectMany(p => CourtUnits(p, index).Select(c => UnitWidth(c, p))).DefaultIfEmpty(154).Max();
                var width = Math.Max(440, ArmyColumns(index) * (widest + CardGap) + MatPadding * 2 + 10) - 16;
                var occupiedHeight = View.Players.Sum(p => CourtUnits(p, index).Chunk(ArmyColumns(index))
                    .Sum(r => r.Max(c => StackHeight(c, p)) + RowGap));
                var courtHeight = Math.Max(340, occupiedHeight + MatHeader + MatPadding + 38);
                var yCourt = defender.Id == Self.Id ? _battleY + _battleHeight - courtHeight : _battleY;
                var bounds = new Rect2(SlotX(defender, 2 + index) + CardHeight / 2 - width / 2, yCourt, width, courtHeight);
                _battleCourts[index] = bounds;
                _matTargets.Add(new($"battle:{defender.Id}:{index}", $"Province {index + 1}", bounds, View.CurrentBattleProvince == index));
                // Top army belongs to the top seat, so the defending seat is
                // always closest to its own province, from either player's view.
                foreach (var player in new[] { enemy, Self })
                {
                    var rows = CourtUnits(player, index).Chunk(ArmyColumns(index)).ToArray();
                    var height = rows.Sum(r => r.Max(c => StackHeight(c, player)) + RowGap);
                    var y = player.Id == enemy.Id ? bounds.Position.Y + MatHeader : bounds.End.Y - MatPadding - height;
                    foreach (var row in rows)
                    {
                        var rowWidth = row.Sum(c => UnitWidth(c, player)) + (row.Length - 1) * CardGap;
                        var cx = bounds.GetCenter().X - rowWidth / 2;
                        foreach (var card in row) { _battlePlacements[card.Id] = new Vector2(cx, y); cx += UnitWidth(card, player) + CardGap; }
                        y += row.Max(c => StackHeight(c, player)) + RowGap;
                    }
                }
            }
        }

        // Keep a player's camera steady while ink and cards change positions.
        // Fit/Home remains explicit and always frames the complete new layout.
        if (_tableLayoutSeen && _cameraFitted && oldSize.DistanceTo(new Vector2(WorldWidth, _worldHeight)) > 1) _cameraFitted = false;
        _tableLayoutSeen = true;
        _board.Size = new Vector2(WorldWidth, _worldHeight); _world.Size = _board.Size; _pileHud.Size = _board.Size;
    }

    private void SyncMatSections()
    {
        _matSections.Sync(_matTargets, _reducedMotion, $"{_matMatchEpoch}:{View.Scenario}:{View.ViewerId}:{_session.Snapshot?.RoomCode}");
        _battleDecor?.QueueRedraw();
    }
}
