shader_type canvas_item;
uniform float elapsed = 0.0;
uniform bool reduced_motion = false;
float ease(float a,float b,float t) { return smoothstep(a,b,t); }
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
 vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
 return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
 vec4 art=texture(TEXTURE,UV);
 if(reduced_motion) art.a *= ease(.08,.22,elapsed)*(1.0-ease(2.4,2.6,elapsed));
 else {
  vec2 p=UV*vec2(249,83);
  float n=noise(p*.09)*.6+noise(p*.23)*.27+noise(p*.55)*.13;
  float field=abs(UV.x-.5)*.92+abs(UV.y-.5)*.3+n*.25;
  float spread=min(ease(.03,.95,elapsed),1.0-ease(2.65,3.5,elapsed));
  art.a *= smoothstep(0.0,.055,spread*.98-field)*ease(.03,.35,elapsed);
 }
 COLOR=art;
}
