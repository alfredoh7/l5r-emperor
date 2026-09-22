shader_type spatial;
render_mode cull_disabled, depth_prepass_alpha;
uniform sampler2D albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
varying vec3 world_position;
varying vec3 world_normal;
void vertex() {
    world_position=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
    world_normal=normalize(MODEL_NORMAL_MATRIX*NORMAL);
}
void fragment() {
    vec3 top=texture(albedo,UV).rgb*vec3(.35,.40,.22);
    // Vertical cut faces use their height, avoiding stretched green top-surface UVs.
    vec3 edge=texture(albedo,vec2(world_position.x+world_position.z,world_position.y)*.7).rgb;
    edge=vec3(dot(edge,vec3(.299,.587,.114)))*vec3(.18,.17,.15);
    ALBEDO=mix(edge,top,smoothstep(.35,.8,abs(world_normal.y)));
    ROUGHNESS=.98;
    ALPHA=1.0-smoothstep(14.2,19.5,-world_position.z);
}
