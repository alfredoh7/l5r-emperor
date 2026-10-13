using Godot;

namespace L5R.Client;

public partial class Main
{
    private HBoxContainer? _valuesReviewBar;
    private int _valuesReviewScenario;
    private OptionButton? _valuesReviewPicker;
    private static readonly string[] ValuesScenarios = ["standard", "bowed", "open", "empty", "crowded", "bonus", "lethal", "armor-off", "armor-on"];

    private void StartBattleValuesReview()
    {
        _autoOpponent = false;
        _valuesReviewBar = new HBoxContainer { Name = "BattleValuesReviewControls", Theme = SidebarSkin.Theme() };
        _valuesReviewBar.AddThemeConstantOverride("separation", 8); _root.AddChild(_valuesReviewBar);
        var picker = _valuesReviewPicker = new OptionButton { CustomMinimumSize = new Vector2(250, 42), ClipText = true };
        foreach (var name in new[] { "Two armies + Follower", "Bowed · Follower ready", "Battle without a province", "Empty armies", "Crowded armies", "Redirect + army bonus", "Attacker force far ahead", "Armor off · exactly short", "Armor on · province falls" }) picker.AddItem(name);
        picker.ItemSelected += i => { _valuesReviewScenario = (int)i; PrepareBattleValuesReview(); };
        _valuesReviewBar.AddChild(picker);
        foreach (var (text, action) in new (string, Action)[]
        {
            ("Other seat", () => { _session.SwitchPracticeSeat(); FocusBattleValues(); }),
            ("View battle", FocusBattleValues),
            ("Toggle armor", ToggleValuesArmor),
            ("Reset", PrepareBattleValuesReview)
        })
        {
            var button = SidebarSkin.Button(text, action); button.CustomMinimumSize = new Vector2(text == "Toggle armor" ? 126 : 100, 42);
            button.AddThemeFontSizeOverride("font_size", 16); _valuesReviewBar.AddChild(button);
        }
        if (OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--battle-values-scenario="))?["--battle-values-scenario=".Length..] is { } chosen && int.TryParse(chosen, out var index) && index >= 0 && index < ValuesScenarios.Length) _valuesReviewScenario = index;
        PrepareBattleValuesReview(); EnterTable(); Callable.From(FocusBattleValues).CallDeferred();
        var capture = _capturePath; _capturePath = null;
        if (capture is not null) Callable.From(() => { _ = CaptureBattleValuesStartup(capture); }).CallDeferred();
        var checks = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--battle-values-checks="))?["--battle-values-checks=".Length..];
        if (checks is not null) Callable.From(() => { _ = CheckBattleValuesReview(checks); }).CallDeferred();
        GD.Print("BATTLE_VALUES_REVIEW_READY: real engine battle; interactive local review.");
    }

    /// <summary>Attach or remove the Light Armor on the Crane Personality; the engine recalculates Force and the Province placard follows.</summary>
    private void ToggleValuesArmor()
    {
        _valuesReviewScenario = ValuesScenarios[_valuesReviewScenario] == "armor-on" ? Array.IndexOf(ValuesScenarios, "armor-off") : Array.IndexOf(ValuesScenarios, "armor-on");
        PrepareBattleValuesReview();
    }

    private void PrepareBattleValuesReview()
    {
        _valuesReviewPicker?.Select(_valuesReviewScenario);
        _selected = null; _chosen = null; _assignments.Clear(); _tableLayoutSeen = false;
        _session.StartBattleValuesReview(ValuesScenarios[_valuesReviewScenario]);
        Render(); FocusBattleValues();
        SetNotice("Prepared battle · select a card or use a legal battle action. Reset restores this scene.");
    }

    private void FocusBattleValues()
    {
        if (_valuesReviewBar is null || !_session.BattleValuesReview || View.CurrentBattleProvince is not int field || !_battleCourts.TryGetValue(field, out var court)) return;
        var region = court.Grow(50);
        if (BattlefieldProvince(field) is int province && _matTargets.FirstOrDefault(s => s.Id == $"province:{1 - View.ActivePlayerId}:{province}") is { } bay)
            region = region.Merge(bay.Bounds.Grow(25));
        _cameraFitted = false; _cameraGlide = false; _cameraZoom = FitRegion(region); _cameraCenter = region.GetCenter(); ApplyTableCamera();
    }

    private void LayoutBattleValuesReview()
    {
        if (_valuesReviewBar is null) return;
        _valuesReviewBar.Position = new Vector2(360, 145);
        _valuesReviewBar.Size = new Vector2(Math.Min(900, _layoutWidth - SidebarWidth - 380), 42);
        _valuesReviewBar.Visible = _session.BattleValuesReview && !MenuVisible;
    }

    private async Task CaptureBattleValuesStartup(string path)
    {
        for (var i = 0; i < 12; i++) await CoverageFrames();
        while (_turnBloom.Playing) await CoverageFrames();
        FocusBattleValues(); await CoverageFrames();
        using var capture = GetViewport().GetTexture().GetImage(); capture.SavePng(path);
    }
}
