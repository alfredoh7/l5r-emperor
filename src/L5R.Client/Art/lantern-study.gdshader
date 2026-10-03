shader_type canvas_item;
render_mode unshaded;

// Direct port of the approved B6 browser lighting. Pixel coordinates describe
// the immutable 1672 x 941 plate, independently of window size or letterboxing.
uniform sampler2D clean_plate : filter_linear, repeat_disable;
uniform float clock = 0.0;
uniform float strength = 0.85;
uniform float motion = 1.0;
uniform bool submenu = false;
uniform int selection = 0;
uniform float emphasis = 1.0;
const vec2 ART_SIZE = vec2(1672.0, 941.0);

float hash(float n) { return fract(sin(n * 127.1 + 311.7) * 43758.5453); }
float noise(float t) {
    float n = floor(t); float f = fract(t);
    f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return mix(hash(n), hash(n + 1.0), f) * 2.0 - 1.0;
}
float ellipse(vec2 uv, vec2 center, vec2 radius) {
    vec2 p = (uv - center) / radius;
    return exp(-2.6 * dot(p, p));
}
float patch(vec2 p, vec4 bounds, float feather) {
    vec2 leading = smoothstep(bounds.xy, bounds.xy + vec2(feather), p);
    vec2 trailing = 1.0 - smoothstep(bounds.zw - vec2(feather), bounds.zw, p);
    return leading.x * leading.y * trailing.x * trailing.y;
}
void fragment() {
    vec2 p = UV * ART_SIZE;
    vec3 color = texture(TEXTURE, UV).rgb;
    if (submenu) {
        // Only the menu area uses the derived clean plate. The sigils, room,
        // title and border always retain the original approved source pixels.
        color = mix(color, texture(clean_plate, UV).rgb,
                    patch(p, vec4(1030.0, 348.0, 1490.0, 809.0), 24.0));
    } else if (selection > 0) {
        // Deactivate the baked Play selection; reuse its real brush stroke at
        // the active entry. Transparent native Buttons own input and focus.
        float old_gold = smoothstep(0.002, 0.04, color.r - max(color.g, color.b) * 1.18);
        float play_stem = patch(p, vec4(1111.0, 414.0, 1126.0, 432.0), 2.0);
        color = mix(color, texture(clean_plate, UV).rgb,
                    patch(p, vec4(1091.0, 418.0, 1476.0, 443.0), 2.0) * old_gold * (1.0 - play_stem));
        float ink = smoothstep(0.20, 0.58, max(color.r, max(color.g, color.b)));
        float play = patch(p, vec4(1105.0, 366.0, 1238.0, 433.0), 2.0) * ink;
        color = mix(color, vec3(color.r, color.r * 0.92, color.r * 0.79), play);
        float offset = 79.0 + float(selection - 1) * 69.0;
        float selected_ink = patch(p, vec4(1094.0, 364.0 + offset, 1474.0, 418.0 + offset), 2.0) * ink;
        color = mix(color, vec3(color.r, color.r * 0.76, color.r * 0.41), selected_ink * emphasis);
        vec3 stroke = texture(TEXTURE, (p - vec2(0.0, offset)) / ART_SIZE).rgb;
        float gold = clamp((stroke.r - max(stroke.g, stroke.b) * 1.18) * 7.0, 0.0, 1.0);
        gold *= 1.0 - patch(p - vec2(0.0, offset), vec4(1111.0, 414.0, 1126.0, 432.0), 2.0);
        float brush = patch(p, vec4(1091.0, 417.0 + offset, 1476.0, 443.0 + offset), 2.0);
        color = mix(color, stroke, gold * brush * emphasis);
    }
    float slow = sin(clock * 0.52 + 0.22 * sin(clock * 0.11));
    float flame = (0.72 * slow + 0.25 * noise(clock * 0.35 + 11.0)
                    + 0.03 * noise(clock * 0.85 + 47.0)) * strength * motion;
    float brightness = dot(color, vec3(0.2126, 0.7152, 0.0722));
    float warm = smoothstep(0.06, 0.32, color.r - color.b);
    float paper = ellipse(UV, vec2(0.017, 0.611), vec2(0.043, 0.17))
                    * smoothstep(0.21, 0.64, brightness) * warm;
    float nearby = ellipse(UV, vec2(0.04, 0.57), vec2(0.25, 0.58));
    float on_screen = smoothstep(0.071, 0.095, UV.x) * (1.0 - smoothstep(0.87, 0.925, UV.y));
    float facet = 1.0 - 0.08 * smoothstep(0.266, 0.273, UV.x)
                    - 0.10 * smoothstep(0.491, 0.498, UV.x)
                    - 0.10 * smoothstep(0.700, 0.707, UV.x);
    float screen = ellipse(UV, vec2(0.21, 0.49), vec2(0.67, 0.84)) * on_screen * facet;
    float floor_light = ellipse(UV, vec2(0.31, 0.941), vec2(0.48, 0.11))
                    * mix(0.6, 1.0, smoothstep(0.10, 0.52, brightness));
    float weight = 0.65 * paper + 0.75 * nearby + 1.85 * screen + 1.05 * floor_light;
    float menu_ink = smoothstep(0.58, 0.64, UV.x) * smoothstep(0.34, 0.72, brightness);
    weight *= 1.0 - menu_ink;
    vec3 linear = pow(max(color, vec3(0.0)), vec3(2.2));
    vec3 gain = exp2(flame * weight * vec3(1.0, 0.85, 0.60));
    linear = (linear * gain) / (vec3(1.0) + linear * (gain - vec3(1.0)));
    COLOR = vec4(pow(max(linear, vec3(0.0)), vec3(1.0 / 2.2)), 1.0);
}
