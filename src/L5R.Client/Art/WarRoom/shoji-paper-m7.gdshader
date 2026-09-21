shader_type spatial;
// Subtle stationary fibres, lit by the room's actual practicals.
varying vec3 position;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void vertex() { position=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
    vec2 p=position.zy;
    float cloud=noise(p*35.0);
    float fibre=noise(p*vec2(650.0,1200.0));
    ALBEDO=vec3(.38,.325,.24)*(.91+.12*cloud+.035*fibre);
    ROUGHNESS=1.0;SPECULAR=.1;
}
