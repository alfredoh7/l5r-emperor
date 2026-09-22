using Godot;
namespace L5R.Client;

/// <summary>Live navigation over the approved moonlit plate; no game state lives here.</summary>
public partial class TitleMenu : Control
{
    public Action PracticeSetup = () => { };
    public Action<int> StartMatch = _ => { };
    public Action PrivateMatch = () => { }, Library = () => { }, DeckBuilder = () => { }, Resume = () => { }, PreferencesChanged = () => { };
    public Func<bool> CanResume = () => false;
    public Func<bool> IsOnline = () => false;
    public MenuPreferences Preferences = null!;
    public string Page { get; private set; } = "main";
    public ShaderMaterial AtmosphereMaterial { get; private set; } = null!;
    public Control Canvas { get; private set; } = null!;
    public VBoxContainer Entries { get; private set; } = null!;
    private Label _message = null!;
    private float _time;
    public float? ReviewTime;
    public override void _Ready()
    {
        Theme = Palette.MakeTheme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var black = new ColorRect { Color = new Color("05090e"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(black); black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Canvas = new Control { Size = new Vector2(1672, 941) }; AddChild(Canvas);
        AtmosphereMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/moonlit-menu.gdshader") };
        Canvas.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://Art/moonlit-menu.png"), Size = Canvas.Size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Material = AtmosphereMaterial, MouseFilter = MouseFilterEnum.Ignore });
        Entries = new VBoxContainer { Position = new Vector2(96, 333), Size = new Vector2(525, 500) };
        Entries.AddThemeConstantOverride("separation", 14); Canvas.AddChild(Entries);
        _message = Palette.Wrapped("", 20, Palette.PaperDim); _message.Position = new Vector2(96, 855); _message.Size = new Vector2(525, 52); Canvas.AddChild(_message);
        var version = Palette.Text("Emperor · 0.3.10", 17, Palette.Gold); version.Position = new Vector2(40, 913); Canvas.AddChild(version);
        Resized += Fit; Fit(); ShowMain();
    }
    private void Fit()
    {
        if (Canvas is null) return;
        var scale = Math.Min(Size.X / 1672f, Size.Y / 941f);
        Canvas.Scale = Vector2.One * scale; Canvas.Position = (Size - Canvas.Size * scale) / 2;
    }
    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (!Preferences.ReducedMotion) _time += (float)delta;
        AtmosphereMaterial.SetShaderParameter("clock", ReviewTime ?? _time);
        AtmosphereMaterial.SetShaderParameter("atmosphere", Preferences.Atmosphere);
        AtmosphereMaterial.SetShaderParameter("motion", Preferences.ReducedMotion ? 0f : 1f);
    }
    private void Clear(string page)
    {
        Page = page; Palette.Clear(Entries); Entries.AddThemeConstantOverride("separation", page == "options" ? 8 : 14); _message.Text = "";
    }
    private Button Entry(string text, Action action, int fontSize = 80)
    {
        var button = new MenuEntry { Text = text.ToUpperInvariant(), ReducedMotion = () => Preferences.ReducedMotion, CustomMinimumSize = new Vector2(0, 82), Alignment = HorizontalAlignment.Left, MouseDefaultCursorShape = CursorShape.PointingHand };
        // This face has generous vertical metrics. Fit its full line box, not just
        // the glyph width, so five menu entries stay inside the artwork.
        while (fontSize > 20 && (Palette.Title.GetHeight(fontSize) > 82 || Palette.Title.GetStringSize(button.Text, fontSize: fontSize).X > 490)) fontSize--;
        button.AddThemeFontOverride("font", Palette.Title); button.AddThemeFontSizeOverride("font_size", fontSize);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" }) button.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = 8 });
        button.AddThemeColorOverride("font_color", new Color("dfd1af"));
        button.AddThemeColorOverride("font_hover_color", new Color("ffe0a0"));
        button.AddThemeColorOverride("font_focus_color", new Color("f8d590"));
        button.Pressed += action; Entries.AddChild(button); return button;
    }
    private void FocusFirst() { if (Entries.GetChildren().OfType<BaseButton>().FirstOrDefault() is Control c) Callable.From(() => { if (IsInstanceValid(c) && c.IsInsideTree() && c.IsVisibleInTree()) c.GrabFocus(); }).CallDeferred(); }
    public void Notify(string message) => _message.Text = message;
    public void ShowMain()
    {
        Clear("main");
        Entries.AddThemeConstantOverride("separation", 4);
        Entry("Play", ShowPlay);
        Entry("Private Match", PrivateMatch);
        Entry("Card Library", Library);
        Entry("Deck Builder", DeckBuilder);
        Entry("Options", ShowOptions);
        Entry("Quit", ShowQuit);
        foreach(var button in Entries.GetChildren().OfType<Button>())
        {
            button.CustomMinimumSize=new Vector2(0,72);
            var size=66;
            while(size>20&&(Palette.Title.GetHeight(size)>72||Palette.Title.GetStringSize(button.Text,fontSize:size).X>490))size--;
            button.AddThemeFontSizeOverride("font_size",size);
        }
        if (CanResume()) _message.Text = IsOnline() ? "Your private table continues online. Play → Return to table." : "Your practice is paused. Play → Return to table.";
        FocusFirst();
    }
    public void ShowPlay()
    {
        Clear("play");
        if (CanResume()) Entry("Return to table", Resume, 44);
        Entry("Practice", PracticeSetup);
        Entry("Back", ShowMain, 43);
        _message.Text = "Choose saved decks or presets for a match against the practice opponent.";
        FocusFirst();
    }
    private void Begin(int seat)
    {
        if (!CanResume()) { ShowName(seat); return; }
        Clear("replace");
        Entries.AddChild(Palette.Wrapped(IsOnline() ? "Leave your private table and start a new practice? This disconnects your seat; it does not concede." : "Replace the current practice? Unsaved progress will be lost.", 25));
        Entry("Start new practice", () => ShowName(seat), 39);
        Entry("Back", ShowPlay, 43); FocusFirst();
    }
    public void ShowName(int seat)
    {
        Clear("name");
        Entries.AddChild(Palette.Text("Take your seat", 48, Palette.Gold, true));
        Entries.AddChild(Palette.Wrapped(seat == 0 ? "Crane · Shinden Asahina" : "Lion · Halls of Memory", 25, Palette.PaperDim));
        Entries.AddChild(Palette.Text("Your display name", 24));
        var name = new LineEdit { Text = Preferences.DisplayName, PlaceholderText = "Enter your name", MaxLength = 24, CustomMinimumSize = new Vector2(0, 60), TooltipText = "Up to 24 characters. Remembered on this device; no account or login." };
        name.AddThemeFontSizeOverride("font_size", 30);
        name.AddThemeStyleboxOverride("normal", Palette.Box(new Color("10181d"),new Color("796442"),12,2));
        name.AddThemeStyleboxOverride("focus", Palette.Box(Colors.Transparent,Palette.Gold,12,2));
        Entries.AddChild(name);
        Entries.AddChild(Palette.Wrapped("Remembered on this device. No account or login.", 21, Palette.PaperDim));
        void Start()
        {
            var clean = MenuPreferences.CleanName(name.Text);
            if (clean.Length == 0) { Notify("Enter a name to take your seat."); name.GrabFocus(); return; }
            Preferences.DisplayName = clean;
            if (Preferences.Save() != Error.Ok) { Notify("Your name could not be saved. Check local storage and try again."); return; }
            PreferencesChanged(); StartMatch(seat);
        }
        var play = Entry("Begin match", Start, 48);
        play.Disabled = MenuPreferences.CleanName(name.Text).Length == 0;
        name.TextChanged += value => play.Disabled = MenuPreferences.CleanName(value).Length == 0;
        name.TextSubmitted += _ => Start();
        Entry("Back", ShowPlay, 40);
        Callable.From(() => { if (IsInstanceValid(name) && name.IsInsideTree() && name.IsVisibleInTree()) name.GrabFocus(); }).CallDeferred();
    }
    public void ShowQuit()
    {
        Clear("quit");
        Entries.AddChild(Palette.Wrapped("Quit the game? Local practice progress is not saved between launches.", 26));
        Entry("Quit game", () => GetTree().Quit(), 45); Entry("Back", ShowMain, 43); FocusFirst();
    }
    public void ShowOptions()
    {
        Clear("options");
        var heading = Palette.Text("Options", 34, Palette.Gold, true); Entries.AddChild(heading);
        var display = Palette.Button(Preferences.Fullscreen ? "Display: Fullscreen" : "Display: Windowed", () => { });
        display.Pressed += () => { Preferences.Fullscreen = !Preferences.Fullscreen; Changed(); display.Text = Preferences.Fullscreen ? "Display: Fullscreen" : "Display: Windowed"; };
        StyleOption(display); display.CustomMinimumSize = new Vector2(0, 48); display.AddThemeFontSizeOverride("font_size", 24); Entries.AddChild(display);
        Toggle("VSync", Preferences.VSync, value => Preferences.VSync = value);
        Toggle("Reduced motion", Preferences.ReducedMotion, value => Preferences.ReducedMotion = value);
        var strength = Palette.Text($"Atmosphere intensity · {Preferences.Atmosphere:P0}", 24); Entries.AddChild(strength);
        var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = .05, Value = Preferences.Atmosphere, CustomMinimumSize = new Vector2(0, 34), TooltipText = "Strength of lantern, moonlight, wind and distant window effects." }; Entries.AddChild(slider);
        slider.ValueChanged += value => { Preferences.Atmosphere = (float)value; strength.Text = $"Atmosphere intensity · {Preferences.Atmosphere:P0}"; Changed(); };
        Entries.AddChild(Palette.Wrapped("F11: fullscreen · Arrows / Tab: navigate · Enter: select\nReduced motion freezes the atmosphere and makes card motion instant.", 22, Palette.PaperDim));
        Entry("Back", ShowMain, 48).CustomMinimumSize = new Vector2(0, 60);
        _message.Text = "Changes apply immediately and are saved on this device.";
        display.CallDeferred(Control.MethodName.GrabFocus);
    }
    private void Toggle(string text, bool value, Action<bool> change)
    {
        var check = new CheckBox { Text = text + (value ? " · On" : " · Off"), ButtonPressed = value, CustomMinimumSize = new Vector2(0, 42) }; check.AddThemeFontSizeOverride("font_size", 24);
        StyleOption(check);
        check.Toggled += enabled => { check.Text = text + (enabled ? " · On" : " · Off"); change(enabled); Changed(); }; Entries.AddChild(check);
    }
    private static void StyleOption(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" })
        {
            var box=Palette.Box(state == "focus" ? Colors.Transparent : new Color(.025f,.040f,.059f,state == "normal" ? .65f : .9f), new Color(.63f,.49f,.27f,state == "focus" ? .9f : .4f), 8, 0);
            box.BorderWidthTop=0; box.BorderWidthLeft=0; box.BorderWidthRight=0;
            button.AddThemeStyleboxOverride(state,box);
        }
    }
    private void Changed()
    {
        PreferencesChanged();
        _message.Text = Preferences.Save() == Error.Ok ? "Settings saved on this device." : "Settings applied, but could not be saved on this device.";
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (Page != "main") ShowMain(); else if (CanResume()) Resume(); else ShowQuit();
        GetViewport().SetInputAsHandled();
    }
}

