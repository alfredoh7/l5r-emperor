using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Softly illuminated brass rail. Presentation never advances game state.</summary>
public partial class PhaseTrack : Control
{
    private readonly string[] _names = ["STRAIGHTEN", "EVENTS", "ACTION", "ATTACK", "DYNASTY", "END"];
    private readonly float[] _weights = new float[6];
    private int _active = -1;
    private float _time;
    public Label PriorityLabel { get; private set; } = null!;
    private Color _priorityColor = Palette.Crane;
    public void PresentActions(MatchView view, ActionOption[] actions, bool available)
    {
        var pass = actions.FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
        var actor = view.Players.Single(p => p.Id == view.PriorityPlayerId);
        var own = view.PriorityPlayerId == view.ViewerId;
        PriorityLabel.Text = view.WinnerId is not null ? "Match complete" : own ? (pass is null ? "Your decision" : "You may act or pass") : $"{actor.Name} · Opponent may act";
        PriorityLabel.TooltipText = view.PendingChoice?.Prompt ?? PriorityLabel.Text;
        _priorityColor = own ? new Color("9ceafa") : new Color("efb967");
        PriorityLabel.Modulate = _priorityColor;
        UpdateArt();
    }
    private TextureRect _plate = null!;
    public TurnIndicator TurnCue { get; } = new();
    private ShaderMaterial _art = null!;
    private readonly List<Button> _areas = [];
    // Actual medallion positions in the selected bitmap; labels/tooltips follow the artwork.
    private readonly float[] _centers = [190f / 1774, 471f / 1774, 748f / 1774, 1031f / 1774, 1319f / 1774, 1596f / 1774];
    public bool ReducedMotion { get; set; }
    public static int SegmentFor(Phase phase) => phase switch
    {
        Phase.Straighten => 0,
        Phase.Events => 1,
        Phase.Action => 2,
        Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense or Phase.BattleSelection or Phase.Battle => 3,
        Phase.Dynasty or Phase.DynastyDiscard => 4,
        Phase.End => 5,
        Phase.Finished => -1,
        _ => -1
    };
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _art = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/phase-art.gdshader") };
        _art.SetShaderParameter("blank_lacquer", GD.Load<Texture2D>("res://Art/lacquer-phase-plate.png"));

