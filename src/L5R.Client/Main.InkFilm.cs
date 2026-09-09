using Godot;
using L5R.AI;
using L5R.Core;

namespace L5R.Client;

/// <summary>24 fps evidence of a real permitted purchase and its resulting layout change.</summary>
public partial class Main
{
    private bool _inkFilmPrepared;
    private bool _inkFilmDrawing;
    private int _inkFilmFrame, _inkFilmWarmup, _inkFilmOwner;
    private GameCommand? _inkFilmPurchase;
    private string _inkFilmCardName = "";
    private long _inkFilmVersion;
    private Rect2 _inkFilmBefore, _inkFilmAfter;
    private Vector2 _inkFilmCameraCenter;
    private float _inkFilmCameraZoom;
    private bool _inkFilmSawMidpoint, _inkFilmSawWaiting;
    private readonly List<object> _inkFilmFrames = [];

    private async void ProcessInkFilm()
    {
        if (_inkFilmDrawing) return;
        _inkFilmDrawing = true;
        try
        {
            if (!_inkFilmPrepared)
            {
                PrepareInkFilm();
                _inkFilmPrepared = true;
                return;
            }
            // Native VBox/Control sizes resolve on deferred frames. Do not make
            // the first saved image a half-laid-out fixture after setup's Render.
            if (_inkFilmWarmup++ < 3) { AdvanceInkFilmVisuals(); return; }
            if (_inkFilmFrame == 0)
            {
                // Record the fixed camera only after the native window and HUD
                // have completed their deferred sizing, before any filmed action.
                FocusMySide(); _cameraZoom *= .8f; ApplyTableCamera();
                _inkFilmCameraCenter = _cameraCenter; _inkFilmCameraZoom = _cameraZoom;
            }
            if (_inkFilmFrame == 12)
            {
                if (_inkFilmPurchase is null) throw new InvalidOperationException("Ink film has no prepared purchase.");
                _session.Submit(_inkFilmPurchase).GetAwaiter().GetResult();
                _uiQueue.Clear(); Render();
                var player = View.Players.Single(p => p.Id == _inkFilmOwner);
                if (View.Version != _inkFilmVersion + 1 || HoldingCards(player).Length != 5 ||
                    !player.Home.Any(c => c.Id == _inkFilmPurchase.CardId && c.Definition?.Type == CardType.Holding))
                    throw new InvalidOperationException("The real Holding purchase was rejected or did not enter play.");
                _inkFilmAfter = _holdingBounds[_inkFilmOwner];
                if (InkSame(_inkFilmBefore, _inkFilmAfter))
                    throw new InvalidOperationException("This purchase did not change the actual Holdings section bounds.");
                if (!_matSections.IsAnimating || _matSections.RevealProgress($"holdings:{_inkFilmOwner}") != 0)
                    throw new InvalidOperationException("The purchase's new border appeared before the card movement.");
                if (!_flights.Any(IsInstanceValid)) throw new InvalidOperationException("The purchased card has no native transfer animation.");
                GD.Print($"INK_FILM_PURCHASE: {_inkFilmCardName}; version {_inkFilmVersion}->{View.Version}; in-play group 4->5 including Stronghold; {_inkFilmBefore}->{_inkFilmAfter}; payment uses offered source ids.");
            }
            AdvanceInkFilmVisuals();
            if (_cameraCenter.DistanceTo(_inkFilmCameraCenter) > .01f || Math.Abs(_cameraZoom - _inkFilmCameraZoom) > .001f)
                throw new InvalidOperationException($"Film camera changed at frame {_inkFilmFrame}: center {_inkFilmCameraCenter}->{_cameraCenter}; zoom {_inkFilmCameraZoom}->{_cameraZoom}; window {GetViewport().GetVisibleRect().Size}.");
            var current = InkBounds(_matSections, $"holdings:{_inkFilmOwner}");
            var reveal = _matSections.RevealProgress($"holdings:{_inkFilmOwner}");
            var moving = !TableMotionsSettled();
            if (_inkFilmFrame >= 12)
            {
                if (moving && reveal > 0) throw new InvalidOperationException("Ink appeared before actual card/pile movement settled.");
                if (moving && reveal == 0) _inkFilmSawWaiting = true;
                if (!moving && reveal > 0 && reveal < 1) _inkFilmSawMidpoint = true;
            }
            _inkFilmFrames.Add(new
            {
                frame = _inkFilmFrame, seconds = _inkFilmFrame / 24.0, version = View.Version,
                holdingsBounds = new { x = current.Position.X, y = current.Position.Y, width = current.Size.X, height = current.Size.Y },
                animating = _matSections.IsAnimating, reveal, cardsMoving = moving
            });
            // Allow deferred native layout before drawing. macOS may stop emitting
            // FramePostDraw when the recording window becomes occluded; explicit
            // drawing after two process frames keeps capture reliable without
            // recording the half-built controls immediately inside Render().
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
            var folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_capturePath!)!, "ink-purchase-frames");
            System.IO.Directory.CreateDirectory(folder);
            using var frame = GetViewport().GetTexture().GetImage();
            if (frame.SavePng(System.IO.Path.Combine(folder, $"frame-{_inkFilmFrame:000}.png")) != Error.Ok)
                throw new InvalidOperationException("Native ink purchase frame could not be saved.");
            if (++_inkFilmFrame < 120) return;

