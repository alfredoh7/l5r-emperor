using Godot;
using L5R.Core;
using L5R.AI;
using System.Collections.Concurrent;
using System.Text.Json;

namespace L5R.Client;

public partial class Main : Node
{
    private readonly MatchSession _session = new();
    private readonly ConcurrentQueue<Action> _uiQueue = new();
    private Control _root = null!;
    private Control _board = null!, _hand = null!, _unitDetail = null!;
    private VBoxContainer _inspector = null!, _phaseActions = null!, _decisionActions = null!;
    private PhaseTrack _timeline = null!;
    private Control _clanHud = null!, _pileHud = null!;
    private Label _status = null!, _notice = null!, _seatLabel = null!, _guide = null!;
    private TableArtStage _table = null!;
    private CardView? _selected;
    private ActionOption? _chosen;
    private readonly HashSet<string> _sources = [];
    private readonly List<CardTile> _tiles = [];
    private readonly Dictionary<string, bool> _previousBows = [];
    private readonly Dictionary<string, bool> _currentBows = [];
    private long _bowVersion = -1;
    private long _displayedVersion = -1;
    private ulong _bowChangedAt;
    private readonly Dictionary<string, int> _assignments = [];
    private readonly HashSet<string> _expandedUnits = [];
    private string? _assignmentUnit;
    private BoardIntentOverlay _intentOverlay = null!;
    private readonly Dictionary<(int Owner, int Province), Button> _provinceButtons = [];
    private readonly Dictionary<int, Button> _laneSummaries = [];
    private bool _autoOpponent = true, _reducedMotion;
    private double _opponentClock, _captureClock;
    private string? _capturePath;
    private string _captureMode = "table";
    private bool _capturePrepared;
    private int _captureFrames, _filmFrames;
    private bool _fullscreenResumeSubmitted;
    private bool _pileHoverSent;
    private string? _hoverProbeCard;
    private long _hoverProbeVersion;
    private GameCommand? _hoverProbeCommand;
    private Button? _paymentConfirm;
    private RoomSeat? _savedSeat;
    private string _savedServer = "http://localhost:5210";
    private string _profile = "default";
    private string SeatPath => ProjectSettings.GlobalizePath($"user://private-seat-{_profile}.json");
    /// <summary>Where saved decks live -- plain files in DeckFormat.cs's text format (see #24),
    /// so they can be hand-edited or dropped into another player's local install.</summary>
    private string DecksDirectory => ProjectSettings.GlobalizePath("user://Decks");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private MatchView View => (_presentationSnapshot ?? _session.Snapshot)!.View;
    private ActionOption[] Actions => PresentationBusy ? [] : _session.Snapshot!.Actions;
    private PlayerView Self => View.Players.Single(p => p.Id == View.ViewerId);

