shader_type spatial;
render_mode cull_disabled, depth_prepass_alpha;
uniform sampler2D artwork : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
void fragment() {
    vec3 art=texture(artwork,UV).rgb;
    float value=max(art.r,max(art.g,art.b));
    ALPHA=smoothstep(.06,.16,value);
    ALBEDO=art;
    METALLIC=.3;
    ROUGHNESS=.42;
    EMISSION=art*.15;
}