public partial class MenuEntry : Button
{
    public Func<bool> ReducedMotion = () => false;
    private float _glow, _time;
    private readonly StyleBoxFlat[] _halos = Enumerable.Range(1,18).Select(i => Palette.Box(Colors.Transparent, null, 0, i)).ToArray();
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        var target = IsHovered() || HasFocus() ? 1f : 0f;
        _glow = ReducedMotion() ? target : Mathf.Lerp(_glow, target, 1-Mathf.Exp(-(float)delta*9));
        if (!ReducedMotion()) _time += (float)delta;
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (_glow < .002) return;
        float width = Math.Min(Size.X-10, Palette.Title.GetStringSize(Text, fontSize: GetThemeFontSize("font_size")).X+55);
        float pulse = ReducedMotion() ? 1 : .92f+.08f*Mathf.Sin(_time*1.3f);
        for (var i=18; i>=1; i--)
        {
            _halos[i-1].BgColor = new Color(.95f,.54f,.16f,_glow*pulse*.008f);
            DrawStyleBox(_halos[i-1], new Rect2(5-i, Size.Y*.68f-i, width+i*2, i*2+4));
        }
        for (var i=0; i<5; i++)
            DrawLine(new Vector2(10+i*13,Size.Y-13+i),new Vector2(width-i*19,Size.Y-13+i),new Color(.88f,.56f,.20f,_glow*(.12f+i*.02f)),1);
    }
}
