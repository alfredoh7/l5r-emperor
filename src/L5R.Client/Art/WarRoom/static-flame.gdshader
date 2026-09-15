shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform float seed=0.0;
uniform float phase=0.0;
uniform float motion=0.0;
uniform float light_energy=1.0;
uniform float turbulence=1.0;
void vertex() {
    float s=length(MODEL_MATRIX[0].xyz);
    MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*s,INV_VIEW_MATRIX[1]*s,INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);
}
void fragment() {
    // Keep the wick anchored; movement grows toward the tip. Explicit phase
    // (never shader TIME) keeps reduced motion and recorded playback stable.
    float h=1.0-UV.y;
    float stretch=1.0-motion*(.065+.040*sin(phase*3.7+seed)+.020*sin(phase*6.1+seed*.7));
    if(turbulence>1.0) {
        stretch=1.0-motion*(.20+.15*sin(phase*2.7+seed)+.04*sin(phase*5.3+seed*1.3));
    }
    h/=stretch;
    float drift=motion*turbulence*(.052*sin(phase*2.4+seed)+.023*sin(phase*5.9+seed*.4));
    if(turbulence>1.0) drift+=motion*.11*sin(phase*3.1+seed+h*5.0);
    float x=(UV.x-.5) + sin(h*8.0+seed+phase*motion*2.2)*.045*h + drift*h*h;
    float w=.32*pow(max(0.0,1.0-h),.75)+.005;
    if(turbulence>1.0) w*=1.0+motion*.16*sin(h*13.0-phase*4.8+seed);
    float edge=1.0-smoothstep(w*.50,w,abs(x));
    float a=edge*smoothstep(0.0,.06,h)*(1.0-smoothstep(.92,1.0,h));
    float core=(1.0-smoothstep(w*.1,w*.52,abs(x)))*(1.0-smoothstep(.55,.80,h));
    ALBEDO=mix(vec3(1.0,.21,.014),vec3(1.0,.77,.23),core);
    ALPHA=a*.85*mix(1.0,light_energy,.55);
}