        _plate = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Art/six-phase-reference.png"),
            Material = _art,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            ShowBehindParent = true
        };
        AddChild(_plate);
        PriorityLabel = Palette.Text("", 19, Palette.Paper); PriorityLabel.HorizontalAlignment = HorizontalAlignment.Center;
        PriorityLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; PriorityLabel.MouseFilter = MouseFilterEnum.Stop; AddChild(PriorityLabel);
        AddChild(TurnCue); TurnCue.Hide(); // Turn information is presented in the player plaques.
        CreateHelp();
        Resized += FitArtwork;
        FitArtwork();
    }
    // The approved raster has an outer mounting tab; align the lacquer frame itself.
    private const float FrameRight = 1748f / 1774f;
    public static float WidthForFrame(float width) => width / FrameRight + 34;
    public Rect2 PlateBounds { get { var bounds = _plate.GetGlobalRect(); return new(bounds.Position, new(bounds.Size.X * FrameRight, bounds.Size.Y)); } }
    private void FitArtwork()
    {
        var width = Math.Max(0, Size.X - 34);
        _plate.Position = new Vector2((Size.X - width - 34) / 2, 0);
        _plate.Size = new Vector2(width, Math.Min(82, width * 154 / 1774));
        _art.SetShaderParameter("plate_size", _plate.Size);
        for (var i = 0; i < _areas.Count; i++)
        {
            var left = i == 0 ? 0 : (_centers[i - 1] + _centers[i]) / 2;
            var right = i == 5 ? 1 : (_centers[i] + _centers[i + 1]) / 2;
            _areas[i].Position = _plate.Position + new Vector2(left * width, 0);
            _areas[i].Size = new Vector2((right - left) * width, Math.Min(_plate.Size.Y, 94));
        }
        PriorityLabel.Position = new Vector2(_plate.Position.X + 20, _plate.Size.Y + 3);
        PriorityLabel.Size = new Vector2(Math.Max(100, width - 40), 32);
        TurnCue.Position = new Vector2(320, 94);
        TurnCue.Size = new Vector2(Math.Max(360, Size.X - 320), 40);
        HelpButton.Position = new Vector2(_plate.Position.X + width + 3, Math.Max(0, _plate.Size.Y / 2 - 14));
        HelpButton.Size = new Vector2(28, 28);
        PlaceHelp();
        QueueRedraw();
    }
    public void Present(MatchView view)
    {
        TurnCue.Present(view);
        TurnCue.ReducedMotion = ReducedMotion;
        _battleWithinAction = view.WinnerId is null && view.Phase != Phase.Finished && view.ActionPhaseTurnPlayerId is not null;
        var next = SegmentFor(_battleWithinAction ? Phase.Action : view.Phase);
        if (_active < 0 && next >= 0) _weights[next] = 1;
        _active = next;
        HelpButton.Disabled = next < 0;
        _turnContext = $"Turn {view.TurnNumber} · {view.Players.Single(p => p.Id == (view.ActionPhaseTurnPlayerId ?? view.ActivePlayerId)).Clan}";
        if (_areas.Count == 0)
            for (var i = 0; i < _names.Length; i++)
            {
                var index = i;
                var area = new Button { Flat = true, MouseFilter = MouseFilterEnum.Stop, FocusMode = FocusModeEnum.All, AccessibilityName = _names[i] + " phase help", AccessibilityDescription = SegmentHelp(i) };
                foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) area.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
                area.MouseEntered += () => { _hoverIndex = index; _hoverTime = 0; };
                area.MouseExited += () => { if (_hoverIndex == index) _hoverIndex = -1; if (!area.HasFocus()) HideHelp(); };
                area.FocusEntered += () => ShowHelp(index);
                area.FocusExited += HideHelp;
                area.Pressed += () => ShowHelp(index);
                area.GuiInput += e => HelpInput(e, index, area);
                AddChild(area); _areas.Add(area);
            }
        if (_help.Visible) ShowHelp(_helpIndex);
        FitArtwork();
        UpdateArt();
    }
    public override void _Process(double delta)
    {
        TurnCue.ReducedMotion = ReducedMotion;
        if (_hoverIndex >= 0) { _hoverTime += (float)delta; if (_hoverTime >= .25f && !_help.Visible) ShowHelp(_hoverIndex); }
        PlaceHelp();
        if (ReducedMotion && _weights.Select((weight, i) => weight == (i == _active ? 1 : 0)).All(settled => settled)) return;
        _time += (float)delta;
        for (var i = 0; i < 6; i++)
            _weights[i] = ReducedMotion ? (i == _active ? 1 : 0) : Mathf.MoveToward(_weights[i], i == _active ? 1 : 0, (float)delta / .45f);
        UpdateArt();
    }
    public void PresentLight(float time, bool animate)
    {
        _time = time;
        ReducedMotion = !animate;
        TurnCue.ReducedMotion = ReducedMotion;
        PlaceHelp();
        if (ReducedMotion) for (var i = 0; i < 6; i++) _weights[i] = i == _active ? 1 : 0;
        UpdateArt();
    }
    private void UpdateArt()
    {
        if (_art is null) return;

        _art.SetShaderParameter("phase_weights", new Vector4(
            Mathf.SmoothStep(0, 1, _weights[0]), Mathf.SmoothStep(0, 1, _weights[1]),
            Mathf.SmoothStep(0, 1, _weights[2]), Mathf.SmoothStep(0, 1, _weights[3])));
        _art.SetShaderParameter("phase_tail", new Vector2(Mathf.SmoothStep(0, 1, _weights[4]), Mathf.SmoothStep(0, 1, _weights[5])));
        _art.SetShaderParameter("glow_energy", ReducedMotion ? 1 : 1 + .045f * Mathf.Sin(_time * Mathf.Tau / 5.8f));
        QueueRedraw();
    }
    public int ActiveSegment => _active;
    public IReadOnlyList<Button> HelpRegions => _areas;
    public bool HelpVisible => _help.Visible;
    public string HelpText => _helpBody.Text;
    public override void _Draw()
    {
        if (_plate is null) return;
        foreach (var area in _areas.Where(a => a.HasFocus())) DrawRect(new Rect2(area.Position + Vector2.One * 3, area.Size - Vector2.One * 6), Palette.Gold with { A = .6f }, false, 1);
        var fontSize = Math.Clamp((int)(_plate.Size.X / 57), 12, 22);
        while (fontSize > 8 && _names.Any(n => Palette.Title.GetStringSize(n, fontSize: fontSize).X > _plate.Size.X * .14f)) fontSize--;
        var baseline = _plate.Size.Y * .48f;
        for (var i = 0; i < 6; i++)
        {
            var color = new Color("bca477").Lerp(new Color("f6dbad"), Mathf.SmoothStep(0, 1, _weights[i]));
            var labelWidth = _plate.Size.X * .15f;
            var position = new Vector2(_plate.Position.X + _centers[i] * _plate.Size.X - labelWidth / 2, baseline);
            DrawString(Palette.Title, position + new Vector2(0, 2), _names[i], HorizontalAlignment.Center, labelWidth, fontSize, new Color(0, 0, 0, .8f));
            DrawString(Palette.Title, position, _names[i], HorizontalAlignment.Center, labelWidth, fontSize, color);
        }
    }
}
