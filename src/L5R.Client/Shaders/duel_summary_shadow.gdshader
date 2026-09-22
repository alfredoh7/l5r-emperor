shader_type canvas_item;
render_mode unshaded;
void fragment() {
    vec2 p = (UV - vec2(0.5)) * 2.0;
    float core = 1.0 - smoothstep(0.42, 1.0, length(p * vec2(0.87, 0.91)));
    COLOR = vec4(0.004, 0.006, 0.009, core * 0.80) * COLOR;
}