    public override void _Ready()
    {
        GetWindow().MinSize = new Vector2I(1120, 700);
        _session.Changed += () =>
        {
            // Capture every permitted snapshot, even when several packets arrive within one rendered frame.
            var snapshot=_session.Snapshot;var generation=_session.Generation;
            _uiQueue.Enqueue(() =>
            {
                if(generation!=_session.Generation)return;
                if(_matchDuelGeneration!=generation){_matchDuelFlow.Reset();_matchDuelGeneration=generation;}
                _matchDuelFlow.Observe(snapshot);Render();
            });
        };
        _session.Notice += message => _uiQueue.Enqueue(() => SetNotice(message));
        _session.SeatCreated += (seat, server) => _uiQueue.Enqueue(() => SaveSeat(seat, server));
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--capture=")) _capturePath = arg[10..];
            if (arg.StartsWith("--capture-mode=")) _captureMode = arg[15..];
            if (arg.StartsWith("--profile=") && arg[10..].Length is > 0 and <= 32 && arg[10..].All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) _profile = arg[10..];
        }
        GetWindow().Mode = _captureMode.Contains("fullscreen", StringComparison.Ordinal) ? Window.ModeEnum.Fullscreen : Window.ModeEnum.Windowed;
        if (_capturePath is not null) GetWindow().GrabFocus();
        if (_captureMode == "battle-reference" && _capturePath is not null)
        { SetProcessInput(false); AddChild(new BattleArtReview(_capturePath)); SetProcess(false); return; }
        if (_captureMode == "battle-escalation-reference" && _capturePath is not null)
        { SetProcessInput(false); AddChild(new BattleEscalationReview(_capturePath)); SetProcess(false); return; }
        if (_captureMode == "threshold-reference" && _capturePath is not null)
        { SetProcessInput(false); SetProcessUnhandledInput(false); SetProcessUnhandledKeyInput(false); AddChild(new ThresholdArtReview(_capturePath)); SetProcess(false); return; }
        LoadSeat();
        _table = new TableArtStage { ReviewMode = false }; AddChild(_table);
        BuildShell();
        InitializeFrontend();
        if (_capturePath is not null && _captureMode.StartsWith("published-interactions-", StringComparison.Ordinal))
        {
            var review = new InteractionReview(this, _captureMode, _capturePath);
            _capturePath = null; AddChild(review); return;
        }
        if(OS.GetCmdlineUserArgs().Contains("--duel-match-review")){StartMatchDuelCapture();return;}
        if(OS.GetCmdlineUserArgs().Contains("--duel-transition-review")){StartTransitionReview();return;}
        if (_capturePath is null || _captureMode.StartsWith("menu", StringComparison.Ordinal)) return;
        var published = _capturePath is null || _captureMode.StartsWith("published", StringComparison.Ordinal);
        if (_captureMode.StartsWith("gold-", StringComparison.Ordinal)) _session.StartShowcase(_captureMode.Split('-')[1]);
        else _session.StartPractice(!published, published, _capturePath is null ? 0 : 1);
        Render();
        SetNotice(published ? "Published Lion vs Crane · choose Practice to change Clan or scenario." : "Economy practice · choose a province card to preview its payment.");
    }

    public override void _Process(double delta)
    {
        while (_uiQueue.TryDequeue(out var update)) update();
        if(_transitionReview){try{ProcessTransitionReview(delta);}catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}return;}
        if (_captureMode.StartsWith("menu", StringComparison.Ordinal) && _capturePath is not null) { try { ProcessMenuCapture(delta); } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); } return; }
        if(MatchDuelOpen){ProcessMatchDuel(delta);ProcessMatchDuelCapture(delta);return;}
        if(ProcessMatchDuelCapture(delta))return;
        if (MenuVisible || DuelDemoOpen || PracticeVisible && _capturePath is null) return;
        ProcessBattlePresentation(delta);
        if (_session.Snapshot is not null) SyncBloodCourts();
        ProcessTableCamera(delta);
        if (_session.Snapshot is not null && _matSections.ReducedMotion != _reducedMotion) SyncMatSections();
        ExpireBattleResolution();
        foreach (var tracker in _sidebarHeader.GetChildren().OfType<BattleTracker>()) tracker.ReducedMotion = _reducedMotion;
        foreach (var plaque in _clanHud.GetChildren().OfType<ClanPlaque>()) plaque.ReducedMotion = _reducedMotion;
        _fullscreenGate.Visible = false;
        if (_autoOpponent && !PresentationBusy && !_session.Online && _capturePath is null)
        {
            _opponentClock += delta;
            if (_opponentClock > (_reducedMotion ? .12 : .7))
            {
                _opponentClock = 0;
                _session.StepPracticeOpponent((view, actions) => view.PendingDuel is not null ? DuelOpponent.Choose(view,actions) : TrainingOpponent.Choose(view, actions).Command);
            }
            // No delay: this only fires when declining is the player's sole legal option, so there is
            // nothing for them to weigh — unlike the opponent's move above, it needs no "thinking" pause.
            _session.StepPracticeReaction();
        }
        if (_capturePath is not null && _captureMode == "published-ink-film") { ProcessInkFilm(); return; }
        if (_capturePath is not null && _captureMode == "published-tactile") { ProcessTactileCapture(); return; }
        if (_capturePath is not null)
        {
            _captureClock += delta;
            if (!_capturePrepared && _captureClock > 1.2)
            {
                _capturePrepared = true;
                _captureClock = 0;
                if (_captureMode.StartsWith("published-home", StringComparison.Ordinal)) PreparePersistentHomeCapture();
                if (_captureMode.StartsWith("published-grants", StringComparison.Ordinal)) PreparePermanentGrantCapture();
                if (_captureMode.StartsWith("published-fallen", StringComparison.Ordinal)) PrepareFallenCapture();
                if (_captureMode is "published-choice" or "published-choice-confirm")
                {
                    for (var fixtureAttempt = 0; fixtureAttempt < 6 && View.PendingChoice?.Type != "shinden"; fixtureAttempt++)
                    {
                        if (fixtureAttempt > 0)
                        {
                            GD.Print("CHOICE_SETUP: practice ended without a Shinden decision; searching another real match before testing the UI.");
                            _session.StartPractice(false, true, 1);
                        }
                        var steps = 0;
                        while (View.PendingChoice?.Type != "shinden" && View.WinnerId is null && steps++ < 800)
                        {
                            _session.FollowPracticePriority();
                            var command = TrainingOpponent.Choose(View, Actions).Command ?? throw new InvalidOperationException("Capture opponent had no action.");
                            var version = View.Version;
                            _session.Submit(command).GetAwaiter().GetResult();
                            if (View.Version == version) throw new InvalidOperationException("Capture action was rejected.");
                        }
                    }
                    _session.FollowPracticePriority(); _uiQueue.Clear(); _selected = null; _chosen = null; Render();
                    if (View.PendingChoice?.Type != "shinden") throw new InvalidOperationException("Capture did not reach a Shinden decision.");
                    if (_captureMode == "published-choice-confirm")
                    {
                        var version = View.Version; var craneHonor = View.Players[0].Honor;
                        var button = _phaseActions.GetChildren().OfType<Button>().Single(b => b.Text == "Allow opponent to gain 1 Honor");
                        button.EmitSignal(Button.SignalName.Pressed);
                        if (View.Version != version + 1 || View.PendingChoice is not null || View.Players[0].Honor != craneHonor + 1)
                            throw new InvalidOperationException("Shinden choice UI smoke failed.");
                        GD.Print("UI_SMOKE_PASS: real published match reached Shinden; opponent's Honor choice resolved through the button handler.");
                    }
                }
                if (_captureMode is "published-refresh" or "published-refresh-confirm")
                {
                    var option = Actions.First(a => a.Enabled && a.Command?.AbilityId == "border-first");
                    _selected = FindCard(option.CardId!); Choose(option);
                    if (_captureMode == "published-refresh-confirm")
                    {
                        var before = Self.Provinces[0].Card!.Id;
                        _inspector.GetChildren().OfType<CheckBox>().First().ButtonPressed = true;
                        _decisionActions.GetChildren().OfType<Button>().First().EmitSignal(Button.SignalName.Pressed);
                        if (View.Version != 1 || View.PendingChoice is not null || Self.Provinces[0].Card!.Id == before)
                            throw new InvalidOperationException("Published province refresh UI smoke failed.");
                        GD.Print("UI_SMOKE_PASS: published Border Keep selected and refreshed through the real controls.");
                    }
                }
                if (_captureMode.StartsWith("published-board", StringComparison.Ordinal))
                {
                    try { PrepareBoardCapture(); }
                    catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); _capturePath = null; return; }
                }
                if (_captureMode.StartsWith("gold-", StringComparison.Ordinal))
                {
                    try { PrepareGoldPaymentCapture(); }
                    catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); _capturePath = null; return; }
                }
                if (_captureMode == "library") LibraryDialog.Show(this);
                if (_captureMode == "published-rules") ShowRules();
                if (_captureMode == "published-lacquer-menu") ShowTableMenu();
                if (_captureMode == "published-lacquer-practice") ShowPractice();
                if (_captureMode == "published-lacquer-private") ShowOnline();
                if (_captureMode == "published-lacquer-coverage") ShowCoverage();
                if (_captureMode == "published-lacquer-history") ShowHistory();
                if (_captureMode == "published-lacquer-pile") ShowPile(View.ViewerId, Zone.Dead);
                if (_captureMode == "published-lacquer-confirm") Confirm("Leave this table?", "Return to the main menu? Your current practice will end.", () => { });
                if (_captureMode == "published-lacquer-tooltip")
                    Place(_root, ReadingTooltip.Create(this, "Bowed / unbowed\nA sideways card is bowed. Each attachment keeps its own bowed state."), 600, 260, 460, 180);
                if (_captureMode == "published-lacquer-card")
                    Place(_root, CardPreview.Create(View.Players.SelectMany(p=>p.Home.Concat(p.Provinces.Where(x=>x.Card is not null).Select(x=>x.Card!))).First(c=>c.Definition is not null), "Your hand", null), 430, 200, 750, 558);
                if (_captureMode is "payment" or "purchase")
                {
                    var option = Actions.First(a => a.Type == "purchase" && a.Enabled && a.Quote is { ClanDiscount: false, ClanAllegiance: false, ProclaimEligible: true });
                    _selected = FindCard(option.CardId!);
                    Choose(option);
                    if (_captureMode == "purchase")
                    {
                        _paymentConfirm!.EmitSignal(Button.SignalName.Pressed);
                        if (View.PendingChoice?.Type != "proclaim") throw new InvalidOperationException("UI purchase smoke failed: no Proclaim reaction.");
                        GD.Print("UI_SMOKE_PASS: confirmed payment through button handler; Proclaim reaction opened.");
                    }
                }
            }
            if (_capturePrepared) _captureFrames++;
            if (_captureMode.Contains("clarity", StringComparison.Ordinal)) MaintainClarityPointer();
            if (_captureMode.StartsWith("published-grants", StringComparison.Ordinal)) MaintainGrantPointer();
            if (_capturePrepared && _captureMode.Contains("camera", StringComparison.Ordinal)) ProcessCameraCapture();
            if (_capturePath is null) return;
            if (_capturePrepared && _captureMode.EndsWith("piletip", StringComparison.Ordinal) && _captureClock > .4 && !_pileHoverSent)
            {
                _pileHoverSent = true; ReadingTooltip.LastText = null;
                var pile = _pileHud.GetChildren().OfType<TablePile>().Single(p => p.OwnerId == View.ViewerId && p.PileZone == Zone.FateDeck);
                var point = ScreenCenter(pile);
                GetViewport().WarpMouse(point);
                GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = Vector2.One }, true);
            }
            if (_capturePrepared && _captureMode.EndsWith("gate-resume", StringComparison.Ordinal) && _captureClock > .6 && !_fullscreenResumeSubmitted)
            {
                _fullscreenResumeSubmitted = true;
                _UnhandledKeyInput(new InputEventKey { Keycode = Key.F11, Pressed = true });
            }
            if (_capturePrepared && _captureMode.Contains("candle", StringComparison.Ordinal) && _captureClock >= .5 && _filmFrames < 72)
            {
                // Record the real renderer at evenly sampled light phases; match state
                // and camera stay unchanged. No offline image compositing is involved.
                _table.PresentLight(_filmFrames / 12f, !_reducedMotion); _timeline.PresentLight(_filmFrames / 12f, !_reducedMotion);
                RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
                var folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_capturePath)!, "candle-frames");
                System.IO.Directory.CreateDirectory(folder);
                var result = GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(folder, $"frame-{_filmFrames:000}.png"));
                if (result != Error.Ok) throw new InvalidOperationException("Could not save candle animation frame.");
                _filmFrames++;
            }

            if (_capturePrepared && _captureClock > .6 && _hoverProbeCard is null && _captureMode.EndsWith("hover", StringComparison.Ordinal))
            {
                var tile = _tiles.First(t => t.Card.Id == _selected?.Id);
                _hoverProbeCard = tile.Card.Id; _hoverProbeVersion = View.Version; _hoverProbeCommand = _chosen?.Command;
                CardTile.LastPreviewCardId = null;
                var point = ScreenCenter(tile);
                GetViewport().WarpMouse(point);
                GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = Vector2.One }, true);
                GD.Print($"HOVER_PROBE: {point}, control={GetViewport().GuiGetHoveredControl()?.GetPath()}");
            }
            // These fixtures review settled menus, after the startup turn announcement clears.
            if ((_captureMode.StartsWith("published-lacquer-", StringComparison.Ordinal) || _captureMode.StartsWith("published-home", StringComparison.Ordinal) || _captureMode == "published-rules" || _captureMode.StartsWith("published-grants", StringComparison.Ordinal)) && _turnBloom.Playing) return;
            if (_capturePrepared && (!_captureMode.StartsWith("published-fallen", StringComparison.Ordinal) || _fallenProbeDone) && (!_captureMode.StartsWith("published-grants", StringComparison.Ordinal) || _grantProbeDone) && (!_captureMode.Contains("courts", StringComparison.Ordinal) || _battleProbeDone) && (!_captureMode.Contains("ink", StringComparison.Ordinal) || _inkProbeDone) && (!_captureMode.Contains("deselect", StringComparison.Ordinal) || _deselectProbeDone) && (!_captureMode.Contains("standards", StringComparison.Ordinal) || _standardProbeDone) && (!_captureMode.Contains("hudicons", StringComparison.Ordinal) || _hudProbeDone) && (!_captureMode.Contains("clarity", StringComparison.Ordinal) || _clarityInputDone && _captureClock > _clarityReadyAt + 1) && (!_captureMode.Contains("camera", StringComparison.Ordinal) || _cameraProbeDone) && (!_captureMode.Contains("candle", StringComparison.Ordinal) || _filmFrames >= 72) && _captureClock > (_captureMode.Contains("gate", StringComparison.Ordinal) ? 3 : (_captureMode.Contains("clarity", StringComparison.Ordinal) || _captureMode.EndsWith("hover", StringComparison.Ordinal) || _captureMode.EndsWith("piletip", StringComparison.Ordinal)) ? 2 : 1) && _captureFrames >= 15)
            {
                if (_captureMode.StartsWith("published-board", StringComparison.Ordinal))
                {
                    try { ValidateBoardCapture(); }
                    catch (Exception e) { GD.PushError(e.Message); GetViewport().GetTexture().GetImage().SavePng(_capturePath); GetTree().Quit(1); _capturePath = null; return; }
                }
                // An occluded macOS window may stop presenting while process frames continue.
                // Capture an explicitly drawn, synchronized frame rather than its previous texture.
                RenderingServer.ForceDraw(false);
                RenderingServer.ForceSync();
                var error = GetViewport().GetTexture().GetImage().SavePng(_capturePath);
                try { ValidateLacquerMenus(); }
                catch (Exception e) { GD.PushError(e.Message); error = Error.Failed; }
                GD.Print($"CAPTURE_RESULT: {error} {_capturePath}");
                GetTree().Quit(error == Error.Ok ? 0 : 1);
                _capturePath = null;
            }
        }
    }

    private Vector2 DesignSize => new(_layoutWidth, 1080);
    private float _layoutWidth = 1920;
    private float BoardWidth => _layoutWidth - 238 - SidebarWidth - 44;
    private const float SidebarWidth = 400;
    private const float HandCardSide = 284;
    private HBoxContainer _menu = null!;
    private Control _inspectorPanel = null!, _fullscreenGate = null!;
    private bool IsFullscreen => GetWindow().Mode is Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen;

    private void FitPresentation()
    {
        if(_transitionReview && _transitionRoom is not null)return;
        var available = GetViewport().GetVisibleRect().Size;
        var scale = available.Y / 1080f;
        // Root controls use an explicit scale, so rasterize system fonts at the
        // matching density instead of magnifying their 1080p glyph textures.
        ((FontFile)Palette.Body).Oversampling = Math.Max(1, scale);
        Palette.HeadingFace.Oversampling = Math.Max(1, scale);
        _layoutWidth = available.X / Math.Max(.1f, scale);
        _root.Size = DesignSize;
        _root.Scale = Vector2.One * scale;
        _root.Position = Vector2.Zero;
        if (_board is null) return;
        FitTableViewport();
        _hand.Position = new Vector2(250, 1060 - HandCardSide); _hand.Size = new Vector2(BoardWidth - 12, HandCardSide);
        _inspectorPanel.Position = new Vector2(_layoutWidth - SidebarWidth - 22, 94);
        _inspectorPanel.Size = new Vector2(SidebarWidth, 960);
        _status.Visible = false;
        _timeline.Position = new Vector2(344, 12); _timeline.Size = new Vector2(_layoutWidth - SidebarWidth - 404, 100);
        _menu.Position = new Vector2(_layoutWidth - 300, 28); _menu.Size = new Vector2(275, 40);
        _notice.Position = new Vector2(_layoutWidth - SidebarWidth - 22, 1060); _notice.Size = new Vector2(SidebarWidth, 19);
        _unitDetail.Size = DesignSize;
        _clanHud.Size = DesignSize;
        _fullscreenGate.Size = DesignSize;
        if (_session.Snapshot is not null) RenderBoard();
    }

    private static T Place<T>(Node parent, T child, float x, float y, float width, float height) where T : Control
    {
        parent.AddChild(child);
        child.Position = new Vector2(x, y); child.Size = new Vector2(width, height);
        return child;
    }

    private VBoxContainer _sidebarHeader = null!;

    private void BuildShell()
    {
        var canvas = new CanvasLayer(); AddChild(canvas);
        _root = new Control { Theme = Palette.MakeTheme(), Size = DesignSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(_root);
        BuildTableViewport();
        _status = Place(_root, Palette.Text("", 17, Palette.PaperDim), 295, 15, 750, 26);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _timeline = Place(_root, new PhaseTrack(), 344, 12, 956, 100);
        _menu = Place(_root, new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }, 1345, 27, 549, 35);
        _menu.AddThemeConstantOverride("separation", 7);
        _menu.AddChild(new TableIconButton(TableGlyph.Menu, ShowTableMenu, "Menu"));
        _menu.AddChild(new TableIconButton(TableGlyph.Rules, ShowRules, "Rules and reference"));
        _matSections = new InkMatSections { MotionSettled = TableMotionsSettled }; _world.AddChild(_matSections);
        _tableViewport.AddChild(_matSections.CreateIconOverlay(WorldToScreen));
        _battleDecor = new TableBattleInk { Paint = DrawBattleInk }; _world.AddChild(_battleDecor);
        _board = Place(_world, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, 0, 0, WorldWidth, 1100);
        _pileHud = Place(_world, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, 0, 0, WorldWidth, 1100);
        _hand = Place(_root, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, 250, 1060 - HandCardSide, BoardWidth - 12, HandCardSide);
        var inspectorPanel = Place(_root, new PanelContainer { Theme = SidebarSkin.Theme() }, 1488, 94, SidebarWidth, 960);
        _inspectorPanel = inspectorPanel;
        inspectorPanel.AddThemeStyleboxOverride("panel", SidebarSkin.Surface());
        var side = new VBoxContainer(); side.AddThemeConstantOverride("separation", 10); inspectorPanel.AddChild(side);
        _seatLabel = Palette.Text("", 13, Palette.Gold); _seatLabel.Visible = false; side.AddChild(_seatLabel);
        _sidebarHeader = new VBoxContainer(); side.AddChild(_sidebarHeader);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; side.AddChild(scroll);
        _inspector = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _inspector.AddThemeConstantOverride("separation", 10); scroll.AddChild(_inspector);
        side.AddChild(new HSeparator());
        _guide = Palette.Wrapped("", 17, Palette.PaperDim); _guide.MouseFilter = Control.MouseFilterEnum.Stop; side.AddChild(_guide);
        _decisionActions = new VBoxContainer(); side.AddChild(_decisionActions);
        _phaseActions = new VBoxContainer(); side.AddChild(_phaseActions);
        _notice = Place(_root, Palette.Text("", 13, Palette.PaperDim), 26, 1060, 1868, 19);
        _notice.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; _notice.MouseFilter = Control.MouseFilterEnum.Stop;
        _intentOverlay = new BoardIntentOverlay { MouseFilter = Control.MouseFilterEnum.Ignore, Arrows = IntentArrows };
        _tableViewport.AddChild(_intentOverlay); _intentOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // Fixed placards overlap the projected table and the hand. Keep both drawing
        // and native GUI picking above those cards, but below expanded/modal views.
        _clanHud = Place(_root, new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 45 }, 0, 0, 1920, 1080);
        _unitDetail = Place(_root, new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 50 }, 0, 0, 1920, 1080);
        BuildCameraControls();
        BuildBattleFeedback();
        BuildFullscreenGate();
        GetViewport().SizeChanged += FitPresentation; FitPresentation();
    }

    private void BuildFullscreenGate()
    {
        _fullscreenGate = Place(_root, new ColorRect { Color = new Color(.025f, .022f, .017f, .97f), ZIndex = 100, Visible = false }, 0, 0, 1920, 1080);

    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.F11)
        {
            GetWindow().Mode = IsFullscreen ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
            _preferences.Fullscreen = IsFullscreen;
            if (_capturePath is null) _preferences.Save();
            if (MenuVisible && _titleMenu!.Page == "options") _titleMenu.ShowOptions();
            GetViewport().SetInputAsHandled();
        }
        else if (_seatHandoff is not null && IsInstanceValid(_seatHandoff))
        {
            // A modal cancellation must not also open the background menu.
            if (key.Keycode == Key.Escape) _seatHandoff.QueueFree();
            GetViewport().SetInputAsHandled();
        }
        else if (MenuVisible || DuelDemoOpen || PracticeVisible || _transitionReview || MatchDuelOpen) return;
        else if (key.Keycode == Key.Escape && _expandedUnits.Count > 0)
        {
            _expandedUnits.Clear(); RenderBoard(); GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Escape) { ShowMainMenu(); GetViewport().SetInputAsHandled(); }
    }

    private void Render()
    {
        if(_transitionReview && _transitionRoom is not null)return;
        if (MenuVisible && _enteringPrivate && _session.Online && _session.Connected && _session.Snapshot is not null) { EnterTable(); return; }
        if (MenuVisible || DuelDemoOpen || PracticeVisible) return;
        if (_session.Snapshot is null)
        {
            _status.Text = _session.Busy ? "Connecting to your private match…" : "Disconnected · use Private match to reconnect";
            _matSections.Sync([], true, "disconnected");
            Palette.Clear(_board); Palette.Clear(_hand); Palette.Clear(_inspector); Palette.Clear(_phaseActions); Palette.Clear(_decisionActions);
            return;
        }
        if(RouteMatchDuel())return;
        ObserveBattleJournal();
        PrepareBattlePresentation();
        RenderBattleFeedback();
        FitTableViewport();
        ObserveBattleResolution();
        if (_displayedVersion != View.Version)
        {
            _displayedVersion = View.Version;
            if (View.Events.LastOrDefault() is { } latest) SetNotice(latest.Message);
        }
        if (_chosen?.Command?.ExpectedVersion != View.Version) { _chosen = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; }
        if (_session.Online && (!_session.Connected || (_session.Snapshot.PlayersConnected?.Any(p => !p) ?? false))) { _chosen = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; }
        if (_selected is not null) _selected = FindCard(_selected.Id);
        _seatLabel.Text = _session.Online ? $"ROOM {_session.Snapshot.RoomCode} · {Self.Clan}" : $"PRACTICE · {Self.Clan}";
        _status.Text = View.WinnerId is { } winner ? $"{View.Players[winner].Clan} wins · {View.VictoryReason}" : $"Turn {View.TurnNumber}  ·  {View.Players[View.ActivePlayerId].Clan}  ·  {(View.PriorityPlayerId == View.ViewerId ? "Your opportunity" : View.Players[View.PriorityPlayerId].Clan + " is deciding")}";
        if (View.Scenario is PublishedContent.Scenario or CustomPractice.Scenario) _status.Text += $"  ·  Favor: {(View.FavorPlayerId is int favor ? View.Players[favor].Clan : "unclaimed")}";
        if (_session.Online && _session.Snapshot.PlayersConnected is { } connected && connected.Any(p => !p)) _status.Text = $"Room {_session.Snapshot.RoomCode} · waiting for both players to connect";
        RenderTimeline(); RenderBoard(); RenderInspector(); RenderPhaseActions(); Highlight(); FrameNewBattle();
        RenderBattleFeedback();
    }

    private void RenderTimeline()
    {
        _timeline.Present(View);
        _timeline.PresentActions(View, Actions, !_session.Busy && (!_session.Online || _session.Connected && !(_session.Snapshot!.PlayersConnected?.Any(p => !p) ?? false)));
        if (PresentationBusy)
        {
            _timeline.PriorityLabel.Text = _battleOutcomeText;
            _timeline.PriorityLabel.TooltipText = _battleOutcomeText;
            _timeline.PriorityLabel.Modulate = Palette.Gold;
        }
    }

    private CardTile Tile(CardView card, bool compact, float width, float height)
    {
        var tile = new CardTile(card, compact, SelectCard, _ => { }) { CustomMinimumSize = new Vector2(width, card.Zone == Zone.Hand ? height : PlaneHeight(height)), FaceHeight = height - 12, ReducedMotion = _reducedMotion, Highlighted = _selected?.Id == card.Id, SelectedSource = _sources.Contains(card.Id), PreviousBowed = Time.GetTicksMsec() - _bowChangedAt < 180 ? _previousBows.GetValueOrDefault(card.Id, card.Bowed) : card.Bowed, ContextualGoldProduction = Production(card, View.Players.Single(p => p.Id == card.OwnerId)) };
        tile.TokenSourceName = id => FindCard(id)?.Definition?.Name;
        tile.ContextHelp = card.AttachedTo is { } parent ? "Attached to " + (FindCard(parent)?.Definition?.Name ?? parent) + ". Moves with this unit." : card.Zone == Zone.Battlefield ? $"At Province {card.ProvinceIndex + 1}'s battlefield. " + RulesHelp.Topic("force") : card.Zone == Zone.Home ? "At home. Not assigned to a province. " + (card.Bowed ? RulesHelp.Topic("bow") : "") : card.Zone == Zone.Hand ? "In your hand. Select to see legal actions and targets." : "Select to inspect available actions.";
        if (card.Zone == Zone.Province && card.ProvinceIndex is int province)
        {
            var owner = View.Players.Single(p => p.Id == card.OwnerId);
            tile.ContextHelp = $"{owner.Clan} · Province {province + 1} · {owner.Provinces.Single(p => p.Index == province).Strength} province strength";
        }
        if (card.Zone != Zone.Hand) tile.ProjectToScreen = _tableSurface.ProjectCanvas;
        BindStatTransition(tile);
        _tiles.Add(tile); return tile;
    }

    private void SelectCard(CardView card)
    {
        if (_session.Busy || PresentationBusy) return;
        if (_chosen is { Type: "attack" or "assignDefense" } assignment)
        {
            if (assignment.TargetIds?.Contains(card.Id) == true) { _selected = card; _assignmentUnit = card.Id; RenderInspector(); Highlight(); return; }
            if (card.Zone == Zone.Province && card.OwnerId == 1 - View.ActivePlayerId && card.ProvinceIndex is int province) { SelectProvince(card.OwnerId, province); return; }
            SetNotice("Choose one of your highlighted units, then a highlighted province. Cancel to inspect other cards."); return;
        }
        if (_chosen is { Type: "equip" or "playStrategy" or "useAbility", Command.CardIds: null } targeted && targeted.TargetIds?.Contains(card.Id) == true)
        {
            ChooseTarget(card.Id); return;
        }
        if (_chosen is { Type: "purchase" or "recruitReserve" or "equip" or "useAbility" or "playStrategy" } && card.OwnerId == View.ViewerId && Production(card, Self) > 0 && !card.Bowed && card.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield)
        {
            if (!_sources.Add(card.Id)) _sources.Remove(card.Id);
            RenderInspector(); Highlight(); return;
        }
        // A misclick during a reviewed action must not silently discard its target or payment.
        if (_chosen is not null) { SetNotice("That card is not a legal target. Cancel the current action to inspect it."); return; }
        if (_selected?.Id == card.Id) { DeselectCard(); return; }
        _selectedHomeReminder = null; HighlightHomeReminders();
        _selected = card; _sources.Clear();
        var assign = Actions.FirstOrDefault(a => a.Enabled && a.Type is "attack" or "assignDefense" && a.TargetIds?.Contains(card.Id) == true);
        if (assign is not null) { Choose(assign); _assignmentUnit = card.Id; RenderInspector(); Highlight(); return; }
        var purchase = Actions.FirstOrDefault(a => a.Type == "purchase" && a.CardId == card.Id && a.Enabled);
        if (purchase is not null) Choose(purchase); else { RenderInspector(); RenderPhaseActions(); Highlight(); }
    }

    private void Highlight()
    {
        var selectedProvince = View.Players.SelectMany(p => p.Provinces.Where(q => q.Card?.Id == _selected?.Id && _selected is not null)
            .Select(q => $"province:{p.Id}:{q.Index}")).FirstOrDefault();
        var rootId = _selected?.AttachedTo ?? _assignmentUnit ?? _selected?.Id;
        var selectedUnit = rootId is null ? null : FindCard(rootId);
        var provinceIndex = rootId is not null && _assignments.TryGetValue(rootId, out var draftProvince) ? draftProvince : selectedUnit?.ProvinceIndex;
        _matSections.Emphasize(selectedProvince ?? (provinceIndex is int p && (selectedUnit?.Zone == Zone.Battlefield || _assignments.ContainsKey(rootId!)) ? $"battle:{1 - View.ActivePlayerId}:{p}" : null));
        foreach (var tile in _tiles)
        {
            tile.Highlighted = tile.Card.Id == (_assignmentUnit ?? _selected?.Id) || rootId is not null && tile.Card.AttachedTo == rootId;
            tile.LegalTarget = _chosen?.TargetIds?.Contains(tile.Card.Id) == true;
            tile.ChosenTarget = _chosen?.Command?.TargetId == tile.Card.Id || _assignments.ContainsKey(tile.Card.Id);
            tile.SelectedSource = _sources.Contains(tile.Card.Id); tile.QueueRedraw();
        }
        foreach (var (key, button) in _provinceButtons)
        {
            var legal = _chosen is { Type: "attack" or "assignDefense" } && key.Owner == 1 - View.ActivePlayerId && _chosen.Provinces?.Contains(key.Province) == true;
            button.Modulate = legal ? new Color(1.2f, 1.15f, .85f) : Colors.White;
            var provinceState = View.Players.Single(p => p.Id == key.Owner).Provinces.Single(p => p.Index == key.Province);
            button.TooltipText = RulesHelp.Tooltip($"Province {key.Province + 1} · {provinceState.Strength} province strength\n" + (legal ? "Assign the selected unit here. Nothing moves until you confirm.\n" : "Inspect this province.\n") + RulesHelp.Topic("province"));
        }
        foreach (var (province, label) in _laneSummaries)
        {
            label.Text = "";
            label.TooltipText = $"Province {province + 1} battlefield";
        }
        _intentOverlay?.QueueRedraw();
    }

    private void Choose(ActionOption option)
    {
        if (!option.Enabled || _session.Busy || PresentationBusy) return;
        if (option.Type is "equip" or "playStrategy" or "useAbility" && option.Command is { CardIds: null, TargetId: null } command && option.TargetIds?.FirstOrDefault() is string target)
            option = option with { Command = command with { TargetId = target } };
        _chosen = option; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null;
        foreach (var id in option.Command?.SourceIds ?? []) _sources.Add(id);
        if (option.Type is "attack" or "assignDefense") _assignmentUnit = option.TargetIds?.FirstOrDefault();
        RenderBoard(); RenderInspector(); RenderPhaseActions(); Highlight();
    }

    private async void Submit(GameCommand command)
    {
        if (PresentationBusy) return;
        if (_session.Online && (_session.Snapshot?.PlayersConnected?.Any(p => !p) ?? false))
        {
            SetNotice("Both players need to be connected before continuing."); return;
        }
        _chosen = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null;
        Highlight();
        await _session.Submit(command);
    }

    private static string ShortName(CardView card) => card.Definition?.Name.Replace("training ", "").Replace("Training ", "") ?? "Hidden card";
    // The engine's own figure (CardView.GoldProduced, which also covers a Personality that can bow for Gold while a cost is being paid: Yoritomo
    // Sakuma, issue #338) when it sends one; the printed-card fallback is for payloads from an older engine.
    private static int Production(CardView card, PlayerView owner) => card.GoldProduced ?? ((card.Definition?.Effect == "crane-gold" && owner.Clan == "Crane" || card.Definition?.Effect == "dragon-gold" && owner.Clan == "Dragon") ? 3 : card.Definition?.GoldProduction ?? 0);
    private static int UnitForce(CardView unit, PlayerView owner) => (unit.Bowed ? 0 : unit.Force) + owner.Home.Concat(owner.Army).Where(c => c.AttachedTo == unit.Id && c.Definition?.Type == CardType.Follower && !c.Bowed).Sum(c => c.Force);
    private CardView? FindCard(string id) => View.Players.SelectMany(p => p.Home.Concat(p.Army).Concat(p.Hand).Concat(p.Discard).Concat(p.RemovedFromGame).Concat(p.EventsInPlay??[]).Concat(p.Celestials??[]).Concat(p.Provinces.Where(p => p.Card is not null).Select(p => p.Card!)).Concat(p.Provinces.Where(p=>p.Region is not null).Select(p=>p.Region!)).Append(p.Stronghold)).FirstOrDefault(c => c.Id == id);
    private static string HumanPhase(Phase phase) => phase switch { Phase.AttackDeclaration => "Declare an attack", Phase.InfantryDeclaration => "Assign attackers", Phase.DefenseAssignment => "Assign defenders", Phase.BattleSelection => "Choose a battlefield", Phase.DynastyDiscard => "Dynasty · discard", _ => phase.ToString() };
    private void SetNotice(string message) { _matchDuel?.Notify(message); _notice.Text = message; _notice.TooltipText = message; if (MenuVisible) _titleMenu!.Notify(message); }
    public override void _ExitTree() => _session.Dispose();
}
