shader_type spatial;
uniform float phase=0.0;
uniform float motion=0.0;
uniform float light_energy=1.0;
varying vec3 coal_position;
void vertex() { coal_position=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
    vec3 p=coal_position*460.0;
    float vein=abs(sin(p.x+sin(p.y*1.5))*sin(p.z*.8+cos(p.x*.55)));
    float fissure=1.0-smoothstep(.035,.14,vein);
    float patch=.5+.5*sin(p.x*.3+p.y*.2+sin(p.z*.7));
    float heat=(.14+.48*patch)*fissure;
    ALBEDO=mix(vec3(.013,.011,.010),vec3(.10,.08,.064),patch);
    EMISSION=mix(vec3(.7,.028,.002),vec3(1.5,.22,.012),heat)*heat*(.83+.17*light_energy);
    ROUGHNESS=.96;
    SPECULAR=.12;
}
