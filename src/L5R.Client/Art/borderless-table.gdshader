shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled;
uniform sampler2D cloth_texture : filter_linear_mipmap, repeat_enable;
uniform vec2 screen_size = vec2(1920.0,1080.0);
uniform vec2 table_origin = vec2(0.0);
uniform float table_scale = 1.0;
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
void fragment() {
    vec2 world = (SCREEN_UV*screen_size-table_origin)/table_scale;
    // Mirror alternate tiles to ensure continuous edges in both axes. The source
    // remains intact; native mipmaps preserve the fine weave at overview scale.
    vec2 uv = 1.0-abs(mod(world/1100.0,2.0)-1.0);
    vec3 cloth = texture(cloth_texture, uv).rgb;
    float flicker = 0.50*sin(candle_phase*2.1)+0.29*sin(candle_phase*3.7+1.2)+0.21*sin(candle_phase*7.6+0.5);
    vec2 d = (world-vec2(-140.0,390.0))/vec2(750.0,850.0);
    float pool = exp(-dot(d,d)*1.4);
    // The flame source sits beyond the playing area. Its pool is anchored to
    // cloth coordinates, so light and cards travel together while panning.
    cloth *= 0.82;
    cloth += vec3(0.060,0.031,0.006)*pool*(0.7+flicker*0.3*candle_motion);
    ALBEDO = preserve_painted_color(clamp(cloth,vec3(0.0),vec3(1.0)));
}
