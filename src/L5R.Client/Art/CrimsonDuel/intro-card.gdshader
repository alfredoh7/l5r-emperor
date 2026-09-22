shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_prepass_alpha;
uniform sampler2D face : source_color, filter_linear;
uniform float softness = 0.0;
void fragment() {
    vec2 d = vec2(0.004,0.003) * softness;
    vec4 c = texture(face, UV)*4.0;
    c += texture(face,UV+d)+texture(face,UV-d);
    c += texture(face,UV+vec2(d.x,-d.y))+texture(face,UV+vec2(-d.x,d.y));
    c += texture(face,UV+vec2(d.x*2.0,0.0))+texture(face,UV-vec2(d.x*2.0,0.0));
    c += texture(face,UV+vec2(0.0,d.y*2.0))+texture(face,UV-vec2(0.0,d.y*2.0));
    c /= 12.0;
    ALBEDO=c.rgb*mix(1.0,0.84,softness); ALPHA=c.a;
}
