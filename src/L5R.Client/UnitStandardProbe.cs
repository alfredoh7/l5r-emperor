using Godot;
using L5R.Core;
using System.Text.Json;

namespace L5R.Client;

/// <summary>Isolated presentation fixtures; never loaded by match setup.</summary>
public partial class UnitStandardProbe : Control
{
    private readonly List<UnitStandard> _standards = [];
    private readonly List<CardTile> _faces = [];
    private string _output = "";
    private CardView[][]? _engineViews;
    private bool _comparison, _small, _bowed, _motion;
    private int _step, _clicks;
    private double _clock;
    private BattleTracker _tracker = null!;
    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--output=")) _output = arg[9..];
            if(arg.StartsWith("--engine-views=")) { _engineViews=JsonSerializer.Deserialize<CardView[][]>(System.IO.File.ReadAllText(arg[15..])); _comparison=true; }
            _comparison |= arg == "--comparison"; _small |= arg == "--small";
            _bowed |= arg == "--bowed"; _motion |= arg == "--motion";
        }
        GetWindow().GrabFocus(); Theme = Palette.MakeTheme();
        var screen = GetViewportRect().Size;
        AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://Art/navy-mat-v4.png"), Size = screen, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore });
        var title = Palette.Text("Unit Standard", 40, Palette.Gold, true); title.Position = new Vector2(48, 25); AddChild(title);
        var caption = Palette.Text(_engineViews is not null ? "Engine views · Bowed: 0 battle Force · Unbowed: 13 · Personality inlays stay 3 / 3" : _comparison ? "Illustrative values · Personality 2 → 0 · Follower 5 · Unit 7 → 5" : "Native layout cases · 0 / 1 / 3 / 6 attachments · upright and bowed", 22); caption.Position = new Vector2(48, 82); AddChild(caption);
        var def = PublishedContent.Snapshot.Cards.First(d => d.Type == CardType.Personality && d.Clan == "Crane");
        var count = _comparison ? 2 : 8;
        var columns = _comparison ? 2 : 4;
        var usable = screen.X - (_comparison ? 320 : 30);
        var cell = usable / columns;
        for (var n = 0; n < count; n++)
        {
            var attachments = _engineViews is not null ? _engineViews[n].Length-1 : _comparison ? 1 : new[] { 0, 1, 3, 6 }[n % 4];
            var side = _comparison ? Math.Min(465, screen.Y - 310) : _small ? 94 : Math.Min(196, (screen.Y - 190) / 2 - 100);
            var x = cell * (n % columns) + cell / 2 - side / 2 - 16;
            var y = 155 + n / columns * (screen.Y - 170) / 2;
            var p = new CardView("fixture-" + n, def, n % 2, Zone.Home, true, _bowed || n >= 4, null, null, _comparison ? n == 0 ? 2 : 0 : n, 3, UnitForce: _comparison ? n == 0 ? 7 : 5 : new[] { 0, 7, 12, 100 }[n % 4]);
            if (_engineViews is not null) p=_engineViews[n][0];
            else if (p.Bowed) p=p with { UnitForce=0 };
            var companions = new List<CardTile>();
            for (var a = attachments - 1; a >= 0; a--)
            {
                var f = p with { Id = $"a-{n}-{a}", AttachedTo = p.Id, Zone = Zone.Attached, UnitForce = null, Force = 5,
                    Definition = PublishedContent.Snapshot.Cards.First(d => d.Type == (_comparison || a % 2 == 1 ? CardType.Follower : CardType.Item)), Bowed = !_comparison && a % 2 == 0 };
                if (_engineViews is not null) f=_engineViews[n][a+1];
                var tile = Face(f, x, y + (attachments - 1 - a) * (_engineViews is not null ? 55 : 12), side); companions.Add(tile);
            }
            var front = Face(p, x, y + attachments * (_engineViews is not null ? 55 : _comparison ? 45 : 12), side);
            var standard = new UnitStandard { CardAnchor = front, Companions = companions.ToArray(), Card = p, Projection = p => p, Explanation = UnitStandard.ExplainUnit(p) };
            standard.Pressed += () => _clicks++;
            AddChild(standard); _standards.Add(standard);
        }
        if (_comparison)
        {
            var view = MatchEngine.CreatePublishedMatch(1).GetView(0) with { Phase = Phase.Battle, CurrentBattleProvince = 1, PriorityPlayerId = 0 };
            _tracker = new BattleTracker { Snapshot = view, Position = new Vector2(screen.X - 285, 160), Size = new Vector2(255, 620), ReducedMotion = !_motion };
            AddChild(_tracker);
        }
    }
    private CardTile Face(CardView c, float x, float y, float side)
    {
        var tile = new CardTile(c, true, _ => {}, _ => {}) { Position = new Vector2(x,y), Size = Vector2.One * side, FaceHeight = side - 12, AlignUnitRightEdge = true, ReducedMotion = !_motion, PreviousBowed = _motion ? !c.Bowed : c.Bowed };
        AddChild(tile); _faces.Add(tile); return tile;
    }
    public override void _Process(double delta)
    {
        _clock += delta; if (_clock < 1.4) return;
        try
        {
            foreach (var b in _standards)
            {
                b.PositionAtCard();
                if (!GetViewportRect().Encloses(b.BodyRectOnViewport())) throw new Exception("Standard clipped");
                var rect = b.BodyRectOnViewport();
                Vector2[] polygon = [rect.Position, new(rect.End.X,rect.Position.Y),rect.End,new(rect.Position.X,rect.End.Y)];
                foreach (var card in _faces)
                    if (Geometry2D.IntersectPolygons(polygon,card.FaceCornersOnViewport()).Count > 0) throw new Exception("Standard overlaps a card face");
                if (b.Card.UnitForce != b.CardAnchor.Card.UnitForce) throw new Exception("Standard detached from server view");
                // Issue #913: the help text follows the engine (each card counts on its own; Elite counts bowed), never "a bowed Personality zeroes the unit".
                var help = UnitStandard.ExplainUnit(b.Card);
                if (help.Contains("entire unit contribute 0") || !help.Contains("does not stop his unbowed Followers") || !help.Contains("Elite")) throw new Exception("Unit Force help contradicts the engine: " + help);
            }
            if (_step++ == 0)
            {
                var point = _standards[0].BodyRectOnViewport().GetCenter();
                Input.ParseInputEvent(new InputEventMouseMotion { Position=point });
                Input.ParseInputEvent(new InputEventMouseButton { Position=point,ButtonIndex=MouseButton.Left,Pressed=true });
                Input.ParseInputEvent(new InputEventMouseButton { Position=point,ButtonIndex=MouseButton.Left,Pressed=false });
                if (_comparison)
                {
                    var v=_tracker.Snapshot;
                    _tracker.Snapshot=v with { Phase=Phase.AttackDeclaration }; if(_tracker.Stage!=0) throw new Exception("Assignment phase mapping");
                    _tracker.Snapshot=v with { PriorityPlayerId=1-v.ViewerId }; if(_tracker.Status!="Opponent may act") throw new Exception("Opponent priority mapping");
                    _tracker.ResolutionMessage="Resolved fixture"; if(_tracker.Stage!=2 || _tracker.Status!="Resolved") throw new Exception("Resolution is not a player wait");
                    _tracker.ResolutionMessage=null; _tracker.Snapshot=v;
                }
                _clock=0; return;
            }
            if (_clicks!=1) throw new Exception("Native banner click did not select exactly once");
            Input.ParseInputEvent(new InputEventMouseMotion { Position=new Vector2(20,20) });
            RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(_output);
            GD.Print($"UNIT_STANDARD_PASS: {_standards.Count} cases; native click, no card occlusion or viewport clipping; bow={_bowed}, small={_small}, motion={_motion}; priority/assignment/resolution mapping checked in comparison.");
            GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetViewport().GetTexture().GetImage().SavePng(_output+".failure.png"); GetTree().Quit(1); }
    }
}
