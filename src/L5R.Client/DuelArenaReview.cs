using Godot;
using L5R.Core;
using System.Text.Json;

namespace L5R.Client;

/// <summary>Composition-only review for #205. Fixture cards are public samples, not a match.
/// The imported Blender geometry and existing TableSurface use the same projection.
/// No duel decisions, hidden match state, or rules calculations exist in this scene.</summary>
public partial class DuelArenaReview : Control
{
    private SubViewport _world = null!;
    private TextureRect _worldImage = null!, _reference = null!;
    private Camera3D _camera = null!;
    private Node3D _model = null!;
    private WorldEnvironment _environment = null!;
    private DirectionalLight3D _keyLight = null!;
    private readonly List<OmniLight3D> _lanternLights = [];
    private TableSurface _table = null!;
    private Control _plane = null!, _hud = null!;
    private readonly List<CardTile> _cards = [];
    private readonly List<Control> _fixed = [];
    private CardTile? _reading;
    private bool _showReference, _revealed, _crowded, _bowed, _sidebar;
    private bool _clean, _lanternDetail, _matDetail, _pineDetail, _pineReverse, _pineBefore;
    private Texture2D _compositionSource = null!, _matSource = null!, _lanternSource = null!;
    private Texture2D _pineSource = null!, _pineReverseSource = null!;
    private bool _selfCheck;
    private string? _capture;
    private string _title = "Duel arena · iteration 6";
    private double _elapsed;
    private double _captureDelay = 1.5;
    private int _checkStep, _inspections;
    private float _zoom = 1, _density;
    private Rect2 _play;
    private const float Unit = 100;
    private static float Distance => Mathf.Sqrt(1 - TableSurface.Pitch * TableSurface.Pitch) / (TableSurface.Depth * Unit);

