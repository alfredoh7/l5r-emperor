using Godot;

namespace L5R.Client;

/// <summary>Visual-only card in transit. Receives permitted textures, never deck identities.</summary>
public partial class CardFlight : Control
{
    public required Vector2[] From { get; init; }
    public required Func<Vector2[]> Destination { get; init; }
    public required Texture2D Face { get; init; }
    public Texture2D? Back { get; init; }
    public bool FadeOnArrival { get; init; }
    public Action? Landed { get; init; }
    public double Delay { get; init; }
    public double Duration { get; init; } = .65;
    private double _clock;
    private bool _done;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ZIndex = 40; Material = CardPaper.FixedTable;
    }
    public override void _Process(double delta)
    {
        _clock += delta;
        if (_clock >= Delay + Duration) { Finish(); return; }
        QueueRedraw();
    }
    public void Finish()
    {
        if (_done) return;
        _done = true; Landed?.Invoke(); QueueFree();
    }
    public override void _Draw()
    {
        if (_clock < Delay || _done) return;
        var t = Math.Clamp((float)((_clock - Delay) / Duration), 0, 1);
        var ease = t*t*(3-2*t);
        var opacity = FadeOnArrival ? 1-Math.Clamp((t-.7f)/.3f,0,1) : 1;
        var end = Destination();
        var lift = Mathf.Sin(t * Mathf.Pi) * 42;
        var quad = From.Zip(end, (a,b) => a.Lerp(b,ease) - new Vector2(0,lift)).ToArray();
        var center = quad.Aggregate(Vector2.Zero,(a,b)=>a+b)/4;
        var flip = Back is null ? 1 : Math.Max(.035f,Math.Abs(Mathf.Cos(t*Mathf.Pi)));
        quad = quad.Select(p=>new Vector2(center.X+(p.X-center.X)*flip,p.Y)).ToArray();
        DrawColoredPolygon(quad.Select(p=>p+new Vector2(2,4+lift*.22f)).ToArray(),new Color(0,0,0,.28f*opacity));
        DrawColoredPolygon(quad,new Color(1,1,1,opacity),new[]{Vector2.Zero,Vector2.Right,Vector2.One,Vector2.Down},Back is not null && t<.5 ? Back : Face);
        DrawPolyline(quad.Append(quad[0]).ToArray(),new Color(.43f,.37f,.27f,.7f*opacity),.7f,true);
    }
}
