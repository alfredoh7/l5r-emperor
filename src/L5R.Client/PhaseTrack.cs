using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Softly illuminated brass rail. Presentation never advances game state.</summary>
public partial class PhaseTrack : Control
{
    private readonly string[] _names = ["ACTION", "BATTLE", "DYNASTY", "END"];
    private readonly float[] _weights = new float[4];
    private int _active = -1;
    private float _time;
    private TextureRect _plate = null!;
    public TurnIndicator TurnCue { get; } = new();
    private ShaderMaterial _art = null!;
    private readonly List<TooltipRegion> _areas = [];
    // Actual medallion positions in the selected bitmap; labels/tooltips follow the artwork.
    private readonly float[] _centers = [230f/2066, 778f/2066, 1293f/2066, 1791f/2066];
    public bool ReducedMotion { get; set; }
    public static int SegmentFor(Phase phase) => phase switch
    {
        Phase.Action => 0, Phase.Dynasty or Phase.DynastyDiscard => 2,
        Phase.End or Phase.Finished => 3, _ => 1
    };
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _art = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/phase-art.gdshader") };
        _art.SetShaderParameter("lit_reference", GD.Load<Texture2D>("res://Art/reference-lit-rail.png"));
        _plate = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Art/lacquer-phase-plate.png"), Material = _art,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true
        };
        AddChild(_plate);
        AddChild(TurnCue);
        Resized += FitArtwork;
        FitArtwork();
    }
    private void FitArtwork()
    {
        var width = Math.Min(Size.X, 1260);
        _plate.Position = new Vector2((Size.X-width)/2,0);
        _plate.Size = new Vector2(width, width * 166/2066);
        _art.SetShaderParameter("plate_size", _plate.Size);
        for (var i=0;i<_areas.Count;i++)
        {
            var left = i==0 ? 0 : (_centers[i-1]+_centers[i])/2;
            var right = i==3 ? 1 : (_centers[i]+_centers[i+1])/2;
            _areas[i].Position = _plate.Position+new Vector2(left*width,0);
            _areas[i].Size = new Vector2((right-left)*width,Math.Min(_plate.Size.Y,94));
        }
        TurnCue.Position = new Vector2(320, 94);
        TurnCue.Size = new Vector2(Math.Max(360, Size.X - 320), 40);
        QueueRedraw();
    }
    public void Present(MatchView view)
    {
        TurnCue.Present(view);
        TurnCue.ReducedMotion = ReducedMotion;
        var next = SegmentFor(view.Phase);
        if (_active < 0) _weights[next] = 1;
        _active = next;
        foreach (var child in GetChildren().OfType<TooltipRegion>()) { RemoveChild(child); child.QueueFree(); }
        _areas.Clear();
        for (var i = 0; i < _names.Length; i++)
        {
            var area = new TooltipRegion { MouseFilter = MouseFilterEnum.Stop };
            area.TooltipText = $"Turn {view.TurnNumber} · {view.Players[view.ActivePlayerId].Clan}\n{_names[i]}\n" + (i switch
            {
                0 => "Eligible cards straighten and Dynasty cards reveal automatically before Action.\n" + RulesHelp.PhaseGuide(Phase.Action),
                1 => RulesHelp.Topic("battle"), 2 => RulesHelp.PhaseGuide(Phase.Dynasty), _ => RulesHelp.PhaseGuide(Phase.End)
            });
            AddChild(area); _areas.Add(area);
        }
        FitArtwork();
        UpdateArt();
    }
    public override void _Process(double delta)
    {
        TurnCue.ReducedMotion = ReducedMotion;
        if (ReducedMotion && _weights.Select((weight, i) => weight == (i == _active ? 1 : 0)).All(settled => settled)) return;
        _time += (float)delta;
        for (var i = 0; i < 4; i++)
            _weights[i] = ReducedMotion ? (i == _active ? 1 : 0) : Mathf.MoveToward(_weights[i], i == _active ? 1 : 0, (float)delta / .45f);
        UpdateArt();
    }
    public void PresentLight(float time, bool animate)
    {
        _time = time;
        ReducedMotion = !animate;
        TurnCue.ReducedMotion = ReducedMotion;
        if (ReducedMotion) for (var i = 0; i < 4; i++) _weights[i] = i == _active ? 1 : 0;
        UpdateArt();
    }
    private void UpdateArt()
    {
        if (_art is null) return;
        _art.SetShaderParameter("phase_weights", new Vector4(
            Mathf.SmoothStep(0,1,_weights[0]), Mathf.SmoothStep(0,1,_weights[1]),
            Mathf.SmoothStep(0,1,_weights[2]), Mathf.SmoothStep(0,1,_weights[3])));
        _art.SetShaderParameter("glow_energy", ReducedMotion ? 1 : 1+.045f*Mathf.Sin(_time*Mathf.Tau/5.8f));
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (_plate is null) return;
        var fontSize = 30;
        var baseline = _plate.Size.Y*.40f;
        for (var i=0;i<4;i++)
        {
            var color = new Color("bca477").Lerp(new Color("f6dbad"),Mathf.SmoothStep(0,1,_weights[i]));
            var labelWidth = _plate.Size.X*.22f;
            var position = new Vector2(_plate.Position.X+_centers[i]*_plate.Size.X-labelWidth/2,baseline);
            DrawString(Palette.Title,position+new Vector2(0,2),_names[i],HorizontalAlignment.Center,labelWidth,fontSize,new Color(0,0,0,.8f));
            DrawString(Palette.Title,position,_names[i],HorizontalAlignment.Center,labelWidth,fontSize,color);
        }
    }
}
