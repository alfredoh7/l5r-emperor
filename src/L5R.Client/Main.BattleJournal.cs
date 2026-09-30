using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private readonly BattleJournal _journal = new();
    private MatchView? _journalView;
    private string? _journalRoom;
    private BattleDefeatBanner _defeatBanner = null!;
    private readonly Dictionary<int, double> _bloodStarts = [];
    private readonly Dictionary<int, CrimsonCourt> _bloodCourts = [];
    private (int Turn, int Attacker)? _bloodBattle;
    // Keep the table announcement separate from the right-sidebar battle history.
    private void BuildBattleFeedback()
    {
        _defeatBanner = new BattleDefeatBanner(); _root.AddChild(_defeatBanner);
    }
    private void ObserveBattleJournal()
    {
        var snapshot = _session.Snapshot!; var next = _battlefieldReviewView ?? snapshot.View;
        var fresh = _journalView is null || _journalRoom != snapshot.RoomCode || _journalView.ViewerId != next.ViewerId || _journalView.Scenario != next.Scenario || next.Version < _journalView.Version;
        if (fresh)
        {
            _journal.Reset(); _journalView = null; _bloodStarts.Clear(); _bloodBattle = null;
            _staticOutcomeUntil = 0;
            _defeatBanner.Visible = false;
        }
        _journalRoom = snapshot.RoomCode;
        var key = (next.TurnNumber, next.ActivePlayerId);
        if (_bloodBattle != key) { _bloodBattle = key; _bloodStarts.Clear(); }
        // Only engine-confirmed attacking roots activate blood. Drafts and defenders
        // are intentionally absent; activation survives later deaths/returns in this battle.
        foreach (var card in next.Players.Single(p => p.Id == next.ActivePlayerId).Army.Where(c => c.AttachedTo is null && c.ProvinceIndex is not null))
            _bloodStarts.TryAdd(card.ProvinceIndex!.Value, fresh ? -10 : _battleVisualTime);
        _journal.Observe(next, _journalView);
        _journalView = next;
    }
    private void RenderBattleFeedback()
    {
        _defeatBanner.Position = new Vector2(_tableViewport.Position.X + Math.Max(0, (_tableViewport.Size.X - 680) / 2), 180);
        _defeatBanner.Size = new Vector2(Math.Min(680, _tableViewport.Size.X), 112);
        _defeatBanner.Visible = PresentationBusy && _battlePresentationStage == 1 || _battleVisualTime < _staticOutcomeUntil;
        _defeatBanner.Headline = _battleDefeatHeadline;
        _defeatBanner.Cause = _battleDefeatCause;
        _defeatBanner.QueueRedraw();
    }
    private void SyncBloodCourts()
    {
        foreach (var index in _bloodCourts.Keys.ToArray())
            if (!_battleCourts.ContainsKey(index) || !_bloodStarts.ContainsKey(index)) { _bloodCourts[index].QueueFree(); _bloodCourts.Remove(index); }
        foreach (var (index, rect) in _battleCourts)
        {
            if (!_bloodStarts.TryGetValue(index, out var start)) continue;
            var bay = _matTargets.FirstOrDefault(s => s.Id == $"province:{1 - View.ActivePlayerId}:{index}");
            var closed = !CourtConnectsToProvince(index);
            if (!_bloodCourts.TryGetValue(index, out var court))
            { court = new CrimsonCourt(); _battleDecor.AddChild(court); _bloodCourts[index] = court; }
            court.Present(rect, closed || bay is not null && bay.Bounds.GetCenter().Y < rect.GetCenter().Y,
                start < 0 ? 8 + _battleVisualTime : _battleVisualTime - start, _reducedMotion, View.CurrentBattleProvince == index, closed);
        }
    }
}

public partial class BattleDefeatBanner : Control
{
    public string Headline { get; set; } = "";
    public string Cause { get; set; } = "";
    private Label _headline = null!, _cause = null!;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ZIndex = 45; Visible = false;
        _headline = Palette.Wrapped("", 34, new Color("e8b6a0")); _headline.Uppercase = true; _headline.AddThemeFontOverride("font", Palette.Title); _headline.HorizontalAlignment = HorizontalAlignment.Center; AddChild(_headline);
        _cause = Palette.Wrapped("", 17, Palette.PaperDim); _cause.HorizontalAlignment = HorizontalAlignment.Center; AddChild(_cause);
    }
    public override void _Draw()
    {
        DrawTextureRect(BattleEscalationArt.Announcement, new Rect2(Vector2.Zero, Size), false);
        var fontSize = 28;
        while (fontSize > 18 && Palette.Title.GetStringSize(Headline, fontSize: fontSize).X > (Size.X - 80) * 1.85f) fontSize--;
        _headline.AddThemeFontSizeOverride("font_size", fontSize);
        _headline.Text = Headline; _headline.Position = new Vector2(40, 10); _headline.Size = new Vector2(Size.X - 80, 62);
        _cause.Text = Cause; _cause.Position = new Vector2(45, 75); _cause.Size = new Vector2(Size.X - 90, 34);
    }
}
