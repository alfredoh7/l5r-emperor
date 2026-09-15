shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform vec4 glow_colour : source_color = vec4(1.0,0.35,0.065,0.20);
void vertex() {
    float sx = length(MODEL_MATRIX[0].xyz);
    float sy = length(MODEL_MATRIX[1].xyz);
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx,
        INV_VIEW_MATRIX[1] * sy, INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    float r = length((UV - vec2(0.5)) * 2.0);
    float glow = exp(-r*r*7.0) * smoothstep(1.0,0.78,r);
    ALBEDO = glow_colour.rgb;
    ALPHA = glow_colour.a * glow;
}
