using Godot;
using System.Text.Json;
namespace L5R.Client;

public partial class Main
{
    private int _menuProbeStep;
    private long _menuProbeVersion;
    private void MenuAssert(bool condition, string message) { if (!condition) throw new InvalidOperationException("MENU_CHECK: " + message); GD.Print("MENU_CHECK: " + message); }
    private static void Click(Button button) => button.EmitSignal(BaseButton.SignalName.Pressed);
    private void ProcessMenuCapture(double delta)
    {
        _captureClock += delta;
        if (_captureClock < .7) return;
        var menu = _titleMenu!;
        if (_menuProbeStep == 0)
        {
            MenuAssert(_session.Snapshot is null && MenuVisible, "startup has no match or hidden opponent turns");
            if (_captureMode == "menu-options") menu.ShowOptions();
            if (_captureMode == "menu-play") menu.ShowPlay();
            if (_captureMode == "menu-library") LibraryDialog.Show(this);
            if (_captureMode == "menu-private")
            {
                _enteringPrivate = true; ShowOnline();
                var dialog = GetChildren().OfType<AcceptDialog>().Last();
                var body = dialog.GetChildren().OfType<VBoxContainer>().Single();
                body.GetChildren().OfType<LineEdit>().First().Text = "invalid-server";
                Click(body.GetChildren().OfType<Button>().First(b => b.Text == "Enter private match"));
                MenuAssert(MenuVisible && _session.Snapshot is null, "invalid private server keeps the player in the menu without starting practice");
            }
            if (_captureMode.Contains("reduced")) { _preferences.ReducedMotion = true; ApplyPreferences(false); }
            if (_captureMode.EndsWith("later", StringComparison.Ordinal)) menu.ReviewTime = 22;
            else menu.ReviewTime = 0;
            _menuProbeStep++;
        }
        if (_captureMode == "menu-film")
        {
            if (_filmFrames >= 80) { GetTree().Quit(); return; }
            Directory.CreateDirectory(_capturePath!);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_capturePath!, $"{_filmFrames:0000}.png"));
            menu.ReviewTime = ++_filmFrames / 8f;
            return;
        }
        if (_captureMode == "menu-input")
        {
            void KeyEvent(Key key) { Input.ParseInputEvent(new InputEventKey { Keycode=key, PhysicalKeycode=key, Pressed=true }); Input.ParseInputEvent(new InputEventKey { Keycode=key, PhysicalKeycode=key, Pressed=false }); }
            void MouseClick(Control control) { var point=control.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position=point, GlobalPosition=point }); Input.ParseInputEvent(new InputEventMouseButton { Position=point, GlobalPosition=point, ButtonIndex=MouseButton.Left, Pressed=true }); Input.ParseInputEvent(new InputEventMouseButton { Position=point, GlobalPosition=point, ButtonIndex=MouseButton.Left, Pressed=false }); }
            if (_menuProbeStep == 1) { KeyEvent(Key.Enter); _menuProbeStep++; _captureClock=0; return; }
            if (_menuProbeStep == 2) { MenuAssert(menu.Page == "play", "native Enter activates focused Play"); KeyEvent(Key.Escape); _menuProbeStep++; _captureClock=0; return; }
            if (_menuProbeStep == 3) { MenuAssert(menu.Page == "main", "native Escape returns from clan selection"); MouseClick(menu.Entries.GetChild<Control>(3)); _menuProbeStep++; _captureClock=0; return; }
            if (_menuProbeStep == 4) { MenuAssert(menu.Page == "options", "native mouse hits Options at rendered coordinates"); MouseClick(menu.Entries.GetChildren().OfType<CheckBox>().Last()); _menuProbeStep++; _captureClock=0; return; }
            if (_menuProbeStep == 5) { MenuAssert(_reducedMotion, "native mouse toggles reduced motion"); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_preferences.Path)); KeyEvent(Key.F11); _menuProbeStep++; _captureClock=0; return; }
            if (_menuProbeStep == 6) { MenuAssert(IsFullscreen, "native F11 enters fullscreen"); KeyEvent(Key.F11); _menuProbeStep++; _captureClock=0; return; }
            if (_menuProbeStep == 7) { MenuAssert(!IsFullscreen, "native F11 returns to windowed play"); KeyEvent(Key.Escape); _menuProbeStep++; _captureClock=0; return; }
            if (_menuProbeStep == 8) { MenuAssert(menu.Page == "main", "Options returns to main menu"); KeyEvent(Key.Escape); _menuProbeStep++; _captureClock=0; return; }
            if (_menuProbeStep == 9) { MenuAssert(menu.Page == "quit", "quit requires confirmation"); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_preferences.Path)); _menuProbeStep++; }
        }
        if (_captureMode == "menu-flow")
        {
            if (_menuProbeStep == 1)
            {
                Click(menu.Entries.GetChild<Button>(0)); MenuAssert(menu.Page == "play", "Play opens clan selection");
                Click(menu.Entries.GetChild<Button>(1)); MenuAssert(!MenuVisible && Self.Clan == "Lion", "Lion selection starts the real published match");
                _menuProbeVersion = View.Version; ShowMainMenu(); _captureClock = 0; _menuProbeStep++; return;
            }
            if (_menuProbeStep == 2)
            {
                MenuAssert(View.Version == _menuProbeVersion, "practice stays paused behind main menu");
                menu.ShowPlay(); Click(menu.Entries.GetChild<Button>(0)); MenuAssert(!MenuVisible && View.Version == _menuProbeVersion, "return resumes unchanged practice");
                ShowMainMenu(); menu.ShowPlay(); Click(menu.Entries.GetChild<Button>(1)); MenuAssert(menu.Page == "replace", "replacing an existing match requires confirmation");
                Click(menu.Entries.GetChildren().OfType<Button>().Last()); MenuAssert(menu.Page == "play", "cancel replacement keeps current match");
                menu.ShowOptions();
                var motion = menu.Entries.GetChildren().OfType<CheckBox>().Last(); motion.ButtonPressed = true;
                MenuAssert(_reducedMotion && _preferences.ReducedMotion, "reduced motion applies to game and menu");
                var loaded = new MenuPreferences(_preferencesProfile); loaded.Load(); MenuAssert(loaded.ReducedMotion, "options persist through a fresh settings instance");
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(loaded.Path));
                _menuProbeStep++;
            }
        }
        if (_captureClock < 1.3) return;
        var path = _capturePath!;
        GetViewport().GetTexture().GetImage().SavePng(path);
        File.WriteAllText(path+".json", JsonSerializer.Serialize(new { Page=menu.Page, MenuVisible, MatchExists=_session.Snapshot is not null, Preferences=_preferences.ReducedMotion, Resolution=GetViewport().GetVisibleRect().Size.ToString() }));
        GD.Print("MENU_CAPTURE: " + path); GetTree().Quit();
    }
}
