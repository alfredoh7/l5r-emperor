shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform sampler2D fire_art : source_color, filter_linear_mipmap, repeat_disable;
uniform float phase=0.0;
uniform float motion=0.0;
uniform float seed=0.0;
uniform float strength=1.0;
uniform float light_energy=1.0;
void vertex() {
    MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0],INV_VIEW_MATRIX[1],INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);
}
void fragment() {
    float t=phase*motion+seed;
    float h=1.0-UV.y;
    vec2 uv=UV;
    // Advect the photographed ribbons upward; a fixed root prevents sliding
    // through the fire pan. Every time input comes from the presentation clock.
    uv.x+=motion*(sin(h*12.0-t*3.1+seed)*.027+sin(h*24.0+t*2.1)*.012)*h;
    uv.y=.9+(uv.y-.9)*(1.0+motion*(.09*sin(t*3.2)+.045*sin(t*6.7+seed)));
    uv.x=.5+(uv.x-.5)*(1.0+motion*.07*sin(t*2.4+h*8.0));
    if(seed>1.0) uv.x=1.0-uv.x;
    vec4 fire=texture(fire_art,uv);
    float edge=smoothstep(0.0,.055,UV.x)*smoothstep(0.0,.055,1.0-UV.x)
        *smoothstep(0.0,.065,UV.y)*smoothstep(0.0,.095,1.0-UV.y);
    float a=fire.a*edge*strength;
    if(a<.003) discard;
    ALBEDO=fire.rgb*a*mix(.88,light_energy,.55);
    ALPHA=1.0;
}