            if (!_inkFilmSawWaiting || !_inkFilmSawMidpoint || _matSections.IsAnimating || !InkSame(current, _inkFilmAfter))
                throw new InvalidOperationException("Real purchase did not wait for cards, spread ink gradually, and settle at its target.");
            if (_tiles.Any(t => t.SelfModulate.A != 1) || TablePiles.Any(p => Math.Abs(p.ShownCount - p.Count) > .01f))
                throw new InvalidOperationException("Purchase film ended with invisible cards or unsettled pile counts.");
            ValidateInkBoardTopology();
            if (View.Version != _inkFilmVersion + 1) throw new InvalidOperationException("Film advanced an extra game decision.");
            if (frame.SavePng(_capturePath!) != Error.Ok) throw new InvalidOperationException("Ink film final still could not be saved.");
            System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(_capturePath!, "json"),
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    description = "Native game capture of one real, engine-accepted Holding purchase; the in-play Holdings group changes from four to five cards including its Stronghold. No fabricated gameplay state.",
                    card = _inkFilmCardName, purchase = _inkFilmPurchase, beforeVersion = _inkFilmVersion,
                    afterVersion = View.Version, framesPerSecond = 24, frames = _inkFilmFrames,
                    checks = new[] { "Engine accepted purchase", "Holding entered its actual Home zone", "Actual layout bounds changed", "Camera stayed fixed", "Ink absent throughout actual card movement", "Gradual reveal after card settlement", "Borders, card opacity and pile counts settled", "Complete real card membership retained" }
                }, Json));
            var replay = _session.ExportPracticeReplay();
            if (replay is not null) System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(_capturePath!, "replay.json"), replay);
            GD.Print("INK_FILM_PASS: 120 native frames at 24 fps; real offered purchase at frame 12; four-to-five in-play group including Stronghold; card-first movement followed by organic ink apparition, stable camera, full card transfer and exact settled state. Contraction is tested separately with renderer geometry.");
            GetTree().Quit(); _capturePath = null;
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); _capturePath = null; }
        finally { _inkFilmDrawing = false; }
    }

    private void PrepareInkFilm()
    {
        _reducedMotion = false;
        var setupCommands = 0;
        for (var attempt = 0; attempt < 6 && _inkFilmPurchase is null; attempt++)
        {
            if (attempt > 0) _session.StartPractice(false, true, 1);
            for (var step = 0; step < 1200 && View.WinnerId is null; step++)
            {
                _session.FollowPracticePriority();
                // Both ownership and legality come from the permitted view and
                // offered actions. Limiting the other shelf prevents it already
                // setting the shared three-column width before this purchase.
                if (HoldingCards(Self).Length == 4 && View.Players.Where(p => p.Id != Self.Id).All(p => HoldingCards(p).Length <= 4))
                {
                    var candidate = Actions.FirstOrDefault(a => a.Enabled && a.Type == "purchase" &&
                        a.Quote is { Error: null, Payments.Length: > 0 } &&
                        Self.Provinces.Any(p => p.Card?.Id == a.CardId && p.Card?.Definition?.Type == CardType.Holding));
                    if (candidate is not null)
                    {
                        _inkFilmPurchase = TrainingOpponent.Choose(View, [candidate]).Command;
                        if (_inkFilmPurchase is not null) break;
                    }
                }
                var command = TrainingOpponent.Choose(View, Actions).Command
                    ?? throw new InvalidOperationException("Ink film setup received no complete legal training action.");
                var version = View.Version;
                _session.Submit(command).GetAwaiter().GetResult();
                if (View.Version == version) throw new InvalidOperationException("Ink film setup submitted a rejected training action.");
                setupCommands++;
            }
        }
        if (_inkFilmPurchase is null) throw new InvalidOperationException("Could not find a legal fourth-to-fifth in-play Holding-group purchase in six real matches.");
        _inkFilmOwner = Self.Id;
        _inkFilmVersion = View.Version;
        _inkFilmCardName = Self.Provinces.Single(p => p.Card?.Id == _inkFilmPurchase.CardId).Card!.Definition!.Name;
        _uiQueue.Clear(); _selected = null; _chosen = null; _expandedUnits.Clear();
        Render();
        foreach (var flight in _flights.Where(IsInstanceValid)) flight.Finish();
        _flights.Clear();
        ResetTableCamera(); FocusMySide();
        _cameraZoom *= .8f; _cameraFitted = false; ApplyTableCamera();
        _inkFilmCameraCenter = _cameraCenter; _inkFilmCameraZoom = _cameraZoom;
        _inkFilmBefore = _holdingBounds[_inkFilmOwner];
        foreach (var control in _tiles.Cast<Control>().Concat(TablePiles))
            foreach (var reflow in control.GetChildren().OfType<TableCardReflow>())
                for (var i = 0; i < 9; i++) reflow._Process(.1);
        foreach (var pile in TablePiles) { pile.SetProcess(false); pile.SettleFrom(pile.Count, true); }
        foreach (var tile in _tiles) tile._Process(.2);
        AdvanceInk(_matSections, 2);
        ReleaseTableFocus(); HideTableTooltip();
        GD.Print($"INK_FILM_SETUP: reached a real affordable {_inkFilmCardName} purchase after {setupCommands} permitted training commands; current version {View.Version}; seat {_inkFilmOwner}. No frames of setup were included.");
    }

    private void AdvanceInkFilmVisuals()
    {
        const double step = 1.0 / 24;
        _matSections.SetProcess(false); _matSections._Process(step); _matSections.SetProcess(false);
        foreach (var flight in _flights.Where(IsInstanceValid).ToArray()) { flight.SetProcess(false); flight._Process(step); }
        foreach (var pile in TablePiles)
        {
            foreach (var reflow in pile.GetChildren().OfType<TableCardReflow>())
            { reflow.SetProcess(false); reflow._Process(step); }
            pile.SetProcess(false); pile._Process(step);
        }
        foreach (var tile in _tiles.Where(IsInstanceValid))
        {
            foreach (var reflow in tile.GetChildren().OfType<TableCardReflow>())
            { reflow.SetProcess(false); reflow._Process(step); }
            tile.SetProcess(false); tile._Process(step);
        }
        _timeline.SetProcess(false); _timeline._Process(step);
        _table.SetProcess(false);
        _table.PresentLight(_inkFilmFrame / 24f, true); _timeline.PresentLight(_inkFilmFrame / 24f, true);
    }
}
