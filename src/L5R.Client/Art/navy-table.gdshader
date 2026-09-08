shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled;
#include "res://Art/candlelight.gdshaderinc"
uniform sampler2D weathered_texture : filter_linear_mipmap, repeat_disable;
uniform vec2 left_lamp = vec2(.08,.88);
uniform vec2 right_lamp = vec2(.91,.16);
uniform sampler2D lantern_texture : filter_linear_mipmap, repeat_disable;
uniform sampler2D plate_texture : filter_linear_mipmap, repeat_disable;
uniform vec2 environment_origin = vec2(0.0);
uniform vec2 environment_size = vec2(1920.0,1080.0);
uniform vec2 screen_size = vec2(1920.0,1080.0);
uniform vec2 screen_center;
uniform float display_density = 1.0;
uniform vec2 camera_center;
uniform float camera_zoom = 1.0;
uniform float plane_pitch = .64;
uniform float plane_depth = .00065;
uniform float candle_phase = 0.0;
uniform float candle_motion = 1.0;
vec3 preserve_painted_color(vec3 c) {
    vec3 linear_target = pow((c + vec3(0.055)) / 1.055, vec3(2.4));
    if (!OUTPUT_IS_SRGB) return mix(c / 12.92, linear_target, step(vec3(0.04045), c));
    vec3 x = max(c, vec3(0.03));
    for (int i=0;i<6;i++) {
        vec3 value = x*(x*(x*0.305306011+0.682171111)+0.012522878);
        vec3 derivative = x*(x*0.915918033+1.364342222)+0.012522878;
        x -= (value-linear_target)/derivative;
    }
    return x;
}

float flame_energy(float phase) {
    return candle_energy(phase,candle_motion);
}
vec3 floor_light(vec2 uv, vec2 source, float phase) {
    vec2 d = (uv-source)/vec2(.24,.31);
    vec2 near_d = (uv-source)/vec2(.066,.105);
    float pool = exp(-dot(d,d)*1.5);
    float near_pool = exp(-dot(near_d,near_d)*1.7);
    return (vec3(.19,.089,.026)*pool + vec3(.17,.079,.020)*near_pool)*flame_energy(phase);
}
vec3 upright_lantern(vec3 background, vec2 source, vec2 pixel, float phase) {
    vec2 base_world = environment_origin+environment_size*source;
    vec2 p = (base_world-camera_center)*camera_zoom;
    float perspective = 1.0/(1.0-plane_depth*p.y);
    vec2 base = screen_center+vec2(p.x,p.y*plane_pitch)*perspective*display_density;
    // Uniform scale keeps the complete upright prop's aspect, unlike applying the mat homography.
    vec2 size = vec2(216.0,300.0)*camera_zoom*perspective*display_density;
    vec2 uv = (pixel-base)/size+vec2(.5,.94);
    vec2 haze = (uv-vec2(.5,.60))/vec2(.45,.35);
    background += vec3(.14,.065,.018)*exp(-dot(haze,haze)*2.0)*flame_energy(phase);
    // Sample continuously: implicit mip derivatives inside a UV branch can
    // leave a rectangular seam. Alpha handles the cutout; bloom has no hard crop.
    // Preserve the existing accurate alpha silhouette. The generated patina
    // plate contains a neutral checker background, so neutral samples retain
    // the original material; no checker pixels enter the rendered cutout.
    vec2 sample_uv = uv;
    float flame = exp(-dot((uv-vec2(.5,.598))/vec2(.035,.065),(uv-vec2(.5,.598))/vec2(.035,.065))*2.0);
    sample_uv.x += sin(phase*3.2)*.0035*flame*candle_motion;
    sample_uv.y += sin(phase*2.7+.8)*.0025*flame*candle_motion;
    vec4 prop = texture(lantern_texture,sample_uv);
    vec3 worn = texture(weathered_texture,sample_uv).rgb;
    float chroma = max(worn.r,max(worn.g,worn.b))-min(worn.r,min(worn.g,worn.b));
    prop.rgb = mix(prop.rgb,worn,smoothstep(.06,.19,chroma)*.92);
    float emissive = smoothstep(.38,.85,prop.r)*smoothstep(.0,.2,prop.r-prop.b);
    prop.rgb *= 1.0 + emissive*(flame_energy(phase)-1.0)*.72;
    prop.rgb += vec3(.080,.042,.010)*emissive;
    float sprite_coverage = smoothstep(0.0,.006,min(min(uv.x,1.0-uv.x),min(uv.y,1.0-uv.y)));
    background = mix(background,prop.rgb,smoothstep(.018,.98,prop.a)*sprite_coverage);
    // A little optical bloom escapes the bright paper, without blurring the metal silhouette.
    vec2 core = (uv-vec2(.5,.63))/vec2(.16,.18);
    background += vec3(.043,.025,.008)*exp(-dot(core,core)*2.0)*flame_energy(phase);
    return background;
}
void fragment() {
    vec2 screen = (SCREEN_UV*screen_size-screen_center)/display_density;
    float y = screen.y/(plane_pitch+plane_depth*screen.y);
    vec2 plane = vec2(screen.x*(1.0-plane_depth*y),y);
    vec2 world = camera_center+plane/camera_zoom;
    vec2 uv = (world-environment_origin)/environment_size;
    float edge = min(min(uv.x,1.0-uv.x),min(uv.y,1.0-uv.y));
    float coverage = smoothstep(0.0,.022,edge);
    vec3 painted = mix(vec3(.012,.013,.015),texture(plate_texture,clamp(uv,vec2(0.0),vec2(1.0))).rgb,coverage);
    vec2 left_base = left_lamp;
    vec2 right_base = right_lamp;
    vec2 left_shadow = (world-(environment_origin+environment_size*left_base))/vec2(83.0,45.0);
    vec2 right_shadow = (world-(environment_origin+environment_size*right_base))/vec2(83.0,45.0);
    float shadow = max(exp(-dot(left_shadow,left_shadow)*1.9),exp(-dot(right_shadow,right_shadow)*1.9));
    vec3 light = floor_light(uv,left_base,candle_phase)+floor_light(uv,right_base,candle_phase+1.8);
    painted += light*coverage;
    painted *= 1.0-.72*shadow;
    painted = upright_lantern(painted,right_base,SCREEN_UV*screen_size,candle_phase+1.8);
    painted = upright_lantern(painted,left_base,SCREEN_UV*screen_size,candle_phase);
    ALBEDO = preserve_painted_color(clamp(painted,vec3(0.0),vec3(1.0)));
}
