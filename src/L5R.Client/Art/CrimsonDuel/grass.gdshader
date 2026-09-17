shader_type spatial;
render_mode unshaded, cull_disabled, world_vertex_coords, fog_disabled, depth_prepass_alpha;
uniform float atmosphere_time = 0.0;
uniform float motion_amount = 1.0;
uniform bool use_foliage_art = false;
uniform sampler2D foliage_art : filter_linear_mipmap_anisotropic, repeat_disable;
uniform vec3 root_color = vec3(.13,.052,.016);
uniform vec3 tip_color = vec3(.78,.43,.14);
varying float height_mask;
varying float variant;
varying vec3 blade_normal;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float field_noise(vec2 p) {
 vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);
 return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1)),f.x),f.y);
}
vec3 painted_color(vec3 c) {
 vec3 target=pow((c+vec3(.055))/1.055,vec3(2.4));
 if(!OUTPUT_IS_SRGB) return mix(c/12.92,target,step(vec3(.04045),c));
 vec3 x=max(c,vec3(.03));
 for(int i=0;i<6;i++) {
  vec3 value=x*(x*(x*.305306011+.682171111)+.012522878);
  vec3 derivative=x*(x*.915918033+1.364342222)+.012522878;
  x-=(value-target)/derivative;
 }
 return x;
}
void vertex() {
 vec3 root=MODEL_MATRIX[3].xyz;
 // glTF flips texture V. Derive bending weight from geometry, never texture orientation.
 height_mask=clamp((VERTEX.y-root.y)/max(length(MODEL_MATRIX[1].xyz),.001),0.0,1.12);
 variant=INSTANCE_CUSTOM.r;
 float t=atmosphere_time*motion_amount;
 vec2 p=root.xz;
 float stiffness=mix(.65,1.10,INSTANCE_CUSTOM.g);
 float broad=field_noise(p*.43-vec2(t*.24,t*.075));
 float gust=sin(p.x*.55+p.y*.24-t*1.10)*.5+.5;
 float sway=sin(t*1.65+p.x*.37+p.y*.56+variant*1.2);
 float secondary=sin(t*3.7+p.x*1.9+variant*6.283)*.013;
 float blade_scale=max(length(MODEL_MATRIX[1].xyz),.001);
 float angle=(.30+.28*broad+.10*gust+.08*sway)*stiffness*height_mask;
 float world_height=height_mask*blade_scale;
 vec2 wind=normalize(vec2(1.0,.30))*world_height*sin(angle);
 // Roots remain fixed, broad wind travels coherently, tip flutter is independent.
 VERTEX.xz+=wind+vec2(secondary,secondary*.32)*blade_scale*pow(height_mask,3.0);
 // Bend along an arc: blade length is retained as its tip leans into the wind.
 VERTEX.y-=world_height*(1.0-cos(angle));
 blade_normal=normalize(NORMAL+vec3(-wind.x*.8,.06,-wind.y*.8));
}
void fragment() {
 float h=clamp(height_mask,0.0,1.0);
 vec3 c=mix(root_color,tip_color,pow(h,.78));
 float facing=abs(dot(normalize(blade_normal),normalize(vec3(.35,.55,-.7))));
 float shade=mix(.63,1.12,facing)*mix(.82,1.10,variant);
 c*=shade;
 c+=vec3(.11,.048,.008)*smoothstep(.78,1.05,height_mask);
 vec4 art=texture(foliage_art,UV);
 if(use_foliage_art) c=art.rgb*vec3(.74,.64,.49)*mix(.82,1.0,variant);
 ALBEDO=painted_color(clamp(c,vec3(.01),vec3(1)));
 ALPHA=use_foliage_art?art.a:1.0;
}
