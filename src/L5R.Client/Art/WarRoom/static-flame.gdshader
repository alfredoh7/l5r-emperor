shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform float seed=0.0;
void vertex() {
    float s=length(MODEL_MATRIX[0].xyz);
    MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*s,INV_VIEW_MATRIX[1]*s,INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);
}
void fragment() {
    float h=1.0-UV.y;
    float x=(UV.x-.5) + sin(h*8.0+seed)*.045*h;
    float w=.32*pow(max(0.0,1.0-h),.75)+.005;
    float edge=1.0-smoothstep(w*.50,w,abs(x));
    float a=edge*smoothstep(0.0,.06,h)*(1.0-smoothstep(.92,1.0,h));
    float core=(1.0-smoothstep(w*.1,w*.52,abs(x)))*(1.0-smoothstep(.55,.80,h));
    ALBEDO=mix(vec3(1.0,.21,.014),vec3(1.0,.77,.23),core);
    ALPHA=a*.85;
}
