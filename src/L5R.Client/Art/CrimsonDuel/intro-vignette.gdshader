shader_type canvas_item;
// A broad directional wash keeps the named champion clear and the opponent in silhouette.
uniform float opponent_on_right : hint_range(0.0, 1.0) = 1.0;
uniform float strength : hint_range(0.0, 1.0) = 0.0;
void fragment() {
    float opposite = mix(smoothstep(0.40, 0.90, 1.0 - UV.x), smoothstep(0.40, 0.90, UV.x), opponent_on_right) * 0.70;
    float edge = smoothstep(0.38, 0.74, length(UV - vec2(0.5))) * 0.17;
    COLOR = vec4(0.015, 0.011, 0.017, min(0.82, opposite + edge) * strength);
}
