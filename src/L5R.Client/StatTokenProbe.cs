using Godot;
using L5R.Core;
using System.Text.Json;

namespace L5R.Client;

/// <summary>Explicit native presentation test scene; never loaded by match setup.</summary>
public partial class StatTokenProbe : Control
{
    private readonly List<StatTokens> _tokens = [];
    private readonly List<CardTile> _cards = [];
    private string _output = "";
    private bool _small, _motion, _tooltip;
    private int _step;
    private double _clock;
    private int _clicks;
    private Control? _tip;
    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--output=")) _output = a[9..];
            _small |= a == "--small"; _motion |= a == "--motion"; _tooltip |= a == "--tooltip";
        }
        GetWindow().GrabFocus();
        Theme = Palette.MakeTheme();
        var screen = GetViewportRect().Size;
        var mat = new TextureRect { Texture = GD.Load<Texture2D>("res://Art/navy-mat-v4.png"), Size = screen,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(mat);
        var heading = Palette.Text("AUTOMATIC FORCE & CHI", 28, Palette.Paper, true); heading.Position = new Vector2(34, 20); AddChild(heading);
        var caption = Palette.Text("Native UI test · illustrative values · 0 / 1 / 3 / 6 attachments · independent bow states", 18); caption.Position = new Vector2(34, 70); AddChild(caption);
        var def = PublishedContent.Snapshot.Cards.First(d => d.Type == CardType.Personality && d.Clan == "Crane");
        var columns = 4;
        var width = screen.X / columns;
        for (var n = 0; n < 8; n++)
        {
            var count = new[] { 0, 1, 3, 6 }[n % 4]; var bowed = n >= 4;
            var side = _small ? 90 : Math.Min(206, (screen.Y - 160) / 2 - 130);
            var x = width * (n % 4) + width / 2 - side / 2;
            var y = 150 + (n / 4) * (screen.Y - 160) / 2;
            var definition = def with { Force = 4, Chi = 3 };
            var card = new CardView("fixture-" + n, definition, n / 4, Zone.Home, true, bowed, null, null, new[] { 0, 7, 12, 100 }[n % 4], new[] { 3, 0, 9, 12 }[n % 4], Modifiers: [new("source", "example", "Force", 3)]);
            var companions = new List<CardTile>();
            for (var i = count - 1; i >= 0; i--)
            {
                var attached = card with { Id = $"attachment-{n}-{i}", AttachedTo = card.Id, Zone = Zone.Attached, Definition = PublishedContent.Snapshot.Cards.First(d => d.Type == (i % 2 == 0 ? CardType.Item : CardType.Follower)), Bowed = i % 2 == 0 };
                var tile = new CardTile(attached, true, _ => {}, _ => {}) { Position = new Vector2(x, y + i * 10), Size = Vector2.One * side, FaceHeight = side - 12, ReducedMotion = true };
                AddChild(tile); companions.Add(tile);
            }
            var front = new CardTile(card, true, _ => {}, _ => {}) { Position = new Vector2(x, y + count * 10), Size = Vector2.One * side, FaceHeight = side - 12, ReducedMotion = !_motion, PreviousBowed = _motion ? !bowed : bowed };
            AddChild(front); _cards.Add(front);
            var tokens = new StatTokens { CardAnchor = front, Companions = companions.ToArray(), Card = card, Projection = p => p, TooltipText = StatTokens.Explain(card, [card]), Allowed = () => true };
            if (_small) tokens.Projection = p => p * .43f;
            tokens.Pressed += () => _clicks++;
            AddChild(tokens); _tokens.Add(tokens);
        }
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (_clock < 1.3) return;
        try
        {
            foreach (var t in _tokens) t.PositionAtCard();
            if (_step == 0)
            {
                for (var i = 0; i < _tokens.Count; i++)
                {
                    var t = _tokens[i];
                    if (!GetViewportRect().Encloses(t.GetGlobalRect())) throw new Exception("Token clipped at viewport edge");
                    if (Math.Abs(t.Rotation) > .001 || t.GetGlobalRect().Position.Y < _cards[i].FaceCornersOnViewport().Max(p => p.Y)) throw new Exception("Token rotated or covers card art");
                    for (var j = i + 1; j < _tokens.Count; j++) if (t.GetGlobalRect().Intersects(_tokens[j].GetGlobalRect())) throw new Exception("Token pairs overlap");
                }
                var p = _tokens[1].GetGlobalRect().Position + new Vector2(_tokens[1].Diameter / 2, _tokens[1].Diameter / 2);
                Input.ParseInputEvent(new InputEventMouseMotion { Position = p });
                Input.ParseInputEvent(new InputEventMouseButton { Position = p, ButtonIndex = MouseButton.Left, Pressed = true });
                Input.ParseInputEvent(new InputEventMouseButton { Position = p, ButtonIndex = MouseButton.Left, Pressed = false });
                _tokens[2].Card = _tokens[2].Card with { Force = 14, Chi = 5 }; _tokens[2].QueueRedraw();
                if (_tooltip)
                {
                    _tip = _tokens[1]._MakeCustomTooltip(_tokens[1].TooltipText); AddChild(_tip);
                    _tip.Position = new Vector2(20, GetViewportRect().Size.Y - _tip.CustomMinimumSize.Y - 10);
                }
                _step++; _clock = 0; return;
            }
            if (_tip is not null && !GetViewportRect().Encloses(_tip.GetChild<Control>(0).GetGlobalRect()))
                throw new Exception("Tooltip text or panel clipped at viewport edge");
            if (_clicks != 1) throw new Exception("Native token click did not select its owner exactly once");
            if (_tokens[2].Card.Force != 14 || _tokens[2].Card.Chi != 5) throw new Exception("Updated view values not presented");
            RenderingServer.ForceDraw();
            GetViewport().GetTexture().GetImage().SavePng(_output);
            System.IO.File.WriteAllText(_output + ".json", JsonSerializer.Serialize(new { Cases = 8, Counts = new[] { 0, 1, 3, 6 }, Bowed = true, NativeClick = _clicks, UpdatedForce = 14, UpdatedChi = 5, Small = _small, Motion = _motion, TokenRects = _tokens.Select(t => t.GetGlobalRect().ToString()).ToArray() }));
            GD.Print("STAT_TOKEN_PASS: zero/one/three/six attachments, upright/bowed, zero and 1/2/3-digit values, view update, native click, no overlaps or clipping.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
