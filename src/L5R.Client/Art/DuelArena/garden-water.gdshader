shader_type spatial;
render_mode cull_disabled, depth_prepass_alpha;
uniform float clock=0.0;
varying vec3 world_position;
void vertex() { world_position=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
    vec2 p=world_position.xz;
    float a=sin(p.x*2.3+p.y*6.0+clock*.38+sin(p.x*3.7));
    float b=sin(p.x*4.2-p.y*9.0-clock*.27+sin(p.y*2.1));
    NORMAL_MAP=normalize(vec3(a*.012,b*.009,1.0))*.5+.5;
    ALBEDO=vec3(.027,.040,.061);
    METALLIC=.15;ROUGHNESS=.30;
    float streak=pow(max(0.0,a*b),7.0)*exp(-pow((p.x-4.5)*.45,2.0));
    EMISSION=vec3(.12,.18,.28)*streak*.16;
    // Disappear inside the same mist band as the bank, avoiding a visible far cap.
    ALPHA=1.0-smoothstep(13.8,18.0,-world_position.z);
}
