shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform float phase=0.0;
uniform float motion=0.0;
uniform float seed=0.0;
uniform float light_energy=1.0;
varying float life;
void vertex() {
    life=fract(phase*.22+seed*.173);
    vec3 offset=vec3(sin(seed*5.3+life*4.0)*(.013+life*.029),.04+life*.22,-life*.045);
    mat4 model=MODEL_MATRIX;
    model[3].xyz+=offset;
    MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0],INV_VIEW_MATRIX[1],INV_VIEW_MATRIX[2],model[3]);
}
void fragment() {
    float glow=exp(-dot(UV-.5,UV-.5)*24.0);
    float a=glow*smoothstep(0.0,.13,life)*(1.0-smoothstep(.45,.85,life));
    if(a<.02) discard;
    ALBEDO=vec3(1.0,.32,.055)*a*.65;
    ALPHA=1.0;
}
