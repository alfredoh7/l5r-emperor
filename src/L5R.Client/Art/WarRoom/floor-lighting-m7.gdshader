shader_type spatial;
render_mode ambient_light_disabled;
// Cycles direct/bounce irradiance, separate from the three Meshy wood variants.
uniform sampler2D wood_colour : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D wood_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D wood_roughness : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D irradiance : filter_linear_mipmap, repeat_disable;
uniform sampler2D fine_grain : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float exposure = 1.0;
uniform vec4 practical_sources[11];
uniform float practical_energy[11];
varying vec3 world_position;
varying vec3 wood_tint;
void vertex() { world_position = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz; wood_tint=COLOR.rgb; }
void fragment() {
    vec2 light_uv = vec2((world_position.x + 2.5) / 5.0, (world_position.z + 2.25) / 3.8);
    vec3 grain = texture(wood_colour,UV).rgb * wood_tint * vec3(.78,.72,.62);
    // Meshy supplies the board finish; a finer existing scanned timber layer
    // keeps fibres readable when the camera is close. Avoid scan plank seams.
    float row=floor((world_position.z+3.1)/.081);
    vec2 detail_uv=vec2(world_position.x*.9+row*.173,
        (mod(row,12.0)+.2+fract((world_position.z+3.1)/.081)*.6)/13.0);
    float fibres=clamp(dot(texture(fine_grain,detail_uv).rgb,vec3(.333))*5.0,.45,1.6);
    grain*=mix(.72,1.22,fibres*.625);
    float inside = smoothstep(0.0,.06,light_uv.x)*smoothstep(0.0,.06,1.0-light_uv.x)*smoothstep(0.0,.06,light_uv.y)*smoothstep(0.0,.06,1.0-light_uv.y);
    vec3 illumination = mix(vec3(.025,.028,.035),texture(irradiance,clamp(light_uv,vec2(0.0),vec2(1.0))).rgb,inside);
    // Keep native light specular/normal response; diffuse energy is baked once.
    ALBEDO=vec3(0.0);
    float contact=mix(1.0,texture(irradiance,clamp(light_uv,vec2(0.0),vec2(1.0))).a,inside);
    // Modulate only nearby warm illumination. The accepted baked baseline,
    // ambient fill and contact shadows never pulse as one full-room layer.
    float weight_sum=0.0;
    float local_delta=0.0;
    for(int i=0;i<11;i++) {
        vec2 d=(world_position.xz-practical_sources[i].xy)/max(practical_sources[i].z,.01);
        float weight=exp(-dot(d,d)*4.0);
        weight_sum+=weight;
        local_delta+=weight*(practical_energy[i]-1.0);
    }
    float warmth=1.0+.60*local_delta/max(1.0,weight_sum);
    EMISSION=grain*(vec3(.34,.30,.28)*pow(contact,1.45)+illumination*2.2*warmth)*exposure;
    NORMAL_MAP=texture(wood_normal,UV).rgb;
    NORMAL_MAP_DEPTH=.26;
    ROUGHNESS=mix(.28,.58,texture(wood_roughness,UV).g);
    SPECULAR=.30;
}
