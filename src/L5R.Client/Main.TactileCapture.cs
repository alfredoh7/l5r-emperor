using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Deterministic native-renderer capture of real commands, not staged card art.</summary>
public partial class Main
{
    private int _tactileFrame;
    private readonly List<string> _tactileChecks = [];
    private void ProcessTactileCapture()
    {
        try
        {
            if (_tactileFrame == 0)
            {
                if (IsFullscreen || _fullscreenGate.Visible) throw new InvalidOperationException("Windowed play is gated.");
                if (_tiles.Where(t => t.Card.Zone == Zone.Hand).Any(t => Math.Abs(t.FaceHeight - (HandCardSide - 12)) > .01)) throw new InvalidOperationException("Hand faces are not 20% larger.");
                _tactileChecks.Add("Windowed play; full original card backs; enlarged hand faces.");
            }
            if (_tactileFrame == 12)
            {
                var option = Actions.First(a => a.Enabled && a.Command?.AbilityId == "border-first");
                var ids = Self.Provinces.Take(2).Select(p => p.Card!.Id).ToArray();
                _session.Submit(option.Command! with { CardIds = ids }).GetAwaiter().GetResult();
                FlushTactile();
                if (_flights.Count < 4) throw new InvalidOperationException("Province replacement did not animate both returns and both draws.");
                _tactileChecks.Add("Border Keep: two Dynasty cards returned and two replacements drawn; four transfers.");
            }
            if (_tactileFrame == 42)
            {
                for (var i = 0; View.Phase != Phase.Dynasty && i < 10; i++)
                    _session.Submit(new GameCommand("pass", View.PriorityPlayerId, View.Version)).GetAwaiter().GetResult();
                if (View.Phase != Phase.Dynasty) throw new InvalidOperationException("Could not reach Dynasty.");
                FlushTactile();
            }
            if (_tactileFrame == 54)
            {
                var prior = Self.Discard.Length;
                _session.Submit(Actions.First(a => a.Enabled && a.Type == "discardProvince").Command!).GetAwaiter().GetResult();
                FlushTactile();
                if (Self.Discard.Length != prior + 1 || _flights.Count < 1) throw new InvalidOperationException("Dynasty discard transfer missing.");
                _tactileChecks.Add("Dynasty discard: visible card travels to its owner's public discard pile.");
            }
            if (_tactileFrame == 84)
            {
                var hand = Self.Hand.Length;
                _session.Submit(Actions.First(a => a.Enabled && a.Type == "endDynasty").Command!).GetAwaiter().GetResult();
                FlushTactile();
                if (Self.Hand.Length != hand + 1 || _flights.Count < 1) throw new InvalidOperationException("End-phase Fate draw missing.");
                _tactileChecks.Add("End-phase Fate draw: the new private hand card flips into view.");
            }
            foreach (var flight in _flights.Where(IsInstanceValid)) { flight.SetProcess(false); flight._Process(1.0 / 24); }
            foreach (var pile in TablePiles) { pile.SetProcess(false); pile._Process(1.0 / 24); }
            _timeline.SetProcess(false); _timeline._Process(1.0 / 24);
            _table.PresentLight(_tactileFrame / 24f, true); _timeline.PresentLight(_tactileFrame / 24f, true);
            RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
            var folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_capturePath!)!, "motion-frames");
            System.IO.Directory.CreateDirectory(folder);
            var frame = GetViewport().GetTexture().GetImage();
            if (frame.SavePng(System.IO.Path.Combine(folder, $"frame-{_tactileFrame:000}.png")) != Error.Ok) throw new InvalidOperationException("Native motion frame failed.");
            if (++_tactileFrame < 120) return;
            if (_tiles.Any(t => t.SelfModulate.A != 1) || TablePiles.Any(p => Math.Abs(p.ShownCount - p.Count) > .01)) throw new InvalidOperationException("Transfer left invisible cards or stale pile height.");
            _tactileChecks.Add("All card opacity and pile counts settle to the authoritative state.");
            // Reduced motion uses the same state transitions without any flight or delayed pile count.
            _reducedMotion = true;
            _session.StartPractice(false, true, 1); FlushTactile();
            var version = View.Version;
            var refresh = Actions.First(a => a.Enabled && a.Command?.AbilityId == "border-first");
            _session.Submit(refresh.Command! with { CardIds = Self.Provinces.Take(2).Select(p => p.Card!.Id).ToArray() }).GetAwaiter().GetResult(); FlushTactile();
            if (View.Version != version + 1 || _flights.Count != 0 || TablePiles.Any(p => p.ShownCount != p.Count)) throw new InvalidOperationException("Reduced motion did not settle immediately.");
            _tactileChecks.Add("Reduced motion bypasses flights and count interpolation.");
            frame.SavePng(_capturePath!);
            System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(_capturePath!, "json"), System.Text.Json.JsonSerializer.Serialize(_tactileChecks, Json));
            GD.Print("UI_SMOKE_PASS: " + string.Join(" ", _tactileChecks));
            GetTree().Quit(); _capturePath = null;
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); _capturePath = null; }
    }
    private void FlushTactile() { _uiQueue.Clear(); Render(); }
}
