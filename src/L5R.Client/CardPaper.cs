using Godot;

namespace L5R.Client;

/// <summary>Reversible presentation grading. Source scans and printed information stay intact.</summary>
public static class CardPaper
{
    private static readonly Shader PaperShader = new() { Code = """
        shader_type canvas_item;
        #include "res://Art/candlelight.gdshaderinc"
        uniform float patina = 1.0;
        uniform float candle_phase = 0.0;
        uniform float candle_motion = 1.0;
        uniform float table_light_strength = 1.0;
        uniform vec2 screen_size = vec2(1920.0,1080.0);
        uniform vec2 table_origin = vec2(0.0);
        uniform float table_scale = 1.0;
        uniform vec2 light_radius = vec2(750.0,850.0);
        uniform vec2 light_origin = vec2(-140.0,390.0);
        uniform vec2 light_origin_right = vec2(2500.0,390.0);
        float grain(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        void fragment() {
            // Godot already multiplies fragment COLOR by the default texture.
            vec4 ink = COLOR;
            float luminance = dot(ink.rgb, vec3(0.2126, 0.7152, 0.0722));
            float paper = smoothstep(0.48, 0.96, luminance);
            float edge = 1.0 - smoothstep(0.0, 0.035, min(min(UV.x, 1.0-UV.x), min(UV.y, 1.0-UV.y)));
            float fiber = (grain(floor(UV * vec2(700.0, 980.0))) - 0.5) * 0.032;
            vec3 muted_ink = mix(vec3(luminance), ink.rgb, .79) * .91;
            vec3 warm_paper = muted_ink * mix(vec3(0.985, 0.968, 0.93), vec3(0.955, 0.89, 0.755), paper * 0.64);
            warm_paper *= 1.0 - edge * 0.22;
            warm_paper += vec3(fiber * paper);
            vec3 result = mix(ink.rgb, warm_paper, patina);
            vec2 world = (SCREEN_UV*screen_size-table_origin)/table_scale;
            vec2 distance = (world - light_origin) / light_radius;
            float light = exp(-dot(distance, distance) * 1.5) * candle_energy(candle_phase,candle_motion);
            vec2 right = (world-light_origin_right)/light_radius;
            light += exp(-dot(right,right)*1.5) * candle_energy(candle_phase+1.8,candle_motion);
            result += vec3(0.090, 0.044, 0.013) * light * table_light_strength;
            COLOR = vec4(clamp(result, vec3(0.0), vec3(1.0)), ink.a);
        }
        """ };
    public static ShaderMaterial Table { get; } = Make(1);
    public static ShaderMaterial Reading { get; } = Make(.48f);
    public static ShaderMaterial FixedTable { get; } = Make(1);
    static CardPaper() => FixedTable.SetShaderParameter("candle_motion", 0f);
    private static ShaderMaterial Make(float amount)
    {
        var material = new ShaderMaterial { Shader = PaperShader };
        material.SetShaderParameter("patina", amount); return material;
    }
    public static void SetTableTransform(Vector2 origin, float scale, Vector2 screen)
    {
        Table.SetShaderParameter("screen_size", screen);
        Table.SetShaderParameter("table_origin", origin);
        Table.SetShaderParameter("table_scale", scale);
        // Reading popovers and the fixed hand have no moving environmental light.
        Reading.SetShaderParameter("candle_motion", 0f);
    }
    public static void SetLight(float time, bool animate, bool tableLamps = true)
    {
        foreach (var material in new[] { Table, Reading, FixedTable })
        {
            material.SetShaderParameter("table_light_strength", tableLamps ? 1f : 0f);
            material.SetShaderParameter("candle_phase", time);
            material.SetShaderParameter("candle_motion", animate && material == Table ? 1 : 0);
        }
    }
}
