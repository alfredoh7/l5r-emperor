shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled;
#include "res://Art/candlelight.gdshaderinc"
#include "res://Art/MatFidelity/fabric-detail.gdshaderinc"
// Physical cloth: the approved navy-table colour and candle treatment,
// sampled on the native plane. Additional weave remains review-only.
uniform float fabric_detail_strength = 0.0;
uniform vec2 left_lamp = vec2(.08,.88);
uniform vec2 right_lamp = vec2(.91,.16);
uniform sampler2D plate_texture : filter_linear_mipmap, repeat_disable;
uniform vec2 environment_origin = vec2(0.0);
uniform vec2 environment_size = vec2(1920.0,1080.0);
uniform float candle_phase = 0.0;
uniform float candle_motion = 1.0;
uniform bool table_lamps = false;
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
void fragment() {
    vec2 uv = UV;
    vec2 world = environment_origin + UV * environment_size;
    float edge = min(min(uv.x,1.0-uv.x),min(uv.y,1.0-uv.y));
    float coverage = smoothstep(0.0,.022,edge);
    vec3 print = texture(plate_texture,clamp(uv,vec2(0.0),vec2(1.0))).rgb;
    if (fabric_detail_strength > 0.0) {
        float interior = smoothstep(.028,.034,edge);
        print = fabric_detail(print,uv,fabric_detail_strength*interior);
    }
    vec3 painted = mix(vec3(.012,.013,.015),print,coverage);
    vec2 left_base = left_lamp;
    vec2 right_base = right_lamp;
    vec2 left_shadow = (world-(environment_origin+environment_size*left_base))/vec2(83.0,45.0);
    vec2 right_shadow = (world-(environment_origin+environment_size*right_base))/vec2(83.0,45.0);
    float shadow = max(exp(-dot(left_shadow,left_shadow)*1.9),exp(-dot(right_shadow,right_shadow)*1.9));
    vec3 light = floor_light(uv,left_base,candle_phase)+floor_light(uv,right_base,candle_phase+1.8);
    painted += table_lamps ? light*coverage : vec3(.014,.015,.018)*coverage;
    painted *= table_lamps ? 1.0-.72*shadow : 1.0;
    ALBEDO = preserve_painted_color(clamp(painted,vec3(0.0),vec3(1.0)));
}
