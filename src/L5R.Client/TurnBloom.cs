using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Observes public turn ownership; it never gates input or changes priority.</summary>
public sealed class TurnBloomGate
{
    private string? _match;
    private int _turn = -1, _owner = -1, _viewer = -1;
    public void Reset() { _match = null; _turn = _owner = _viewer = -1; }
    public bool Observe(MatchView view, string match)
    {
        bool fresh = _match != match;
        var turnOwner = view.ActionPhaseTurnPlayerId ?? view.ActivePlayerId;
        bool changed = fresh || _turn != view.TurnNumber || _owner != turnOwner;
        // Changing a sandbox viewing seat is not a new turn.
        bool seatChanged = !fresh && _viewer != view.ViewerId;
        _match = match; _turn = view.TurnNumber; _owner = turnOwner; _viewer = view.ViewerId;
        return changed && !seatChanged && view.WinnerId is null && turnOwner == view.ViewerId;
    }
}

/// <summary>The approved Ink Bloom bitmap and center-out absorption, in screen space.</summary>
public partial class TurnBloom : Control
{
    public bool ReducedMotion { get; set; }
    public Action? Observe { get; set; }
    public float Elapsed { get; private set; } = 4;
    public bool Playing => Elapsed < (ReducedMotion ? 2.6f : 3.5f);
    public bool ManualTime { get; set; }
    public int PlayCount { get; private set; }
    private TextureRect _ink = null!;
    private ShaderMaterial _material = null!;
    private Font _font = null!;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; FocusMode = FocusModeEnum.None;
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/turn-ink-bloom.gdshader") };
        _ink = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Art/turn-ink-bloom.png"),
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Material = _material,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_ink); _ink.ZIndex = -1;
        _font = new FontVariation { BaseFont = GD.Load<FontFile>("res://Art/Fonts/WeatheredQuill-Regular.ttf"), SpacingGlyph = -1 };
        SetTime(4);
    }
    public void Play() { PlayCount++; SetTime(0); }
    public void Cancel() { if (Elapsed != 4) SetTime(4); }
    public void SetTime(float time)
    {
        Elapsed = time; Visible = Playing;
        if (_material is null) return;
        _material.SetShaderParameter("elapsed", Elapsed);
        _material.SetShaderParameter("reduced_motion", ReducedMotion);
        _ink.Size = Size;
        QueueRedraw();
    }
    public override void _Process(double delta)
    {
        Observe?.Invoke();
        if (!ManualTime && Playing) SetTime(Elapsed + Math.Min((float)delta, .1f));
    }
    private static float Ease(float a, float b, float t) { var x = Math.Clamp((t - a) / (b - a), 0, 1); return x * x * (3 - 2 * x); }
    public override void _Draw()
    {
        if (_font is null || !Playing) return;
        var opacity = ReducedMotion ? Ease(.08f, .22f, Elapsed) * (1 - Ease(2.4f, 2.6f, Elapsed))
            : Ease(.52f, .92f, Elapsed) * (1 - Ease(2.5f, 3.15f, Elapsed));
        int size = Math.Max(18, (int)Math.Round(Size.X * 56 / 940));
        var width = _font.GetStringSize("YOUR TURN", fontSize: size).X;
        // Actual glyph bounds, not the font's unusually large line-box padding.
        var textAt = new Vector2((Size.X - width) / 2, Size.Y / 2 + size * .37f - Size.X * 6 / 940);
        DrawStringOutline(_font, textAt, "YOUR TURN", fontSize: size, size: Math.Max(1, size / 10), modulate: new Color(1, .74f, .34f, opacity * .07f));
        DrawString(_font, textAt, "YOUR TURN", fontSize: size, modulate: new Color("efdfbd") { A = opacity });
    }
}
