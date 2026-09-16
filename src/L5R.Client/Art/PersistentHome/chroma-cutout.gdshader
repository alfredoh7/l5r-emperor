shader_type canvas_item;
render_mode unshaded;
void fragment() {
    vec4 pixel = texture(TEXTURE, UV);
    float key = min(pixel.r, pixel.b) - pixel.g;
    float mask = 1.0 - smoothstep(0.18, 0.48, key);
    COLOR = vec4(pixel.rgb, pixel.a * mask) * COLOR;
}
