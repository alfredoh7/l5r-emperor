using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private TableBattleInk _battleDecor = null!;
    private SessionSnapshot? _presentationSnapshot;
    private int _battlePresentationStage;
    private double _battlePresentationClock, _battleVisualTime;
    private long _presentedResolutionVersion = -1;
    private string _battleOutcomeText = "";
    private readonly Dictionary<(int Owner, int Province), double> _fallTimes = [];
    private string? _presentationRoom;
    private bool PresentationBusy => _battlePresentationStage != 0;

    // Only the permitted previous snapshot is held briefly for the outcome beat.
    // No fabricated intermediate rules state, prediction or command is introduced.
    private void PrepareBattlePresentation()
    {
        var next = _session.Snapshot!;
        if (!_tableLayoutSeen || _presentationRoom != next.RoomCode || _motionView is null || next.View.Version < _motionView.Version || next.View.ViewerId != _motionView.ViewerId || next.View.Scenario != _motionView.Scenario)
        { _fallTimes.Clear(); _presentationSnapshot = null; _battlePresentationStage = 0; _presentedResolutionVersion = -1; _motionView = null; _tableLayoutSeen = false; }
        _presentationRoom = next.RoomCode;
        if (PresentationBusy || _motionView?.Phase != Phase.Battle || _reducedMotion || _motionView is null || next.View.Version <= _motionView.Version ||
            next.View.ViewerId != _motionView.ViewerId || next.View.Scenario != _motionView.Scenario) return;
        var sequence = _motionView.Events.LastOrDefault()?.Sequence ?? 0;
        var events = next.View.Events.Where(e => e.Sequence > sequence).ToArray();
        if (!events.Any(e => e.Type == "battleResolved") || next.View.Version == _presentedResolutionVersion) return;
        _presentedResolutionVersion = next.View.Version;
        _battleOutcomeText = events.FirstOrDefault(e => e.Type == "provinceDestroyed")?.Message
            ?? events.First(e => e.Type == "battleResolved").Message;
        _presentationSnapshot = next with { View = _motionView, Actions = [] };
        _battlePresentationStage = 1; _battlePresentationClock = 0;
        _chosen = null; _selected = null; _expandedUnits.Clear(); _assignments.Clear(); _sources.Clear(); _assignmentUnit = null;
    }

    private void ProcessBattlePresentation(double delta)
    {
        _battleVisualTime += delta;
        if (!PresentationBusy) return;
        if (_session.Snapshot is null) { _battlePresentationStage = 0; _presentationSnapshot = null; return; }
        _battlePresentationClock += delta;
        if (_reducedMotion || _battlePresentationStage == 1 && _battlePresentationClock >= .65)
        {
            if (_battlePresentationStage == 1)
            {
                _presentationSnapshot = null; _battlePresentationStage = 2; _battlePresentationClock = 0;
                Render();
            }
            if (_reducedMotion)
            {
                foreach (var flight in _flights.Where(IsInstanceValid)) flight.Finish();
                _battlePresentationStage = 0; Render(); return;
            }
        }
        if (_battlePresentationStage == 2 && _battlePresentationClock > .85 && TableMotionsSettled())
        {
            _battlePresentationStage = 3; _battlePresentationClock = 0;
            foreach (var key in _fallTimes.Keys.ToArray()) if (_fallTimes[key] == -1) _fallTimes[key] = _battleVisualTime;
        }
        if (_battlePresentationStage == 3 && _battlePresentationClock >= 1.25)
        { _battlePresentationStage = 0; RenderInspector(); RenderPhaseActions(); RenderTimeline(); FrameNewBattle(); }

    }

    private void DrawBattleInk(Control canvas)
    {
        if (_session.Snapshot is null) return;
        var defender = View.Players.Single(p => p.Id != View.ActivePlayerId);
        foreach (var (index, court) in _battleCourts)
        {
            var province = _matTargets.FirstOrDefault(s => s.Id == $"province:{defender.Id}:{index}");
            if (province is null) continue;
            var above = province.Bounds.GetCenter().Y < court.GetCenter().Y;
            var edge = new Vector2(court.GetCenter().X, above ? court.Position.Y : court.End.Y);
            var active = View.CurrentBattleProvince == index;
            var reveal = _matSections.RevealProgress($"battle:{defender.Id}:{index}");
            BattleInkArt.Neck(canvas, edge, above, new Color(active ? .76f : 1, active ? 1.18f : 1, active ? 1.3f : 1, reveal));

        }
        foreach (var (id, province) in _assignments)
        {
            var unit = Self.Home.FirstOrDefault(c => c.Id == id);
            if (unit is null || !_battlePlacements.TryGetValue(id, out var origin)) continue;
            var cards = Self.Home.Where(c => c.AttachedTo == id).Append(unit).ToArray();
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                var tex = card.Definition is { } definition ? CardTile.Artwork(definition) : null;
                if (tex is null) continue;
                var height = CardHeight - 12;
                var face = new Vector2(height * tex.GetWidth() / tex.GetHeight(), height);
                var position = origin + new Vector2(8, i * 24 + 4);
                if (card.Bowed)
                {
                    canvas.DrawSetTransform(position + new Vector2(face.Y, 0), Mathf.Pi / 2);
                    canvas.DrawTextureRect(tex, new Rect2(Vector2.Zero, face), false, new Color(.78f, .87f, .88f, .24f));
                    canvas.DrawSetTransform(Vector2.Zero);
                }
                else canvas.DrawTextureRect(tex, new Rect2(position, face), false, new Color(.78f, .87f, .88f, .24f));
            }
            var footprint = new Rect2(origin, new Vector2(UnitWidth(unit, Self), StackHeight(unit, Self)));
            var corners = new[] { footprint.Position, new Vector2(footprint.End.X, footprint.Position.Y), footprint.End, new Vector2(footprint.Position.X, footprint.End.Y), footprint.Position };
            for (var i = 0; i < 4; i++) canvas.DrawDashedLine(corners[i], corners[i + 1], new Color(.57f, .85f, .90f, .65f), 1.5f, 8, true);
        }
        foreach (var player in View.Players)
        foreach (var province in player.Provinces.Where(p => p.Destroyed))
        {
            var key = (player.Id, province.Index);
            if (!_fallTimes.ContainsKey(key)) _fallTimes[key] = _battlePresentationStage == 2 ? -1 : -10;
            var start = _fallTimes[key];
            var progress = start < -2 || _reducedMotion ? 1 : start < 0 ? 0 : Math.Clamp((float)(_battleVisualTime - start) / 1.15f, 0, 1);
            var width = Math.Min(CardHeight + 8, _laneWidths[province.Index] - 12);
            var rect = new Rect2(SlotX(player, province.Index + 2) + (CardHeight - width) / 2,
                ProvinceRowY(player) + PlaneHeight(CardHeight) + MatPadding, width, 64);
            BattleInkArt.Fallen(canvas, rect, province.Index + 1, progress);
        }
    }
}

public partial class TableBattleInk : Control
{
    public Action<Control>? Paint { get; init; }
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.LinearWithMipmaps; }
    public override void _Process(double delta) => QueueRedraw();
    public int DrawCalls { get; private set; }
    public override void _Draw() { DrawCalls++; Paint?.Invoke(this); }
}
