shader_type spatial;
render_mode unshaded,cull_disabled,world_vertex_coords,fog_disabled,depth_prepass_alpha;
uniform float wind_time=0.0;
uniform float wind_strength=1.0;
uniform bool use_art=false;
uniform sampler2D foliage_art : filter_linear_mipmap_anisotropic,repeat_disable;
varying float h;
varying float shade;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1)),f.x),f.y);}
vec3 painted(vec3 c){
 vec3 target=pow((c+vec3(.055))/1.055,vec3(2.4));
 if(!OUTPUT_IS_SRGB)return mix(c/12.92,target,step(vec3(.04045),c));
 vec3 x=max(c,vec3(.03));
 for(int i=0;i<6;i++){vec3 value=x*(x*(x*.305306011+.682171111)+.012522878);vec3 derivative=x*(x*.915918033+1.364342222)+.012522878;x-=(value-target)/derivative;}
 return x;
}
void vertex(){
 vec3 root=MODEL_MATRIX[3].xyz;float scale=length(MODEL_MATRIX[1].xyz);
 h=1.0-UV.y;
 float phase=INSTANCE_CUSTOM.r*6.283;
 float gust=noise(root.xz*.38-vec2(wind_time*.27,wind_time*.11));
 float swell=sin(root.x*.74+root.z*.4-wind_time*1.3);
 float stiffness=mix(.70,1.15,INSTANCE_CUSTOM.g);
 float angle=(.12+wind_strength*(.23*gust+.18*swell+.11*sin(wind_time*1.8+phase)))*stiffness*h;
 float flutter=sin(wind_time*4.8+phase+root.x)*.016*wind_strength*h*h*h;
 VERTEX.x+=scale*(sin(angle)*h+flutter);
 VERTEX.z+=scale*sin(angle*.67)*h*.26;
 VERTEX.y-=scale*h*(1.0-cos(angle));
 shade=mix(.76,1.06,INSTANCE_CUSTOM.b);
}
void fragment(){
 vec4 art=texture(foliage_art,UV);
 vec3 c=use_art?art.rgb*vec3(.95,.78,.58):mix(vec3(.25,.10,.03),vec3(.86,.57,.26),pow(h,.75));
 c*=shade;
 // Golden rim varies with blade depth/height, without an expensive dynamic shadow pass.
 c+=vec3(.06,.025,.004)*smoothstep(.65,1.0,h);
 ALBEDO=painted(clamp(c,vec3(.008),vec3(1.0)));
 ALPHA=use_art?art.a:1.0;
}