    public override void _Ready()
    {
        var preferences = new MenuPreferences("default"); preferences.Load();
        _gardenReducedMotion = preferences.ReducedMotion;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--capture=")) _capture = arg[10..];
            _showReference |= arg == "--reference";
            _revealed |= arg == "--revealed";
            _crowded |= arg == "--crowded";
            _bowed |= arg == "--bowed";
            _sidebar |= arg == "--sidebar";
            _selfCheck |= arg == "--check";
            _clean |= arg == "--clean";
            _lanternDetail |= arg == "--lantern";
            _matDetail |= arg == "--mat";
            _pineDetail |= arg == "--pine";
            _pineReverse |= arg == "--reverse";
            if (arg == "--pine-before") _pineBefore = _pineDetail = true;
            if (arg == "--reduced-motion") _gardenReducedMotion = true;
            if (arg == "--motion") _gardenReducedMotion = false;
            if (arg.StartsWith("--phase=")) _gardenPhase = int.Parse(arg[8..]);
            if (arg.StartsWith("--zoom=")) _zoom = Math.Clamp(float.Parse(arg[7..], System.Globalization.CultureInfo.InvariantCulture), .75f, 1.35f);
            if (arg.StartsWith("--settle=")) _captureDelay = Math.Clamp(double.Parse(arg[9..], System.Globalization.CultureInfo.InvariantCulture), 1.5, 10);
        }
        if (_pineBefore) _title = "Duel arena · iteration 4 baseline";
        Theme = Palette.MakeTheme();
        _world = new SubViewport { OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = false, Msaa3D = Viewport.Msaa.Msaa4X };
        AddChild(_world);
        _model = GD.Load<PackedScene>(_pineBefore
            ? "res://Art/DuelArena/duel-arena-v4.glb"
            : "res://Art/DuelArena/duel-arena-v5.glb").Instantiate<Node3D>();
        _world.AddChild(_model);
        var matMesh = (MeshInstance3D)_model.FindChild("ApprovedDuelMat", true, false);
        var originalMaterial = (BaseMaterial3D)matMesh.GetActiveMaterial(0);
        var paintedMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/DuelArena/approved-mat.gdshader") };
        paintedMat.SetShaderParameter("artwork", originalMaterial.AlbedoTexture);
        matMesh.MaterialOverride = paintedMat;
        _environment = new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("111823"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("b8c5d4"), AmbientLightEnergy = .45f,
            TonemapMode = Godot.Environment.ToneMapper.Linear
        }};
        _world.AddChild(_environment);
        _keyLight = new DirectionalLight3D { RotationDegrees = new Vector3(-62, -25, 0),
            LightColor = new Color("e4ebff"), LightEnergy = .8f, ShadowEnabled = true };
        _world.AddChild(_keyLight);
        foreach (var name in new[] { "StoneLanternLeftGlow", "StoneLanternRightGlow" })
        {
            var anchor = (Node3D)_model.FindChild(name, true, false);
            var light = new OmniLight3D { LightColor = new Color("ffaf58"), LightEnergy = 1,
                OmniRange = 2.3f, ShadowEnabled = true };
            anchor.AddChild(light); _lanternLights.Add(light);
        }
        _camera = new Camera3D { Current = true, Near = .05f, Far = 100,
            Position = new Vector3(0, Distance * TableSurface.Pitch, Distance * Mathf.Sqrt(1 - TableSurface.Pitch * TableSurface.Pitch)) };
        _world.AddChild(_camera); _camera.LookAt(Vector3.Zero);
        BuildGardenPresentation();
        _worldImage = new TextureRect { Texture = _world.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_worldImage);
        Texture2D Source(string name) => ImageTexture.CreateFromImage(Image.LoadFromFile(
            ProjectSettings.GlobalizePath("res://../../assets/ui/duel-arena/" + name)));
        _compositionSource = Source("approved-generation-3.png");
        _matSource = Source("approved-duel-mat.png"); _lanternSource = Source("approved-stone-lantern.png");
        _pineSource = Source("approved-pine-primary.png"); _pineReverseSource = Source("approved-pine-reverse.png");
        _reference = new TextureRect { Texture = _compositionSource,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_reference);
        _table = new TableSurface(); AddChild(_table);
        _plane = new Control(); _table.Canvas.AddChild(_plane);
        _hud = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_hud);
        GetViewport().SizeChanged += Layout;
        Layout();
        GD.Print("DUEL_LAYOUT: approved mat, Meshy lantern and pine, real published scans, TableSurface camera. No gameplay commands.");
    }

    private Vector2 LayoutAnchor(string name)
    {
        var node = _model.FindChild(name, true, false) as Node3D
            ?? throw new InvalidOperationException($"Missing Blender anchor: {name}");
        return new Vector2(node.GlobalPosition.X, node.GlobalPosition.Z) * Unit;
    }

    private void Layout()
    {
        foreach (var c in _plane.GetChildren()) { _plane.RemoveChild(c); c.QueueFree(); }
        foreach (var c in _hud.GetChildren()) { _hud.RemoveChild(c); c.QueueFree(); }
        _cards.Clear(); _fixed.Clear(); _reading = null;
        var screen = GetViewportRect().Size;
        var detail = _lanternDetail || _matDetail || _pineDetail;
        _environment.Environment.AmbientLightEnergy = detail ? .55f : .25f;
        _environment.Environment.BackgroundColor = _pineDetail ? new Color("999999") : new Color("111823");
        _environment.Environment.AmbientLightColor = _pineDetail ? Colors.White : new Color("b8c5d4");
        _keyLight.LightColor = _pineDetail ? Colors.White : new Color("e4ebff");
        _keyLight.LightEnergy = detail ? .8f : .4f;
        _keyLight.RotationDegrees = new Vector3(-62, _pineDetail && _pineReverse ? 155 : -25, 0);
        foreach (var light in _lanternLights) light.Visible = !detail;
        var side = _sidebar ? Math.Min(260, screen.X * .20f) : 0;
        _play = new Rect2(Vector2.Zero, new Vector2(detail ? screen.X / 2 : screen.X - side, screen.Y));
        var uiScale = Math.Min(screen.X / 1600, screen.Y / 900);
        // Extra render height shifts the optical center down while the native
        // window crops the unused foreground. Both 3D and cards share this
        // canvas, leaving headroom for the complete central tree.
        var stageSize = _play.Size + new Vector2(0, detail ? 0 : 300 * uiScale);
        _density = Math.Min(_play.Size.X / 1600, _play.Size.Y / 900) * (detail ? .8f : .68f) * _zoom;
        _world.Size = (Vector2I)stageSize;
        _worldImage.Position = _play.Position; _worldImage.Size = stageSize;
        _camera.Projection = Camera3D.ProjectionType.Perspective;
        _camera.Position = new Vector3(0, Distance * TableSurface.Pitch, Distance * Mathf.Sqrt(1 - TableSurface.Pitch * TableSurface.Pitch));
        _camera.LookAt(Vector3.Zero);
        _camera.Fov = Mathf.RadToDeg(2 * Mathf.Atan(stageSize.Y / (2 * Distance * Unit * _density)));
        foreach (var child in _model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            child.Visible = !detail || child.Name == (_lanternDetail ? "StoneLanternLeft" : _pineDetail ? (_pineBefore ? "PineLeft" : "PineHero") : "ApprovedDuelMat");
        if (_lanternDetail)
        {
            var lantern = (Node3D)_model.FindChild("StoneLanternLeft", true, false);
            _camera.Position = lantern.GlobalPosition + new Vector3(3.4f, 2.8f, 5.2f);
            _camera.LookAt(lantern.GlobalPosition + new Vector3(0, 1.28f, 0));
            _camera.Fov = 31;
        }
        if (_matDetail)
        {
            _camera.Projection = Camera3D.ProjectionType.Orthogonal;
            _camera.Position = new Vector3(0, 12, -1.2f); _camera.RotationDegrees = new Vector3(-90, 0, 0);
            _camera.Size = Math.Max(7.8f, 16 / (_play.Size.X / _play.Size.Y));
        }
        if (_pineDetail)
        {
            var pine = (MeshInstance3D)_model.FindChild(_pineBefore ? "PineLeft" : "PineHero", true, false);
            var bounds = pine.GetAabb();
            var height = bounds.Size.Y;
            var center = bounds.Position + bounds.Size * .5f;
            _camera.Position = pine.GlobalTransform * (center + new Vector3(0, height * .02f, height * (_pineReverse ? -2.35f : 2.35f)));
            _camera.LookAt(pine.GlobalTransform * center);
            _camera.Fov = 32;
        }
        _reference.Texture = _lanternDetail ? _lanternSource : _matDetail ? _matSource
            : _pineDetail ? (_pineReverse ? _pineReverseSource : _pineSource) : _compositionSource;
        _reference.Position = detail ? new Vector2(screen.X / 2, 80) : Vector2.Zero;
        _reference.Size = detail ? new Vector2(screen.X / 2, screen.Y - 160) : screen;
        if (_matDetail)
        {
            var imageWidth = _play.Size.X * 15.65f / 16;
            _reference.Position = new Vector2(screen.X / 2 + (_play.Size.X - imageWidth) / 2, 80);
            _reference.Size = new Vector2(imageWidth, screen.Y - 160);
        }
        _reference.Visible = detail || _showReference;
        _worldImage.Visible = detail || !_showReference;
        LayoutGardenPresentation(detail);
        _table.Scale = Vector2.One * _density;
        _table.ResizePlane(stageSize / _density, Math.Max(1, _density * 1.4f));
        _table.Position = _play.Position; // after ResizePlane, which resets Position when no drawing area is given
        _table.Visible = !detail && !_showReference && !_clean;
        _plane.Size = _table.CanvasSize;
        _hud.Visible = detail || (!_showReference && !_clean);
        if (detail)
        {
            if (_pineDetail)
            {
                foreach (var y in new[] { 0f, screen.Y - 80 })
                    _hud.AddChild(new ColorRect { Color = new Color("111823"), Position = new Vector2(0, y),
                        Size = new Vector2(screen.X, 80), MouseFilter = MouseFilterEnum.Ignore });
            }
            Text(_lanternDetail ? "Lantern · Meshy asset in Godot" : _pineDetail
                ? (_pineBefore ? "Pine · previous foliage" : _pineReverse ? "Pine · refined reverse view" : "Pine · refined needle clusters") : "Mat · native top view",
                new Vector2(28, 22), 24, Palette.Paper, true);
            Text("Supplied reference", new Vector2(screen.X / 2 + 28, 22), 24, Palette.Paper, true);
            Text("T pine   G reverse   L lantern   M mat   V clear view   Tab concept   Esc close", new Vector2(28, screen.Y - 32), 14, Palette.PaperDim);
            return;
        }
        var definitions = PublishedContent.Snapshot.Cards;
        CardDefinition Named(string name) => CardCatalog.Cards.Values.First(d => d.Name == name);
        var left = Named("Doji Tatsuki"); var right = Named("Matsu Hachiro");
        var attachments = definitions.Where(d => d.Type is CardType.Item or CardType.Follower).ToArray();
        foreach (var (name, owner, def) in new[] { ("DuelLeft", 0, left), ("DuelRight", 1, right) })
        {
            var center = LayoutAnchor(name);
            var count = _crowded ? 6 : 2;
            for (var i = count - 1; i >= 0; i--)
            {
                var attached = attachments[i % attachments.Length];
                AddCard(Sample($"attachment-{owner}-{i}", attached, owner, Zone.Attached, _bowed && i % 2 == 0),
                    center + new Vector2(0, -28 - i * 14), 320);
            }
            AddCard(Sample($"unit-{owner}", def, owner, Zone.Battlefield, _bowed && owner == 1), center, 320);
            var focus = LayoutAnchor(owner == 0 ? "FocusLeft" : "FocusRight");
            for (var i = 0; i < 4; i++)
            {
                var fate = definitions.Where(d => d.Type == CardType.Strategy).ElementAt(i);
                // Public composition fixtures only. Hidden samples deliberately carry no definition.
                var card = _revealed ? Sample($"focus-{owner}-{i}", fate, owner, Zone.Focused)
                    : new CardView($"anonymous-focus-{owner}-{i}", null, owner, Zone.Focused, false, false, null, null, 0);
                AddCard(card, focus + new Vector2((i - 1.5f) * 117, 0), 180);
            }
        }
        var ui = Math.Min(screen.X / 1600, screen.Y / 900);
        Text(_title, new Vector2(28, 18) * ui, 24 * ui, Palette.Paper, true);
        Text(_pineBefore ? "Earlier paired-pine layout · public sample cards"
            : "Moonlit garden · central pine · public sample cards",
            new Vector2(28, 54) * ui, 14 * ui, Palette.PaperDim);
        Text("Tab concept   V clear   T pine   L lantern   M mat   R reveal   C attachments   B bow   P sidebar   N motion   Wheel zoom   F fit   Esc close",
            new Vector2(28 * ui, screen.Y - 27 * ui), 13 * ui, Palette.PaperDim);
        var handDefs = definitions.Where(d => d.Type == CardType.Strategy).Take(5).ToArray();
        for (var i = 0; i < handDefs.Length; i++)
        {
            var c = new CardTile(Sample($"hand-{i}", handDefs[i], 0, Zone.Hand), true, Inspect, _ => {})
            { FaceHeight = 133 * ui, Size = new Vector2(141, 141) * ui,
                Position = new Vector2(_play.Size.X / 2 - 305 * ui + i * 61 * ui, screen.Y - 181 * ui),
                ReducedMotion = true, ShowTokenRail = false, Material = CardPaper.FixedTable };
            _hud.AddChild(c); _fixed.Add(c);
        }
        var controls = new VBoxContainer { Position = new Vector2(_play.Size.X / 2 + 85 * ui, screen.Y - 172 * ui),
            Size = new Vector2(222, 140) * ui };
        controls.AddThemeConstantOverride("separation", (int)(4 * ui));
        _hud.AddChild(controls); _fixed.Add(controls);
        foreach (var label in new[] { "Focus from hand", "Focus from deck", "Strike" })
        {
            var b = new Button { Text = label, Disabled = true, CustomMinimumSize = new Vector2(220, 35) * ui,
                TooltipText = "Placement study only; duel actions remain with the engine." };
            b.AddThemeStyleboxOverride("disabled", Palette.Box(new Color("1c242a"), new Color("655943"), (int)(6 * ui)));
            b.AddThemeFontSizeOverride("font_size", (int)(16 * ui)); controls.AddChild(b);
        }
        if (_sidebar)
        {
            var panel = new ColorRect { Position = new Vector2(_play.Size.X, 0), Size = new Vector2(side, screen.Y),
                Color = new Color("12191e") }; _hud.AddChild(panel);
            Text("Sidebar clearance", new Vector2(_play.Size.X + 20, 125 * ui), 19 * ui, Palette.Paper);
            Text("Reserved space only", new Vector2(_play.Size.X + 20, 162 * ui), 14 * ui, Palette.PaperDim);
        }
    }

    private static CardView Sample(string id, CardDefinition definition, int owner, Zone zone, bool bowed = false) =>
        new(id, definition, owner, zone, true, bowed, null, null, definition.Force, definition.Chi);

    private void AddCard(CardView card, Vector2 center, float height)
    {
        CardTile tile = card.Definition is null ? new DuelArenaFocusBack(card) : new CardTile(card, true, Inspect, _ => {});
        tile.Size = Vector2.One * (height + 12); tile.FaceHeight = height;
        tile.Position = _table.CanvasSize / 2 + center - Vector2.One * (height + 12) / 2;
        tile.ReducedMotion = true; tile.ShowTokenRail = false;
        tile.Material = CardPaper.FixedTable; tile.ProjectToScreen = _table.ProjectCanvas;
        _plane.AddChild(tile); _cards.Add(tile);
    }

    private void Text(string text, Vector2 position, float size, Color color, bool title = false)
    {
        var label = Palette.Text(text, Math.Max(11, (int)size), color, title);
        label.Position = position; label.MouseFilter = MouseFilterEnum.Ignore; _hud.AddChild(label);
    }

    private void Inspect(CardView card)
    {
        _inspections++;
        if (_reading is not null) { _hud.RemoveChild(_reading); _reading.QueueFree(); }
        if (card.Definition is null) { _reading = null; return; }
        var h = Math.Min(580, GetViewportRect().Size.Y * .68f);
        _reading = new CardTile(card with { Bowed = false, Zone = Zone.Home }, true, _ => ClearReading(), _ => {})
        { FaceHeight = h, Size = Vector2.One * (h + 12), ReducedMotion = true, ShowTokenRail = false,
            Position = _play.Size / 2 - Vector2.One * (h + 12) / 2, Material = CardPaper.Reading };
        _hud.AddChild(_reading);
    }

    private void ClearReading()
    {
        if (_reading is null) return;
        _hud.RemoveChild(_reading); _reading.QueueFree(); _reading = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } wheel && wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        { _zoom = Math.Clamp(_zoom + (wheel.ButtonIndex == MouseButton.WheelUp ? .05f : -.05f), .75f, 1.35f); Layout(); return; }
        if (!_showReference && !_clean && !_lanternDetail && !_matDetail && !_pineDetail && @event is InputEventMouse mouse && _reading is null && _play.HasPoint(mouse.Position)
            && !_fixed.Any(c => c.GetGlobalRect().HasPoint(mouse.Position)))
            _table.ForwardMouse(mouse);
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (_capture is not null && key.Keycode != Key.Escape) return;
        switch (key.Keycode)
        {
            case Key.Escape: GetTree().Quit(); return;
            case Key.Tab:
                _showReference = !_showReference; _lanternDetail = _matDetail = _pineDetail = false; break;
            case Key.V:
                _clean = _lanternDetail || _matDetail || _pineDetail || !_clean;
                _lanternDetail = _matDetail = _pineDetail = _showReference = false; break;
            case Key.L: _lanternDetail = !_lanternDetail; _matDetail = _pineDetail = _showReference = false; break;
            case Key.M: _matDetail = !_matDetail; _lanternDetail = _pineDetail = _showReference = false; break;
            case Key.T: _pineDetail = !_pineDetail; _lanternDetail = _matDetail = _showReference = false; break;
            case Key.G: if (!_pineDetail) return; _pineReverse = !_pineReverse; break;
            case Key.R: _revealed = !_revealed; break;
            case Key.C: _crowded = !_crowded; break;
            case Key.B: _bowed = !_bowed; break;
            case Key.P: _sidebar = !_sidebar; break;
            case Key.F: _zoom = 1; break;
            case Key.N: _gardenReducedMotion = !_gardenReducedMotion; break;
            default: return;
        }
        Layout();
    }

    public override void _Process(double delta)
    {
        AnimateGardenPresentation(delta);
        if (_capture is null) return;
        _elapsed += delta;
        if (_elapsed < _captureDelay) return;
        try
        {
            // Compare an independently imported Blender anchor against the existing 2.5D projection.
            var detail = _lanternDetail || _matDetail || _pineDetail;
            if (_selfCheck && (detail || _clean || _showReference))
                throw new InvalidOperationException("--check requires the visible card layout.");
            var maxError = detail ? 0 : new[] { "DuelLeft", "DuelRight", "FocusLeft", "FocusRight", "Center" }.Max(name =>
            {
                var a = LayoutAnchor(name);
                var screen = _camera.UnprojectPosition(new Vector3(a.X / Unit, 0, a.Y / Unit));
                return screen.DistanceTo(_table.ProjectCanvas(_table.CanvasSize / 2 + a));
            });
            if (maxError > .5f) throw new Exception($"Blender/card camera mismatch: {maxError} px");
            foreach (var c in _cards)
            {
                if (c.Card.Definition is { } def && CardTile.Artwork(def) is null)
                    throw new Exception($"Missing actual card art: {def.Name}");
                if (c.FaceCornersOnViewport().Any(p => !_play.HasPoint(p)))
                    throw new Exception($"Card is outside the play viewport: {c.Card.Id}");
                // Inner parchment bounds of the original 1817x866 image on its 15.65-unit plane.
                var silk = new Rect2(-748, -460, 1496, 671);
                if (c.FaceCornersInCanvas().Any(p => !silk.HasPoint(p - _table.CanvasSize / 2)))
                    throw new Exception($"Card is outside the silk: {c.Card.Id}");
            }
            foreach (var c in _fixed)
                if (!GetViewportRect().Encloses(c.GetGlobalRect()))
                    throw new Exception("Hand or controls are clipped by the viewport");
            if (_selfCheck && _checkStep == 0)
            {
                var front = _cards.First(c => c.Card.Id == "unit-0");
                var points = front.FaceCornersOnViewport();
                var point = points.Aggregate(Vector2.Zero, (sum, p) => sum+p) / 4;
                Input.ParseInputEvent(new InputEventMouseMotion { Position = point });
                Input.ParseInputEvent(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = true });
                Input.ParseInputEvent(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = false });
                _checkStep = 1; _elapsed = 0; return;
            }
            if (_selfCheck && _checkStep == 1)
            {
                if (_inspections != 1 || _reading?.Card.Id != "unit-0")
                    throw new Exception("Native projected click did not open the correct card exactly once");
                ClearReading();
                Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(15, 100) });
                _checkStep = 2; _elapsed = 0; return;
            }
            RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
            Rect2? pineScreenBounds = null;
            if (!detail && !_showReference && !_pineBefore)
            {
                // Check the actual imported silhouette, including roots. A matching
                // card projection alone cannot catch a tree cropped by the window.
                var pine = (MeshInstance3D)_model.FindChild("PineHero", true, false);
                var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (var surface = 0; surface < pine.Mesh.GetSurfaceCount(); surface++)
                    foreach (var vertex in pine.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    {
                        var point = _camera.UnprojectPosition(pine.GlobalTransform * vertex);
                        minimum = minimum.Min(point); maximum = maximum.Max(point);
                    }
                pineScreenBounds = new Rect2(minimum, maximum - minimum);
                if (Mathf.IsEqualApprox(_zoom, 1) && !_play.Grow(-16 * Math.Min(_play.Size.X / 1600, _play.Size.Y / 900)).Encloses(pineScreenBounds.Value))
                    throw new Exception($"Complete pine does not fit the default view: {pineScreenBounds}");
            }
            var error = GetViewport().GetTexture().GetImage().SavePng(_capture);
            if (error != Error.Ok) throw new Exception($"Capture failed: {error}");
            System.IO.File.WriteAllText(_capture + ".json", JsonSerializer.Serialize(new {
                Stage = _pineBefore ? "iteration 4 pine baseline" : "iteration 6", Viewport = GetViewportRect().Size.ToString(),
                GardenPhase = _gardenPhase, ReducedMotion = _gardenReducedMotion, AtmosphereTime = _gardenTime,
                Zoom = _zoom,
                MatDetail = _matDetail, LanternDetail = _lanternDetail, ClearView = _clean,
                PineDetail = _pineDetail, PineReverse = _pineReverse, PineBefore = _pineBefore,
                CameraChecked = !detail,
                PineScreenBounds = pineScreenBounds?.ToString(),
                MaxCameraErrorPixels = maxError, NativeCardClicks = _inspections,
                Crowded = _crowded, Bowed = _bowed, Revealed = _revealed, Sidebar = _sidebar,
                ObservedFramesPerSecond = Engine.GetFramesPerSecond(),
                RenderedPrimitives = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame),
                CardFaces = _cards.Count, GameplayTested = false, FinalArt = false }));
            GD.Print($"DUEL_LAYOUT_PASS: camera error {maxError:F4}px; {_cards.Count} cards in viewport; native clicks {_inspections}.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

/// <summary>The existing generic hidden-card renderer assumes a Dynasty card. Keep this Fate-back
/// presentation local to the review until the live duel presentation is implemented.</summary>
public partial class DuelArenaFocusBack : CardTile
{
    public DuelArenaFocusBack() { }
    public DuelArenaFocusBack(CardView card) : base(card, true, _ => {}, _ => {})
    { SetMeta("flat_tooltip", true); TooltipText = "Focused Fate card · face down"; }
    public override void _Draw()
    {
        var texture = GD.Load<Texture2D>("res://Art/original-fate-back.png");
        var face = new Vector2(FaceHeight * texture.GetWidth() / texture.GetHeight(), FaceHeight);
        var rect = new Rect2((Size - face) / 2, face);
        DrawRect(new Rect2(rect.Position + new Vector2(2, 3), face), new Color(0, 0, 0, .45f));
        DrawTextureRect(texture, rect, false);
    }
}
