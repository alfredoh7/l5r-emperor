shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled, depth_draw_never, depth_test_disabled;
#include "res://Art/candlelight.gdshaderinc"
uniform sampler2D lantern_texture : filter_linear_mipmap, repeat_disable;
uniform sampler2D weathered_texture : filter_linear_mipmap, repeat_disable;
uniform vec2 source_uv;
uniform vec2 environment_origin;
uniform vec2 environment_size;
uniform vec2 screen_size;
uniform vec2 screen_center;
uniform float display_density = 1.0;
uniform vec2 camera_center;
uniform float camera_zoom = 1.0;
uniform float candle_phase;
uniform float candle_motion;
uniform float phase_offset;
vec3 painted_color(vec3 c) {
    vec3 target = pow((c+vec3(.055))/1.055,vec3(2.4));
    if (!OUTPUT_IS_SRGB) return mix(c/12.92,target,step(vec3(.04045),c));
    vec3 x=max(c,vec3(.03));
    for(int i=0;i<6;i++) {
        vec3 v=x*(x*(x*.305306011+.682171111)+.012522878);
        vec3 d=x*(x*.915918033+1.364342222)+.012522878;
        x-=(v-target)/d;
    }
    return x;
}
void vertex() {
    vec2 p=(environment_origin+environment_size*source_uv-camera_center)*camera_zoom;
    float perspective=1.0/(1.0-.00065*p.y);
    vec2 base=screen_center+vec2(p.x,p.y*.64)*perspective*display_density;
    vec2 size=vec2(216.0,300.0)*camera_zoom*perspective*display_density;
    vec2 pixel=base+(UV-vec2(.5,.94))*size;
    POSITION=vec4(pixel*2.0/screen_size-1.0,0.0,1.0);
}
void fragment() {
    float phase=candle_phase+phase_offset;
    vec2 uv=UV;
    float flame=exp(-dot((uv-vec2(.5,.598))/vec2(.035,.065),(uv-vec2(.5,.598))/vec2(.035,.065))*2.0);
    vec2 sample_uv=uv+vec2(sin(phase*3.2)*.0035,sin(phase*2.7+.8)*.0025)*flame*candle_motion;
    vec4 prop=texture(lantern_texture,sample_uv);
    vec3 worn=texture(weathered_texture,sample_uv).rgb;
    float chroma=max(worn.r,max(worn.g,worn.b))-min(worn.r,min(worn.g,worn.b));
    prop.rgb=mix(prop.rgb,worn,smoothstep(.06,.19,chroma)*.92);
    float emissive=smoothstep(.38,.85,prop.r)*smoothstep(.0,.2,prop.r-prop.b);
    prop.rgb*=1.0+emissive*(candle_energy(phase,candle_motion)-1.0)*.72;
    prop.rgb+=vec3(.080,.042,.010)*emissive;
    ALBEDO=painted_color(clamp(prop.rgb,vec3(0.0),vec3(1.0)));
    ALPHA=smoothstep(.018,.98,prop.a)*smoothstep(0.0,.006,min(min(uv.x,1.0-uv.x),min(uv.y,1.0-uv.y)));
}
