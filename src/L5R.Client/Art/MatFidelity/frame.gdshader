shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled;
uniform sampler2D artwork : filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D wood_grain : filter_linear_mipmap_anisotropic, repeat_enable;
varying vec3 local_position;
varying vec3 local_normal;
vec3 painted_color(vec3 c) {
    vec3 target = pow((c + vec3(0.055)) / 1.055, vec3(2.4));
    if (!OUTPUT_IS_SRGB) return mix(c / 12.92, target, step(vec3(0.04045), c));
    vec3 x = max(c, vec3(0.03));
    for (int i=0; i<6; i++) {
        vec3 value = x * (x * (x * 0.305306011 + 0.682171111) + 0.012522878);
        vec3 derivative = x * (x * 0.915918033 + 1.364342222) + 0.012522878;
        x -= (value - target) / derivative;
    }
    return x;
}
void vertex() { local_position = VERTEX; local_normal = NORMAL; }
void fragment() {
    vec3 top_art = texture(artwork, UV).rgb;
    float top = smoothstep(0.6, 0.98, abs(local_normal.y));
    // Longitudinal grain only for the newly exposed side walls. The top retains
    // the original narrow painted wooden rim, including all gold ornament.
    float along = mix(local_position.x, local_position.z, step(0.8, abs(local_normal.x)));
    float phase = local_position.y * 5200.0 + sin(along * 14.0) * 2.0;
    float grain = sin(phase) * (1.0 - smoothstep(0.8, 2.0, fwidth(phase)));
    vec3 walnut = texture(wood_grain, vec2(along * 1.8, local_position.y * 12.0)).rgb * 0.72 * (1.0 + grain * 0.06);
    float edge_light = 0.82 + max(dot(normalize(local_normal), normalize(vec3(-0.3, 1.0, 0.5))), 0.0) * 0.18;
    ALBEDO = painted_color(mix(walnut, top_art, top) * edge_light);
}
