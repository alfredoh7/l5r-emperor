shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;
uniform float clock = 0.0;
uniform float offset = 0.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p), f=fract(p);f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
    vec2 p=UV*vec2(11.0,3.0)+vec2(clock*.018+offset,offset);
    float n=noise(p)*.62+noise(p*2.13)*.26+noise(p*4.57)*.12;
    float edge=pow(max(0.0,sin(UV.y*3.14159265)),2.0);
    edge*=smoothstep(0.0,.12,UV.x)*smoothstep(0.0,.12,1.0-UV.x);
    ALBEDO=vec3(.32,.40,.51);
    ALPHA=edge*smoothstep(.34,.78,n)*.17;
}
