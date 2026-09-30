using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // Explicit local presentation fixtures. No room, replay, engine state or legal
    // action is fabricated by these views, and they cannot submit commands.
    private MatchView? _battlefieldReviewView;
    private MatchEngine? _battlefieldReviewSeed;
    private HBoxContainer? _battlefieldReviewBar;
    private OptionButton? _battlefieldReviewPicker;
    private int _battlefieldReviewScenario, _battlefieldReviewSeat;
    private long _battlefieldReviewVersion = 5000;
    private static readonly string[] BattlefieldReviewScenarios =
    ["Open battlefield", "Province and open battlefield", "Two open battlefields", "Province destroyed before engagement",
     "Two fronts at one province", "Six extra battlefields", "Empty battlefield", "Crowded armies and attachments", "Battle during the Action Phase"];

    private void StartOpenBattlefieldReview()
    {
        _autoOpponent = false;
        _battlefieldReviewSeed = MatchEngine.CreatePublishedMatch(91);
        _session.StartPractice(false, true);
        _battlefieldReviewBar = new HBoxContainer { Theme = SidebarSkin.Theme() };
        _battlefieldReviewBar.AddThemeConstantOverride("separation", 8);
        _root.AddChild(_battlefieldReviewBar);
        var picker = new OptionButton { CustomMinimumSize = new Vector2(400, 46) };
        _battlefieldReviewPicker = picker;
        foreach (var name in BattlefieldReviewScenarios) picker.AddItem(name);
        picker.ItemSelected += i => { _battlefieldReviewScenario = (int)i; PrepareOpenBattlefieldReview(); };
        _battlefieldReviewBar.AddChild(picker);
        foreach (var (name, callback) in new (string, Action)[]
        {
            ("Other seat", () => { _battlefieldReviewSeat = 1 - _battlefieldReviewSeat; PrepareOpenBattlefieldReview(); }),
            ("View courts", FocusReviewBattlefields),
            ("Remove province", RemoveReviewProvince)
        })
        {
            var button = SidebarSkin.Button(name, callback);
            button.CustomMinimumSize = new Vector2(140, 46);
            button.AddThemeFontSizeOverride("font_size", 17);
            button.AutowrapMode = TextServer.AutowrapMode.Off;
            _battlefieldReviewBar.AddChild(button);
        }
        PrepareOpenBattlefieldReview(); EnterTable();
        Callable.From(FocusReviewBattlefields).CallDeferred();
        var output = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--open-battlefield-checks="))?["--open-battlefield-checks=".Length..];
        if (output is not null) Callable.From(() => { _ = CheckOpenBattlefieldReview(output); }).CallDeferred();
        GD.Print("OPEN_BATTLEFIELD_REVIEW_READY: interactive presentation fixtures; engine commands disabled.");
    }

    private void PrepareOpenBattlefieldReview()
    {
        CloseBattlefieldHelp();
        _battlefieldReviewPicker?.Select(_battlefieldReviewScenario);
        var snapshot = _battlefieldReviewSeed!.GetView(_battlefieldReviewSeat);
        int[] fields = _battlefieldReviewScenario switch
        {
            1 or 4 => [1, 100],
            2 => [100, 101],
            3 => [1],
            5 => [100, 101, 102, 103, 104, 105],
            _ => [100]
        };
        var definitions = CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Type == CardType.Personality && c.ArtworkKey is not null).ToArray();
        var crane = definitions.First(c => c.Name.Contains("Hakuseki", StringComparison.Ordinal));
        var lion = definitions.First(c => c.Name == "Ikoma Shika");
        var second = definitions.First(c => c.Name == "Doji Dainagon");
        var follower = CardCatalog.Cards.Values.First(c => !c.ScenarioOnly && c.Type == CardType.Follower && c.ArtworkKey is not null);
        CardView Face(string id, CardDefinition definition, int owner, int field, bool bowed = false, string? attached = null) =>
            new(id, definition, owner, Zone.Battlefield, true, bowed, field, attached, definition.Force, definition.Chi,
                UnitForce: attached is null ? definition.Force : null);
        var players = snapshot.Players.Select(player =>
        {
            var army = new List<CardView>();
            if (_battlefieldReviewScenario != 6)
                foreach (var field in fields)
                {
                    var id = $"open-review-{player.Id}-{field}";
                    army.Add(Face(id, player.Id == 0 ? crane : lion, player.Id, field));
                    if (player.Id == 1) army.Add(Face(id + "-attachment", follower, player.Id, field, attached: id));
                    if (player.Id == 0 || _battlefieldReviewScenario == 7)
                        army.Add(Face(id + "-second", second, player.Id, field, bowed: _battlefieldReviewScenario == 7));
                    if (_battlefieldReviewScenario == 7)
                        army.Add(Face(id + "-third", player.Id == 0 ? crane : lion, player.Id, field));
                }
            return player with
            {
                Army = army.ToArray(),
                Home = player.Home.Where(c => c.Definition?.Type == CardType.Holding).ToArray(),
                Hand = [],
                HandCount = 0,
                Provinces = player.Provinces.Select(p => _battlefieldReviewScenario == 3 && player.Id == 1 && p.Index == 1
                    ? p with { Destroyed = true, Card = null, DynastySlots = null, Region = null } : p).ToArray()
            };
        }).ToArray();
        _battlefieldReviewView = snapshot with
        {
            Scenario = "open-battlefield-ui-review",
            Version = ++_battlefieldReviewVersion,
            Players = players,
            Phase = Phase.Battle,
            ActivePlayerId = 0,
            PriorityPlayerId = _battlefieldReviewSeat,
            CurrentBattleProvince = fields.Contains(100) ? 100 : 1,
            UnresolvedBattlefields = fields,
            Battlefields = fields.Select(i => new BattlefieldView(i,
                _battlefieldReviewScenario == 4 || i == 1 && _battlefieldReviewScenario != 3 ? 1 : null)).ToArray(),
            PendingChoice = null,
            PendingProvinceOrder = null,
            ActionPhaseTurnPlayerId = _battlefieldReviewScenario == 8 ? 1 : null,
            Events = []
        };
        _selected = null; _chosen = null; _assignments.Clear(); _motionView = null; _tableLayoutSeen = false;
        _journalView = null; _battlePresentationStage = 0; _presentationSnapshot = null;
        Render(); FocusReviewBattlefields();
        SetNotice("UI review · " + BattlefieldReviewScenarios[_battlefieldReviewScenario] + " · card effects are not running.");
    }

    private void RemoveReviewProvince()
    {
        if (_battlefieldReviewView is null || !View.Battlefields!.Any(b => b.Province == 1)) return;
        CloseBattlefieldHelp();
        _battlefieldReviewView = View with
        {
            Version = ++_battlefieldReviewVersion,
            Players = View.Players.Select(p => p.Id != 1 ? p : p with
            {
                Provinces = p.Provinces.Select(v => v.Index != 1 ? v : v with
                { Destroyed = true, Card = null, DynastySlots = null, Region = null }).ToArray()
            }).ToArray(),
            Battlefields = View.Battlefields!.Select(b => b.Province == 1 ? b with { Province = null } : b).ToArray()
        };
        Render();
        SetNotice("UI review · province removed; its battlefields stay in place.");
    }

    private void FocusReviewBattlefields()
    {
        if (_battlefieldReviewView is null || _battleCourts.Count == 0) return;
        var region = _battleCourts.Values.Aggregate((a, b) => a.Merge(b)).Grow(60);
        foreach (var index in _battleCourts.Keys.Where(CourtConnectsToProvince))
            if (_matTargets.FirstOrDefault(s => s.Id == $"province:1:{index}") is { } bay)
                region = region.Merge(bay.Bounds.Grow(25));
        _cameraFitted = false; _cameraGlide = false;
        _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter(); ApplyTableCamera();
    }

    private void LayoutOpenBattlefieldReview()
    {
        if (_battlefieldReviewBar is null) return;
        _battlefieldReviewBar.Position = new Vector2(360, 145);
        _battlefieldReviewBar.Size = new Vector2(Math.Min(960, _layoutWidth - SidebarWidth - 380), 46);
    }

    private void ClearOpenBattlefieldReview()
    {
        CloseBattlefieldHelp();
        if (_battlefieldReviewView is null) return;
        _battlefieldReviewView = null; _battlefieldReviewSeed = null; _battlefieldReviewPicker = null;
        _battlefieldReviewBar?.QueueFree(); _battlefieldReviewBar = null;
        _motionView = null; _journalView = null; _bloodStarts.Clear(); _bloodBattle = null;
        _selected = null; _tableLayoutSeen = false; _cameraFitted = true;
    }

    private async Task CaptureOpenBattlefieldStartup(string path)
    {
        for (var i = 0; i < 12; i++) await CoverageFrames();
        if (_battlefieldReviewView is null) return;
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(path);
    }
}
