using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private MatchView? _provinceReviewView;
    private MatchEngine? _provinceReviewSeed;
    private HBoxContainer? _provinceReviewBar;
    private OptionButton? _provinceReviewPicker;
    private int _provinceReviewScenario, _provinceReviewSeat;
    private long _provinceReviewRevision = 1000;
    private string? _provinceReviewConfirmed;
    private static readonly string[] ProvinceReviewScenarios =
    ["Two visible Dynasty cards", "Second slot face down", "Choose reveal order", "Choose refill order", "Empty second slot", "Crowded province row", "Dark Experiments mark"];

    private void StartProvinceReview()
    {
        _autoOpponent = false;
        _provinceReviewSeed = MatchEngine.CreatePublishedMatch(91);
        _session.StartPractice(false, true);
        _provinceReviewBar = new HBoxContainer { Theme = SidebarSkin.Theme(), Position = new Vector2(370, 150), Size = new Vector2(940, 60) };
        _provinceReviewBar.AddThemeConstantOverride("separation", 10);
        _root.AddChild(_provinceReviewBar);
        var picker = new OptionButton { CustomMinimumSize = new Vector2(460, 54) };
        _provinceReviewPicker = picker;
        foreach (var name in ProvinceReviewScenarios) picker.AddItem(name);
        picker.ItemSelected += index => { _provinceReviewScenario = (int)index; PrepareProvinceReview(); };
        _provinceReviewBar.AddChild(picker);
        var seat = SidebarSkin.Button("Other seat", () => { _provinceReviewSeat = 1 - _provinceReviewSeat; PrepareProvinceReview(); });
        var focus = SidebarSkin.Button("Focus province", FocusProvinceReview);
        foreach (var button in new[] { seat, focus })
        {
            button.CustomMinimumSize = new Vector2(190, 60); button.AutowrapMode = TextServer.AutowrapMode.Off;
            _provinceReviewBar.AddChild(button);
        }
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--province-review-scenario=")) is { } chosen && int.TryParse(chosen[27..], out var scenario) && scenario >= 0 && scenario < ProvinceReviewScenarios.Length) _provinceReviewScenario = scenario;
        if (OS.GetCmdlineUserArgs().Contains("--province-review-seat=1")) _provinceReviewSeat = 1;
        PrepareProvinceReview(); EnterTable();
        Callable.From(FocusProvinceReview).CallDeferred();
        var output = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--province-review-checks="))?[25..];
        if (output is not null) Callable.From(() => { _ = CheckProvinceReview(output); }).CallDeferred();
        GD.Print("PROVINCE_REVIEW_READY: isolated presentation fixtures; no Plains rules executed.");
    }

    private void PrepareProvinceReview()
    {
        _provinceReviewPicker?.Select(_provinceReviewScenario);
        var snapshot = _provinceReviewSeed!.GetView(_provinceReviewSeat);
        var visible = CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Type == CardType.Personality && c.ArtworkKey is not null).ToArray();
        var first = visible.First(c => c.Name.Contains("Hakuseki", StringComparison.Ordinal));
        var second = visible.First(c => c.Name == "Doji Dainagon");
        var regionDefinition = new CardDefinition("preview-plains", "Plains of Otosan Uchi", CardType.Region,
            ScenarioOnly: false, ArtworkKey: "res://Art/ProvinceReview/plains-of-otosan-uchi-ee.webp",
            Text: "Presentation fixture only. The scan is the Emperor Edition printing; this preview does not execute its card effects.");
        CardView Face(string id, CardDefinition def, int owner, int province, Zone zone = Zone.Province) =>
            new(id, def, owner, zone, true, false, province, null, def.Force, def.Chi);
        CardView Hidden(int owner, int province, int slot) => new($"hidden-p{owner}-province{province}-slot{slot}", null, owner, Zone.Province, false, false, province, null, 0);
        var players = snapshot.Players.Select(player => player with
        {
            Home = player.Home.Where(c => c.Definition?.Type == CardType.Holding).ToArray(),
            Army = [],
            Provinces = player.Provinces.Select(province =>
            {
                // Dark Experiments mark: province 2 is marked (-2 strength), province 3 was marked and then destroyed (no marker), province 4 is untouched.
                if (_provinceReviewScenario == 6 && province.Index == 3) return province with { Destroyed = true, Card = null, Region = null, DynastySlots = null, DarkExperimentsMarked = true };
                if (province.Index != 1 && _provinceReviewScenario != 5) return province;
                var a = Face($"preview-{player.Id}-{province.Index}-a", first, player.Id, province.Index);
                CardView? b = _provinceReviewScenario switch
                {
                    1 or 2 or 3 => Hidden(player.Id, province.Index, 1),
                    4 or 6 => null,
                    _ => Face($"preview-{player.Id}-{province.Index}-b", second, player.Id, province.Index)
                };
                if (_provinceReviewScenario is 2 or 3) a = Hidden(player.Id, province.Index, 0);
                return province with
                {
                    DarkExperimentsMarked = _provinceReviewScenario == 6,
                    Strength = _provinceReviewScenario == 6 ? province.Strength + DarkExperimentsMark.Strength : province.Strength,
                    StrengthModifier = _provinceReviewScenario == 6 ? DarkExperimentsMark.Strength : province.StrengthModifier,
                    Card = a,
                    Region = _provinceReviewScenario == 6 ? null : Face($"preview-region-{player.Id}-{province.Index}", regionDefinition, player.Id, province.Index, Zone.Region),
                    DynastySlots = _provinceReviewScenario == 6 ? null : [new(0, a), new(1, b)]
                };
            }).ToArray()
        }).ToArray();
        var revision = ++_provinceReviewRevision;
        ProvinceOrderView? order = null;
        if (_provinceReviewScenario is 2 or 3)
            order = new("preview-order-" + revision, 0, 0, 1,
                _provinceReviewScenario == 2 ? "Choose which Dynasty slot to reveal first." : "Choose which Dynasty slot to refill first.",
                _provinceReviewSeat != 0 ? [] :
                [new("a-b", "A first · B second", [0,1], new GameCommand("previewOnly", 0, revision, Choice: "a-b")),
                 new("b-a", "B first · A second", [1,0], new GameCommand("previewOnly", 0, revision, Choice: "b-a"))]);
        _provinceReviewView = snapshot with
        {
            Version = revision,
            Scenario = "province-presentation-preview",
            Players = players,
            Phase = Phase.Dynasty,
            PendingChoice = null,
            PriorityPlayerId = 0,
            ActivePlayerId = 0,
            Events = [],
            PendingProvinceOrder = order
        };
        _provinceReviewConfirmed = null; _selected = null; _chosen = null; _motionView = null; _tableLayoutSeen = false;
        Render(); FocusProvinceReview();
        SetNotice("Presentation preview · " + ProvinceReviewScenarios[_provinceReviewScenario] + " · card effects are not running.");
    }

    private void FocusProvinceReview()
    {
        if (_provinceReviewView is null || MenuVisible) return;
        var owner = View.Players.Single(p => p.Id == (View.PendingProvinceOrder?.OwnerId ?? View.ViewerId));
        var bounds = _matTargets.Single(s => s.Id == $"province:{owner.Id}:1").Bounds;
        _cameraFitted = false; _cameraGlide = false;
        _cameraZoom = Math.Min(1.7f, FitRegion(new Rect2(bounds.Position - new Vector2(155, 65), bounds.Size + new Vector2(310, 120))));
        _cameraCenter = bounds.GetCenter();
        if (View.PendingProvinceOrder?.PlayerId == View.ViewerId) _cameraCenter.Y += 190 / _cameraZoom;
        ApplyTableCamera();
    }

    private void LayoutProvinceReview()
    {
        LayoutProvinceOrder();
        if (_provinceReviewBar is null) return;
        _provinceReviewBar.Position = new Vector2(370, 150);
        _provinceReviewBar.Size = new Vector2(Math.Min(940, _layoutWidth - SidebarWidth - 400), 54);
    }

    private void ClearProvinceReview()
    {
        if (_provinceReviewView is null) return;
        _provinceReviewView = null; _provinceReviewSeed = null; _motionView = null;
        _provinceReviewPicker = null;
        if (_provinceReviewBar is not null) { _provinceReviewBar.GetParent().RemoveChild(_provinceReviewBar); _provinceReviewBar.QueueFree(); _provinceReviewBar = null; }
        if (_provinceOrderTray is not null) { _provinceOrderTray.GetParent().RemoveChild(_provinceOrderTray); _provinceOrderTray.QueueFree(); _provinceOrderTray = null; }
        _selected = null; _tableLayoutSeen = false; _cameraFitted = true;
    }

    private async Task CaptureProvinceStartup(string path)
    {
        for (var i = 0; i < 12; i++) await CoverageFrames();
        if (_provinceReviewView is null) return;
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(path);
    }
}
