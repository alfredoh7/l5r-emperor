shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled;
uniform sampler2D artwork : filter_linear_mipmap_anisotropic, repeat_disable;

// Same painted-colour convention as the existing table: retain the supplied
// pixels under Compatibility's approximate output conversion. No added pattern.
vec3 painted_color(vec3 c) {
    vec3 target = pow((c + vec3(0.055)) / 1.055, vec3(2.4));
    if (!OUTPUT_IS_SRGB) return mix(c / 12.92, target, step(vec3(0.04045), c));
    vec3 x = max(c, vec3(0.03));
    for (int i = 0; i < 6; i++) {
        vec3 value = x * (x * (x * 0.305306011 + 0.682171111) + 0.012522878);
        vec3 derivative = x * (x * 0.915918033 + 1.364342222) + 0.012522878;
        x -= (value - target) / derivative;
    }
    return x;
}
void fragment() { ALBEDO = painted_color(texture(artwork, UV).rgb); }
