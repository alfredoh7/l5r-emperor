using Godot;

namespace L5R.Client;

/// <summary>Issue #875: names of shader parameters written every frame. A string passed to SetShaderParameter is turned into a new StringName (and marshalled) on each call; these are built once.</summary>
internal static class ShaderParameters
{
    public static readonly StringName CandlePhase = "candle_phase", CandleMotion = "candle_motion", TableLightStrength = "table_light_strength";
}
