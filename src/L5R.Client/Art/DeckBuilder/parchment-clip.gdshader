shader_type canvas_item;
render_mode unshaded;
uniform vec2 panel_size = vec2(700.0, 220.0);
varying vec2 local_position;
void vertex() { local_position = VERTEX; }
void fragment() {
    vec2 q = abs(local_position - panel_size * 0.5) - (panel_size * 0.5 - vec2(13.0));
    float distance = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - 13.0;
    COLOR.a *= 1.0 - smoothstep(-0.75, 0.75, distance);
}
