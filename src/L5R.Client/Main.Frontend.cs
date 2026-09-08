using Godot;
namespace L5R.Client;

public partial class Main
{
    private TitleMenu? _titleMenu;
    private MenuPreferences _preferences = null!;
    private bool _enteringPrivate;
    private string _preferencesProfile = "default";
    private bool MenuVisible => _titleMenu?.Visible == true;
    private void InitializeFrontend()
    {
        _preferencesProfile = _capturePath is null ? _profile : "menu-capture-" + Guid.NewGuid().ToString("N")[..12];
        _preferences = new MenuPreferences(_preferencesProfile);
        // Screenshot fixtures stay independent of the user's settings.
        if (_capturePath is null) _preferences.Load();
        ApplyPreferences(_capturePath is null);
        var layer = new CanvasLayer { Layer = 20 }; AddChild(layer);
        _titleMenu = new TitleMenu
        {
            Preferences = _preferences,
            StartMatch = StartFromMenu,
            PrivateMatch = () => { _enteringPrivate = true; ShowOnline(); },
            Library = () => LibraryDialog.Show(this),
            Resume = EnterTable,
            CanResume = () => _session.Snapshot is not null,
            IsOnline = () => _session.Online,
            PreferencesChanged = () => ApplyPreferences(true)
        };
        layer.AddChild(_titleMenu);
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += () => { ShowMainMenu(); _titleMenu.ShowQuit(); };
        if (_capturePath is null || _captureMode.StartsWith("menu", StringComparison.Ordinal)) ShowMainMenu();
        else _titleMenu.Hide();
    }
    private void ApplyPreferences(bool window)
    {
        _session.LocalDisplayName = _preferences.DisplayName;
        _reducedMotion = _preferences.ReducedMotion;
        _table.ReducedMotion = _reducedMotion; _timeline.ReducedMotion = _reducedMotion;
        foreach (var tile in _tiles) tile.ReducedMotion = _reducedMotion;
        if (_reducedMotion)
        {
            foreach (var flight in _flights.Where(IsInstanceValid)) flight.Finish();
            _flights.Clear(); foreach (var pile in TablePiles) pile.SettleFrom(pile.Count, true);
        }
        if (window)
        {
            GetWindow().Mode = _preferences.Fullscreen ? Window.ModeEnum.Fullscreen : Window.ModeEnum.Windowed;
            DisplayServer.WindowSetVsyncMode(_preferences.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        }
    }
    private void ShowMainMenu()
    {
        if (_titleMenu is null) return;
        _cameraDragging = false; HideTableTooltip(); ReleaseTableFocus();
        _root.Hide(); _table.Hide(); _table.SetProcess(false);
        _titleMenu.Show(); _titleMenu.ShowMain();
        _opponentClock = 0;
    }
    private void ShowConcede()
    {
        if (_session.Snapshot is null || View.WinnerId is not null) return;
        Confirm("Concede this match?", "The other player will win the current match.", () => Submit(new L5R.Core.GameCommand("concede", View.ViewerId, View.Version)));
    }
    private void ShowFrontendOptions() { ShowMainMenu(); _titleMenu!.ShowOptions(); }
    private void EnterTable()
    {
        if (_session.Snapshot is null) return;
        _enteringPrivate = false;
        _titleMenu?.Hide(); _root.Show(); _table.Show(); _table.SetProcess(true);
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
        _opponentClock = 0; Render();
    }
    private void StartFromMenu(int seat)
    {
        ResetStatHistory();
        _enteringPrivate = false; _tableLayoutSeen = false; _cameraFitted = true;
        _selected = null; _chosen = null; _expandedUnits.Clear(); _sources.Clear(); _assignments.Clear();
        _session.StartPractice(false, true, seat); EnterTable();
    }
}
