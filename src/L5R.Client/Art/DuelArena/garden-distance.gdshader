shader_type canvas_item;
render_mode unshaded;
uniform float clock = 0.0;
uniform vec2 screen_size = vec2(1600.0,900.0);
uniform float horizon = 250.0;

// Actual approved distant garden pixels. This is a flat backdrop, not geometry.
// Crop the central upper band only; the modeled garden occludes its lower edge.
void fragment() {
    float scale = .15 * screen_size.y / horizon;
    float width = scale * screen_size.x / screen_size.y * float(textureSize(TEXTURE,0).y) / float(textureSize(TEXTURE,0).x);
    vec2 uv = vec2(.5+(UV.x-.5)*width, min(UV.y * scale, 0.145));
    float water = smoothstep(0.12, 0.15, uv.y);
    uv.x += water * sin(uv.y * 600.0 + clock * 0.8) * 0.00045;
    vec3 art = texture(TEXTURE, uv).rgb;
    art = mix(art, vec3(.060,.077,.100), smoothstep(.105,.165,UV.y*scale));
    COLOR = vec4(art, 1.0);
}
