using Godot;

namespace L5R.Client;

public partial class Main
{
    private static IEnumerable<StatTokens> DescendantTokens(Node node) => node.GetChildren()
        .SelectMany(child => child is StatTokens token && token is not UnitStandard ? new[] { token } : DescendantTokens(child));
    private IEnumerable<StatTokens> InspectableTokens => TokenPairs.Concat(DescendantTokens(_unitDetail));
    private static Rect2 TokenVisualRect(StatTokens token) => new(token.GlobalPosition,
        token.GetGlobalTransform().Scale * new Vector2(token.Diameter * 2.12f, token.Diameter + 4));
    private bool _tokenClickChecked;
    private string? _tokenProbeError;
    private async void ProbeStatTokenInput()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (var t in TokenPairs) t.PositionAtCard();
            var pair = InspectableTokens.First(t => t.IsVisibleInTree() && (t.Inline || _tableViewport.GetGlobalRect().Encloses(TokenVisualRect(t))));
            var version = View.Version;
            var point = pair.GetGlobalTransform() * new Vector2(pair.Diameter / 2, pair.Diameter / 2);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = true });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = false });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_selected?.Id != pair.Card.Id || View.Version != version) throw new InvalidOperationException("Token click failed to inspect its Personality without changing the match.");
            _tokenClickChecked = true;
            if (_captureMode.Contains("tokentip", StringComparison.Ordinal))
            {
                GetWindow().GrabFocus();
                await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
                pair.PositionAtCard();
                point = pair.GetGlobalTransform() * new Vector2(pair.Diameter / 2, pair.Diameter / 2);
                GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            }
        }
        catch (Exception e) { _tokenProbeError = e.Message; }
    }

    private void ValidateStatTokens()
    {
        var pairs = InspectableTokens.Where(t => t.IsVisibleInTree()).ToArray();
        if (_expandedUnits.Count == 0)
        {
            var expected = View.Players.SelectMany(p => p.Home.Concat(p.Army)).Where(c => c.Definition?.Type == L5R.Core.CardType.Personality && c.AttachedTo is null).Select(c => c.Id).Order();
            if (!expected.SequenceEqual(TokenPairs.Select(t => t.Card.Id).Order())) throw new InvalidOperationException("Personality token pair missing or duplicated.");
        }
        for (var i = 0; i < pairs.Length; i++)
        {
            var t = pairs[i]; t.PositionAtCard();
            if (t.Card.Force != t.CardAnchor.Card.Force || t.Card.Chi != t.CardAnchor.Card.Chi) throw new InvalidOperationException("Token value differs from engine view.");
            var rect = TokenVisualRect(t);
            if (!t.Inline && _cameraFitted && !_tableViewport.GetGlobalRect().Grow(2).Encloses(rect)) throw new InvalidOperationException("Token clipped in fitted view.");
            foreach (var tile in _tiles.Where(c => t.Inline ? _unitDetail.IsAncestorOf(c) : _world.IsAncestorOf(c)))
            {
                var face = tile.FaceCornersOnViewport();
                var min = new Vector2(face.Min(p => p.X), face.Min(p => p.Y));
                var max = new Vector2(face.Max(p => p.X), face.Max(p => p.Y));
                if (!rect.Grow(-1).Intersects(new Rect2(min, max - min))) continue;
                var intersects = Enumerable.Range(0, 2).Any(index =>
                {
                    var center = new Vector2(t.Diameter * (.5f + index * 1.12f), t.Diameter / 2);
                    var circle = Enumerable.Range(0, 48).Select(n => t.GetGlobalTransform() *
                        (center + Vector2.FromAngle(n * Mathf.Tau / 48) * (t.Diameter / 2 - 1))).ToArray();
                    return Geometry2D.IntersectPolygons(circle, face).Count > 0;
                });
                if (intersects)
                {
                    GetViewport().GetTexture().GetImage().SavePng(_capturePath + ".failure.png");
                    throw new InvalidOperationException($"Token {t.Card.Id} at {rect} obscures {tile.Card.Id} at {new Rect2(min, max - min)}.");
                }
            }
            for (var j = i + 1; j < pairs.Length; j++)
                if (rect.Intersects(TokenVisualRect(pairs[j]))) throw new InvalidOperationException("Token pairs collide.");
        }
        if (_captureMode.Contains("tokens", StringComparison.Ordinal) && (!_tokenClickChecked || _tokenProbeError is not null)) throw new InvalidOperationException(_tokenProbeError ?? "Native token input probe incomplete");
        if (_captureMode.Contains("tokentip", StringComparison.Ordinal) &&
            (ReadingTooltip.LastText?.Contains("Click either token to select this Personality.", StringComparison.Ordinal) != true || _tableTooltip is null || _tableHover is not StatTokens))
            throw new InvalidOperationException($"Native token hover failed: token={OverStatToken(_lastTableMouse)}, pointer={_lastTableMouse}, native={GetViewport().GuiGetHoveredControl()?.GetType().Name}, tableTip={_tableTooltip is not null}, text={ReadingTooltip.LastText}");
        GD.Print($"STAT_TOKEN_PASS: {pairs.Length} engine-bound pairs; no card occlusion, clipping or token collisions; nativeClick={_tokenClickChecked}.");
    }
}
