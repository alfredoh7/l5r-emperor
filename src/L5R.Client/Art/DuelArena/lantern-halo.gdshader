shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;
void fragment() {
    float radius=length(UV-.5)*2.0;
    float halo=exp(-radius*radius*5.5)*smoothstep(1.0,.6,radius);
    ALBEDO=vec3(1.0,.37,.085)*halo*.27;
    ALPHA=halo*.38;
}
