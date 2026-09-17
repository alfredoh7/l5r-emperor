shader_type canvas_item;
render_mode unshaded;
void fragment() {
    vec2 p = (UV - vec2(0.5)) * 2.0;
    float glow = exp(-dot(p, p) * 3.5) * (1.0 - smoothstep(0.6, 1.0, length(p)));
    COLOR = vec4(1.0, 0.59, 0.10, glow * 0.28) * COLOR;
}
