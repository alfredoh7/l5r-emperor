shader_type spatial;
render_mode cull_disabled;
varying vec3 position_world;
float hash(vec3 p) { return fract(sin(dot(p,vec3(127.1,311.7,74.7)))*43758.5453); }
float noise3(vec3 p) {
    vec3 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x),mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x),f.y),
      mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x),mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x),f.y),f.z);
}
void vertex() { position_world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
    float wear=noise3(position_world*7.0)*.60+noise3(position_world*35.0)*.25+noise3(position_world*180.0)*.15;
    ALBEDO=mix(vec3(.045,.029,.019),vec3(.19,.125,.075),wear);
    ROUGHNESS=.98;
    SPECULAR=.12;
}
