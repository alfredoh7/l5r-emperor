shader_type canvas_item;
void fragment() {
    vec4 sample_color = texture(TEXTURE, UV);
    float value = max(sample_color.r, max(sample_color.g, sample_color.b));
    sample_color.a *= smoothstep(0.015, 0.11, value);
    COLOR = sample_color;
}
