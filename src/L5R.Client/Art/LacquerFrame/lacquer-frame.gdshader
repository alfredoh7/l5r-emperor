shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled;
#include "res://Art/candlelight.gdshaderinc"
uniform sampler2D source_art : filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D lantern_texture : filter_linear_mipmap, repeat_disable;
uniform bool project_to_table = false;
uniform vec2 screen_size = vec2(1920.0,1080.0);
uniform vec2 screen_center;
uniform float display_density = 1.0;
uniform vec2 camera_center;
uniform float camera_zoom = 1.0;
uniform float plane_pitch = .64;
uniform float plane_depth = .00065;
uniform vec2 environment_origin;
uniform vec2 environment_size = vec2(1800.0,1013.0383);
uniform vec2 left_lamp = vec2(.08,.88);
uniform vec2 right_lamp = vec2(.91,.16);
uniform float candle_phase = 0.0;
uniform float candle_motion = 0.0;
varying vec2 frame_uv;
varying float frame_height;
varying vec3 frame_normal;

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
    frame_uv = vec2(VERTEX.x/1.8+.5, VERTEX.z/1.01303828+.5);
    frame_height=VERTEX.y;
    frame_normal=NORMAL;
    POSITION=PROJECTION_MATRIX*MODELVIEW_MATRIX*vec4(VERTEX,1.0);
    if(project_to_table) {
        vec2 world = environment_origin+frame_uv*environment_size;
        vec2 p=(world-camera_center)*camera_zoom;
        float height=VERTEX.y*environment_size.x/1.8*camera_zoom;
        float w=1.0-plane_depth*p.y;
        vec2 offset=vec2(p.x,p.y*plane_pitch-height*sqrt(1.0-plane_pitch*plane_pitch));
        vec2 clip=(screen_center*2.0/screen_size-1.0)*w+offset*display_density*2.0/screen_size;
        // Real side/bevel geometry shares the card plane's projective mapping.
        // The environment plate is behind this surface; height orders bevels.
        POSITION=vec4(clip,(.80+frame_uv.y*.04+VERTEX.y*.2)*w,w);
    }
}
// Upright lanterns currently live in the painted background pass. Respect their
// existing alpha silhouette when the independent rail crosses behind them.
float lantern_coverage(vec2 source, float phase, vec2 pixel) {
    vec2 p=(environment_origin+environment_size*source-camera_center)*camera_zoom;
    float perspective=1.0/(1.0-plane_depth*p.y);
    vec2 base=screen_center+vec2(p.x,p.y*plane_pitch)*perspective*display_density;
    vec2 size=vec2(216.0,300.0)*camera_zoom*perspective*display_density;
    vec2 uv=(pixel-base)/size+vec2(.5,.94);
    float edge=min(min(uv.x,1.0-uv.x),min(uv.y,1.0-uv.y));
    float flame=exp(-dot((uv-vec2(.5,.598))/vec2(.035,.065),(uv-vec2(.5,.598))/vec2(.035,.065))*2.0);
    vec2 sample_uv=uv+vec2(sin(phase*3.2)*.0035,sin(phase*2.7+.8)*.0025)*flame*candle_motion;
    return texture(lantern_texture,sample_uv).a*smoothstep(0.0,.006,edge);
}
void fragment() {
    if(project_to_table) {
        // The cloth is a screen-projected plate, so reproduce its occlusion of
        // the below-surface inner wall. The native review uses real 3D depth.
        float inset=min(min(frame_uv.x,1.0-frame_uv.x)*1.8,min(frame_uv.y,1.0-frame_uv.y)*1.01303828);
        if(frame_height<-.0001 && inset>.0295) discard;
        if(dot(frame_normal,vec3(0.0,plane_pitch,sqrt(1.0-plane_pitch*plane_pitch)))<-.001) discard;
        if(max(lantern_coverage(left_lamp,candle_phase,SCREEN_UV*screen_size),lantern_coverage(right_lamp,candle_phase+1.8,SCREEN_UV*screen_size))>.018) discard;
    }
    vec3 wood=mix(texture(source_art,UV).rgb,texture(source_art,UV2).rgb,COLOR.r);
    if(project_to_table) {
        vec2 dl=(frame_uv-left_lamp)/vec2(.24,.31);
        vec2 dr=(frame_uv-right_lamp)/vec2(.24,.31);
        float warmth=exp(-dot(dl,dl)*1.5)*candle_energy(candle_phase,candle_motion)
                    +exp(-dot(dr,dr)*1.5)*candle_energy(candle_phase+1.8,candle_motion);
        wood+=vec3(.075,.033,.009)*warmth;
    }
    ALBEDO=painted_color(clamp(wood,vec3(0.0),vec3(1.0)));
}
